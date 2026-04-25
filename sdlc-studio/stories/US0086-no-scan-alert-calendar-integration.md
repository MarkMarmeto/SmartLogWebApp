# US0086: No-Scan Alert — Calendar-Driven Auto-Disable & Event Prompt

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Admin Amy (Administrator)
**I want** the No-Scan Alert to automatically skip Holidays and Suspensions, and to prompt me when I create an Event whether the alert should still run that day
**So that** parents do not receive absence alerts on days when no scan was possible by design, without me having to remember to toggle the feature off.

## Context

### Persona Reference
**Admin Amy** — Creates Calendar entries for Holidays, Events, and Suspensions.

### Background
`NoScanAlertService` already calls `CalendarService.IsSchoolDayAsync()` to skip non-school days. However:
1. The current check is implicit — admins cannot tell from the Calendar UI which event types will silence the alert.
2. `CalendarEvent.AffectedGrades` is not applied; a partial-grade Suspension (e.g. Grade 11 only) currently silences the alert system-wide (or not at all — needs verification during implementation).
3. `Event` type (e.g. School Sports Day) is ambiguous: sometimes scans happen, sometimes not. Admin should decide per-event.

This story makes the calendar → alert interaction **explicit, per-grade, and admin-controlled for Events**.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| TRD | Data | `CalendarEvent` already has `EventType`, `AffectsAttendance`, `AffectedGrades` | Reuse existing fields; do not add new required columns |
| EP0009 | Correctness | No-Scan Alert must not fire on days scans are not expected | Holiday + Suspension must silence alert for affected grades |
| PRD | UX | Admin Amy should see the effect in the calendar UI, not guess it | Visual indicator on calendar entries when they suppress alert |

---

## Acceptance Criteria

### AC1: Holiday → Alert Auto-Disabled (System-Wide or Per-Grade)
- **Given** a `CalendarEvent` of type **Holiday** for today
- **When** the No-Scan Alert service runs
- **Then** the alert is suppressed for grade levels listed in `AffectedGrades` (or all grades if the field is empty → system-wide)
- **And** `RetentionRun` or equivalent log records "Skipped: Holiday — {EventName}"

### AC2: Suspension → Alert Auto-Disabled (Per-Grade)
- **Given** a `CalendarEvent` of type **Suspension** for today, `AffectedGrades = ["Grade 11"]`
- **When** the alert service runs
- **Then** students in Grade 11 are excluded from the no-scan query
- **And** students in other grades are still evaluated normally

### AC3: Event Type — Admin Prompt on Create
- **Given** I am creating/editing a `CalendarEvent` with `EventType = Event`
- **Then** the form shows a new toggle: **"Disable No-Scan Alert for this day"** (default: **off**)
- **When** I tick the toggle and save
- **Then** `CalendarEvent.SuppressesNoScanAlert = true` is persisted
- **And** the alert service treats this event the same as a Holiday for its `AffectedGrades`

### AC4: Holiday/Suspension — No Prompt, Auto-Applied
- **Given** I am creating/editing a Holiday or Suspension
- **Then** the suppress-alert toggle is **hidden** (behaviour is fixed: alert is always suppressed)
- **And** an inline helper text reads: "No-Scan Alert is automatically disabled for this day."

### AC5: Calendar UI Indicator
- **Given** I am viewing the Calendar list / month view
- **Then** events that suppress the no-scan alert (any Holiday, any Suspension, any Event with `SuppressesNoScanAlert = true`) show a small "Alert: Off" badge
- **And** events that do not suppress (plain Events) show no badge

### AC6: Alert Service Reads Calendar
- **Given** the alert service is running at the scheduled time
- **Then** before querying students it builds a set of "alert-suppressed grade levels for today" from calendar events
- **And** excludes students whose grade level is in that set from the no-scan query

