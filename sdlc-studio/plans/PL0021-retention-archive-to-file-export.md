# PL0021: Archive-to-File Export Before Purge — Implementation Plan

> **Status:** Done
> **Story:** [US0102: Archive-to-File Export Before Purge](../stories/US0102-retention-archive-to-file-export.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Implement `IArchiveService` and `CsvArchiveService` — the archive mechanism called by retention handlers when `RetentionPolicy.ArchiveEnabled = true`. Each invocation writes a CSV file to a configured directory with a structured path (`{ArchiveDir}/{EntityName}/{yyyy-MM}/{entityName}-{yyyyMMdd-HHmmss}-{batchIndex}.csv`) plus a per-entity-per-day `.schema.json` companion.

When this story ships, all six handlers that had archive stub warnings (PL0015–PL0019, PL0018) start archiving for real. No handler code change is needed — they already inject `IArchiveService?`; registering the implementation automatically activates the archive path.

**Pre-existing state:**
- Archive hook stubs exist in all six handlers (PL0015–PL0019, PL0018). Each does: if `_archiveService is null → log warning + skip delete`. When this service is registered, the stub activates.
- Retention admin page exists (PL0013) — extend with archive directory display + disk usage.
- `Retention:ArchiveDirectory` and `Retention:ArchiveFormat` settings keys reserved but not yet read.
- CSV libraries: check `SmartLog.Web.csproj` for `CsvHelper`. If absent, implement a hand-written RFC-4180 writer (preferred for zero-dependency simplicity).

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | IArchiveService interface | `ArchiveBatchAsync<T>(entityName, rows, ct)` returning `ArchiveResult` |
| AC2 | CSV output format | Structured path, header row, UTF-8, RFC-4180 escaped |
| AC3 | Schema companion file | `.schema.json` written once per entity per day; reused on subsequent batches |
| AC4 | Configuration | `Retention:ArchiveDirectory` (default `"./archives"`); directory created on first use |
| AC5 | Archive triggers delete | Success → handler deletes batch; failure → handler skips delete, run = Partial |
| AC6 | Archive file retention | `Retention:ArchiveFileDays` setting reserved (null = keep forever); no auto-cleanup yet |
| AC7 | Admin UI — archive location | Retention page shows archive dir path + disk usage summary |
| AC8 | Restore documentation | `docs/retention-archive-restore.md` created |

---

## Technical Context

### File Path Structure
```
{ArchiveDir}/
  SmsLog/
    2026-04/
      smslog-20260424-023145-0.csv     # batch 0
      smslog-20260424-023145-0.schema.json
      smslog-20260424-023245-1.csv     # batch 1 (reuses schema from same day)
```

The batch index is incremented by the caller for multi-batch runs within the same second. Pass it as a parameter or derive from existing file count.

### RFC-4180 CSV Rules (hand-written writer)
- Fields containing commas, double-quotes, or newlines must be wrapped in double quotes.
- Double-quotes within a field are escaped as `""`.
- Line ending: `\r\n` (CRLF per RFC-4180).
- Encoding: UTF-8 with BOM (for Excel compatibility on Windows).

### ArchiveResult type
```csharp
public record ArchiveResult(bool Success, string? FilePath, int RowCount, string? ErrorMessage = null) {
    public static ArchiveResult Ok(string path, int rows) => new(true, path, rows);
    public static ArchiveResult Fail(string error) => new(false, null, 0, error);
}
```

### Schema JSON format
```json
{
  "entityName": "SmsLog",
  "generatedAt": "2026-04-24T02:31:45Z",
  "columns": [
    { "name": "Id", "type": "bigint" },
    { "name": "Provider", "type": "nvarchar(50)" },
    ...
  ]
}
```
Column types are inferred from the C# property type (simple mapping: `long` → `bigint`, `string` → `nvarchar`, `DateTime` → `datetime2`, `bool` → `bit`, `int` → `int`, `int?` → `int (nullable)`, etc.). No reflection magic needed — a simple switch on `property.PropertyType`.

### Disk Usage Summary
Use `Directory.EnumerateFiles(archiveDir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)` for total size. Show top-level subdirectory breakdown.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** CSV writing logic is testable; integration test verifies file structure; handler wiring verifies archive-then-delete sequence.

---

## Implementation Phases

### Phase 1: IArchiveService + ArchiveResult

**Goal:** Define the interface.

- [ ] Create `src/SmartLog.Web/Services/Retention/IArchiveService.cs`:
  ```csharp
  public interface IArchiveService {
      Task<ArchiveResult> ArchiveBatchAsync<T>(
          string entityName,
          IEnumerable<T> rows,
          CancellationToken ct,
          int batchIndex = 0);
  }

  public record ArchiveResult(bool Success, string? FilePath, int RowCount, string? ErrorMessage = null);
  ```

**Files:** `Services/Retention/IArchiveService.cs`

### Phase 2: CsvArchiveService

**Goal:** Implement CSV writer with schema companion.

- [ ] Create `src/SmartLog.Web/Services/Retention/CsvArchiveService.cs`.
- [ ] Constructor injects `IConfiguration` (to read `Retention:ArchiveDirectory`) and `ILogger<CsvArchiveService>`.
- [ ] `ArchiveBatchAsync<T>`:
  1. Resolve archive dir from config; create if missing (`Directory.CreateDirectory`).
  2. Build file path: `{archiveDir}/{entityName}/{yyyy-MM}/{entityName.ToLower()}-{yyyyMMdd-HHmmss}-{batchIndex}.csv`
  3. Get properties via `typeof(T).GetProperties()` — order by Name for determinism.
  4. Write schema companion if not already written today for this entity (check for `*.schema.json` in the date directory).
  5. Write CSV:
     ```csharp
     await using var writer = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
     // Header
     await writer.WriteLineAsync(string.Join(",", props.Select(p => EscapeCsvField(p.Name))));
     // Rows
     foreach (var row in rows) {
         var fields = props.Select(p => EscapeCsvField(p.GetValue(row)?.ToString() ?? ""));
         await writer.WriteLineAsync(string.Join(",", fields));
     }
     ```
  6. Return `ArchiveResult.Ok(filePath, rowCount)`.
  7. On `IOException` or `UnauthorizedAccessException`: return `ArchiveResult.Fail(ex.Message)`.
- [ ] `EscapeCsvField(string value)`:
  ```csharp
  private static string EscapeCsvField(string value) {
      if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
          return $"\"{value.Replace("\"", "\"\"")}\"";
      return value;
  }
  ```
- [ ] Schema JSON writer:
  ```csharp
  private static string MapType(Type t) => t switch {
      var x when x == typeof(long) || x == typeof(long?) => "bigint",
      var x when x == typeof(int) || x == typeof(int?) => "int",
      var x when x == typeof(bool) || x == typeof(bool?) => "bit",
      var x when x == typeof(DateTime) || x == typeof(DateTimeOffset) => "datetime2",
      _ => "nvarchar"
  };
  ```

**Files:** `Services/Retention/CsvArchiveService.cs`

### Phase 3: Configuration + DI Registration

- [ ] In `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IArchiveService, CsvArchiveService>();
  ```
- [ ] Ensure `Retention:ArchiveDirectory` is read from `IConfiguration` (falls through to `appsettings.json` or env var); default `"./archives"` in code (not required to be in `appsettings.json`).

**Files:** `Program.cs`

### Phase 4: Admin UI — Archive Location Widget

**Goal:** Retention page shows archive directory path + disk usage.

- [ ] In `Retention.cshtml.cs`:
  - Add `string ArchiveDirectory { get; set; }` and `string ArchiveDiskUsage { get; set; }`.
  - In `OnGetAsync`:
    ```csharp
    ArchiveDirectory = _config["Retention:ArchiveDirectory"] ?? "./archives";
    ArchiveDiskUsage = ComputeDiskUsage(ArchiveDirectory);
    ```
  - `ComputeDiskUsage`: try `Directory.Exists` → sum file sizes; on failure → "N/A".
- [ ] In `Retention.cshtml`, add a section below the policy table:
  ```html
  <p>Archive location: <code>@Model.ArchiveDirectory</code> — @Model.ArchiveDiskUsage</p>
  ```

**Files:** `Pages/Admin/Settings/Retention.cshtml(.cs)`

### Phase 5: Restore Documentation

- [ ] Create `docs/retention-archive-restore.md`:
  - File format description (UTF-8 CSV with BOM, CRLF, RFC-4180)
  - Schema JSON format
  - How to load into staging DB: BULK INSERT example + EF manual import example
  - Caveat: archives are audit evidence, not operational restore; FK integrity may not restore cleanly
  - Security note: archive directory may contain PII (phone numbers in SmsLog); restrict OS-level access

**Files:** `docs/retention-archive-restore.md`

### Phase 6: Tests

| AC | Test | File |
|----|------|------|
| AC2 | CSV written with correct header + RFC-4180 escaped values (embedded comma, quote, newline) | `CsvArchiveServiceTests.cs` |
| AC3 | Schema JSON written once per day; not rewritten on second call same day | same |
| AC2 | Directory structure follows `{entity}/{yyyy-MM}/` pattern | same |
| AC5 | Archive failure (mock IOException) → `ArchiveResult.Fail` returned | same |
| AC4 | Non-existent archive dir created on first use | same |
| Integration | Handler with `ArchiveEnabled = true` calls archive before delete; on archive failure, does not delete | Handler integration test in PL0015/PL0017/PL0018/PL0019 test files |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Disk full during CSV write | `IOException` caught; `ArchiveResult.Fail` returned; handler marks Partial and stops loop |
| 2 | Archive dir path has no write permission | Service logs `LogError`; handler receives `Fail`; Partial run; admin sees on Retention page |
| 3 | Very wide string field (e.g. `SmsLog.ErrorMessage` with embedded CRLF) | RFC-4180 escaping wraps in double-quotes and escapes internal quotes; newlines preserved within field |
| 4 | Two handlers archive in parallel | Each entity writes to its own subdirectory; no file contention |
| 5 | `typeof(T)` is a navigation property | Use `typeof(T).GetProperties().Where(p => !p.PropertyType.IsClass || p.PropertyType == typeof(string))` to skip navigation properties |

---

## Definition of Done

- [ ] `IArchiveService` interface with `ArchiveBatchAsync<T>` defined
- [ ] `CsvArchiveService` writes RFC-4180 CSV with UTF-8 BOM + schema companion
- [ ] Directory structure `{entity}/{yyyy-MM}/` created on demand
- [ ] Archive failure returns `ArchiveResult.Fail`; never silently loses data
- [ ] All six handlers now receive real `IArchiveService` via DI; archive stubs activate automatically
- [ ] Admin Retention page shows archive location + disk usage
- [ ] `docs/retention-archive-restore.md` created
- [ ] Registered in DI
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
