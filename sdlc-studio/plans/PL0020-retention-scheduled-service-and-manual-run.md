# PL0020: Scheduled Retention Service + Manual Run + Dry-Run — Implementation Plan

> **Status:** Done
> **Story:** [US0101: Scheduled Retention Service + Manual Run + Dry-Run](../stories/US0101-retention-scheduled-service-and-dry-run.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

Implement `RetentionService` — an `IHostedService` that runs all enabled retention handlers on a configurable daily schedule (default 02:00). Wire the Admin UI "Run Now" and "Dry Run" buttons on `/Admin/Settings/Retention` to invoke handlers on demand. Add a "Recent Runs" section to the Retention page showing the last 20 `RetentionRun` entries.

This story depends on all six handler stories (US0094–US0100 via PL0013–PL0019). Handlers must be registered before this service is useful.

**Pre-existing state:**
- `NoScanAlertService` (IHostedService) — use as the pattern template for daily wake-up timing.
- All six `IEntityRetentionHandler` implementations registered in DI (assumed complete).
- `RetentionRun` entity exists (PL0013).
- Retention admin page exists (PL0013) — this story extends it with buttons + recent runs section.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | IHostedService | `RetentionService` wakes at configurable daily time; acquires app-instance lock |
| AC2 | Handler iteration | Iterates all registered `IEntityRetentionHandler`; runs enabled ones sequentially |
| AC3 | Daily idempotency guard | Skips entity if `RetentionRun` row exists today with `Status = Success`; overridable by manual run |
| AC4 | Manual Run Now | Admin button triggers handler immediately in background; UI polls for completion |
| AC5 | Manual Dry Run | Admin button runs dry-run path; UI shows row count without deleting |
| AC6 | Recent runs view | Last 20 `RetentionRun` rows across all entities shown on Retention page |
| AC7 | Error isolation | One handler failure does not block others; logged as Failed in `RetentionRun` |
| AC8 | Graceful shutdown | Current batch completes; run marked Partial; service stops |

---

## Technical Context

### NoScanAlertService Pattern
`NoScanAlertService` uses a background loop:
```csharp
while (!stoppingToken.IsCancellationRequested) {
    var now = DateTime.Now;
    var targetTime = TimeOnly.Parse(_settings.RunTime);
    var delay = ComputeDelayUntilNextRun(now, targetTime);
    await Task.Delay(delay, stoppingToken);
    await RunAsync(stoppingToken);
}
```
Use the same approach for `RetentionService`. Pull `Retention:RunTime` from `AppSettings` table (same key/value pattern as SMS settings), default `"02:00"`.

### Handler Discovery
All `IEntityRetentionHandler` implementations registered as `AddScoped` in Program.cs. Use `IServiceScopeFactory` in the hosted service to resolve them (hosted services have singleton lifetime; handlers need scoped DbContext):
```csharp
using var scope = _scopeFactory.CreateScope();
var handlers = scope.ServiceProvider.GetServices<IEntityRetentionHandler>();
```

### Daily Guard
```csharp
var today = DateTime.UtcNow.Date;
var alreadyRan = await db.RetentionRuns
    .AnyAsync(r => r.EntityName == handler.EntityName
                && r.StartedAt.Date == today
                && r.Status == "Success"
                && r.RunMode == "Scheduled");
if (alreadyRan) continue;
```

### Run Now / Dry Run — Background Execution
Use `Task.Run` + `IServiceScopeFactory` to invoke the handler in background from the page handler. The page immediately returns a redirect; the UI polls a lightweight endpoint `GET /Admin/Settings/Retention/RunStatus?entity={name}` to check the latest `RetentionRun` row for that entity.

### "Run Now" vs scheduled RunMode
- Scheduled run: `RunMode = "Scheduled"` (set by `RetentionService`).
- Manual Run Now: `RunMode = "Manual"` (set by the page handler before calling `ExecuteAsync`).
- The `ExecuteAsync` signature doesn't carry RunMode — the handler always logs "Manual" or "Scheduled" based on the caller. Simplest: `ExecuteAsync` returns `RetentionResult`; the caller writes the `RetentionRun` row with the correct `RunMode`. Handlers currently write their own run row — standardize: handlers write their run row; callers pass `runMode` as a parameter to `ExecuteAsync`.

> **Decision:** Add `string runMode = "Manual"` parameter to `ExecuteAsync`. Update the interface in `IEntityRetentionHandler.cs`:
> ```csharp
> Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct, string runMode = "Manual");
> ```
> `RetentionService` passes `"Scheduled"`. Manual buttons pass `"Manual"`. Dry Run passes `"DryRun"`.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Integration-heavy story; unit tests cover scheduling logic and daily guard; handler execution already tested in PL0014-PL0019.

---

## Implementation Phases

### Phase 1: Update IEntityRetentionHandler Signature

**Goal:** Add `runMode` parameter so callers set the run mode in `RetentionRun`.

- [ ] In `IEntityRetentionHandler.cs`, update `ExecuteAsync` signature:
  ```csharp
  Task<RetentionResult> ExecuteAsync(bool dryRun, CancellationToken ct, string runMode = "Manual");
  ```
- [ ] Update all six handler implementations to accept and use `runMode` when writing the `RetentionRun` row.

**Files:** `Services/Retention/IEntityRetentionHandler.cs`, all six handler files.

### Phase 2: RetentionService (IHostedService)

**Goal:** Daily scheduler that iterates handlers.

- [ ] Create `src/SmartLog.Web/Services/Retention/RetentionService.cs`:
  ```csharp
  public class RetentionService : BackgroundService {
      private readonly IServiceScopeFactory _scopeFactory;
      private readonly ILogger<RetentionService> _logger;

      protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
          while (!stoppingToken.IsCancellationRequested) {
              var runTime = await GetRunTimeAsync(); // e.g. "02:00"
              var delay = ComputeDelayUntilNext(runTime);
              await Task.Delay(delay, stoppingToken);
              await RunAllHandlersAsync("Scheduled", dryRun: false, stoppingToken);
          }
      }

      private async Task RunAllHandlersAsync(string runMode, bool dryRun, CancellationToken ct) {
          using var scope = _scopeFactory.CreateScope();
          var handlers = scope.ServiceProvider
              .GetServices<IEntityRetentionHandler>()
              .ToList();
          foreach (var handler in handlers) {
              if (ct.IsCancellationRequested) break;
              try {
                  if (!dryRun && await IsAlreadyRunToday(scope, handler.EntityName, ct)) {
                      _logger.LogDebug("Retention skipped for {entity} — already ran today", handler.EntityName);
                      continue;
                  }
                  await handler.ExecuteAsync(dryRun, ct, runMode);
              }
              catch (Exception ex) {
                  _logger.LogError(ex, "Retention handler {entity} failed", handler.EntityName);
                  // handler already writes Failed RetentionRun row internally
              }
          }
      }
  }
  ```
- [ ] `ComputeDelayUntilNext(string hhmm)`: parse time, compute ms until next occurrence in local time (handle DST same as `NoScanAlertService`).
- [ ] `GetRunTimeAsync()`: read `Retention:RunTime` from `AppSettings` table; default `"02:00"`.

**Files:** `Services/Retention/RetentionService.cs`

### Phase 3: Register Service

- [ ] In `Program.cs`:
  ```csharp
  builder.Services.AddHostedService<RetentionService>();
  ```

**Files:** `Program.cs`

### Phase 4: Admin UI — Run Now / Dry Run Buttons

**Goal:** Wire buttons on the Retention page to invoke handlers.

- [ ] Add to `Retention.cshtml.cs`:
  - `OnPostRunNowAsync(string entityName)` — fire-and-forget:
    ```csharp
    _ = Task.Run(async () => {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IEntityRetentionHandler>()
            .FirstOrDefault(h => h.EntityName == entityName);
        if (handler != null)
            await handler.ExecuteAsync(dryRun: false, ct: CancellationToken.None, runMode: "Manual");
    });
    TempData["RunStarted"] = entityName;
    return RedirectToPage();
    ```
  - `OnPostDryRunAsync(string entityName)` — same but `dryRun: true`, `runMode: "DryRun"`.
- [ ] Add a lightweight JSON endpoint (or inline API page handler) `OnGetRunStatusAsync(string entityName)` — returns the latest `RetentionRun` row for the entity as JSON for the UI polling:
  ```csharp
  var run = await _db.RetentionRuns
      .Where(r => r.EntityName == entityName)
      .OrderByDescending(r => r.StartedAt)
      .FirstOrDefaultAsync();
  return new JsonResult(run);
  ```
- [ ] In `Retention.cshtml`:
  - Enable "Run Now" + "Dry Run" buttons (they were disabled in PL0013; remove the `disabled` attribute now).
  - Each button is a form submit with `asp-page-handler="RunNow"` / `asp-page-handler="DryRun"` + hidden `entityName`.
  - Small JS polling snippet: after button click, poll the status endpoint every 3s, update a status badge per row until `CompletedAt` is not null.

**Files:** `Pages/Admin/Settings/Retention.cshtml(.cs)`

### Phase 5: Recent Runs Section

**Goal:** Show last 20 `RetentionRun` rows across all entities.

- [ ] In `Retention.cshtml.cs` `OnGetAsync`, load:
  ```csharp
  RecentRuns = await _db.RetentionRuns
      .OrderByDescending(r => r.StartedAt)
      .Take(20)
      .ToListAsync();
  ```
- [ ] In `Retention.cshtml`, add a table below the policy table:
  - Columns: Entity | Mode | Status | Rows Affected | Duration | Started At
  - Status shown as coloured badge (Success=green, Failed=red, Partial=orange).

**Files:** `Pages/Admin/Settings/Retention.cshtml(.cs)`

### Phase 6: Tests

| AC | Test | File |
|----|------|------|
| AC2 | Handler iteration: enabled handlers run, disabled skipped | `RetentionServiceTests.cs` |
| AC3 | Daily guard skips entity with today's Success run | same |
| AC3 | Manual Run Now overrides daily guard | same |
| AC7 | One handler failure does not block subsequent handlers | same |
| AC8 | CancellationToken propagated; service stops gracefully | same |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | `Retention:RunTime` not set in AppSettings | Default to `"02:00"`; log info on startup |
| 2 | App restart while run in progress | Current batch (already committed) is preserved; remainder runs on next scheduled fire or manual trigger |
| 3 | Manual Run Now while scheduled run is in progress | Each handler has its own single-flight `SemaphoreSlim` — second invocation returns `Skipped`; UI shows "Concurrent run in progress" |
| 4 | Dry Run triggered from UI while scheduled run active | Same single-flight guard; second invocation skipped |
| 5 | Configured run time in the past for today | Service computes next occurrence (tomorrow at that time); does not run retroactively |

---

## Definition of Done

- [ ] `RetentionService` IHostedService implemented; wakes at configured time; runs handlers sequentially
- [ ] Daily idempotency guard prevents double-run
- [ ] Manual Run Now + Dry Run buttons wired; UI polls for completion
- [ ] Recent runs section shows last 20 entries
- [ ] Error isolation: one handler failure does not block others
- [ ] `IEntityRetentionHandler.ExecuteAsync` updated with `runMode` parameter; all handlers updated
- [ ] Registered in DI
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
