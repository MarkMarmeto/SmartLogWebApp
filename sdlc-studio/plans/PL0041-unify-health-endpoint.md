# PL0041: Unify /health, /health/details, and /health/time Into One Endpoint

> **Status:** Done
> **Story:** [US0121: Unify Health Endpoint](../stories/US0121-unify-health-endpoint.md)
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Created:** 2026-05-08
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8 + SQL Server
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Refactor `HealthController` so that `GET /api/v1/health` is a **single auth-aware endpoint**:

- **No / missing `X-API-Key`** → minimal payload (`status`, `serverTime`, `version`), no DB hit.
- **Present-but-invalid `X-API-Key`** → `401 InvalidApiKey`.
- **Valid `X-API-Key`** → detailed payload (`database`, `scanners`) + `serverTime`, with `Device.LastSeenAt` updated.

`GET /api/v1/health/details` and `GET /api/v1/health/time` become **thin `[Obsolete]` shims** that delegate to the unified handler so old scanner builds keep working through the rollout window. Removal is a follow-up story after US0132 ships.

`POST /api/v1/devices/heartbeat` is **not** touched.

No DB schema change. No new AppSettings. No new services.

---

## Acceptance Criteria Mapping

| AC (US0121) | Phase |
|-------------|-------|
| AC1: Unauthenticated minimal response, no DB hit | Phase 2 — `HealthController.Get` |
| AC2: Authenticated full response | Phase 2 — same handler, auth-aware branch |
| AC3: Invalid key → 401 (distinguish "missing" from "invalid") | Phase 2 — three-way auth state |
| AC4: `serverTime` always present | Phase 1 — unified `HealthResponse` shape |
| AC5: Shims preserved | Phase 3 — `[Obsolete]` shim methods |
| AC6: No-DB-hit on unauth path | Phase 2 — verified by tests in Phase 4 |
| AC7: `Device.LastSeenAt` updated on auth path | Phase 2 — preserved from current `/health/details` |
| AC8: 503 on auth path when DB down; 200 on unauth path regardless | Phase 2 — split error handling |
| AC9: `Cache-Control` header | Phase 2 — middleware/attribute |
| AC10: Auth-fail logging rate-limited | Phase 2 — reuse existing IP rate-limit util |

---

## Technical Context

### Current state (verified)

**`HealthController.cs`** — `src/SmartLog.Web/Controllers/Api/HealthController.cs`
- Line 33-37: `GetServerTime` (`/health/time`) — returns `{ utc }`, no auth, no DB.
- Line 43-70: `Get` (`/health`) — basic, no auth, **does** hit DB via `Database.CanConnectAsync()`.
- Line 76-159: `GetDetails` (`/health/details`) — `X-API-Key` auth, full DB latency + scanner counts, updates `Device.LastSeenAt`.
- Line 165-194: Response DTOs — `HealthResponse`, `HealthDetailsResponse`, `DatabaseHealth`.

**`Program.cs:300`** — `app.MapHealthChecks("/health")` is **the framework health-checks endpoint**, distinct from `/api/v1/health`. Out of scope; not mapped to this controller.

**Existing API-key auth pattern** — `ScansApiController.cs:412-416`:
```csharp
private async Task<Device?> AuthenticateDeviceAsync(string apiKey)
{
    var keyHash = _deviceService.HashApiKey(apiKey);
    return await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);
}
```
Currently duplicated inline in `HealthController.GetDetails:80-103`. **Will be duplicated again here** (same scope-deferral as PL0039).

**Frontend usage** — `wwwroot/js/session-manager.js:138` calls `/health` (no auth) for session keep-alive. Unauth response shape is preserved (`status` + `serverTime` + `version`) so no JS change required.

**Test pattern** — existing tests live under `tests/SmartLog.Web.Tests/`. Search for `HealthControllerTests` to extend; if it does not exist, create a new test file following the patterns in `ScansApiControllerTests`.

### Why three auth states (not two)

The naïve refactor — "if header present, do the detailed path; else, basic path" — breaks the setup wizard. The wizard sends a *candidate* key to validate it; if we silently fall back to the unauth response when the key is wrong, the wizard would see a 200 and conclude the key is valid.

