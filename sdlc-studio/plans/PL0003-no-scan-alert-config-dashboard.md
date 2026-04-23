# PL0003: No-Scan Alert Admin Configuration & Dashboard — Implementation Plan

> **Status:** Complete
> **Story:** [US0053: No-Scan Alert Admin Configuration & Dashboard](../stories/US0053-no-scan-alert-config-dashboard.md)
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Approach:** Test-After
> **Created:** 2026-04-16

---

## Overview

Add a "No-Scan Alert Time" field to the SMS Settings page and a "No-Scan Alert" status card to the SMS Dashboard. Both read/write AppSettings key `Sms:NoScanAlertTime`. The dashboard reads AuditLog for last run status (NO_SCAN_ALERT_EXECUTED / NO_SCAN_ALERT_SUPPRESSED).

AC6 (seeded AppSettings key) is already satisfied by US0052 / `DbInitializer.SeedAppSettingsAsync`.

---

## Files to Modify

| File | Change |
|------|--------|
| `src/SmartLog.Web/Pages/Admin/Sms/Settings.cshtml.cs` | Inject `IAppSettingsService`; add `NoScanAlertTime` bind property; load + save; validate HH:mm |
| `src/SmartLog.Web/Pages/Admin/Sms/Settings.cshtml` | Add "No-Scan Alert" section with `<input type="time">` |
| `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml.cs` | Inject `IAppSettingsService`; query last AuditLog run entry; populate `NoScanAlertStatus` |
| `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml` | Add No-Scan Alert card (3 states: success/warning/gray) |
| `tests/SmartLog.Web.Tests/Pages/NoScanAlertConfigTests.cs` | New test file |

---

## Edge Case Handling Plan

| # | Edge Case | Handling Strategy | Phase |
|---|-----------|-------------------|-------|
| 1 | Settings page loads before AppSettings seeded | `GetAsync` returns null → default "18:10" displayed | Phase 1 |
| 2 | Multiple admins save simultaneously | EF upsert (SetAsync) — last write wins | Phase 1 |
| 3 | Alert time set to school hours (e.g., "10:00") | Only validate HH:mm format; do not restrict range | Phase 1 |
| 4 | Invalid time format submitted | `TimeOnly.TryParse` check → model error | Phase 1 |
| 5 | Dashboard loads during alert processing | Query AuditLog for last *completed* entry only | Phase 2 |
| 6 | AppSettings table empty | `GetAsync` default → show "18:10" and "Never" | Phase 2 |

---

## Phase 1: Settings Page — NoScanAlertTime Field

### Settings.cshtml.cs changes

1. Inject `IAppSettingsService _appSettingsService` (add to constructor)
2. Add bind property: `[BindProperty] public string NoScanAlertTime { get; set; } = "18:10";`
3. `OnGetAsync`: load `NoScanAlertTime = await _appSettingsService.GetAsync("Sms:NoScanAlertTime") ?? "18:10";`
4. `OnPostAsync` validation block (before saving):
   ```csharp
   if (!TimeOnly.TryParse(NoScanAlertTime, out _))
   {
       ErrorMessage = "Please enter a valid time in HH:mm format";
       return Page();
   }
   ```
5. Save: `await _appSettingsService.SetAsync("Sms:NoScanAlertTime", NoScanAlertTime, "Sms", null);`

### Settings.cshtml changes

Add new card section "No-Scan Alert Settings" (between Queue Settings and Save button):
```html
<div class="card mb-4">
    <div class="card-header">
        <i class="bi bi-bell me-2"></i>No-Scan Alert Settings
    </div>
    <div class="card-body">
        <div class="col-md-4 mb-3">
            <label for="noScanAlertTime" class="form-label">Alert Time</label>
            <input type="time" class="form-control" id="noScanAlertTime"
                   name="NoScanAlertTime" value="@Model.NoScanAlertTime" />
            <div class="form-text">Time to send end-of-day no-scan alerts (24-hour local time)</div>
        </div>
    </div>
</div>
```

---

## Phase 2: Dashboard — No-Scan Alert Card

### IndexModel changes

1. Inject `IAppSettingsService _appSettingsService` (already need to pass `ApplicationDbContext`)  
   **Note:** `ApplicationDbContext _context` already injected.
2. Add model property:
   ```csharp
   public NoScanAlertRunStatus NoScanAlert { get; set; } = new();
   ```
3. In `OnGetAsync`, query last AuditLog:
   ```csharp
   var lastRun = await _context.AuditLogs
       .Where(a => a.Action == "NO_SCAN_ALERT_EXECUTED" || a.Action == "NO_SCAN_ALERT_SUPPRESSED")
       .OrderByDescending(a => a.Timestamp)
       .FirstOrDefaultAsync();
   NoScanAlert = NoScanAlertRunStatus.FromAuditLog(lastRun);
   ```
