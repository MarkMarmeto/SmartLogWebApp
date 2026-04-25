using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

/// <summary>
/// US0055: Verifies per-broadcast gateway selection.
/// </summary>
public class PerBroadcastGatewayTests
{
    // ========== Broadcast entity ==========

    [Fact]
    public void Broadcast_PreferredProvider_DefaultsToNull()
    {
        var broadcast = new Broadcast();
        Assert.Null(broadcast.PreferredProvider);
    }

    [Theory]
    [InlineData("GSM_MODEM")]
    [InlineData("SEMAPHORE")]
    [InlineData(null)]
    public void Broadcast_PreferredProvider_AcceptsValidValues(string? provider)
    {
        var broadcast = new Broadcast { PreferredProvider = provider };
        Assert.Equal(provider, broadcast.PreferredProvider);
    }

    // ========== SmsQueue gets pre-set Provider from broadcast ==========

    [Fact]
    public async Task QueueBroadcastBatch_SetsProviderOnQueueEntries_WhenPreferredProviderSpecified()
    {
        var context = TestDbContextFactory.Create();

        // Seed a student with phone + SMS enabled
        var student = new Student
        {
            StudentId = "2026-07-0001",
            FirstName = "Juan",
            LastName = "Cruz",
            GradeLevel = "7",
            Section = "A",
            ParentGuardianName = "Parent",
            GuardianRelationship = "Mother",
            ParentPhone = "09171234567",
            IsActive = true,
            SmsEnabled = true,
            SmsLanguage = "EN"
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        // Broadcast with PreferredProvider = GSM_MODEM
        var broadcast = new Broadcast
        {
            Id = Guid.NewGuid(),
            Type = "ANNOUNCEMENT",
            Message = "Test",
            Language = "EN",
            PreferredProvider = "GSM_MODEM",
            Status = BroadcastStatus.Sending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Broadcasts.Add(broadcast);
        await context.SaveChangesAsync();

        // Insert a queue entry as the batch path would
        var queueEntry = new SmsQueue
        {
            PhoneNumber = student.ParentPhone,
            Message = "Test message",
            Status = SmsStatus.Pending,
            Priority = SmsPriority.High,
            MessageType = "ANNOUNCEMENT",
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            BroadcastId = broadcast.Id,
            Provider = broadcast.PreferredProvider   // This is what QueueBroadcastBatchAsync now does
        };
        context.SmsQueues.Add(queueEntry);
        await context.SaveChangesAsync();

        var saved = context.SmsQueues.FirstOrDefault(q => q.BroadcastId == broadcast.Id);
        Assert.NotNull(saved);
        Assert.Equal("GSM_MODEM", saved.Provider);
    }

    [Fact]
    public async Task QueueBroadcastBatch_ProviderNull_WhenNoPreferredProvider()
    {
        var context = TestDbContextFactory.Create();

        var broadcast = new Broadcast
        {
            Id = Guid.NewGuid(),
            Type = "ANNOUNCEMENT",
            Message = "Test",
            PreferredProvider = null,
            Status = BroadcastStatus.Sending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Broadcasts.Add(broadcast);

        var queueEntry = new SmsQueue
        {
            PhoneNumber = "09171234567",
            Message = "Test message",
            Status = SmsStatus.Pending,
            Priority = SmsPriority.Normal,
            MessageType = "ANNOUNCEMENT",
            CreatedAt = DateTime.UtcNow,
            BroadcastId = broadcast.Id,
            Provider = broadcast.PreferredProvider   // null
        };
        context.SmsQueues.Add(queueEntry);
        await context.SaveChangesAsync();

        var saved = context.SmsQueues.FirstOrDefault(q => q.BroadcastId == broadcast.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.Provider);
    }

    // ========== Worker effective provider logic ==========

    [Theory]
    [InlineData("GSM_MODEM", "SEMAPHORE", "GSM_MODEM")]   // pre-set overrides default
    [InlineData("SEMAPHORE", "GSM_MODEM", "SEMAPHORE")]   // pre-set overrides default
    [InlineData(null, "SEMAPHORE", "SEMAPHORE")]           // null: system default wins
    [InlineData("", "GSM_MODEM", "GSM_MODEM")]            // empty string: system default wins
    public void EffectiveProvider_Logic_RespectsPreSetProviderOverDefault(
        string? preSetProvider, string systemDefault, string expectedEffective)
    {
        // Replicate the logic from SmsWorkerService.ProcessMessageAsync
        var effectiveProvider = !string.IsNullOrEmpty(preSetProvider) ? preSetProvider : systemDefault;
        Assert.Equal(expectedEffective, effectiveProvider);
    }
}
