# EP0017: Data Retention & Archival

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-24
> **Target Release:** V2 — Phase 2 (Feature Enhancements)

## Summary

Introduce configurable retention policies and automated archival/purge for high-volume, regulated, or audit-bearing tables: `SmsQueue`, `SmsLog`, `Broadcast`, `Scan`, `AuditLog`, and `VisitorScan`. Prevent unbounded DB growth while keeping the system aligned with RA 10173 (Data Privacy Act) retention principles. This Epic lands the operational retention controls; the broader PII/consent compliance work remains with EP0016 (V3-deferred).

## Inherited Constraints

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Legal | RA 10173 — personal data retained only as long as necessary | Default retention windows must justify duration; no indefinite retention of PII |
| PRD | Operational | Offline-first LAN deployment, SQL Server on school hardware | Purge/archival must run locally, no cloud dependency |
| TRD | Architecture | IHostedService pattern (NoScanAlertService, SmsWorkerService) | Reuse BackgroundService pattern for retention worker |
| TRD | Data | Existing audit trail requirements (auth events, admin actions) | AuditLog retention must respect legal-hold flag |

---

## Business Context

### Problem Statement
SmartLog writes millions of SMS-related rows over time: a 1,900-student school queuing ~570 `NO_SCAN_ALERT` + ~11,400 `ENTRY/EXIT` (when opt-in) messages per day produces ~4.3M SmsQueue rows/year plus matching SmsLog entries. Scan rows accumulate at similar scale. Without retention, the SQL Server database degrades query performance, backups grow unbounded, and the school retains personal communications far longer than necessary — conflicting with RA 10173's storage-limitation principle.

