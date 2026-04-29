# PL0034: School Branding Settings (Logo, Name, Return Address)

> **Status:** Complete
> **Story:** [US0111: School Branding Settings (Logo, Name, Return Address)](../stories/US0111-school-branding-settings.md)
> **Epic:** EP0013: QR Permanence & Card Redesign
> **Created:** 2026-04-27
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
> **Drafted by:** Claude (Opus 4.7)

## Overview

Add a single Razor Page under `Pages/Admin/Settings/Branding.cshtml` that lets Admins manage three branding values: school name (existing `System.SchoolName`), school logo (new file upload), and return-address text (new). The logo is stored as a static file under `wwwroot/branding/`; both new keys go into `AppSettings`. Reuses the existing `IAppSettingsService` and the validation pattern from `FileUploadService`.

This story is the prerequisite for US0112's redesigned card and US0113's bulk print, both of which read these settings.

---

## Acceptance Criteria Summary

| AC | Name | Implementation Phase |
|----|------|----------------------|
| AC1 | Settings page exists with 3 controls | Phase 2 |
| AC2 | School name editable, persists to `System.SchoolName` | Phase 2 |
| AC3 | Logo upload — happy path → `wwwroot/branding/` + `Branding:SchoolLogoPath` | Phase 3 |
| AC4 | Allowed types (PNG/JPG/SVG), ≤ 2 MB, magic-byte + SVG safety | Phase 1, 3 |
| AC5 | Remove logo clears file + setting | Phase 3 |
| AC6 | Return-address text persists to `Branding:ReturnAddressText`, ≤ 120 chars, escaped | Phase 2 |
| AC7 | `RequireAdmin` policy on the page | Phase 2 |
| AC8 | Audit log written on save / upload / remove (`SchoolBrandingUpdated`) | Phase 2, 3 |

---

## Technical Context

### Why a New Service (Not Extend FileUploadService)
`FileUploadService` is purpose-built for profile pictures (entity-keyed filenames, no SVG). A separate `IBrandingService` keeps the SVG sanitization, deterministic filename, and atomic replace logic in one place — `FileUploadService` stays focused on its current contract.

### AppSettings Keys

| Key | Category | Initial Value | Sensitive? |
|-----|----------|---------------|------------|
| `Branding:SchoolLogoPath` | Branding | `""` | No |
| `Branding:ReturnAddressText` | Branding | `""` | No |
| `System.SchoolName` | System | (existing — not changed by this plan, only edited via new UI) | No |

Use `IAppSettingsService.SetAsync(key, value, "Branding", User.Identity.Name)` to persist.

### File Storage
- Directory: `src/SmartLog.Web/wwwroot/branding/`
- Filename: `school-logo.{ext}` — deterministic, replaces prior on upload
- Served by static-file middleware automatically (no route change)

### SVG Safety Check
Before saving an SVG, read its full bytes as UTF-8 and reject if the lowercased content contains any of: `<script`, `onload=`, `onerror=`, `<foreignobject`. Cheap regex; covers the realistic XSS surface for an admin-only upload.

### Magic Bytes
Reuse the byte signatures from `FileUploadService` for PNG/JPG. SVG has no magic bytes — rely on the `<svg` opening tag check after the safety scan.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Standard upload + Razor settings page. Tests cover the `IBrandingService` validation and the page handler's persistence; visual smoke covers the settings UI.

---

## Implementation Phases

### Phase 1: Branding Service

**Goal:** Encapsulate logo upload/removal + validation in one testable service.

- [ ] Create `src/SmartLog.Web/Services/Branding/IBrandingService.cs`:
  ```csharp
  public interface IBrandingService
  {
      Task<string> UploadLogoAsync(IFormFile file);  // returns relative path; throws on invalid
      Task RemoveLogoAsync();                        // deletes file + clears AppSettings key
      Task<bool> IsValidLogoAsync(IFormFile file);   // size, ext, MIME, magic bytes, SVG safety
  }
  ```
- [ ] Create `src/SmartLog.Web/Services/Branding/BrandingService.cs` with:
  - `_allowedExtensions = { ".png", ".jpg", ".jpeg", ".svg" }`
  - `_maxFileSize = 2 * 1024 * 1024`
  - `_svgUnsafePatterns = { "<script", "onload=", "onerror=", "<foreignobject" }`
  - Constructor: `IWebHostEnvironment`, `IAppSettingsService`, `ILogger<BrandingService>`
  - `UploadLogoAsync` — validates → ensures `wwwroot/branding/` exists → writes `school-logo.{ext}` (delete prior if extension changed) → `SetAsync("Branding:SchoolLogoPath", "/branding/school-logo.{ext}", "Branding", updatedBy: null)`. Caller adds audit; service stays generic.
  - `RemoveLogoAsync` — read current path from settings → delete file if exists → `SetAsync(..., null, ...)`
  - `IsValidLogoAsync` — size/ext/MIME/magic bytes for PNG/JPG; for SVG: read full bytes, lowercase, check unsafe-patterns regex, plus require `<svg` to appear
