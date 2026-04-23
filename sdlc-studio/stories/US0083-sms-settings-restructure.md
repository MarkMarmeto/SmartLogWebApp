# US0083: SMS Settings Restructure — Alert Toggle, Global Guard & Default Provider

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-19

## User Story

**As a** Admin Amy (Administrator)
**I want** granular control over SMS features — a dedicated toggle to enable/disable No-Scan Alerts, a clear global SMS kill switch, and a default broadcast provider set once in Settings
**So that** I can manage SMS behavior from one place without repeating choices on every broadcast

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who configures school communications.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
Currently:
1. **No-Scan Alert** has no independent enable/disable — it only checks the global `Sms:Enabled` flag. Schools may want SMS broadcasting (announcements, emergency) active while keeping the automated no-scan alert off during transition periods.
2. **`Sms:Enabled`** (global) disables the SMS worker but its role as a "kill switch for everything SMS" is not explicit in the UI. It should be clearly labeled as the global guard.
3. **Broadcast provider** (`PreferredProvider`) is selected per-broadcast on Announcement, Emergency, and BulkSend pages. This is redundant — most schools use one provider consistently. Move this to Settings as a default, and remove the per-broadcast dropdown.

---

## Acceptance Criteria

### AC1: No-Scan Alert Enable/Disable Toggle
- **Given** I am on the SMS Settings page (`/Admin/Sms/Settings`)
- **Then** I see a "No-Scan Alert" section with:
  - An enable/disable toggle (default: enabled)
  - The existing "Alert Time" field (already present)
- **When** I disable the No-Scan Alert toggle and save
- **Then** `AppSettings["Sms:NoScanAlertEnabled"]` is set to `"false"`
- **And** the `NoScanAlertService` skips execution when this setting is `false`
- **And** the SMS Dashboard "Next Run" label shows "Disabled" (same as when global SMS is off)

### AC2: No-Scan Alert Independent of Global SMS
- **Given** global SMS is enabled (`Sms:Enabled = true`)
- **And** No-Scan Alert is disabled (`Sms:NoScanAlertEnabled = false`)
- **Then** the no-scan alert does not run
- **But** manual broadcasts (Announcement, Emergency, BulkSend) still work normally

### AC3: Global SMS Kill Switch — UI Clarity
- **Given** I am on the SMS Settings page
- **Then** the existing `Sms:Enabled` toggle is in a clearly labeled "Global SMS" section at the top
- **And** it has a description: "Master switch — when disabled, no SMS messages will be sent (broadcasts, alerts, notifications)"
- **When** I disable the global SMS toggle
- **Then** all SMS-related features are blocked:
  - SmsWorkerService stops processing the queue
  - NoScanAlertService skips (even if its own toggle is enabled)
  - Broadcast pages show a warning banner: "SMS sending is globally disabled. Messages will be queued but not sent."
  - Dashboard Run Now/Re-run buttons remain disabled (existing US0082 behavior)
  - Dashboard Next Run shows "Disabled"

### AC4: Default Broadcast Provider in Settings
- **Given** I am on the SMS Settings page
- **Then** I see a "Default Broadcast Provider" dropdown in the Gateway section (SEMAPHORE or GSM_MODEM)
- **And** this replaces the existing `DefaultProvider` setting (already present as `Sms:DefaultProvider`)
- **When** I select "SEMAPHORE" and save
- **Then** all broadcasts use SEMAPHORE by default

### AC5: Remove Per-Broadcast Provider Dropdown
- **Given** the default provider is set in Settings
- **When** I create an Announcement, Emergency, or BulkSend broadcast
- **Then** no "Send via" provider dropdown is shown on those pages
- **And** the broadcast uses the default provider from Settings
- **And** the broadcast pages read `DefaultProvider` from `ISmsSettingsService` instead of accepting it as a form field

### AC6: No-Scan Alert Provider Setting
- **Given** I am on the SMS Settings page
- **Then** the "No-Scan Alert" section includes a "Provider" dropdown (SEMAPHORE or GSM_MODEM)
- **And** this is stored as `AppSettings["Sms:NoScanAlertProvider"]`
- **When** I select "GSM_MODEM" and save
- **Then** the `NoScanAlertService` queues all NO_SCAN_ALERT messages with `Provider = "GSM_MODEM"`
- **And** this is independent of the default broadcast provider

