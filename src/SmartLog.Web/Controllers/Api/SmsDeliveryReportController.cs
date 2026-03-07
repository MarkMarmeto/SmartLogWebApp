using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;

namespace SmartLog.Web.Controllers.Api;

[ApiController]
[Route("api/sms/delivery-report")]
[Produces("application/json")]
public class SmsDeliveryReportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsDeliveryReportController> _logger;

    public SmsDeliveryReportController(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<SmsDeliveryReportController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] DeliveryReportRequest request)
    {
        // Optional webhook secret validation
        var webhookSecret = _configuration["Sms:Semaphore:WebhookSecret"];
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            var providedSecret = Request.Headers["X-Webhook-Secret"].FirstOrDefault();
            if (providedSecret != webhookSecret)
            {
                _logger.LogWarning("Delivery report webhook rejected: invalid secret");
                return Unauthorized();
            }
        }

        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return BadRequest(new { error = "message_id is required" });
        }

        try
        {
            var smsLog = await _context.SmsLogs
                .FirstOrDefaultAsync(l => l.ProviderMessageId == request.MessageId);

            if (smsLog == null)
            {
                _logger.LogDebug("Delivery report for unknown message_id: {MessageId}", request.MessageId);
                return Ok(new { status = "ignored", reason = "unknown_message_id" });
            }

            smsLog.DeliveryStatus = request.Status?.ToUpperInvariant() switch
            {
                "DELIVERED" => "DELIVERED",
                "UNDELIVERED" => "UNDELIVERED",
                "REJECTED" => "REJECTED",
                "FAILED" => "REJECTED",
                _ => request.Status?.ToUpperInvariant() ?? "UNKNOWN"
            };

            smsLog.DeliveredAt = request.Status?.ToUpperInvariant() == "DELIVERED"
                ? DateTime.UtcNow
                : null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Delivery report received: {MessageId} -> {Status}",
                request.MessageId, smsLog.DeliveryStatus);

            return Ok(new { status = "processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing delivery report for {MessageId}", request.MessageId);
            return Ok(new { status = "error" });
        }
    }
}

public class DeliveryReportRequest
{
    public string? MessageId { get; set; }
    public string? Status { get; set; }
    public string? PhoneNumber { get; set; }
}
