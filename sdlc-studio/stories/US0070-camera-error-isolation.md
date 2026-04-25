# US0070: Camera Error Isolation & Health

> **Status:** Done
> **Epic:** [EP0011: Multi-Camera Scanning](../epics/EP0011-multi-camera-scanning.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Guard Gary (Security Staff)
**I want** one camera failure to not affect the other cameras
**So that** scanning continues on working cameras even when one malfunctions

## Context

### Background
Each camera instance runs in isolation. A failure (disconnect, driver error, decode exception) affects only that camera. The UI shows per-camera health status. USB 3.0 warning is shown for 3+ cameras.

---

## Acceptance Criteria

### AC1: Error Isolation
- **Given** cameras 1, 2, 3 are scanning
- **When** camera 2's USB cable is disconnected
- **Then** camera 2 status changes to "Error" (red indicator)
- **And** cameras 1 and 3 continue scanning without interruption

### AC2: Auto-Recovery Attempt
- **Given** camera 2 enters Error state
- **Then** the system attempts to reconnect every 10 seconds (max 3 attempts)
- **And** if reconnected, camera resumes scanning
- **And** if all retries fail, status changes to "Offline"

### AC3: Per-Camera Health Display
- **Given** I am on the main scanner page
- **Then** each camera cell shows:
  - Frame rate indicator (e.g., "6 fps")
  - Last decode time (e.g., "2ms ago")
  - Error message if applicable

### AC4: USB 3.0 Warning
- **Given** 3 or more cameras are configured in setup
- **Then** a warning banner shows: "3+ cameras recommended with USB 3.0 ports for optimal performance"

### AC5: Manual Camera Restart
- **Given** camera 2 is in Error or Offline state
- **Then** a "Restart" button appears on camera 2's cell
- **When** I click Restart
- **Then** camera 2 reinitializes and attempts to resume scanning

### AC6: Setup Page Camera Health
- **Given** I am on the Setup page
- **Then** each configured camera shows:
  - Connected / Disconnected status
  - Device name and ID
  - "Test" button to verify camera works

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| All cameras disconnect | "No Active Cameras" message; retry all |
| Camera driver crash | Caught by exception handler; camera → Error state |
| Decode thread hangs | Watchdog timer (30s) kills and restarts thread |
| Camera returns black frames | Detect after 30 frames; status → "No Signal" |
| USB hub power insufficient | Multiple cameras may fail; show "Check USB power" hint |

---

## Test Scenarios

- [ ] Camera disconnect doesn't affect other cameras
- [ ] Error camera shows red indicator
- [ ] Auto-recovery attempts (3 retries)
- [ ] Offline state after retries exhausted
- [ ] Frame rate indicator visible per camera
- [ ] USB 3.0 warning for 3+ cameras
- [ ] Manual restart button works
- [ ] Setup page shows camera health
- [ ] Test button verifies camera connection

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0066](US0066-multi-camera-manager.md) | Functional | MultiCameraManager | Draft |
| [US0068](US0068-multi-camera-grid-ui.md) | UI | Grid cells for status display | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
