# PL0011: Scan Logs — Record and Display Camera Identity — Implementation Plan

> **Status:** Complete
> **Story:** [US0093: Scan Logs — Record and Display Camera Identity](../stories/US0093-scan-logs-camera-column.md)
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Extend the server side of the camera-identity contract so every scan row records which physical camera captured it (slot index + user-assigned name), for both student `Scan` and anonymous `VisitorScan`. Add the matching UI column + filter on Scan Logs and Visitor Scan Log. This is the WebApp counterpart to scanner story US0090 — the wire format decided here fixes the scanner payload shape.

**Pre-existing state discovered during planning** (important — reduces scope):

- `Scan.CameraIndex` (int?) **already exists** (migration `20260416232129_AddCameraIndexToScan`).
- `ScansApiController.ScanSubmissionRequest.CameraIndex` **already exists**; the student-scan path already persists it (`ScansApiController.cs:240`).
- `CameraName` does **not** exist anywhere.
- `VisitorScan` has **neither** `CameraIndex` nor `CameraName`; visitor ingestion does not carry camera identity.
- `ScanLogs.cshtml(.cs)` has filters for Status, ScanType, Student, Device, Date — no Camera filter yet, no Camera column.
- `VisitorScanLog.cshtml(.cs)` similar.

**Contract decision (must be made here, affects US0090):** index is **1-based** (matches US0093/US0090 AC). The existing `Scan.CameraIndex` XML doc comment says "0-based" — this is a **pre-existing comment-vs-story drift**; this plan aligns the docstring to 1-based. No data migration needed since the column is nullable and no production data exists yet that depends on 0- vs 1-based semantics.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Scan entity extended | `CameraIndex` already exists; add `CameraName` (nvarchar(100), nullable) |
| AC2 | API accepts new fields | Add `CameraName` to `ScanSubmissionRequest`; persist on student + visitor paths |
| AC3 | Backward compatibility | Nullable fields; legacy scanners still accepted |
| AC4 | Validation | `CameraIndex` in `[1, 8]` when non-null; `CameraName` silently truncated at 100 |
| AC5 | Scan Logs UI — camera column | New "Camera" column between Device and Scan Type, format `{index} · {name}` |
| AC6 | Scan Logs UI — filter by camera | New Camera filter; lists distinct `(index, name)` combos for the selected Device |
| AC7 | CSV export includes camera | `CameraIndex` and `CameraName` columns in exports |
| AC8 | VisitorScan parity | Same two columns + ingestion + Visitor Scan Log UI |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 Razor Pages + EF Core 8.0 + SQL Server
- **Test Framework:** xUnit + Moq (`tests/SmartLog.Web.Tests`)

### Existing Patterns
- **Entity + migration pattern:** entities in `Data/Entities/`, configure field lengths in `ApplicationDbContext.OnModelCreating`, migration via `dotnet ef migrations add`.
- **API DTO pattern:** `ScanSubmissionRequest` sits at the bottom of `ScansApiController.cs` alongside other DTOs. Validation via data-annotation attributes.
- **Scan Logs page pattern:** `ScanLogsModel` uses `[BindProperty(SupportsGet = true)]` for filter params; `query.Where(...)` chain per filter; `Include` for eager loads. Device dropdown populated in `OnGetAsync`. Follow the same shape for Camera filter.
- **Backward-compat field pattern:** precedent `Scan.CameraIndex` is nullable with no `[Required]` — extend the same way for `CameraName`.
- **Visitor scan persistence:** look at the visitor scan creation in `ScansApiController.HandleVisitorScanAsync` — needs the two new fields wired alongside student path.

### Library Documentation (Context7)

No external library needed. Standard EF Core + Razor Pages patterns.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Schema + DTO + UI changes; each has a clear, narrow surface. Tests are straightforward after implementation: one API integration test per path (student + visitor), one UI render test per page, one validation-rejection test. No algorithmic complexity justifies TDD.

