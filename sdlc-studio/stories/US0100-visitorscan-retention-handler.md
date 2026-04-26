# US0100: VisitorScan Retention Handler

> **Status:** Done\n> **Marked Done:** 2026-04-26\n> **Owner:** AI Assistant
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin)
**I want** `VisitorScan` rows older than the configured window to be purged
**So that** visitor entry/exit history stays bounded (default: one school year) without polluting the database with legacy visitor activity.

## Context

### Background
`VisitorScan` records entries and exits of anonymous visitor passes (EP0012). Retention does not have the same legal complexity as `Scan` (no personal student data) or `AuditLog`. Default window: 365 days — covers any annual safety-incident review.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0012 | Data | `VisitorScan` references `VisitorPass` and optionally `Device` | Purge must not leave pass/device FK dangling |
| US0094 | Foundation | RetentionPolicy row for `VisitorScan` exists | Handler reads policy |

---

## Acceptance Criteria

### AC1: Handler Registered
- Implements `IEntityRetentionHandler` with `EntityName = "VisitorScan"`

### AC2: Eligibility
- `ScannedAt < UtcNow - N days`

### AC3: FK Safety
- Deletion does not affect `VisitorPass` rows (passes are reusable)
- Deletion does not affect `Device` rows

### AC4: Batched Delete + Dry Run + Archive + Logging
- Standard contracts

### AC5: Camera Identity Columns Carried to Archive
- **Given** the archive path (US0102)
- **Then** when a `VisitorScan` row is archived, the `CameraIndex` + `CameraName` columns (added in US0093) are included

---

## Scope

### In Scope
- `VisitorScanRetentionHandler`
- Dry run, archive hook, run logging

### Out of Scope
- `VisitorPass` retention (passes are a finite admin-managed pool; they are not auto-purged)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Visitor pass with entries only (no exit) | Standard age-based purge; no special handling |
| Purge reveals an orphan pass-in-use state | Out of scope; flagged only, not fixed here |

---

## Test Scenarios

- [ ] Old visitor scans purged
- [ ] Young visitor scans preserved
- [ ] Archive includes camera identity
- [ ] Dry run

---

## Technical Notes

### Files
- **New:** `src/SmartLog.Web/Services/Retention/VisitorScanRetentionHandler.cs`
- **Modify:** DI registration

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
