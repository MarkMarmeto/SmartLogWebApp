# US0076: Scanner Visitor Scan Display

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Guard Gary (Security Staff)
**I want** the scanner to display visitor pass information when a visitor QR is scanned
**So that** I can verify the visitor pass is valid and see the scan type

## Context

### Background
The scanner app currently shows student name, grade, and section after a scan. Visitor scans return different data (pass code, pass number, no student info). The scanner must handle this gracefully.

---

## Acceptance Criteria

### AC1: Visitor Scan Response Handling
- **Given** the server responds with a visitor scan result (no studentName field)
- **Then** the scanner displays:
  - "Visitor Pass #5"
  - "ENTRY" or "EXIT" (scan type)
  - "ACCEPTED" (status)
  - Timestamp

### AC2: Visual Distinction
- **Given** a visitor scan is accepted
- **Then** the result card background is blue (#2196F3) instead of green used for student scans
- **And** shows a visitor icon (person-with-badge) instead of student photo placeholder

### AC3: Rejected Visitor Scan
- **Given** the server returns `REJECTED_PASS_INACTIVE` for a visitor QR
- **Then** the scanner shows:
  - "Visitor Pass #5"
  - "REJECTED — Pass Inactive"
  - Red result card

### AC4: Duplicate Visitor Scan
- **Given** the server returns `DUPLICATE` for a visitor QR
- **Then** the scanner shows:
  - "Visitor Pass #5"
  - "DUPLICATE — Already scanned"
  - Yellow result card

### AC5: Backward Compatibility
- **Given** a student QR is scanned
- **Then** the scanner displays student info as before (name, grade, section)
- **And** no change to existing student scan UX

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Server returns unknown format | Show generic "Scan processed" with raw status |
| Network error during visitor scan | Optimistic accept (existing behaviour) |
| Visitor scan in offline queue | Queued for sync like student scans |
| Pass number is very high (e.g., #999) | Display truncated if needed; full number in tooltip |
| Server returns both student and visitor fields | Parse based on presence of passCode field |

---

## Test Scenarios

- [ ] Visitor scan shows pass number
- [ ] Visitor scan shows ENTRY/EXIT type
- [ ] Blue color for visitor (vs green for student)
- [ ] Rejected pass shows red card
- [ ] Duplicate shows yellow card
- [ ] Student scans unchanged
- [ ] Offline queuing works for visitor scans

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0073](US0073-visitor-scan-processing.md) | API | Visitor scan response format | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
