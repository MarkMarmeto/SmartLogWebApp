# PL0023: Broadcast — Separate EN and FIL Message Inputs

> **Status:** Complete
> **Story:** [US0085: Broadcast — Separate EN and FIL Message Inputs](../stories/US0085-broadcast-per-language-message-inputs.md)
> **Epic:** EP0009: SMS Strategy Overhaul
> **Created:** 2026-04-25
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Replace the single free-text message body on all broadcast composer pages (Announcement, Emergency, BulkSend) with a language-mode selector ("English only" / "Filipino only" / "Both EN+FIL") and up to two message inputs. In dual-language mode, each targeted student receives the body matching their `SmsLanguage` preference wrapped in the matching language template. No DB schema changes required.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Language selector | Three options: EN only / FIL only / Both (default: Both) |
| AC2 | Single-language mode | One text area; only language-matched students targeted; skip-warning banner |
| AC3 | Dual-language mode | Two text areas (EN + FIL); both required; each student receives matching body+template |
| AC4 | Correct substitution | Final SMS text = language wrapper with correct `{Message}` body per student |
| AC5 | Per-language char count | Each text area shows character count + SMS segment estimate |
| AC6 | Preview | Preview panel shows both rendered messages before send |
| AC7 | Queue rows | Each `SmsQueue` row carries the fully-rendered, language-correct final SMS body |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
- **Architecture:** Razor Pages + EF Core 8.0
- **Test Framework:** xUnit + Moq

### Key Existing Patterns
- **Per-student language routing:** `Student.SmsLanguage` (`"EN"` or `"FIL"`); already used by entry/exit and no-scan alert services — reuse same logic
- **SMS templates:** `SmsTemplate` entity stores EN and FIL template bodies; template renderer substitutes `{Message}`, `{SchoolName}`, `{SchoolPhone}` — extend to accept a language-specific body
- **SmsQueue insert:** broadcast send handler iterates students and inserts a row per student; modify to use per-student `{Message}` body
- **Existing composer model:** each page has a `string Message` bind-property; replace with `BroadcastMessageBodies`

---

## Recommended Approach

**Strategy:** Test-After  
**Rationale:** No DB changes; logic is per-student routing into existing pipeline. Tests cover body-routing logic and final SMS text rendering.

---

## Implementation Phases

### Phase 1: Model

**Goal:** Define the bilingual message model and language mode enum.

- [ ] Create `src/SmartLog.Web/Models/Sms/BroadcastMessageBodies.cs`:
  ```csharp
  public enum BroadcastLanguageMode { EnglishOnly, FilipinoOnly, Both }

  public class BroadcastMessageBodies {
      public BroadcastLanguageMode Mode { get; set; } = BroadcastLanguageMode.Both;
      public string? EnglishBody { get; set; }
      public string? FilipinoBody { get; set; }
  }
  ```

**Files:** `Models/Sms/BroadcastMessageBodies.cs`

### Phase 2: Per-Language Body Resolution

**Goal:** Extend the broadcast send path to select the correct body per student.

- [ ] In `Services/Sms/BroadcastService.cs` (or the handler that iterates students and inserts `SmsQueue` rows), replace:
  ```csharp
  // OLD: single message body for all
  var finalSms = _templateRenderer.Render(template, message, student);
  ```
  with:
  ```csharp
  // NEW: per-student language routing
  var (template, body) = student.SmsLanguage switch {
      "FIL" when bodies.Mode is BroadcastLanguageMode.FilipinoOnly or BroadcastLanguageMode.Both
          => (filTemplate, bodies.FilipinoBody!),
      _ => (enTemplate, bodies.EnglishBody!)          // default EN; covers null SmsLanguage
  };
  var finalSms = _templateRenderer.Render(template, body, student);
  ```
- [ ] For `EnglishOnly` mode: skip students with `SmsLanguage == "FIL"` (count skipped for warning).
- [ ] For `FilipinoOnly` mode: skip students with `SmsLanguage == "EN"` (or null) — count skipped.
- [ ] Return `(int queued, int skipped)` from send method so the controller can show the warning banner.

