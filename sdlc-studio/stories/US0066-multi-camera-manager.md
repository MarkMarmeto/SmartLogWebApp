# US0066: MultiCameraManager Service

> **Status:** Done
> **Epic:** [EP0011: Multi-Camera Scanning](../epics/EP0011-multi-camera-scanning.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Guard Gary (Security Staff)
**I want** the scanner app to manage multiple cameras simultaneously
**So that** I can scan students at multiple lanes from a single device

## Context

### Background
A new `MultiCameraManager` service in SmartLog.Scanner.Core manages 1-8 camera instances. Each camera has its own lifecycle (start, stop, restart) and platform-specific scanner. Cross-camera deduplication prevents the same QR from being processed on multiple cameras within 5 seconds.

---

## Acceptance Criteria

### AC1: Camera Lifecycle Management
- **Given** MultiCameraManager is initialized with 3 cameras configured
- **When** `StartAllAsync()` is called
- **Then** all 3 cameras begin scanning independently
- **And** each camera has its own decode thread

### AC2: Individual Camera Control
- **Given** cameras 1, 2, 3 are running
- **When** I call `StopCameraAsync(2)`
- **Then** camera 2 stops scanning
- **And** cameras 1 and 3 continue unaffected

### AC3: Camera Restart
- **Given** camera 2 encountered an error and stopped
- **When** I call `RestartCameraAsync(2)`
- **Then** camera 2 reinitializes and resumes scanning

### AC4: Cross-Camera Deduplication
- **Given** student "SL-2026-00001" scans at camera 1
- **When** the same student scans at camera 2 within 5 seconds
- **Then** the second scan is suppressed locally (not sent to server)
- **And** a "Duplicate (cross-camera)" message appears on camera 2's status

### AC5: Scan Result Routing
- **Given** a QR code is decoded on camera 3
- **Then** the scan is submitted to the server API with the scan type configured for camera 3 (ENTRY or EXIT)
- **And** the result is displayed on both camera 3's cell and the shared result panel

### AC6: Max Camera Limit
- **Given** 8 cameras are already configured
- **When** I attempt to add a 9th camera
- **Then** the system rejects with error message "Maximum 8 cameras supported"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Camera disconnected mid-scan | Camera status → Error; other cameras unaffected |
| All cameras fail | System shows "No active cameras" alert |
| Camera index out of range | Ignored with log warning |
| Same camera device assigned to two instances | Error: "Device already in use" |
| USB hub disconnected | All cameras on hub fail; others continue |
| StartAllAsync called when already running | No-op for running cameras; log warning |

---

## Test Scenarios

- [ ] Start multiple cameras simultaneously
- [ ] Stop individual camera without affecting others
- [ ] Restart a failed camera
- [ ] Cross-camera dedup within 5 seconds
- [ ] Scan routed with correct scan type
- [ ] Result appears on shared panel
- [ ] Max 8 cameras enforced
- [ ] Camera failure isolated

---

## Dependencies

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| CameraQrScannerService | Existing service | Needs scan type parameter |
| CameraEnumerationService | Existing service | Used for device discovery |

---

## Estimation

**Story Points:** 8
**Complexity:** Very High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
