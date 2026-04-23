# US0079: QR Invalidation & Audit Trail

> **Status:** Done
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** system
**I want** QR code invalidation to preserve old records with timestamps and replacement links
**So that** there is a complete audit trail of all QR codes ever issued

## Context

### Background
Currently QR regeneration deletes the old record. This story adds `InvalidatedAt` and `ReplacedByQrCodeId` fields so old QR records are preserved. The scan API already rejects `IsValid = false` QR codes.

---

## Acceptance Criteria

### AC1: QrCode Entity Extended
- **Given** the QrCode entity
- **Then** two new fields exist:
  - `InvalidatedAt` (DateTime?, nullable) — when the QR was invalidated
  - `ReplacedByQrCodeId` (Guid?, FK → QrCode, nullable) — link to replacement QR

### AC2: InvalidateAsync Method
- **Given** student "SL-2026-00001" has a valid QR code
- **When** `QrCodeService.InvalidateAsync(studentId)` is called
- **Then** the existing QR is updated:
  - `IsValid = false`
  - `InvalidatedAt = DateTime.UtcNow`
- **And** the old record is NOT deleted

### AC3: Regenerate with Audit Link
- **Given** student "SL-2026-00001" has an invalidated QR (Id = "abc-123")
- **When** a new QR is generated
- **Then** the new QR record is created with same StudentId
- **And** the old QR's `ReplacedByQrCodeId` is set to the new QR's Id

### AC4: Audit Trail Query
- **Given** a student has had 3 QR codes over time (QR-A → QR-B → QR-C)
- **Then** the audit chain is: QR-A (InvalidatedAt, ReplacedBy: QR-B) → QR-B (InvalidatedAt, ReplacedBy: QR-C) → QR-C (current, valid)

### AC5: Scan Rejection Unchanged
- **Given** an old QR with `IsValid = false` is scanned
- **Then** the response is `REJECTED_QR_INVALIDATED` (existing behaviour, no change needed)

### AC6: Migration
- **Given** existing QrCode records
- **When** the migration runs
- **Then** `InvalidatedAt` and `ReplacedByQrCodeId` are added as nullable columns
- **And** existing records have both as NULL

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Invalidate already-invalid QR | No-op; return existing InvalidatedAt |
| Student with no QR code | InvalidateAsync returns false |
| ReplacedByQrCodeId points to deleted record | FK is nullable; orphan reference logged |
| Multiple QR regenerations in same day | Each creates new record with proper chain |

---

## Test Scenarios

- [ ] InvalidatedAt and ReplacedByQrCodeId fields exist
- [ ] InvalidateAsync sets IsValid=false and InvalidatedAt
- [ ] Old record preserved (not deleted)
- [ ] New QR links back via ReplacedByQrCodeId
- [ ] Audit chain traversable (A → B → C)
- [ ] Invalid QR scan still rejected
- [ ] Migration adds nullable columns

---

## Dependencies

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| EF Core migration | Technical | Required |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
