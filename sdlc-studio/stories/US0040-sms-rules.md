# US0040: SMS Notification Rules

> **Status:** Done
> **Epic:** [EP0007: SMS Notifications](../epics/EP0007-sms-notifications.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to configure when SMS notifications are sent
**So that** parents receive notifications based on school policy

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages school notification policies.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Global SMS Settings
- **Given** I am on Settings > SMS Configuration
- **Then** I see global SMS settings:
  - SMS Notifications: Enabled / Disabled toggle
  - Notification Mode: Entry Only / Exit Only / Both

### AC2: Enable/Disable SMS
- **Given** SMS is currently enabled
- **When** I toggle to "Disabled"
- **Then** all SMS notifications stop
- **And** I see warning: "No SMS will be sent while disabled"

### AC3: Notification Mode Selection
- **Given** I select notification mode "Entry Only"
- **Then** SMS is only sent when students scan entry
- **And** exit scans do not trigger SMS

### AC4: Quiet Hours Configuration
- **Given** I am on SMS Configuration
- **Then** I can set quiet hours:
  - Start Time (e.g., 9:00 PM)
  - End Time (e.g., 6:00 AM)
- **And** SMS during quiet hours are queued for later

### AC5: School Name Configuration
- **Given** I am on SMS Configuration
- **Then** I can set the school name used in templates
- **And** this value is used for `{SchoolName}` variable

### AC6: Test SMS Function
- **Given** I have configured SMS settings
- **When** I click "Send Test SMS"
- **Then** I enter a phone number
- **And** I see current gateway status:
  - GSM Modem: "Connected, Signal: 4/5 bars" or "Not connected"
  - Cloud Gateway: "Configured" or "Not configured"
- **And** a test message is sent using the Entry template
- **And** I see result: "Test SMS sent successfully via GSM modem" or error details

### AC7: Settings Audit Log
- **Given** I change SMS configuration
- **Then** an audit log entry is created with:
  - Action: "SmsConfigurationUpdated"
  - Details: what changed (e.g., "Mode changed from Both to Entry Only")

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Disable with messages in queue | Warning: "X messages in queue will still be sent" |
| Invalid quiet hours (end before start) | Allow (crosses midnight) |
| Empty school name | Use default or show error |
| Test SMS fails | Show gateway error message |
| No SMS gateway configured | Show "SMS gateway not configured" |
| GSM modem not connected | Show "GSM modem not detected. Check USB connection" |
| GSM modem low signal | Show warning "Low signal - SMS may fail to send" |

---

## Test Scenarios

- [ ] Enable/Disable toggle works
- [ ] Notification mode selection works
- [ ] Entry Only mode sends only on entry
- [ ] Exit Only mode sends only on exit
- [ ] Both mode sends on entry and exit
- [ ] Quiet hours can be configured
- [ ] SMS during quiet hours are queued
- [ ] School name is configurable
- [ ] Test SMS function works
- [ ] Audit log created on config change
- [ ] Disabled state stops all notifications

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0039](US0039-sms-templates.md) | Functional | Templates exist | Ready |
| [US0042](US0042-sms-gateway.md) | Functional | Gateway for test SMS | Ready |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Updated test SMS to show GSM modem status |
