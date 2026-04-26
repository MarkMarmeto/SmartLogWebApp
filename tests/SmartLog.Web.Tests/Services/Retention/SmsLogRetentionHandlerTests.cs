using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class SmsLogRetentionHandlerTests
{
    private static SmsLogRetentionHandler CreateHandler(
        Data.ApplicationDbContext db,
        IArchiveService? archiveService = null) =>
        new(db, NullLogger<SmsLogRetentionHandler>.Instance, archiveService);

    private static void SeedPolicy(
        Data.ApplicationDbContext db,
        int retentionDays = 30,
        bool enabled = true,
        bool archiveEnabled = false)
    {
        db.RetentionPolicies.Add(new RetentionPolicy
        {
            EntityName = "SmsLog",
            RetentionDays = retentionDays,
            Enabled = enabled,
            ArchiveEnabled = archiveEnabled,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedRows(Data.ApplicationDbContext db, int count, DateTime createdAt)
    {
        for (var i = 0; i < count; i++)
        {
            db.SmsLogs.Add(new SmsLog
            {
                PhoneNumber = "09170000000",
                Message = "test",
                Status = "SENT",
                CreatedAt = createdAt
            });
        }
        db.SaveChanges();
    }

    // ─── AC2: Eligibility — all rows by age ───────────────────────────────────

    [Fact]
    public async Task Execute_DeletesRowsOlderThanCutoff()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var recent = DateTime.UtcNow.AddDays(-10);

        SeedRows(db, 3, old);
        SeedRows(db, 2, recent);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(3, result.RowsAffected);
        Assert.Equal(2, db.SmsLogs.Count()); // recent rows survive
    }

    // ─── AC4: Dry run ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DryRun_ReturnsCountWithoutDeleting()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 4, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: true);

        Assert.Equal("Success", result.Status);
        Assert.Equal(4, result.RowsAffected);
        Assert.Equal(4, db.SmsLogs.Count()); // untouched
    }

    // ─── AC5: Archive hook — null service ─────────────────────────────────────

    [Fact]
    public async Task Execute_ArchiveEnabledNullService_LogsWarningAndSkipsDelete()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30, archiveEnabled: true);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 3, old);

        var handler = CreateHandler(db, archiveService: null);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Partial", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Contains("Archive service not configured", result.ErrorMessage ?? "");
        Assert.Equal(3, db.SmsLogs.Count()); // nothing deleted
    }

    // ─── AC5: Archive hook — service failure blocks delete ────────────────────

    [Fact]
    public async Task Execute_ArchiveServiceFails_StopsLoopAndReturnsPartial()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30, archiveEnabled: true);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 3, old);

        var archiveMock = new Mock<IArchiveService>();
        archiveMock
            .Setup(a => a.ArchiveBatchAsync("SmsLog", It.IsAny<IReadOnlyList<SmsLog>>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync(ArchiveResult.Fail("disk full"));

        var handler = CreateHandler(db, archiveMock.Object);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Failed", result.Status); // 0 rows deleted before failure
        Assert.Equal(0, result.RowsAffected);
        Assert.Contains("disk full", result.ErrorMessage ?? "");
        Assert.Equal(3, db.SmsLogs.Count()); // nothing deleted
    }

    // ─── AC5: Archive hook — success path ─────────────────────────────────────

    [Fact]
    public async Task Execute_ArchiveSucceeds_DeletesRows()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30, archiveEnabled: true);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 3, old);

        var archiveMock = new Mock<IArchiveService>();
        archiveMock
            .Setup(a => a.ArchiveBatchAsync("SmsLog", It.IsAny<IReadOnlyList<SmsLog>>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync(ArchiveResult.Ok("/archives/smslog/batch.csv", 3));

        var handler = CreateHandler(db, archiveMock.Object);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(3, result.RowsAffected);
        Assert.Equal(0, db.SmsLogs.Count());
    }

    // ─── AC7: Disabled policy ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DisabledPolicy_ReturnsDisabledWithoutTouchingData()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, enabled: false);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 5, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
        Assert.Equal(5, db.SmsLogs.Count());
    }

    // ─── Run logging ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesRetentionRun()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 2, old);

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var run = db.RetentionRuns.Single(r => r.EntityName == "SmsLog");
        Assert.Equal("Manual", run.RunMode);
        Assert.Equal("Success", run.Status);
        Assert.Equal(2, run.RowsAffected);
        Assert.NotNull(run.CompletedAt);
    }
}
