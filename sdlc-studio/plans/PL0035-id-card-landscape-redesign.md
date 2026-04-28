# PL0035: ID Card Landscape Redesign (CR80, Single-Sided)

> **Status:** Complete
> **Story:** [US0112: ID Card Landscape Redesign](../stories/US0112-id-card-landscape-redesign.md)
> **Epic:** EP0013: QR Permanence & Card Redesign
> **Created:** 2026-04-27
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
> **Drafted by:** Claude (Opus 4.7)

## Overview

Replace the existing portrait-stacked card layout in `Pages/Admin/PrintQrCode.cshtml` with a CR80 landscape layout: 9mm header (school logo + name), 42mm body (left identity panel / right QR), 3mm footer (return-address text). The page handler is updated to load the new branding settings from US0111. The card markup is extracted into a Razor partial `_StudentIdCard.cshtml` so US0113's bulk-print page can reuse it without duplication.

This is a presentation-only change. No data model, no auth, no service layer changes.

---

## Acceptance Criteria Summary

| AC | Name | Implementation Phase |
|----|------|----------------------|
| AC1 | Card 85.6mm × 54mm, exact CR80 | Phase 2 (CSS) |
| AC2 | Header band ~9mm with logo + school name | Phase 2 |
| AC3 | Body split 45mm left / 40mm right | Phase 2 |
| AC4 | Left column: photo, name, LRN, ID | Phase 2 |
| AC5 | Right column: QR sized to fill | Phase 2 |
| AC6 | No Grade/Section/AY/Program | Phase 2 (deletion) |
| AC7 | Print fidelity (`print-color-adjust: exact`) | Phase 2 |
| AC8 | Photo fallback (initials) | Phase 2 |
| AC9 | Long-name truncation | Phase 2 (CSS `text-overflow`) |
| AC10 | Authorization unchanged (`CanViewStudents`) | Phase 1 (no change) |
| AC11 | Footer band with return-address; collapses when empty | Phase 1, 2 |

---

## Technical Context

### Current State
- `Pages/Admin/PrintQrCode.cshtml` — portrait-stacked: header → photo+text row → centered QR (200px) → footer bar. Layout exceeds CR80 height in print and was never visually a card.
- `PrintQrCode.cshtml.cs` — loads `Student`, active `QrCode`, `System.SchoolName` from `IAppSettingsService`. Inline SVG placeholder logo.

### After This Plan
- Same handler but also loads `Branding:SchoolLogoPath` and `Branding:ReturnAddressText`.
- Markup moved into `_StudentIdCard.cshtml` partial taking a `StudentIdCardViewModel`.
- New CSS — flex layout with absolute mm sizing; print + screen behave identically.

### Layout Math (Reference)

```
Width = 85.6mm (fixed)
Header: 9mm   = top branding band
Footer: 3mm   = bottom return-address band (or 0mm when address empty)
Body:   42mm  = 54 - 9 - 3   (or 45mm when footer collapsed)

Body horizontal split:
  Left identity column:  45mm  (photo 25mm wide × 30mm tall + text)
  Right QR column:       40mm  (QR ~36mm square + 2mm padding + caption)
```

### View Model

```csharp
public class StudentIdCardViewModel
{
    public Student Student { get; init; } = null!;
    public QrCode QrCode { get; init; } = null!;
    public string SchoolName { get; init; } = "SmartLog School";
    public string? SchoolLogoPath { get; init; }       // null = use placeholder SVG
    public string? ReturnAddressText { get; init; }    // null/empty = collapse footer
}
```

---

## Recommended Approach

**Strategy:** Visual-First with Test-After
**Rationale:** This is a CSS-driven layout. Verify on screen with the browser ruler/devtools first, then add a smoke-only assertion that key elements render. Pixel-precision tests aren't tractable in xUnit; rely on manual screenshot review.

---

## Implementation Phases

### Phase 1: Handler Update + View Model

**Goal:** Extract branding values; pass everything via the view model.

