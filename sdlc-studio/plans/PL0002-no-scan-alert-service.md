# PL0002: End-of-Day No-Scan Alert Service - Implementation Plan

> **Status:** Complete
> **Story:** [US0052: End-of-Day No-Scan Alert Service](../stories/US0052-no-scan-alert-service.md)
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Created:** 2026-04-16
> **Language:** C# / ASP.NET Core 8.0

## Overview

Implement `NoScanAlertService` as an `IHostedService` (BackgroundService) that wakes at a configurable daily time, identifies active enrolled students with zero scans for the day, and queues a `NO_SCAN_ALERT` SMS for each. Includes school-day guard, scanner-health guard, idempotency guard, and audit logging.

This follows the exact same pattern as `SmsWorkerService` — constructor-injected `IServiceProvider` with per-iteration scope creation, `Task.Delay` for timing, and `QueueCustomSmsAsync` for queuing.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Scheduled Execution | Service runs daily at configured `Sms:NoScanAlertTime` (default "18:10") |
| AC2 | School Day Guard | Skip if CalendarService says today is not a school day |
| AC3 | Scanner Health Guard | Suppress batch if zero scans exist across all devices today |
| AC4 | Identify No-Scan Students | Query active enrolled students with SmsEnabled + ParentPhone + zero accepted scans |
| AC5 | SMS Template Rendering | Render NO_SCAN_ALERT template with EN/FIL, fill all 5 placeholders |
| AC6 | Idempotency | Prevent duplicate NO_SCAN_ALERT for same student + same day |
| AC7 | Audit Logging | AuditLog entry on completion (executed) and suppression |
| AC8 | Template Seeded | NO_SCAN_ALERT template present after fresh DB init |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / .NET 8
- **Framework:** ASP.NET Core 8.0 Razor Pages
- **Test Framework:** xUnit (existing in `tests/SmartLog.Web.Tests/`)

### Existing Patterns (From Codebase Exploration)

| Pattern | Location | Notes |
|---------|----------|-------|
| BackgroundService base | `SmsWorkerService.cs:10` | Constructor: IServiceProvider, ILogger, IConfiguration |
| Daily timing | N/A (new pattern) | `Task.Delay(timeUntilAlert)` — wake-and-sleep, not polling |
| Scope per iteration | `SmsWorkerService.cs:51` | `_serviceProvider.CreateScope()` |
| SMS queuing | `SmsService.cs:670` | `QueueCustomSmsAsync(phone, message, priority, messageType)` |
| Duplicate check | `SmsService.cs:898` | `IsDuplicateAsync(phone, message, windowMinutes)` — exact match |
| Template rendering | `SmsService.cs:94` | `_templateService.RenderTemplateAsync(code, language, placeholders)` |
| School day check | `CalendarService.cs:85` | `IsSchoolDayAsync(date, gradeLevel?)` |
| Template seeding | `DbInitializer.cs:805` | UPSERT by Code; add to `systemTemplates` list |
| Service registration | `Program.cs:104` | `builder.Services.AddHostedService<T>()` |
| Audit log write | `AuditLog` entity | Action (50 chars), Details (500 chars), Timestamp |
| Active enrollment | `StudentEnrollment.IsActive=true` + `AcademicYear.IsCurrent=true` | |

### Student Fields Used
- `Student.IsActive`, `Student.SmsEnabled`, `Student.ParentPhone`, `Student.AlternatePhone`
- `Student.SmsLanguage` (EN/FIL), `Student.FirstName`, `Student.GradeLevel`, `Student.Section`

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** The service integrates with multiple existing services (CalendarService, SmsService, DbContext). Testing against real DB context is more valuable than unit-testing with mocks. The existing test project uses integration-style tests.

### Test Priority
1. Core alert path: school day + scans exist + students with no scans → SMS queued
2. Guard conditions: non-school day skip, zero-scans suppression
3. Idempotency: second run produces no duplicates

---

## Implementation Tasks

