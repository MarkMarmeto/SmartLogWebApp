using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Background service that processes the SMS queue
/// </summary>
public class SmsWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SmsWorkerService> _logger;
    private readonly IConfiguration _configuration;

    public SmsWorkerService(
        IServiceProvider serviceProvider,
        ILogger<SmsWorkerService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SMS Worker Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SMS worker service");
            }

            // Poll every 5 seconds
            var pollingInterval = _configuration.GetValue<int>("Sms:Queue:PollingIntervalSeconds", 5);
            await Task.Delay(TimeSpan.FromSeconds(pollingInterval), stoppingToken);
        }

        _logger.LogInformation("SMS Worker Service stopped");
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();

        // Check if SMS is globally enabled
        if (!await settingsService.IsSmsEnabledAsync())
        {
            return;
        }

        // Get pending messages ordered by priority (Emergency → High → Normal → Low) then FIFO
        var pendingMessages = await context.SmsQueues
            .Where(q => (q.Status == SmsStatus.Pending &&
                        (q.ScheduledAt == null || q.ScheduledAt <= DateTime.UtcNow)) ||
                       (q.Status == SmsStatus.Failed &&
                        q.RetryCount < q.MaxRetries &&
                        q.NextRetryAt <= DateTime.UtcNow))
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.CreatedAt)
            .Take(10) // Process in batches of 10
            .ToListAsync(stoppingToken);

        if (!pendingMessages.Any())
        {
            return;
        }

        _logger.LogDebug("Processing {Count} pending SMS messages", pendingMessages.Count);

        // Get gateways
        var gsmGateway = scope.ServiceProvider.GetService<GsmModemGateway>();
        var semaphoreGateway = scope.ServiceProvider.GetService<SemaphoreGateway>();

        var dbDefaultProvider = await settingsService.GetSettingAsync("Sms.DefaultProvider");
        var defaultProvider = dbDefaultProvider
            ?? _configuration.GetValue<string>("Sms:DefaultProvider", "SEMAPHORE")
            ?? "SEMAPHORE";

        var dbFallbackEnabled = await settingsService.GetSettingAsync("Sms.FallbackEnabled");
        var fallbackEnabled = dbFallbackEnabled != null
            ? dbFallbackEnabled.Equals("true", StringComparison.OrdinalIgnoreCase) || dbFallbackEnabled == "1"
            : _configuration.GetValue<bool>("Sms:FallbackEnabled", true);

        foreach (var message in pendingMessages)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessMessageAsync(
                context,
                message,
                gsmGateway,
                semaphoreGateway,
                defaultProvider,
                fallbackEnabled);
        }
    }

    private async Task ProcessMessageAsync(
        ApplicationDbContext context,
        SmsQueue message,
        GsmModemGateway? gsmGateway,
        SemaphoreGateway? semaphoreGateway,
        string defaultProvider,
        bool fallbackEnabled)
    {
        try
        {
            // Mark as processing
            message.Status = SmsStatus.Processing;
            message.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            // Select gateway
            ISmsGateway? gateway = null;
            string provider = defaultProvider;

            if (defaultProvider == "GSM_MODEM" && gsmGateway != null)
            {
                if (await gsmGateway.IsAvailableAsync())
                {
                    gateway = gsmGateway;
                    provider = "GSM_MODEM";
                }
                else if (fallbackEnabled && semaphoreGateway != null)
                {
                    _logger.LogWarning("GSM modem unavailable, falling back to Semaphore");
                    if (await semaphoreGateway.IsAvailableAsync())
                    {
                        gateway = semaphoreGateway;
                        provider = "SEMAPHORE";
                    }
                }
            }
            else if (defaultProvider == "SEMAPHORE" && semaphoreGateway != null)
            {
                if (await semaphoreGateway.IsAvailableAsync())
                {
                    gateway = semaphoreGateway;
                    provider = "SEMAPHORE";
                }
                else if (fallbackEnabled && gsmGateway != null)
                {
                    _logger.LogWarning("Semaphore unavailable, falling back to GSM modem");
                    if (await gsmGateway.IsAvailableAsync())
                    {
                        gateway = gsmGateway;
                        provider = "GSM_MODEM";
                    }
                }
            }

            if (gateway == null)
            {
                throw new Exception("No SMS gateway available");
            }

            // Send the message
            var result = await gateway.SendAsync(message.PhoneNumber, message.Message);

            if (result.Success)
            {
                // Update queue entry
                message.Status = SmsStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                message.Provider = provider;
                message.ProviderMessageId = result.ProviderMessageId;
                message.ErrorMessage = null;

                // Log to SmsLog
                context.SmsLogs.Add(new SmsLog
                {
                    QueueId = message.Id,
                    PhoneNumber = message.PhoneNumber,
                    Message = message.Message,
                    Status = "SENT",
                    Provider = provider,
                    ProviderMessageId = result.ProviderMessageId,
                    MessageParts = result.MessageParts,
                    ProcessingTimeMs = result.ProcessingTimeMs,
                    StudentId = message.StudentId,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
                await TryFinalizeBroadcastAsync(context, message.BroadcastId);

                _logger.LogInformation("SMS {Id} sent successfully via {Provider} to {Phone}",
                    message.Id, provider, message.PhoneNumber);
            }
            else
            {
                // Increment retry count
                message.RetryCount++;
                message.ErrorMessage = result.ErrorMessage;

                if (message.RetryCount >= message.MaxRetries)
                {
                    // Mark as failed
                    message.Status = SmsStatus.Failed;

                    // Log failure
                    context.SmsLogs.Add(new SmsLog
                    {
                        QueueId = message.Id,
                        PhoneNumber = message.PhoneNumber,
                        Message = message.Message,
                        Status = "FAILED",
                        Provider = provider,
                        ProviderMessageId = result.ProviderMessageId,
                        ErrorMessage = result.ErrorMessage,
                        ProcessingTimeMs = result.ProcessingTimeMs,
                        StudentId = message.StudentId,
                        CreatedAt = DateTime.UtcNow
                    });

                    _logger.LogError("SMS {Id} failed after {Retries} retries: {Error}",
                        message.Id, message.MaxRetries, result.ErrorMessage);
                }
                else
                {
                    // Schedule retry with exponential backoff
                    message.Status = SmsStatus.Failed;
                    var backoffMinutes = Math.Pow(2, message.RetryCount); // 2, 4, 8 minutes
                    message.NextRetryAt = DateTime.UtcNow.AddMinutes(backoffMinutes);

                    _logger.LogWarning("SMS {Id} failed, retry {Retry}/{Max} scheduled for {NextRetry}",
                        message.Id, message.RetryCount, message.MaxRetries, message.NextRetryAt);
                }

                await context.SaveChangesAsync();
                await TryFinalizeBroadcastAsync(context, message.BroadcastId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SMS {Id}", message.Id);

            // Increment retry count
            message.RetryCount++;
            message.ErrorMessage = ex.Message;

            if (message.RetryCount >= message.MaxRetries)
            {
                message.Status = SmsStatus.Failed;
            }
            else
            {
                message.Status = SmsStatus.Failed;
                var backoffMinutes = Math.Pow(2, message.RetryCount);
                message.NextRetryAt = DateTime.UtcNow.AddMinutes(backoffMinutes);
            }

            await context.SaveChangesAsync();
            await TryFinalizeBroadcastAsync(context, message.BroadcastId);
        }
    }

    /// <summary>
    /// After a message is processed, check if all messages in the broadcast are done
    /// and update the Broadcast status accordingly.
    /// </summary>
    private async Task TryFinalizeBroadcastAsync(ApplicationDbContext context, Guid? broadcastId)
    {
        if (broadcastId == null) return;

        try
        {
            var broadcast = await context.Broadcasts
                .FirstOrDefaultAsync(b => b.Id == broadcastId);

            if (broadcast == null || broadcast.Status == Data.Entities.BroadcastStatus.Cancelled)
                return;

            // Check if any messages are still active (Pending or Processing)
            var stillActive = await context.SmsQueues
                .AnyAsync(q => q.BroadcastId == broadcastId &&
                               (q.Status == SmsStatus.Pending || q.Status == SmsStatus.Processing));

            if (stillActive) return;

            // All done — determine final status
            var anyFailed = await context.SmsQueues
                .AnyAsync(q => q.BroadcastId == broadcastId && q.Status == SmsStatus.Failed);

            broadcast.Status = anyFailed
                ? Data.Entities.BroadcastStatus.Sent   // partial failure still counts as Sent
                : Data.Entities.BroadcastStatus.Sent;
            broadcast.SentAt = DateTime.UtcNow;
            broadcast.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Broadcast {BroadcastId} finalized with status {Status}",
                broadcastId, broadcast.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing broadcast {BroadcastId}", broadcastId);
        }
    }
}
