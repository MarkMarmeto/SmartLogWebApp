# PL0016: Broadcast Retention Handler — Implementation Plan

> **Status:** Draft
> **Story:** [US0097: Broadcast Retention Handler](../stories/US0097-broadcast-retention-handler.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Implement `BroadcastRetentionHandler`. The key distinction vs earlier handlers: `Broadcast` has a FK-dependent child table (`SmsQueue.BroadcastId`). Before deleting a broadcast row, the handler must:

1. Verify no `SmsQueue` child rows remain in active states (Pending/Queued/Processing).
2. Delete remaining terminal-state child `SmsQueue` rows for that broadcast (cascade cleanup).
3. Then delete the broadcast row.

Deletions run in batches of 100 broadcasts at a time (smaller than SmsQueue because each broadcast may cascade to many queue rows).

**Pre-existing state:**
- `IEntityRetentionHandler` + base types defined (PL0014).
- `Broadcast` entity + `SmsQueue.BroadcastId` FK verified to exist in `ApplicationDbContext`.
- Archive hook stub pattern established in PL0015.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Handler registered | `EntityName = "Broadcast"` |
| AC2 | Eligibility | `CreatedAt < UtcNow - N days` AND no active child queue rows |
| AC3 | FK-safe deletion | Terminal child `SmsQueue` rows deleted first; then broadcast rows |
| AC4 | Dry run | Count broadcasts + cascade queue rows that would be deleted |
| AC5 | Archive hook | Broadcast + targeting summary archived before delete when `ArchiveEnabled` |
| AC6 | Policy + logging + concurrency | Same contracts as US0095 |

---

## Technical Context

### FK Relationship
`SmsQueue.BroadcastId` (int?, nullable FK) — broadcasts created via bulk send queue many `SmsQueue` rows with this FK. Check `ApplicationDbContext.OnModelCreating` for the cascade delete configuration. If cascade delete is not configured, the handler must delete child rows explicitly.

### Deletion Order

```
For each batch of eligible broadcasts:
  1. Check: no Pending/Queued/Processing SmsQueue rows for these broadcast IDs
     → if any found: skip those broadcasts (log at Debug)
  2. Delete terminal-state SmsQueue rows WHERE BroadcastId IN (batch IDs)
  3. Delete the broadcast rows
```

Batch size: 100 broadcasts (not 1,000 — each broadcast may cascade hundreds of queue rows).

### Archive Content for Broadcast
When `ArchiveEnabled`: export broadcast fields + `RecipientCount`, `MessageBody`, targeting filter summary. Not per-recipient (that would require joining `SmsQueue` which is already purged or about to be).

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Referential-check logic is the key complexity; test cases drive out edge cases.

---

## Implementation Phases

### Phase 1: BroadcastRetentionHandler

**Goal:** FK-safe batch delete with concurrency guard, dry run, run logging.

- [ ] Create `src/SmartLog.Web/Services/Retention/BroadcastRetentionHandler.cs`.
- [ ] `EntityName => "Broadcast"`.
- [ ] `ExecuteAsync` logic:
  1. Policy check + concurrency guard (same pattern as PL0014).
  2. Compute `cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays)`.
  3. Dry-run path: count eligible broadcasts (age filter); count associated terminal-state queue rows.
  4. Live-run batch loop:
     ```csharp
     // Get a batch of candidate broadcast IDs
     var candidateIds = await _db.Broadcasts
         .Where(b => b.CreatedAt < cutoff)
         .Select(b => b.Id)
         .Take(100)
         .ToListAsync(ct);
     if (!candidateIds.Any()) break;

     // Filter out any with active queue children
     var activeChildBroadcastIds = await _db.SmsQueue
         .Where(q => candidateIds.Contains(q.BroadcastId!.Value)
                  && new[]{"Pending","Queued","Processing"}.Contains(q.Status))
         .Select(q => q.BroadcastId!.Value)
         .Distinct()
         .ToListAsync(ct);
     var safeIds = candidateIds.Except(activeChildBroadcastIds).ToList();

     if (safeIds.Any()) {
         // Archive if enabled
         // Delete terminal child queue rows for safeIds
         await _db.Database.ExecuteSqlInterpolatedAsync($@"
             DELETE FROM SmsQueue
             WHERE BroadcastId IN ({string.Join(',', safeIds)})
               AND Status IN ('Sent','Failed','Cancelled')", ct);
         // Delete broadcast rows
         await _db.Database.ExecuteSqlInterpolatedAsync($@"
             DELETE FROM Broadcasts WHERE Id IN ({string.Join(',', safeIds)})", ct);
         totalAffected += safeIds.Count;
     }
     await Task.Delay(50, ct);
     ```
     > Note: Use parameterized SQL for the IN list — avoid string interpolation of IDs directly. Either use `EF.CompileQuery` or issue separate commands per ID when the batch is small. For production correctness, prefer: pass IDs as a temporary table or use `.Where(b => safeIds.Contains(b.Id))` + `ExecuteDeleteAsync` (EF Core 7+).
  5. Update `RetentionRun` + `RetentionPolicy.LastRunAt`.

- [ ] Confirm EF Core `ExecuteDeleteAsync` is available (EF Core 7+); prefer over raw SQL for type safety:
  ```csharp
  await _db.Broadcasts.Where(b => safeIds.Contains(b.Id)).ExecuteDeleteAsync(ct);
  ```

**Files:** `Services/Retention/BroadcastRetentionHandler.cs`

### Phase 2: DI Registration

- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IEntityRetentionHandler, BroadcastRetentionHandler>();
  ```

### Phase 3: Tests

| AC | Test | File |
|----|------|------|
| AC2 | Broadcast with all queue rows in terminal state → deleted | `BroadcastRetentionHandlerTests.cs` |
| AC2 | Broadcast with 1 pending queue row → skipped | same |
| AC3 | Child queue rows for deleted broadcast are also removed | same |
| AC4 | Dry run counts both broadcast + cascade rows | same |
| AC6 | `Enabled = false` → no-op | same |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Broadcast with no queue children (e.g. emergency broadcast with 0 recipients) | No child delete needed; broadcast row deleted directly |
| 2 | `SmsQueue.BroadcastId` is nullable — orphan queue rows with null BroadcastId | Handler ignores these; US0095 (SmsQueue handler) handles them by age |
| 3 | Cascade delete configured in DB | If EF OnDelete = Cascade, child rows auto-delete; skip explicit child delete step. Check `OnModelCreating` first. |
| 4 | Very large broadcast (100k recipients) | Archive summary only (counts + message body); batch still capped at 100 broadcasts |

---

## Definition of Done

- [ ] `BroadcastRetentionHandler` with FK-safe deletion order implemented
- [ ] Skips broadcasts with active queue children
- [ ] Deletes terminal-state child `SmsQueue` rows before broadcast row
- [ ] Dry run counts both levels
- [ ] Archive hook stub wired
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
