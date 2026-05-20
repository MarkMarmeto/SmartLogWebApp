# US0112: ID Card Landscape Redesign (CR80, Single-Sided)

> **Status:** Done
> **Plan:** [PL0035: ID Card Landscape Redesign](../plans/PL0035-id-card-landscape-redesign.md)
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-27
> **Supersedes:** [US0077: CR80 Card Template Redesign](US0077-cr80-card-template.md)

## User Story

**As a** Admin Amy (Administrator)
**I want** to print a student ID card in CR80 landscape with a left-side identity panel and a right-side QR
**So that** the QR is large enough to scan reliably from gate cameras while the front shows clear, permanent identity info

## Context

### Background

US0077 shipped a CR80 card template, but the implementation is **portrait-stacked** (header → photo + name → QR underneath) which exceeds the card height once printed and is awkward to scan because the QR shares vertical space with the photo.

Stakeholder direction (2026-04-27):

- **Single-sided PVC** printer — no card back available.
- **Landscape orientation** — 85.6mm × 54mm.
- **Top-center header band** with school logo + school name (logo configured via US0111).
- **Left half**: photo, name, LRN, StudentId.
- **Right half**: large QR code (sized as big as the body height allows).
- **Bottom footer band** with single-line return-address text from `AppSettings.Branding:ReturnAddressText`.
- **Permanent identity only** — no Grade, Section, Academic Year, or Program (the card must remain valid across a student's entire enrollment per the EP0013 mandate).

This story redesigns `Pages/Admin/PrintQrCode.cshtml` to match.

---

## Acceptance Criteria

### AC1: Card Dimensions & Orientation
- **Given** I print a student ID card from `/Admin/PrintQrCode/{id}`
- **Then** the card is rendered at CR80 landscape: **85.6mm wide × 54mm tall**
- **And** the print CSS clips at exactly those dimensions with no overflow

### AC2: Header Band — School Branding
- **Given** the rendered card
- **Then** the top of the card has a header band of approximately **9mm height**
- **And** the header contains the **uploaded school logo** (from `AppSettings.Branding:SchoolLogoPath`, US0111) and the **school name** (from `AppSettings.System.SchoolName`)
- **And** logo + name are visually centered within the header band
- **And** if no logo has been uploaded, the placeholder SmartLog SVG is used

### AC3: Body — Two-Column Split
- **Given** the body region between the header and the footer (~42mm tall × 85.6mm wide)
- **Then** it is split into two columns:
  - **Left column** (~45mm wide): student photo + identity text
  - **Right column** (~40mm wide): QR code
- **And** the columns are visually separated by a subtle vertical divider or whitespace

### AC4: Left Column — Identity Panel
- **Given** the left column for a student "Maria Reyes Santos", StudentId "2026-07-0001", LRN "123456789012"
- **Then** the left column shows in stack order:
  - Student photo: ~25mm × 30mm with rounded corners (placeholder initials if no photo)
  - Full name: "Maria Reyes Santos" (truncate at 28 characters with ellipsis)
  - LRN: "LRN: 123456789012"
  - Student ID: "ID: 2026-07-0001"
- **And** font sizing fits without overflow (name larger than detail rows)

### AC5: Right Column — QR Code
- **Given** the right column (42mm tall body region)
- **Then** the QR is rendered as a square sized to fill the column height with ~2mm padding (yielding ~36–38mm square QR — still well above the 30mm minimum for reliable gate-camera scanning)
- **And** the QR image is `data:image/png;base64,@Model.QrCode.QrImageBase64`
- **And** there is a small "SCAN FOR ATTENDANCE" caption directly under the QR (≤ 7pt)

### AC6: No Year-Specific Data
- **Given** the rendered card
- **Then** the card displays **no** Grade, Section, Academic Year, Program, or any other field whose value changes across enrollments

### AC7: Print Fidelity
- **Given** I click the Print button
- **Then** the browser print dialog renders the card at exact CR80 dimensions
- **And** colors, gradients, and the header band print correctly (`print-color-adjust: exact`)
- **And** screen-only controls (Print / Close buttons) do not appear in the printed output

### AC8: Photo Fallback
- **Given** the student has no `ProfilePicturePath`
- **Then** the photo slot shows initials (first letter of FirstName + first letter of LastName) on a tinted background
- **And** the layout dimensions remain identical

### AC9: Long Names
- **Given** a student name longer than 28 characters
- **Then** the name is truncated with "…" and the layout does not break

### AC10: Authorization Unchanged
- **Given** the page is `[Authorize(Policy = "CanViewStudents")]` today
- **Then** the policy is preserved after the redesign

### AC11: Footer Band — Return Address
- **Given** `AppSettings.Branding:ReturnAddressText` is "Marmeto National HS · Sample St., Quezon City · (02) 1234-5678"
- **Then** the bottom of the card has a footer band of approximately **3mm height**
- **And** the footer shows the return-address text on a single line, centered, prefixed with the label "If found:" (e.g. `If found: Marmeto National HS · Sample St., Quezon City · (02) 1234-5678`)
- **And** the text is sized to fit (≤ 6.5pt) and is HTML-escaped
- **And** if the text overflows the card width, it is truncated with "…" rather than wrapping
- **And** if `Branding:ReturnAddressText` is empty/null, the footer band is omitted entirely and the body region expands to ~45mm to use the full card height

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student has no valid `QrCode` | Page returns 400 with "No valid QR code available" (existing behaviour preserved) |
| Logo file path in `AppSettings` but file missing on disk | Fall back to placeholder SVG; do not break layout |
| LRN is null | Render "LRN: —" |
| Very wide logo (e.g. 3:1 aspect) | Logo is `object-fit: contain` and capped to header height (no horizontal overflow into header text) |
| Browser scaling at print | CSS uses absolute mm units; no `vh`/`vw`/`%` for card sizing |
| Return-address text contains HTML/JS | Escaped on render — never injected as raw markup |
| Return-address text very long (~120 chars) | Truncated with "…" on a single line; no wrapping |
| Return-address empty | Footer band omitted; body uses full ~45mm |

---

## Test Scenarios

- [ ] Card renders at exactly 85.6mm × 54mm (visual snapshot or measured screenshot)
- [ ] Header band shows uploaded logo when configured
- [ ] Header band falls back to SmartLog placeholder when no logo
- [ ] Left column shows photo, name, LRN, StudentId in correct order
- [ ] Right column shows QR ≥ 38mm square
- [ ] No Grade / Section / AY / Program rendered anywhere
- [ ] Long name truncated cleanly
- [ ] Missing photo shows initials placeholder
- [ ] Print dialog hides screen controls
- [ ] Existing `[Authorize(Policy="CanViewStudents")]` policy still applies
- [ ] No regression on QR HMAC payload (untouched — only display changes)
- [ ] Footer band renders return-address text when configured
- [ ] Footer band omitted when return-address empty (body expands)
- [ ] HTML in return-address is escaped, not rendered
- [ ] Long return-address truncates with ellipsis

---

## Technical Notes

### Files Modified
- `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml` — full markup + CSS rewrite
- `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml.cs` — load `Branding:SchoolLogoPath` and `Branding:ReturnAddressText` from `AppSettings` (next to existing `System.SchoolName` load)

### Layout Spec (Reference)

```
┌──────────────────────────────────────────────────────┐ ← 85.6mm wide
│  [LOGO]   MARMETO NATIONAL HIGH SCHOOL               │ ← ~9mm header
├──────────────────────────────────────────────────────┤
│  ┌────────┐                                          │
│  │ PHOTO  │  Maria Reyes Santos          ┌────────┐  │
│  │ 25×30  │                              │   QR   │  │ ← ~42mm body
│  │   mm   │  LRN: 123456789012           │ ~36mm² │  │
│  └────────┘  ID:  2026-07-0001           └────────┘  │
│                                          SCAN FOR    │
├──────────────────────────────────────────────────────┤
│  If found: Marmeto National HS · Sample St. · (02)…  │ ← ~3mm footer
└──────────────────────────────────────────────────────┘
   ↑ 45mm left column                 ↑ 40mm right column
   Total height: 9 + 42 + 3 = 54mm (CR80)
```

### Why These Dimensions
- 85.6 × 54 = ISO/IEC 7810 ID-1 (CR80) standard, matches commercial PVC card stock and lanyard holders.
- 9mm header gives the school name room without crowding; logo locked to header height.
- 3mm footer fits a single line of ~6.5pt text — visible to a finder, not visually heavy.
- Right column 40mm wide allows a ~36mm-square QR after 2mm right-edge padding and accounting for the caption — still well above the 30mm+ minimum for reliable scan at 30–50cm camera distance.
- When the return-address is empty, the footer collapses to 0mm and the body expands to ~45mm, giving a slightly larger QR (~38mm).

### Backward Compatibility
- Route `/Admin/PrintQrCode/{id}` unchanged — same query handler, same model.
- Existing single-student link from StudentDetails continues to work.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0111](US0111-school-branding-settings.md) | Functional | School logo upload — provides `Branding:SchoolLogoPath` | Draft |
| [US0019](US0019-generate-qr.md) | Functional | QR codes exist | Done |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium — pure presentation rewrite; no data model or auth changes.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft — supersedes US0077 with landscape single-sided design |
