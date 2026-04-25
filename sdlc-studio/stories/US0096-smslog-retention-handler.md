# US0096: SmsLog Retention Handler

> **Status:** Draft
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin)
**I want** `SmsLog` rows older than the configured window to be purged (optionally archived first) in safe batches
**So that** delivery-history storage stays bounded while still covering the typical parent-complaint / billing-reconciliation review window.

## Context

### Background
`SmsLog` is the authoritative SMS delivery audit: provider, provider message ID, delivery status, processing time, error details. It grows in lockstep with `SmsQueue`. The default window (180 days) covers two full billing cycles and most delivery dispute windows.

Unlike `SmsQueue`, `SmsLog` rows are immutable once written — no row ever transitions back to an "active" state. That makes the purge simpler: age is the only eligibility criterion.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0017 | Compliance | Log rows may be needed for billing disputes | Default window ≥ 180 days; archive option available |
| TRD | Data | `SmsLog` has an index on `CreatedAt` already | Purge queries can use it efficiently |
| US0094 | Foundation | RetentionPolicy row for `SmsLog` exists | Handler reads policy |

---

## Acceptance Criteria

### AC1: Handler Registered
- **Given** the handler is registered
- **Then** it implements `IEntityRetentionHandler` with `EntityName = "SmsLog"`

### AC2: Eligibility
- **Given** retention window of N days
- **Then** eligible rows: `CreatedAt < UtcNow - N days`
- **And** no row is exempt (all rows are immutable logs)

### AC3: Batched Delete
- **Given** eligible rows exist
- **Then** deletion runs in 1,000-row batches with a 50ms yield between batches

### AC4: Dry Run Support
- **Given** `dryRun = true`
- **Then** the handler returns count + oldest/newest timestamps in the would-delete set, and writes a `RetentionRun` with `RunMode = DryRun`; no deletes occur

### AC5: Archive Integration Hook
- **Given** `RetentionPolicy.ArchiveEnabled = true`
- **Then** before deletion, the handler invokes the archive path (US0102) to export the batch to file
- **And** archive failure prevents deletion for that batch (the batch is retried next run)

### AC6: Run Logging
- **Given** the handler runs
- **Then** a `RetentionRun` row is written (same contract as US0095 AC6)

### AC7: Respects Policy Enabled Flag
- **Given** policy `Enabled = false`
- **Then** the handler exits no-op (same as US0095)

### AC8: Preserves Index Health
- **Given** a large purge run
- **Then** after the run, `SmsLog` index fragmentation is not significantly worse (validation: measure before/after on a test DB)

---

## Scope

### In Scope
- `SmsLogRetentionHandler`
- Batched delete
- Dry run
- Archive hook (to be wired when US0102 lands; until then the hook is a no-op stub that logs a warning if `ArchiveEnabled` is set)

### Out of Scope
- Retention of the archive file itself (US0102 handles)
- Restoring from archive

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Archive write fails | Current batch not deleted; run status = Partial; error captured |
| Extremely large backlog | Multi-hour run; admin sees progress in log |
| `CreatedAt` index missing | Handler logs an operational warning; continues but slowly |

---

## Test Scenarios

- [ ] Rows older than window are deleted in batches
- [ ] Rows within window are preserved
- [ ] Dry run returns counts without deleting
- [ ] Archive-enabled flag invokes archive hook (stub tested, real path tested in US0102)
- [ ] Archive failure blocks delete and logs Partial
- [ ] Disabled policy → no-op

---

## Technical Notes

### Files to Create / Modify
- **New:** `src/SmartLog.Web/Services/Retention/SmsLogRetentionHandler.cs`
- **Modify:** `Program.cs` — DI registration

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |
| [US0102](US0102-retention-archive-to-file-export.md) | Optional integration | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