| # | Task | File | Depends On | Status |
|---|------|------|------------|--------|
| 1 | Seed `NO_SCAN_ALERT` template | `Data/DbInitializer.cs` | — | [ ] |
| 2 | Seed `Sms:NoScanAlertTime` AppSettings | `Data/DbInitializer.cs` | — | [ ] |
| 3 | Create `NoScanAlertService.cs` | `Services/NoScanAlertService.cs` | 1, 2 | [ ] |
| 4 | Register service in DI | `Program.cs` | 3 | [ ] |
| 5 | Write unit/integration tests | `tests/SmartLog.Web.Tests/` | 3 | [ ] |

### Parallel Execution Groups

| Group | Tasks | Prerequisite |
|-------|-------|--------------|
| A | 1, 2 | None (both in DbInitializer) |
| B | 3 | A complete |
| C | 4 | B complete |
| D | 5 | C complete |

---

## Implementation Phases

### Phase 1: Seeding (DbInitializer)
**Goal:** Ensure template and AppSettings key exist on fresh install and upgrades

**File:** `src/SmartLog.Web/Data/DbInitializer.cs`

- [ ] Add `NO_SCAN_ALERT` to `systemTemplates` list in `SeedSmsTemplatesAsync()`:
  ```csharp
  new()
  {
      Code = "NO_SCAN_ALERT",
      Name = "End-of-Day No-Scan Alert",
      TemplateEn = "SmartLog: We have no attendance record for {StudentFirstName} ({GradeLevel} - {Section}) today, {Date}. Please verify their whereabouts or contact the school at {SchoolPhone}.",
      TemplateFil = "SmartLog: Wala kaming rekord ng pagdalo ni {StudentFirstName} ({GradeLevel} - {Section}) ngayon, {Date}. Mangyaring tiyakin ang kanilang kinaroroonan o makipag-ugnayan sa paaralan sa {SchoolPhone}.",
      AvailablePlaceholders = "{StudentFirstName},{GradeLevel},{Section},{Date},{SchoolPhone}",
      IsActive = true,
      IsSystem = true
  }
  ```
- [ ] In `SeedAppSettingsAsync()` (or equivalent), add key `Sms:NoScanAlertTime` = `"18:10"`, Category = `"Sms"` — only if key does not already exist

**Files:** `src/SmartLog.Web/Data/DbInitializer.cs`

---

### Phase 2: NoScanAlertService
**Goal:** Implement the background service with all guards and queuing logic

**File:** `src/SmartLog.Web/Services/NoScanAlertService.cs`

- [ ] Class declaration:
  ```csharp
  public class NoScanAlertService : BackgroundService
  {
      private readonly IServiceProvider _serviceProvider;
      private readonly ILogger<NoScanAlertService> _logger;
      private readonly IConfiguration _configuration;
  }
  ```

- [ ] **Timing in `ExecuteAsync`:**
  - Parse `Sms:NoScanAlertTime` (default `"18:10"`) from config
  - On start: if current local time < alert time today → `Task.Delay(alertTime - now)` then execute
  - On start: if current local time is within 2 hours past alert time → execute immediately (AC1 edge case: service starts after alert time same day)
  - On start: if current local time is >2 hours past alert time → wait until alert time tomorrow
  - After each execution: wait until tomorrow's alert time

- [ ] **`ExecuteAlertAsync(CancellationToken)`:**
  1. Create scope, resolve: `ApplicationDbContext`, `ICalendarService`, `ISmsService`, `ISmsTemplateService`
  2. Check `ICalendarService.IsSchoolDayAsync(DateTime.Today)` → if false, log skip and return
  3. Check total accepted scans today across all devices → if zero, write `AuditLog` (Action: `NO_SCAN_ALERT_SUPPRESSED`, Details: suppression reason), return
  4. Get current academic year (`AcademicYears.FirstOrDefault(ay => ay.IsCurrent && ay.IsActive)`)
  5. Query students: `IsActive=true` + active enrollment in current year + `SmsEnabled=true` + `ParentPhone` not null/empty + no accepted scan today
  6. For each student:
     - Render template via `RenderTemplateAsync("NO_SCAN_ALERT", student.SmsLanguage, placeholders)` where placeholders = `{StudentFirstName, GradeLevel, Section, Date, SchoolPhone}`
     - Queue via `QueueCustomSmsAsync(student.ParentPhone, message, SmsPriority.Normal, "NO_SCAN_ALERT")`
     - `ISmsService.IsDuplicateAsync` handles idempotency internally
  7. Write `AuditLog` (Action: `NO_SCAN_ALERT_EXECUTED`, Details: `$"Queued {count} alerts. Duration: {elapsed}ms"`)

