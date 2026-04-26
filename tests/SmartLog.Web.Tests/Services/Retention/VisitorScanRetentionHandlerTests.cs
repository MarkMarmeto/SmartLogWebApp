using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class VisitorScanRetentionHandlerTests
{
    private static VisitorScanRetentionHandler CreateHandler(Data.ApplicationDbContext db) =>
        new(db, NullLogger<VisitorScanRetentionHandler>.Instance);

    private static void SeedPolicy(Data.ApplicationDbContext db, int retentionDays = 7, bool enabled = true)
    {
        db.RetentionPolicies.Add(new RetentionPolicy
        {
            EntityName = "VisitorScan",
            RetentionDays = retentionDays,
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedVisitorScan(Data.ApplicationDbContext db, DateTime scannedAt)
    {
        db.VisitorScans.Add(new VisitorScan
        {
            VisitorPassId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ScanType = "ENTRY",
            ScannedAt = scannedAt,
            ReceivedAt = scannedAt,
            Status = "ACCEPTED"
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Execute_DeletesOldVisitorScans()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 7);

        var old = DateTime.UtcNow.AddDays(-30);
        var recent = DateTime.UtcNow.AddDays(-2);

        SeedVisitorScan(db, old);
        SeedVisitorScan(db, old);
        SeedVisitorScan(db, recent);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(1, db.VisitorScans.Count());
    }

    [Fact]
    public async Task Execute_DryRun_ReturnsCountWithoutDeleting()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 7);

        var old = DateTime.UtcNow.AddDays(-30);
        SeedVisitorScan(db, old);
        SeedVisitorScan(db, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: true);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(2, db.VisitorScans.Count());
    }

    [Fact]
    public async Task Execute_DisabledPolicy_ReturnsDisabled()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, enabled: false);

        SeedVisitorScan(db, DateTime.UtcNow.AddDays(-30));

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
        Assert.Equal(1, db.VisitorScans.Count());
    }

    [Fact]
    public async Task Execute_WritesRetentionRun()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 7);

        SeedVisitorScan(db, DateTime.UtcNow.AddDays(-30));

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var run = db.RetentionRuns.Single(r => r.EntityName == "VisitorScan");
        Assert.Equal("Manual", run.RunMode);
        Assert.Equal("Success", run.Status);
        Assert.Equal(1, run.RowsAffected);
    }
}