### Test Priority
1. API integration: camera fields persisted end-to-end for student scan and visitor scan
2. API validation: out-of-range `CameraIndex` returns 400; over-length `CameraName` truncated silently
3. Backward compatibility: payload without new fields still succeeds
4. UI render: Camera column shows correctly (including legacy "—" rows)
5. Filter behaviour: Camera filter narrows results correctly

---

## Implementation Phases

### Phase 1: Schema — Scan.CameraName + VisitorScan.CameraIndex/Name

**Goal:** Persist the two camera fields on both scan tables; align the Scan docstring to 1-based.

- [ ] Add `CameraName` to `Scan.cs` — `public string? CameraName { get; set; }`, XML-doc it with the 1-based companion note; correct the existing `CameraIndex` docstring from "0-based" to "1-based slot index (1..N)".
- [ ] Add `CameraIndex` (int?) and `CameraName` (string?) to `VisitorScan.cs` with matching XML docs.
- [ ] Configure max length for both new `CameraName` columns in `ApplicationDbContext.OnModelCreating`:
  ```csharp
  modelBuilder.Entity<Scan>()
      .Property(s => s.CameraName).HasMaxLength(100);
  modelBuilder.Entity<VisitorScan>()
      .Property(v => v.CameraName).HasMaxLength(100);
  modelBuilder.Entity<VisitorScan>()
      .Property(v => v.CameraIndex).IsRequired(false);
  ```
- [ ] Create migration: `dotnet ef migrations add AddCameraIdentityColumns -p src/SmartLog.Web`
- [ ] Verify generated `Up()` adds three columns (no data loss) and `Down()` drops them.

**Files:** `Data/Entities/Scan.cs`, `Data/Entities/VisitorScan.cs`, `Data/ApplicationDbContext.cs`, `Migrations/{ts}_AddCameraIdentityColumns.cs`.

### Phase 2: API DTO + Persistence

**Goal:** API accepts `cameraName`; both paths persist `CameraIndex` + `CameraName`; validation as per AC4.

