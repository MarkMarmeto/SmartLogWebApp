# US0121: Unify /health, /health/details, and /health/time Into One Endpoint

> **Status:** Done
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** AI Assistant
> **Reviewer:** Mark Daniel Marmeto
> **Created:** 2026-05-08

## User Story

**As a** maintainer of the SmartLog server-scanner contract
**I want** one health endpoint whose response detail level adapts to the caller's auth
**So that** there is a single source of truth for "is the server up, what time is it, and is my API key valid?" — fewer routes to document, version, and keep in sync, and one fewer round-trip during scanner clock sync

## Context

### Background

Today the WebApp exposes three closely-related GET routes on `HealthController`:

| Route | Auth | Caller | Purpose |
|-------|------|--------|---------|
| `GET /api/v1/health` | None | Scanner `HealthCheckService` (every 15s); session keep-alive JS | "Is the server reachable?" |
| `GET /api/v1/health/details` | `X-API-Key` | Scanner `ConnectionTestService` (setup wizard, one-shot) | "Is my API key valid + server healthy?" |
| `GET /api/v1/health/time` | None | Scanner `TimeService` (startup) | Server UTC for clock-offset bracketing |

These overlap awkwardly:

- `/health` and `/health/details` differ only by auth and by which fields populate. Both hit the DB.
- `/health/time` is a tiny endpoint that only exists because the basic `/health` doesn't include the server's UTC. Two startup round-trips when one would do.
- `POST /api/v1/devices/heartbeat` is a separate concern (scanner→server vitals push) and is **out of scope** for this story.

Fleet-level cost: with N gates × ~4 polls/min, the unauthenticated `/health` already runs `Database.CanConnectAsync()` on every poll. Even cheap, it's a DB hit per scanner per 15s for no actionable signal — the DB-down case shows up in scan submissions anyway.

### Goal

Collapse the three routes into a single `GET /api/v1/health` whose response **adapts to the request's authentication state**:

- **No / invalid `X-API-Key` →** minimal payload (status, server time, version). No DB hit on the hot path.
- **Valid `X-API-Key` →** detailed payload (database latency, active scanners, scans today) **plus** server time. DB hit included.

Server time is included **in every response** so that `TimeService` no longer needs `/health/time`.

`/health/details` and `/health/time` remain as **thin shims** that delegate to the unified handler for one release window; they are removed after scanner clients have rolled out (tracked in companion scanner story US0132).

### Why this is *not* a regression on `/health/details`

The setup-wizard contract is preserved: the wizard sends a candidate `X-API-Key` to `/api/v1/health` and receives the same fields it used to receive from `/health/details`. If the key is invalid, it gets the minimal payload + a `401`-equivalent indicator (see AC2 — we keep the explicit 401 response so the wizard can distinguish "wrong key" from "server down").

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0005 | Architecture | Scanner devices authenticate with `X-API-Key` over LAN | Auth-aware response uses the same header/format as `ScansApiController` |
| US0033 | Behavior | `/health` must return 200 on healthy, 503 on DB unreachable, response < 100ms | Preserved on the unauthenticated path; 503 only if **light** liveness check fails |
| US0119 | Distinct concern | `POST /devices/heartbeat` is the scanner-vitals push channel | Out of scope; heartbeat is unchanged |
| project_lan_setup | Deployment | LAN-first; multiple scanners poll continuously | Unauthenticated path must avoid per-poll DB hits |

---

## Acceptance Criteria

### AC1: Unified Endpoint — Unauthenticated Response
- **Given** a `GET /api/v1/health` with no `X-API-Key` header (or with a header that does not match any device)
- **Then** the server returns `200 OK` with body:
  ```json
  {
    "status": "healthy",
    "serverTime": "2026-05-08T03:14:15.926Z",
    "version": "1.0.0"
  }
  ```
- **And** no database query is executed on this path (a static "process is alive" liveness check is sufficient)
- **And** response time is < 50ms p95 under normal load

