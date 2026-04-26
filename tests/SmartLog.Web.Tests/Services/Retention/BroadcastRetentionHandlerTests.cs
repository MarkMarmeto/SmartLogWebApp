using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services.Retention;

public class BroadcastRetentionHandlerTests
{
    private static BroadcastRetentionHandler CreateHandler(Data.ApplicationDbContext db) =>
        new(db, NullLogger<BroadcastRetentionHandler>.Instance);

    private static void SeedPolicy(Data.ApplicationDbContext db, int retentionDays = 30, bool enabled = true)
    {
        db.RetentionPolicies.Add(new RetentionPolicy
        {
            EntityName = "Broadcast",
            RetentionDays = retentionDays,
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static Broadcast SeedBroadcast(Data.ApplicationDbContext db, DateTime createdAt)
    {
        var b = new Broadcast
        {
            Type = "ANNOUNCEMENT",
            Message = "test",
            Status = BroadcastStatus.Sent,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        db.Broadcasts.Add(b);
        db.SaveChanges();
        return b;
    }

    private static void SeedQueueChild(
        Data.ApplicationDbContext db,
        Guid broadcastId,
        SmsStatus status)
    {
        db.SmsQueues.Add(new SmsQueue
        {
            PhoneNumber = "09170000000",
            Message = "test",
            Status = status,
            MessageType = "BROADCAST",
            BroadcastId = broadcastId
        });
        db.SaveChanges();
    }

    // ─── AC2: Eligible broadcasts deleted ────────────────────────────────────

    [Fact]
    public async Task Execute_DeletesOldBroadcastWithAllTerminalChildren()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var b = SeedBroadcast(db, old);
        SeedQueueChild(db, b.Id, SmsStatus.Sent);
        SeedQueueChild(db, b.Id, SmsStatus.Failed);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(1, result.RowsAffected);
        Assert.False(db.Broadcasts.Any());
        Assert.False(db.SmsQueues.Any()); // child rows also cleaned
    }

    // ─── AC2: Skip broadcasts with active children ────────────────────────────

    [Fact]
    public async Task Execute_SkipsBroadcastWithPendingQueueChild()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var b = SeedBroadcast(db, old);
        SeedQueueChild(db, b.Id, SmsStatus.Pending);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.True(db.Broadcasts.Any()); // not deleted
    }

    [Fact]
    public async Task Execute_SkipsBroadcastWithProcessingQueueChild()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var b = SeedBroadcast(db, old);
        SeedQueueChild(db, b.Id, SmsStatus.Processing);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.True(db.Broadcasts.Any());
    }

    // ─── AC3: Child queue rows deleted ───────────────────────────────────────

    [Fact]
    public async Task Execute_DeletesTerminalChildQueueRows()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        var b1 = SeedBroadcast(db, old);
        var b2 = SeedBroadcast(db, old);
        SeedQueueChild(db, b1.Id, SmsStatus.Sent);
        SeedQueueChild(db, b1.Id, SmsStatus.Cancelled);
        SeedQueueChild(db, b2.Id, SmsStatus.Failed);

        // Orphan queue row (null BroadcastId) — should be untouched
        db.SmsQueues.Add(new SmsQueue
        {
            PhoneNumber = "09170000001",
            Message = "orphan",
            Status = SmsStatus.Sent,
            MessageType = "ATTENDANCE",
            BroadcastId = null
        });
        db.SaveChanges();

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected); // 2 broadcasts deleted
        Assert.False(db.Broadcasts.Any());

        // Orphan row survives
        Assert.Equal(1, db.SmsQueues.Count());
    }

    // ─── AC4: Dry run ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DryRun_ReturnsBroadcastCountWithoutDeleting()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedBroadcast(db, old);
        SeedBroadcast(db, old);
        SeedBroadcast(db, DateTime.UtcNow.AddDays(-5)); // recent, not eligible

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: true);

        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(3, db.Broadcasts.Count()); // untouched
    }

    // ─── AC6: Disabled policy ────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DisabledPolicy_ReturnsDisabled()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, enabled: false);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedBroadcast(db, old);

        var handler = CreateHandler(db);
        var result = await handler.ExecuteAsync(dryRun: false);

        Assert.Equal("Success", result.Status);
        Assert.Equal(0, result.RowsAffected);
        Assert.Equal("Policy disabled", result.Note);
        Assert.True(db.Broadcasts.Any());
    }

    // ─── Run logging ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesRetentionRun()
    {
        var db = TestDbContextFactory.Create();
        SeedPolicy(db, retentionDays: 30);

        var old = DateTime.UtcNow.AddDays(-60);
        SeedBroadcast(db, old);

        var handler = CreateHandler(db);
        await handler.ExecuteAsync(dryRun: false);

        var run = db.RetentionRuns.Single(r => r.EntityName == "Broadcast");
        Assert.Equal("Manual", run.RunMode);
        Assert.Equal("Success", run.Status);
        Assert.NotNull(run.CompletedAt);
    }
}