- [ ] Get `SchoolPhone` from AppSettings key `System.SchoolPhone` (empty string if missing)

---

### Phase 3: Registration
**Goal:** Wire up the service in DI

**File:** `src/SmartLog.Web/Program.cs`

- [ ] Add immediately after `AddHostedService<SmsWorkerService>()` line:
  ```csharp
  builder.Services.AddHostedService<NoScanAlertService>();
  ```

---

### Phase 4: Testing & Validation

| AC | Verification Method | File Evidence | Status |
|----|---------------------|---------------|--------|
| AC1 | Unit test: `ExecuteAsync` calculates correct delay for given times | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC2 | Unit test: mock `IsSchoolDayAsync` returns false → 0 SMS queued | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC3 | Unit test: zero scans in DB → AuditLog SUPPRESSED, 0 SMS queued | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC4 | Integration test: seed students with/without scans, verify correct subset queued | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC5 | Unit test: template rendered with all 5 placeholders, EN/FIL both correct | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC6 | Integration test: run twice, verify SmsQueue count unchanged on second run | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC7 | Unit test: AuditLog entry written with correct Action and Details | `tests/NoScanAlertServiceTests.cs` | Pending |
| AC8 | Integration test: fresh DB init contains NO_SCAN_ALERT template | `tests/DbInitializerTests.cs` | Pending |

---

## Edge Case Handling Plan

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Alert time past when service starts | If within 2h window: execute immediately; else wait until tomorrow | Phase 2 |
| 2 | No enrolled students with SmsEnabled | Loop processes 0 students; AuditLog shows count=0; no error | Phase 2 |
| 3 | Student has ParentPhone but SmsEnabled=false | WHERE clause excludes `SmsEnabled=false` students | Phase 2 |
| 4 | DB connection error during job | Caught at scope level; log error; retry tomorrow | Phase 2 |
| 5 | `Sms:NoScanAlertTime` missing from config | Default to `"18:10"` in config read | Phase 2 |
| 6 | `System.SchoolPhone` missing | Render `{SchoolPhone}` as empty string (existing pattern) | Phase 2 |
| 7 | School day with early dismissal | Alert still runs at configured time (no special handling needed) | N/A |
| 8 | Student enrolled in multiple sections | `DISTINCT` on Student.Id in query prevents duplicates | Phase 2 |

**Coverage:** 8/8 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Timezone mismatch (UTC vs local) | Alert runs at wrong local time | Use `DateTime.Now` (local) for alert time calculation; existing app uses local time for scans |
| Large student body causes slow query | Alert delayed or timeouts | Add index on `Scans(StudentId, ScannedAt, Status)`; query runs once daily |
| IsDuplicateAsync checks message content — template change breaks idempotency | Duplicate alerts sent on same day | Acceptable edge case; template changes are rare admin actions |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] Unit tests written and passing
- [ ] Edge cases handled
- [ ] Code follows existing patterns (SmsWorkerService as reference)
- [ ] No linting errors
- [ ] `dotnet test` passes

---

## Notes

- `IsDuplicateAsync` in `SmsService.cs` checks phone + exact message content. This means idempotency is tied to the rendered message text. If the template changes between two runs on the same day, a duplicate could be sent. This is acceptable — template changes are rare admin operations.
- The timing logic uses local server time (same as `DateTime.Now` pattern used elsewhere in the app). No UTC conversion needed for the alert time.
- `SchoolPhone` placeholder: if `System.SchoolPhone` AppSetting is missing, render as empty string. The existing `SmsService` pattern does the same for missing config values.
