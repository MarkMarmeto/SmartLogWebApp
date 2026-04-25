# PL0009: No-Scan Alert Next Run Label — Implementation Plan

> **Status:** Done
> **Story:** [US0082: No-Scan Alert Next Run Label](../stories/US0082-no-scan-alert-next-run-label.md)
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Created:** 2026-04-19
> **Language:** C# / ASP.NET Core 8.0 Razor Pages

## Overview

Add a "Next Run" label to the No-Scan Alert card on the SMS Dashboard (`/Admin/Sms/Index`). When SMS is globally enabled, show "Next Run: Today at {time}" or "Tomorrow at {time}" based on whether the alert has already run today. When SMS is disabled, show "Next Run: Disabled" and disable the Run Now / Re-run buttons.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Next Run — SMS Enabled | Show "Today at {time}" or "Tomorrow at {time}" based on ran-today state |
| AC2 | Next Run — SMS Disabled | Show "Disabled" with muted style; disable Run Now/Re-run buttons |
| AC3 | Respects Config | Next Run reflects the configured `Sms:NoScanAlertTime` value |
| AC4 | Non-School Day | Still shows scheduled time (service handles skip logic internally) |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 Razor Pages
- **Test Framework:** xUnit + Moq (SmartLog.Web.Tests)

### Existing Patterns
- **SMS Dashboard page model:** `IndexModel` in `Pages/Admin/Sms/Index.cshtml.cs` — already injects `INoScanAlertService`, loads `NoScanAlertRanToday` (bool) and `NoScanAlert` (last run status)
- **SMS enabled check:** `ISmsSettingsService.IsSmsEnabledAsync()` — reads from `SmsSettings` table, falls back to `appsettings.json` `Sms:Enabled`
- **Alert time config:** Stored in `AppSettings["Sms:NoScanAlertTime"]`, default "18:10", read via `IAppSettingsService.GetAsync()`
- **No-Scan Alert card:** Already renders last run status with badges and Run Now/Re-run buttons in `Index.cshtml` (lines 83–133)

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Changes are purely UI display logic in the page model (computing a display string) and Razor view (rendering it). No new services, no data model changes. The logic is simple enough to verify manually and add a few unit tests after.

---

## Implementation Phases

### Phase 1: Extend IndexModel with Next Run Properties
**Goal:** Add `NextRunDisplay`, `IsSmsEnabled`, and `AlertTime` to the page model

- [ ] Inject `ISmsSettingsService` into `IndexModel` constructor (already available via DI)
- [ ] Inject `IAppSettingsService` into `IndexModel` constructor
- [ ] Add properties:
  - `public bool IsSmsEnabled { get; set; }`
  - `public string NextRunDisplay { get; set; } = "";`
- [ ] In `OnGetAsync()`:
  1. `IsSmsEnabled = await _smsSettingsService.IsSmsEnabledAsync()`
  2. Read alert time: `var alertTimeStr = await _appSettingsService.GetAsync("Sms:NoScanAlertTime") ?? "18:10"`
  3. Parse with `TimeOnly.TryParse(alertTimeStr, out var alertTime)` — fallback to 18:10 on failure
  4. Compute display:
     - If `!IsSmsEnabled` → `NextRunDisplay = "Disabled"`
     - Else if `NoScanAlertRanToday` → `NextRunDisplay = $"Tomorrow at {alertTime.ToString("h:mm tt")}"`
     - Else → `NextRunDisplay = $"Today at {alertTime.ToString("h:mm tt")}"`

**Files:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml.cs`

### Phase 2: Update Razor View
**Goal:** Render Next Run label and conditionally disable buttons

- [ ] Inside the No-Scan Alert card body (`<div class="card-body py-2">`), add a "Next Run" line after the last-run display:
  - If `IsSmsEnabled`: `<span class="ms-3">Next Run: <strong>{NextRunDisplay}</strong></span>`
  - If `!IsSmsEnabled`: `<span class="ms-3 text-muted">Next Run: <strong>Disabled</strong></span>` with small text "(SMS sending is disabled in Settings)"
- [ ] Conditionally disable the Run Now / Re-run buttons when `!Model.IsSmsEnabled`:
  - Add `disabled` attribute to `<button>` elements
  - Add `title="SMS sending is disabled"` tooltip
  - Wrap in conditional: existing buttons render normally when enabled; add `disabled` + muted style when not

**Files:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml`

### Phase 3: Testing
**Goal:** Verify display logic

- [ ] Add tests in `SmartLog.Web.Tests` (new or existing test file):
  - Test: SMS enabled + not ran today → "Today at 6:10 PM"
  - Test: SMS enabled + ran today → "Tomorrow at 6:10 PM"
  - Test: SMS disabled → "Disabled"
  - Test: Custom alert time "17:30" → "Today at 5:30 PM"
  - Test: Invalid alert time falls back to "6:10 PM"

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Alert time setting missing from DB | `GetAsync` returns null → fall back to "18:10" | Phase 1 |
| 2 | Invalid alert time in DB (e.g., "99:99") | `TimeOnly.TryParse` fails → fall back to `new TimeOnly(18, 10)`, display as default | Phase 1 |
| 3 | SMS just disabled while viewing page | Next page load reads fresh `IsSmsEnabledAsync()` → shows "Disabled" | Phase 1 |
| 4 | Alert time is in the past today and alert hasn't run | Show "Today at {time}" — service may still trigger (no special handling needed) | Phase 1 |
| 5 | Midnight edge case (alert time "00:05") | `TimeOnly` handles this correctly; display shows "12:05 AM" via `h:mm tt` format | Phase 1 |

**Coverage:** 5/5 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| ISmsSettingsService not registered in DI | Low | Already registered — used by SmsWorkerService and SmsService |
| IAppSettingsService not injected in IndexModel | Low | Already used elsewhere; straightforward constructor injection |
| Time zone display mismatch | Low | Use same `ToLocalTime()` pattern as existing last-run display |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] "Next Run" label displays correctly for SMS enabled/disabled states
- [ ] Run Now/Re-run buttons disabled when SMS is off
- [ ] Fallback to default time on missing/invalid config
- [ ] Tests passing
- [ ] Build succeeds