- [ ] Register in `Program.cs`: `builder.Services.AddScoped<IBrandingService, BrandingService>();`

**Files:**
- `src/SmartLog.Web/Services/Branding/IBrandingService.cs` (new)
- `src/SmartLog.Web/Services/Branding/BrandingService.cs` (new)
- `src/SmartLog.Web/Program.cs` (DI registration)

### Phase 2: Settings/Branding Razor Page

**Goal:** Admin UI for the three fields, wired to `IAppSettingsService` + `IBrandingService`.

- [ ] Create `src/SmartLog.Web/Pages/Admin/Settings/Branding.cshtml.cs`:
  ```csharp
  [Authorize(Policy = "RequireAdmin")]
  public class BrandingModel : PageModel
  {
      // Constructor: IAppSettingsService, IBrandingService, IAuditService, ILogger
      [BindProperty] public BrandingForm Form { get; set; } = new();
      public string? CurrentLogoPath { get; set; }
      [TempData] public string? StatusMessage { get; set; }
      [TempData] public string? ErrorMessage { get; set; }

      public async Task OnGetAsync() { /* load from AppSettings */ }
      public async Task<IActionResult> OnPostSaveAsync() { /* validate + persist name + return-address */ }
      public async Task<IActionResult> OnPostUploadLogoAsync(IFormFile logoFile) { /* call BrandingService.UploadLogoAsync */ }
      public async Task<IActionResult> OnPostRemoveLogoAsync() { /* call BrandingService.RemoveLogoAsync */ }
  }

  public class BrandingForm
  {
      [Required, StringLength(200)] public string SchoolName { get; set; } = "";
      [StringLength(120)] public string? ReturnAddressText { get; set; }
  }
  ```
- [ ] Create `src/SmartLog.Web/Pages/Admin/Settings/Branding.cshtml`:
  - `@page "/Admin/Settings/Branding"`
  - Form 1 (Save) — School Name input + Return Address input + Save button
  - Form 2 (Upload Logo) — `<input type="file" accept=".png,.jpg,.jpeg,.svg">` + Upload button (multipart/form-data)
  - Form 3 (Remove Logo) — only renders when `CurrentLogoPath != null`; single Remove button
  - Logo preview: `<img src="@Model.CurrentLogoPath" />` when present
- [ ] Audit calls (after each successful operation):
  ```csharp
  await _audit.LogAsync("SchoolBrandingUpdated",
      User.Identity?.Name,
      details: $"keys: {string.Join(",", changedKeys)}");
  ```
- [ ] Add a navigation link in `Pages/Shared/_AdminMenu.cshtml` (or wherever Settings menu lives) under Settings group: "Branding" → `/Admin/Settings/Branding`

**Files:**
- `src/SmartLog.Web/Pages/Admin/Settings/Branding.cshtml` (new)
- `src/SmartLog.Web/Pages/Admin/Settings/Branding.cshtml.cs` (new)
- Admin menu partial (modify — add link)

### Phase 3: Wire Upload Handler + Error Surfaces

**Goal:** The two POST handlers translate service exceptions to user-visible TempData messages and 400 status where appropriate.

- [ ] In `OnPostUploadLogoAsync`:
  - If `logoFile == null || logoFile.Length == 0`: `ErrorMessage = "Please select a file to upload"`; redirect to GET.
  - Wrap `_branding.UploadLogoAsync(logoFile)` in try/catch; on `InvalidOperationException` (validation failure), set `ErrorMessage` with the message; redirect.
  - On success: `StatusMessage = "School logo uploaded"`, audit, redirect.
- [ ] In `OnPostRemoveLogoAsync`:
  - Call `_branding.RemoveLogoAsync()`, audit, redirect with `StatusMessage = "School logo removed"`.
- [ ] Render `StatusMessage` / `ErrorMessage` in the page (Bootstrap alert classes — match the Retention page convention).

**Files:** Same as Phase 2 (`Branding.cshtml.cs`).

### Phase 4: Tests

**Goal:** Lock the validation contract and the persistence path.

