namespace SmartLog.Web.Data.Entities;

public class RetentionPolicy
{
    public int Id { get; set; }

    /// <summary>One of: SmsQueue, SmsLog, Broadcast, Scan, AuditLog, VisitorScan</summary>
    public string EntityName { get; set; } = null!;

    public int RetentionDays { get; set; }

    public bool Enabled { get; set; } = true;

    public bool ArchiveEnabled { get; set; } = false;

    public DateTime? LastRunAt { get; set; }

    public int? LastRowsAffected { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
