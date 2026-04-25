# PL0010: SMS Settings Restructure — Implementation Plan

> **Status:** Done
> **Story:** [US0083: SMS Settings Restructure — Alert Toggle, Global Guard & Default Provider](../stories/US0083-sms-settings-restructure.md)
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Created:** 2026-04-19
> **Language:** C# / ASP.NET Core 8.0 Razor Pages

## Overview

Three interconnected changes to the SMS settings architecture:
1. **No-Scan Alert toggle** — independent enable/disable (`Sms:NoScanAlertEnabled`) so the alert can be turned off without disabling all SMS
2. **Global SMS clarity** — rename/reposition the existing `Sms:Enabled` as the master kill switch with clear description
3. **Default broadcast provider in Settings** — move the per-broadcast "Send via" dropdown to Settings; remove it from Announcement, Emergency, BulkSend pages
4. **No-Scan Alert provider** — dedicated provider setting (`Sms:NoScanAlertProvider`) for the alert service

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Alert Enable/Disable | New `Sms:NoScanAlertEnabled` toggle on Settings page |
| AC2 | Alert Independent | Alert toggle works independently of global SMS for broadcasts |
| AC3 | Global SMS Clarity | `Sms:Enabled` clearly labeled as master kill switch; warning banner on broadcast pages when off |
| AC4 | Default Broadcast Provider | `DefaultProvider` dropdown stays in Settings (already there); becomes the sole provider config for broadcasts |
| AC5 | Remove Per-Broadcast Dropdown | Remove "Send via" from Announcement, Emergency, BulkSend pages |
| AC6 | Alert Provider Setting | Dedicated `Sms:NoScanAlertProvider` dropdown in No-Scan Alert section |
| AC7 | Dashboard Reflects Alert Toggle | Next Run shows "Disabled" when alert toggle is off |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 Razor Pages
- **Test Framework:** xUnit + Moq (SmartLog.Web.Tests)

### Existing Patterns
- **Settings page:** `Settings.cshtml.cs` loads from `ISmsSettingsService` + `IAppSettingsService`, saves on POST. Current sections: General, GSM Modem, Semaphore, Queue, No-Scan Alert.
- **Broadcast provider:** `PreferredProvider` is a `[BindProperty]` on Announcement/Emergency/BulkSend pages. Passed to `SmsService.QueueAnnouncementAsync()` which sets `SmsQueue.Provider`. When null, `SmsWorkerService` uses the default.
- **NoScanAlertService:** Checks `IsSmsEnabledAsync()` (global guard) before running. Queues `SmsQueue` entries with no `Provider` set (uses worker default).
- **Dashboard `ComputeNextRunDisplay`:** Static method taking `(bool isSmsEnabled, bool ranToday, string alertTimeStr)` — needs a new `bool isAlertEnabled` parameter.
- **AppSettings vs SmsSettings:** `AppSettings` table for app-wide config (key: `Sms:NoScanAlertTime`), `SmsSettings` table for SMS-specific config (key: `Sms.Enabled`). New alert keys go in `AppSettings` (category "Sms").

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Changes are primarily UI reorganization and config wiring. No complex algorithms. Test the guard logic in NoScanAlertService and the updated ComputeNextRunDisplay after implementation.

---

## Implementation Phases

### Phase 1: Seed New AppSettings Keys
**Goal:** Add `Sms:NoScanAlertEnabled` and `Sms:NoScanAlertProvider` to DbInitializer

- [ ] Add to `DbInitializer.cs` seed data:
  - `Sms:NoScanAlertEnabled` = `"true"`, category `"Sms"`, description "Enable/disable the end-of-day no-scan alert"
  - `Sms:NoScanAlertProvider` = `"SEMAPHORE"`, category `"Sms"`, description "SMS provider for no-scan alerts"

### Phase 2: NoScanAlertService — Alert Toggle + Provider
**Goal:** Check `Sms:NoScanAlertEnabled` and set provider on queued messages

- [ ] In `RunAlertCoreAsync()`, after the global SMS guard, add:
  ```csharp
  var appSettings = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
  var alertEnabled = await appSettings.GetAsync("Sms:NoScanAlertEnabled");
  if (alertEnabled != null && alertEnabled.Equals("false", StringComparison.OrdinalIgnoreCase))
  {
      _logger.LogInformation("No-scan alert skipped: alert is disabled");
      return 0;
  }
  ```
- [ ] Read `Sms:NoScanAlertProvider` and set on each queued `SmsQueue` entry:
  ```csharp
  var alertProvider = await appSettings.GetAsync("Sms:NoScanAlertProvider");
  // In the foreach loop, when creating SmsQueue:
  Provider = string.IsNullOrEmpty(alertProvider) ? null : alertProvider,
  ```

### Phase 3: Settings Page — Restructure Sections
**Goal:** Reorganize Settings UI into clear sections with new fields

- [ ] Add properties to `Settings.cshtml.cs`:
  - `[BindProperty] public bool NoScanAlertEnabled { get; set; } = true;`
  - `[BindProperty] public string NoScanAlertProvider { get; set; } = "SEMAPHORE";`
