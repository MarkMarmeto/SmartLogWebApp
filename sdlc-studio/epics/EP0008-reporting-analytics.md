# EP0008: Reporting & Analytics

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 3

## Summary

Provide attendance reports, audit logs, and analytics for school administrators. Includes daily/weekly/monthly attendance reports, student attendance history, and system audit trail viewing.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Performance | Reports generate within 30 seconds | Query optimization |
| PRD | Data | Audit logs immutable | Append-only design |
| TRD | Architecture | SQL Server queries | Report query design |

---

## Business Context

### Problem Statement
Schools need attendance reports for compliance, parent meetings, and identifying at-risk students. Administrators need audit logs to investigate issues and ensure accountability.

**PRD Reference:** [Feature FT-012](../prd.md#3-feature-inventory)

### Value Proposition
- Compliance reporting for education authorities
- Early identification of attendance patterns
- Accountability through comprehensive audit trail
- Data-driven decision making for school administration

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Report generation time | N/A | < 30 seconds | Performance testing |
| Report accuracy | N/A | 100% | Data validation |
| Audit log completeness | N/A | 100% of actions | Log coverage audit |

---

## Scope

### In Scope
- Daily attendance report (by class/grade)
- Weekly attendance summary report
- Monthly attendance report
- Student attendance history (individual student)
- Export reports to PDF and Excel
- Audit log viewer with filtering
- Audit log search by user, action, date range
- System activity summary

### Out of Scope
- Custom report builder
- Scheduled report generation
- Report emailing
- Advanced analytics/predictions
- Attendance alerts/thresholds

### Affected Personas
- **Tech-Savvy Tony (Super Admin):** Full audit log access, system reports
- **Admin Amy (Administrator):** Attendance reports, limited audit access
- **Teacher Tina:** Class attendance reports only

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can generate daily attendance report for any date
- [ ] Report shows: Present, Absent, Late for each student
- [ ] Admin can filter report by grade, section
- [ ] Admin can generate weekly attendance summary
- [ ] Admin can generate monthly attendance report
- [ ] Admin can view individual student attendance history
- [ ] Reports can be exported to PDF
- [ ] Reports can be exported to Excel
- [ ] Super Admin can view system audit logs
- [ ] Audit logs can be filtered by user, action, date
- [ ] Audit logs can be searched by entity
- [ ] Teacher can view reports for their classes only
- [ ] Reports generate within 30 seconds

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0001: Authentication & Authorization | Epic | Not Started | Development |
| EP0003: Student Management | Epic | Not Started | Development |
| EP0006: Attendance Tracking | Epic | Not Started | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | Reporting is end-of-pipeline feature |

---

## Risks & Assumptions

### Assumptions
- Attendance data is accurate from scan records
- Audit logs exist for all relevant actions
- PDF/Excel libraries are available for .NET

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Large reports slow to generate | Medium | Medium | Pagination, background generation |
| Audit log storage grows large | Medium | Low | Archival strategy, partitioning |
| Export file size issues | Low | Low | Streaming export, compression |

---

## Technical Considerations

### Architecture Impact
- ReportService for report generation
- AuditLogService for log queries
- PDF generation (QuestPDF or similar)
- Excel generation (ClosedXML or EPPlus)
- Optimized queries with proper indexing

### Integration Points
- Database: Scan, Student, AuditLog tables
- EP0006: Attendance calculations
- Export: PDF, Excel file generation

---

## Sizing

**Story Points:** 20
**Estimated Story Count:** 7

**Complexity Factors:**
- Report query optimization
- PDF/Excel generation
- Multiple report types
- Role-based report access

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0045](../stories/US0045-daily-report.md) | Daily Attendance Report | 3 | Done |
| [US0046](../stories/US0046-weekly-report.md) | Weekly Attendance Summary | 3 | Done |
| [US0047](../stories/US0047-monthly-report.md) | Monthly Attendance Report | 3 | Done |
| [US0048](../stories/US0048-student-history.md) | Student Attendance History | 3 | Done |
| [US0049](../stories/US0049-report-export.md) | Report Export (PDF/Excel) | 3 | Done |
| [US0050](../stories/US0050-audit-log-viewer.md) | Audit Log Viewer | 3 | Done |
| [US0051](../stories/US0051-audit-log-search.md) | Audit Log Search and Filter | 2 | Done |

**Total:** 20 story points across 7 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0008`

---

## Open Questions

- [ ] What defines "Late"? (Arrival after specific time?) - Owner: Product
- [ ] How long should audit logs be retained? - Owner: Compliance
- [ ] Should reports be printable directly from browser? - Owner: UX

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
