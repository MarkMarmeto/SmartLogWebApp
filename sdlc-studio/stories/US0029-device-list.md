# US0029: Device List and Revocation

> **Status:** Done
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** to view all registered scanner devices and revoke compromised ones
**So that** I can manage device access and maintain security

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Administrator who manages scanner device security.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

---

## Acceptance Criteria

### AC1: View Device List
- **Given** I am logged in as Super Admin
- **When** I navigate to Devices
- **Then** I see a table with columns: Name, Location, Status, Registered Date, Last Scan, Actions
- **And** devices are sorted by registration date (newest first)

### AC2: Device Status Display
- **Given** I am viewing the device list
- **Then** each device shows status:
  - "Active" (green badge) - currently usable
  - "Revoked" (red badge) - API key invalidated

### AC3: Last Scan Information
- **Given** a device has submitted scans
- **Then** the "Last Scan" column shows the timestamp of most recent scan
- **And** if no scans, shows "Never"

### AC4: Revoke Device Action
- **Given** I am viewing an active device "Main Gate Scanner 1"
- **When** I click "Revoke"
- **Then** I see confirmation: "Revoke Main Gate Scanner 1? This device will no longer be able to submit scans."

### AC5: Revocation Effect
- **Given** I confirm device revocation
- **Then** the device record is set to IsActive = false
- **And** RevokedAt is set to current timestamp
- **And** RevokedBy is set to my user ID
- **And** I see success message "Device revoked"
- **And** device status changes to "Revoked"

### AC6: Revoked Device Rejected
- **Given** a device has been revoked
- **When** it attempts to submit a scan with its API key
- **Then** the API returns 401 Unauthorized
- **And** error message: "Device has been revoked"

### AC7: View Device Details
- **Given** I click on a device name
- **Then** I see device details:
  - Name, Location, Description
  - Status (Active/Revoked)
  - Registered date and by whom
  - Revoked date and by whom (if applicable)
  - Total scans submitted
  - Last scan timestamp

### AC8: Regenerate API Key
- **Given** I am viewing an active device
- **When** I click "Regenerate API Key"
- **Then** I see confirmation: "Generate new API key? The current key will stop working immediately."
- **And** on confirmation, a new API key is generated and displayed once
- **And** the old API key hash is replaced

### AC9: Audit Log Entries
- **Given** I revoke a device or regenerate its API key
- **Then** an audit log entry is created with appropriate action details

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Revoke already revoked device | Button not visible |
| No devices registered | Show "No devices registered" message |
| Large number of devices | Pagination (20 per page) |
| Search for device | Filter by name or location |
| Revoke during active scan | Scan completes, next scan rejected |
| Network error during revoke | Show error, no changes made |

---

## Test Scenarios

- [ ] Device list displays all registered devices
- [ ] Device status shown correctly (Active/Revoked)
- [ ] Last scan timestamp displayed
- [ ] Revoke confirmation dialog shown
- [ ] Revocation sets IsActive = false
- [ ] Revoked device cannot submit scans
- [ ] Device details page shows all info
- [ ] Regenerate API key works
- [ ] Old API key stops working after regeneration
- [ ] Audit log entries created
- [ ] Pagination works for large device list
- [ ] Search/filter by name works

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0028](US0028-register-scanner.md) | Functional | Devices exist | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Super Admin role check | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
