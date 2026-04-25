# US0102: Archive-to-File Export Before Purge

> **Status:** Draft
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin) / school legal representative
**I want** retention purges to optionally export rows to a CSV file before deletion
**So that** we retain an offline, restorable record of purged data for compliance evidence (especially AuditLog and SmsLog) without keeping the rows in the hot database.

## Context

### Background
EP0017 handlers expose an `ArchiveEnabled` flag on each `RetentionPolicy`. This story provides the archive mechanism all handlers call into.

Design: per-entity, per-run CSV file written to a configured archive directory, filename-encoded with entity + timestamp + row range for easy retrieval. Archive failure blocks the associated delete (covered in each handler's contract).

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0017 | Compliance | Archive must be restorable | CSV is machine-readable; schema snapshot included |
| TRD | Deployment | LAN deployment with local disk | Default archive dir is a local path, configurable |
| Security | PII | Archives may contain personal data (phone numbers) | Archive dir must be access-controlled; documented |

---

## Acceptance Criteria

### AC1: IArchiveService Interface
- **Given** the retention subsystem
- **Then** a new `IArchiveService` interface exists with:
  - `Task<ArchiveResult> ArchiveBatchAsync<T>(string entityName, IEnumerable<T> rows, CancellationToken ct)`
  - Returns file path + row count on success

### AC2: CSV Output Format
- **Given** a batch to archive
- **Then** the service writes a CSV to `{ArchiveDir}/{EntityName}/{yyyy}-{MM}/{entityName}-{yyyyMMdd-HHmmss}-{batchIndex}.csv`
- **And** the first line is a header row with all column names
- **And** values are UTF-8 encoded, RFC-4180 escaped

### AC3: Schema Companion File
- **Given** the first archive file of the day for an entity
- **Then** a sibling `.schema.json` is written alongside with column names + types
- **And** subsequent batches the same day reuse this schema (no rewrite)

### AC4: Configuration
- **Given** the archive setting keys
- **Then** `Retention:ArchiveDirectory` (default `"./archives"`) and `Retention:ArchiveFormat` (default `"csv"`, reserved for future `"json"`) are exposed
- **And** the directory is created on first use if missing

### AC5: Archive Triggers Delete
- **Given** a handler calls `ArchiveBatchAsync` and it returns success
- **Then** the handler proceeds with the batch delete
- **Given** archive returns failure (disk full, permission error)
- **Then** the handler does NOT delete the batch
- **And** the run is marked `Status = Partial` with error details

### AC6: Archive File Retention
- **Given** archive files on disk
- **Then** a separate policy `Retention:ArchiveFileDays` (default `null` = keep forever) governs cleanup of archive files themselves
- **And** a helper script / admin action to cull old archive files is available (can be a later story if out-of-scope here)

### AC7: Admin UI — Archive Directory Link
- **Given** the Retention admin page
- **Then** a section "Archive location: `{ArchiveDir}`" is displayed
- **And** the page shows disk-usage summary for the archive dir (top-level only, no traversal)

### AC8: Restore Documentation
- **Given** archive files
- **Then** `docs/retention-archive-restore.md` documents:
  - CSV + schema.json format
  - How to load into a staging DB table with `BULK INSERT` or EF manual import
  - Caveats: FK integrity may not restore cleanly; archives are audit evidence, not live restore

---

## Scope

### In Scope
- `IArchiveService` + CSV implementation
- Schema companion file
- Configuration settings
- Admin UI dir + disk-usage surface
- Restore documentation

### Out of Scope
- JSON archive format (setting reserved, not implemented)
- Compression (.gz) — could be added later; CSV is readable as-is
- Automatic restore-to-live-DB (evidence only, not operational restore)
- Cloud upload (S3/Azure) — LAN deployment, out of scope

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Disk full during archive | Archive fails; batch not deleted; error captured in `RetentionRun` |
| Archive dir path missing permissions | Handler fails at service start; clear log; admin sees error on Retention page |
| Extremely wide columns (e.g. SmsLog.ErrorMessage) | CSV escaping handles embedded commas/newlines per RFC-4180 |
| Restore attempt by admin | Documentation makes clear the archive is not a hot-restore; staging DB load is the recommended path |

---

## Test Scenarios

- [ ] CSV written with correct header + escaped values
- [ ] Schema JSON written once per day per entity
- [ ] Directory structure follows `{entity}/{yyyy-MM}/` pattern
- [ ] Archive failure blocks delete
- [ ] Configured archive dir honoured
- [ ] Disk usage summary displayed on Retention page

---

## Technical Notes

### Files
- **New:** `src/SmartLog.Web/Services/Retention/IArchiveService.cs`
- **New:** `src/SmartLog.Web/Services/Retention/CsvArchiveService.cs`
- **Modify:** all handler implementations (US0095-US0100) — inject and call `IArchiveService` when `ArchiveEnabled`
- **Modify:** `Program.cs` — DI registration
- **New doc:** `docs/retention-archive-restore.md`
- **Modify:** `Pages/Admin/Settings/Retention.cshtml(.cs)` — disk-usage widget

### CSV Library
- Prefer a lightweight CSV writer (e.g. `CsvHelper`) already in use — check dependencies first; if not, hand-written writer with RFC-4180 escaping is acceptable

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |
| US0095-US0100 | Consumers | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium — CSV writer + integration points in existing handlers

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
