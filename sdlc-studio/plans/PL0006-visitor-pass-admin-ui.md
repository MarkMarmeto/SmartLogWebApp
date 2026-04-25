# PL0006: Visitor Pass Admin Management — Implementation Plan

> **Status:** Done
> **Story:** [US0074: Visitor Pass Admin Management](../stories/US0074-visitor-pass-admin-ui.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-04-18
> **Language:** C# / ASP.NET Core 8 Razor Pages

## Overview

Build the admin Razor pages for managing visitor passes: a list page with status badges (Available/InUse/Deactivated), generate button, max-passes configuration, activate/deactivate toggles, and a printable QR card layout. Follows existing admin page patterns (pagination, `[TempData]` status messages, audit logging).

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Pass List Page | Table at `/Admin/VisitorPasses` with Pass #, Code, Status badge, Last Entry, Last Exit, Actions |
| AC2 | Generate Passes Button | Creates only missing passes up to MaxPasses |
| AC3 | Configure Max Passes | Settings modal: "Maximum Passes" input, saves to AppSettings |
| AC4 | Deactivate Pass | Changes status to Deactivated; scanning returns REJECTED_PASS_INACTIVE |
| AC5 | Reactivate Pass | Changes Deactivated pass back to Available |
| AC6 | Print QR Cards | Printable page with QR image + code per card, grid layout for cutting |
| AC7 | Authorization | `RequireAdmin` policy; Teacher/Staff get 403 |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 Razor Pages
- **Test Framework:** xUnit (page model tests are thin — focus on service layer)

### Existing Patterns
- **Admin page conventions:** `[Authorize(Policy = "RequireAdmin")]`, DI for DbContext + UserManager + AuditService
- **Pagination:** `[BindProperty(SupportsGet = true)] PageNumber`, `PageSize = 20`, `TotalPages` computed
- **Status messages:** `[TempData] string? StatusMessage`
- **POST handlers:** `OnPostDeactivateAsync(Guid id)` pattern, audit log on state changes
- **Print pages:** Existing `PrintQrCode.cshtml` uses `@media print` CSS for card layout

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Razor pages are primarily view layer. Business logic (generate, deactivate) lives in `VisitorPassService` (tested in PL0004). Page model tests would only verify DI wiring and redirect behavior — low value.

---

## Implementation Phases

### Phase 1: VisitorPasses Index Page
**Goal:** List all passes with status and actions

- [ ] Create `src/SmartLog.Web/Pages/Admin/VisitorPasses/Index.cshtml.cs`
  - `[Authorize(Policy = "RequireAdmin")]`
  - Inject: `IVisitorPassService`, `IAuditService`, `UserManager<ApplicationUser>`
  - Properties: `List<VisitorPass> Passes`, `int MaxPasses`, `int ExistingCount`, `int AvailableToGenerate`
  - `OnGetAsync()`: load all passes ordered by PassNumber, load MaxPasses from service
  - `OnPostGenerateAsync()`: call `_visitorPassService.GeneratePassesAsync()`, audit log, redirect with StatusMessage
  - `OnPostDeactivateAsync(Guid id)`: call `_visitorPassService.DeactivatePassAsync(id)`, audit log
  - `OnPostActivateAsync(Guid id)`: call `_visitorPassService.ActivatePassAsync(id)`, audit log
- [ ] Create `src/SmartLog.Web/Pages/Admin/VisitorPasses/Index.cshtml`
  - Summary bar: "Total: N | Available: N | In Use: N | Deactivated: N"
  - "Generate Passes" button with count badge: "(N new passes available)"
  - "Settings" button → collapse panel with MaxPasses input + Save
  - Table: Pass # | Code | Status (badge) | Last Entry | Last Exit | Actions (Activate/Deactivate)
  - Status badges: `badge bg-success` (Available), `badge bg-warning` (In Use), `badge bg-danger` (Deactivated)

### Phase 2: Max Passes Configuration
**Goal:** Inline settings for pass count

- [ ] Add `OnPostSettingsAsync(int maxPasses)` to Index page model:
  - Validate: maxPasses >= 1
  - Call `_visitorPassService.SetMaxPassesAsync(maxPasses)`
  - Call `_visitorPassService.SyncPassCountAsync()` (deactivate excess if reduced)
  - Audit log, redirect with StatusMessage

### Phase 3: Print QR Cards Page
**Goal:** Printable card layout for selected or all active passes

- [ ] Create `src/SmartLog.Web/Pages/Admin/VisitorPasses/Print.cshtml.cs`
  - `[Authorize(Policy = "RequireAdmin")]`
  - Query param: `?ids=guid1,guid2,...` (optional, default = all active)
  - `OnGetAsync()`: load selected passes or all active, ordered by PassNumber
- [ ] Create `src/SmartLog.Web/Pages/Admin/VisitorPasses/Print.cshtml`
  - `@page` with `@{ Layout = null; }` (full-page print layout)
  - CSS: `@media print` styles, card grid (3 columns × N rows)
  - Each card: QR image (from QrImageBase64), "VISITOR-001", "SmartLog Visitor Pass"
  - Card size: ~85mm × 54mm (CR80 standard, same as student cards)
  - "Print" button (hidden in print CSS)

### Phase 4: Navigation & Menu Integration
**Goal:** Add visitor passes to admin sidebar

- [ ] Add menu item in admin layout (`_AdminLayout.cshtml` or shared nav partial):
  - "Visitor Passes" link under Security/Devices section
  - Visible to Admin and SuperAdmin roles
- [ ] Add "Deactivate InUse" confirmation modal:
  - When deactivating a pass with `CurrentStatus == "InUse"`, show JS confirm: "This pass is currently in use. Deactivate anyway?"

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Deactivate InUse pass | JavaScript confirm dialog: "This pass is currently in use. Deactivate anyway?" | Phase 4 |
| 2 | All passes in use | Summary bar highlights: "⚠ All passes in use" in orange; no action blocked | Phase 1 |
| 3 | MaxPasses set to 0 | Server-side validation: return error "Minimum 1 pass required" | Phase 2 |
| 4 | Print with no active passes | Show info message: "No active passes to print" instead of empty page | Phase 3 |
| 5 | MaxPasses reduced below current active count | `SyncPassCountAsync` deactivates highest-numbered excess passes | Phase 2 |

**Coverage:** 5/5 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| QR image not generated yet | Medium | Generate button calls `GeneratePassesAsync` which creates QR images |
| Print layout cross-browser issues | Low | Use simple CSS grid; test in Chrome (primary admin browser) |
| Large number of passes in table | Low | Max 100 passes; no pagination needed |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] Admin page renders with correct badges
- [ ] Generate/deactivate/activate functions work
- [ ] Print layout produces usable QR cards
- [ ] Authorization enforced
- [ ] Build succeeds (0 errors)
