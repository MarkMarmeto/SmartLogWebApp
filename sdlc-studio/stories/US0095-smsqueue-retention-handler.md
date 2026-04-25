# US0095: SmsQueue Retention Handler

> **Status:** Draft
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin)
**I want** processed SmsQueue rows older than the configured retention window to be purged in safe batches
**So that** the `SmsQueue` table stays small and queue queries remain fast, without disrupting live SMS processing.

## Context

### Background
`SmsQueue` accumulates fast: ~570 NO_SCAN_ALERT + opt-in ENTRY/EXIT rows daily, plus broadcast bursts. Processed rows (Status = Sent / Failed / Cancelled) have no operational purpose after a short window — delivery follow-up moves to `SmsLog`. Keeping them indefinitely bloats the table and slows queue polling queries.

This handler deletes processed rows older than the configured window, in batches, skipping rows that are still Pending or Queued.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0017 | Operational | Purge must never lock `SmsQueue` during active send windows | Batched deletes, off-hours recommended |
| TRD | Architecture | `SmsWorkerService` polls `SmsQueue` every 5s | Handler must not contend with worker |
| US0094 | Data | `RetentionPolicy` for `SmsQueue` exists | Handler reads policy at runtime |

---

## Acceptance Criteria

### AC1: Handler Interface
- **Given** the handler is registered
- **Then** it implements `IEntityRetentionHandler` with:
  - `string EntityName => "SmsQueue"`
  - `Task<RetentionPreview> PreviewAsync(CancellationToken ct)`
  - `Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct)`

### AC2: Eligibility Filter
- **Given** a retention window of N days
- **Then** eligible rows match: `Status IN ('Sent', 'Failed', 'Cancelled') AND CreatedAt < UtcNow - N days`
- **And** rows with `Status IN ('Pending', 'Queued', 'Processing')` are NEVER deleted regardless of age

### AC3: Batch Deletion
- **Given** a large set of eligible rows
- **Then** deletions run in batches of 1,000 rows
- **And** each batch commits in its own transaction
- **And** the handler yields briefly (≤50ms) between batches to avoid locking

### AC4: Idempotent Re-runs
- **Given** the handler ran earlier today
- **When** it runs again
- **Then** no rows are re-processed (they were already deleted)
- **And** the run completes successfully with `RowsAffected = 0` if nothing new is eligible

### AC5: Dry Run
- **Given** `dryRun = true`
- **Then** the handler returns the count and date range of rows that *would* be deleted
- **And** writes a `RetentionRun` row with `RunMode = DryRun`
- **And** no actual deletes occur

### AC6: RetentionRun Logging
- **Given** the handler runs (scheduled, manual, or dry-run)
- **Then** it writes a `RetentionRun` row with: EntityName, RunMode, StartedAt, CompletedAt, Status, RowsAffected, DurationMs, TriggeredBy
- **And** on error, `Status = Failed` and `ErrorMessage` captures the exception

### AC7: Policy Enabled Flag
- **Given** the `SmsQueue` policy has `Enabled = false`
- **When** the handler is invoked
- **Then** it exits early with a no-op run log (`Status = Success`, `RowsAffected = 0`, note: "Disabled")

### AC8: Concurrency Guard
- **Given** a retention run is in progress for `SmsQueue`
- **When** another invocation is triggered
- **Then** the second invocation exits without action (single-flight guard)

---

## Scope

### In Scope
- `SmsQueueRetentionHandler` implementation
- Batched delete
- Dry-run support
- Run logging
- Single-flight guard (semaphore keyed on entity name)

### Out of Scope
- Archive-to-file (US0102 adds that across all handlers)
- Purging `SmsLog` (US0096)
- Scheduling the run (US0101)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Very large backlog (e.g. 5M rows eligible on first run) | Runs in batches; may span hours; admin sees progress via `RetentionRun` rows or logging |
| DB timeout on batch | Current batch rolls back; run status = Partial; next run resumes from next eligible row |
| Clock skew between app and DB | Handler uses DB-side `GETUTCDATE()` for the cutoff to avoid skew |
| Row transitions from Pending → Sent mid-run | New transition is not eligible until it also ages past the window; no contention |

---

## Test Scenarios

- [ ] Only Sent/Failed/Cancelled rows older than window are deleted
- [ ] Pending rows of any age are never deleted
- [ ] Batched delete runs in 1,000-row chunks
- [ ] Dry run returns correct count and no deletes
- [ ] Disabled policy exits no-op
- [ ] Concurrent invocation guarded (second exits)
- [ ] Error during batch → run logged as Partial with error message
- [ ] Idempotent re-run

---

## Technical Notes

### Files to Create / Modify
- **New:** `src/SmartLog.Web/Services/Retention/IEntityRetentionHandler.cs` — interface + `RetentionPreview` + `RetentionResult` types
- **New:** `src/SmartLog.Web/Services/Retention/SmsQueueRetentionHandler.cs`
- **Modify:** `src/SmartLog.Web/Program.cs` — register handler in DI

### Implementation Sketch
```csharp
public async Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct) {
    var policy = await _db.RetentionPolicies.SingleAsync(p => p.EntityName == EntityName, ct);
    if (!policy.Enabled) return RetentionResult.Disabled();

    var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
    var totalAffected = 0;

    while (!ct.IsCancellationRequested) {
        var batchSize = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE TOP (1000) FROM SmsQueue
            WHERE Status IN ('Sent','Failed','Cancelled')
              AND CreatedAt < {cutoff}", ct);
        if (batchSize == 0) break;
        totalAffected += batchSize;
        if (dryRun) return RetentionResult.PreviewOnly(totalAffected);
        await Task.Delay(50, ct);
    }
    return RetentionResult.Success(totalAffected);
}
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | RetentionPolicy + RetentionRun entities | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — batched delete pattern must be tested against realistic volume

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 retention planning session |
