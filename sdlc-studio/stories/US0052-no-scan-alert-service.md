# US0052: End-of-Day No-Scan Alert Service

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** the system to automatically send SMS alerts to parents of students who had no scans for the day
**So that** parents are notified when their child may not have attended school

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who configures school communications.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
Instead of sending SMS on every entry/exit scan (~11,400/day), the system sends a single end-of-day alert only for students with zero scans (~570/day). This reduces SMS costs by 95% while providing a higher-value safety signal.

---

## Acceptance Criteria

### AC1: Scheduled Execution
- **Given** the configured alert time is "18:10" (from AppSettings `Sms:NoScanAlertTime`)
- **When** the system clock reaches 18:10
- **Then** the `NoScanAlertService` background job executes

### AC2: School Day Guard
- **Given** today is NOT a school day (holiday, weekend, or suspension per CalendarService)
- **When** the alert job runs
- **Then** the job skips with log message "No-scan alert skipped: not a school day"
- **And** no SMS is queued

### AC3: Scanner Health Guard
- **Given** today is a school day
- **And** zero total accepted scans exist across ALL devices for today
- **When** the alert job runs
- **Then** the job suppresses the batch with log "No-scan alert suppressed: zero total scans (possible scanner issue)"
- **And** no SMS is queued for students
- **And** an AuditLog entry is created with Action "NO_SCAN_ALERT_SUPPRESSED"

### AC4: Identify Students with No Scans
- **Given** today is a school day and at least one scan was recorded system-wide
- **When** the alert job runs
- **Then** it queries active enrolled students (current academic year) where:
  - `Student.SmsEnabled = true`
  - `Student.ParentPhone` is not null/empty
  - Student has zero accepted Scan records for today
- **And** for each matching student, a NO_SCAN_ALERT SMS is queued

### AC5: SMS Template Rendering
- **Given** a student (Juan, Grade 7, STE, Ruby) with SmsLanguage = "FIL"
- **When** the NO_SCAN_ALERT is queued
- **Then** the message uses the FIL template with placeholders filled:
  - `{StudentFirstName}` → "Juan"
  - `{GradeLevel}` → "Grade 7"
  - `{Section}` → "Ruby"
  - `{Date}` → "April 16, 2026"
  - `{SchoolPhone}` → value from AppSettings `System.SchoolPhone`

### AC6: Idempotency Guard
- **Given** a NO_SCAN_ALERT has already been queued for student "SL-2026-00001" for today
- **When** the alert job runs again (e.g., manual re-trigger or restart)
- **Then** no duplicate NO_SCAN_ALERT is queued for that student
- **And** log message "Skipped duplicate NO_SCAN_ALERT for {StudentId}"

### AC7: Audit Logging
- **Given** the alert job completes
- **Then** an AuditLog entry is created with:
  - Action: "NO_SCAN_ALERT_EXECUTED"
  - Details: start time, students queried, alerts queued count, completion time

### AC8: NO_SCAN_ALERT Template Seeded
- **Given** a fresh database initialization
- **Then** the SmsTemplate table contains a `NO_SCAN_ALERT` template with:
  - EN: "SmartLog: We have no attendance record for {StudentFirstName} ({GradeLevel} - {Section}) today, {Date}. Please verify their whereabouts or contact the school at {SchoolPhone}."
  - FIL: "SmartLog: Wala kaming rekord ng pagdalo ni {StudentFirstName} ({GradeLevel} - {Section}) ngayon, {Date}. Mangyaring tiyakin ang kanilang kinaroroonan o makipag-ugnayan sa paaralan sa {SchoolPhone}."
  - AvailablePlaceholders: `{StudentFirstName}, {GradeLevel}, {Section}, {Date}, {SchoolPhone}`

---

## Technical Notes

- Implement as `NoScanAlertService : BackgroundService` registered in `Program.cs`
- Use `PeriodicTimer` or similar to wake at configured time daily
- Query pattern: active enrolled students LEFT JOIN today's accepted scans WHERE scan is NULL
- Insert into SmsQueue with `MessageType = "NO_SCAN_ALERT"`, `Priority = Normal`
- Seed template in `DbInitializer.cs` alongside existing ENTRY/EXIT templates

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Alert time is past when service starts | Run immediately if within same day, then schedule next day |
| No enrolled students with SmsEnabled | Job completes with 0 alerts queued |
| Student has ParentPhone but SmsEnabled=false | Student excluded from alert |
| Database connection error during job | Log error, retry next day |
| AppSettings `Sms:NoScanAlertTime` missing | Default to "18:10" |
| AppSettings `System.SchoolPhone` missing | Render `{SchoolPhone}` as empty string |
| School day with early dismissal | Alert still runs at configured time |
| Student enrolled in multiple sections (shouldn't happen) | Use current active enrollment |

---

## Test Scenarios

- [ ] Job runs at configured time
- [ ] Job skips on non-school days (holiday, weekend, suspension)
- [ ] Job suppresses when zero total scans (scanner health)
- [ ] Job correctly identifies students with no scans
- [ ] Job skips students with SmsEnabled=false
- [ ] Job skips students without ParentPhone
- [ ] SMS uses correct language template (EN/FIL)
- [ ] Placeholders render correctly
- [ ] Duplicate alerts are prevented (idempotency)
- [ ] AuditLog entry created on completion
- [ ] AuditLog entry created on suppression
- [ ] NO_SCAN_ALERT template seeded correctly

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0041](US0041-sms-queue.md) | Functional | SmsQueue and SmsWorkerService | Ready |
| [US0039](US0039-sms-templates.md) | Functional | SmsTemplate table | Ready |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
