# PL0008: Scanner Visitor Scan Display — Implementation Plan

> **Status:** Done
> **Story:** [US0076: Scanner Visitor Scan Display](../stories/US0076-scanner-visitor-display.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-04-18
> **Language:** C# / .NET MAUI 8
> **Project:** SmartLogScannerApp

## Overview

Extend the Scanner App to handle visitor scan responses from the server. When the API response contains `passCode`/`passNumber` (and no `studentName`), display "Visitor Pass #N" with a blue result card instead of the green student card. Handle rejection (`REJECTED_PASS_INACTIVE`) and duplicate states with appropriate colors and messages. Student scan display remains unchanged.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Visitor Scan Response | Display "Visitor Pass #5", scan type, status, timestamp when `passCode` present |
| AC2 | Visual Distinction | Blue (#2196F3) card for visitors instead of green for students |
| AC3 | Rejected Visitor Scan | Red card with "REJECTED — Pass Inactive" |
| AC4 | Duplicate Visitor Scan | Yellow card with "DUPLICATE — Already scanned" |
| AC5 | Backward Compatibility | Student scans display unchanged (name, grade, section) |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** .NET MAUI 8 (Mac Catalyst + Windows)
- **Test Framework:** xUnit + Moq (SmartLog.Scanner.Tests)

### Existing Patterns
- **Scan result display:** `MainViewModel` receives `ScanApiResponse`, sets `StudentName`, `StudentGrade`, `StudentSection`, `ResultColor` properties bound to XAML
- **Color mapping:** Green for ACCEPTED, red for REJECTED, yellow for DUPLICATE — set via `ResultColor` property
- **Multi-camera flash:** `TriggerSlotFlash()` sets `FlashStudentName` on the camera slot card
- **ScanApiResponse model:** Deserialized from server JSON — currently has `StudentName`, `Grade`, `Section`, `Status`, `ScanType`

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Changes are primarily in view model display logic and response model parsing. Test the response parsing and display property mapping after implementation.

---

## Implementation Phases

### Phase 1: Extend ScanApiResponse Model
**Goal:** Add visitor fields to the response model

- [ ] Modify `SmartLog.Scanner.Core/Models/ScanApiResponse.cs` (or wherever the response model lives):
  - Add `string? PassCode { get; set; }`
  - Add `int? PassNumber { get; set; }`
  - Add computed: `bool IsVisitorScan => PassCode != null`
- [ ] Ensure JSON deserialization handles both student responses (with studentName) and visitor responses (with passCode) gracefully — missing fields default to null

### Phase 2: MainViewModel Display Logic
**Goal:** Branch display based on IsVisitorScan

- [ ] Modify `MainViewModel` scan result handler (the method that processes `ScanApiResponse`):
  - If `response.IsVisitorScan`:
    - Set `StudentName = $"Visitor Pass #{response.PassNumber}"`
    - Set `StudentGrade = ""` (or hide grade row)
    - Set `StudentSection = ""` (or hide section row)
    - Set `ResultColor` based on status:
      - `ACCEPTED` → `#2196F3` (blue)
      - `REJECTED_PASS_INACTIVE` → `#F44336` (red)
      - `DUPLICATE` → `#FF9800` (yellow/orange)
    - Set `ResultMessage`:
      - `ACCEPTED` → scan type ("ENTRY" or "EXIT")
      - `REJECTED_PASS_INACTIVE` → "REJECTED — Pass Inactive"
      - `DUPLICATE` → "DUPLICATE — Already scanned"
  - If student scan: existing logic unchanged

### Phase 3: Multi-Camera Flash Update
**Goal:** Show visitor info in camera slot flash

- [ ] Modify `TriggerSlotFlash()` or `OnMultiCameraScanCompleted`:
  - If visitor scan: `FlashStudentName = $"Visitor #{response.PassNumber} — {response.ScanType}"`
  - If student scan: existing behavior (student name)

### Phase 4: Offline Queue Compatibility
**Goal:** Ensure visitor scans queue correctly when offline

- [ ] Verify `OfflineQueueService` stores the full `ScanSubmissionRequest` (it should — the QR payload is the same format `SMARTLOG-V:...`)
- [ ] Verify `BackgroundSyncService` replays queued scans correctly — server handles routing by prefix
- [ ] No changes expected — the scanner doesn't parse the QR prefix; it just forwards the payload

### Phase 5: Testing
**Goal:** Test visitor response handling

- [ ] Add tests in `SmartLog.Scanner.Tests`:
  - Test: `ScanApiResponse.IsVisitorScan` returns true when PassCode is set
  - Test: `ScanApiResponse.IsVisitorScan` returns false when PassCode is null (student scan)
  - Test: Visitor ACCEPTED scan sets blue color
  - Test: Visitor REJECTED scan sets red color
  - Test: Visitor DUPLICATE scan sets yellow color
  - Test: Student scan display properties unchanged

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Server returns unknown format | If neither `studentName` nor `passCode` present, show generic "Scan processed" with raw status | Phase 2 |
| 2 | Network error during visitor scan | Existing offline queue behavior — scan queued for retry. No special visitor handling needed | Phase 4 |
| 3 | Visitor scan in offline queue | Stored as regular `ScanSubmissionRequest`; server handles prefix routing on replay | Phase 4 |
| 4 | Pass number is very high (e.g., #999) | Display as-is — "Visitor Pass #999" fits in the result card | Phase 2 |
| 5 | Server returns both student and visitor fields | Check `PassCode != null` first (visitor takes precedence); this shouldn't happen but handles gracefully | Phase 2 |

**Coverage:** 5/5 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Response model breaking change | Medium | New fields are nullable — old server responses still work (null defaults) |
| Color accessibility | Low | Blue (#2196F3) has sufficient contrast on white; follows Material Design |
| Offline queue format change | Low | No format change — same `ScanSubmissionRequest` payload |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] Visitor scan shows blue card with pass number
- [ ] Rejected/duplicate states display correctly
- [ ] Student scans unchanged
- [ ] Offline queue works for visitor scans
- [ ] Tests passing
- [ ] Build succeeds on both Mac Catalyst and Windows TFMs