**PRD Reference:** [SMS Notifications](../prd.md#3-feature-inventory), [Compliance](../prd.md#6-constraints)

### Value Proposition
- Keeps the production DB fast by capping hot-table row counts
- Aligns with RA 10173 storage-limitation principle
- Gives admins transparent, configurable controls rather than hardcoded cleanup
- Produces an auditable archive trail (export-to-file) before purge
- Unblocks backup strategy — predictable DB size

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| SmsQueue row count (steady state) | Unbounded | ≤ 90 days of rows by default | Row count query |
| SmsLog row count (steady state) | Unbounded | ≤ 180 days of rows by default | Row count query |
| Purge job failure rate | N/A | < 1% | RetentionRun log |
| Admin confidence in retention controls | N/A | Documented policy + UI | Stakeholder sign-off |

---

## Scope

### In Scope
- **Retention policy storage** — new `RetentionPolicy` config entity (per-table window, archive-enabled flag, last-run timestamp)
- **Admin UI** — `/Admin/Settings/Retention` page to view and edit retention windows per entity
- **Background worker** — `RetentionService` (IHostedService) runs daily at a configurable time, iterates entities, applies per-entity policy
- **Purge/archive per entity:**
  - `SmsQueue` — purge processed (Sent/Failed/Cancelled) rows older than window
  - `SmsLog` — archive then purge rows older than window
  - `Broadcast` — retain Broadcast + cascade related SmsQueue lookup stays functional
  - `Scan` — purge rows older than window (respects legal minimum for attendance)
  - `AuditLog` — respects `LegalHold` flag; only purges rows where `LegalHold = false`
  - `VisitorScan` — purge rows older than window
- **Manual run** — admin "Run Now" button per entity with dry-run preview (row count + date range affected)
- **Archive-to-file export** — optional CSV/JSON snapshot to configured archive directory before purge
- **Run history** — `RetentionRun` table logs each execution: entity, rows-affected, duration, status, error
- **Retention documentation page** — justification per table + RA 10173 mapping

### Out of Scope
- Cloud archival / S3 upload (keep file-based local archive only)
- Per-row retention overrides (all rows in a table share one policy)
- Purge of PII fields on `Student` or `Faculty` records (covered by EP0016)
- Consent-driven retention (covered by EP0016)
- GDPR-specific right-to-erasure workflows (covered by EP0016)

### Affected Personas
- **Admin Amy** — Configures retention windows, reviews run history, triggers manual runs
- **Tony (Tech Admin)** — Monitors DB size, validates purge worked, investigates failures
- **Legal / School Admin (indirect)** — Relies on documented policy for RA 10173 compliance

---

## Acceptance Criteria (Epic Level)

- [ ] Retention policies configurable per entity (SmsQueue, SmsLog, Broadcast, Scan, AuditLog, VisitorScan) from admin UI
- [ ] Default windows seeded on first run with documented justification per table
- [ ] `RetentionService` runs on schedule, respects per-entity enabled flag, logs every run to `RetentionRun`
- [ ] Dry-run preview shows exact rows affected before execution
- [ ] Manual "Run Now" executes the policy for a single entity
- [ ] `AuditLog` purge never deletes rows with `LegalHold = true`
- [ ] Optional archive-to-file produces a restorable CSV/JSON snapshot before purge
- [ ] Retention policy doc page displays current windows and RA 10173 mapping
- [ ] Purge operations run in batches to avoid locking large tables

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0007: SMS Notifications | Epic | Done | Development |
| EP0006: Attendance Tracking | Epic | Done | Development |
| EP0009: SMS Strategy Overhaul | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0016: PII & RA 10173 Compliance | Epic (Deferred) | Consent/erasure extends retention framework established here |

---

## Risks & Assumptions

### Assumptions
- Schools will set retention windows that meet their local compliance needs (defaults documented, not enforced)
- Archive-to-file directory is on the same host as the DB (LAN deployment model)
- Batch purges run during low-traffic hours (default 02:00)
- `Broadcast` table does not have FK-cascade concerns with active `SmsQueue` items

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Purge locks table during school hours | Low | High | Batch-sized deletes, off-hours schedule, admin-visible run window |
| AuditLog purge hides evidence in dispute | Low | Critical | `LegalHold` flag prevents deletion; default window long enough for normal ops |
| Scan purge breaks attendance reports | Medium | High | Default window ≥ 2 years; reports check if date-range exceeds retention and warn |
| Archive file grows unbounded | Medium | Medium | Archive rotation / configurable retention of archive files themselves |
| Policy misconfigured to 0 days | Low | Critical | UI validation: minimum 7 days per entity; warning on values below documented floor |

---

## Technical Considerations

### Architecture Impact
- New entity: `RetentionPolicy` (Id, EntityName, RetentionDays, ArchiveEnabled, Enabled, LastRunAt, LastRowsAffected)
- New entity: `RetentionRun` (Id, EntityName, StartedAt, CompletedAt, Status, RowsAffected, ErrorMessage)
- New field on `AuditLog`: `LegalHold` (bool, default false)
- New service: `RetentionService` (IHostedService) — scheduled daily runner
- New service: `IEntityRetentionHandler` — one implementation per entity (SmsQueueRetentionHandler, etc.)
- New admin page: `/Admin/Settings/Retention`

### Default Retention Windows (proposed — finalise per story)

| Entity | Default Window | Justification |
|--------|----------------|---------------|
| SmsQueue | 90 days | Delivery status review window; processed rows no longer actionable |
| SmsLog | 180 days | Delivery audit for parent complaints; aligns with typical billing cycle |
| Broadcast | 365 days | Admin may reference past announcements within a school year |
| Scan | 730 days (2 school years) | Attendance records referenced for enrolment / academic review |
| AuditLog | 1,095 days (3 years) | Security audit trail; RA 10173 breach investigation window |
| VisitorScan | 365 days | Visitor log for safety incidents within one school year |

### Integration Points
- `ApplicationDbContext` — add `RetentionPolicy`, `RetentionRun`
- `Program.cs` — register `RetentionService` and per-entity handlers
- `DbInitializer` — seed default policies on first run
- Admin menu — new "Retention" item under Settings

### Key Files to Create/Modify
- **New:** `src/SmartLog.Web/Services/Retention/RetentionService.cs`
- **New:** `src/SmartLog.Web/Services/Retention/IEntityRetentionHandler.cs`
- **New:** `src/SmartLog.Web/Services/Retention/{Entity}RetentionHandler.cs` (6 handlers)
- **New:** `src/SmartLog.Web/Data/Entities/RetentionPolicy.cs`
- **New:** `src/SmartLog.Web/Data/Entities/RetentionRun.cs`
- **New:** `src/SmartLog.Web/Pages/Admin/Settings/Retention.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Data/Entities/AuditLog.cs` (add `LegalHold`)
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs`
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs`
- **Modify:** `src/SmartLog.Web/Program.cs`

---

## Sizing

**Story Points:** TBD (estimated 21-28 points across 9 stories)
**Estimated Story Count:** 9

**Complexity Factors:**
- Six independent entity handlers with different purge rules and FK concerns
- Batch-delete pattern must not lock production tables during school hours
- Archive-to-file needs tested restore path
- Legal-hold flag spans app + UI + audit review

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0094](../stories/US0094-retention-policy-entity-and-admin-ui.md) | Retention Policy Entity & Admin UI | 3 | Draft |
| [US0095](../stories/US0095-smsqueue-retention-handler.md) | SmsQueue Retention Handler | 3 | Draft |
| [US0096](../stories/US0096-smslog-retention-handler.md) | SmsLog Retention Handler | 3 | Draft |
| [US0097](../stories/US0097-broadcast-retention-handler.md) | Broadcast Retention Handler | 2 | Draft |
| [US0098](../stories/US0098-scan-retention-handler.md) | Scan Retention Handler | 3 | Draft |
| [US0099](../stories/US0099-auditlog-retention-with-legal-hold.md) | AuditLog Retention with Legal Hold | 3 | Draft |
| [US0100](../stories/US0100-visitorscan-retention-handler.md) | VisitorScan Retention Handler | 2 | Draft |
| [US0101](../stories/US0101-retention-scheduled-service-and-dry-run.md) | Scheduled Retention Service + Manual Run + Dry-Run | 3 | Draft |
| [US0102](../stories/US0102-retention-archive-to-file-export.md) | Archive-to-File Export Before Purge | 2 | Draft |

**Total:** 24 story points across 9 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0017`

---

## Open Questions

- [ ] Should AuditLog retention default to 3 years or longer given RA 10173 breach-investigation windows?
- [ ] Archive file format — CSV (simple, Excel-friendly) or JSON (structured, richer)? Proposed: CSV default, JSON opt-in.
- [ ] Archive file retention — purge archive files after N days? Proposed: configurable, default never (manual cleanup).
- [ ] Should the retention service surface a dashboard widget on the admin home page?

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial epic created from V2 retention planning session |