### AC2: Unified Endpoint — Authenticated Response
- **Given** a `GET /api/v1/health` with a valid `X-API-Key` matching an active device
- **Then** the server returns `200 OK` with body:
  ```json
  {
    "status": "healthy",
    "serverTime": "2026-05-08T03:14:15.926Z",
    "version": "1.0.0",
    "database": { "status": "healthy", "latencyMs": 5 },
    "scanners":  { "active": 3, "scansToday": 1250 }
  }
  ```
- **And** the device's `LastSeenAt` is updated to the server-received timestamp (preserves current `/health/details` side-effect)
- **And** if the database is unreachable, the response is `503` with `database.status: "unhealthy"`, `database.latencyMs: -1` — top-level `status: "unhealthy"`, but `serverTime` and `version` are still present

### AC3: Unified Endpoint — Invalid API Key
- **Given** a `GET /api/v1/health` with an `X-API-Key` header whose value does not match any registered device
- **Then** the server returns `401 Unauthorized` with body:
  ```json
  { "error": "InvalidApiKey", "message": "Invalid or missing API key" }
  ```
- **Note:** A *missing* `X-API-Key` is **not** an error — that's the unauthenticated path (AC1). Only a *present-but-invalid* key returns 401, so the setup wizard can distinguish "wrong key" from "server down".

### AC4: Server Time Always Present
- **Given** any successful response from `/api/v1/health` (auth or no-auth)
- **Then** the response body contains a `serverTime` field
- **And** the value is ISO-8601 UTC with millisecond precision and trailing `Z` (e.g. `"2026-05-08T03:14:15.926Z"`)
- **And** scanners can use this field for clock-offset bracketing without calling `/health/time` separately

### AC5: Backwards-Compatible Shims
- **Given** scanners running pre-US0132 client code in the field
- **When** they call `GET /api/v1/health/details` (with `X-API-Key`) or `GET /api/v1/health/time` (no auth)
- **Then** both routes still respond correctly:
  - `/health/details` returns the same shape it returned before this story (so old clients keep working)
  - `/health/time` returns `{ "utc": "..." }` exactly as before
- **And** internally both routes delegate to the unified handler so behavior cannot drift
- **And** both shims are marked `[Obsolete]` in code and slated for removal in a follow-up story after the scanner client roll-out

### AC6: No-Hit on Unauthenticated Polling
- **Given** the scanner fleet is polling `/api/v1/health` every 15s without an API key (the existing `HealthCheckService` cadence)
- **When** I look at SQL Server query stats over a 10-minute window
- **Then** there are no DB queries originating from the `/health` route
- **And** the response status remains `200` for the entire window provided the application process is alive

### AC7: Side-Effect Preservation on Authenticated Path
- **Given** a valid `X-API-Key` call to `/api/v1/health`
- **Then** `Device.LastSeenAt` is updated, matching the prior `/health/details` behavior (so admin "Last Seen" semantics do not regress)
- **And** the heartbeat-specific column `Device.LastHeartbeatAt` is **not** touched (only `POST /devices/heartbeat` writes that column, per US0119)

### AC8: 503 Behavior on Authenticated Path
- **Given** the database is unreachable
- **When** an authenticated `/health` request arrives
- **Then** the response is `503 Service Unavailable` with `status: "unhealthy"` and `database.status: "unhealthy"`
- **And** `serverTime` and `version` are still populated (so a scanner can still synchronise its clock against an unhealthy-but-reachable server)
- **And** the unauthenticated path continues to return `200` (process is alive even if DB is not — distinguishing "app dead" from "DB dead" is valuable)

### AC9: Response Headers
- **Given** any `/health` response
- **Then** the headers include `Cache-Control: no-cache, no-store` (preserved from US0033)

