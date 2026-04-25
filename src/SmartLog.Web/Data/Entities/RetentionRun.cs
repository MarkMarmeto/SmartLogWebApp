namespace SmartLog.Web.Data.Entities;

public class RetentionRun
{
    public long Id { get; set; }

    public string EntityName { get; set; } = null!;

    /// <summary>Scheduled | Manual | DryRun</summary>
    public string RunMode { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Success | Failed | Partial</summary>
    public string Status { get; set; } = null!;

    public int RowsAffected { get; set; }

    public int? DurationMs { get; set; }

    public string? ErrorMessage { get; set; }

    public string? TriggeredBy { get; set; }
}
