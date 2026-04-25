# US0097: Broadcast Retention Handler

> **Status:** Draft
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin)
**I want** `Broadcast` rows older than the configured window to be purged safely, leaving no orphan or dangling references in `SmsQueue` or `SmsLog`
**So that** the admin broadcast history stays manageable (default: one school year) while the already-purged queue/log data remains internally consistent.

## Context

### Background
`Broadcast` represents admin-authored bulk sends (Announcement, Emergency, BulkSend). The row is small but carries the message body and audit fields (who sent, when, targeting summary). Retention is longer than `SmsQueue` because admins occasionally reference past broadcasts, but a school year is a reasonable upper bound for active operational need.

The handler must check referential consistency: a broadcast whose queued messages have not yet been processed must not be purged.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0017 | Correctness | No purge of a broadcast with un-sent queue rows | Check before delete |
| TRD | Data | `SmsQueue.BroadcastId` FK exists | Use to guard |
| US0094 | Foundation | RetentionPolicy row for `Broadcast` exists | Handler reads policy |

---

## Acceptance Criteria

### AC1: Handler Registered
- Implements `IEntityRetentionHandler` with `EntityName = "Broadcast"`

### AC2: Eligibility
- **Given** retention window of N days
- **Then** eligible rows: `CreatedAt < UtcNow - N days` AND no related `SmsQueue` row remains with Status IN ('Pending','Queued','Processing')
- **And** broadcasts with fully-processed or purged queue children are safe to delete

### AC3: FK-Safe Deletion
- **Given** eligible broadcasts
- **Then** any remaining `SmsQueue` rows referencing them (all terminal states) are deleted first, in batches
- **And** then the broadcast rows are deleted in batches of 100

### AC4: Dry Run
- Standard dry-run contract (AC5 from US0095)

### AC5: Archive Hook
- When `ArchiveEnabled = true`, export broadcast + targeting summary before delete (via US0102 path)

### AC6: Policy + Logging + Concurrency
- Respects `Enabled`, logs `RetentionRun`, single-flight guarded — same contracts as US0095

---

## Scope

### In Scope
- `BroadcastRetentionHandler`
- FK-safe deletion order
- Archive hook
- Dry-run + run logging

### Out of Scope
- Exporting full recipient list (targeting summary only)
- Cross-linking purge decisions with `SmsLog` retention

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Broadcast with a pending queue row | Not eligible; handler skips and logs |
| Orphan queue rows from earlier crash (no matching broadcast) | Logged as warning; handled by US0095 (SmsQueue handler) |
| Broadcast with very large recipient count | Archive summary only (counts + targeting filter + message body), not per-recipient |

---

## Test Scenarios

- [ ] Old broadcast with all queue rows in terminal state → deleted
- [ ] Old broadcast with 1 pending queue row → skipped
- [ ] Dry run counts both broadcasts and cascade queue rows
- [ ] Archive hook invoked when ArchiveEnabled

---

## Technical Notes

### Files
- **New:** `src/SmartLog.Web/Services/Retention/BroadcastRetentionHandler.cs`
- **Modify:** DI registration

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |
| [US0095](US0095-smsqueue-retention-handler.md) | Related (pattern) | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium — referential check adds care but the pattern is set

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
