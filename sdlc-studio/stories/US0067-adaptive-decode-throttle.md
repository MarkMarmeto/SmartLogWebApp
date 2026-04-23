# US0067: Adaptive Decode Throttle

> **Status:** Done
> **Epic:** [EP0011: Multi-Camera Scanning](../epics/EP0011-multi-camera-scanning.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Guard Gary (Security Staff)
**I want** the scanner app to automatically adjust QR decode frequency based on camera count
**So that** the system remains performant even with multiple cameras

## Context

### Background
ZXing.Net QR decode is CPU-intensive. With multiple cameras, the total decode budget (~30fps) must be shared. The throttle dynamically adjusts frame skip count per camera as cameras are added/removed.

---

## Acceptance Criteria

### AC1: Single Camera Throttle
- **Given** 1 active camera
- **Then** decode runs every 5th frame (~6 decode fps)

### AC2: Two Camera Throttle
- **Given** 2 active cameras
- **Then** each camera decodes every 5th frame (~12 total decode fps)

### AC3: Three-Four Camera Throttle
- **Given** 3-4 active cameras
- **Then** each camera decodes every 8th frame (~4-5 decode fps each)

### AC4: Five-Eight Camera Throttle
- **Given** 5-8 active cameras
- **Then** each camera decodes every 10th frame (5 cameras) to every 15th frame (8 cameras)

### AC5: Dynamic Recalculation
- **Given** 4 cameras running (every 8th frame)
- **When** camera 4 is stopped
- **Then** remaining 3 cameras recalculate to every 8th frame
- **And** the transition is seamless (no pause in scanning)

### AC6: Decode Resolution
- **Given** multiple cameras are active
- **Then** decode resolution is 320x240 pixels (regardless of preview resolution)
- **And** preview resolution remains at camera native resolution

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| CPU usage exceeds 80% | Increase frame skip count dynamically |
| All cameras added at once | Calculate throttle once for final count |
| Camera produces no frames | Skip throttle calculation for that camera |
| Very slow USB camera (<15fps native) | Decode every 3rd frame minimum |
| Throttle value already optimal | No recalculation triggered |

---

## Test Scenarios

- [ ] 1 camera: every 5th frame decoded
- [ ] 2 cameras: every 5th frame each
- [ ] 4 cameras: every 8th frame each
- [ ] 8 cameras: every 10-15th frame each
- [ ] Throttle recalculates when camera added/removed
- [ ] Decode resolution is 320x240
- [ ] Preview resolution unaffected

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
