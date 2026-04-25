# US0072: Visitor Pass Entity & QR Generation

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to generate a configurable set of reusable visitor QR passes
**So that** the school can track visitor entry/exit using the existing scanner infrastructure

## Context

### Background
Visitor passes are pre-generated anonymous QR cards (VISITOR-001 to VISITOR-N). Each pass has a unique QR with the `SMARTLOG-V:` prefix to distinguish from student QR codes. Admin configures the pass count (default 20).

---

## Acceptance Criteria

### AC1: VisitorPass Entity
- **Given** the database schema
- **When** the migration is applied
- **Then** a `VisitorPass` table exists with:
  - `Id` (Guid, PK)
  - `PassNumber` (int, required, unique)
  - `Code` (string, max 20, required, unique) — e.g., "VISITOR-001"
  - `QrPayload` (string, max 500, required) — `SMARTLOG-V:{code}:{timestamp}:{hmac}`
  - `HmacSignature` (string, max 100)
  - `QrImageBase64` (string?, nullable)
  - `IsActive` (bool, default: true)
  - `IssuedAt` (DateTime)
  - `CurrentStatus` (enum: Available, InUse, Deactivated)

### AC2: VisitorScan Entity
- **Given** the database schema
- **When** the migration is applied
- **Then** a `VisitorScan` table exists with:
  - `Id` (Guid, PK)
  - `VisitorPassId` (Guid, FK → VisitorPass)
  - `DeviceId` (Guid, FK → Device)
  - `ScanType` (string, max 20) — ENTRY or EXIT
  - `ScannedAt` (DateTime)
  - `ReceivedAt` (DateTime)
  - `Status` (string, max 50)
  - `AcademicYearId` (Guid?, FK)

### AC3: QR Payload Format
- **Given** visitor pass VISITOR-005
- **Then** the QR payload is: `SMARTLOG-V:VISITOR-005:{unix_timestamp}:{base64_hmac}`
- **And** HMAC is computed as `HMAC-SHA256("VISITOR-005:{timestamp}")`

### AC4: Generate Passes
- **Given** `Visitor:MaxPasses` is set to 20
- **When** "Generate Passes" is triggered
- **Then** 20 VisitorPass records are created (VISITOR-001 to VISITOR-020)
- **And** each has a unique HMAC-signed QR payload
- **And** QR image is generated as Base64 PNG
- **And** status is Available

### AC5: Increase Pass Count
- **Given** 20 passes exist and admin changes `Visitor:MaxPasses` to 30
- **When** "Generate Passes" is triggered
- **Then** 10 new passes are created (VISITOR-021 to VISITOR-030)
- **And** existing 20 passes are untouched

### AC6: Decrease Pass Count
- **Given** 30 passes exist and admin changes `Visitor:MaxPasses` to 20
- **Then** passes 021-030 are deactivated (not deleted)
- **And** they no longer scan as valid

### AC7: AppSettings Seeded
- **Given** fresh database initialization
- **Then** `Visitor:MaxPasses` is seeded with value "20" in AppSettings

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| HMAC secret not configured | Use same SMARTLOG_HMAC_SECRET_KEY as student QR |
| Pass number gap (e.g., 005 deleted) | Never delete; deactivate only |
| Generate called when all passes exist | No-op; log "All passes already generated" |
| Database error during batch generation | Rollback; no partial passes |
| Very large MaxPasses (>100) | Allow but warn "Consider if this many passes are needed" |
| MaxPasses set to negative value | Error: "Maximum passes must be at least 1" |
| Concurrent generate requests | Second request waits for first to complete |

---

## Test Scenarios

- [ ] VisitorPass entity creates correctly
- [ ] VisitorScan entity creates correctly
- [ ] QR payload uses SMARTLOG-V: prefix
- [ ] HMAC is valid and verifiable
- [ ] 20 passes generated with correct codes
- [ ] Pass increase generates only new passes
- [ ] Pass decrease deactivates excess
- [ ] QR image generated as Base64
- [ ] AppSettings key seeded

---

## Dependencies

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| QrCodeService | Existing | Needs visitor QR generation method |
| SMARTLOG_HMAC_SECRET_KEY | Config | Same key as student QR |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