- [ ] In `OnGetAsync()`:
  - Load `NoScanAlertEnabled` from `AppSettings["Sms:NoScanAlertEnabled"]` (default true)
  - Load `NoScanAlertProvider` from `AppSettings["Sms:NoScanAlertProvider"]` (default "SEMAPHORE")
- [ ] In `OnPostAsync()`:
  - Save `NoScanAlertEnabled` to AppSettings
  - Save `NoScanAlertProvider` to AppSettings
- [ ] Restructure `Settings.cshtml` sections:
  1. **Global SMS** — `Sms:Enabled` toggle with master switch description, fallback toggle
  2. **Default Broadcast Provider** — `DefaultProvider` dropdown (already exists, just relabel)
  3. **No-Scan Alert** — Enable toggle, Alert Time, Alert Provider dropdown
  4. **GSM Modem Settings** — unchanged
  5. **Semaphore Cloud Settings** — unchanged
  6. **Queue Settings** — unchanged

### Phase 4: Remove Per-Broadcast Provider Dropdown
**Goal:** Remove "Send via" from broadcast pages; use default provider

- [ ] **Announcement.cshtml.cs:** Remove `PreferredProvider` BindProperty. In `OnPostAsync()`, read default provider from `ISmsSettingsService.GetSettingAsync("Sms.DefaultProvider")` and pass to `QueueAnnouncementAsync()`.
- [ ] **Announcement.cshtml:** Remove the "Send via" `<select>` block (lines ~64-72).
- [ ] **Emergency.cshtml.cs:** Same — remove `PreferredProvider`, read from settings service.
- [ ] **Emergency.cshtml:** Remove the "Send via" `<select>` block.
- [ ] **BulkSend.cshtml.cs:** Same — remove `PreferredProvider`, read from settings service.
- [ ] **BulkSend.cshtml:** Remove the "Send via" `<select>` block.
- [ ] Add `ISmsSettingsService` injection to all three page models (if not already injected).
- [ ] Add warning banner to all three pages when global SMS is off:
  ```html
  @if (!Model.IsSmsEnabled)
  {
      <div class="alert alert-warning">...</div>
  }
  ```
  Add `IsSmsEnabled` property loaded in `OnGetAsync()`.

### Phase 5: Dashboard — Update Next Run for Alert Toggle
**Goal:** ComputeNextRunDisplay accounts for alert-specific toggle

- [ ] Update `ComputeNextRunDisplay` signature: add `bool isAlertEnabled` parameter
- [ ] Logic: if `!isSmsEnabled || !isAlertEnabled` → "Disabled"
- [ ] In `OnGetAsync()`: load `isAlertEnabled` from `AppSettings["Sms:NoScanAlertEnabled"]`
- [ ] Update card header badge: if alert is disabled (but global SMS is on), show "Disabled" badge instead of "Not Run Today"
- [ ] Update existing tests in `SmsIndexNextRunTests.cs` for new parameter

### Phase 6: Testing
**Goal:** Verify guard logic and display

- [ ] Update `SmsIndexNextRunTests`:
  - Add `isAlertEnabled` parameter to all existing test calls
  - Add test: SMS enabled + alert disabled → "Disabled"
  - Add test: SMS disabled + alert enabled → "Disabled" (global takes precedence)
  - Add test: SMS enabled + alert enabled → shows time
- [ ] Add `NoScanAlertServiceTests` (if not existing) or extend:
  - Test: alert disabled → returns 0, logs skip
  - Test: global SMS disabled → returns 0 (existing behavior)
  - Test: both enabled → proceeds normally
  - Test: queued messages have provider set from `Sms:NoScanAlertProvider`

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | `Sms:NoScanAlertEnabled` missing from DB | Default to `true` (enabled) — backward compatible | Phase 2 |
| 2 | Global SMS off + Alert toggle on | Alert does not run (global guard checked first) | Phase 2 |
| 3 | Global SMS on + Alert toggle off | Alert does not run, but broadcasts work | Phase 2 |
| 4 | Admin toggles alert off mid-day after it already ran | No effect until next day | Phase 2 |
| 5 | Default provider changed while messages are in queue | Queued messages already have provider set; only future messages affected | Phase 4 |
| 6 | Broadcast page loaded when global SMS is off | Warning banner shown; form still submits (messages queue) | Phase 4 |
| 7 | `Sms:NoScanAlertProvider` missing from DB | Default to `"SEMAPHORE"` | Phase 2 |

**Coverage:** 7/7 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing broadcast flow | Medium | Broadcast pages still call same `QueueAnnouncementAsync` — only change is where provider value comes from |
| NoScanAlertService regression | Medium | Existing guard logic unchanged; new guard added after existing one |
| Settings page visual regression | Low | Section reordering only; no field removals except "Send via" on broadcast pages |
| Existing test breakage (ComputeNextRunDisplay) | Low | Signature change is compile-time caught; update all callers |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] No-Scan Alert can be independently enabled/disabled
- [ ] Global SMS disables everything
- [ ] Settings page has clear sections
- [ ] "Send via" removed from all broadcast pages
- [ ] No-Scan Alert provider setting works
- [ ] Dashboard Next Run reflects alert toggle
- [ ] Warning banner on broadcast pages when SMS is off
- [ ] Tests passing
- [ ] Build succeeds
