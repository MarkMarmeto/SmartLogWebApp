# PL0024: No-Scan Alert — Calendar-Driven Auto-Disable & Event Prompt

> **Status:** Complete
> **Story:** [US0086: No-Scan Alert — Calendar-Driven Auto-Disable & Event Prompt](../stories/US0086-no-scan-alert-calendar-integration.md)
> **Epic:** EP0009: SMS Strategy Overhaul
> **Created:** 2026-04-25
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Make the calendar → no-scan alert suppression explicit, per-grade, and admin-controlled for Event-type entries. Adds a `SuppressesNoScanAlert` column to `CalendarEvent`; Holidays and Suspensions suppress automatically; Event-type entries show a toggle on the create/edit form. The alert service gains per-grade suppression logic. The SMS Dashboard shows the suppression reason when the alert was skipped.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Holiday auto-suppression | Holiday event silences alert for its `AffectedGrades` (or all if empty) |
| AC2 | Suspension per-grade | Suspension with specific `AffectedGrades` excludes only those grades |
| AC3 | Event toggle | Event-type calendar form shows "Disable No-Scan Alert" toggle; persisted to `SuppressesNoScanAlert` |
| AC4 | Holiday/Suspension — no toggle | Toggle hidden; inline helper text "No-Scan Alert is automatically disabled for this day" |
| AC5 | Calendar UI badge | Suppressing events show "Alert: Off" badge in list/month view |
| AC6 | Alert service reads calendar | Builds suppressed-grade set before query; excludes affected students |
| AC7 | Dashboard reason | Last-run status shows "Skipped — {reason}" with event name |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
- **Architecture:** Razor Pages + EF Core 8.0 + SQL Server
- **Test Framework:** xUnit + Moq

### Key Existing Patterns
- **CalendarEvent entity:** `src/SmartLog.Web/Data/Entities/CalendarEvent.cs` — has `EventType`, `AffectsAttendance`, `AffectedGrades`; `AffectedGrades` likely stored as JSON or comma-separated string
- **NoScanAlertService:** `Services/Sms/NoScanAlertService.cs` — already calls `CalendarService.IsSchoolDayAsync()` before running the query
- **CalendarService:** `Services/CalendarService.cs` — provides school-day checks; add new `GetTodaysSuppressionsAsync()` method
- **SMS Dashboard:** `Pages/Admin/Sms/Index.cshtml(.cs)` — shows No-Scan Alert last-run status; extend with suppression reason

### Suppression Rules (from US0086)
```
EventType == Holiday || EventType == Suspension  →  always suppress (ignore field value)
EventType == Event && SuppressesNoScanAlert == true  →  suppress per AffectedGrades
EventType == Event && SuppressesNoScanAlert != true  →  no suppression
```

---

## Recommended Approach

**Strategy:** Test-After  
**Rationale:** Small DB change + service enhancement + UI badge. Tests cover suppression logic per event type and per-grade filtering.

---

## Implementation Phases

### Phase 1: Entity + Migration

**Goal:** Add `SuppressesNoScanAlert` to `CalendarEvent`.

- [ ] In `CalendarEvent.cs`, add:
  ```csharp
  public bool? SuppressesNoScanAlert { get; set; }  // null/false = no suppression; true = suppress per AffectedGrades
  ```
- [ ] In `ApplicationDbContext.OnModelCreating`, no special config needed (nullable bool).
- [ ] Run: `dotnet ef migrations add AddCalendarEventSuppressesNoScanAlert -p src/SmartLog.Web`
- [ ] Verify `Up()` adds nullable bool column; `Down()` drops it.

**Files:** `Data/Entities/CalendarEvent.cs`, `Migrations/{ts}_AddCalendarEventSuppressesNoScanAlert.cs`

### Phase 2: CalendarService — Suppression Query

**Goal:** New method returns today's suppressed grade sets.

- [ ] Add to `CalendarService.cs`:
  ```csharp
  // Returns list of suppression entries; entry with empty GradeLevels = system-wide
  public async Task<List<AlertSuppression>> GetTodaysSuppressionsAsync(DateOnly today) {
      var events = await _db.CalendarEvents
          .Where(e => e.Date == today &&
              (e.EventType == "Holiday" || e.EventType == "Suspension" ||
               (e.EventType == "Event" && e.SuppressesNoScanAlert == true)))
          .ToListAsync();
      return events.Select(e => new AlertSuppression {
          Reason = $"{e.EventType}: {e.Name}",
          GradeLevels = ParseGradeLevels(e.AffectedGrades),  // empty list = system-wide
      }).ToList();
  }
  ```
- [ ] Create `Models/Sms/AlertSuppression.cs`:
  ```csharp
  public class AlertSuppression {
      public string Reason { get; set; } = "";
      public List<string> GradeLevels { get; set; } = new();  // empty = system-wide
  }
  ```

**Files:** `Services/CalendarService.cs`, `Models/Sms/AlertSuppression.cs`

### Phase 3: NoScanAlertService — Per-Grade Suppression

