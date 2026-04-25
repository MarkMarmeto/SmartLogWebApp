# EP0016: PII & RA 10173 Compliance

> **Status:** Draft
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V3 — Phase 3 (Commercial Readiness) — **DEFERRED**

## Summary

Ensure SmartLog complies with the Philippine Data Privacy Act of 2012 (RA 10173) by implementing consent fields on student records, a data retention policy with auto-purge for old logs, and a bilingual privacy policy page. Essential compliance scope — focused on what's legally required for commercial distribution.

## Inherited Constraints

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| Legal | RA 10173 | Data Privacy Act of 2012 | Consent, retention, privacy policy required |
| PRD | Data | Student PII (name, LRN, phone, photo) | Must have explicit consent for processing |
| TRD | Data | AuditLog, SmsLog, ScanLog tables | Retention policy with auto-purge needed |

---

## Business Context

### Problem Statement
SmartLog processes student PII (names, LRN, parent phone numbers, photos). Philippine law (RA 10173) requires explicit consent for data processing, defined retention periods, and a published privacy policy. Commercial distribution without compliance exposes the business to legal liability.

### Value Proposition
- Legal compliance enables commercial sales to Philippine schools
- Consent tracking protects both the school and the vendor
- Data retention auto-purge reduces storage costs and legal exposure
- Privacy policy builds trust with parents and administrators

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Consent coverage | 0% | 100% of active students | Student.DataProcessingConsent field |
| Data retention compliance | No policy | Defined retention + auto-purge | DataRetentionService runs |

---

## Scope

### In Scope
- **Consent fields:** `Student.DataProcessingConsent` (bool), `Student.ConsentDate` (DateTime?)
- **DataRetentionService:** Background job to auto-purge old AuditLogs, SmsLogs, and scan records beyond retention period
- **Configurable retention periods:** via AppSettings (e.g., 3 years for scans, 1 year for SMS logs)
- **Privacy policy page:** Bilingual (EN/FIL), accessible without login
- **Consent collection UI:** Checkbox on student registration/edit form

### Out of Scope
- Data Protection Officer (DPO) appointment process
- NPC registration filing
- Encryption at rest (SQL Server TDE is separate infrastructure concern)
- Right to be forgotten (full data deletion on request)
- Detailed scope deferred to implementation phase

### Affected Personas
- **Admin Amy:** Collects consent during enrollment
- **SuperAdmin Tony:** Configures retention periods
- **Parents (Indirect):** Provide consent, can view privacy policy

---

## Acceptance Criteria (Epic Level)

- [ ] Student record includes DataProcessingConsent and ConsentDate fields
- [ ] Consent checkbox on student create/edit form
- [ ] DataRetentionService runs on schedule and purges expired records
- [ ] Retention periods configurable via AppSettings
- [ ] Privacy policy page accessible at `/Privacy` without authentication
- [ ] Privacy policy available in English and Filipino
- [ ] Consent status visible on student list/detail pages

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0003: Student Management | Epic | Done | Development |
| All Phase 2 Epics | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| Commercial launch | Business | Cannot sell without compliance |

---

## Sizing

**Story Points:** TBD (estimated 5-7 stories)
**Estimated Story Count:** 5-7

---

## Story Breakdown

Stories to be generated when this epic moves to Ready status.

---

## Open Questions

- [ ] Exact retention periods per data type? — Owner: Legal/Business
- [ ] Privacy policy content review by legal counsel? — Owner: Business
- [ ] NPC registration required before commercial launch? — Owner: Legal

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2/V3 brainstorm (deferred) |
