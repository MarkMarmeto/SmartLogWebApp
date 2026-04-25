# US0082: No-Scan Alert Next Run Label

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-19

## User Story

**As a** Admin Amy (Administrator)
**I want** to see when the next No-Scan Alert will run on the SMS Dashboard
**So that** I can confirm the alert is scheduled and know when to expect it

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who configures school communications.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
The SMS Dashboard (`/Admin/Sms/Index`) currently shows the **last run** status for the No-Scan Alert (time, alert count, suppressed state). However, it does not show **when the next run** is scheduled. Admin Amy needs to see the upcoming run time at a glance — and if SMS sending is globally disabled, the label should clearly indicate that the alert will not fire.

---

## Acceptance Criteria

### AC1: Next Run Label — SMS Enabled
- **Given** SMS sending is globally enabled (`Sms:Enabled = true`)
- **And** the configured alert time is "18:10"
- **When** I view the SMS Dashboard (`/Admin/Sms/Index`)
- **Then** the No-Scan Alert card shows "Next Run: Today at 6:10 PM" (if alert hasn't run today yet)
- **Or** "Next Run: Tomorrow at 6:10 PM" (if alert already ran today)

### AC2: Next Run Label — SMS Disabled
- **Given** SMS sending is globally disabled (`Sms:Enabled = false`)
- **When** I view the SMS Dashboard (`/Admin/Sms/Index`)
- **Then** the No-Scan Alert card shows "Next Run: Disabled" with a muted/gray style
- **And** the "Run Now" / "Re-run" button is disabled (grayed out, non-clickable)
- **And** a tooltip or small text explains "SMS sending is disabled in Settings"

### AC3: Next Run Respects Alert Time Config
- **Given** SMS is enabled and the alert time is changed to "17:30" in Settings
- **When** I return to the SMS Dashboard
- **Then** "Next Run" reflects the updated time (e.g., "Today at 5:30 PM" or "Tomorrow at 5:30 PM")

### AC4: Next Run on Non-School Day
- **Given** SMS is enabled and today is not a school day (weekend or holiday)
- **When** I view the SMS Dashboard
- **Then** "Next Run" still shows the scheduled time (today or tomorrow based on whether the time has passed)
- **And** no additional "will be skipped" warning is required (the service handles skip logic internally)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Alert time setting missing from DB | Fall back to default "18:10" for display |
| Invalid alert time in DB (e.g., "99:99") | Show "Next Run: 6:10 PM (default)" and log warning |
| SMS just disabled while viewing page | Next page load shows "Disabled" state |
| Alert time is in the past today and alert hasn't run | Show "Today at {time}" — service may still trigger |
| Midnight edge case (alert time "00:05") | Show "Today at 12:05 AM" or "Tomorrow at 12:05 AM" correctly |

---

## Test Scenarios

- [ ] Next Run shows "Today at {time}" when alert hasn't run and time is in the future
- [ ] Next Run shows "Tomorrow at {time}" when alert already ran today
- [ ] Next Run shows "Disabled" when SMS is globally disabled
- [ ] Run Now button is disabled when SMS is globally disabled
- [ ] Next Run reflects updated alert time after config change
- [ ] Fallback to "18:10" when alert time setting is missing

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml.cs` — Add `NextRunDisplay` (string) and `IsSmsEnabled` (bool) properties. Load alert time from `AppSettings["Sms:NoScanAlertTime"]` and SMS enabled state from `ISmsSettingsService.IsSmsEnabledAsync()`. Compute display string based on current time vs alert time vs ran-today state.
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml` — Add "Next Run" label inside the No-Scan Alert card. Conditionally disable Run Now/Re-run buttons when SMS is disabled.

### Dependencies
- `ISmsSettingsService.IsSmsEnabledAsync()` — already exists
- `IAppSettingsService.GetAsync("Sms:NoScanAlertTime")` — already exists
- `INoScanAlertService.HasRunTodayAsync()` — already exists

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0052](US0052-no-scan-alert-service.md) | Functional | NoScanAlertService exists | Done |
| [US0053](US0053-no-scan-alert-config-dashboard.md) | Functional | Dashboard card and alert time config exist | Done |

---

## Estimation

**Story Points:** 1
**Complexity:** Low — UI-only changes, all data sources already available

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-19 | Claude | Initial story created |
