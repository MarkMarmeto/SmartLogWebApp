# US0054: Entry/Exit SMS Opt-In per Student

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to opt individual students into entry/exit SMS notifications
**So that** parents who specifically want real-time scan alerts can receive them while the default remains end-of-day only

## Context

### Background
The default SMS strategy is end-of-day no-scan alert. Entry/exit SMS is disabled by default for all students. Admins can enable it per student for parents who specifically request real-time notifications.

---

## Acceptance Criteria

### AC1: New Student Field
- **Given** the Student entity
- **Then** a new field `EntryExitSmsEnabled` (bool, default: false) exists
- **And** it is independent of the existing `SmsEnabled` field

### AC2: Student Edit Form
- **Given** I am editing a student record
- **Then** I see a checkbox "Enable Entry/Exit SMS" (unchecked by default)
- **And** a help text: "When enabled, parents receive SMS on each scan. When disabled, parents only receive end-of-day alerts for absent students."

### AC3: ScansApiController Respects Flag
- **Given** a student with `EntryExitSmsEnabled = false` and `SmsEnabled = true`
- **When** the student scans (ENTRY or EXIT)
- **Then** no attendance SMS is queued
- **And** the scan is accepted normally

### AC4: ScansApiController Sends When Opted In
- **Given** a student with `EntryExitSmsEnabled = true` and `SmsEnabled = true`
- **When** the student scans ENTRY
- **Then** the ENTRY SMS is queued to ParentPhone

### AC5: SmsEnabled Master Switch
- **Given** a student with `SmsEnabled = false` and `EntryExitSmsEnabled = true`
- **When** the student scans
- **Then** no SMS is queued (master switch overrides)

### AC6: Migration Default
- **Given** existing students in the database
- **When** the migration runs
- **Then** all existing students have `EntryExitSmsEnabled = false`

### AC7: Student List Display
- **Given** I view the student list
- **Then** students with `EntryExitSmsEnabled = true` show an indicator (e.g., SMS icon with "E/E" badge)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Bulk import with no EntryExitSmsEnabled column | Default to false |
| Student with no ParentPhone but EntryExitSmsEnabled=true | No SMS queued (existing phone validation) |
| Both SmsEnabled=false and EntryExitSmsEnabled=true | No SMS (master switch wins) |
| Re-enrollment changes section | EntryExitSmsEnabled persists on student |
| Student deactivated | No SMS regardless of flags |

---

## Test Scenarios

- [ ] New students default to EntryExitSmsEnabled=false
- [ ] Checkbox appears on student edit form
- [ ] Scan does NOT queue SMS when EntryExitSmsEnabled=false
- [ ] Scan queues SMS when EntryExitSmsEnabled=true and SmsEnabled=true
- [ ] SmsEnabled=false overrides EntryExitSmsEnabled=true
- [ ] Migration sets all existing students to false
- [ ] Student list shows opt-in indicator

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0030](US0030-scan-ingestion-api.md) | Functional | ScansApiController exists | Ready |
| [US0016](US0016-edit-student.md) | Functional | Student edit form | Ready |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