So:

| `X-API-Key` header | Action | Status |
|---|---|---|
| Missing (or empty) | Unauth path: liveness only | `200` |
| Present, matches active device | Auth path: full detail + LastSeenAt | `200` (or `503` if DB down) |
| Present, no match | Reject explicitly | `401 InvalidApiKey` |

This three-way state is the load-bearing decision in the design.

---

## Implementation Phases

### Phase 1 — Response DTO Consolidation

**File:** `src/SmartLog.Web/Controllers/Api/HealthController.cs`

Replace the three existing DTOs with a single `HealthResponse` that uses optional sub-objects for the authenticated-only fields. Use `JsonIgnoreCondition.WhenWritingNull` so the unauthenticated response doesn't include `database`/`scanners` keys at all.

```csharp
public class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public string ServerTime { get; set; } = string.Empty; // ISO-8601 UTC, ms precision, "Z"
    public string Version { get; set; } = "1.0.0";
    public string? Error { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DatabaseHealth? Database { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScannerStats? Scanners { get; set; }
}

public class DatabaseHealth
{
    public string Status { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}

public class ScannerStats
{
    public int Active { get; set; }
    public int ScansToday { get; set; }
}
```

`ServerTime` is a string (not `DateTime`) so we control the format end-to-end and avoid surprise re-serialisation differences. Format: `DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)`.

Remove `HealthDetailsResponse` (replaced by `HealthResponse`).

> **Why string, not DateTime?** EF Core has bitten us before with `DateTimeKind.Unspecified` round-tripping; the scanner's `TimeService` does latency-bracketed bracketing where exact wire-format determinism matters. String avoids the foot-gun.

---

### Phase 2 — Unified Handler

**File:** `src/SmartLog.Web/Controllers/Api/HealthController.cs`

Replace the three existing methods with a private `BuildHealthAsync` core + a single public `Get` action.

**Auth-state classification:**
```csharp
private async Task<(AuthState state, Device? device)> ClassifyAuthAsync()
{
    if (!Request.Headers.TryGetValue("X-API-Key", out var headerVal)
        || string.IsNullOrWhiteSpace(headerVal))
    {
        return (AuthState.Unauthenticated, null);
    }

    var keyHash = _deviceService.HashApiKey(headerVal!);
    var device = await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);
    return device is null
        ? (AuthState.InvalidKey, null)
        : (AuthState.Authenticated, device);
}

private enum AuthState { Unauthenticated, InvalidKey, Authenticated }
```

**Public action:**
```csharp
[HttpGet]
public async Task<IActionResult> Get()
{
    Response.Headers.CacheControl = "no-cache, no-store";

    var (auth, device) = await ClassifyAuthAsync();

    if (auth == AuthState.InvalidKey)
    {
        LogInvalidKeyOnce();  // IP-based rate-limit, AC10
        return Unauthorized(new ErrorResponse
        {
            Error = "InvalidApiKey",
            Message = "Invalid or missing API key"
        });
    }

    var response = new HealthResponse
    {
        ServerTime = FormatServerTime(DateTime.UtcNow),
    };

    if (auth == AuthState.Unauthenticated)
    {
        // AC1: liveness only, NO DB hit.
        return Ok(response);
    }

    // AC2: authenticated path
    return await PopulateAuthenticatedAsync(response, device!);
}

private async Task<IActionResult> PopulateAuthenticatedAsync(HealthResponse response, Device device)
{
    device.LastSeenAt = DateTime.UtcNow;  // AC7 — preserve /health/details side-effect

    try
    {
        var sw = Stopwatch.StartNew();
        await _context.Database.CanConnectAsync();
        sw.Stop();

        var activeScanners = await _context.Devices.CountAsync(d => d.IsActive);
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var scansToday = await _context.Scans
            .CountAsync(s => s.ScannedAt >= todayUtc && s.ScannedAt < tomorrowUtc);

        await _context.SaveChangesAsync();

        response.Database = new DatabaseHealth { Status = "healthy", LatencyMs = sw.ElapsedMilliseconds };
        response.Scanners = new ScannerStats { Active = activeScanners, ScansToday = scansToday };
        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Authenticated health check failed");
        try { await _context.SaveChangesAsync(); } catch { /* best-effort LastSeenAt */ }

        response.Status = "unhealthy";
        response.Error = "Database connectivity issue";
        response.Database = new DatabaseHealth { Status = "unhealthy", LatencyMs = -1 };
        return StatusCode(503, response);  // AC8 — serverTime + version still present
    }
}

private static string FormatServerTime(DateTime utc) =>
    utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
```

