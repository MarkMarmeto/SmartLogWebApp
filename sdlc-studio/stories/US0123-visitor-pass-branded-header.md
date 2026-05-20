# US0123: Visitor Pass — Branded Header (Match Student ID Style)

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** AI Assistant
> **Reviewer:** Mark Daniel Marmeto
> **Created:** 2026-05-20

## User Story

**As an** admin printing visitor passes
**I want** the printed card to carry the school's branding header (logo + name) like the student ID does, plus an orange "VISITOR PASS" accent band that makes it instantly distinguishable from a student card
**So that** the visitor pass looks like part of the school's card family, and a guard can tell at arm's length whether they're looking at a student or a visitor

## Context

### Background

US0122 shipped the single-pass print page on CR100 portrait stock with QR + code label only. The card is functional but visually plain — no branding, no school identity, and no obvious distinction from a student ID at a glance.

Student ID cards (`_StudentIdCard.cshtml`) already establish a card-design language:

- **7mm teal-gradient header** (`linear-gradient(135deg, #2C5F5D 0%, #3d8a87 100%)`) with `print-color-adjust: exact`
- School logo + `System.SchoolName` + optional `Branding:SchoolAddress` from AppSettings
- White text, Segoe UI font stack
- SVG placeholder when no logo is configured

This story adapts the same header pattern to the CR100 portrait visitor pass, plus adds a visitor-specific accent band so guards can distinguish at distance.

### Design intent

```
┌──────────────────────────────────────┐  ← top edge of CR100 portrait
│ [logo]  SmartLog School              │  ← 10mm teal-gradient header
│         School address (optional)    │
├──────────────────────────────────────┤
│         ── VISITOR PASS ──           │  ← 6mm orange accent band (#E07B39)
├──────────────────────────────────────┤
│                                       │
│         ┌──────────────┐              │
│         │   QR 50mm    │              │  ← unchanged from US0122
│         └──────────────┘              │
│                                       │
│            VISITOR-001                │  ← unchanged from US0122
│                                       │
│         Return to Reception          │  ← 5mm hardcoded footer
└──────────────────────────────────────┘  ← bottom edge
```

Vertical layout math (98.6mm card height):
- Header band: `0–10mm`
- Orange band: `10–16mm`
- QR top: `~20mm` (4mm breathing room below the orange band)
- QR bottom: `~70mm`
- Code label: `~74mm`
- Footer: `~93–98mm`

QR center sits at ~45mm — slightly above the card centerline (49.3mm), preserving the "QR above center" requirement from US0122 AC3.

### Decisions confirmed with reviewer

1. **Accent band color:** orange `#E07B39` (for at-a-distance distinction from student-ID teal).
2. **School address:** controlled by the same `Branding:SchoolAddress` AppSettings toggle as the student ID — shown when set, hidden when null/empty.
3. **Footer text:** hardcoded "Return to Reception" — no new AppSettings key.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0122 | Predecessor | Page route, dimensions, "no auto-fire print", grep-clean | All unchanged; only the card's *internal* layout changes |
| Student ID design (`_StudentIdCard.cshtml`) | Pattern | Teal gradient header, logo + name + optional address, `print-color-adjust: exact` | Reuse exactly so the cards feel like a family |
| EP0012 | Behavior | No PII on visitor passes | Header may include school name/logo only — no visitor identity beyond `VISITOR-NNN` |
| Project convention | Code health | AppSettings reads use `IAppSettingsService.GetAsync` | Page model injects the service; no direct DbContext |

---

## Acceptance Criteria

### AC1: Branded Header Band
- **Given** I open `/Admin/PrintVisitorPass/{id}`
- **Then** the top of the card has a 10mm header band with the teal gradient `linear-gradient(135deg, #2C5F5D 0%, #3d8a87 100%)`
- **And** the gradient renders in both screen and print (uses `-webkit-print-color-adjust: exact` and `print-color-adjust: exact`)
- **And** the header contains, left-aligned: the school logo (or the SVG placeholder if `Branding:SchoolLogoPath` is null); right of the logo, vertically centred: the school name from `System.SchoolName` (or default `"SmartLog School"`), and below it the optional `Branding:SchoolAddress` if set

### AC2: Visitor Accent Band
- **Given** the print page is rendered
- **Then** directly below the teal header sits a 6mm orange band with background `#E07B39`
- **And** the band contains the text `VISITOR PASS`, centred, white, bold, ~9pt, letter-spaced for emphasis
- **And** the orange prints (uses `print-color-adjust: exact`)

### AC3: QR + Code Layout Preserved
- **Given** the header + accent band consume the top 16mm
- **Then** the QR image (50mm × 50mm) sits centred horizontally with ~4mm vertical breathing room below the accent band
- **And** the `VISITOR-NNN` label remains directly below the QR, centred, bold, 14pt (unchanged from US0122)
- **And** the QR's vertical centre is still **above the card's vertical centre** (preserving US0122 AC3 "QR above center")

### AC4: Footer Band
- **Given** the print page is rendered
- **Then** the bottom of the card has a 5mm footer band with the hardcoded text `Return to Reception`
- **And** the footer text is centred, small (~5pt), neutral grey (`#888`), so it does not visually compete with the orange accent

### AC5: Branding Reads From AppSettings Through Service Layer
- **Given** the page model loads
- **Then** `System.SchoolName`, `Branding:SchoolAddress`, `Branding:SchoolLogoPath` are read via `IAppSettingsService.GetAsync` (same keys + same fallback behavior as `PrintQrCode.cshtml.cs`)
- **And** no direct DbContext access happens in the page model
- **And** if `System.SchoolName` is null/empty, the default `"SmartLog School"` is used (matches student-ID convention)

