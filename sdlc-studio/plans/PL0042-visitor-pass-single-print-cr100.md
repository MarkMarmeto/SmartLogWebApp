# PL0042: Visitor Pass — Single-Pass Print on CR100 (Portrait)

> **Status:** Done
> **Story:** [US0122: Visitor Pass — Single-Pass Print on CR100](../stories/US0122-visitor-pass-single-print-cr100.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-05-20
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages + CSS print media
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Three discrete changes:

1. **New page** `Pages/Admin/PrintVisitorPass.cshtml` (singular) — renders one pass on a CR100 portrait card with QR above centre and `Code` label below.
2. **Edit** `Pages/Admin/VisitorPasses.cshtml` — add a "Print" button to each row's Actions column; remove the "Print Cards" navigation button.
3. **Delete** `Pages/Admin/PrintVisitorPasses.cshtml` and `.cshtml.cs` — bulk-print page is gone, no shim.

No schema changes, no service-layer changes, no new packages. We mirror the proven `PrintQrCode.cshtml` pattern (singular route, `Layout = null`, on-screen preview + `@media print` rules) so visual / behavioural consistency comes for free.

---

## Acceptance Criteria Mapping

| AC (US0122) | Phase |
|-------------|-------|
| AC1: Per-row Print button | Phase 2 — `VisitorPasses.cshtml` edit |
| AC2: Singular print page | Phase 1 — new `PrintVisitorPass.cshtml` + `.cs` |
| AC3: CR100 portrait layout | Phase 1 — `@page` + card CSS |
| AC4: On-screen preview | Phase 1 — preview scaling block |
| AC5: Verbatim PassCode label | Phase 1 — Razor `@Model.Pass.Code` |
| AC6: Bulk page removed cleanly | Phase 3 — delete files + grep verify |
| AC7: QR placeholder ("contact support" copy) | Phase 1 — Razor null-guard |
| AC8: Print defaults | Phase 1 — `@page { margin: 0 }` |
| AC9: No auto-fire `window.print()` on load | Phase 1 — Print only on button click |
| AC10: Access control | Phase 1 — copy attribute from `VisitorPasses.cshtml.cs` |

---

## Technical Context

### Current state (verified)

**`Pages/Admin/VisitorPasses.cshtml`** — list page.
- Line 14–15: "Print Cards" navigation button → `/Admin/PrintVisitorPasses` (will be deleted).
- Lines 128–135: table header — Pass # / Code / Status / Last Entry / Last Exit / Actions.
- Lines 182–215: Actions column — currently only Activate/Deactivate form button. Print button goes here, before the form.

**`Pages/Admin/PrintVisitorPasses.cshtml` + `.cshtml.cs`** — bulk-print page (to delete).
- `OnGetAsync` (lines 24–42) loads passes (all active, or filtered by `?ids=` query).
- 3-column CR80 grid in `@media print`.

**`Pages/Admin/PrintQrCode.cshtml`** — singular per-student ID print page; used as **template** for the new visitor page.
- Route constraint: `@page "{id:guid}"`.
- `Layout = null` + embedded `<style>`.
- Controls bar with Print + Back buttons hidden via `@media print`.
- On-screen `.card-wrap` scaled to ~323px for CR80 (will be adjusted to ~330px for CR100 portrait — see CSS plan below).

**`IVisitorPassService`** — service interface. Need to confirm a "get one by id" method exists. From the explore report: `VisitorPassService.cs:107` has `GetAllAsync()`. Phase 1 will verify and, if needed, add a thin `GetByIdAsync(Guid id)` accessor (single LINQ `FirstOrDefaultAsync` on `_context.VisitorPasses`). Existing service file owns DB access — no controller-side EF leak.

**Access control** — `VisitorPasses.cshtml.cs` page attribute (verify in Phase 1): expected `[Authorize(Policy = "CanManageStudents")]` based on `CLAUDE.md`. Mirror it on the new page.

**Route conflict check** — `/Admin/PrintVisitorPass/{id}` (singular) is currently unmapped. `/Admin/PrintVisitorPasses` (plural) goes away in Phase 3, so no overlap. The two are distinct routes and Razor Pages routing handles them by file name.

### CR100 dimensions

- **Spec:** 2.63" × 3.88" = **66.8mm × 98.6mm** portrait.
- **CSS:** use `mm` units in `@page` and in card `width`/`height` so browsers compute the same physical size regardless of screen DPI.
- **Print:** the `@page` rule sets the **sheet** the browser thinks it is printing on. Real users print on whatever paper is loaded (often A4); the browser will centre the CR100 card on the sheet with white margin. That's acceptable and standard for card-stock workflows — the card itself is dimensionally exact, the cutting is manual or done by a card printer that auto-crops to its loaded stock.

### QR sizing

- Stock QR PNGs from `QrImageBase64` are 300×300 logical px at gen time (per existing `IVisitorPassService` behaviour — verify in Phase 1).
- Print as `50mm × 50mm`. At 300 DPI that's ~590 image px → upscale from 300px source is fine; QR codes survive moderate upscale because they're pure-black pixel grids.
- Position: top edge 6mm from card top; centred horizontally with `margin: 0 auto`.

### Label sizing

- Font: `Inter, "Segoe UI", Arial, sans-serif`, weight 700, size `14pt`, letter-spacing `0.04em` (unitless-relative — avoids mixing px into the mm/pt layout).
- Top margin from QR's bottom: `4mm`.
- Centred (`text-align: center`).
- 12-char headroom for `VISITOR-1000` (per AC5) — `14pt × 12ch ≈ 35mm` width, well under the 66.8mm card width.

---

## Implementation Phases

### Phase 0 — Mandatory verification (do this BEFORE writing any code)

Phase 0 is not optional — Phase 1's code snippets are *templates* that may need
small tweaks based on what you find here. Run all three checks:

**Check 1: `IVisitorPassService` service method.**

`src/SmartLog.Web/Services/IVisitorPassService.cs` + `VisitorPassService.cs`

Look for an existing single-pass accessor: `GetByIdAsync(Guid id)`, `FindAsync(Guid id)`,
`GetPassAsync(Guid id)`, `GetByCodeAsync(string code)`, etc.

- If a method with signature `Task<VisitorPass?>(Guid id)` exists under any name →
  **use it in Phase 1** and update the snippet's `_visitorPassService.GetByIdAsync(id)`
  call to match the real name.
- If only `GetAllAsync()` exists → **add** `GetByIdAsync(Guid id)` per the snippet
  below. This is the assumption Phase 1 was drafted against.

```csharp
// Interface
Task<VisitorPass?> GetByIdAsync(Guid id);

// Implementation
public Task<VisitorPass?> GetByIdAsync(Guid id) =>
    _context.VisitorPasses.FirstOrDefaultAsync(p => p.Id == id);
```

> **Why through the service layer?** Project convention keeps controllers / pages
> free of direct `DbContext` access for the visitor-pass aggregate. Adding the
> method is ~5 LOC and keeps the cleanup consistent.

**Check 2: Authorization policy on the list page.**

Open `src/SmartLog.Web/Pages/Admin/VisitorPasses.cshtml.cs` and read the
class-level `[Authorize(Policy = "...")]` attribute (or page-folder convention
from `_ViewImports.cshtml` if no per-page attribute).

- If the policy is `CanManageStudents` → Phase 1 snippet is correct as-is.
- If it's something else (e.g. `RequireAdmin`, `CanManageVisitorPasses`) →
  **update Phase 1's `[Authorize(Policy = "...")]` to match exactly.**
- Record the actual policy name in the commit message so future review of this
  story has the trace.

**Check 3: `_ViewImports.cshtml` for namespace / `@using` directives.**

Razor Pages under `Pages/Admin/` typically inherit `@using` directives from a
nearby `_ViewImports.cshtml`. Skim it so Phase 1's `.cshtml` doesn't re-import
already-imported namespaces (cosmetic, but matches house style).

---

---

### Phase 1 — New `PrintVisitorPass.cshtml` Page

**New file:** `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml`
**New file:** `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml.cs`

**Backing page model (`.cshtml.cs`):**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageStudents")]  // mirror VisitorPasses.cshtml.cs
public class PrintVisitorPassModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;

    public PrintVisitorPassModel(IVisitorPassService visitorPassService)
    {
        _visitorPassService = visitorPassService;
    }

    public VisitorPass Pass { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var pass = await _visitorPassService.GetByIdAsync(id);
        if (pass is null)
        {
            return NotFound();
        }

        Pass = pass;
        return Page();
    }
}
```

**Razor view (`.cshtml`):** Follow the structure of `PrintQrCode.cshtml`:

```razor
@page "{id:guid}"
@model SmartLog.Web.Pages.Admin.PrintVisitorPassModel
@{
    ViewData["Title"] = "Print Visitor Pass";
    Layout = null;
}
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Visitor Pass — @Model.Pass.Code</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }

        body {
            font-family: 'Inter', 'Segoe UI', Arial, sans-serif;
            background: #f0f0f0;
            padding: 24px;
        }

        .controls {
            max-width: 500px;
            margin: 0 auto 24px;
            display: flex;
            align-items: center;
            gap: 10px;
        }
        .controls h1 { font-size: 15px; font-weight: 600; color: #333; flex: 1; }
        .btn { padding: 8px 18px; border-radius: 6px; font-size: 13px; font-weight: 600; cursor: pointer; border: none; text-decoration: none; display: inline-flex; align-items: center; gap: 5px; }
        .btn-primary   { background: #2C5F5D; color: #fff; }
        .btn-secondary { background: #fff; color: #555; border: 1px solid #ccc; }
        .btn:hover     { opacity: .85; }

        /* On-screen preview: scale CR100 portrait so it's legible (~330px wide) */
        .card-wrap { width: 252px; /* 66.8mm × 3.779 px/mm ≈ 252px */ margin: 0 auto; }

        /* Base (print-true) sizing in mm + pt. Used as-is when printing.
           IMPORTANT: keep all base .visitor-card descendant rules ABOVE the
           @media screen block so the screen overrides win on source order. */
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

        .visitor-card .qr-img {
            width: 50mm;
            height: 50mm;
            display: block;
            margin: 0 auto;
        }

        .visitor-card .qr-placeholder {
            width: 50mm;
            height: 50mm;
            display: flex;
            align-items: center;
            justify-content: center;
            text-align: center;
            font-size: 9pt;
            color: #888;
            border: 1px dashed #bbb;
        }

        .visitor-card .code-label {
            margin-top: 4mm;
            font-weight: 700;
            font-size: 14pt;
            letter-spacing: 0.04em;   /* unitless-relative; safer than mixing px into a mm/pt layout */
            text-align: center;
            color: #000;
        }

        /* On-screen: convert mm box to pixel preview at ~3.779 px/mm so the
           preview visually matches the printed card. MUST come after the base
           rules above — same selector specificity, so source order decides. */
        @@media screen {
            .visitor-card {
                width: 252px;        /* 66.8mm × 3.779 px/mm ≈ 252.4px */
                height: 372px;       /* 98.6mm × 3.779 px/mm ≈ 372.6px */
                padding-top: 23px;   /* 6mm × 3.779 px/mm ≈ 22.7px */
            }
            .visitor-card .qr-img,
            .visitor-card .qr-placeholder { width: 189px; height: 189px; }   /* 50mm @ ~3.78 */
            .visitor-card .code-label { margin-top: 15px; font-size: 14pt; }
        }

        /* Nesting @page inside @media print is valid CSS Paged Media and is
           well-supported in Chrome 90+, Firefox 100+, Edge. Do not "fix" by
           moving @page to the top level — the size only applies for print here. */
        @@media print {
            body { background: #fff; padding: 0; margin: 0; }
            .controls { display: none; }
            .card-wrap { width: auto; }
            .visitor-card { width: 66.8mm; height: 98.6mm; padding-top: 6mm; }
            @@page { size: 66.8mm 98.6mm; margin: 0; }
        }
    </style>
</head>
<body>
    <div class="controls">
        <h1>Print Visitor Pass — @Model.Pass.Code</h1>
        <a href="/Admin/VisitorPasses" class="btn btn-secondary">Back</a>
        <button class="btn btn-primary" onclick="window.print()">Print</button>
    </div>

    <div class="card-wrap">
        <div class="visitor-card">
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
        </div>
    </div>
</body>
</html>
```

**Notes:**

- `@@` in Razor escapes a literal `@`. The CSS rules `@@media`, `@@page` will render as `@media`, `@page`.
- Screen and print blocks coexist: on-screen the card is sized in pixels (252×372) for legible preview; in print mode the same `.visitor-card` switches to mm units to guarantee physical dimensions.
- The QR placeholder is intentionally visible (dashed border) so it can't be confused with an empty card — AC7 wants the admin to notice. Copy is "QR not available — contact support"; the missing Regenerate-QR affordance is tracked as a discovered follow-up story in US0122.
- **Print is button-triggered only.** Do NOT add `onload="window.print()"`, `<body onload>`, or any auto-fire. Preview-first lets the admin catch a wrong-pass click before paper is wasted (AC9). Matches `PrintQrCode.cshtml` UX.

---

### Phase 2 — `VisitorPasses.cshtml` edits

**File:** `src/SmartLog.Web/Pages/Admin/VisitorPasses.cshtml`

**Edit 1: remove the "Print Cards" nav button (lines 14–15).** Delete the `<a asp-page="/Admin/PrintVisitorPasses" …>` block entirely. Keep surrounding layout (any container `<div>` etc.) intact — verify in plan that line 14 is the *button itself*, not the wrapping container.

**Edit 2: add a per-row Print button** inside the Actions column (the `<td>` starting at line 182), *before* the existing Activate/Deactivate form so the most common action stays rightmost:

```razor
<a asp-page="/Admin/PrintVisitorPass" asp-route-id="@pass.Id"
   target="_blank"
   class="btn btn-sm btn-outline-secondary me-1"
   title="Print this visitor pass">
    <i class="bi bi-printer"></i> Print
</a>
```

The `me-1` margin spaces it from the form button. `target="_blank"` matches the per-section ID-card print pattern (`Sections.cshtml:93`).

---

### Phase 3 — Delete the Bulk-Print Page

**Files to delete:**
- `src/SmartLog.Web/Pages/Admin/PrintVisitorPasses.cshtml`
- `src/SmartLog.Web/Pages/Admin/PrintVisitorPasses.cshtml.cs`

Use `rm` (or `git rm`) — no `[Obsolete]` shim, no redirect. Per AC6 and project convention.

**Post-delete grep verification (run during execution):**

```bash
grep -rn "PrintVisitorPasses\|/Admin/PrintVisitorPasses" \
  src/SmartLog.Web/ tests/SmartLog.Web.Tests/ \
  | grep -v "/bin/" | grep -v "/obj/"
```

Should return **zero hits**. If anything turns up (sidebar nav, test fixtures, doc links), fix it in the same commit.

---

### Phase 4 — Doc Sync

**File:** `src/SmartLog.Web/CLAUDE.md`

Search for any mention of `PrintVisitorPasses` or "bulk print" in the visitor-pass section. From the explore report, the visitor-pass flow doc (section "Visitor Pass Flow (EP0012)") doesn't reference the print page by name, so probably no edit needed — but verify and update if found.

**Optional:** add a one-liner under EP0012's flow diagram noting that pass cards are printed individually via `/Admin/PrintVisitorPass/{id}`. Keep it brief — one sentence.

---

### Phase 5 — Tests

**File:** `tests/SmartLog.Web.Tests/Pages/PrintVisitorPassTests.cs` (new) — *if the test project has a `Pages` folder pattern for similar pages; otherwise inline under `Pages` mirroring whatever convention exists.*

Looking at the test project structure from earlier work: `tests/SmartLog.Web.Tests/{Controllers, Pages, Services, Helpers}`. So `Pages/PrintVisitorPassTests.cs` is the right home.

Test cases:

| # | Test | Setup | Asserts |
|---|------|-------|---------|
| 1 | `OnGetAsync_ValidId_ReturnsPage` | Seed one pass via `IVisitorPassService` mock | `PageResult`; `Model.Pass.Code` matches seeded code |
| 2 | `OnGetAsync_UnknownId_ReturnsNotFound` | Service mock returns `null` | `NotFoundResult` |
| 3 | *(Razor rendering tests are not standard in this codebase — skip)* | — | — |

For the bulk-page deletion verification, the grep test in Phase 3 is sufficient as a build-time safeguard; no xUnit needed.

For the per-row Print button — covered by manual smoke (AC1) in Phase 6.

> **Why so few tests?** Razor Pages CRUD tests in this project are sparse (per the test folder structure). Adding heavyweight `WebApplicationFactory` integration tests for a print page is over-investment — the page is a thin model + static template. Two PageModel unit tests cover the only branching logic (`Pass` lookup → 200 or 404).

---

### Phase 6 — Manual Smoke

1. Run `dotnet run` locally (`http://localhost:5050`).
2. Navigate `/Admin/VisitorPasses` — confirm:
   - No "Print Cards" button at the top.
   - Each row has a Print button in the Actions column.
3. Click Print on row 1:
   - New tab opens at `/Admin/PrintVisitorPass/{guid}`.
   - **Print dialog does NOT auto-open** (AC9 — preview-first).
   - On-screen preview shows QR centred, label `VISITOR-001` below.
   - Click the in-page Print button → browser print dialog shows a portrait card with the dimensions visible in preview.
4. Visit `/Admin/PrintVisitorPasses` directly — confirm `404`.
5. Visit `/Admin/PrintVisitorPass/00000000-0000-0000-0000-000000000000` — confirm `404`.
6. Visit `/Admin/PrintVisitorPass/not-a-guid` — confirm `404` (route constraint).
7. Logout, hit `/Admin/PrintVisitorPass/{realId}` — confirm redirect to login.

---

## File-Level Change List

| File | Change | Approx LOC |
|------|--------|------------|
| `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml` | **New** | +110 |
| `src/SmartLog.Web/Pages/Admin/PrintVisitorPass.cshtml.cs` | **New** | +25 |
| `src/SmartLog.Web/Pages/Admin/VisitorPasses.cshtml` | Edit (remove top button, add per-row button) | -3 / +7 |
| `src/SmartLog.Web/Pages/Admin/PrintVisitorPasses.cshtml` | **Delete** | -~140 |
| `src/SmartLog.Web/Pages/Admin/PrintVisitorPasses.cshtml.cs` | **Delete** | -~45 |
| `src/SmartLog.Web/Services/IVisitorPassService.cs` | Maybe add `GetByIdAsync` | +1 (if needed) |
| `src/SmartLog.Web/Services/VisitorPassService.cs` | Maybe add `GetByIdAsync` | +3 (if needed) |
| `tests/SmartLog.Web.Tests/Pages/PrintVisitorPassTests.cs` | **New** | +60 |
| `src/SmartLog.Web/CLAUDE.md` | Optional one-liner under EP0012 flow | +1 (if any) |

**No migrations. No DI changes. No new packages.**

---

## Risk & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| `IVisitorPassService.GetByIdAsync` already exists with a different signature (e.g. takes string code) | Low | Low | Phase 0 verifies before adding; if it exists, reuse it (rename if needed) |
| The CR100 dimensions are too tight for the printer driver to keep on one page | Medium | Low | Browser print dialog has "Fit to page" toggle; the `@page size` is advisory. Manual smoke verifies on actual print preview |
| Browser print rendering differs (Chrome vs Edge vs Firefox) | Medium | Low | The `mm` units + `@page size` are part of CSS Paged Media spec — well-supported. AC8 notes the best-effort caveat |
| Operator clicks "Print Cards" link from a stale bookmark | Low | Low | Page 404s — clean and obvious. Cleaner than a redirect that obscures what happened |
| `target="_blank"` opens a new tab per click, accumulating tabs during a re-issuance session | Low | Low | Acceptable — admin closes the tab after printing. Matches `PrintIdCards` pattern; users already know this UX |
| Visual regression in screen preview (preview doesn't match printed dimensions) | Low | Medium | The CSS uses a `@media screen` block to lock the preview's px size matching the `mm` print size at 3.779 px/mm — same math as `PrintQrCode.cshtml` |

---

## Out of Scope (explicit)

- New batch-print page on CR100. *Possible follow-up* if the workflow regression bites; explicitly deferred.
- Audit-log entry on print. Not a security-relevant action.
- Print-headless / PDF export. Browser print only.
- Card stock toggle (CR80 ↔ CR100). Single stock per spec.
- Visual changes to the list page beyond removing one nav button and adding one row button.

---

## Estimation

**Story Points:** 2
**Complexity:** Low
**Estimated Time:** 1.5–2 hrs

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-20 | Claude (Opus 4.7) | Initial draft |
