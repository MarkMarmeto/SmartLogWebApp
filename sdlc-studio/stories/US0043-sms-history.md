# US0043: SMS History and Status

> **Status:** Done
> **Epic:** [EP0007: SMS Notifications](../epics/EP0007-sms-notifications.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to view SMS notification history and status
**So that** I can verify messages are being delivered and troubleshoot issues

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who monitors notification delivery.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: SMS History List
- **Given** I am logged in as Admin
- **When** I navigate to Reports > SMS History
- **Then** I see a table with columns:
  - Date/Time
  - Student Name
  - Parent Phone
  - Message (truncated)
  - Type (Entry/Exit)
  - Status
  - Actions

### AC2: Status Display
- **Given** the SMS history list
- **Then** status is shown with colored badges:
  - Pending: Yellow
  - Sent: Green
  - Delivered: Green (checkmark)
  - Failed: Red
  - Retry: Orange
  - Skipped: Gray

### AC3: Filter by Status
- **Given** I am on SMS History
- **When** I filter by status "Failed"
- **Then** I see only failed messages
- **And** I can identify delivery issues

### AC4: Filter by Date Range
- **Given** I am on SMS History
- **When** I select date range "Today" or custom dates
- **Then** I see SMS messages within that range

### AC5: Search by Student
- **Given** I am on SMS History
- **When** I search for "Maria Santos"
- **Then** I see all SMS sent for that student

### AC6: View SMS Details
- **Given** I click on an SMS entry
- **Then** I see full details:
  - Student name and ID
  - Parent phone number
  - Full message content
  - Scan type and time
  - Queue time
  - Send time (if sent)
  - Delivery time (if delivered)
  - Retry count
  - Failure reason (if failed)
  - Gateway message ID

### AC7: Retry Failed Message
- **Given** I am viewing a failed SMS
- **When** I click "Retry"
- **Then** the message is re-queued
- **And** status changes to "Pending"
- **And** retry count is reset

### AC8: SMS Summary Stats
- **Given** I am on SMS History
- **Then** I see summary statistics for selected date range:
  - Total Sent: 150
  - Delivered: 145 (97%)
  - Failed: 3
  - Pending: 2

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No SMS in range | Show "No messages found" |
| Large history (10,000+) | Pagination, optimized queries |
| Retry already pending message | Button disabled |
| Phone number masked | Show partial: ***1234 |
| Very long message | Truncate in list, full in details |

---

## Test Scenarios

- [ ] SMS history list displays correctly
- [ ] Status badges show correct colors
- [ ] Filter by status works
- [ ] Filter by date range works
- [ ] Search by student works
- [ ] SMS details modal shows all info
- [ ] Retry button re-queues message
- [ ] Summary statistics accurate
- [ ] Pagination works
- [ ] Failed reasons displayed
- [ ] Phone numbers partially masked

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0041](US0041-sms-queue.md) | Functional | SMS records exist | Ready |
| [US0007](US0007-authorization-enforcement.md) | Functional | Admin access | Ready |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
