# US0071: Multi-Camera Setup Page

> **Status:** Done
> **Epic:** [EP0011: Multi-Camera Scanning](../epics/EP0011-multi-camera-scanning.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** SuperAdmin Tony (IT Administrator)
**I want** a setup page to configure the number of cameras and assign USB devices
**So that** I can set up the multi-camera scanning station

## Context

### Background
The setup page replaces the single-camera configuration with a multi-camera interface. Admin selects camera count (1-8), assigns USB devices to each slot, configures scan type, and can enable/disable individual cameras.

---

## Acceptance Criteria

### AC1: Camera Count Selector
- **Given** I am on the Setup page
- **Then** I see a "Number of Cameras" selector (1-8)
- **And** changing the count adds/removes camera configuration rows

### AC2: Per-Camera Configuration Row
- **Given** 3 cameras are configured
- **Then** each row shows:
  - Camera number (1, 2, 3)
  - Display name (editable text, e.g., "Gate A")
  - Device dropdown (lists available USB cameras)
  - Scan Type toggle (ENTRY / EXIT)
  - Enable/Disable toggle
  - Test button

### AC3: Device Enumeration
- **Given** 4 USB cameras are connected
- **Then** the device dropdown for each camera shows all 4 cameras
- **And** a camera already assigned to another slot is marked "(in use)" but still selectable

### AC4: Save Configuration
- **Given** I configure 3 cameras and click Save
- **Then** the configuration is persisted to local settings
- **And** the scanner starts with the configured cameras

### AC5: Reduce Camera Count
- **Given** 5 cameras are configured
- **When** I change count to 3
- **Then** cameras 4 and 5 configuration rows are removed
- **And** confirmation: "Remove cameras 4-5? Active scans on these cameras will stop."

### AC6: Single Camera Backward Compatibility
- **Given** camera count is set to 1
- **When** the scanner starts
- **Then** the scanner operates identically to the current single-camera mode
- **And** the main page shows full-width single camera view

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No USB cameras detected | Show "No cameras found. Connect a USB camera." |
| Camera count exceeds available devices | Warning: "Only 2 cameras detected. Cameras 3-4 will show as Offline." |
| Device disconnected between enumerate and save | Error on save; re-enumerate |
| Duplicate device assignment | Warning but allow (user may know what they're doing) |
| Settings file corrupted | Fall back to single camera defaults |

---

## Test Scenarios

- [ ] Camera count selector shows 1-8
- [ ] Changing count adds/removes rows
- [ ] Device dropdown lists available cameras
- [ ] Scan type toggle works per camera
- [ ] Configuration persists after save
- [ ] Reducing count shows confirmation
- [ ] Single camera mode backward compatible
- [ ] No cameras detected shows helpful message
- [ ] Test button verifies camera

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0066](US0066-multi-camera-manager.md) | Functional | MultiCameraManager | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
