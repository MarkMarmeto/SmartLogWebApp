# US0101: Scheduled Retention Service + Manual Run + Dry-Run

> **Status:** Done
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Tony (IT Admin)
**I want** retention to run automatically on a daily schedule, with manual "Run Now" and "Dry Run" buttons per entity from the Admin UI
**So that** routine cleanup happens without anyone remembering, and I can safely test or force a run on demand.

## Context

### Background
Stories US0094-US0100 define the policy + per-entity handlers. This story ties them together: a scheduled `IHostedService` that iterates enabled policies daily and runs each handler, plus the Admin UI buttons wired into the same flow.

Follows the existing `NoScanAlertService` pattern (configured time, daily, skipped if already run today for that entity).

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| TRD | Architecture | `IHostedService` pattern | Reuse for retention worker |
| EP0017 | Operational | Purge during low-traffic hours | Default run time 02:00 local, admin-configurable |
| US0095-US0100 | Contracts | All handlers implement `IEntityRetentionHandler` | Scheduler iterates DI-registered handlers |

---

## Acceptance Criteria

### AC1: RetentionService IHostedService
- **Given** the app is running
- **Then** `RetentionService` is a registered `IHostedService`
- **And** it wakes at the configured daily time (setting: `Retention:RunTime`, default `"02:00"`)
- **And** it acquires a single-flight app-instance lock before running

### AC2: Handler Iteration
- **Given** the scheduled time arrives
- **Then** the service iterates all registered `IEntityRetentionHandler` instances
- **And** for each handler whose policy has `Enabled = true`, it calls `ExecuteAsync(dryRun: false)`
- **And** handlers run sequentially (not in parallel) to avoid DB contention

### AC3: Idempotent Daily Guard
- **Given** a retention run completed today for entity X
- **When** the service is invoked again the same day (e.g. restart + re-trigger)
- **Then** entity X is skipped (last `RetentionRun` for today exists with `Status = Success`)
- **And** this guard can be overridden via manual Run Now

### AC4: Manual Run Now from Admin UI
- **Given** I am on `/Admin/Settings/Retention`
- **When** I click "Run Now" on an entity row
- **Then** the handler's `ExecuteAsync(dryRun: false)` is invoked immediately (in background)
- **And** the UI polls `RetentionRun` for completion and shows status

### AC5: Manual Dry Run from Admin UI
- **Given** same page
- **When** I click "Dry Run"
- **Then** the handler's `PreviewAsync` (or `ExecuteAsync(dryRun: true)`) runs
- **And** the result shows: rows that would be deleted, oldest/newest row timestamps
- **And** no deletion occurs

### AC6: Run History View
- **Given** the Retention page
- **Then** a "Recent runs" section shows the last 20 `RetentionRun` rows (across all entities)
- **And** each row shows: Entity, Mode (Scheduled/Manual/DryRun), Status, RowsAffected, Duration, StartedAt

### AC7: Error Handling
- **Given** one handler fails
- **Then** the scheduler logs the failure (`RetentionRun.Status = Failed`)
- **And** continues with the next handler (one failure does not block the rest)

### AC8: Service Stop on App Shutdown
- **Given** app shutdown is requested mid-run
- **Then** the current batch completes, the run is marked `Status = Partial`, and the service stops gracefully

---

## Scope

### In Scope
- `RetentionService` hosted service
- Daily-schedule guard
- Manual Run Now / Dry Run buttons
- Recent runs view on Admin page
- Error isolation per handler
- Graceful shutdown

### Out of Scope
- Per-entity schedule (all entities run together daily)
- Cron-style expressions (single HH:mm field)
- Alert / email on failure (future: tie into SmsQueue or email infra)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Configured time passed mid-day | Service waits until next day; does not run retroactively (prevents unexpected prod activity) |
| Clock changes (DST) | Service anchors to next occurrence of HH:mm in local time |
| Admin triggers Run Now while scheduled run is in progress | Second trigger sees the single-flight lock and queues or rejects with clear message |
| Run Now on disabled policy | Handler short-circuits (existing contract) and UI shows "Policy disabled" |
| App restart with Run Now in flight | Current batch commits; remainder picked up on next invocation |

---

## Test Scenarios

- [ ] Service wakes at configured time and iterates handlers
- [ ] Sequential execution (not parallel)
- [ ] Daily guard prevents double-run
- [ ] Run Now button triggers handler and updates UI
- [ ] Dry Run button returns count without deleting
- [ ] Recent runs section displays last 20 entries
- [ ] One handler failure does not block others
- [ ] Graceful shutdown marks Partial

---

## Technical Notes

### Files
- **New:** `src/SmartLog.Web/Services/Retention/RetentionService.cs` (IHostedService)
- **Modify:** `src/SmartLog.Web/Pages/Admin/Settings/Retention.cshtml(.cs)` — wire buttons + poll
- **Modify:** `Program.cs` — register `RetentionService`
- **New settings key:** `Retention:RunTime` (default `"02:00"`), stored in AppSettings

### Single-flight Lock
- In-memory `SemaphoreSlim(1)` keyed per entity for handler concurrency
- App-instance-level lock for the scheduler (single-node deployment assumption matches LAN topology)

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0094](US0094-retention-policy-entity-and-admin-ui.md) | Foundation | Draft |
| US0095-US0100 | Handlers | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — hosted service + UI poll + concurrency care

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted |
