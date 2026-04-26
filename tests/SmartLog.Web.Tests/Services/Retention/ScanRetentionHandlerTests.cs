using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class ScanRetentionHandlerTests
{
    private static ScanRetentionHandler CreateHandler(Data.ApplicationDbContext db) =>
        new(db, NullLogger<ScanRetentionHandler>.Instance);

    private static void SeedPolicy(Data.ApplicationDbContext db, int retentionDays = 30, bool enabled = true)
    {
        db.RetentionPolicies.Add(new RetentionPolicy
        {
            EntityName = "Scan",
            RetentionDays = retentionDays,
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedScan(Data.ApplicationDbContext db, DateTime scannedAt)
    {
        var deviceId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        db.Scans.Add(new Scan
        {
            DeviceId = deviceId,
            StudentId = studentId,
            QrPayload = "SMARTLOG:test:123:abc",
            ScannedAt = scannedAt,
            ReceivedAt = scannedAt,
            ScanType = "ENTRY",
            Status = "ACCEPTED"
        });
        db.SaveChanges();
    }

    // ─── AC2: Eligibility — ScannedAt cutoff ──────────────────────────────────

    [Fact]
    public async Task Execute_DeletesScansOlderThanCutoff()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var recent = DateTime.UtcNow.AddDays(-10);

        SeedScan(db, old);
        SeedScan(db, old);
        SeedScan(db, recent);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(1, db.Scans.Count());
    }

    // ─── AC4: Dry run ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DryRun_ReturnsCountWithoutDeleting()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedScan(db, old);
        SeedScan(db, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: true);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(2, db.Scans.Count()); // untouched
    }

    // ─── Disabled policy ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DisabledPolicy_ReturnsDisabled()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, enabled: false);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedScan(db, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
        Assert.Equal(1, db.Scans.Count());
    }

    // ─── Run logging ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesRetentionRun()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedScan(db, old);

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var run = db.RetentionRuns.Single(r => r.EntityName == "Scan");
        Assert.Equal("Manual", run.RunMode);
        Assert.Equal("Success", run.Status);
        Assert.Equal(1, run.RowsAffected);
        Assert.NotNull(run.CompletedAt);
    }

    // ─── Preview ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_ReturnsEligibleCountAndDateRange()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-90);
        SeedScan(db, old);
        SeedScan(db, old);
        SeedScan(db, DateTime.UtcNow.AddDays(-5)); // not eligible

        var handler = CreateHandler(db);
        var preview = await handler.PreviewAsync();

        Assert.Equal(2, preview.EligibleRows);
        Assert.NotNull(preview.OldestRow);
    }
}
