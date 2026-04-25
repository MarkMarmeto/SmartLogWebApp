# US0073: Visitor QR Routing & Scan Processing

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** system
**I want** to detect visitor QR codes by prefix and process them separately from student scans
**So that** visitor entry/exit is tracked without triggering SMS notifications

## Context

### Background
The ScansApiController parses the QR prefix: `SMARTLOG:` routes to student scan flow (existing), `SMARTLOG-V:` routes to visitor scan flow (new). Visitor scans create VisitorScan records and update pass status. No SMS is queued.

---

## Acceptance Criteria

### AC1: Prefix Detection
- **Given** a QR payload `SMARTLOG-V:VISITOR-005:1739512547:BASE64_HMAC`
- **When** `POST /api/v1/scans` receives it
- **Then** it is routed to the visitor scan handler (not student)

### AC2: HMAC Verification
- **Given** a visitor QR payload
- **When** the HMAC is verified
- **Then** it uses constant-time comparison (same as student QR)
- **And** invalid HMAC returns `REJECTED_INVALID_QR`

### AC3: Pass Lookup
- **Given** QR contains code "VISITOR-005"
- **When** the system looks up the pass
- **Then** it finds VisitorPass with Code = "VISITOR-005"
- **And** if not found, returns `REJECTED_INVALID_QR`

### AC4: Active Check
- **Given** pass VISITOR-005 has `IsActive = false`
- **When** scanned
- **Then** response status is `REJECTED_PASS_INACTIVE`

### AC5: Duplicate Detection
- **Given** VISITOR-005 scanned ENTRY at 8:00:00
- **When** scanned ENTRY again at 8:02:00 (within 5 minutes)
- **Then** response status is `DUPLICATE`

### AC6: Entry Scan
- **Given** pass VISITOR-005 has `CurrentStatus = Available`
- **When** scanned as ENTRY
- **Then** VisitorScan created with ScanType = "ENTRY", Status = "ACCEPTED"
- **And** pass `CurrentStatus` updated to `InUse`

### AC7: Exit Scan
- **Given** pass VISITOR-005 has `CurrentStatus = InUse`
- **When** scanned as EXIT
- **Then** VisitorScan created with ScanType = "EXIT", Status = "ACCEPTED"
- **And** pass `CurrentStatus` updated to `Available`

### AC8: No SMS
- **Given** a visitor scan is accepted
- **Then** no SmsQueue entry is created
- **And** no attendance notification is triggered

### AC9: Response Format
- **Given** a successful visitor scan
- **Then** the API response includes:
  - `scanId`: the VisitorScan Id
  - `passCode`: "VISITOR-005"
  - `passNumber`: 5
  - `scanType`: "ENTRY"
  - `status`: "ACCEPTED"
  - (no studentName, grade, section fields)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Unknown prefix (not SMARTLOG: or SMARTLOG-V:) | Return REJECTED_INVALID_QR |
| EXIT scan when pass is Available | Accept (guard may have missed entry) |
| ENTRY scan when pass is InUse | Accept (guard may have missed exit) |
| Malformed visitor QR (missing fields) | Return REJECTED_INVALID_QR |
| Concurrent scans on same pass | Database lock prevents race condition |
| Device not authenticated | Return 401 (existing auth check) |
| Visitor QR with expired HMAC timestamp (>24h) | Accept (visitor passes are permanent, timestamp is for signing only) |
| Pass code contains special characters | Return REJECTED_INVALID_QR |

---

## Test Scenarios

- [ ] SMARTLOG-V: prefix routes to visitor handler
- [ ] SMARTLOG: prefix routes to student handler (unchanged)
- [ ] HMAC verification works for visitor QR
- [ ] Invalid HMAC rejected
- [ ] Pass not found returns REJECTED_INVALID_QR
- [ ] Inactive pass returns REJECTED_PASS_INACTIVE
- [ ] Duplicate within 5 min rejected
- [ ] ENTRY sets pass to InUse
- [ ] EXIT sets pass to Available
- [ ] No SMS queued for visitor scans
- [ ] Response has passCode/passNumber (no student fields)

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0072](US0072-visitor-pass-entity-generation.md) | Schema | VisitorPass/VisitorScan entities | Draft |
| [US0030](US0030-scan-ingestion-api.md) | Functional | ScansApiController | Ready |
| [US0031](US0031-qr-validation.md) | Functional | QR/HMAC validation | Ready |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
