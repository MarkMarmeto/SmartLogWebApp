# PL0005: Visitor QR Routing & Scan Processing — Implementation Plan

> **Status:** Done
> **Story:** [US0073: Visitor QR Routing & Scan Processing](../stories/US0073-visitor-scan-processing.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-04-18
> **Language:** C# / ASP.NET Core 8 + EF Core

## Overview

Extend `ScansApiController` to detect the `SMARTLOG-V:` QR prefix and route visitor scans to a dedicated handler. The visitor scan flow: HMAC verify → pass lookup → active check → duplicate detection (5-min window) → create `VisitorScan` record → update pass `CurrentStatus` (Available↔InUse). No SMS is ever queued for visitor scans. Response format includes `passCode`/`passNumber` instead of student fields.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Prefix Detection | `SMARTLOG-V:` routed to visitor handler; `SMARTLOG:` to student (unchanged) |
| AC2 | HMAC Verification | Constant-time comparison; invalid → `REJECTED_INVALID_QR` |
| AC3 | Pass Lookup | Find VisitorPass by code; not found → `REJECTED_INVALID_QR` |
| AC4 | Active Check | `IsActive=false` → `REJECTED_PASS_INACTIVE` |
| AC5 | Duplicate Detection | Same pass + same scan type within 5 min → `DUPLICATE` |
| AC6 | Entry Scan | Create VisitorScan(ENTRY, ACCEPTED); set pass to InUse |
| AC7 | Exit Scan | Create VisitorScan(EXIT, ACCEPTED); set pass to Available |
| AC8 | No SMS | No SmsQueue entry; no attendance notification |
| AC9 | Response Format | Return scanId, passCode, passNumber, scanType, status (no student fields) |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 + EF Core 8.0
- **Test Framework:** xUnit + Moq + EF Core InMemory

### Existing Patterns
- **ScansApiController flow:** Auth device → parse QR → verify HMAC → lookup student → check calendar → check duplicate → save scan → queue SMS → return response
- **ParseQrPayload:** Splits on `:`, checks `parts[0] == "SMARTLOG"`, returns `(studentId, timestamp, signature)` tuple
- **Duplicate detection:** Queries `Scans` table for same student + same scan type + `Status == "ACCEPTED"` within configurable window (default 5 min from AppSettings `QRCode.DuplicateScanWindowMinutes`)
- **ScanResponse model:** Has studentName, grade, section fields — visitor response needs different shape

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** The controller already has comprehensive test infrastructure (`ScansApiControllerTests` with `TestDbContextFactory`). Extend the existing test class with visitor-specific test cases after implementing the routing logic.

---

## Implementation Phases

### Phase 1: QR Prefix Routing in ScansApiController
**Goal:** Branch on QR prefix before student lookup

- [ ] Modify `ScansApiController.SubmitScan()`:
  - After existing `ParseQrPayload()` call, check if payload starts with `SMARTLOG-V:`
  - If visitor: call new `ParseVisitorQrPayload()` from QrCodeService (added in PL0004)
  - If visitor: call `HandleVisitorScanAsync(device, code, timestamp, signature, request)`
  - If student: continue existing flow unchanged
  - Unknown prefix: return `REJECTED_INVALID_QR`

### Phase 2: HandleVisitorScanAsync
**Goal:** Implement the full visitor scan pipeline

- [ ] Add private method `HandleVisitorScanAsync(Device device, string code, long timestamp, string signature, ScanSubmissionRequest request)`:
  1. **HMAC verify:** Call `QrCodeService.VerifyVisitorQrAsync(code, timestamp, signature)` → if false, return `REJECTED_INVALID_QR`
  2. **Pass lookup:** `_context.VisitorPasses.FirstOrDefaultAsync(p => p.Code == code)` → if null, return `REJECTED_INVALID_QR`
  3. **Active check:** If `!pass.IsActive`, return `REJECTED_PASS_INACTIVE`
  4. **Duplicate check:** Query `_context.VisitorScans` for same VisitorPassId + same ScanType + Status "ACCEPTED" within 5-min window → if found, return `DUPLICATE`
  5. **Create VisitorScan:** New record with ScannedAt, ReceivedAt, Status "ACCEPTED", DeviceId, AcademicYearId
  6. **Update pass status:** ENTRY → `CurrentStatus = "InUse"`, EXIT → `CurrentStatus = "Available"`
  7. **Save:** Single `SaveChangesAsync()` call (scan + pass status update in one transaction)
  8. **Update device:** `device.LastSeenAt = DateTime.UtcNow`
  9. **No SMS:** Explicitly skip `QueueAttendanceNotificationAsync()` — no SMS for visitors
  10. **Return:** `VisitorScanResponse` with scanId, passCode, passNumber, scanType, status

### Phase 3: VisitorScanResponse Model
**Goal:** Define visitor-specific response shape

- [ ] Add `VisitorScanResponse` class in `ScansApiController.cs` (or separate file):
  ```
  Guid ScanId, string PassCode, int PassNumber, string ScanType, string Status, DateTime ScannedAt, string? Message
  ```
- [ ] Return `Ok(new VisitorScanResponse { ... })` from `HandleVisitorScanAsync`

### Phase 4: Testing
**Goal:** Comprehensive visitor scan tests

- [ ] Add visitor test cases to `ScansApiControllerTests.cs`:
  - Test: SMARTLOG-V: prefix routes to visitor handler
  - Test: SMARTLOG: prefix still routes to student handler
  - Test: Invalid HMAC returns REJECTED_INVALID_QR
  - Test: Pass not found returns REJECTED_INVALID_QR
  - Test: Inactive pass returns REJECTED_PASS_INACTIVE
  - Test: Duplicate within 5 min returns DUPLICATE
  - Test: ENTRY sets pass CurrentStatus to InUse
  - Test: EXIT sets pass CurrentStatus to Available
  - Test: No SMS queued (verify `_smsService` never called)
  - Test: Response has passCode and passNumber, no student fields
  - Test: EXIT when Available still accepted
  - Test: ENTRY when InUse still accepted

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Unknown prefix (not SMARTLOG: or SMARTLOG-V:) | `ParseQrPayload` returns null AND `ParseVisitorQrPayload` returns null → return `REJECTED_INVALID_QR` | Phase 1 |
| 2 | EXIT scan when pass is Available | Accept — guard may have missed entry. Create scan, keep status Available | Phase 2 |
| 3 | ENTRY scan when pass is InUse | Accept — guard may have missed exit. Create scan, keep status InUse | Phase 2 |
| 4 | Malformed visitor QR (missing fields) | `ParseVisitorQrPayload` returns null → `REJECTED_INVALID_QR` | Phase 1 |
| 5 | Concurrent scans on same pass | EF Core `SaveChangesAsync` serializes within same DbContext; concurrent requests use separate contexts — last write wins on status (acceptable for pass status) | Phase 2 |
| 6 | Device not authenticated | Existing auth check runs before QR parsing — returns 401 (no change needed) | Phase 1 |
| 7 | Visitor QR with expired HMAC timestamp (>24h) | Accept — visitor passes are permanent; timestamp is for signing only, not expiry | Phase 2 |
| 8 | Pass code contains special characters | `ParseVisitorQrPayload` validates code format (alphanumeric + hyphen only) | Phase 1 |

**Coverage:** 8/8 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing student scan flow | High | Prefix check is first branch; student path untouched. Existing tests validate. |
| Duplicate detection query perf | Low | Index on `(VisitorPassId, ScannedAt)` covers the query |
| Concurrent status update race | Low | Acceptable — last write wins; status is informational |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] Unit tests written and passing
- [ ] Edge cases handled
- [ ] Existing student scan tests still pass
- [ ] Build succeeds (0 errors)
