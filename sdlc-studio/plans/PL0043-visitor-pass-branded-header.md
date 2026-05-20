# PL0043: Visitor Pass — Branded Header (Match Student ID Style)

> **Status:** Done
> **Story:** [US0123: Visitor Pass — Branded Header](../stories/US0123-visitor-pass-branded-header.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-05-20
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages + CSS print media
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Two narrow changes to the existing `/Admin/PrintVisitorPass/{id}` page:

1. **`PrintVisitorPass.cshtml.cs`** — inject `IAppSettingsService`, load three branding values, expose them as page properties (mirror `PrintQrCode.cshtml.cs`).
2. **`PrintVisitorPass.cshtml`** — add a 10mm teal-gradient header, a 6mm orange accent band, and a 5mm footer. Recompute QR/label vertical positioning to absorb the 16mm consumed at the top.

No new files. No service methods. No tests added (existing 2 PageModel tests cover the only branching logic — `Pass` lookup → Page or NotFound; branding is read-and-render only).

---

## Acceptance Criteria Mapping

| AC (US0123) | Phase |
|-------------|-------|
| AC1: Branded header band | Phase 2 — `.visitor-card__header` block |
| AC2: Orange accent band | Phase 2 — `.visitor-card__accent` block |
| AC3: QR + label layout preserved | Phase 2 — adjust `padding-top` math |
| AC4: Footer band | Phase 2 — `.visitor-card__footer` block |
| AC5: Branding via AppSettings | Phase 1 — page model change |
| AC6: Logo fallback (SVG) | Phase 2 — copy inline SVG from `_StudentIdCard.cshtml` |
| AC7: On-screen preview reflects print | Phase 2 — `@media screen` overrides for new elements |
| AC8: No CR100 overflow | Phase 2 — vertical math verified in plan |
| AC9: No US0122 regressions | All phases — only additive markup; route, auth, no-auto-print untouched |
| AC10: QR-missing placeholder still works | Phase 2 — placeholder renders inside the same QR slot |

---

## Technical Context

### Current state (post-US0122, verified)

**`Pages/Admin/PrintVisitorPass.cshtml.cs`** — minimal page model. Takes only `IVisitorPassService`; `OnGetAsync(Guid id)` returns Page or NotFound.

**`Pages/Admin/PrintVisitorPass.cshtml`** — has a `.card-wrap > .visitor-card` structure with:
- Base `.visitor-card { width: 66.8mm; height: 98.6mm; flex-direction: column; align-items: center; padding-top: 6mm; }`
- `.visitor-card .qr-img` (50mm)
- `.visitor-card .qr-placeholder` (50mm dashed)
- `.visitor-card .code-label` (14pt bold)
- `@media screen` overrides for px preview
- `@media print { @page { size: 66.8mm 98.6mm; margin: 0 } }`

### Pattern source — student ID

**`Pages/Admin/_StudentIdCard.cshtml`** (lines 1–269) holds the proven branded-header pattern. We will **copy** the header CSS + Razor block, adapted to the portrait card:

- Header: 10mm (vs student ID's 7mm) — more vertical room on the portrait layout. School block stays centred horizontally.
- Logo slot: 6mm tall (vs 5mm on student ID).
- Font sizes: school name 8pt (vs 7pt), address 5pt (same).
- SVG placeholder: copied **byte-for-byte** from `_StudentIdCard.cshtml:204–214`.

> **Why not extract a shared `_BrandHeader.cshtml` partial?** Tempting, but the two cards have different dimensions, different layouts, and different downstream layouts. A partial would either accept enough parameters to be a confusing god-component, or be too rigid to fit both. The duplication is ~30 LOC of well-commented CSS — cheaper than the abstraction.

### Branding AppSettings keys (existing)

| Key | Default | Source pattern |
|-----|---------|----------------|
| `System.SchoolName` | `"SmartLog School"` | `PrintQrCode.cshtml.cs:58` |
| `Branding:SchoolAddress` | `null` (hidden) | `PrintQrCode.cshtml.cs:59` |
| `Branding:SchoolLogoPath` | `null` (SVG fallback) | `PrintQrCode.cshtml.cs:60` |

All read via `IAppSettingsService.GetAsync(string key)`.

### Vertical budget on CR100 (98.6mm)

| Region | Height | Cumulative top |
|--------|--------|----------------|
| Header band | 10mm | 0–10 |
| Orange accent band | 6mm | 10–16 |
| Gap (breathing) | 4mm | 16–20 |
| QR | 50mm | 20–70 |
| Gap | 4mm | 70–74 |
| Code label (14pt ≈ 5mm) | 5mm | 74–79 |
| Free space | ~14mm | 79–93 |
| Footer band | 5mm | 93–98 |
| Bottom margin within card | 0.6mm | 98–98.6 |

QR vertical centre: 20 + 25 = **45mm** (above card midline 49.3mm — preserves US0122 AC3 "QR above center"). ✓

---

## Implementation Phases

### Phase 0 — Verify (mandatory, quick)

Run before editing:
1. Confirm `IAppSettingsService.GetAsync(string)` signature returns `Task<string?>` (it does; verified in this codebase already).
2. Re-read `_StudentIdCard.cshtml:18–75` for the header CSS to copy.
3. Re-read `_StudentIdCard.cshtml:195–223` for the header Razor block + SVG placeholder.

No code changes; just confirm the snippets in Phase 2 match the current source.

---

### Phase 1 — Page model: inject `IAppSettingsService`, expose branding

**File:** `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml.cs`

Add three properties + load them in `OnGetAsync` after the pass lookup. Match the property defaulting convention from `PrintQrCode.cshtml.cs`.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class PrintVisitorPassModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;
    private readonly IAppSettingsService _appSettings;

    public PrintVisitorPassModel(
        IVisitorPassService visitorPassService,
        IAppSettingsService appSettings)
    {
        _visitorPassService = visitorPassService;
        _appSettings = appSettings;
    }

    public VisitorPass Pass { get; private set; } = null!;
    public string SchoolName { get; private set; } = "SmartLog School";
    public string? SchoolAddress { get; private set; }
    public string? SchoolLogoPath { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var pass = await _visitorPassService.GetByIdAsync(id);
        if (pass is null)
        {
            return NotFound();
        }

        Pass = pass;
        SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
        SchoolAddress = await _appSettings.GetAsync("Branding:SchoolAddress");
        SchoolLogoPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
        return Page();
    }
}
```

**Test impact:** The existing 2 tests inject only `IVisitorPassService`. The CTOR signature is changing, so both tests must be updated to pass a mock `IAppSettingsService`. Since the tests don't assert on branding, a `new Mock<IAppSettingsService>().Object` with no setups (returns null for any `GetAsync`) is sufficient — the page model will fall back to defaults, which is fine for the assertions.

---

### Phase 2 — Razor view: header, accent, footer, layout shift

**File:** `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml`

Phase 2 has six discrete sub-changes. Do them in order:

- **2a:** Modify existing base `.visitor-card { ... }` rule.
- **2b:** Modify existing base `.visitor-card .qr-img { ... }` rule (extend selector + change margin).
- **2c:** Insert new base rules for header / accent / footer (between the existing `.code-label` rule and the `@media screen` block — preserves PL0042's source-order discipline).
- **2d:** Modify the existing `@media screen` block (drop `padding-top`, add overrides for new elements).
- **2e:** Modify the existing `@media print` block (lock mm dimensions for new elements).
- **2f:** Restructure the Razor body to render header + accent + QR/label + footer.

---

**2a — Modify `.visitor-card` base rule.** Remove `padding-top: 6mm` (header now sits flush at the top edge). Add `overflow: hidden` as belt-and-braces against rounding overflow.

Replace:

```css
.visitor-card {
    width: 66.8mm;
    height: 98.6mm;
    border: 1pt solid #000;
    background: #fff;
    display: flex;
    flex-direction: column;
    align-items: center;
    padding-top: 6mm;
}
```

With:

```css
.visitor-card {
    width: 66.8mm;
    height: 98.6mm;
    border: 1pt solid #000;
    background: #fff;
    display: flex;
    flex-direction: column;
    align-items: center;
    overflow: hidden;             /* belt-and-braces against rounding overflow */
}
```

---

**2b — Modify `.visitor-card .qr-img` rule.** Extend the selector to also apply to `.qr-placeholder`, and change the margin shorthand from `0 auto` to `4mm auto 0` so both get a 4mm gap below the accent band.

Replace:

```css
.visitor-card .qr-img {
    width: 50mm;
    height: 50mm;
    display: block;
    margin: 0 auto;
}
```

With:

```css
.visitor-card .qr-img,
.visitor-card .qr-placeholder {
    /* margin shorthand: top right bottom left — 4mm top gap below the accent band */
    margin: 4mm auto 0;
}

.visitor-card .qr-img {
    width: 50mm;
    height: 50mm;
    display: block;
}
```

The `.qr-placeholder` rule already declares its own `width: 50mm; height: 50mm` further down, so we don't repeat them here. The margin rule is shared via the combined selector.

---

**2c — Insert new base rules** for header, accent, and footer. Place them **after** the existing `.code-label` rule and **before** the `@media screen` block (PL0042 source-order discipline):

```css
/* ── Branded header (mirrors student ID's id-card__header) ── */
.visitor-card__header {
    height: 10mm;
    min-height: 10mm;
    width: 100%;
    background: linear-gradient(135deg, #2C5F5D 0%, #3d8a87 100%) !important;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 2mm;
    padding: 0 3mm;
    overflow: hidden;
}
.visitor-card__logo { height: 6mm; width: auto; object-fit: contain; max-width: 14mm; flex-shrink: 0; }
.visitor-card__logo-placeholder { height: 6mm; width: 6mm; flex-shrink: 0; display: flex; align-items: center; justify-content: center; }
.visitor-card__logo-placeholder svg { width: 6mm; height: 6mm; }
.visitor-card__school-block { display: flex; flex-direction: column; align-items: center; justify-content: center; min-width: 0; overflow: hidden; }
.visitor-card__school { font-size: 8pt; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 100%; line-height: 1.2; }
.visitor-card__school-address { font-size: 5pt; font-weight: 400; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 100%; opacity: 0.85; line-height: 1.2; }

/* ── Orange "VISITOR PASS" accent band ── */
.visitor-card__accent {
    height: 6mm;
    min-height: 6mm;
    width: 100%;
    background: #E07B39 !important;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 9pt;
    font-weight: 700;
    letter-spacing: 0.12em;
}

/* ── Footer ── */
.visitor-card__footer {
    margin-top: auto;             /* push to the bottom of the flex column */
    height: 5mm;
    min-height: 5mm;
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 5pt;
    color: #888;
}
```

---

**2d — Modify the existing `@media screen` block.** Remove the `padding-top: 23px` override (no longer needed) and the QR/placeholder `margin-top: 15px` (now overrides the 4mm base) and add overrides for the new bands so the screen preview matches print proportions:

```css
@@media screen {
    .visitor-card {
        width: 252px;
        height: 372px;
        /* padding-top removed (was 23px) */
    }
    .visitor-card__header { height: 38px; }                  /* 10mm */
    .visitor-card__accent { height: 23px; font-size: 9pt; }   /* 6mm */
    .visitor-card .qr-img,
    .visitor-card .qr-placeholder { width: 189px; height: 189px; margin-top: 15px; }
    .visitor-card .code-label { margin-top: 15px; font-size: 14pt; }
    .visitor-card__footer { height: 19px; }                  /* 5mm */
}
```

---

**2e — Modify the existing `@media print` block** so the new bands also force their mm dimensions in print. Remove the `padding-top: 6mm` override too (the base no longer has it):

```css
@@media print {
    body { background: #fff; padding: 0; margin: 0; }
    .controls { display: none; }
    .card-wrap { width: auto; }
    .visitor-card { width: 66.8mm; height: 98.6mm; }
    .visitor-card__header { height: 10mm; }
    .visitor-card__accent { height: 6mm; }
    .visitor-card__footer { height: 5mm; }
    @@page { size: 66.8mm 98.6mm; margin: 0; }
}
```

---

**2f — Restructure the Razor body.** Replace the existing `.card-wrap > .visitor-card` block with the branded structure. SVG placeholder copied verbatim from `_StudentIdCard.cshtml:205–213`:

```razor
<div class="card-wrap">
    <div class="visitor-card">

        <div class="visitor-card__header">
            @if (!string.IsNullOrEmpty(Model.SchoolLogoPath))
            {
                <img class="visitor-card__logo" src="@Model.SchoolLogoPath" alt="" />
            }
            else
            {
                <div class="visitor-card__logo-placeholder">
                    <svg viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <rect x="8" y="8" width="16" height="4" rx="1" fill="white"/>
                        <rect x="8" y="8" width="4" height="16" rx="1" fill="white"/>
                        <rect x="40" y="8" width="16" height="4" rx="1" fill="white"/>
                        <rect x="52" y="8" width="4" height="16" rx="1" fill="white"/>
                        <rect x="8" y="52" width="16" height="4" rx="1" fill="white"/>
                        <rect x="8" y="40" width="4" height="16" rx="1" fill="white"/>
                        <polyline points="22,34 29,41 42,26" stroke="white" stroke-width="4.5" stroke-linecap="round" stroke-linejoin="round" fill="none"/>
                    </svg>
                </div>
            }
            <div class="visitor-card__school-block">
                <span class="visitor-card__school">@Model.SchoolName</span>
                @if (!string.IsNullOrEmpty(Model.SchoolAddress))
                {
                    <span class="visitor-card__school-address">@Model.SchoolAddress</span>
                }
            </div>
        </div>

        <div class="visitor-card__accent">VISITOR PASS</div>

        @if (!string.IsNullOrEmpty(Model.Pass.QrImageBase64))
        {
            <img class="qr-img" alt="QR code for @Model.Pass.Code"
                 src="data:image/png;base64,@Model.Pass.QrImageBase64" />
        }
        else
        {
            <div class="qr-placeholder">
                QR not available —<br />contact support
            </div>
        }
        <div class="code-label">@Model.Pass.Code</div>

        <div class="visitor-card__footer">Return to Reception</div>

    </div>
</div>
```

Notes:
- The `.visitor-card__footer { margin-top: auto; }` rule lets it stick to the card's bottom even though earlier siblings have natural heights — the flex column distributes the free space.
- BEM-style class names (`visitor-card__header`, `__accent`, `__footer`) intentionally parallel the student ID's `id-card__*` pattern.
- The `.qr-img` / `.qr-placeholder` / `.code-label` selectors are kept as-is (not renamed to `visitor-card__qr` etc.) to minimise diff churn and keep the existing CSS coherent.

---

### Phase 3 — Update existing PageModel tests

**File:** `tests/SmartLog.Web.Tests/Pages/PrintVisitorPassTests.cs`

The CTOR now takes `IAppSettingsService`. Both tests need a mock:

```csharp
var appSettings = new Mock<IAppSettingsService>();
// No setups needed. Moq 4.x returns a completed Task with default(T) for
// unstubbed Task<T> methods, so `await GetAsync(...)` yields null and the
// page model takes the fallback path (SchoolName -> "SmartLog School",
// SchoolAddress/SchoolLogoPath -> null). If a future Moq version regresses,
// the test will fail loudly with NullReferenceException at the await.

var model = new PrintVisitorPassModel(service.Object, appSettings.Object);
```

No new test cases. The 2 existing tests still cover the only branching logic (`Pass` lookup → Page or NotFound). Branding rendering is visual and validated via manual smoke (Phase 5).

---

### Phase 4 — Build + run tests

```bash
dotnet build tests/SmartLog.Web.Tests --nologo -v q
dotnet test tests/SmartLog.Web.Tests --filter "FullyQualifiedName~PrintVisitorPassTests" --nologo --no-build
dotnet test tests/SmartLog.Web.Tests --nologo --no-build  # full suite, expect green
```

---

### Phase 5 — Manual Smoke

1. Set `System.SchoolName` to a real value via `/Admin/Settings/Branding` (or whatever admin page owns this — confirm during execution).
2. Set `Branding:SchoolLogoPath` to a real logo file, then to null. Verify both render.
3. Set `Branding:SchoolAddress`, then unset. Verify show/hide.
4. Navigate `/Admin/PrintVisitorPass/{realId}`. Verify on screen:
   - Teal gradient header with logo + school name (+ address if set).
   - Orange `VISITOR PASS` band directly below.
   - QR centred, label below, "Return to Reception" footer.
   - No clipping, no scrollbars.
5. Click Print → Browser print preview. Verify:
   - Gradient renders (not stripped to solid color).
   - Orange band renders.
   - Dimensions are 66.8mm × 98.6mm with zero margins.
6. Visit the page with a pass whose `QrImageBase64` is null (or temporarily NULL one in DB). Verify the placeholder still renders inside the QR slot and the rest of the card is intact.

---

## File-Level Change List

| File | Change | Approx LOC |
|------|--------|------------|
| `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml.cs` | Inject `IAppSettingsService`; expose 3 branding properties; load them in `OnGetAsync` | -2 / +18 |
| `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml` | Add header/accent/footer CSS + Razor blocks; remove `.visitor-card { padding-top }`; add QR `margin-top` | -2 / +90 |
| `tests/SmartLog.Web.Tests/Pages/PrintVisitorPassTests.cs` | Update both tests to pass a mock `IAppSettingsService` | -2 / +5 |

No migrations. No DI changes (services already registered). No new packages.

---

## Risk & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| `flex-column + margin-top: auto` on footer doesn't push to bottom in some Edge / WebKit print path | Low | Low | Verified pattern in student ID; if it fails, fall back to `position: absolute; bottom: 0` on `.visitor-card__footer` |
| Browser strips background colors in print despite `print-color-adjust: exact` | Medium | Medium | User must enable "Background graphics" in print dialog (standard browser behavior). AC2/AC4 are best-effort, noted in story Edge Cases. Same constraint applies to student ID — already accepted. |
| Logo image very wide (banner) breaks the header layout | Low | Low | `max-width: 14mm; object-fit: contain` mirrors the student ID constraint |
| `IAppSettingsService` mock setup forgotten in some test (CTOR error blocks build) | Low | Low | Build will fail loudly — easy fix. Caught in Phase 4. |
| The 4mm `margin-top` on QR pushes the label below 79mm threshold, causing overlap with footer | Very low | Medium | Vertical budget verified in Technical Context. Header 10 + accent 6 + qr-margin 4 + QR 50 + gap 4 + label 5 = 79mm; footer at 93mm. 14mm cushion. |
| Tests pass but Razor template has a runtime null-ref (e.g. `Model.SchoolName.ToString()` on null) | Low | Medium | `SchoolName` defaults to `"SmartLog School"` (never null); `SchoolAddress` / `SchoolLogoPath` are guarded with `!string.IsNullOrEmpty` in the template. Same convention as student ID. |

---

## Out of Scope (explicit)

- Refactoring student ID + visitor pass into a shared header partial.
- AppSettings key for accent color or footer text.
- Visual changes to the student ID, the list page, or any other page.
- New PageModel test cases (existing 2 are sufficient).

---

## Estimation

**Story Points:** 1
**Complexity:** Low
**Estimated Time:** 45–60 min

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-20 | Claude (Opus 4.7) | Initial draft |
