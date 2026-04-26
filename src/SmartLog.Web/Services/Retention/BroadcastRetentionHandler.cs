using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Retention;

public class BroadcastRetentionHandler : IEntityRetentionHandler
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly SmsStatus[] ActiveStatuses = [SmsStatus.Pending, SmsStatus.Processing];
    private static readonly SmsStatus[] TerminalStatuses = [SmsStatus.Sent, SmsStatus.Failed, SmsStatus.Cancelled];

    private readonly ApplicationDbContext _db;
    private readonly ILogger<BroadcastRetentionHandler> _logger;
    private readonly IArchiveService? _archiveService;

    public string EntityName => "Broadcast";

    public BroadcastRetentionHandler(
        ApplicationDbContext db,
        ILogger<BroadcastRetentionHandler> logger,
        IArchiveService? archiveService = null)
    {
        _db = db;
        _logger = logger;
        _archiveService = archiveService;
    }

    public async Task<RetentionPreview> PreviewAsync(CancellationToken ct = default)
    {
        var policy = await _db.RetentionPolicies
            .FirstOrDefaultAsync(p => p.EntityName == EntityName, ct);

        if (policy is null || !policy.Enabled)
            return new RetentionPreview(0, null, null);

        var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);

        // Broadcasts eligible: old and no active queue children
        var activeChildBroadcastIds = await _db.SmsQueues
            .Where(q => q.BroadcastId != null && ActiveStatuses.Contains(q.Status))
            .Select(q => q.BroadcastId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var eligible = _db.Broadcasts.Where(b =>
            b.CreatedAt < cutoff && !activeChildBroadcastIds.Contains(b.Id));

        var count = await eligible.CountAsync(ct);
        var oldest = count > 0 ? await eligible.MinAsync(b => (DateTime?)b.CreatedAt, ct) : null;
        var newest = count > 0 ? await eligible.MaxAsync(b => (DateTime?)b.CreatedAt, ct) : null;
        return new RetentionPreview(count, oldest, newest);
    }

    public async Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct = default, string runMode = "Manual")
    {
        if (!await _lock.WaitAsync(0))
            return RetentionResult.Skipped();

        try
        {
            var policy = await _db.RetentionPolicies
                .FirstOrDefaultAsync(p => p.EntityName == EntityName, ct);

            if (policy is null || !policy.Enabled)
                return RetentionResult.Disabled();

            var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
            var startedAt = DateTime.UtcNow;
            var totalAffected = 0;
            var runStatus = "Success";
            string? errorMessage = null;

            try
            {
                if (dryRun)
                {
                    var activeChildIds = await _db.SmsQueues
                        .Where(q => q.BroadcastId != null && ActiveStatuses.Contains(q.Status))
                        .Select(q => q.BroadcastId!.Value)
                        .Distinct()
                        .ToListAsync(ct);

                    totalAffected = await _db.Broadcasts
                        .CountAsync(b => b.CreatedAt < cutoff && !activeChildIds.Contains(b.Id), ct);
                }
                else
                {
                    int batchRows;
                    do
                    {
                        // Collect broadcast IDs that have active queue children — re-check each iteration
                        var activeChildIds = await _db.SmsQueues
                            .Where(q => q.BroadcastId != null && ActiveStatuses.Contains(q.Status))
                            .Select(q => q.BroadcastId!.Value)
                            .Distinct()
                            .ToListAsync(ct);

                        var batch = await _db.Broadcasts
                            .Where(b => b.CreatedAt < cutoff && !activeChildIds.Contains(b.Id))
                            .Take(100)
                            .ToListAsync(ct);

                        batchRows = batch.Count;
                        if (batchRows > 0)
                        {
                            if (policy.ArchiveEnabled)
                            {
                                if (_archiveService is null)
                                {
                                    _logger.LogWarning(
                                        "ArchiveEnabled is set for {Entity} but IArchiveService is not registered. Skipping deletion.",
                                        EntityName);
                                    runStatus = "Partial";
                                    errorMessage = "Archive service not configured — no rows deleted";
                                    break;
                                }

                                var archiveResult = await _archiveService.ArchiveBatchAsync(EntityName, batch, ct);
                                if (!archiveResult.Success)
                                {
                                    _logger.LogError("{Entity} archive failed: {Error}", EntityName, archiveResult.ErrorMessage);
                                    runStatus = totalAffected > 0 ? "Partial" : "Failed";
                                    errorMessage = $"Archive failed: {archiveResult.ErrorMessage}";
                                    break;
                                }
                            }

                            var batchIds = batch.Select(b => b.Id).ToList();

                            // Delete terminal child queue rows first
                            var childQueue = await _db.SmsQueues
                                .Where(q => q.BroadcastId != null
                                         && batchIds.Contains(q.BroadcastId!.Value)
                                         && TerminalStatuses.Contains(q.Status))
                                .ToListAsync(ct);
                            if (childQueue.Count > 0)
                                _db.SmsQueues.RemoveRange(childQueue);

                            // Delete the broadcasts
                            _db.Broadcasts.RemoveRange(batch);
                            await _db.SaveChangesAsync(ct);
                            totalAffected += batchRows;

                            if (!ct.IsCancellationRequested)
                                await Task.Delay(50, ct);
                        }
                    } while (batchRows >= 100 && !ct.IsCancellationRequested);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "{Entity} retention failed after {Rows} rows", EntityName, totalAffected);
                runStatus = totalAffected > 0 ? "Partial" : "Failed";
                errorMessage = ex.Message;
            }

            var completedAt = DateTime.UtcNow;
            _db.RetentionRuns.Add(new RetentionRun
            {
                EntityName = EntityName,
                RunMode = dryRun ? "DryRun" : runMode,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Status = runStatus,
                RowsAffected = totalAffected,
                DurationMs = (int)(completedAt - startedAt).TotalMilliseconds,
                ErrorMessage = errorMessage,
                TriggeredBy = "Manual"
            });

            if (!dryRun && runStatus == "Success")
            {
                policy.LastRunAt = completedAt;
                policy.LastRowsAffected = totalAffected;
            }

            await _db.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation(
                "{Entity} retention {Mode}: affected {Rows} rows (cutoff {Cutoff:yyyy-MM-dd})",
                EntityName, dryRun ? "dry-run" : "purge", totalAffected, cutoff);

            return runStatus switch
            {
                "Partial" => RetentionResult.Partial(totalAffected, errorMessage!),
                "Failed" => new RetentionResult("Failed", 0, ErrorMessage: errorMessage),
                _ => RetentionResult.Success(totalAffected)
            };
        }
        finally
        {
            _lock.Release();
        }
    }
}
