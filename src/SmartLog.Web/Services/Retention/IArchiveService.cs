namespace SmartLog.Web.Services.Retention;

public record ArchiveResult(bool Success, string? FilePath, int RowCount, string? ErrorMessage = null)
{
    public static ArchiveResult Ok(string path, int rows) => new(true, path, rows);
    public static ArchiveResult Fail(string error) => new(false, null, 0, error);
}

public interface IArchiveService
{
    Task<ArchiveResult> ArchiveBatchAsync<T>(
        string entityName,
        IReadOnlyList<T> rows,
        CancellationToken ct = default,
        int batchIndex = 0);
}
