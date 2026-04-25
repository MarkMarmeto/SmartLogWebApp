# PL0017: Scan Retention Handler — Implementation Plan

> **Status:** Draft
> **Story:** [US0098: Scan Retention Handler](../stories/US0098-scan-retention-handler.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Implement `ScanRetentionHandler` for the `Scan` table (student attendance scans). The `Scan` table is the largest in the system (~760K–2.3M rows/year). The handler uses `ScannedAt` (not `ReceivedAt`) as the eligibility timestamp, runs in 1,000-row batches, and integrates the archive hook.

The unique addition in this story: **report boundary warning**. Attendance Report pages must detect when a requested date range predates the retention horizon and show a warning banner to prevent silent incomplete results.

**Pre-existing state:**
- `IEntityRetentionHandler` + base types defined (PL0014).
- `Scan` entity has `ScannedAt` (DateTimeOffset or DateTime — check entity). Handler uses `ScannedAt`.
- Attendance report pages: `Pages/Admin/Reports/Daily.cshtml(.cs)`, `Weekly.cshtml(.cs)`, `Monthly.cshtml(.cs)`, `StudentReport.cshtml(.cs)`. Each accepts a date/range parameter.
- Archive hook stub pattern established in PL0015.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Handler registered | `EntityName = "Scan"` |
| AC2 | Eligibility | `ScannedAt < UtcNow - N days` |
| AC3 | VisitorScan separate | This handler is `Scan` only; `VisitorScan` has its own handler (US0100/PL0019) |
| AC4 | Batch delete + dry run + archive + logging | Standard pattern |
| AC5 | Report boundary warning | Banner shown when report date range extends beyond retention horizon |
| AC6 | Archive hint on Retention page | Retention page shows "Archiving recommended" hint when `ArchiveEnabled = false` for Scan row |

---

## Technical Context

### ScannedAt field type
`Scan.ScannedAt` is likely `DateTimeOffset` (check entity). SQL cutoff: `CAST(GETUTCDATE() AS DATETIMEOFFSET) - {retentionDays} days`. EF interpolation handles type mapping.

### Report Boundary Warning

The warning banner logic:

```csharp
// In each report page model's OnGetAsync, after loading retention policy:
var scanPolicy = await _db.RetentionPolicies
    .AsNoTracking()
    .SingleOrDefaultAsync(p => p.EntityName == "Scan");
var retentionHorizon = scanPolicy != null && scanPolicy.Enabled
    ? DateTime.UtcNow.AddDays(-scanPolicy.RetentionDays)
    : (DateTime?)null;

ViewData["ScanRetentionHorizon"] = retentionHorizon;
// In the page: if StartDate < retentionHorizon → show banner
```

Banner XAML pattern (Razor):
```html
@if (Model.RetentionHorizon.HasValue && Model.StartDate < Model.RetentionHorizon.Value) {
    <div class="alert alert-warning">
        Data before @Model.RetentionHorizon.Value.ToString("yyyy-MM-dd") may have been purged per 
        <a asp-page="/Admin/Settings/Retention">retention policy</a>. Results may be incomplete.
    </div>
}
```

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Standard delete pattern + UI banner. Tests cover delete, dry run, and report boundary detection.

---

## Implementation Phases

### Phase 1: ScanRetentionHandler

**Goal:** Implement handler using `ScannedAt` cutoff.

- [ ] Create `src/SmartLog.Web/Services/Retention/ScanRetentionHandler.cs`.
- [ ] `EntityName => "Scan"`.
- [ ] Batch SQL:
  ```sql
  DELETE TOP (1000) FROM Scans WHERE ScannedAt < {cutoff}
  ```
  > Verify table name (`Scans` vs `Scan`) from `ApplicationDbContext`.
- [ ] Same single-flight lock, run logging, policy guard, archive hook stub as PL0014/PL0015.
- [ ] `PreviewAsync`: `COUNT(*)`, `MIN(ScannedAt)`, `MAX(ScannedAt)` on eligible set.

**Files:** `Services/Retention/ScanRetentionHandler.cs`

### Phase 2: DI Registration

- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IEntityRetentionHandler, ScanRetentionHandler>();
  ```

### Phase 3: Report Boundary Warning

**Goal:** Each attendance report page shows a warning banner when the requested range predates the retention horizon.

- [ ] Create a shared helper or base class method `LoadScanRetentionHorizonAsync()` that reads the Scan policy and returns `DateTime? horizon`.
- [ ] Add `DateTime? ScanRetentionHorizon` property to each report page model.
- [ ] Call the helper in `OnGetAsync` of each report page:
  - `Pages/Admin/Reports/Daily.cshtml.cs`
  - `Pages/Admin/Reports/Weekly.cshtml.cs`
  - `Pages/Admin/Reports/Monthly.cshtml.cs`
  - `Pages/Admin/Reports/StudentReport.cshtml.cs` (if it exists)
- [ ] Add the warning banner HTML to each report page's `.cshtml` (reuse a shared partial or inline the same pattern).
- [ ] The banner includes a link to `/Admin/Settings/Retention`.

**Files:** `Pages/Admin/Reports/Daily.cshtml(.cs)`, `Weekly.cshtml(.cs)`, `Monthly.cshtml(.cs)`, and any other report pages.

### Phase 4: Retention Page Archive Hint

**Goal:** Retention page shows "Archiving recommended" hint on the Scan row.

- [ ] In `Retention.cshtml`, for the Scan entity row, conditionally render:
  ```html
  @if (!policy.ArchiveEnabled) {
      <span class="badge bg-warning text-dark" title="Attendance records are referenced for enrolment audits">Archive recommended</span>
  }
  ```
  This is a display-only hint; no validation change.

**Files:** `Pages/Admin/Settings/Retention.cshtml`

### Phase 5: Tests

| AC | Test | File |
|----|------|------|
| AC2 | Scans older than window deleted; recent preserved | `ScanRetentionHandlerTests.cs` |
| AC4 | Dry run returns counts without deleting | same |
| AC5 | Report page model: `ScanRetentionHorizon` set; banner shown when start date < horizon | `ScanReportPageTests.cs` (new or extend) |
| AC5 | Banner not shown when start date is within horizon | same |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | `ScannedAt` is `DateTimeOffset` | Use `DateTimeOffset.UtcNow.AddDays(-N)` for cutoff comparison |
| 2 | Report requested for a date range that partially overlaps the horizon | Banner shown; partial results returned for the within-horizon portion |
| 3 | Scan retention policy not yet seeded (e.g. fresh DB before US0094 seeds) | `SingleOrDefaultAsync` returns null; horizon = null; no banner shown; no crash |
| 4 | Scan table very large — first run handles millions of rows | Loop runs until all eligible are purged; `RetentionRun` shows final count; admin monitors via log |

---

## Definition of Done

- [ ] `ScanRetentionHandler` using `ScannedAt` cutoff, batch delete, archive hook, run logging
- [ ] Report pages show retention boundary warning banner when date range exceeds horizon
- [ ] Retention page shows archive hint for Scan row when `ArchiveEnabled = false`
- [ ] Registered in DI
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