- [ ] Create `tests/SmartLog.Web.Tests/Services/Branding/BrandingServiceTests.cs`:
  - **`IsValidLogoAsync_PngUnder2Mb_ReturnsTrue`** — happy path PNG.
  - **`IsValidLogoAsync_OversizedFile_ReturnsFalse`** — 3 MB file.
  - **`IsValidLogoAsync_PdfFile_ReturnsFalse`** — wrong extension.
  - **`IsValidLogoAsync_SvgWithScriptTag_ReturnsFalse`** — `<svg><script>alert(1)</script></svg>`.
  - **`IsValidLogoAsync_SvgWithOnloadAttr_ReturnsFalse`** — `<svg onload="x"...>`.
  - **`IsValidLogoAsync_PlainSvg_ReturnsTrue`** — `<svg ...><circle.../></svg>`.
  - **`IsValidLogoAsync_PngMagicBytesMismatch_ReturnsFalse`** — `.png` extension on a JPG.
  - **`UploadLogoAsync_HappyPath_WritesFileAndPersistsPath`** — uses `Mock<IAppSettingsService>` + temp `IWebHostEnvironment`; asserts file exists and `SetAsync` called.
  - **`RemoveLogoAsync_DeletesFileAndClearsSetting`** — file exists, then service called, then file gone + `SetAsync(..., null, ...)`.
  - **`UploadLogoAsync_ChangingExtension_DeletesOldFile`** — upload PNG, then upload SVG, then PNG file no longer exists.
- [ ] Create `tests/SmartLog.Web.Tests/Pages/BrandingPageTests.cs` (or extend an existing settings-page test if one exists):
  - **`OnPostSaveAsync_TooLongReturnAddress_AddsModelError`** — 121-char string.
  - **`OnPostSaveAsync_HappyPath_PersistsBothKeys_AuditLogged`** — verify both `SetAsync` calls and audit invocation.

**Files:**
- `tests/SmartLog.Web.Tests/Services/Branding/BrandingServiceTests.cs` (new)
- `tests/SmartLog.Web.Tests/Pages/BrandingPageTests.cs` (new)

### Phase 5: Manual Smoke

- [ ] `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"`
- [ ] Sign in as `superadmin`. Visit `/Admin/Settings/Branding`.
- [ ] Edit School Name → Save. Reload page; value persists.
- [ ] Edit Return Address → Save. Reload; value persists.
- [ ] Upload a 1MB PNG. Page reloads with preview; `wwwroot/branding/school-logo.png` exists; `AppSettings.Branding:SchoolLogoPath` set.
- [ ] Upload a 3MB PNG → rejected with friendly error.
- [ ] Upload `evil.svg` containing `<script>` → rejected.
- [ ] Upload a clean SVG → accepted; preview shows it.
- [ ] Click Remove Logo → file deleted; `Branding:SchoolLogoPath` cleared.
- [ ] Sign out, sign in as a Teacher. Visit URL directly → 403.
- [ ] Check `AuditLog` table for `SchoolBrandingUpdated` rows on each operation.

### Phase 6: Build, Test, Check

- [ ] `dotnet build` clean.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` passes.
- [ ] Update US0111 status → Review.

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Services/Branding/IBrandingService.cs` | Create | 1 |
| `src/SmartLog.Web/Services/Branding/BrandingService.cs` | Create | 1 |
| `src/SmartLog.Web/Program.cs` | Modify (DI) | 1 |
| `src/SmartLog.Web/Pages/Admin/Settings/Branding.cshtml` | Create | 2 |
| `src/SmartLog.Web/Pages/Admin/Settings/Branding.cshtml.cs` | Create | 2 |
| Admin menu partial | Modify (link) | 2 |
| `src/SmartLog.Web/wwwroot/branding/` | Create directory (auto on first upload) | 1 |
| `tests/SmartLog.Web.Tests/Services/Branding/BrandingServiceTests.cs` | Create | 4 |
| `tests/SmartLog.Web.Tests/Pages/BrandingPageTests.cs` | Create | 4 |

No DB migration — both keys live in existing `AppSettings` table.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Old extension file lingers when admin uploads a new format (e.g. PNG → SVG) | Service deletes prior file when extension changes; covered by test |
| SVG sanitizer misses an XSS vector | Admin-only upload, RequireAdmin policy already gates uploaders. Pattern check is defense-in-depth, not the primary control. |
| Static file middleware caches old logo after replace | Filename includes content type but not a hash. If browsers cache aggressively, append a cache-buster query string when rendering on the card (`?v={mtime-ticks}`). Add only if observed in smoke. |
| `IAuditService` signature varies | Match the call shape used in `Pages/Admin/Settings/Retention.cshtml.cs` exactly. |

---

## Open Questions

- **Logo height in card header — fixed 7mm or auto?** Resolved in PL0035. This plan only stores the file.
- **Validation messages — i18n?** No. The app does not localize admin pages. Plain English.

---

## Done Definition

- [ ] All Phase 1–6 tasks checked off
- [ ] AC1–AC8 verified by tests + manual smoke
- [ ] `dotnet build` clean; `dotnet test` passes
- [ ] US0111 status flipped to Review

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude (Opus 4.7) | Initial plan drafted |
