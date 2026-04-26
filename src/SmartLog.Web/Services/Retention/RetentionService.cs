using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartLog.Web.Data;

namespace SmartLog.Web.Services.Retention;

public class RetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(IServiceScopeFactory scopeFactory, ILogger<RetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var runTime = await GetRunTimeAsync(stoppingToken);
            var delay = ComputeDelayUntilNext(runTime);

            _logger.LogInformation(
                "RetentionService next scheduled run in {Delay:hh\\:mm\\:ss} at {RunTime:HH:mm} UTC",
                delay, DateTime.UtcNow.Add(delay));

            await Task.Delay(delay, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
                await RunAllHandlersAsync("Scheduled", dryRun: false, stoppingToken);
        }
    }

    public async Task RunAllHandlersAsync(string runMode, bool dryRun, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEntityRetentionHandler>().ToList();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var handler in handlers)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                if (runMode == "Scheduled" && !dryRun)
                {
                    var today = DateTime.UtcNow.Date;
                    var alreadyRan = await db.RetentionRuns.AnyAsync(r =>
                        r.EntityName == handler.EntityName &&
                        r.StartedAt >= today &&
                        r.Status == "Success" &&
                        r.RunMode == "Scheduled", ct);

                    if (alreadyRan)
                    {
                        _logger.LogDebug(
                            "RetentionService: {Entity} already has a successful scheduled run today, skipping",
                            handler.EntityName);
                        continue;
                    }
                }

                await handler.ExecuteAsync(dryRun, ct, runMode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RetentionService: {Entity} handler threw unhandled exception", handler.EntityName);
            }
        }
    }

    private async Task<TimeOnly> GetRunTimeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setting = await db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "Retention:RunTime", ct);
            if (setting?.Value is not null && TimeOnly.TryParse(setting.Value, out var parsed))
                return parsed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RetentionService: failed to read Retention:RunTime, defaulting to 02:00");
        }
        return new TimeOnly(2, 0);
    }

    internal static TimeSpan ComputeDelayUntilNext(TimeOnly runTime)
    {
        var now = DateTime.UtcNow;
        var todayRun = now.Date.Add(runTime.ToTimeSpan());
        if (todayRun <= now)
            todayRun = todayRun.AddDays(1);
        return todayRun - now;
    }
}
