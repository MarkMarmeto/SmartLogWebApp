using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class SmsQueueRetentionHandlerTests
{
    private static SmsQueueRetentionHandler CreateHandler(Data.ApplicationDbContext db) =>
        new(db, NullLogger<SmsQueueRetentionHandler>.Instance);

    private static void SeedPolicy(Data.ApplicationDbContext db, int retentionDays = 30, bool enabled = true)
    {
        db.RetentionPolicies.Add(new RetentionPolicy
        {
            EntityName = "SmsQueue",
            RetentionDays = retentionDays,
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedRows(
        Data.ApplicationDbContext db,
        int count,
        SmsStatus status,
        DateTime createdAt)
    {
        for (var i = 0; i < count; i++)
        {
            db.SmsQueues.Add(new SmsQueue
            {
                PhoneNumber = "09170000000",
                Message = "test",
                Status = status,
                MessageType = "BROADCAST",
                CreatedAt = createdAt
            });
        }
        db.SaveChanges();
    }

    // ─── AC2: Eligibility filter ───────────────────────────────────────────────

    [Fact]
    public async Task Execute_DeletesOnlySentFailedCancelled_OlderThanCutoff()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var recent = DateTime.UtcNow.AddDays(-10);

        // Eligible: old Sent/Failed/Cancelled
        SeedRows(db, 2, SmsStatus.Sent, old);
        SeedRows(db, 1, SmsStatus.Failed, old);
        SeedRows(db, 1, SmsStatus.Cancelled, old);

        // Not eligible: recent or Pending/Processing
        SeedRows(db, 1, SmsStatus.Sent, recent);
        SeedRows(db, 1, SmsStatus.Pending, old);
        SeedRows(db, 1, SmsStatus.Processing, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(4, result.RowsAffected);

        // Pending and Processing survive
        Assert.Equal(1, db.SmsQueues.Count(q => q.Status == SmsStatus.Pending));
        Assert.Equal(1, db.SmsQueues.Count(q => q.Status == SmsStatus.Processing));

        // Recent Sent survives
        Assert.Equal(1, db.SmsQueues.Count(q => q.Status == SmsStatus.Sent));
    }

    // ─── AC3: Batched deletion ────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ProcessesIn1000RowBatches()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 1);

        var old = DateTime.UtcNow.AddDays(-10);
        SeedRows(db, 1500, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(1500, result.RowsAffected);
        Assert.Equal(0, db.SmsQueues.Count());
    }

    // ─── AC4: Idempotent re-run ───────────────────────────────────────────────

    [Fact]
    public async Task Execute_SecondRun_ReturnsZeroRowsAffected()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 5, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var second = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", second.Status);
        Assert.Equal(0, second.RowsAffected);
    }

    // ─── AC5: Dry run ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DryRun_ReturnsCountWithoutDeleting()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 3, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: true);

        Assert.Equal("Success", result.Status);
        Assert.Equal(3, result.RowsAffected);
        Assert.Equal(3, db.SmsQueues.Count()); // no rows deleted
    }

    // ─── AC6: RetentionRun logged ─────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesRetentionRun()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 2, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var run = db.RetentionRuns.Single(r => r.EntityName == "SmsQueue");
        Assert.Equal("Manual", run.RunMode);
        Assert.Equal("Success", run.Status);
        Assert.Equal(2, run.RowsAffected);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task Execute_DryRun_WritesRetentionRunWithDryRunMode()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 2, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: true);

        var run = db.RetentionRuns.Single(r => r.EntityName == "SmsQueue");
        Assert.Equal("DryRun", run.RunMode);
        Assert.Equal("Success", run.Status);
    }

    // ─── AC7: Disabled policy ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DisabledPolicy_ReturnsDisabledWithoutTouchingData()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30, enabled: false);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 5, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
        Assert.Equal(5, db.SmsQueues.Count()); // untouched
    }

    [Fact]
    public async Task Execute_NoPolicyRow_ReturnsDisabled()
    {
        var db = TestDbContextFactory.Create();
        // no policy seeded

        var old = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 3, SmsStatus.Sent, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
    }

    // ─── Preview ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_ReturnsEligibleCountAndDateRange()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old1 = DateTime.UtcNow.AddDays(-90);
        var old2 = DateTime.UtcNow.AddDays(-60);
        SeedRows(db, 1, SmsStatus.Sent, old1);
        SeedRows(db, 1, SmsStatus.Sent, old2);
        SeedRows(db, 1, SmsStatus.Pending, old1); // not eligible

        var handler = CreateHandler(db);
        var preview = await handler.PreviewAsync();

        Assert.Equal(2, preview.EligibleRows);
        Assert.NotNull(preview.OldestRow);
        Assert.NotNull(preview.NewestRow);
    }
}
