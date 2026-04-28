using Microsoft.AspNetCore.Identity;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditService(
        ApplicationDbContext context,
        ILogger<AuditService> logger,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task LogAsync(
        string action,
        string? userId = null,
        string? performedByUserId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        ValidateIdShape(userId, nameof(userId));
        ValidateIdShape(performedByUserId, nameof(performedByUserId));

        var userName = userId is null ? null : await ResolveUserNameAsync(userId);
        var performedByUserName = performedByUserId is null ? null : await ResolveUserNameAsync(performedByUserId);

        var auditLog = new AuditLog
        {
            Action = action,
            UserId = userId,
            UserName = userName,
            PerformedByUserId = performedByUserId,
            PerformedByUserName = performedByUserName,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Audit: {Action} - UserId: {UserId}, PerformedBy: {PerformedBy}, Details: {Details}",
            action, userId, performedByUserId, details);
    }

    private static void ValidateIdShape(string? id, string paramName)
    {
        if (id is null) return;
        if (!Guid.TryParse(id, out _))
            throw new ArgumentException(
                $"Audit user-id must be a GUID-formatted Identity Id; received: '{id}'",
                paramName);
    }

    private async Task<string?> ResolveUserNameAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user?.UserName;
    }
}