- [ ] Add `src/SmartLog.Web/Models/StudentIdCardViewModel.cs` with the shape above.
- [ ] Update `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml.cs`:
  - Add fields: `string? SchoolLogoPath`, `string? ReturnAddressText`
  - In `OnGetAsync`, after loading student + QR:
    ```csharp
    SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
    SchoolLogoPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
    ReturnAddressText = await _appSettings.GetAsync("Branding:ReturnAddressText");
    ```
  - Add a computed property exposing the view model:
    ```csharp
    public StudentIdCardViewModel CardModel => new()
    {
        Student = Student,
        QrCode = QrCode,
        SchoolName = SchoolName,
        SchoolLogoPath = SchoolLogoPath,
        ReturnAddressText = ReturnAddressText,
    };
    ```
- [ ] Verify policy `[Authorize(Policy = "CanViewStudents")]` is preserved (AC10).

**Files:**
- `src/SmartLog.Web/Models/StudentIdCardViewModel.cs` (new)
- `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml.cs` (modify)

### Phase 2: Card Partial — Markup + CSS

**Goal:** Single source of truth for card layout, reused by single + bulk pages.

- [ ] Create `src/SmartLog.Web/Pages/Admin/_StudentIdCard.cshtml`:
  - `@model SmartLog.Web.Models.StudentIdCardViewModel`
  - Markup (single root `.id-card` div):
    ```razor
    <div class="id-card">
        <div class="id-card__header">
            @if (!string.IsNullOrEmpty(Model.SchoolLogoPath))
            {
                <img class="id-card__logo" src="@Model.SchoolLogoPath" alt="" />
            }
            else
            {
                @* Inline SmartLog placeholder SVG — copy from current PrintQrCode.cshtml *@
            }
            <span class="id-card__school">@Model.SchoolName</span>
        </div>

        <div class="id-card__body">
            <div class="id-card__identity">
                <div class="id-card__photo">
                    @if (!string.IsNullOrEmpty(Model.Student.ProfilePicturePath))
                    {
                        <img src="@Model.Student.ProfilePicturePath" alt="" />
                    }
                    else
                    {
                        <span class="id-card__photo-initials">@(Model.Student.FirstName[0])@(Model.Student.LastName[0])</span>
                    }
                </div>
                <div class="id-card__text">
                    <div class="id-card__name" title="@Model.Student.FullName">@Model.Student.FullName</div>
                    <div class="id-card__detail"><span>LRN</span> @(string.IsNullOrEmpty(Model.Student.LRN) ? "—" : Model.Student.LRN)</div>
                    <div class="id-card__detail"><span>ID</span> @Model.Student.StudentId</div>
                </div>
            </div>
            <div class="id-card__qr">
                <img src="data:image/png;base64,@Model.QrCode.QrImageBase64" alt="QR Code" />
                <div class="id-card__qr-caption">SCAN FOR ATTENDANCE</div>
            </div>
        </div>

        @if (!string.IsNullOrWhiteSpace(Model.ReturnAddressText))
        {
            <div class="id-card__footer">
                <span class="id-card__footer-label">If found:</span>
                <span class="id-card__footer-text">@Model.ReturnAddressText</span>
            </div>
        }
    </div>
    ```
  - Razor escapes `@Model.ReturnAddressText` automatically — satisfies AC11 escape requirement.

