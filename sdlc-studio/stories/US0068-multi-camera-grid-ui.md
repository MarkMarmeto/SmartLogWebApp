# US0068: Multi-Camera Grid UI

> **Status:** Done
> **Epic:** [EP0011: Multi-Camera Scanning](../epics/EP0011-multi-camera-scanning.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Guard Gary (Security Staff)
**I want** to see all active cameras in a responsive grid layout
**So that** I can monitor multiple scanning lanes at a glance

## Context

### Background
The main scanner page displays cameras in a responsive grid. Each cell shows a live preview, camera name, scan type badge, and status indicator. A shared result panel shows the most recent scan from any camera.

---

## Acceptance Criteria

### AC1: Grid Layouts
- **Given** the number of active cameras
- **Then** the grid layout adjusts:
  - 1 camera: full width
  - 2 cameras: side by side (2x1)
  - 3-4 cameras: 2x2 grid
  - 5-6 cameras: 3x2 grid
  - 7-8 cameras: 4x2 grid

### AC2: Camera Cell Content
- **Given** camera 1 is active with name "Gate A" and scan type ENTRY
- **Then** the cell shows:
  - Live camera preview
  - Camera name: "Gate A"
  - Scan type badge: "ENTRY" (green badge)
  - Status indicator: green dot (Scanning)

### AC3: Status Indicators
- **Given** camera status changes
- **Then** the indicator reflects:
  - Green dot: Scanning (active)
  - Yellow dot: Idle (connected but not scanning)
  - Red dot: Error (camera failure)
  - Gray dot: Offline (disconnected)

### AC4: Shared Result Panel
- **Given** student "Maria Santos, Grade 7 - STE - Ruby" scans at camera 2
- **Then** the shared result panel at the bottom shows:
  - Student name, grade, section
  - Scan type (ENTRY/EXIT)
  - Time
  - Which camera captured it
- **And** the panel auto-clears after 5 seconds

### AC5: Per-Cell Scan Result
- **Given** a successful scan on camera 1
- **Then** camera 1's cell shows a green flash for 3 seconds
- **And** displays the student name as an overlay during the flash

### AC6: Responsive Window Resize
- **Given** the window is resized
- **Then** the grid cells resize proportionally
- **And** preview maintains aspect ratio

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Camera preview freezes | Show "No Signal" overlay after 10 seconds |
| Window too small for grid | Scroll enabled; minimum cell size 200x150px |
| Rapid sequential scans on same camera | Each result shows briefly; queue if overlapping |
| Camera removed while showing | Cell shows "Disconnected" with gray overlay |
| All cameras offline | Full-screen "No Active Cameras" message with retry button |

---

## Test Scenarios

- [ ] 1 camera shows full width
- [ ] 2 cameras show side by side
- [ ] 4 cameras show 2x2 grid
- [ ] 8 cameras show 4x2 grid
- [ ] Camera name and scan type badge displayed
- [ ] Status indicators change with camera state
- [ ] Shared result panel shows last scan
- [ ] Result panel identifies which camera
- [ ] Per-cell green flash on scan
- [ ] Grid resizes on window resize

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
