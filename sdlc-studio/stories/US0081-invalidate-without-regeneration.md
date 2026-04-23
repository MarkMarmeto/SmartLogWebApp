# US0081: Invalidate QR Without Regeneration

> **Status:** Done
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to invalidate a student's QR code without generating a new one
**So that** I can immediately disable a lost card without issuing a replacement right away

## Context

### Background
When a student reports a lost card, the admin may want to invalidate it immediately to prevent misuse, but wait to issue a new card (e.g., until the student pays for replacement). This is "Invalidate Only" — no new QR is created.

---

## Acceptance Criteria

### AC1: Invalidate Button
- **Given** I am on a student's detail page and the student has a valid QR
- **Then** I see an "Invalidate Card" button (separate from "Regenerate QR Code")

### AC2: Confirmation Dialog
- **Given** I click "Invalidate Card"
- **Then** a dialog appears:
  - Title: "Invalidate Student ID Card?"
  - Body: "This will disable the student's current QR code. They will not be able to scan until a new card is issued."
  - Buttons: "Cancel" | "Invalidate" (danger)

### AC3: Invalidation
- **Given** I confirm
- **Then** the QR is marked `IsValid = false` with `InvalidatedAt` set
- **And** NO new QR is generated
- **And** student detail shows "QR Status: Invalidated" with option to "Generate New QR"

### AC4: Scanning Blocked
- **Given** the invalidated QR is scanned at the gate
- **Then** the response is `REJECTED_QR_INVALIDATED`

### AC5: Generate After Invalidation
- **Given** the student's QR is invalidated (no valid QR exists)
- **When** admin clicks "Generate New QR"
- **Then** a new QR is created and linked to the old one via `ReplacedByQrCodeId`

### AC6: Audit Log
- **Given** QR is invalidated
- **Then** AuditLog entry: Action "QR_INVALIDATED", StudentId, QR Id

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student already has no valid QR | "Invalidate" button hidden; only "Generate QR" shown |
| Invalidate then re-invalidate | No-op on second attempt |
| Student scans between invalidation and new card | REJECTED_QR_INVALIDATED at gate |

---

## Test Scenarios

- [ ] Invalidate button visible for valid QR
- [ ] Confirmation dialog shows correct warning
- [ ] QR marked invalid without new generation
- [ ] Student detail shows "Invalidated" status
- [ ] "Generate New QR" option available after invalidation
- [ ] Scanning rejected after invalidation
- [ ] AuditLog entry created

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0079](US0079-qr-invalidation-audit.md) | Functional | InvalidateAsync method | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
