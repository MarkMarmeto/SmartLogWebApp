using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SmartLog.Web.Services.Retention;

public class CsvArchiveService : IArchiveService
{
    private readonly IConfiguration _config;
    private readonly ILogger<CsvArchiveService> _logger;

    public CsvArchiveService(IConfiguration config, ILogger<CsvArchiveService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<ArchiveResult> ArchiveBatchAsync<T>(
        string entityName,
        IReadOnlyList<T> rows,
        CancellationToken ct = default,
        int batchIndex = 0)
    {
        if (rows.Count == 0)
            return ArchiveResult.Ok(string.Empty, 0);

        try
        {
            var archiveDir = _config["Retention:ArchiveDirectory"] ?? "./archives";
            var now = DateTime.UtcNow;
            var monthDir = Path.Combine(archiveDir, entityName, now.ToString("yyyy-MM"));
            Directory.CreateDirectory(monthDir);

            var fileName = $"{entityName.ToLowerInvariant()}-{now:yyyyMMdd-HHmmss}-{batchIndex}.csv";
            var filePath = Path.Combine(monthDir, fileName);

            var props = GetExportProperties<T>();

            await WriteSchemaAsync(monthDir, entityName, props, now, ct);
            await WriteCsvAsync(filePath, props, rows, ct);

            _logger.LogInformation(
                "CsvArchiveService: archived {Count} {Entity} rows to {File}",
                rows.Count, entityName, filePath);

            return ArchiveResult.Ok(filePath, rows.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "CsvArchiveService: failed to archive {Entity} batch", entityName);
            return ArchiveResult.Fail(ex.Message);
        }
    }

    private static PropertyInfo[] GetExportProperties<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && (!p.PropertyType.IsClass || p.PropertyType == typeof(string)))
            .OrderBy(p => p.Name)
            .ToArray();

    private static async Task WriteCsvAsync<T>(
        string filePath,
        PropertyInfo[] props,
        IReadOnlyList<T> rows,
        CancellationToken ct)
    {
        await using var writer = new StreamWriter(
            filePath,
            append: false,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(string.Join(",", props.Select(p => EscapeCsvField(p.Name))));

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var fields = props.Select(p => EscapeCsvField(p.GetValue(row)?.ToString() ?? ""));
            await writer.WriteLineAsync(string.Join(",", fields));
        }
    }

    private static async Task WriteSchemaAsync(
        string monthDir,
        string entityName,
        PropertyInfo[] props,
        DateTime now,
        CancellationToken ct)
    {
        var schemaPattern = $"{entityName.ToLowerInvariant()}-{now:yyyyMMdd}*.schema.json";
        if (Directory.EnumerateFiles(monthDir, schemaPattern).Any())
            return;

        var schemaPath = Path.Combine(monthDir,
            $"{entityName.ToLowerInvariant()}-{now:yyyyMMdd}.schema.json");

        var schema = new
        {
            entityName,
            generatedAt = now.ToString("O"),
            columns = props.Select(p => new
            {
                name = p.Name,
                type = MapSqlType(p.PropertyType)
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(schemaPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string MapSqlType(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        var nullable = Nullable.GetUnderlyingType(t) != null;
        string baseType;
        if (underlying == typeof(long))
            baseType = "bigint";
        else if (underlying == typeof(int))
            baseType = "int";
        else if (underlying == typeof(bool))
            baseType = "bit";
        else if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
            baseType = "datetime2";
        else if (underlying == typeof(Guid))
            baseType = "uniqueidentifier";
        else if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
            baseType = "decimal";
        else
            baseType = "nvarchar";
        return nullable ? $"{baseType} (nullable)" : baseType;
    }
}
