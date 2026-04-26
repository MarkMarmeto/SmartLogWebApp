using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class RetentionServiceTests
{
    // ─── helpers ──────────────────────────────────────────────────────────────

    private static (RetentionService service, ApplicationDbContext db) Build(
        params IEntityRetentionHandler[] handlers)
    {
        var db = TestDbContextFactory.Create();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<ApplicationDbContext>(db);
        foreach (var h in handlers)
            services.AddSingleton<IEntityRetentionHandler>(h);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = new Mock<ILogger<RetentionService>>().Object;
        var svc = new RetentionService(scopeFactory, logger);
        return (svc, db);
    }

    // ─── AC2: handler iteration ────────────────────────────────────────────────

    [Fact]
    public async Task RunAllHandlers_CallsAllRegisteredHandlers()
    {
        var h1 = new SpyHandler("Entity1");
        var h2 = new SpyHandler("Entity2");
        var (svc, _) = Build(h1, h2);

        await svc.RunAllHandlersAsync("Manual", dryRun: false);

        Assert.True(h1.Called);
        Assert.True(h2.Called);
    }

    [Fact]
    public async Task RunAllHandlers_DryRun_PassesDryRunToHandler()
    {
        var h = new SpyHandler("Entity1");
        var (svc, _) = Build(h);

        await svc.RunAllHandlersAsync("DryRun", dryRun: true);

        Assert.True(h.DryRun);
        Assert.Equal("DryRun", h.LastRunMode);
    }

    // ─── AC3: daily idempotency guard ─────────────────────────────────────────

    [Fact]
    public async Task ScheduledRun_SkipsEntityWithTodaysSuccessRun()
    {
        var h = new SpyHandler("Entity1");
        var (svc, db) = Build(h);

        db.RetentionRuns.Add(new RetentionRun
        {
            EntityName = "Entity1",
            RunMode = "Scheduled",
            Status = "Success",
            StartedAt = DateTime.UtcNow.Date.AddHours(1),
            RowsAffected = 5
        });
        await db.SaveChangesAsync();

        await svc.RunAllHandlersAsync("Scheduled", dryRun: false);

        Assert.False(h.Called); // guard prevented execution
    }

    [Fact]
    public async Task ManualRun_IgnoresDailyGuard()
    {
        var h = new SpyHandler("Entity1");
        var (svc, db) = Build(h);

        db.RetentionRuns.Add(new RetentionRun
        {
            EntityName = "Entity1",
            RunMode = "Scheduled",
            Status = "Success",
            StartedAt = DateTime.UtcNow.Date.AddHours(1),
            RowsAffected = 5
        });
        await db.SaveChangesAsync();

        await svc.RunAllHandlersAsync("Manual", dryRun: false);

        Assert.True(h.Called); // manual bypasses guard
    }

    [Fact]
    public async Task ScheduledRun_SkipsOnlyEntityWithGuard_OtherEntitiesStillRun()
    {
        var h1 = new SpyHandler("Entity1");
        var h2 = new SpyHandler("Entity2");
        var (svc, db) = Build(h1, h2);

        db.RetentionRuns.Add(new RetentionRun
        {
            EntityName = "Entity1",
            RunMode = "Scheduled",
            Status = "Success",
            StartedAt = DateTime.UtcNow.Date.AddHours(1),
            RowsAffected = 0
        });
        await db.SaveChangesAsync();

        await svc.RunAllHandlersAsync("Scheduled", dryRun: false);

        Assert.False(h1.Called); // guarded
        Assert.True(h2.Called);  // not guarded
    }

    // ─── AC7: error isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task ErrorIsolation_OneHandlerThrows_OthersStillRun()
    {
        var throwing = new ThrowingHandler("Failer");
        var spy = new SpyHandler("Succeeder");
        var (svc, _) = Build(throwing, spy);

        await svc.RunAllHandlersAsync("Manual", dryRun: false);

        Assert.True(spy.Called); // error in previous handler did not block this one
    }

    // ─── ComputeDelayUntilNext ─────────────────────────────────────────────────

    [Fact]
    public void ComputeDelay_FutureTimeToday_ReturnsPositiveDelay()
    {
        var nowUtc = DateTime.UtcNow;
        // A run time 1 hour from now
        var runTime = TimeOnly.FromDateTime(nowUtc.AddHours(1));
        var delay = RetentionService.ComputeDelayUntilNext(runTime);
        Assert.InRange(delay.TotalMinutes, 59, 61);
    }

    [Fact]
    public void ComputeDelay_PastTimeToday_SchedulesTomorrow()
    {
        var nowUtc = DateTime.UtcNow;
        // A run time 1 hour ago
        var runTime = TimeOnly.FromDateTime(nowUtc.AddHours(-1));
        var delay = RetentionService.ComputeDelayUntilNext(runTime);
        Assert.InRange(delay.TotalHours, 22.9, 23.1);
    }

    // ─── fake handlers ────────────────────────────────────────────────────────

    private class SpyHandler : IEntityRetentionHandler
    {
        public string EntityName { get; }
        public bool Called { get; private set; }
        public bool DryRun { get; private set; }
        public string? LastRunMode { get; private set; }

        public SpyHandler(string entityName) => EntityName = entityName;

        public Task<RetentionPreview> PreviewAsync(CancellationToken ct = default)
            => Task.FromResult(new RetentionPreview(0, null, null));

        public Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct = default, string runMode = "Manual")
        {
            Called = true;
            DryRun = dryRun;
            LastRunMode = runMode;
            return Task.FromResult(RetentionResult.Success(0));
        }
    }

    private class ThrowingHandler : IEntityRetentionHandler
    {
        public string EntityName { get; }
        public ThrowingHandler(string entityName) => EntityName = entityName;

        public Task<RetentionPreview> PreviewAsync(CancellationToken ct = default)
            => Task.FromResult(new RetentionPreview(0, null, null));

        public Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct = default, string runMode = "Manual")
            => throw new InvalidOperationException("Simulated handler failure");
    }
}