### AC10: Metrics & Logging
- **Given** authentication failures (AC3 — present-but-invalid key)
- **Then** they are logged once per source IP per 5-minute window (matches existing pattern in `ScansApiController`)
- **And** successful `/health` calls (auth or no-auth) are not logged (would dominate the log at fleet scale)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Empty `X-API-Key` header (header present, value empty) | Treat as missing → unauthenticated path (AC1) |
| Header present, value matches a **revoked / inactive** device | `401 InvalidApiKey` (AC3) — same as unknown key |
| `X-API-Key` and `Authorization` both present | `X-API-Key` wins (current behavior) |
| Server clock is wrong (e.g. NTP drift) | `serverTime` reflects whatever the server thinks it is — scanner's bracket logic absorbs reasonable skew. Out of scope to fix here. |
| Migration: scanner sends old call to `/health/details` | Shim still returns the same shape (AC5) |
| Concurrent fleet polls (~50 scanners × 4/min) | No DB pressure on the unauthenticated path (AC6); CPU cost is negligible |
| HEAD request | Returns same status code, empty body (default ASP.NET Core behavior — accept) |
| HTTPS with self-signed cert | Out of scope (existing scanner cert-trust behavior is unchanged) |

---

## Test Scenarios

- [ ] `/health` with no header → 200, `{status, serverTime, version}` only, no DB hit (verified via EF Core query log)
- [ ] `/health` with valid X-API-Key → 200, full payload, `Device.LastSeenAt` updated
- [ ] `/health` with invalid X-API-Key → 401 with `InvalidApiKey` error code
- [ ] `/health` with valid X-API-Key when DB is down → 503 with `serverTime` still populated
- [ ] `/health` with no header when DB is down → 200 (process alive ≠ DB up — by design)
- [ ] `/health/details` shim returns the legacy response shape (unchanged contract)
- [ ] `/health/time` shim returns `{utc: "..."}` (unchanged contract)
- [ ] Both shims internally delegate (no logic duplication — verified via test that breaking unified handler breaks shims too)
- [ ] `Cache-Control: no-cache, no-store` header present on all responses
- [ ] Repeated invalid-key attempts from same IP log once per 5-min window
- [ ] `serverTime` ISO-8601 round-trips through `DateTime.Parse` to within 1ms

---

## Out of Scope (Deferred)

- **Removing the shims.** `/health/details` and `/health/time` are kept for one release window. A follow-up story will delete them after the scanner client (US0132) is rolled out to all gates. Tracked as a discovered story.
- **`POST /api/v1/devices/heartbeat`.** Heartbeat is a different direction (scanner → server) with a different payload and concern. Untouched.
- **Session keep-alive frontend usage** (`session-manager.js` calling `/health`). The unauthenticated response shape it relies on is preserved, so no JS change is needed.
- **Auth-aware response on a single endpoint as a general API pattern.** This story applies it only to `/health` because the routes were already aliases for the same intent. Other endpoints keep their explicit auth boundaries.
- **Refactoring the `AuthenticateDeviceAsync` duplication** (still inline in `ScansApiController` and `HealthController`). Same as US0119's deferred cleanup.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0033](US0033-health-check.md) | Predecessor | Original `/health` + `/health/details` contract | Done |
| [US0119](US0119-scanner-health-monitoring.md) | Distinct | `POST /devices/heartbeat` (untouched here) | Done |

### Cross-Repo Dependencies

| Repo | Change | Notes |
|------|--------|-------|
| SmartLogScannerApp | Update `HealthCheckService`, `ConnectionTestService`, `TimeService` to use unified `/health` | Companion story US0132. Old endpoints remain as shims (AC5) so deployment order does not matter — server can ship first, scanner can roll out at its own pace. |

### Technical Dependencies

- `Device` table (existing) — no schema change.
- `IDeviceService.HashApiKey` (existing).
- `IAppSettingsService` not required (no new settings).

---

## Estimation

**Story Points:** 2
**Complexity:** Low

Rough split:
- Controller refactor (unified handler + shims): 1 pt
- Tests + verifying no-DB-hit on unauth path: 1 pt

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-08 | Claude (Opus 4.7) | Initial draft |
