# US0099: AuditLog Retention with Legal-Hold Flag

> **Status:** Done\n> **Marked Done:** 2026-04-26\n> **Owner:** AI Assistant
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin) / school legal representative
**I want** `AuditLog` retention with a legal-hold flag that prevents purge of flagged rows
**So that** audit evidence survives any future RA 10173 breach investigation or internal dispute, while routine aging rows are cleaned up on schedule.

## Context

### Background
`AuditLog` is the security audit trail: login attempts, role changes, student data edits, SMS config changes, retention policy changes (per US0094 AC7). It grows steadily and must be retained long enough to cover legal windows, but indefinitely is excessive. A 3-year default (1,095 days) aligns with typical RA 10173 breach investigation windows.

The legal-hold concept: when an active investigation is underway, rows related to that investigation must not be purged. We implement this as a boolean flag on `AuditLog` itself — set manually (or by a higher-level hold API later), checked by the purge handler.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Legal | RA 10173 evidence retention during breach investigations | Legal-hold must short-circuit purge |
| EP0016 | Future | Full compliance Epic may extend legal-hold with a separate hold entity | Keep the flag implementation simple; future Epic can layer on top |
| US0094 | Foundation | RetentionPolicy row for `AuditLog` exists | Handler reads policy |

---

## Acceptance Criteria

### AC1: LegalHold Column
- **Given** the `AuditLog` entity
- **Then** a new `LegalHold` column is added (bool, default false, non-null)
- **And** an EF Core migration backfills existing rows to `false`

### AC2: Handler Registered
- Implements `IEntityRetentionHandler` with `EntityName = "AuditLog"`

### AC3: Eligibility Honours Legal Hold
- **Given** retention window of N days
- **Then** eligible rows: `CreatedAt < UtcNow - N days AND LegalHold = 0`
- **And** rows with `LegalHold = 1` are NEVER deleted regardless of age

### AC4: Legal-Hold Admin UI
- **Given** I am on the AuditLog Viewer (`/Admin/AuditLog`)
- **Then** each row has a "Legal Hold" toggle (admin-only)
- **When** I toggle it
- **Then** the row's `LegalHold` is updated
- **And** a secondary audit row is created: `Action = "AuditLegalHoldSet"` (or Cleared), referencing the original row
- **And** secondary audit rows can themselves be put on legal hold (no special exemption)

### AC5: Bulk Legal-Hold Action
- **Given** I filter the AuditLog viewer to a set of rows (e.g. all rows related to a specific user / date range / event type)
- **Then** a "Apply Legal Hold to Filtered Set" button is available
- **When** I confirm
- **Then** all matching rows receive `LegalHold = 1`
- **And** a single audit entry records the bulk action with filter parameters

### AC6: Batched Delete + Dry Run + Archive + Logging
- Standard retention contracts from earlier stories; all respect `LegalHold = 1` exclusion

### AC7: Dashboard Surfaces Held Count
- **Given** the Retention admin page row for AuditLog
- **Then** it shows "On legal hold: N rows" informational value (does not affect action buttons)

### AC8: Archive Highly Recommended
- **Given** AuditLog is a compliance table
- **Then** the Retention page shows "Archive strongly recommended" next to the AuditLog row
- **And** the default seed sets `ArchiveEnabled = true` for AuditLog (only entity defaulted to true)

---

## Scope

### In Scope
- `AuditLog.LegalHold` column + migration
- Admin UI: per-row toggle + bulk action on filtered set
- Secondary audit entry on hold change
- `AuditLogRetentionHandler` with LegalHold filter
- Dashboard count of held rows

### Out of Scope
- Hold "cases" / grouping multiple rows under a named investigation — future Epic (EP0016)
- Hold expiration dates — future
- Release-on-close workflow — future

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Bulk hold on huge filter (e.g. everything) | Batched update (1,000 rows); single summary audit entry |
| Held row age exceeds retention by years | Ignored by purge; admin sees row retained indefinitely |
| Attempt to delete a held row via direct API | Blocked with 403; existing UI has no such path, but the guard is defence-in-depth |
| Legal-hold toggle auth | SuperAdmin + Admin only |

---

## Test Scenarios

- [ ] Migration adds LegalHold column, defaults existing rows to false
- [ ] Purge honours LegalHold = 1 (never deletes)
- [ ] Toggling LegalHold writes secondary audit row
- [ ] Bulk hold applies to filtered set + writes summary audit
- [ ] Retention page shows held count
- [ ] Archive-by-default for AuditLog seeded true
- [ ] Non-admin cannot toggle legal hold

---

## Technical Notes

### Files
- **Modify:** `src/SmartLog.Web/Data/Entities/AuditLog.cs`
- **New migration:** `AddLegalHoldToAuditLog`
- **Modify:** `src/SmartLog.Web/Pages/Admin/AuditLog/Index.cshtml(.cs)` — toggle + bulk action
- **New:** `src/SmartLog.Web/Services/Retention/AuditLogRetentionHandler.cs`
- **Modify:** `DbInitializer` — seed AuditLog policy with `ArchiveEnabled = true`
- **Modify:** DI registration

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |
| [US0050](US0050-audit-log-viewer.md) | UI | Done |
| [US0102](US0102-retention-archive-to-file-export.md) | Strongly paired | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — column + UI + handler + bulk action

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