### AC7: Dashboard Reflects Why Alert Skipped
- **Given** the alert ran and was suppressed for one or more grade levels (or system-wide)
- **When** I view the SMS Dashboard No-Scan Alert card
- **Then** the "Last Run" status shows "Skipped — {reason}" with the event name(s) (e.g. "Skipped — Holiday: Independence Day")

---

## Scope

### In Scope
- New `CalendarEvent.SuppressesNoScanAlert` column (nullable bool; defaults per event-type rule)
- Calendar create/edit UI: conditional toggle for Event type only
- Calendar list/month view: "Alert: Off" badge
- `NoScanAlertService` enhancement: per-grade suppression + suppression reason logging
- SMS Dashboard: display suppression reason in last-run status

### Out of Scope
- Partial-day suppression (e.g. only morning) — full-day granularity only
- Per-student suppression overrides
- Changing existing `AffectsAttendance` semantics (that flag still governs attendance-report exclusion; unrelated to alerts)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Multiple events same day (e.g. Holiday + Event) | Union of suppressed grade sets; if any is system-wide, full suppression |
| Event with empty `AffectedGrades` | Treated as system-wide |
| Legacy Events created before this story | `SuppressesNoScanAlert = null` → treated as `false` (no suppression) |
| Admin ticks the toggle on a Holiday somehow (through API) | Toggle is hidden in UI; backend always treats Holiday/Suspension as suppressing regardless of field value |
| Alert already ran today, then admin creates a Holiday for today | No re-trigger; next-day behaviour unaffected |

---

## Test Scenarios

- [ ] Holiday event suppresses alert system-wide when `AffectedGrades` empty
- [ ] Holiday with specific grades suppresses only those grades
- [ ] Suspension with `AffectedGrades = ["Grade 11"]` excludes only Grade 11 students
- [ ] Event with `SuppressesNoScanAlert = false` does not suppress
- [ ] Event with `SuppressesNoScanAlert = true` suppresses per its `AffectedGrades`
- [ ] Create form shows suppress toggle only when EventType = Event
- [ ] Calendar list shows "Alert: Off" badge for suppressing events
- [ ] Dashboard shows suppression reason in last-run status

---

## Technical Notes

### Data Model Change
```csharp
public class CalendarEvent {
    // ...existing fields...
    public bool? SuppressesNoScanAlert { get; set; }  // null / false → don't suppress; true → suppress per AffectedGrades
}
```
Effective suppression:
- `EventType == Holiday || EventType == Suspension` → always suppress
- `EventType == Event` → `SuppressesNoScanAlert == true` → suppress

### NoScanAlertService
```csharp
var suppressions = await _calendar.GetTodaysSuppressionsAsync();  // returns List<GradeLevel> (empty = system-wide)
if (suppressions.Any(s => s.IsSystemWide)) {
    await LogSkipAsync("System-wide suppression: " + reason);
    return;
}
var suppressedGrades = suppressions.SelectMany(s => s.GradeLevels).ToHashSet();
var students = await _students.GetWithNoScansAsync(today, excludeGrades: suppressedGrades);
```

### Files to Modify
- **Modify:** `src/SmartLog.Web/Data/Entities/CalendarEvent.cs`
- **New migration:** add `SuppressesNoScanAlert` column
- **Modify:** `src/SmartLog.Web/Pages/Admin/Calendar/*.cshtml(.cs)` — create/edit form, list view
- **Modify:** `src/SmartLog.Web/Services/CalendarService.cs` — new `GetTodaysSuppressionsAsync()`
- **Modify:** `src/SmartLog.Web/Services/Sms/NoScanAlertService.cs` — consume suppressions
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml(.cs)` — display suppression reason

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0052](US0052-no-scan-alert-service.md) | Functional | NoScanAlertService + CalendarService.IsSchoolDayAsync | Done |
| [US0082](US0082-no-scan-alert-next-run-label.md) | UI | Dashboard last-run label pattern | Done |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — calendar form + alert service + dashboard surface, small DB change

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 planning session |