**Files:** `Services/Sms/BroadcastService.cs`

### Phase 3: Server-Side Validation

**Goal:** Enforce required fields per mode in `OnPostAsync`.

- [ ] Add validation helper (inline in page model or shared):
  ```csharp
  private void ValidateMessageBodies(BroadcastMessageBodies bodies, int index = -1) {
      var prefix = index >= 0 ? $"[{index}]." : "";
      if (bodies.Mode is BroadcastLanguageMode.EnglishOnly or BroadcastLanguageMode.Both
          && string.IsNullOrWhiteSpace(bodies.EnglishBody))
          ModelState.AddModelError($"{prefix}EnglishBody", "English message is required.");
      if (bodies.Mode is BroadcastLanguageMode.FilipinoOnly or BroadcastLanguageMode.Both
          && string.IsNullOrWhiteSpace(bodies.FilipinoBody))
          ModelState.AddModelError($"{prefix}FilipinoBody", "Filipino message is required when 'Both' is selected.");
  }
  ```
- [ ] Call from `OnPostAsync` in each of the three broadcaster page models.

**Files:** `Pages/Admin/Sms/Announcement.cshtml.cs`, `Emergency.cshtml.cs`, `BulkSend.cshtml.cs`

### Phase 4: Composer UI

**Goal:** Replace single message text area with language selector + conditional inputs on all three pages.

- [ ] Extract the language composer section into a shared partial: `Pages/Admin/Sms/_MessageBodiesInput.cshtml`.
- [ ] Partial content:
  - `<select asp-for="MessageBodies.Mode">` with the three options.
  - EN text area: shown when Mode ≠ FilipinoOnly; labelled "English Message"; character count span.
  - FIL text area: shown when Mode ≠ EnglishOnly; labelled "Filipino Message"; character count span.
  - JS: on mode-select change, show/hide the relevant text areas; update combined segment estimate.
  - Preview panel: renders both filled-in bodies with the actual EN/FIL template wrappers; AJAX endpoint or client-side rendering.
  - Warning banner area: shown after POST when students were skipped due to language mode.
- [ ] Replace the existing `Message` text area on Announcement, Emergency, BulkSend with `<partial name="_MessageBodiesInput" for="MessageBodies" />`.
- [ ] Rename `[BindProperty] string Message` to `[BindProperty] BroadcastMessageBodies MessageBodies` on each page model.

**Files:** `Pages/Admin/Sms/_MessageBodiesInput.cshtml`, `Announcement.cshtml(.cs)`, `Emergency.cshtml(.cs)`, `BulkSend.cshtml(.cs)`

### Phase 5: Tests

| AC | Test | File |
|----|------|------|
| AC3 | EN-only mode queues only EN-preference students | `BroadcastServiceTests.cs` |
| AC3 | FIL-only mode queues only FIL-preference students | same |
| AC3 | Both mode routes each student to matching body+template | same |
| AC4 | Final SMS text matches expected string for EN and FIL example (AC4 data) | same |
| AC2 | Students without SmsLanguage default to EN routing | same |
| AC3 | Missing FIL body in Both mode fails validation | `BroadcastPageTests.cs` |

- [ ] Run `dotnet test`; confirm zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Student has null `SmsLanguage` | Treat as EN (matches existing alert behaviour) |
| 2 | EN-only mode targets FIL students | They are skipped; count returned and displayed as warning |
| 3 | Both mode, FIL body empty | Validation error blocks submit |
| 4 | Template placeholder count differs between EN and FIL bodies | Allowed — `{Message}` is literal text; no extra substitution inside it |
| 5 | Legacy Broadcast history records | `Broadcast` history rows already have rendered final SMS text in `SmsLog`; no retro-migration needed |

---

## Definition of Done

- [ ] `BroadcastMessageBodies` model and enum created
- [ ] BroadcastService routes each student to their language's body+template
- [ ] All three composer pages use `_MessageBodiesInput` partial
- [ ] Server-side validation enforces required bodies per mode
- [ ] Skip count returned and shown as warning on single-language modes
- [ ] Preview renders both language bodies
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-25 | Claude | Initial plan |
