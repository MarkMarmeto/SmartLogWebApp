# US0080: QR Regeneration with Confirmation Dialog

> **Status:** Done
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** QR regeneration to show a warning that the physical card will be invalidated
**So that** I don't accidentally invalidate a student's working ID card

## Context

### Background
QR regeneration is a deliberate "Replace Card" action for lost/damaged cards. The admin must confirm they understand the old physical card becomes useless. The old QR is preserved for audit but marked invalid.

---

## Acceptance Criteria

### AC1: Regenerate Button
- **Given** I am on a student's detail page
- **Then** I see a "Regenerate QR Code" button (if student has an existing QR)

### AC2: Confirmation Dialog
- **Given** I click "Regenerate QR Code"
- **Then** a confirmation dialog appears:
  - Title: "Replace Student ID Card?"
  - Body: "This will invalidate the current QR code. The student's existing physical card will stop working and they will need a new printed card."
  - Buttons: "Cancel" (default) | "Regenerate" (danger/red)

### AC3: Regeneration Flow
- **Given** I confirm regeneration
- **Then**:
  1. Old QR marked invalid (IsValid=false, InvalidatedAt set)
  2. New QR created with same StudentId, new timestamp, new HMAC
  3. Old QR's ReplacedByQrCodeId set to new QR's Id
  4. Student detail page refreshes to show new QR
  5. Success message: "QR code regenerated. Please print a new ID card."

### AC4: Audit Log Entry
- **Given** QR is regenerated
- **Then** an AuditLog entry is created:
  - Action: "QR_REGENERATED"
  - Details: StudentId, old QR Id, new QR Id

### AC5: Print Prompt
- **Given** QR regeneration succeeds
- **Then** a "Print New Card" button appears prominently
- **And** clicking it opens the CR80 card print for this student

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student has no QR code | "Generate QR Code" button instead (not regenerate) |
| Cancel clicked | No changes; dialog closes |
| Database error during regeneration | Rollback; old QR remains valid; show error |
| Regenerate twice rapidly | Second attempt finds first already invalid; regenerates from current valid |

---

## Test Scenarios

- [ ] Regenerate button visible on student detail
- [ ] Confirmation dialog appears with warning text
- [ ] Cancel leaves QR unchanged
- [ ] Confirm invalidates old QR
- [ ] New QR created with same StudentId
- [ ] Audit chain linked (old → new)
- [ ] AuditLog entry created
- [ ] Print prompt appears after regeneration

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0079](US0079-qr-invalidation-audit.md) | Functional | Invalidation and audit trail | Draft |
| [US0077](US0077-cr80-card-template.md) | UI | New card print template | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
