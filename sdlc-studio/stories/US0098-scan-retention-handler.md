# US0098: Scan Retention Handler

> **Status:** Done\n> **Marked Done:** 2026-04-26\n> **Owner:** AI Assistant
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin)
**I want** `Scan` rows older than the configured window to be purged, while attendance reports warn if a requested date range exceeds the retention window
**So that** the system does not silently return partial results on historical queries, and the scan table stays bounded.

## Context

### Background
`Scan` is the most-written table: every student entry and exit produces a row. At 1,900 students × 2-6 scans/day × 200 school days = 760,000-2,280,000 rows/year. A 2-year default (730 days) keeps ~2 school years of detail for report-backs and enrolment audits.

Unlike SMS tables, scans have a *report consumer* — the Attendance Reports UI. We must prevent silent data loss for admins running reports beyond the retention window.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0017 | Compliance | Attendance records are referenced for enrolment audits | Default window ≥ 730 days |
| TRD | Reports | Historical attendance reports read directly from `Scan` | Reports must know the retention horizon |
| PRD | UX | Admin Amy must not see silent incomplete results | Warn banner on reports |

---

## Acceptance Criteria

### AC1: Handler Registered
- Implements `IEntityRetentionHandler` with `EntityName = "Scan"`

### AC2: Eligibility
- `ScannedAt < UtcNow - N days` (use `ScannedAt`, not `ReceivedAt`, as the authoritative event time)

### AC3: Visitor Scans Paired
- This handler is for `Scan` only. `VisitorScan` has its own handler (US0100) with its own policy row.

### AC4: Batched Delete + Dry Run + Archive + Logging
- Standard contracts from US0095 / US0096

### AC5: Report Boundary Warning
- **Given** the configured Scan retention is 730 days
- **When** Admin Amy requests an Attendance Report spanning a date earlier than `UtcNow - 730 days`
- **Then** the report UI shows a banner: "Data beyond {N} days may have been purged per retention policy. Results may be incomplete."
- **And** the warning links to `/Admin/Settings/Retention`

### AC6: Archive Strongly Recommended
- If `ArchiveEnabled = false`, the Retention page shows a hint on the Scan row: "Archiving recommended for audit compliance"
- Nothing is enforced — admin choice

---

## Scope

### In Scope
- `ScanRetentionHandler`
- Report-date-range warning banner integration
- Archive hook

### Out of Scope
- Rollup / summary retention (e.g. keeping daily counts after raw deletion) — could be a future story
- Bulk re-ingestion from archive

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Report spans both within- and beyond-retention dates | Warning banner shown; rows still returned for the within-retention portion |
| Clock skew on device vs server (`ScannedAt` slightly in the future) | Handler uses UTC; tolerate future-dated rows (never eligible) |
| Retention set to very short window (< 1 year) | Validation in US0094 prevents this; if somehow set, handler still operates |

---

## Test Scenarios

- [ ] Scans older than window deleted in batches
- [ ] Scans within window preserved
- [ ] Report request beyond window shows warning
- [ ] Dry run works as expected
- [ ] Archive hook called when enabled

---

## Technical Notes

### Files
- **New:** `src/SmartLog.Web/Services/Retention/ScanRetentionHandler.cs`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Reports/*.cshtml(.cs)` — compute retention horizon and show banner when request exceeds it
- **Modify:** DI registration

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |
| [US0102](US0102-retention-archive-to-file-export.md) | Optional integration | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — delete is simple; the report-boundary UX needs care

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
