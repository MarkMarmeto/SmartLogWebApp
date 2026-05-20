# US0122: Visitor Pass — Single-Pass Print on CR100 (Portrait)

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** AI Assistant
> **Reviewer:** Mark Daniel Marmeto
> **Created:** 2026-05-20

## User Story

**As an** admin issuing visitor passes
**I want** to print a single visitor pass at a time on a CR100 portrait card with the QR above center and the pass code beneath
**So that** I can re-issue lost passes on demand without printing the whole batch, and the printed card matches the physical CR100 lanyard cards we stock

## Context

### Background

The current admin workflow exposes one print entry point: `/Admin/VisitorPasses` → "Print Cards" → bulk page `/Admin/PrintVisitorPasses` rendering every active pass in a 3-column CR80 landscape grid. This was useful for the initial pool issuance but is now a regression for the common case:

- Pool size is set once (default 20 passes per EP0012). After initial issuance, the only print events are **single-card replacements** (lost / damaged / re-issued passes).
- Bulk printing the whole pool to reprint one card wastes 19 cards of stock per incident.
- The physical card stock standardised on **CR100 portrait (2.63" × 3.88")**, not CR80 landscape (3.37" × 2.13"). The current template's CR80 layout overflows or under-fills the new stock.

### Goal

1. Add a **per-row "Print" button** on `/Admin/VisitorPasses` (Actions column) that opens a per-pass print page.
2. Replace the bulk-print page with a **single-pass print page** at `/Admin/PrintVisitorPass/{id}` (singular route).
3. Redesign the printed card for **CR100 portrait** (2.63" × 3.88" = 66.8mm × 98.6mm):
   - QR code centred horizontally, positioned **above the vertical centre** of the card.
   - **PassCode label** (e.g. `VISITOR-001`) at the bottom, centred.
   - White card background, 1pt black border for cut alignment.
4. Remove the bulk-print page (`PrintVisitorPasses.cshtml` + `.cshtml.cs`) and the "Print Cards" nav button — cleanly, no fallback shim. Per project convention (CLAUDE.md style), avoid backwards-compat dead routes.

### Why "VISITOR-001" exactly (not "VISITOR 01")

Confirmed with reviewer: keep the database `Code` format verbatim (`VISITOR-001`) on the printed card. Avoids two-source-of-truth confusion between what the admin reads on screen and what the guard reads on the lanyard.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0012 | Behavior | Visitor passes are reusable; no PII printed | Card shows only QR + code; no name, no date, no school branding beyond optional logo |
| Project convention | Code health | No backwards-compat shims for removed routes (`feedback_commit_workflow`) | Bulk page is deleted, not redirected |
| Existing pattern | UI consistency | `/Admin/PrintQrCode/{id}` is the established singular-route template | New page mirrors its structure (controls bar + scaled on-screen preview + `@media print` block) |
| CR100 spec | Hardware | 2.63" × 3.88" = 66.8mm × 98.6mm portrait | CSS uses `mm` units in `@page` and card sizing |

---

## Acceptance Criteria

### AC1: Per-Row Print Button on Visitor Passes List
- **Given** I am on `/Admin/VisitorPasses`
- **Then** each row's Actions column has a new "Print" button (alongside the existing Activate/Deactivate button)
- **And** the button is enabled regardless of `IsActive` / `CurrentStatus` (admin may want to reprint a lost-but-revoked card for audit, then deactivate)
- **And** clicking it opens `/Admin/PrintVisitorPass/{id}` in a new tab (target=_blank), matching the per-section ID-card print pattern in `Sections.cshtml`

### AC2: Single-Pass Print Page
- **Given** I navigate to `/Admin/PrintVisitorPass/{passId}`
- **Then** the page shows exactly one visitor pass card (the requested one)
- **And** if the GUID does not match any pass, the page returns `404 Not Found`
- **And** the page uses `Layout = null` (matches `PrintQrCode.cshtml` convention)
- **And** a "Print" button (`window.print()`) and a "Back" link to `/Admin/VisitorPasses` are shown in the screen-only controls bar (hidden via `@media print`)

