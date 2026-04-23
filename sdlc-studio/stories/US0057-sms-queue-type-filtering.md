# US0057: SMS Queue Message Type Filtering

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to filter the SMS Queue and History pages by message type (NO_SCAN_ALERT, PERSONAL, ATTENDANCE, etc.)
**So that** I can quickly find and monitor specific types of SMS messages

## Context

### Background
With new message types (NO_SCAN_ALERT, PERSONAL) added to the SMS system, the queue and history pages need filtering capability to help admins manage and troubleshoot specific message categories.

---

## Acceptance Criteria

### AC1: Filter Dropdown on SMS Queue Page
- **Given** I am on the SMS Queue page (`/Admin/Sms/Queue`)
- **Then** I see a "Message Type" filter dropdown with options:
  - "All Types" (default)
  - "Attendance (Entry/Exit)"
  - "No-Scan Alert"
  - "Personal"
  - "Announcement"
  - "Emergency"
  - "Custom"

### AC2: Filter Applied
- **Given** I select "No-Scan Alert" from the dropdown
- **When** the page refreshes/filters
- **Then** only SmsQueue entries with `MessageType = "NO_SCAN_ALERT"` are shown

### AC3: Filter on SMS History Page
- **Given** I am on the SMS History page (`/Admin/Sms/History`)
- **Then** the same "Message Type" filter dropdown is available
- **And** filtering works identically

### AC4: Combined Filters
- **Given** I select "No-Scan Alert" type filter
- **And** I select date range "April 16, 2026"
- **Then** only NO_SCAN_ALERT messages from that date are shown

### AC5: Count by Type
- **Given** I am on the SMS Queue page
- **Then** I see a summary bar showing counts per type:
  - "Pending: 45 No-Scan Alert, 2 Personal, 1 Announcement"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No messages of selected type | Show "No messages found" |
| Unknown MessageType in database | Show as "Other" |
| Filter with pagination | Filter persists across pages |
| URL query parameter | Filter can be set via `?type=NO_SCAN_ALERT` |
| New message type added in future | Appears under "Other" until dropdown updated |

---

## Test Scenarios

- [ ] Filter dropdown appears on Queue page
- [ ] Filter dropdown appears on History page
- [ ] Filtering by NO_SCAN_ALERT shows correct results
- [ ] Filtering by PERSONAL shows correct results
- [ ] "All Types" shows all messages
- [ ] Combined filters (type + date) work
- [ ] Summary counts by type are accurate
- [ ] Pagination works with filter applied

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0043](US0043-sms-history.md) | Functional | SMS History page | Ready |
| [US0052](US0052-no-scan-alert-service.md) | Functional | NO_SCAN_ALERT type exists | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
