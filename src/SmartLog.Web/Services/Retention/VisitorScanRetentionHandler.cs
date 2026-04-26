using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Retention;

public class VisitorScanRetentionHandler : IEntityRetentionHandler
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<VisitorScanRetentionHandler> _logger;
    private readonly IArchiveService? _archiveService;

    public string EntityName => "VisitorScan";

    public VisitorScanRetentionHandler(
        ApplicationDbContext db,
        ILogger<VisitorScanRetentionHandler> logger,
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
        var eligible = _db.VisitorScans.Where(v => v.ScannedAt < cutoff);
        var count = await eligible.CountAsync(ct);
        var oldest = count > 0 ? await eligible.MinAsync(v => (DateTime?)v.ScannedAt, ct) : null;
        var newest = count > 0 ? await eligible.MaxAsync(v => (DateTime?)v.ScannedAt, ct) : null;
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
                    totalAffected = await _db.VisitorScans
                        .Where(v => v.ScannedAt < cutoff)
                        .CountAsync(ct);
                }
                else
                {
                    var archiveBlockedDelete = false;
                    int batchRows;
                    do
                    {
                        var batch = await _db.VisitorScans
                            .Where(v => v.ScannedAt < cutoff)
                            .Take(1000)
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
                                    archiveBlockedDelete = true;
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

                            _db.VisitorScans.RemoveRange(batch);
                            await _db.SaveChangesAsync(ct);
                            totalAffected += batchRows;
                            if (!ct.IsCancellationRequested)
                                await Task.Delay(50, ct);
                        }
                    } while (batchRows >= 1000 && !ct.IsCancellationRequested);

                    if (archiveBlockedDelete)
                    {
                        runStatus = "Partial";
                        errorMessage = "Archive service not configured — no rows deleted";
                    }
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
