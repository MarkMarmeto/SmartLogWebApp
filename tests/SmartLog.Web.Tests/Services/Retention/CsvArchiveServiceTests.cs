using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Services.Retention;

namespace SmartLog.Web.Tests.Services.Retention;

public class CsvArchiveServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"smartlog-archive-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private CsvArchiveService CreateService(string? archiveDir = null) =>
        new CsvArchiveService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Retention:ArchiveDirectory"] = archiveDir ?? _tempDir
                })
                .Build(),
            NullLogger<CsvArchiveService>.Instance);

    // ─── AC4: directory creation ───────────────────────────────────────────────

    [Fact]
    public async Task ArchiveBatch_CreatesDirectoryStructure()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "hello") };

        await svc.ArchiveBatchAsync("TestEntity", rows);

        var now = DateTime.UtcNow;
        var expectedDir = Path.Combine(_tempDir, "TestEntity", now.ToString("yyyy-MM"));
        Assert.True(Directory.Exists(expectedDir));
    }

    // ─── AC2: CSV format ───────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveBatch_WritesHeaderAndRows()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "Alice"), new(2, "Bob") };

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        Assert.True(result.Success);
        var lines = await File.ReadAllLinesAsync(result.FilePath!);
        Assert.Equal("Id,Name", lines[0]); // header (ordered by property name)
        Assert.Equal("1,Alice", lines[1]);
        Assert.Equal("2,Bob", lines[2]);
    }

    [Fact]
    public async Task ArchiveBatch_EscapesCommaInField()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "Smith, John") };

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        var content = await File.ReadAllTextAsync(result.FilePath!);
        Assert.Contains("\"Smith, John\"", content);
    }

    [Fact]
    public async Task ArchiveBatch_EscapesQuotesInField()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "He said \"hello\"") };

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        var content = await File.ReadAllTextAsync(result.FilePath!);
        Assert.Contains("\"He said \"\"hello\"\"\"", content);
    }

    [Fact]
    public async Task ArchiveBatch_EscapesNewlineInField()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "line1\nline2") };

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        var content = await File.ReadAllTextAsync(result.FilePath!);
        Assert.Contains("\"line1\nline2\"", content);
    }

    [Fact]
    public async Task ArchiveBatch_WritesUtf8BomFile()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "test") };

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        var bytes = await File.ReadAllBytesAsync(result.FilePath!);
        // UTF-8 BOM is EF BB BF
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    // ─── AC3: schema companion ────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveBatch_WritesSchemaJsonCompanion()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "test") };

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        var now = DateTime.UtcNow;
        var monthDir = Path.Combine(_tempDir, "TestEntity", now.ToString("yyyy-MM"));
        var schemaFiles = Directory.GetFiles(monthDir, "*.schema.json");
        Assert.Single(schemaFiles);

        var json = await File.ReadAllTextAsync(schemaFiles[0]);
        Assert.Contains("\"entityName\"", json);
        Assert.Contains("TestEntity", json);
    }

    [Fact]
    public async Task ArchiveBatch_SchemaWrittenOnce_NotDuplicated()
    {
        var svc = CreateService();
        var rows = new List<SimpleRow> { new(1, "test") };

        await svc.ArchiveBatchAsync("TestEntity", rows, batchIndex: 0);
        await svc.ArchiveBatchAsync("TestEntity", rows, batchIndex: 1);

        var now = DateTime.UtcNow;
        var monthDir = Path.Combine(_tempDir, "TestEntity", now.ToString("yyyy-MM"));
        var schemaFiles = Directory.GetFiles(monthDir, "*.schema.json");
        Assert.Single(schemaFiles); // only one schema file even after two batches
    }

    // ─── AC2: row count + result ───────────────────────────────────────────────

    [Fact]
    public async Task ArchiveBatch_ReturnsCorrectRowCount()
    {
        var svc = CreateService();
        var rows = Enumerable.Range(1, 5).Select(i => new SimpleRow(i, $"Row{i}")).ToList();

        var result = await svc.ArchiveBatchAsync("TestEntity", rows);

        Assert.Equal(5, result.RowCount);
    }

    [Fact]
    public async Task ArchiveBatch_EmptyList_ReturnsOkZeroRows()
    {
        var svc = CreateService();

        var result = await svc.ArchiveBatchAsync("TestEntity", new List<SimpleRow>());

        Assert.True(result.Success);
        Assert.Equal(0, result.RowCount);
    }

    // ─── AC5: archive failure ─────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveBatch_InvalidDirectory_ReturnsFailResult()
    {
        // Point archive dir inside an existing file — Directory.CreateDirectory throws
        // IOException on both Windows and Unix when a path component is a file, not a dir.
        var tempFile = Path.GetTempFileName();
        try
        {
            var badDir = Path.Combine(tempFile, "subdir");
            var svc = CreateService(badDir);
            var rows = new List<SimpleRow> { new(1, "test") };

            var result = await svc.ArchiveBatchAsync("TestEntity", rows);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private record SimpleRow(int Id, string Name);
}