- [ ] Create the CSS — colocate in the partial via `<style>` (single-purpose component) OR move to `wwwroot/css/id-card.css`. **Decision: colocate** — keeps the partial self-contained and the CSS lives next to the markup it owns. Bulk page pulls in the partial → gets the styles for free.

  Key CSS rules:
  ```css
  .id-card {
      width: 85.6mm;
      height: 54mm;
      background: #fff;
      box-shadow: 0 2px 14px rgba(0,0,0,.18);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      font-family: 'Segoe UI', Arial, sans-serif;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
  }

  .id-card__header {
      height: 9mm;
      background: linear-gradient(135deg, #2C5F5D 0%, #3d8a87 100%);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;   /* AC2: centered */
      gap: 2mm;
      padding: 0 3mm;
  }
  .id-card__logo { height: 6mm; width: auto; object-fit: contain; max-width: 12mm; }
  .id-card__school { font-size: 9pt; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

  .id-card__body { flex: 1; display: flex; }   /* fills 42mm or 45mm */
  .id-card__identity { width: 45mm; display: flex; gap: 2mm; padding: 2mm; box-sizing: border-box; }
  .id-card__photo { width: 25mm; height: 30mm; flex-shrink: 0; border: 0.4mm solid #dce9e9; background: #e8f5f4; border-radius: 1mm; overflow: hidden; display: flex; align-items: center; justify-content: center; }
  .id-card__photo img { width: 100%; height: 100%; object-fit: cover; }
  .id-card__photo-initials { font-size: 14pt; font-weight: 700; color: #2C5F5D; }
  .id-card__text { min-width: 0; display: flex; flex-direction: column; justify-content: center; gap: 1mm; }
  .id-card__name { font-size: 10pt; font-weight: 700; line-height: 1.1; color: #1a1a1a; max-height: 11mm; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
  .id-card__detail { font-size: 7pt; color: #333; font-family: 'Courier New', monospace; }
  .id-card__detail span { color: #888; font-family: 'Segoe UI', sans-serif; font-weight: 600; margin-right: 1mm; }

  .id-card__qr { width: 40mm; padding: 1mm 2mm 1mm 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.5mm; }
  .id-card__qr img { width: 100%; max-width: 36mm; aspect-ratio: 1 / 1; object-fit: contain; }
  .id-card__qr-caption { font-size: 5.5pt; letter-spacing: 0.3mm; color: #888; font-weight: 600; }

  .id-card__footer { height: 3mm; background: #f5f5f5; display: flex; align-items: center; justify-content: center; gap: 1mm; padding: 0 2mm; overflow: hidden; }
  .id-card__footer-label { font-size: 5.5pt; font-weight: 700; color: #555; flex-shrink: 0; }
  .id-card__footer-text { font-size: 5.5pt; color: #555; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; min-width: 0; }
  ```

**Files:**
- `src/SmartLog.Web/Pages/Admin/_StudentIdCard.cshtml` (new — markup + colocated `<style>`)

### Phase 3: Update PrintQrCode Page

**Goal:** Single-card page renders the partial inside its existing print frame.

- [ ] Rewrite `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml`:
  - Keep: `Layout = null`, `<head>` with `<meta>`, screen controls (Print / Close buttons)
  - Replace the entire card markup with: `<partial name="_StudentIdCard" model="Model.CardModel" />`
  - Remove the old card CSS — partial owns it now
  - Keep the page-level CSS for screen background + controls + print rules:
    ```css
    @@media print {
        @@page { size: 85.6mm 54mm; margin: 0; }
        body { background: #fff; padding: 0; margin: 0; }
        .controls { display: none; }
        .id-card { box-shadow: none; }
    }
    ```
- [ ] On screen, scale up the card 3× via a wrapper for legibility (CSS `transform: scale(3)` or absolute mm-to-px factor). Keep the actual `.id-card` at exact mm so print stays correct.

**Files:**
- `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml` (rewrite)

### Phase 4: Smoke Tests + Targeted Unit Tests

- [ ] `tests/SmartLog.Web.Tests/Pages/PrintQrCodePageTests.cs` (extend if exists, otherwise create):
  - **`OnGetAsync_ValidStudent_LoadsBrandingSettings`** — seed `Branding:SchoolLogoPath = "/branding/x.png"`, `Branding:ReturnAddressText = "test"`. Assert page model exposes both via `CardModel`.
  - **`OnGetAsync_NoBrandingConfigured_ModelHasNullsAndPagesRenders`** — no settings seeded; `CardModel.SchoolLogoPath` and `CardModel.ReturnAddressText` are null. Page returns `PageResult`.
  - **`OnGetAsync_StudentMissingQr_ReturnsBadRequest`** — preserve existing behaviour.
- [ ] `tests/SmartLog.Web.Tests/Razor/StudentIdCardPartialTests.cs` (new — bUnit-style or snapshot via `RazorPartialRenderer` if available; otherwise skip and rely on smoke):
  - **`Renders_FooterBand_WhenReturnAddressSet`** — output contains `id-card__footer`.
  - **`OmitsFooterBand_WhenReturnAddressNull`** — output does NOT contain `id-card__footer`.
  - **`EscapesHtmlInReturnAddress`** — input `<script>alert(1)</script>` appears as `&lt;script&gt;...` in output.

  > **Note:** If the test project doesn't already have a Razor partial renderer set up, skip this file and rely on Phase 5 smoke. Adding a renderer is out of scope.