**`LogInvalidKeyOnce`** uses an in-memory dictionary keyed by `HttpContext.Connection.RemoteIpAddress` with a 5-minute sliding window. If `ScansApiController` already has this util — reuse it. If not, a small `MemoryCache` entry per IP suffices; do not introduce Polly or distributed cache.

**No DB hit on unauth path:** Critical. The unauthenticated branch must complete with zero awaits on `_context.*`. Verified by Phase 4 test using EF Core's `LogTo` capture.

---

### Phase 3 — Backwards-Compatible Shims

**File:** `src/SmartLog.Web/Controllers/Api/HealthController.cs`

Keep the old routes alive, but as `[Obsolete]` delegations:

```csharp
[HttpGet("details")]
[Obsolete("Use GET /api/v1/health with X-API-Key. This route will be removed after scanner client US0132 rollout completes.")]
public Task<IActionResult> GetDetails() => Get();   // Same auth-aware handler.

[HttpGet("time")]
[Obsolete("Use GET /api/v1/health — `serverTime` is now in every response. Will be removed after US0132 rollout.")]
public IActionResult GetServerTime()
{
    Response.Headers.CacheControl = "no-cache, no-store";
    return Ok(new { utc = FormatServerTime(DateTime.UtcNow) });
}
```

**Important:** `GetDetails` shim returns the unified `HealthResponse`, **not** the old `HealthDetailsResponse` shape. Pre-US0132 scanner code (`ConnectionTestService`) reads only `status` and treats anything else as "OK" — verified in scanner code at `ConnectionTestService.cs:62-79`. The new shape is a strict superset (adds `serverTime`; renames `activeScanners`/`scansToday` to nested `scanners.{active,scansToday}`).

> **Risk:** The field rename inside the auth response (`activeScanners` → `scanners.active`) is a contract change for the *shim* response. **Mitigation:** verify in Phase 4 that no scanner-side code reads those nested fields today (search confirms only `status` is read). If a deeper consumer surfaces, add a top-level mirror field for one release.

---

### Phase 4 — Tests

**File:** `tests/SmartLog.Web.Tests/Controllers/HealthControllerTests.cs` (create if missing)

Test cases (one per AC, plus shim tests):

| # | Test | Setup | Asserts |
|---|------|-------|---------|
| 1 | `Get_NoHeader_Returns200WithMinimalPayload` | No `X-API-Key` | 200, body has `status`, `serverTime`, `version`, **no** `database`, **no** `scanners` |
| 2 | `Get_NoHeader_DoesNotHitDatabase` | No header; capture EF logs via `LogTo` | Zero queries containing `SELECT` against `Devices`/`Scans` |
| 3 | `Get_ValidKey_Returns200WithFullPayload` | Seeded device with valid hash | 200, `database.status="healthy"`, `scanners.active >= 1`, `serverTime` present, `device.LastSeenAt` updated |
| 4 | `Get_InvalidKey_Returns401` | `X-API-Key: not-a-real-key` | 401, body `{error:"InvalidApiKey"}` |
| 5 | `Get_EmptyKey_TreatedAsMissing` | `X-API-Key: ""` | 200 (unauth path), no DB hit |
| 6 | `Get_DbDown_AuthPath_Returns503WithServerTime` | Mock context to throw on `CanConnectAsync` | 503, `serverTime` and `version` populated, `database.status="unhealthy"` |
| 7 | `Get_DbDown_UnauthPath_StillReturns200` | Same mock; no header | 200 (process alive ≠ DB up) |
| 8 | `Get_NoHeader_HasCacheControlHeader` | No header | `Cache-Control: no-cache, no-store` |
| 9 | `Get_ValidKey_UpdatesLastSeenAt` | Seeded device with stale `LastSeenAt` | After call, `LastSeenAt` is fresh (within 1s of UTC now) |
| 10 | `GetDetails_Shim_Returns200_AndDelegates` | Valid key | Same body as `Get` with valid key |
| 11 | `GetTime_Shim_Returns200WithUtc` | No auth | Body matches `{utc:"..."}` ISO-8601 with `Z` |
| 12 | `Get_ServerTime_IsParseableAsUtc` | Any path | `DateTime.Parse(serverTime, ...)` round-trips within 1ms |
| 13 | `Get_RepeatedInvalidKey_LogsOnce` | 10 invalid-key calls from same IP within 1 min | Only 1 warning log emitted |

