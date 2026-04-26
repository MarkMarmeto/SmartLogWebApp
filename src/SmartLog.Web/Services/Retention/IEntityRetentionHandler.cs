namespace SmartLog.Web.Services.Retention;

public record RetentionPreview(int EligibleRows, DateTime? OldestRow, DateTime? NewestRow);

public record RetentionResult(
    string Status,
    int RowsAffected,
    string? Note = null,
    string? ErrorMessage = null)
{
    public static RetentionResult Success(int rows) => new("Success", rows);
    public static RetentionResult Disabled() => new("Success", 0, "Policy disabled");
    public static RetentionResult Skipped() => new("Success", 0, "Concurrent run in progress");
    public static RetentionResult Partial(int rows, string err) => new("Partial", rows, ErrorMessage: err);
}

public interface IEntityRetentionHandler
{
    string EntityName { get; }
    Task<RetentionPreview> PreviewAsync(CancellationToken ct = default);
    Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct = default, string runMode = "Manual");
}
