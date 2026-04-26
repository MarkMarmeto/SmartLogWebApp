using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class AuditLogRetentionHandlerTests
{
    private static AuditLogRetentionHandler CreateHandler(Data.ApplicationDbContext db) =>
        new(db, NullLogger<AuditLogRetentionHandler>.Instance);

    private static void SeedPolicy(Data.ApplicationDbContext db, int retentionDays = 30, bool enabled = true)
    {
        db.RetentionPolicies.Add(new RetentionPolicy
        {
            EntityName = "AuditLog",
            RetentionDays = retentionDays,
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedLog(Data.ApplicationDbContext db, DateTime timestamp, bool legalHold = false)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = "TestAction",
            Timestamp = timestamp,
            LegalHold = legalHold
        });
        db.SaveChanges();
    }

    // ─── AC3: LegalHold rows are never deleted ────────────────────────────────

    [Fact]
    public async Task Execute_SkipsRowsWithLegalHoldTrue()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedLog(db, old, legalHold: false); // eligible
        SeedLog(db, old, legalHold: true);  // held — must survive

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(1, result.RowsAffected);

        var remaining = db.AuditLogs.ToList();
        Assert.Single(remaining);
        Assert.True(remaining[0].LegalHold);
    }

    [Fact]
    public async Task Execute_DeletesOldRowsWhereHoldIsFalse()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var recent = DateTime.UtcNow.AddDays(-10);

        SeedLog(db, old);
        SeedLog(db, old);
        SeedLog(db, recent);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(1, db.AuditLogs.Count());
    }

    // ─── Dry run ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DryRun_ReturnsCountWithoutDeleting()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedLog(db, old);
        SeedLog(db, old, legalHold: true); // held — not counted

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: true);

        Assert.Equal("Success", result.Status);
        Assert.Equal(1, result.RowsAffected); // only non-held
        Assert.Equal(2, db.AuditLogs.Count()); // nothing deleted
    }

    // ─── Disabled policy ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DisabledPolicy_ReturnsDisabled()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, enabled: false);

        SeedLog(db, DateTime.UtcNow.AddDays(-60));

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
    }

    // ─── Run logging ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesRetentionRun()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        SeedLog(db, DateTime.UtcNow.AddDays(-60));

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var run = db.RetentionRuns.Single(r => r.EntityName == "AuditLog");
        Assert.Equal("Manual", run.RunMode);
        Assert.Equal("Success", run.Status);
        Assert.Equal(1, run.RowsAffected);
    }
}
