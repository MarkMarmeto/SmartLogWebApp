# EP0013: QR Code Permanence & Card Redesign

> **Status:** In Progress
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Re-opened:** 2026-04-27 (V2.1 — landscape card redesign + bulk print + branding settings)
> **Target Release:** V2 — Phase 2 (Feature Enhancements)

## Summary

Confirm QR code lifetime permanence (already functional — StudentId doesn't change), redesign the ID card to driver's license size (CR80: 85.6mm x 53.98mm) without academic year, create a separate enrollment sticker print system, and improve QR regeneration flow to keep old records for audit trail.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| TRD | Data | QrCode.StudentId is permanent | QR already persists across years — no change needed |
| TRD | Security | HMAC-SHA256 signing | Regenerated QR uses same StudentId, new timestamp |
| PRD | Physical | CR80 card standard (85.6mm x 53.98mm) | Card template must fit driver's license dimensions |

---

## Business Context

### Problem Statement
The current QR card template displays S.Y. (School Year), Grade, and Section — all year-specific data that becomes invalid after promotion. Schools print new cards every year unnecessarily since the QR code itself is already permanent (tied to StudentId, not enrollment). Additionally, the card lacks a standard physical size specification.

### Value Proposition
- One-time card printing saves schools money (no annual reprinting)
- Driver's license-size cards are durable and wallet-compatible
- Enrollment stickers (per-year) are cheap and easy to apply
- QR regeneration keeps old records for audit trail (lost/damaged card flow)
- Clear separation: permanent front (name, ID, QR) + annual sticker back (S.Y., Grade, Program, Section)

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Cards printed per student per year | 1 (full reprint) | 1 sticker only | Print job count |
| QR regeneration data loss | Old QR deleted | Old QR preserved | QrCode.InvalidatedAt field |
| Card size standardization | Undefined | CR80 (85.6mm x 53.98mm) | Template specification |

---

## Scope

### In Scope
- **Card template redesign:** CR80 size (85.6mm x 53.98mm), permanent front with school logo, student name, StudentId, LRN, photo, QR code — NO academic year
- **Card back:** Sticker area with 4 slots for annual enrollment stickers
- **Enrollment sticker print page:** `/Admin/PrintEnrollmentSticker` — batch print stickers by section/grade, sticker size ~75mm x 15mm, content: "S.Y. 2026-2027 | Grade 8 | STE | Ruby"
- **QR regeneration improvement:** Old QR marked `IsValid = false` with `InvalidatedAt` timestamp (NOT deleted); new QR created with new timestamp but same StudentId; add `ReplacedByQrCodeId` for audit chain
- **Invalidate without regeneration:** Admin can invalidate a lost card without issuing a new one immediately
- **Regeneration warning dialog:** "This will invalidate the current physical card. The student will need a new printed card."
- **PrintQrCode page redesign:** Remove S.Y. from card layout, apply CR80 dimensions

### Out of Scope
- PVC card printer integration (schools use their own printing solution)
- NFC or RFID card features
- Digital ID card (mobile app)
- Bulk card printing service

### Affected Personas
- **Admin Amy (Administrator):** Prints cards and stickers, manages regeneration
- **Students (Indirect):** Receive permanent ID cards with annual stickers

---

## Acceptance Criteria (Epic Level)

- [ ] Card template uses CR80 dimensions (85.6mm x 53.98mm / 3.375" x 2.125")
- [ ] Card front shows: school logo, student name, StudentId, LRN, photo, QR code — NO S.Y.
- [ ] Card back has 4 sticker slots for annual enrollment info
- [ ] Enrollment sticker page prints stickers with S.Y., Grade, Program, Section
- [ ] Sticker batch print supports filtering by section and grade
- [ ] QR regeneration marks old QR as `IsValid = false` with `InvalidatedAt` timestamp
- [ ] Old QR records are preserved (not deleted) with `ReplacedByQrCodeId` link
- [ ] Admin can invalidate a QR without regenerating (lost card scenario)
- [ ] Regeneration shows confirmation warning dialog
- [ ] QR code continues to work across academic years without regeneration
- [ ] PrintQrCode page uses new card template layout

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0003: Student Management | Epic | Done | Development |
| EP0010: Programs & Sections Overhaul | Epic | Done | Development (Program field on sticker) |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | Independent feature |

---

## Risks & Assumptions

### Assumptions
- StudentId ({SchoolCode}-{Year}-{5-digit}) is truly permanent and never changes
- Schools have access to CR80-compatible card printers or printing services
- Sticker paper/labels are readily available and affordable
- 4 sticker slots are sufficient (covers 4 years of enrollment)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Schools can't print CR80 cards | Low | Medium | Template also works with standard paper; optional PVC |
| Sticker adhesion issues | Low | Low | Recommend specific label paper in documentation |
| Old invalidated QR scanned | Low | Low | Scanner rejects with REJECTED_QR_INVALIDATED status |

---

## Technical Considerations

### Architecture Impact
- Modified `QrCode` entity: add `InvalidatedAt` (DateTime?), `ReplacedByQrCodeId` (Guid?)
- Modified `QrCodeService`: new `InvalidateAsync()` method, regeneration keeps old record
- Redesigned `PrintQrCode.cshtml`: CR80 layout, no S.Y.
- New `PrintEnrollmentSticker.cshtml(.cs)`: sticker print page
- Existing `ScansApiController` already rejects `IsValid = false` QR codes

### Integration Points
- `QrCodeService` — invalidation and regeneration flow
- `PrintQrCode` page — card template redesign
- `StudentDetails` page — regeneration confirmation dialog
- EP0010 (Programs) — sticker includes Program field

### Key Files to Modify
- **New:** `src/SmartLog.Web/Pages/Admin/PrintEnrollmentSticker.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Data/Entities/QrCode.cs` (InvalidatedAt, ReplacedByQrCodeId)
- **Modify:** `src/SmartLog.Web/Services/QrCodeService.cs` (InvalidateAsync, regeneration flow)
- **Modify:** `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml(.cs)` (CR80 template, remove S.Y.)
- **Modify:** `src/SmartLog.Web/Pages/Admin/StudentDetails.cshtml.cs` (regeneration flow)
- **Migration:** Add InvalidatedAt, ReplacedByQrCodeId columns to QrCodes

---

## Sizing

**Story Points:** TBD (estimated 4-6 stories)
**Estimated Story Count:** 4-6

**Complexity Factors:**
- CSS/HTML card template at exact physical dimensions
- Sticker print layout with batch filtering
- QR invalidation chain (old → new linking)
- Regeneration confirmation UX flow

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0077](../stories/US0077-cr80-card-template.md) | CR80 Card Template Redesign | 5 | Done (superseded by US0112) |
| [US0078](../stories/US0078-enrollment-sticker-print.md) | Enrollment Sticker Print Page | 3 | Done (removed by US0110) |
| [US0079](../stories/US0079-qr-invalidation-audit.md) | QR Invalidation & Audit Trail | 3 | Done |
| [US0080](../stories/US0080-qr-regeneration-confirmation.md) | QR Regeneration with Confirmation Dialog | 3 | Done |
| [US0081](../stories/US0081-invalidate-without-regeneration.md) | Invalidate QR Without Regeneration | 2 | Done |
| [US0110](../stories/US0110-remove-enrollment-sticker.md) | Remove Enrollment Sticker Feature | 1 | Done |
| [US0111](../stories/US0111-school-branding-settings.md) | School Branding Settings (Logo, Name, Return Address) | 3 | Draft |
| [US0112](../stories/US0112-id-card-landscape-redesign.md) | ID Card Landscape Redesign (supersedes US0077) | 5 | Draft |
| [US0113](../stories/US0113-bulk-print-id-cards-per-section.md) | Bulk Print ID Cards per Section (supersedes US0022) | 5 | Draft |

**Total:** 30 story points across 9 stories (3 in Draft for V2.1 wave)

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0013`

---

## Open Questions

- [x] Is StudentId permanent? — **Decision: Yes, {SchoolCode}-{Year}-{5-digit}, doesn't change on re-enrollment**
- [x] Card size? — **Decision: CR80 (85.6mm x 53.98mm), same as driver's license**
- [x] QR regeneration policy? — **Decision: Keep old (mark invalid), create new, audit chain**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2 feature brainstorm |
| 2026-04-27 | Claude | Re-opened — landscape single-sided redesign (US0112 supersedes US0077), branding settings (US0111), bulk print per section (US0113 supersedes US0022). Sticker scope already removed by US0110. |
