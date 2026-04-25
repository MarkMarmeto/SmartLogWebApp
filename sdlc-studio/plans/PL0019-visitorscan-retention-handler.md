# PL0019: VisitorScan Retention Handler — Implementation Plan

> **Status:** Draft
> **Story:** [US0100: VisitorScan Retention Handler](../stories/US0100-visitorscan-retention-handler.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Implement `VisitorScanRetentionHandler` — the simplest of the six handlers. `VisitorScan` rows are standalone (no blocking FK constraints on child tables), and there is no status column — all rows are eligible by age alone. The only nuance: include the `CameraIndex` + `CameraName` columns (added in US0093/PL0011) in the archive output.

**Pre-existing state:**
- `IEntityRetentionHandler` + base types defined (PL0014).
- `VisitorScan` entity has `ScannedAt`, `VisitorPassId` (FK — but `VisitorPass` is retained permanently and is not affected by purging `VisitorScan` rows).
- `VisitorScan.CameraIndex` + `CameraName` exist (PL0011).
- Archive hook stub pattern established in PL0015.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Handler registered | `EntityName = "VisitorScan"` |
| AC2 | Eligibility | `ScannedAt < UtcNow - N days` |
| AC3 | FK safety | `VisitorPass` and `Device` rows not affected |
| AC4 | Batched delete + dry run + archive + logging | Standard contracts |
| AC5 | Camera identity in archive | `CameraIndex` + `CameraName` included in archived rows |

---

## Technical Context

### FK Safety
`VisitorScan.VisitorPassId` is a FK to `VisitorPass`. Deleting `VisitorScan` rows does not affect `VisitorPass` rows (the pass stays in the pool). No cascade or constraint issue. Similarly, `DeviceId` FK to `Device` is unaffected.

### ScannedAt field type
Check `VisitorScan.ScannedAt` — likely `DateTimeOffset` (same as `Scan.ScannedAt`). Use the same cutoff pattern as `ScanRetentionHandler`.

### Camera Fields in Archive
When `ArchiveService.ArchiveBatchAsync<VisitorScan>` is called, the serialisation of `VisitorScan` includes all properties including `CameraIndex` and `CameraName`. No special handling needed beyond using the full entity.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Simplest handler — straightforward age-based delete, no special filters.

---

## Implementation Phases

### Phase 1: VisitorScanRetentionHandler

**Goal:** Age-based batch delete handler.

- [ ] Create `src/SmartLog.Web/Services/Retention/VisitorScanRetentionHandler.cs`.
- [ ] `EntityName => "VisitorScan"`.
- [ ] Batch SQL:
  ```sql
  DELETE TOP (1000) FROM VisitorScans WHERE ScannedAt < {cutoff}
  ```
  > Verify table name from `ApplicationDbContext` (EF Core may pluralise to `VisitorScans`).
- [ ] Same structure as `ScanRetentionHandler`: single-flight lock, policy check, dry-run path, batch loop with 50ms yield, run logging, archive hook stub.
- [ ] `PreviewAsync`: `COUNT(*)`, `MIN(ScannedAt)`, `MAX(ScannedAt)` on eligible set.
- [ ] Archive content: full `VisitorScan` row projection including `CameraIndex` + `CameraName` — these are naturally included when the entity is passed to `ArchiveService`.

**Files:** `Services/Retention/VisitorScanRetentionHandler.cs`

### Phase 2: DI Registration

- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IEntityRetentionHandler, VisitorScanRetentionHandler>();
  ```

### Phase 3: Tests

| AC | Test | File |
|----|------|------|
| AC2 | Old visitor scans purged; young preserved | `VisitorScanRetentionHandlerTests.cs` |
| AC2 | `VisitorPass` rows unaffected after purge | same |
| AC4 | Dry run returns count without deleting | same |
| AC4 | Archive hook stub logs warning when `ArchiveEnabled = true` and no service | same |
| AC1 | `Enabled = false` → no-op | same |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | `VisitorScan.ScannedAt` is `DateTimeOffset` | Use `DateTimeOffset.UtcNow.AddDays(-N)` for cutoff |
| 2 | DB cascade delete configured for VisitorScan → VisitorPass | Unlikely (VisitorPass persists); verify OnModelCreating; no action needed |
| 3 | CameraIndex/CameraName null on older rows (pre-US0093) | Archive CSV handles null values as empty fields; no issue |

---

## Definition of Done

- [ ] `VisitorScanRetentionHandler` implemented with `ScannedAt` cutoff, batch delete, archive hook stub, run logging
- [ ] Camera identity fields (CameraIndex, CameraName) naturally included in archive output
- [ ] Registered in DI
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
