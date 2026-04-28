using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class AuditServiceTests
{
    private static AuditService BuildSut(
        Data.ApplicationDbContext db,
        Mock<UserManager<ApplicationUser>>? userManagerMock = null)
    {
        userManagerMock ??= BuildUserManagerMock();
        return new AuditService(db, NullLogger<AuditService>.Instance, userManagerMock.Object);
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    // ─── AC5: Runtime guard rejects malformed user ids ───────────────────────

    [Fact]
    public async Task LogAsync_RejectsMalformedPerformedByUserId()
    {
        var db = TestDbContextFactory.Create();
        var sut = BuildSut(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.LogAsync(
                action: "TestAction",
                performedByUserId: "this is a sentence, not an id"));

        Assert.Equal("performedByUserId", ex.ParamName);
    }

    [Fact]
    public async Task LogAsync_RejectsMalformedUserId()
    {
        var db = TestDbContextFactory.Create();
        var sut = BuildSut(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.LogAsync(
                action: "TestAction",
                userId: "not-a-guid-123-!!!"));

        Assert.Equal("userId", ex.ParamName);
    }

    [Fact]
    public async Task LogAsync_AcceptsNullPerformedByUserId()
    {
        var db = TestDbContextFactory.Create();
        var sut = BuildSut(db);

        await sut.LogAsync(action: "SystemAction", performedByUserId: null);

        var row = db.AuditLogs.Single();
        Assert.Equal("SystemAction", row.Action);
        Assert.Null(row.PerformedByUserId);
        Assert.Null(row.PerformedByUserName);
    }

    // ─── AC4: Snapshot username captured at write time ───────────────────────

    [Fact]
    public async Task LogAsync_SnapshotsPerformedByUserNameAtWriteTime()
    {
        var aliceId = Guid.NewGuid().ToString();
        var db = TestDbContextFactory.Create();

        var umMock = BuildUserManagerMock();
        umMock.Setup(m => m.FindByIdAsync(aliceId))
              .ReturnsAsync(new ApplicationUser { UserName = "alice" });

        var sut = BuildSut(db, umMock);
        await sut.LogAsync(action: "TestAction", performedByUserId: aliceId);

        var row = db.AuditLogs.Single();
        Assert.Equal(aliceId, row.PerformedByUserId);
        Assert.Equal("alice", row.PerformedByUserName);
    }

    [Fact]
    public async Task LogAsync_LeavesUserNameNullWhenIdDoesNotResolve()
    {
        var unknownId = Guid.NewGuid().ToString();
        var db = TestDbContextFactory.Create();

        var umMock = BuildUserManagerMock();
        umMock.Setup(m => m.FindByIdAsync(unknownId))
              .ReturnsAsync((ApplicationUser?)null);

        var sut = BuildSut(db, umMock);
        await sut.LogAsync(action: "TestAction", performedByUserId: unknownId);

        var row = db.AuditLogs.Single();
        Assert.Equal(unknownId, row.PerformedByUserId);
        Assert.Null(row.PerformedByUserName);
    }
}
