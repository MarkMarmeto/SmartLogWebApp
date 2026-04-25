# EP0011: Multi-Camera Scanning

> **Status:** In Progress (re-opened 2026-04-24 — V2.1 scanner additions US0088-US0092)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V2 — Phase 2 (Feature Enhancements)
> **Project:** SmartLogScannerApp

## Summary

Enable configurable 1-8 simultaneous cameras per scanner device with adaptive decode throttling, per-camera scan type (ENTRY/EXIT), responsive grid UI, and error isolation. This allows schools to set up multiple scanning stations from a single device, significantly improving throughput at gates.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| TRD | Platform | .NET 8 MAUI (Windows + macOS) | Platform-specific camera handling |
| TRD | Performance | ZXing.Net QR decode | CPU-bound; adaptive throttling needed |
| TRD | Hardware | USB bandwidth limits | USB 3.0 required for 3+ cameras |
| TRD | Architecture | CameraQrScannerService | Must extend to multi-camera lifecycle |

---

## Business Context

### Problem Statement
Schools with wide gates or multiple entry points need simultaneous scanning stations. Currently, one scanner device = one camera. Setting up multiple devices is expensive and complex. Multi-camera support from a single device reduces hardware costs and simplifies administration.

### Value Proposition
- One laptop can serve multiple scanning lanes (up to 8)
- Per-camera scan type allows separate ENTRY and EXIT lanes
- Adaptive throttling ensures stable performance across camera counts
- Error isolation prevents one camera failure from disrupting others
- Responsive grid UI provides clear status for all cameras at a glance

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Cameras per device | 1 | Up to 8 | Configuration |
| Scan throughput | ~12 scans/min | ~60 scans/min (5 cameras) | Load test |
| Camera failure impact | Full stop | Single camera only | Error isolation test |

---

## Scope

### In Scope
- **MultiCameraManager:** New service managing N camera instances with start/stop/restart per camera
- **CameraInstance model:** Index, CameraDeviceId, DisplayName, ScanType (ENTRY/EXIT), IsActive, Status, DecodeThrottle
- **Adaptive decode throttle:** Total decode budget ~30fps shared across cameras (1 cam: every 5th frame, 3-4 cams: every 8th, 5-8 cams: every 10th-15th)
- **Per-camera scan type:** Each camera independently configured as ENTRY or EXIT
- **Responsive grid UI:** 1=full, 2=side-by-side, 3-4=2x2, 5-6=3x2, 7-8=4x2; each cell shows preview, name, scan type badge, status
- **Settings/Setup page:** Camera count selector (1-8), per-camera device dropdown, scan type toggle, enable/disable
- **Error isolation:** One camera failure doesn't affect others; per-camera status indicator
- **USB 3.0 warning:** UI warning when configuring 3+ cameras without USB 3.0

### Out of Scope
- Network/IP cameras (USB only)
- Camera PTZ controls
- Video recording or playback
- Cross-device camera sharing
- Server-side multi-camera management

### Affected Personas
- **Guard Gary (Security):** Operates multi-camera scanning at gate
- **SuperAdmin Tony (IT Admin):** Configures camera count and assignments

---

## Acceptance Criteria (Epic Level)

- [ ] Scanner app supports 1-8 simultaneous USB cameras
- [ ] Each camera can be independently configured as ENTRY or EXIT
- [ ] Adaptive decode throttle adjusts frame skip based on active camera count
- [ ] Responsive grid layout adjusts to camera count (1=full to 8=4x2)
- [ ] Each camera cell shows live preview, name, scan type badge, and status indicator
- [ ] Camera failure is isolated — other cameras continue scanning
- [ ] Settings page allows configuring camera count, device selection, and scan type per camera
- [ ] USB 3.0 warning displayed when 3+ cameras configured
- [ ] Shared result panel shows most recent scan from any camera
- [ ] Each camera gets its own decode thread (no thread contention)

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0005: Scanner Integration | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | Independent scanner enhancement |

---

## Risks & Assumptions

### Assumptions
- Target devices have USB 3.0 ports for 3+ cameras
- Windows and macOS both support multiple USB camera enumeration
- ZXing.Net decode can run on separate threads without contention
- 320x240 decode resolution is sufficient for QR recognition

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CPU overload with 5+ cameras | Medium | Medium | Adaptive throttle; lower decode resolution |
| USB 2.0 bandwidth limit | Medium | Medium | USB 3.0 requirement; test and warn |
| Camera enumeration differences across OS | Medium | Medium | Platform-specific handlers already exist |
| Memory pressure with multiple MediaCapture instances | Low | Medium | Monitor memory; lazy initialization |