### AC6: Logo Fallback
- **Given** `Branding:SchoolLogoPath` is null or empty
- **Then** the same SVG placeholder used by the student ID renders inline (corner-bracket + check-mark icon, white on teal)
- **And** the logo never breaks the header layout (`max-width` / `object-fit: contain` constraints copied from student ID)

### AC7: On-Screen Preview Reflects Print
- **Given** I open the page in a browser
- **Then** the on-screen preview shows the header, orange band, QR, label, and footer at proportions that visually match the printed output (using the same 3.779 px/mm screen-conversion the rest of the card already uses)

### AC8: Card Still Fits CR100 Without Overflow
- **Given** the full card content (header 10mm + accent 6mm + QR area + label + footer 5mm)
- **Then** the total stays within the 98.6mm card height with no clipping or scrollbars in print mode
- **And** in screen mode, no inner element forces the card-wrap container to scroll

### AC9: No Regression on US0122 Behaviors
- **Given** the design changes made by this story
- **Then** none of the following from US0122 regress:
  - Per-row Print button on `/Admin/VisitorPasses` (unchanged)
  - 404 on unknown / malformed GUID (unchanged)
  - No auto-fire `window.print()` on load (unchanged)
  - `[Authorize(Policy = "RequireAdmin")]` (unchanged)
  - Grep-clean — no references to `/Admin/PrintVisitorPasses` reappear

### AC10: QR-Missing Placeholder Still Works
- **Given** `QrImageBase64` is null for some pass
- **Then** the placeholder ("QR not available — contact support") still renders in the QR slot
- **And** the rest of the card (header, accent, label, footer) renders normally around it

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| `System.SchoolName` is null or whitespace | Use `"SmartLog School"` default (matches student ID) |
| `Branding:SchoolAddress` is set but very long | Truncate with ellipsis (`text-overflow: ellipsis; white-space: nowrap;` — same as student ID) |
| `Branding:SchoolLogoPath` points to a 404 image | Browser renders broken-image glyph in 5mm slot. Acceptable — admin notices and fixes the setting. Same behavior as student ID; no defensive fallback needed here. |
| Logo is a wide rectangle (e.g. 4:1 banner) | `max-width: 12mm` + `object-fit: contain` constrains it (mirrors student ID `.id-card__logo`) |
| Browser ignores `print-color-adjust: exact` | Gradient + orange band may print as outlined boxes instead of solid colors. Out of our control — modern Chrome / Edge / Firefox respect the property. |
| Pass code grows to `VISITOR-1000` (4 digits) | Label sizing still fits (≥12-char headroom from US0122 AC5) |

---

## Test Scenarios

- [ ] Header renders with teal gradient on screen and in print preview
- [ ] Orange `#E07B39` accent band sits directly below the header
- [ ] "VISITOR PASS" text is centred, white, bold inside the orange band
- [ ] School name from `System.SchoolName` renders in the header
- [ ] School address renders when `Branding:SchoolAddress` is set; hidden when null
- [ ] SVG placeholder appears when `Branding:SchoolLogoPath` is null
- [ ] QR + label remain in the same relative positions as US0122 (regression test — visual)
- [ ] "Return to Reception" footer appears centred in 5pt grey at the bottom
- [ ] Total card content fits 98.6mm height without clipping
- [ ] On-screen preview proportions match print preview (visual)
- [ ] Existing US0122 PageModel unit tests still pass
- [ ] Grep finds no references to `/Admin/PrintVisitorPasses` (no regression on US0122 cleanup)

---

## Out of Scope (Deferred)

- **Configurable accent color.** Orange `#E07B39` is hardcoded; no AppSettings key. If a school wants to rebrand the visitor-pass accent, a follow-up story can lift it into `Branding:VisitorPassAccentColor`.
- **Configurable footer text.** "Return to Reception" is hardcoded per reviewer decision. If multilingual or per-tenant variants emerge, a future story adds `Branding:VisitorReturnText`.
- **Photo / visitor name on the card.** EP0012 explicitly excludes PII on visitor passes — does not change here.
- **Bulk-print page revival.** US0122 deleted it; this story does not bring it back.
- **Updating `PrintQrCode.cshtml` or `_StudentIdCard.cshtml`** to share a partial. Refactoring into a shared partial is a *possible* future cleanup; not worth the indirection cost right now since the two card layouts are visually similar but structurally different (CR80 landscape vs CR100 portrait).

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0122](US0122-visitor-pass-single-print-cr100.md) | Predecessor | The print page exists, with route, auth, page model, and tests | Done (committed; pending push) |
| Student ID branding (US0111-ish school branding settings) | Pattern reference | `Branding:SchoolLogoPath`, `Branding:SchoolAddress`, `System.SchoolName` AppSettings keys + UI to set them | Done |

### Technical Dependencies

- `IAppSettingsService` (existing).
- No new packages, no migration, no DI changes, no new service methods.

---

## Estimation

**Story Points:** 1
**Complexity:** Low

Rough split:
- Page model: read branding from AppSettings (~10 LOC): 0.25 pt
- CSS + Razor markup for header + accent + footer: 0.5 pt
- Test sanity (existing 2 tests still pass; no new tests strictly required — branding is mostly visual): 0.25 pt

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-20 | Claude (Opus 4.7) | Initial draft |
