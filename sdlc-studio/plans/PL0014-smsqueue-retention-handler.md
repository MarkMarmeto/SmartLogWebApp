# PL0014: SmsQueue Retention Handler — Implementation Plan

> **Status:** Done
> **Story:** [US0095: SmsQueue Retention Handler](../stories/US0095-smsqueue-retention-handler.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Define the `IEntityRetentionHandler` interface (shared by all six handlers), then implement `SmsQueueRetentionHandler` as the first concrete handler. The handler purges processed `SmsQueue` rows (Status ∈ {Sent, Failed, Cancelled}) older than the configured window, in 1,000-row batches with a 50ms yield, with dry-run support, run logging, disabled-policy guard, and single-flight concurrency guard.

**Pre-existing state:**
- `RetentionPolicy` + `RetentionRun` entities exist (PL0013/US0094).
- `SmsQueue` entity in `Data/Entities/SmsQueue.cs`; `Status` is an enum or string — confirm during implementation.
- `SmsWorkerService` polls `SmsQueue` every 5s; batched deletes commit independently and do not conflict with the polling writes.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Handler interface | `IEntityRetentionHandler` with `EntityName`, `PreviewAsync`, `ExecuteAsync` |
| AC2 | Eligibility filter | Only Sent/Failed/Cancelled rows older than window; Pending/Queued/Processing never touched |
| AC3 | Batch deletion | 1,000-row batches; each commits separately; 50ms yield between |
| AC4 | Idempotent re-run | Second run finds 0 eligible rows; returns `RowsAffected = 0` |
| AC5 | Dry run | Returns count + date range; no deletes; `RetentionRun.RunMode = DryRun` |
| AC6 | RetentionRun logging | Row written for every run including errors |
| AC7 | Disabled policy | Handler exits no-op immediately |
| AC8 | Concurrency guard | Second concurrent invocation exits without action |

---

## Technical Context

### Key Existing Patterns
- **Hosted service pattern:** `NoScanAlertService` — use as model for background execution; no need to replicate that lifecycle here (US0101 builds the scheduler).
- **Raw SQL in EF Core:** `_db.Database.ExecuteSqlInterpolatedAsync(...)` — used elsewhere in the codebase for bulk operations.
- **SmsQueue Status values:** Check `Data/Entities/SmsQueue.cs` or `SmsWorkerService.cs` — likely `SmsStatus` enum with values: `Pending`, `Queued`, `Processing`, `Sent`, `Failed`, `Cancelled`. Use the string representations matching the DB column.
- **Single-flight pattern:** `SemaphoreSlim(1, 1)` per entity, held for duration of run; try-enter with `WaitAsync(0)` to skip if locked.

### RetentionPreview and RetentionResult types

Define alongside `IEntityRetentionHandler`:

```csharp
public record RetentionPreview(int EligibleRows, DateTime? OldestRow, DateTime? NewestRow);

public record RetentionResult(
    string Status,      // "Success" | "Partial" | "Disabled" | "Skipped"
    int RowsAffected,
    string? Note = null,
    string? ErrorMessage = null)
{
    public static RetentionResult Success(int rows) => new("Success", rows);
    public static RetentionResult Disabled() => new("Success", 0, "Policy disabled");
    public static RetentionResult Skipped() => new("Success", 0, "Concurrent run in progress");
    public static RetentionResult Partial(int rows, string err) => new("Partial", rows, ErrorMessage: err);
}
```

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** The batched-delete pattern is deterministic; unit tests with an in-memory or real test-DB are straightforward post-implementation.

---

## Implementation Phases

### Phase 1: IEntityRetentionHandler Interface

**Goal:** Define the shared contract all six handlers implement.

- [ ] Create `src/SmartLog.Web/Services/Retention/IEntityRetentionHandler.cs`:
  ```csharp
  public interface IEntityRetentionHandler {
      string EntityName { get; }
      Task<RetentionPreview> PreviewAsync(CancellationToken ct);
      Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct);
  }
  ```
- [ ] Define `RetentionPreview` and `RetentionResult` records in the same file.

**Files:** `Services/Retention/IEntityRetentionHandler.cs`

### Phase 2: SmsQueueRetentionHandler

**Goal:** Implement the first concrete handler.

- [ ] Create `src/SmartLog.Web/Services/Retention/SmsQueueRetentionHandler.cs`:
  ```csharp
  public class SmsQueueRetentionHandler : IEntityRetentionHandler {
      public string EntityName => "SmsQueue";
      private readonly ApplicationDbContext _db;
      private readonly ILogger<SmsQueueRetentionHandler> _logger;
      private static readonly SemaphoreSlim _lock = new(1, 1);

      // PreviewAsync: query COUNT(*), MIN(CreatedAt), MAX(CreatedAt) of eligible rows (no delete)
      // ExecuteAsync: batch-delete loop with 50ms yield; writes RetentionRun row
  }
  ```
- [ ] `ExecuteAsync` implementation:
  1. Try-acquire `_lock` with `WaitAsync(0)` — return `RetentionResult.Skipped()` if already held.
  2. Load policy from `_db.RetentionPolicies`. If `!policy.Enabled` return `RetentionResult.Disabled()`.
  3. Write opening `RetentionRun` row: `StartedAt = DateTime.UtcNow`, `RunMode = dryRun ? "DryRun" : "Manual"` (scheduler sets "Scheduled" — see US0101).
  4. Compute `cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays)`.
  5. If `dryRun`:
     ```csharp
     var count = await _db.SmsQueue
         .Where(q => new[]{"Sent","Failed","Cancelled"}.Contains(q.Status) && q.CreatedAt < cutoff)
         .CountAsync(ct);
     // update RetentionRun; return PreviewOnly
     ```
  6. Batch loop:
     ```csharp
     int batchRows;
     do {
         batchRows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
             DELETE TOP (1000) FROM SmsQueue
             WHERE Status IN ('Sent','Failed','Cancelled')
               AND CreatedAt < {cutoff}", ct);
         totalAffected += batchRows;
         if (batchRows > 0) await Task.Delay(50, ct);
     } while (batchRows > 0 && !ct.IsCancellationRequested);
     ```
  7. Update `RetentionRun`: `CompletedAt`, `Status`, `RowsAffected`, `DurationMs`.
  8. Update `RetentionPolicy.LastRunAt` + `LastRowsAffected`.
  9. Release `_lock`.
  10. On exception: catch, update `RetentionRun.Status = "Failed"`, `ErrorMessage`, re-release lock.
- [ ] `PreviewAsync`: calls `ExecuteAsync(dryRun: true, ct)` and returns the preview data.

**Files:** `Services/Retention/SmsQueueRetentionHandler.cs`

### Phase 3: DI Registration

**Goal:** Register the handler and interface.

- [ ] In `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IEntityRetentionHandler, SmsQueueRetentionHandler>();
  ```

**Files:** `Program.cs`

### Phase 4: Tests

| AC | Test | File |
|----|------|------|
| AC2 | Only Sent/Failed/Cancelled rows older than window deleted; Pending never touched | `SmsQueueRetentionHandlerTests.cs` |
| AC3 | Batched — two calls each deleting ≤1000 rows for a 1500-row set | same |
| AC4 | Re-run after purge: `RowsAffected = 0` | same |
| AC5 | Dry run returns count, no rows deleted | same |
| AC7 | Disabled policy → no-op RetentionResult | same |
| AC8 | Second concurrent call → Skipped | same |

- [ ] Run `dotnet test`; confirm zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | `SmsQueue.Status` stored as enum int vs string | Confirm column type; if enum-int, adapt the SQL `IN` list to use integer values |
| 2 | DB timeout mid-batch | Transaction for that batch rolls back; `RetentionRun.Status = "Partial"` written; `CancellationToken` propagated |
| 3 | `CreatedAt` vs `ProcessedAt` for cutoff | Story specifies `CreatedAt`; confirm field name in entity |
| 4 | No `CreatedAt` on `SmsQueue` | If field is named differently (e.g. `QueuedAt`), adapt WHERE clause |

---

## Definition of Done

- [ ] `IEntityRetentionHandler` + `RetentionPreview` + `RetentionResult` types defined
- [ ] `SmsQueueRetentionHandler` implemented with batch delete, dry run, run logging, policy guard, concurrency guard
- [ ] Registered in DI
- [ ] Tests for all listed ACs; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