### AC3: CR100 Portrait Card Layout (Print Mode)
- **Given** the print page is rendered
- **When** the user prints (or print-previews)
- **Then** the `@page` size is `66.8mm 98.6mm` (CR100 portrait) with zero margins
- **And** the card itself is exactly `66.8mm × 98.6mm` with a 1pt solid black border
- **And** the QR image fills roughly the upper half: centred horizontally, top edge ~6mm from the card top, QR size ~50mm × 50mm
- **And** the `PassCode` text (`VISITOR-001` etc.) renders below the QR, centred, bold, ~14pt, with at least 4mm vertical breathing room from the QR's bottom edge
- **And** the QR + label block is **vertically biased upward** — the QR sits *above the card's vertical centre*, with empty space toward the bottom (per spec)

### AC4: On-Screen Preview Mirrors Print
- **Given** I open the print page in a browser (no print dialog)
- **Then** the page shows a scaled preview of the CR100 card that visually matches the printed output (proportions, QR placement, label position)
- **And** the preview is centred on a light grey body background (matching `PrintQrCode.cshtml` aesthetic)
- **And** the preview is large enough to verify legibility (~330px wide on screen)

### AC5: PassCode Label Uses Stored Value Verbatim
- **Given** the database stores `VisitorPass.Code` as `VISITOR-001`, `VISITOR-002`, … `VISITOR-020`, …
- **Then** the printed label renders that exact string with no reformatting (no dash strip, no zero strip)
- **And** the label uses a monospace or geometric sans-serif (e.g. `Inter`, `Segoe UI`, `Arial`) sized to fit at minimum 12 characters comfortably (room for `VISITOR-1000` if pool ever grows beyond 999)

### AC6: Bulk-Print Page Removed Cleanly
- **Given** the codebase after this story
- **Then** `src/SmartLog.Web/Pages/Admin/PrintVisitorPasses.cshtml` is deleted
- **And** `src/SmartLog.Web/Pages/Admin/PrintVisitorPasses.cshtml.cs` is deleted
- **And** the "Print Cards" navigation button on `VisitorPasses.cshtml` (currently lines 14–15) is removed
- **And** no other page references `/Admin/PrintVisitorPasses` (grep-verified during plan execution)
- **And** there is no redirect, route placeholder, or comment pointer left behind

### AC7: QR Image Source
- **Given** the pass has a non-null `QrImageBase64` (PNG data URL)
- **Then** the card embeds the QR via `<img src="data:image/png;base64,@pass.QrImageBase64">`
- **And** if `QrImageBase64` is null (legacy edge case), the page renders a visible placeholder ("QR not available — contact support") rather than a broken image
- **And** the missing "Regenerate QR" admin affordance is logged as a discovered follow-up story (see "Discovered Follow-Ups" below) — out of scope for this story

### AC8: Print-Friendly Defaults
- **Given** the print dialog opens
- **Then** the browser-default header/footer (URL, timestamp, page number) should not appear on the printed card
- **And** the page has `@page { margin: 0; }` so no implicit print margins reduce the card area
- **Note:** Browser print-header suppression is best-effort — Chrome and Edge respect `@page` margins; Firefox honours them when "Print backgrounds" is enabled. We do not attempt to suppress via JS hacks.

### AC9: Print Button Does Not Auto-Fire
- **Given** the print page loads in the browser
- **Then** the print dialog **does not** open automatically (no `window.print()` on load, no `<body onload>` trigger)
- **And** the user must click the in-page Print button to open the dialog
- **Rationale:** preview-first lets the admin catch a wrong-pass click before paper is wasted; matches existing `PrintQrCode.cshtml` UX. This AC exists explicitly so a future "helpful" refactor doesn't quietly add auto-print.

