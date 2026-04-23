# US0069: Per-Camera Scan Type Configuration

> **Status:** Done
> **Epic:** [EP0011: Multi-Camera Scanning](../epics/EP0011-multi-camera-scanning.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** SuperAdmin Tony (IT Administrator)
**I want** to assign ENTRY or EXIT scan type to each camera independently
**So that** different cameras can serve different scanning lanes (entry gate vs exit gate)

## Context

### Background
Each camera instance has its own scan type (ENTRY or EXIT). When a QR is decoded, the scan is submitted to the server with that camera's configured scan type. This replaces the current global scan type toggle.

---

## Acceptance Criteria

### AC1: Per-Camera Scan Type in Settings
- **Given** I am on the Scanner Setup page
- **And** 3 cameras are configured
- **Then** each camera row shows a scan type toggle: ENTRY / EXIT

### AC2: Default Scan Type
- **Given** a new camera is added
- **Then** its default scan type is ENTRY

### AC3: Scan Submitted with Camera's Type
- **Given** camera 1 is configured as ENTRY and camera 2 as EXIT
- **When** a student scans at camera 2
- **Then** the scan is submitted to `POST /api/v1/scans` with `scanType: "EXIT"`

### AC4: Scan Type Badge on Grid
- **Given** camera 1 is ENTRY and camera 2 is EXIT
- **Then** camera 1's cell shows green "ENTRY" badge
- **And** camera 2's cell shows blue "EXIT" badge

### AC5: Persist Configuration
- **Given** I set camera 1 = ENTRY, camera 2 = EXIT, camera 3 = ENTRY
- **When** I restart the scanner app
- **Then** the per-camera scan types are preserved

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| All cameras set to ENTRY | Valid; no EXIT scans recorded |
| Scan type changed while scanning | Takes effect on next scan |
| Camera reassigned to different device | Scan type preserved for that slot |

---

## Test Scenarios

- [ ] Per-camera scan type toggle visible in setup
- [ ] Default is ENTRY for new cameras
- [ ] Scan submitted with correct scan type
- [ ] Badge reflects configured type
- [ ] Configuration persists across restarts

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0066](US0066-multi-camera-manager.md) | Functional | MultiCameraManager | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