**Goal:** Replace the boolean school-day check with per-grade suppression logic.

- [ ] In `NoScanAlertService.cs`:
  ```csharp
  var suppressions = await _calendarService.GetTodaysSuppressionsAsync(today);
  if (suppressions.Any()) {
      var isSystemWide = suppressions.Any(s => s.GradeLevels.Count == 0);
      if (isSystemWide) {
          var reason = string.Join(", ", suppressions.Select(s => s.Reason));
          await LogRunSkippedAsync(reason);
          return;
      }
      suppressedGrades = suppressions.SelectMany(s => s.GradeLevels).ToHashSet();
  }
  // Pass suppressedGrades as an exclude filter to the student no-scan query
  var students = await _studentService.GetStudentsWithNoScansAsync(today, excludeGrades: suppressedGrades);
  ```
- [ ] Retain the existing `IsSchoolDayAsync` check for non-calendar-event school days (weekends, etc.) — run it first; `GetTodaysSuppressionsAsync` is additive.
- [ ] `LogRunSkippedAsync`: write a `RetentionRun`-style log entry (or existing service log) with the skip reason; surface it on the dashboard.

**Files:** `Services/Sms/NoScanAlertService.cs`

### Phase 4: Calendar Create/Edit UI

**Goal:** Conditional toggle on Event-type entries; helper text on Holiday/Suspension.

- [ ] In `Pages/Admin/Calendar/Create.cshtml` and `Edit.cshtml`:
  - Add a `<div id="suppress-alert-section">`:
    - For `EventType == Event`: show toggle checkbox `asp-for="CalendarEvent.SuppressesNoScanAlert"` labelled "Disable No-Scan Alert for this day".
    - For Holiday/Suspension: hide toggle; show `<p class="form-text text-muted">No-Scan Alert is automatically disabled for this day.</p>`.
  - Use a JS listener on the `EventType` dropdown to show/hide the section dynamically.
- [ ] In page model `OnPostAsync`: for Holiday and Suspension, force `event.SuppressesNoScanAlert = null` (ignore any UI value).

**Files:** `Pages/Admin/Calendar/Create.cshtml(.cs)`, `Pages/Admin/Calendar/Edit.cshtml(.cs)`

### Phase 5: Calendar List — "Alert: Off" Badge

**Goal:** Visual indicator on suppressing events.

- [ ] In `Pages/Admin/Calendar/Index.cshtml`, add a badge column / inline badge in the event name cell:
  ```html
  @if (item.EventType == "Holiday" || item.EventType == "Suspension" ||
       (item.EventType == "Event" && item.SuppressesNoScanAlert == true))
  {
      <span class="badge bg-secondary ms-1">Alert: Off</span>
  }
  ```

**Files:** `Pages/Admin/Calendar/Index.cshtml`

### Phase 6: SMS Dashboard — Suppression Reason

**Goal:** Show suppression reason in the No-Scan Alert "Last Run" status card.

- [ ] Add `string? LastSkipReason` property to the no-scan alert run log (or an existing `NoScanAlertConfig` entity's last-run summary).
- [ ] In `Pages/Admin/Sms/Index.cshtml(.cs)`, display:
  ```html
  @if (Model.NoScanLastRunStatus == "Skipped") {
      <span class="text-muted">Skipped — @Model.NoScanSkipReason</span>
  }
  ```

**Files:** `Pages/Admin/Sms/Index.cshtml(.cs)`, `Services/Sms/NoScanAlertService.cs`

### Phase 7: Tests

| AC | Test | File |
|----|------|------|
| AC1 | Holiday with empty AffectedGrades → system-wide skip | `NoScanAlertServiceTests.cs` |
| AC2 | Suspension with Grade 11 → only Grade 11 excluded | same |
| AC3 | Event + SuppressesNoScanAlert=true → suppress per AffectedGrades | same |
| AC3 | Event + SuppressesNoScanAlert=false → no suppression | same |
| AC6 | Multiple events same day → union of suppressed grades | same |

- [ ] Run `dotnet test`; confirm zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Multiple events same day | Union of suppressed grade sets; if any system-wide, full skip |
| 2 | Event with empty `AffectedGrades` | Treated as system-wide suppression |
| 3 | Legacy Events (null `SuppressesNoScanAlert`) | Treated as false — no suppression |
| 4 | Admin creates Holiday via API with toggle set | Backend always overrides to null for Holiday/Suspension |
| 5 | Alert already ran today; holiday created retroactively | No re-trigger; idempotent daily guard unchanged |

---

## Definition of Done

- [ ] Migration adds `SuppressesNoScanAlert` column
- [ ] `GetTodaysSuppressionsAsync` returns correct suppression entries
- [ ] Alert service applies per-grade exclusions
- [ ] System-wide suppression logs skip reason and returns
- [ ] Calendar create/edit form shows conditional toggle
- [ ] Calendar list shows "Alert: Off" badge
- [ ] Dashboard shows suppression reason on skip
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-25 | Claude | Initial plan |