### AC7: Dashboard Next Run Reflects Alert Toggle
- **Given** global SMS is enabled but No-Scan Alert is disabled
- **When** I view the SMS Dashboard
- **Then** "Next Run" shows "Disabled"
- **And** the No-Scan Alert card header badge shows "Disabled" instead of "Not Run Today"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| `Sms:NoScanAlertEnabled` missing from DB (fresh install) | Default to `true` (enabled) — backward compatible |
| Global SMS off + Alert toggle on | Alert does not run (global takes precedence) |
| Global SMS on + Alert toggle off | Alert does not run, but broadcasts work |
| Admin toggles alert off mid-day after it already ran | No effect until next day; today's run already completed |
| Default provider changed while messages are in queue | Queued messages already have provider set; new default applies to future messages only |
| Broadcast page loaded when global SMS is off | Show warning banner but allow form submission (messages queue for when SMS is re-enabled) |
| `Sms:NoScanAlertProvider` missing from DB | Default to `"SEMAPHORE"` — same as broadcast default |

---

## Test Scenarios

- [ ] No-Scan Alert toggle saves to `AppSettings["Sms:NoScanAlertEnabled"]`
- [ ] NoScanAlertService skips when alert toggle is disabled
- [ ] NoScanAlertService skips when global SMS is disabled (regardless of alert toggle)
- [ ] NoScanAlertService runs when both global SMS and alert toggle are enabled
- [ ] Dashboard Next Run shows "Disabled" when alert toggle is off
- [ ] Dashboard Next Run shows "Disabled" when global SMS is off
- [ ] Broadcast pages no longer show "Send via" dropdown
- [ ] Broadcasts use default provider from Settings
- [ ] Global SMS disable shows warning banner on broadcast pages
- [ ] No-Scan Alert provider setting saves to `AppSettings["Sms:NoScanAlertProvider"]`
- [ ] NoScanAlertService queues messages with the configured provider
- [ ] Settings page renders Global SMS, No-Scan Alert, and Gateway sections correctly

---

## Technical Notes

### New AppSettings Keys
- `Sms:NoScanAlertEnabled` — string `"true"`/`"false"`, default `"true"`, category `"Sms"`
- `Sms:NoScanAlertProvider` — string `"SEMAPHORE"` or `"GSM_MODEM"`, default `"SEMAPHORE"`, category `"Sms"`

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Settings.cshtml(.cs)` — Add `NoScanAlertEnabled` toggle; reorganize into sections (Global SMS, No-Scan Alert, Gateway, Queue)
- **Modify:** `src/SmartLog.Web/Services/NoScanAlertService.cs` — Check `Sms:NoScanAlertEnabled` before execution (after global SMS check); set `Provider` on queued messages from `Sms:NoScanAlertProvider`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml(.cs)` — Update `ComputeNextRunDisplay` to accept alert-enabled flag; update card header badge
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Announcement.cshtml(.cs)` — Remove `PreferredProvider` dropdown; read default from service
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Emergency.cshtml(.cs)` — Remove `PreferredProvider` dropdown; read default from service
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/BulkSend.cshtml(.cs)` — Remove `PreferredProvider` dropdown; read default from service
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs` — Seed `Sms:NoScanAlertEnabled` = `"true"`
- **Modify:** `tests/SmartLog.Web.Tests/Pages/SmsIndexNextRunTests.cs` — Update `ComputeNextRunDisplay` tests for new parameter

### Broadcast Pages — Warning Banner
When global SMS is disabled, add at the top of Announcement/Emergency/BulkSend pages:
```html
<div class="alert alert-warning">
    <i class="bi bi-exclamation-triangle me-2"></i>
    SMS sending is globally disabled. Messages will be queued but not sent until SMS is re-enabled in
    <a href="/Admin/Sms/Settings">Settings</a>.
</div>
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0052](US0052-no-scan-alert-service.md) | Functional | NoScanAlertService exists | Done |
| [US0053](US0053-no-scan-alert-config-dashboard.md) | Functional | Dashboard card and alert time config exist | Done |
| [US0055](US0055-per-broadcast-gateway.md) | Functional | Per-broadcast gateway exists (being replaced) | Done |
| [US0082](US0082-no-scan-alert-next-run-label.md) | Functional | Next Run label exists (being extended) | Done |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — touches multiple pages (Settings, Dashboard, 3 broadcast pages) but each change is straightforward

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-19 | Claude | Initial story created |