4. Add inner record `NoScanAlertRunStatus` at bottom of `Index.cshtml.cs`:
   ```csharp
   public record NoScanAlertRunStatus
   {
       public bool HasRun { get; init; }
       public bool WasSuppressed { get; init; }
       public DateTime? RunAt { get; init; }
       public int AlertsQueued { get; init; }

       public static NoScanAlertRunStatus FromAuditLog(AuditLog? log)
       {
           if (log == null) return new NoScanAlertRunStatus();
           var suppressed = log.Action == "NO_SCAN_ALERT_SUPPRESSED";
           // Parse "Alerts queued: 45" from Details
           var count = 0;
           if (!suppressed && log.Details != null)
           {
               var match = System.Text.RegularExpressions.Regex.Match(
                   log.Details, @"Alerts queued: (\d+)");
               if (match.Success) int.TryParse(match.Groups[1].Value, out count);
           }
           return new NoScanAlertRunStatus
           {
               HasRun = true,
               WasSuppressed = suppressed,
               RunAt = log.Timestamp,
               AlertsQueued = count
           };
       }
   }
   ```

### Index.cshtml changes

Add No-Scan Alert card in the row after Gateway Health Status (new col-md-12 row or add as 3rd col):
```html
<div class="row mb-4">
    <div class="col-md-12">
        <div class="card">
            <div class="card-header">
                <i class="bi bi-bell me-2"></i>No-Scan Alert
            </div>
            <div class="card-body">
                @if (!Model.NoScanAlert.HasRun)
                {
                    <span class="badge bg-secondary me-2">No Data</span>
                    <span class="text-muted">Last Run: Never</span>
                }
                else if (Model.NoScanAlert.WasSuppressed)
                {
                    <span class="badge bg-warning text-dark me-2">Suppressed</span>
                    <span>Last Run: @Model.NoScanAlert.RunAt!.Value.ToLocalTime().ToString("MMMM d, yyyy h:mm tt")
                        — Suppressed (no scanner activity)</span>
                }
                else
                {
                    <span class="badge bg-success me-2">Success</span>
                    <span>Last Run: @Model.NoScanAlert.RunAt!.Value.ToLocalTime().ToString("MMMM d, yyyy h:mm tt")</span>
                    <span class="ms-3 text-muted">Alerts Sent: <strong>@Model.NoScanAlert.AlertsQueued</strong></span>
                }
            </div>
        </div>
    </div>
</div>
```

---

## Phase 3: Tests

New file: `tests/SmartLog.Web.Tests/Pages/NoScanAlertConfigTests.cs`

Tests:
1. `Settings_LoadsNoScanAlertTime_FromAppSettings` — Default "18:10" when key absent
2. `Settings_LoadsNoScanAlertTime_WhenKeyExists` — Custom value loaded
3. `Settings_InvalidTimeFormat_ReturnsError` — "25:00" → error message set
4. `Settings_ValidTime_SavedToAppSettings` — "17:30" saved correctly
5. `Dashboard_NoRunYet_ShowsNever` — `FromAuditLog(null)` → `HasRun=false`
6. `Dashboard_LastRunExecuted_ShowsCountAndTime` — EXECUTED log parsed correctly
7. `Dashboard_LastRunSuppressed_ShowsSuppressed` — SUPPRESSED log → `WasSuppressed=true`
8. `Dashboard_AleretsQueuedParsedFromDetails` — "Alerts queued: 45" → `AlertsQueued=45`

---

## Test Strategy Decision

**Test-After.** This story is pure UI/page model — thin logic (HH:mm validation, AuditLog parsing). The `NoScanAlertRunStatus.FromAuditLog` static method is the only non-trivial logic and is straightforward to unit test directly. No complex algorithms that benefit from TDD.

---

## Acceptance Criteria Coverage

| AC | Implementation |
|----|---------------|
| AC1: Alert Time Config field | `Settings.cshtml` time picker + `SettingsModel.NoScanAlertTime` load/save |
| AC2: Time Validation | `TimeOnly.TryParse` in `OnPostAsync` |
| AC3: Dashboard last run (success) | `NoScanAlertRunStatus.FromAuditLog` + EXECUTED card |
| AC4: Dashboard "never run" | `HasRun=false` → "Last Run: Never" card |
| AC5: Dashboard suppressed | SUPPRESSED log → warning badge |
| AC6: AppSettings seeded | Already done in US0052 (DbInitializer.SeedAppSettingsAsync) |
