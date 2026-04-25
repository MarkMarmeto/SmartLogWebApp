# US0053: No-Scan Alert Admin Configuration & Dashboard

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to configure the no-scan alert time and see the last run status on the SMS dashboard
**So that** I can control when alerts are sent and verify they are working

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who configures school communications.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Alert Time Configuration
- **Given** I am on the SMS Settings page (`/Admin/Sms/Settings`)
- **Then** I see a "No-Scan Alert Time" field with a time picker
- **And** the current value shows "18:10" (default) or the configured value
- **When** I change the time to "17:30" and click Save
- **Then** AppSettings key `Sms:NoScanAlertTime` is updated to "17:30"
- **And** I see success message "Settings saved successfully"

### AC2: Time Validation
- **Given** I enter an invalid time format (e.g., "25:00" or empty)
- **When** I click Save
- **Then** I see error "Please enter a valid time in HH:mm format"

### AC3: Dashboard Last Run Display
- **Given** the NoScanAlertService last ran at 18:10 today and queued 45 alerts
- **When** I view the SMS Dashboard (`/Admin/Sms/Index`)
- **Then** I see a "No-Scan Alert" card showing:
  - "Last Run: April 16, 2026 6:10 PM"
  - "Alerts Sent: 45"
  - Status indicator: green (success)

### AC4: Dashboard — No Run Yet
- **Given** the NoScanAlertService has never run
- **When** I view the SMS Dashboard (`/Admin/Sms/Index`)
- **Then** the No-Scan Alert card shows "Last Run: Never"
- **And** status indicator is gray (no data)

### AC5: Dashboard — Suppressed Run
- **Given** the last run was suppressed (zero total scans)
- **When** I view the SMS Dashboard
- **Then** the card shows "Last Run: April 16, 2026 6:10 PM — Suppressed (no scanner activity)"
- **And** status indicator is yellow (warning)

### AC6: Seed AppSettings Key
- **Given** a fresh database initialization
- **Then** AppSettings contains key `Sms:NoScanAlertTime` with value "18:10" and category "Sms"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Settings page loads before service registered | Time field shows default "18:10" |
| Multiple admins change time simultaneously | Last save wins |
| Alert time set to school hours (e.g., "10:00") | Allow (admin responsibility) |
| Dashboard loads during alert processing | Show last completed run |
| AppSettings table empty | Show defaults |

---

## Test Scenarios

- [ ] Time picker displays current configured value
- [ ] Time can be changed and saved
- [ ] Invalid time format rejected
- [ ] Dashboard shows last run time and count
- [ ] Dashboard handles "never run" state
- [ ] Dashboard shows suppressed run with warning
- [ ] AppSettings key seeded on fresh install

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0052](US0052-no-scan-alert-service.md) | Functional | NoScanAlertService exists | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