---

## Technical Considerations

### Architecture Impact
- New `MultiCameraManager` service in SmartLog.Scanner.Core
- New `CameraInstance` model
- Modified `MainViewModel` and `MainPage` for multi-camera grid
- Modified `SetupViewModel` for multi-camera configuration
- Each camera instance wraps a platform-specific scanner (WindowsCameraScanner / MacCameraScanner)
- Decode throttle is dynamic: recalculated when cameras are added/removed

### Integration Points
- `CameraQrScannerService` — accept scanType per invocation
- `CameraEnumerationService` — enumerate multiple available cameras
- Platform handlers: `WindowsCameraScanner` (MediaCapture), `MacCameraScanner` (AVFoundation)
- `ScanApiClient` — unchanged; each camera posts scans independently
- Cross-camera dedup: if same QR scanned on two cameras within 5s, suppress duplicate

### Key Files to Modify
- **New:** `SmartLog.Scanner.Core/Services/MultiCameraManager.cs`
- **New:** `SmartLog.Scanner.Core/Models/CameraInstance.cs`
- **Modify:** `SmartLog.Scanner/ViewModels/MainViewModel.cs` (multi-camera support)
- **Modify:** `SmartLog.Scanner/Views/MainPage.xaml` (grid layout)
- **Modify:** `SmartLog.Scanner/ViewModels/SetupViewModel.cs` (multi-camera config)
- **Modify:** `SmartLog.Scanner/Views/SetupPage.xaml` (camera count, per-camera settings)
- **Modify:** `SmartLog.Scanner.Core/Services/CameraQrScannerService.cs` (per-call scanType)
- **Modify:** Platform handlers for multiple CameraQrView instances

---

## Sizing

**Story Points:** TBD (estimated 6-8 stories)
**Estimated Story Count:** 6-8

**Complexity Factors:**
- Platform-specific camera APIs (Windows MediaCapture, macOS AVFoundation)
- Thread management for parallel decode
- Adaptive throttling algorithm
- Responsive grid layout for variable camera counts
- Cross-camera duplicate detection

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0066](../stories/US0066-multi-camera-manager.md) | MultiCameraManager Service | 8 | Done |
| [US0067](../stories/US0067-adaptive-decode-throttle.md) | Adaptive Decode Throttle | 5 | Done |
| [US0068](../stories/US0068-multi-camera-grid-ui.md) | Multi-Camera Grid UI | 5 | Done |
| [US0069](../stories/US0069-per-camera-scan-type.md) | Per-Camera Scan Type Configuration | 3 | Done |
| [US0070](../stories/US0070-camera-error-isolation.md) | Camera Error Isolation & Health | 5 | Done |
| [US0071](../stories/US0071-multi-camera-setup-page.md) | Multi-Camera Setup Page | 5 | Done |
| [US0088](../../SmartLogScannerApp/sdlc-studio/stories/US0088-multi-camera-windows-compatibility.md) | Multi-Camera — Windows Platform Compatibility Verification (Scanner) | 3 | Draft |
| [US0089](../../SmartLogScannerApp/sdlc-studio/stories/US0089-unify-scan-type-to-device-level.md) | Unify Scan Type to Device-Level (deprecates US0069) (Scanner) | 3 | Draft |
| [US0090](../../SmartLogScannerApp/sdlc-studio/stories/US0090-scan-payload-camera-identity.md) | Scan Payload — Include CameraIndex + CameraName (Scanner) | 3 | Draft |
| [US0091](../../SmartLogScannerApp/sdlc-studio/stories/US0091-scanner-section-name-trim-and-program-code.md) | Scanner Tile — Fix Section Name Trimming, Show Program Code (Scanner) | 2 | Draft |
| [US0092](../../SmartLogScannerApp/sdlc-studio/stories/US0092-scanner-datetime-prominent-leftmost.md) | Scanner Header — Enlarge Date/Time, Anchor Left-Most (Scanner) | 1 | Draft |

**Total:** 43 story points across 11 stories (6 Done, 5 Draft)

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0011`

---

## Open Questions

- [x] Maximum camera count? — **Decision: Configurable 1-8**
- [x] Per-camera scan type? — **Decision: Yes, each camera independently ENTRY or EXIT**
- [x] Throttling strategy? — **Decision: Adaptive frame skip based on active camera count**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2 feature brainstorm |
| 2026-04-24 | Claude | Re-opened for V2.1 scanner-side additions: Windows compat (US0088), unify scan type to device-level / deprecate US0069 (US0089), camera identity in scan payload (US0090), section-name fix (US0091), date/time prominence (US0092). Primary story files live in the Scanner project registry. |
