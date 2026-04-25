# US0093: Scan Logs — Record and Display Camera Identity

> **Status:** Done
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Admin Amy (Administrator)
**I want** every scan log entry to record and display which camera produced the scan — both slot index and user-assigned name
**So that** I can audit gate activity per camera lane, diagnose hardware issues, and attribute scans correctly in multi-camera deployments.

## Context

### Persona Reference
**Admin Amy** — Reviews scan logs daily.
**Tony (IT Admin)** — Investigates camera-level issues.

### Background
EP0011 introduced multi-camera scanning on the scanner device (up to 8 cameras). US0090 extends the scanner's scan submission payload with `cameraIndex` and `cameraName`. This story is the WebApp counterpart: extend the `Scan` entity + ingestion API to accept and persist those fields, and surface them in the Scan Logs UI.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| TRD | Compatibility | `/api/v1/scans` must remain backward-compatible for legacy scanner installs | New fields are nullable |
| EP0011 | Data | Scanner sends `cameraIndex` (int) and `cameraName` (string) | Server accepts and persists both |
| PRD | Audit | Scan records are retained per EP0017 retention window | Camera columns included in retention + export paths |

---

## Acceptance Criteria

### AC1: Scan Entity Extended
- **Given** the `Scan` entity
- **Then** two new nullable columns exist: `CameraIndex` (int?) and `CameraName` (nvarchar(100)?)
- **And** an EF Core migration adds them with no data loss

### AC2: API Accepts New Fields
- **Given** a scanner submits `POST /api/v1/scans` with `cameraIndex` and `cameraName` in the JSON body
- **Then** both fields are persisted on the `Scan` row
- **And** the existing response shape is unchanged

### AC3: Backward Compatibility
- **Given** an older scanner submits a scan WITHOUT `cameraIndex`/`cameraName`
- **Then** the scan is still accepted
- **And** `CameraIndex` and `CameraName` are stored as NULL
- **And** no validation errors are returned

### AC4: Validation
- **Given** `cameraIndex` is provided
- **Then** it must be an integer in `[1, 8]` (inclusive) or `null`
- **Given** `cameraName` is provided
- **Then** it is truncated at 100 characters if longer

### AC5: Scan Logs UI — Camera Column
- **Given** I view the Scan Logs page (Attendance log / scan history)
- **Then** the table includes a "Camera" column positioned between "Device" and "Scan Type"
- **And** the cell shows `{CameraIndex} · {CameraName}` (e.g. "1 · Main Gate Left")
- **And** for legacy rows with null fields, the cell shows "—"

### AC6: Scan Logs UI — Filter by Camera
- **Given** the Scan Logs filter panel
- **Then** a "Camera" filter is available
- **And** it lists distinct `(CameraIndex, CameraName)` combos seen for the selected Device
- **And** applying the filter narrows the log to the chosen camera

### AC7: CSV Export Includes Camera
- **Given** I export Scan Logs (or any attendance report that includes scan rows)
- **Then** the CSV includes `CameraIndex` and `CameraName` columns

### AC8: Visitor Scans Also Carry Camera Identity
- **Given** the `VisitorScan` entity
- **Then** it also receives the same two nullable columns
- **And** visitor scan ingestion handles camera identity the same way
- **And** the Visitor Scan log UI displays them (if that view exists)

---

## Scope

### In Scope
- `Scan` + `VisitorScan` entity changes + migration
- `ScansApiController` DTO + persistence changes
- Scan Logs page column + filter
- CSV/report export includes camera fields
- Attendance dashboard: no change to counts, only per-scan detail

### Out of Scope
- Scanner-side payload changes (US0090)
- Per-camera analytics / heatmap
- Reorganising Scan Logs beyond adding the new column + filter

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| `cameraIndex` out of range (e.g. 99) | Return 400 with clear error; scan not persisted |
| `cameraName` over 100 chars | Truncated silently; logged as warning |
| Scanner provides `cameraIndex` but null `cameraName` | Accept; store index, `cameraName = null`; UI shows "Camera 1 · —" |
| Scanner provides `cameraName` but null `cameraIndex` | Accept; store name, `cameraIndex = null`; UI shows "— · {name}" |
| Legacy row with both null | UI shows "—"; filter shows "(unknown)" option |

---

## Test Scenarios

- [ ] Migration adds two columns without data loss
- [ ] API accepts payload with new fields and persists them
- [ ] API accepts payload without new fields (backward compat)
- [ ] Validation rejects out-of-range index
- [ ] Long name truncated at 100 chars
- [ ] Scan Logs table shows "Camera" column with index + name
- [ ] Camera filter narrows results correctly
- [ ] CSV export includes CameraIndex and CameraName columns
- [ ] Visitor Scan path also persists + surfaces fields

---

## Technical Notes

### Data Model
```csharp
public class Scan {
    // existing fields...
    public int? CameraIndex { get; set; }
    public string? CameraName { get; set; }  // max 100 chars
}

public class VisitorScan {
    // existing fields...
    public int? CameraIndex { get; set; }
    public string? CameraName { get; set; }
}
```

### Files to Modify
- **Modify:** `src/SmartLog.Web/Data/Entities/Scan.cs`
- **Modify:** `src/SmartLog.Web/Data/Entities/VisitorScan.cs`
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` — field config (max length)
- **New migration:** `AddCameraIdentityToScans`
- **Modify:** `src/SmartLog.Web/Controllers/Api/ScansApiController.cs` — DTO + validation + persistence
- **Modify:** `src/SmartLog.Web/Services/VisitorPassService.cs` — persist for visitor scans
- **Modify:** `src/SmartLog.Web/Pages/Admin/ScanLogs/Index.cshtml(.cs)` — column + filter
- **Modify:** `src/SmartLog.Web/Services/ScanExportService.cs` (or equivalent) — CSV columns
- **Modify:** `src/SmartLog.Web/Services/AttendanceReportService.cs` (if it includes per-scan detail) — include new fields

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| US0090 (Scanner) | Paired | Scanner sends camera identity | Draft |
| [US0030](US0030-scan-ingestion-api.md) | Foundation | Scan ingestion API exists | Done |
| [US0032](US0032-duplicate-detection.md) | Foundation | Dedup logic (unaffected by new fields) | Done |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — entity + migration + API + UI + export; small surface per file but several files

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 planning session |