**Files:**
- `tests/SmartLog.Web.Tests/Pages/PrintQrCodePageTests.cs` (new or extend)
- `tests/SmartLog.Web.Tests/Razor/StudentIdCardPartialTests.cs` (optional — only if renderer is already wired)

### Phase 5: Manual Smoke

- [ ] `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"`
- [ ] Configure branding via US0111 page: upload a PNG logo, set school name, set return-address.
- [ ] Visit `/Admin/PrintQrCode/{any-student-guid}`.
- [ ] **Visual check** — screen card matches the layout spec (header centered logo+name, left photo+text, right big QR, footer line).
- [ ] Open DevTools, inspect `.id-card` element — confirm computed dimensions are `85.6mm × 54mm`.
- [ ] Click Print → browser preview shows exactly one card on a page sized 85.6mm × 54mm. No screen controls visible. No overflow.
- [ ] Clear `Branding:ReturnAddressText` → reload — footer band gone, body now ~45mm tall.
- [ ] Test with a student missing photo — initials placeholder shown.
- [ ] Test with a 50-character name — truncates, doesn't break layout.
- [ ] Test with no logo configured — placeholder SVG renders.
- [ ] Print to PDF and measure with PDF reader's ruler — card size is 85.6mm × 54mm.

### Phase 6: Build, Test, Update Story

- [ ] `dotnet build` clean (no warnings on the modified files).
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` passes.
- [ ] Update US0112 status → Review.
- [ ] Update US0077 with a "Superseded by US0112 on 2026-MM-DD" note in Revision History (do not change US0077's "Done" status — historical record).

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Models/StudentIdCardViewModel.cs` | Create | 1 |
| `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml.cs` | Modify (load 2 new keys, expose CardModel) | 1 |
| `src/SmartLog.Web/Pages/Admin/_StudentIdCard.cshtml` | Create (markup + CSS) | 2 |
| `src/SmartLog.Web/Pages/Admin/PrintQrCode.cshtml` | Rewrite (use partial, new print CSS) | 3 |
| `tests/SmartLog.Web.Tests/Pages/PrintQrCodePageTests.cs` | Create or extend | 4 |

No DB migration. No DI registrations.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| `@@page { size: 85.6mm 54mm; margin: 0 }` ignored by some printers | Most modern browsers honour `@page` size. If a printer driver doesn't, the card renders centered on the chosen paper size at exact mm — still correct, just framed by paper. |
| Long school name overflows header | `text-overflow: ellipsis` on `.id-card__school` truncates cleanly. Tested at 40+ chars. |
| Logo aspect ratio extreme (e.g. wide banner) | `object-fit: contain` + `max-width: 12mm` caps horizontal spread. |
| `<partial>` tag helper not registered | Already in use elsewhere (verify with grep). If not, fallback to `@await Html.PartialAsync("_StudentIdCard", Model.CardModel)`. |
| Print preview shows extra blank page | Caused by `body` margins; the print CSS sets `margin: 0` and `body { padding: 0; margin: 0 }`. |
| QR caption + QR collide on short cards | QR `aspect-ratio: 1/1` plus parent `flex` keeps caption beneath without overlap. |

---

## Open Questions

- **Should the screen preview show 1×, 2×, or 3× scale?** Trivial CSS toggle; keep at 3× for parity with the existing page (more legible to the admin).
- **Cache-bust the logo URL?** Defer until/unless we see stale-logo reports. Static-file middleware sends ETags by default.

---

## Done Definition

- [ ] All Phase 1–6 tasks checked off
- [ ] AC1–AC11 verified by smoke + tests
- [ ] `dotnet build` clean; `dotnet test` passes
- [ ] US0112 status → Review
- [ ] US0077 has "Superseded" note appended

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude (Opus 4.7) | Initial plan drafted |
