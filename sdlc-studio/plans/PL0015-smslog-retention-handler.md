# PL0015: SmsLog Retention Handler — Implementation Plan

> **Status:** Draft
> **Story:** [US0096: SmsLog Retention Handler](../stories/US0096-smslog-retention-handler.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Implement `SmsLogRetentionHandler` following the batched-delete pattern established in PL0014. `SmsLog` rows are immutable once written — no status transitions — so the only eligibility criterion is age (`CreatedAt < cutoff`). Adds an archive hook stub: when `RetentionPolicy.ArchiveEnabled = true`, the handler calls `IArchiveService` (US0102) before deleting; until US0102 ships, the stub logs a warning and skips deletion for that batch.

**Pre-existing state:**
- `IEntityRetentionHandler`, `RetentionPreview`, `RetentionResult` types exist (PL0014/US0095).
- `SmsLog` entity has a `CreatedAt` column indexed for efficient range queries.
- `IArchiveService` does NOT exist yet — stub the call using a `null`-coalescing check or optional DI parameter.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Handler registered | `IEntityRetentionHandler`, `EntityName = "SmsLog"` |
| AC2 | Eligibility | `CreatedAt < UtcNow - N days`; all rows eligible (no exempt status) |
| AC3 | Batched delete | 1,000-row batches, 50ms yield |
| AC4 | Dry run | Count + oldest/newest; no deletes; `RunMode = DryRun` |
| AC5 | Archive hook | `ArchiveEnabled = true` → call `IArchiveService` before batch delete; failure blocks delete |
| AC6 | Run logging | `RetentionRun` row per run |
| AC7 | Disabled policy | No-op |

---

## Technical Context

### Key Patterns (from PL0014)
- Inherit the full handler skeleton (policy load, run-log open/close, cutoff, batch loop, single-flight lock) from `SmsQueueRetentionHandler`.
- `SmsLog.CreatedAt` — confirm field name; could be `SentAt` or `LoggedAt` in the entity. Adapt the WHERE clause.

### Archive Hook Stub

Until `IArchiveService` (US0102) ships, inject it as `IArchiveService? _archiveService = null` (optional DI) or via a null-object pattern:

```csharp
if (policy.ArchiveEnabled) {
    if (_archiveService is null) {
        _logger.LogWarning("ArchiveEnabled is set for SmsLog but IArchiveService is not registered. Skipping batch delete for this run.");
        // Do NOT delete — fail safely
        break;
    }
    var archiveResult = await _archiveService.ArchiveBatchAsync("SmsLog", batch, ct);
    if (!archiveResult.Success) {
        // mark Partial, stop loop
        break;
    }
}
```

This ensures: (a) archive-enabled policies fail safely before US0102 ships, (b) no code change needed when US0102 wires in the real service.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Same rationale as PL0014 — deterministic delete pattern; tests cover eligibility, archive hook stub behaviour, dry run.

---

## Implementation Phases

### Phase 1: SmsLogRetentionHandler

**Goal:** Implement handler with batch delete, archive hook stub, dry run, run logging.

- [ ] Create `src/SmartLog.Web/Services/Retention/SmsLogRetentionHandler.cs`.
- [ ] Same structure as `SmsQueueRetentionHandler` (PL0014) with:
  - `EntityName => "SmsLog"`
  - Eligibility: `CreatedAt < {cutoff}` (no status filter)
  - Batch SQL: `DELETE TOP (1000) FROM SmsLogs WHERE CreatedAt < {cutoff}`
    > Note: verify actual table name — EF Core may pluralise to `SmsLogs`.
  - Archive hook: checked before each batch delete (see stub pattern above).
  - `PreviewAsync`: for dry run, also return `OldestRow` and `NewestRow` timestamps (from `MIN(CreatedAt)`, `MAX(CreatedAt)` on eligible set).
- [ ] Constructor injects `ApplicationDbContext`, `ILogger<SmsLogRetentionHandler>`, and `IArchiveService?` (nullable/optional).

**Files:** `Services/Retention/SmsLogRetentionHandler.cs`

### Phase 2: DI Registration

- [ ] In `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IEntityRetentionHandler, SmsLogRetentionHandler>();
  ```

**Files:** `Program.cs`

### Phase 3: Tests

| AC | Test | File |
|----|------|------|
| AC2 | Rows older than window deleted; rows within window preserved | `SmsLogRetentionHandlerTests.cs` |
| AC4 | Dry run returns correct counts, no rows deleted | same |
| AC5 | `ArchiveEnabled = true` with null archive service → warning logged, no delete | same |
| AC5 | `ArchiveEnabled = true` with archive service throwing → batch not deleted, run = Partial | same |
| AC7 | Disabled policy → no-op | same |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | `SmsLog.CreatedAt` field named differently | Check entity; adapt SQL |
| 2 | Archive service present but disk full | Archive call throws/returns failure; batch skipped; run = Partial |
| 3 | Index missing on `CreatedAt` | Handler continues but logs an operational warning; performance degrades |

---

## Definition of Done

- [ ] `SmsLogRetentionHandler` implemented with correct eligibility, batch delete, archive hook stub, dry run, run logging
- [ ] Registered in DI
- [ ] Archive hook stub fails safely when `IArchiveService` not registered
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