Use `WebApplicationFactory<Program>` for integration tests (matches existing pattern). For DB-down simulation, swap `ApplicationDbContext` for one whose `Database.CanConnectAsync` throws.

---

### Phase 5 — Documentation Sync

**File:** `src/SmartLog.Web/CLAUDE.md`

Update the **API Reference → Scanner APIs** section:

```markdown
### Scanner APIs (Device Auth, optional)
- `POST /api/v1/scans` — Submit QR scan (X-API-Key required)
- `POST /api/v1/devices/heartbeat` — Scanner vitals push (X-API-Key required)
- `GET  /api/v1/health` — **Auth-aware**:
  - No `X-API-Key`: minimal liveness response (status, serverTime, version)
  - Valid `X-API-Key`: full response incl. database latency, scanner counts; updates LastSeenAt
  - Invalid `X-API-Key`: 401
- `GET  /api/v1/health/details` *(deprecated, will be removed — use /health with X-API-Key)*
- `GET  /api/v1/health/time` *(deprecated, will be removed — serverTime now in /health)*
```

**File:** `docs/API.md` (if it exists per earlier search) — add the same change.

**Migration note for ops:** No DB migration. No restart-time config change. Deploy is a code-only change; old scanners continue working via shims.

---

## File-Level Change List

| File | Change | Lines (approx) |
|------|--------|----------------|
| `src/SmartLog.Web/Controllers/Api/HealthController.cs` | Replace handler with auth-aware unified `Get`; keep `GetDetails`/`GetServerTime` as `[Obsolete]` shims; consolidate DTOs | -120 / +160 |
| `tests/SmartLog.Web.Tests/Controllers/HealthControllerTests.cs` | New file (or extend existing) | +200 |
| `src/SmartLog.Web/CLAUDE.md` | Update Scanner APIs reference | +5 / -1 |
| `docs/API.md` | Same update if file exists | +5 / -1 |

No migrations. No `Program.cs` changes. No DI changes.

---

## Risk & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Scanner client somewhere relies on the exact `/health/details` JSON shape | Low | Medium | Phase 3 shims keep the route; Phase 4 test #10 pins the shape; companion story US0132 migrates the scanner explicitly. Field rename impact verified by code-search (only `status` is read). |
| Frontend `session-manager.js` breaks when unauth response shape changes | Very low | Low | Adds `serverTime`; preserves `status`. JS reads only `status`. |
| Auth-fail rate-limit log util doesn't exist yet → noisy logs | Low | Low | Inline a `MemoryCache`-based one in this PR if not present (≤20 lines). |
| `[Obsolete]` warnings break CI | Low | Low | Suppress via `#pragma warning disable CS0618` *only* at the call site within `HealthController` for the inter-shim delegation. Other consumers should never call shims internally. |
| Removing shims later breaks long-tail scanner deployments | Medium | Medium | The follow-up removal story will gate on US0132 having shipped to **all** known gates (verified via `Device.AppVersion` from heartbeats — bonus consumer of US0119 data). |

---

## Out of Scope (explicit)

- Refactoring `AuthenticateDeviceAsync` duplication across controllers.
- `MapHealthChecks("/health")` framework endpoint in `Program.cs` (different route, different consumer).
- Heartbeat endpoint or any of its fields.
- Server clock correctness (NTP, drift detection).
- Scanner-side code — see PL0030 in SmartLogScannerApp.

---

## Estimation

**Story Points:** 2
**Complexity:** Low
**Estimated Time:** 2–3 hrs

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-08 | Claude (Opus 4.7) | Initial draft |