- [ ] Extend `ScanSubmissionRequest` (in `ScansApiController.cs`):
  ```csharp
  [Range(1, 8, ErrorMessage = "CameraIndex must be between 1 and 8")]
  public int? CameraIndex { get; set; }

  [StringLength(100, ErrorMessage = "CameraName cannot exceed 100 characters")]
  public string? CameraName { get; set; }
  ```
  > Note: AC4 says silent truncation for CameraName, not reject. Implement as: keep the attribute for validation-error reporting, but in the controller **before** persistence, truncate programmatically if length > 100:
  ```csharp
  var cameraName = request.CameraName is { Length: > 100 }
      ? request.CameraName[..100]
      : request.CameraName;
  ```
  (And remove `[StringLength]` so over-length doesn't return 400 — the attribute is incompatible with silent truncation. Keep `[Range]` for CameraIndex.)
- [ ] Update student-scan persistence (`ScansApiController.cs:230-241`):
  ```csharp
  var scan = new Scan {
      ...
      CameraIndex = request.CameraIndex,
      CameraName = cameraName
  };
  ```
- [ ] Update `HandleVisitorScanAsync` — pass `request` + truncated `cameraName` through to the VisitorScan creation; set both fields.
- [ ] Confirm error shape on invalid CameraIndex: ModelState invalid → existing framework behaviour returns 400 with validation detail.

**Files:** `Controllers/Api/ScansApiController.cs` (DTO + student path + visitor path).

### Phase 3: Scan Logs UI — Column + Filter

**Goal:** Scan Logs page shows Camera column and supports filtering by camera identity.

- [ ] `ScanLogs.cshtml.cs`:
  - Add `[BindProperty(SupportsGet = true)] public string? CameraFilter { get; set; }` — value shape `"1|Main Gate Left"` (pipe-joined index+name so distinct combos round-trip cleanly in a querystring).
  - In `OnGetAsync`, after existing filters:
    ```csharp
    if (!string.IsNullOrWhiteSpace(CameraFilter)) {
        var parts = CameraFilter.Split('|', 2);
        if (int.TryParse(parts[0], out var idx)) {
            var name = parts.Length > 1 ? parts[1] : null;
            query = query.Where(s => s.CameraIndex == idx && s.CameraName == name);
        }
    }
    ```
  - Load distinct camera combos **scoped to the currently-selected Device** for the dropdown:
    ```csharp
    AvailableCameras = await _context.Scans
        .Where(s => DeviceFilter == null || s.Device.Name == DeviceFilter)
        .Where(s => s.CameraIndex != null)
        .Select(s => new { s.CameraIndex, s.CameraName })
        .Distinct()
        .OrderBy(x => x.CameraIndex).ThenBy(x => x.CameraName)
        .ToListAsync();
    ```
  - Add `CameraIndex` + `CameraName` to the projected `ScanLogEntry` (check existing shape — may already be a class in this file).
- [ ] `ScanLogs.cshtml`:
  - Add a Camera filter dropdown alongside the existing Device filter; options come from `AvailableCameras` with value `"{idx}|{name}"`.
  - Add a "Camera" column header between Device and Scan Type.
  - Cell rendering:
    ```html
    @(s.CameraIndex.HasValue
        ? $"{s.CameraIndex} · {s.CameraName ?? "—"}"
        : "—")
    ```

**Files:** `Pages/Admin/ScanLogs.cshtml(.cs)`.

### Phase 4: Visitor Scan Log UI — Column + Filter

**Goal:** Same treatment on `VisitorScanLog` page.

- [ ] Apply the same filter + column addition pattern from Phase 3 to `Pages/Admin/VisitorScanLog.cshtml(.cs)`.
- [ ] Keep wording consistent with Scan Logs ("Camera" column; `{idx} · {name}` format).

**Files:** `Pages/Admin/VisitorScanLog.cshtml(.cs)`.

### Phase 5: CSV Export

**Goal:** Any scan-row export includes camera columns.

- [ ] Locate the export path — likely `Services/ScanExportService.cs` and/or `ReportsApiController.cs`. If an abstraction like `IScanRowMapper` exists, extend there; else update each CSV writer directly.
- [ ] Add `CameraIndex` and `CameraName` columns (append to existing column order; no reordering to keep older downstream consumers happy).
- [ ] Confirm `AttendanceReportService` or equivalent uses the same mapper — if it only emits aggregates, leave alone.

**Files:** `Services/ScanExportService.cs` (or wherever export lives — verify during implementation), `Controllers/Api/ReportsApiController.cs` if it projects row-level columns.

### Phase 6: Testing & Validation

**Goal:** Verify all acceptance criteria.

| AC | Verification Method | File Evidence | Status |
|----|---------------------|---------------|--------|
| AC1 | Migration applies cleanly; entity has `CameraName`; VisitorScan has both | `Migrations/{ts}_AddCameraIdentityColumns.cs` | Pending |
| AC2 | API integration test submits with both fields; row reflects them | `tests/ScansApiControllerTests.cs` (new: `SubmitScan_WithCameraIdentity_PersistsBothFields`) | Pending |
| AC3 | Submit without new fields → success, fields null | same file: `SubmitScan_WithoutCameraFields_IsAccepted` | Pending |
| AC4 | CameraIndex=0 / 99 → 400; CameraName=120 chars → truncated, not rejected | same file: `SubmitScan_WithInvalidCameraIndex_Returns400`, `SubmitScan_WithOverLongCameraName_Truncates` | Pending |
| AC5 | Render test / manual smoke: Camera column shows `{idx} · {name}` | `Pages/Admin/ScanLogs.cshtml` | Pending |
| AC6 | Filter submits with combined value, query narrows correctly | `tests/ScanLogsPageTests.cs` (new or extend) | Pending |
| AC7 | Run CSV export; verify columns present | manual smoke + export service unit test | Pending |
| AC8 | Visitor scan path persists both; Visitor Scan Log renders column | `tests/VisitorScanApiTests.cs` + `Pages/Admin/VisitorScanLog.cshtml` | Pending |

- [ ] Run `dotnet test` from repo root; confirm zero regressions.
- [ ] `dotnet build` clean with new migration applied on a scratch DB.

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | `cameraIndex` out of range (99) | `[Range(1,8)]` on DTO → 400 with ModelState error | Phase 2 |
| 2 | `cameraName` > 100 chars | Truncate silently before persistence; log `LogWarning` noting truncation | Phase 2 |
| 3 | `cameraIndex` null + `cameraName` set | Accept; store name only; UI shows "— · {name}" | Phase 2 + 3 |
| 4 | `cameraIndex` set + `cameraName` null | Accept; UI shows "{idx} · —" | Phase 2 + 3 |
| 5 | Legacy row (both null) | UI shows "—"; Camera filter includes an "(unknown)" option that matches both-null rows | Phase 3 |
| 6 | Camera filter when Device filter changes | `AvailableCameras` recomputes scoped to selected Device | Phase 3 |
| 7 | Existing CameraIndex semantics comment says 0-based | Update docstring to 1-based; no data migration needed (nullable column, no prod data) | Phase 1 |

**Coverage:** 7/7 edge cases handled.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Scanner (US0090) implementers adopt 0-based index to match stale docstring | High — end-to-end mismatch | Phase 1 explicitly corrects the docstring; call out 1-based in US0090 plan when written |
| `[StringLength(100)]` on DTO conflicts with silent-truncation AC4 requirement | Medium | Do not put `StringLength` on `CameraName` DTO field; truncate programmatically instead |
| Camera filter dropdown scales badly if many distinct `(idx, name)` combos across devices | Low | Dropdown is scoped to selected Device; bounded at 8 slots × small N of names per device |
| Visitor scan path misses persistence | Medium | Integration test in Phase 6 AC8 covers this explicitly |
| Export columns change breaks downstream parsers | Low | Append new columns rather than reorder; existing consumers ignore extra columns |

---

## Definition of Done

- [ ] `CameraName` (string?, max 100) added to `Scan` entity
- [ ] `CameraIndex` (int?) + `CameraName` (string?, max 100) added to `VisitorScan` entity
- [ ] Migration applied cleanly; three columns added nullable; `Down()` reverses
- [ ] `ScansApiController` accepts `cameraName`; validates `cameraIndex` range `[1, 8]`; silently truncates over-length name
- [ ] Both student-scan and visitor-scan paths persist both fields
- [ ] Legacy payloads (no camera fields) still accepted end-to-end
- [ ] Scan Logs page shows Camera column + Camera filter scoped to Device
- [ ] Visitor Scan Log page shows Camera column + Camera filter
- [ ] CSV exports include `CameraIndex` and `CameraName` columns
- [ ] Scan.CameraIndex XML doc corrected from "0-based" to "1-based"
- [ ] Unit + integration tests for all 8 AC passing
- [ ] `dotnet test` clean, `dotnet build` clean
- [ ] No lint errors

---

## Notes

- **Dependency on US0090:** US0093 fixes the wire contract the scanner must match. Once this plan is implemented, US0090 can be planned with certainty about field names (`cameraIndex`, `cameraName`), index base (1), and truncation semantics (client can send >100; server truncates).
- **Retention alignment:** when EP0017 lands, both columns travel with scan rows during archive-to-file (US0102); no extra change needed here.
- **No UI for editing Camera identity on existing rows** — this is scanner-provided metadata, immutable after ingestion (same semantics as `ScannedAt`).
- **Assumption to verify during implementation:** `ScanLogEntry` projection class (referenced in `ScanLogsModel`) — add `CameraIndex` + `CameraName` fields to it as well; the `.cs` file I scanned only shows the model's query setup, not the projection shape.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan drafted after discovering partial pre-existing state (`Scan.CameraIndex` + ingestion already in place) |
