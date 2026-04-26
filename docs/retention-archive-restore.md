# Retention Archive: File Format & Restore Guide

> **Scope:** EP0017 – Data Retention & Archival (SmartLog Web App)

## Overview

When a retention policy has **Archive** enabled, rows are written to CSV files before being deleted from the database. This document describes the file format, directory structure, and how to restore data from archives for audit or recovery purposes.

---

## Directory Structure

```
{ArchiveDirectory}/              ← default: ./archives (config: Retention:ArchiveDirectory)
  SmsLog/
    2026-04/
      smslog-20260424-023145-0.csv
      smslog-20260424-023145-0.schema.json  ← written once per entity per day
      smslog-20260424-023245-1.csv
  Broadcast/
    2026-04/
      broadcast-20260424-030012-0.csv
      broadcast-20260424-030012-0.schema.json
  AuditLog/
    2026-04/
      auditlog-20260424-020033-0.csv
      auditlog-20260424-020033-0.schema.json
```

**File naming:** `{entityName.lower}-{yyyyMMdd-HHmmss}-{batchIndex}.csv`

---

## CSV File Format

- **Encoding:** UTF-8 with BOM (byte order mark: `EF BB BF`) — for Excel compatibility
- **Line endings:** CRLF (`\r\n`) per RFC 4180
- **Field separator:** comma (`,`)
- **Quoting:** Fields containing commas, double-quotes, or newlines are wrapped in double-quotes (`"`)
- **Quote escaping:** Internal double-quotes are doubled (`""`)
- **Header row:** First row contains column names in alphabetical order

### Example (`SmsLog`)

```csv
CreatedAt,DeliveryStatus,ErrorMessage,Id,MessageType,ProviderMessageId,QueueId
2026-01-15T03:22:11Z,Delivered,,1234,ENTRY,SMPH-789012-A,5678
2026-01-15T03:22:15Z,Failed,"Network timeout, retry exhausted",,BROADCAST,,5679
```

---

## Schema JSON Companion

One `.schema.json` file is written per entity per calendar day (UTC). It describes the column types for the CSV exported that day.

```json
{
  "entityName": "SmsLog",
  "generatedAt": "2026-04-24T02:31:45.0000000Z",
  "columns": [
    { "name": "CreatedAt", "type": "datetime2" },
    { "name": "DeliveryStatus", "type": "nvarchar" },
    { "name": "ErrorMessage", "type": "nvarchar (nullable)" },
    { "name": "Id", "type": "bigint" },
    { "name": "MessageType", "type": "nvarchar" },
    { "name": "ProviderMessageId", "type": "nvarchar (nullable)" },
    { "name": "QueueId", "type": "bigint (nullable)" }
  ]
}
```

**Type mapping (C# → SQL Server):**

| C# Type | SQL Type |
|---------|----------|
| `long` / `long?` | `bigint` |
| `int` / `int?` | `int` |
| `bool` / `bool?` | `bit` |
| `DateTime` / `DateTimeOffset` | `datetime2` |
| `Guid` | `uniqueidentifier` |
| `double` / `float` / `decimal` | `decimal` |
| `string` / other | `nvarchar` |

---

## Loading Archives into a Database

### Option 1: SQL Server BULK INSERT

```sql
-- Create a staging table matching the CSV columns
CREATE TABLE SmsLog_Restore (
    CreatedAt     datetime2,
    DeliveryStatus nvarchar(50),
    ErrorMessage  nvarchar(max),
    Id            bigint,
    MessageType   nvarchar(50),
    ProviderMessageId nvarchar(100),
    QueueId       bigint
);

BULK INSERT SmsLog_Restore
FROM 'C:\archives\SmsLog\2026-04\smslog-20260424-023145-0.csv'
WITH (
    FORMAT           = 'CSV',
    FIRSTROW         = 2,           -- skip header
    CODEPAGE         = '65001',     -- UTF-8
    FIELDTERMINATOR  = ',',
    ROWTERMINATOR    = '\n',
    TABLOCK
);
```

> **Note:** Remove the UTF-8 BOM from the file if BULK INSERT rejects it: open in a text editor and re-save without BOM, or use `bcp` with explicit format.

### Option 2: Manual EF Core Import

```csharp
// Read CSV and re-insert into staging context
using var reader = new StreamReader(archiveFilePath, new UTF8Encoding(detectEncodingFromByteOrderMarks: true));
var header = await reader.ReadLineAsync();   // skip header
while (!reader.EndOfStream)
{
    var line = await reader.ReadLineAsync();
    // parse line (respect RFC-4180 quoting)
    // map columns to entity
    // context.StagingTable.Add(entity);
}
await context.SaveChangesAsync();
```

---

## Caveats & Limitations

1. **Not an operational restore** — Archives are audit evidence. Re-inserting archive rows may violate FK constraints (e.g. a student referenced in `SmsLog` may have been deleted). Use a separate staging schema for investigations.

2. **Partial batches** — If a retention run fails mid-way, some batches may be archived but not deleted (the handler keeps the row in the database). Re-running retention will archive them again with a new file.

3. **Column order** — CSV columns are sorted alphabetically (not by original DB column order). Always use the header row or schema JSON to map columns correctly.

4. **Nulls** — Null values are written as empty strings in the CSV. Use the schema JSON to identify nullable columns.

---

## Security Notice

Archive files may contain **personal data** (e.g. phone numbers in `SmsLog`, IP addresses in `AuditLog`). Restrict OS-level access to the archive directory to the application service account only. Do not place the archive directory in a web-accessible path.

```
# Restrict on Linux/macOS
chown smartlog:smartlog ./archives
chmod 700 ./archives
```

---

## Archive Retention

The `Retention:ArchiveFileDays` configuration key is reserved for future automatic cleanup of old archive files. Currently, archive files are kept indefinitely. Monitor disk usage via the **Archive Location** section on the `/Admin/Settings/Retention` page.