### AC10: Access Control
- **Given** the new page lives under `/Admin/...`
- **Then** access requires the same policy as the existing `/Admin/VisitorPasses` page (verified — currently `CanManageStudents` per `_ViewImports` / page-level attribute; mirror exactly)
- **And** unauthenticated requests are redirected to login

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Invalid GUID in URL (`/Admin/PrintVisitorPass/not-a-guid`) | Route constraint `{id:guid}` rejects → 404 |
| GUID well-formed but no matching pass | Page returns `NotFound()` from `OnGetAsync` |
| Pass exists but `QrImageBase64` is null | Render placeholder text + admin guidance (AC7) |
| Pass is deactivated | Still prints (AC1 — admin discretion) |
| Browser ignores `@page` size | Card still prints at the specified mm dimensions because the inner `.card` div sizes itself; some white border around the card on a non-CR100 sheet is acceptable |
| Two admins click Print on the same pass simultaneously | No conflict — print page is read-only, no state mutation |
| User prints a stale page after pass deactivation | Acceptable — printed cards are physical artefacts, server state doesn't invalidate them |
| Pool grows past `VISITOR-999` → `VISITOR-1000` | Label still fits (AC5 sizing guidance) |

---

## Test Scenarios

- [ ] `/Admin/VisitorPasses` shows a "Print" button on each row
- [ ] "Print" button opens `/Admin/PrintVisitorPass/{id}` in a new tab
- [ ] Print page renders the correct pass (matches URL `id`)
- [ ] Print page returns 404 for unknown GUIDs
- [ ] Print page returns 404 for malformed (non-GUID) `id`
- [ ] Print page is access-controlled with the same policy as the list page
- [ ] On-screen preview shows QR above centre, label below
- [ ] `@page` rule sets size to `66.8mm 98.6mm` with zero margins (verified by inspecting computed styles or visual print-preview)
- [ ] Card border is 1pt solid black in print mode
- [ ] Label text matches the stored `Code` field verbatim
- [ ] Missing `QrImageBase64` shows the placeholder ("QR not available — contact support"), not a broken image icon
- [ ] Page does not auto-fire `window.print()` on load (manual: open page, confirm no print dialog)
- [ ] `PrintVisitorPasses.cshtml` / `.cshtml.cs` are deleted (verified by file existence test or grep)
- [ ] `/Admin/PrintVisitorPasses` route returns 404 (no shim, no redirect)
- [ ] "Print Cards" nav button on the list page is gone
- [ ] Grep finds no remaining references to `PrintVisitorPasses` in `src/SmartLog.Web/`

---

## Discovered Follow-Ups

- **"Regenerate QR" admin affordance for visitor passes.** Today there is no way to re-trigger `QrImageBase64` generation from the admin UI; passes created before QR generation existed (or with a corrupted blob) cannot be repaired without a DB poke. This story's missing-QR placeholder copy ("QR not available — contact support") points to this gap. A follow-up story should add a Regenerate button on `/Admin/VisitorPasses` rows.

---

## Out of Scope (Deferred)

- **Bulk-printing CR100 cards** (e.g. an admin onboarding a fresh batch of 20). If this becomes a real workflow regression, a follow-up story can add an *opt-in* "Print all" page rendering N CR100 cards in sequence — but with explicit per-card page breaks, not the current grid layout.
- **Card front/back two-sided printing.** Single-side only; back is blank per current design.
- **School logo / branding** on the card. EP0012 explicitly excludes branding on visitor cards.
- **Card stock other than CR100.** If we ever stock CR80 again, a new story handles the size toggle (likely an `AppSettings` key).
- **Print-from-CLI / headless export to PDF.** Browser-print is the only supported path.
- **Audit log entry for "printed pass X".** Not security-relevant; printing is a passive read.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| EP0012 stories (pool, generation, QR HMAC) | Predecessor | `VisitorPass.Code`, `QrImageBase64`, `IVisitorPassService.GetByIdAsync` (or equivalent) | Done |
| US0029 / US0089-ish | Pattern reference | `PrintQrCode.cshtml` per-student print page used as template | Done |

### Technical Dependencies

- `IVisitorPassService` — needs a "get one by id" method. Verify in plan; if not present, add a small accessor.
- Razor Pages route constraint `{id:guid}`.
- No new packages, no migration, no DI changes.

---

## Estimation

**Story Points:** 2
**Complexity:** Low

Rough split:
- New single-pass print page (Razor + CSS): 1 pt
- Per-row Print button + bulk-page deletion + cleanup: 0.5 pt
- Tests + verification (route, 404, placeholder, grep-clean): 0.5 pt

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-20 | Claude (Opus 4.7) | Initial draft |
