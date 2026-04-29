# PL0039: Scanner Health Monitoring

> **Status:** Draft
> **Story:** [US0119: Scanner Health Monitoring](../stories/US0119-scanner-health-monitoring.md)
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Created:** 2026-04-28
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8 + SQL Server (Razor Pages + REST API)
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Add a server-side **heartbeat ingestion endpoint** that scanners ping every ~60s, persist the latest snapshot on the `Device` row, and surface it in the admin UI as a real-time **Online / Stale / Offline** badge. Reuse the existing `X-API-Key` device authentication pattern from `ScansApiController`. Thresholds (Online ≤ 2 min, Stale 2–10 min) are configurable via `AppSettings`. The existing `NoScanAlertService` zero-scan suppression log is enriched (not gated) with heartbeat-derived context. Scanner-side `HeartbeatService` is a companion change in SmartLogScannerApp tracked under a separate scanner-SDLC story (cross-repo dependency).

No new tables, no new audit logging — heartbeats are best-effort and overwrite a single per-device snapshot to keep storage flat.

---

## Acceptance Criteria Mapping

| AC (US0119) | Phase |
|-------------|-------|
| AC1: Heartbeat endpoint accepts payload, returns 204 | Phase 2 — controller |
| AC2: Same X-API-Key auth, revoked → 401 | Phase 2 — reuse `AuthenticateDeviceAsync` pattern |
| AC3: Scanner cadence (60s + backoff) | Out of scope here — belongs to scanner-side companion story |
| AC4: Online/Stale/Offline computation + AppSettings thresholds | Phase 3 — `DeviceHealthService` |
| AC5: Devices list Health column + 30s auto-refresh | Phase 4 — Razor page edits + small JS poll |
| AC6: Device detail Health panel | Phase 4b — extend existing detail rendering |
| AC7: `GET /api/v1/devices/health` for dashboard / external | Phase 3 — same controller |
| AC8: No-Scan-Alert log enrichment | Phase 5 — `NoScanAlertService` log message split |
| AC9: No AuditLog rows for heartbeats; rate-limit unchanged | Verified by code review — no AuditService call in heartbeat path |
| AC10: Latest-only snapshot, no history table | Phase 1 — columns on `Device`, no new entity |

---

## Technical Context

### Current state (verified)

**`Device` entity** — `src/SmartLog.Web/Data/Entities/Device.cs:9-45`. Already has `LastSeenAt` (DateTime?) which is updated by:
- `ScansApiController.SubmitScan` line 95 (every accepted/rejected scan attempt)
- `HealthController.GetDetails` line 106 (when scanner calls authenticated `/health/details`)

So `LastSeenAt` is *already* a partial liveness signal — but only updated on user-driven scan activity, not on idle. The new heartbeat endpoint makes idle scanners observable.

**Device authentication** — `ScansApiController.cs:412-416` is the canonical helper:
```csharp
private async Task<Device?> AuthenticateDeviceAsync(string apiKey)
{
    var keyHash = _deviceService.HashApiKey(apiKey);
    return await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);
}
```
`HealthController.GetDetails` (lines 80-103) duplicates the same logic inline. The new heartbeat controller will follow the same pattern. **Not refactoring this duplication in this story** — out of scope (separate cleanup story).

**Devices admin page** — `Pages/Admin/Devices.cshtml.cs:36, 58-74` is the list page; `Devices.cshtml:50-105` is the table. Current columns: Device Name, Location, Registered By, Registered At, Last Seen, Status, Actions. The page model has no auto-refresh — full server render only. Help text on line 189 ("Last Seen is updated each time a device submits a scan") needs to be updated to reflect heartbeat semantics.

**No-Scan-Alert suppression** — `Services/NoScanAlertService.cs:247-266`. Current behaviour: counts accepted scans today; if zero, writes a single warning log + `NO_SCAN_ALERT_SUPPRESSED` audit row, returns 0. AC8 enriches the *Details* string and warning log with heartbeat-derived context but **does not change the suppression rule itself**.

**AppSettings access pattern** — `IAppSettingsService.GetAsync<T>(string key, T defaultValue)`, used at `ScansApiController.cs:421` (`GetAsync("QRCode.DuplicateScanWindowMinutes", 5)`). New keys follow the `Health:` prefix.

**Last migration:** `20260427140915_DecoupleAuditLogFromAspNetUsers` (PL0038). New migration name: `AddDeviceHeartbeatSnapshot`.

---

## Implementation Phases

### Phase 1 — Entity + DbContext + Migration

**File:** `src/SmartLog.Web/Data/Entities/Device.cs`

Add seven nullable columns below `LastSeenAt` (line 40):
```csharp
[StringLength(50)]
public string? AppVersion { get; set; }

[StringLength(100)]
public string? OsVersion { get; set; }

public int? BatteryPercent { get; set; }

public bool? IsCharging { get; set; }

[StringLength(20)]
public string? NetworkType { get; set; }      // "WIFI", "ETHERNET", "CELLULAR", "OFFLINE"

public DateTime? LastHeartbeatAt { get; set; }  // distinct from LastSeenAt — only set by heartbeat endpoint

public int? QueuedScansCount { get; set; }
```

**Why a separate `LastHeartbeatAt`?** `LastSeenAt` is already updated by scan submissions and the `/health/details` call — overloading it would conflate "last activity" with "last heartbeat". Keep both: `LastSeenAt` is the canonical liveness anchor (updated by *all* device traffic, including the new heartbeat); `LastHeartbeatAt` is heartbeat-only and used to detect "silent" devices that talk via scans but never heartbeat (misconfigured scanner client). The Health Status formula uses `LastSeenAt` (broadest signal) per AC4.

**File:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` — no changes needed; properties auto-mapped, no new indexes (heartbeat-row reads are always by `Device.Id`, which is already the PK).

**Migration:**
```bash
dotnet ef migrations add AddDeviceHeartbeatSnapshot -p src/SmartLog.Web
```
Auto-generated `Up()` / `Down()` should be sufficient (seven `AddColumn` / `DropColumn` calls). Inspect the generated SQL with `dotnet ef migrations script <previous> AddDeviceHeartbeatSnapshot -p src/SmartLog.Web` before applying. No backfill needed — null is the correct initial state.

**Expected build state after Phase 1:** clean (only additions).

### Phase 2 — Heartbeat ingestion endpoint

**New file:** `src/SmartLog.Web/Controllers/Api/DeviceHeartbeatController.cs`

```csharp
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// Scanner heartbeat ingestion. Implements US0119 AC1, AC2, AC9, AC10.
/// </summary>
[ApiController]
[Route("api/v1/devices/heartbeat")]
[Produces("application/json")]
[EnableCors("ScannerDevices")]
public class DeviceHeartbeatController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DeviceHeartbeatController> _logger;

    public DeviceHeartbeatController(
        ApplicationDbContext context,
        IDeviceService deviceService,
        ILogger<DeviceHeartbeatController> logger)
    {
        _context = context;
        _deviceService = deviceService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] HeartbeatRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return Unauthorized(new ErrorResponse { Error = "InvalidApiKey", Message = "Invalid or missing API key" });
        }

        var keyHash = _deviceService.HashApiKey(apiKeyHeader.ToString());
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);

        if (device == null)
        {
            _logger.LogWarning("Invalid API key on heartbeat from {IpAddress}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new ErrorResponse { Error = "InvalidApiKey", Message = "Invalid or missing API key" });
        }
        if (!device.IsActive)
        {
            return Unauthorized(new ErrorResponse { Error = "DeviceRevoked", Message = "Device has been revoked" });
        }

        var now = DateTime.UtcNow;
        device.LastSeenAt = now;
        device.LastHeartbeatAt = now;
        device.AppVersion = Truncate(request.AppVersion, 50);
        device.OsVersion = Truncate(request.OsVersion, 100);
        device.BatteryPercent = ClampBattery(request.BatteryPercent);
        device.IsCharging = request.IsCharging;
        device.NetworkType = Truncate(request.NetworkType, 20);
        device.QueuedScansCount = request.QueuedScansCount is < 0 ? 0 : request.QueuedScansCount;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];

    private static int? ClampBattery(int? value)
        => value is null ? null : Math.Clamp(value.Value, 0, 100);
}

public class HeartbeatRequest
{
    [StringLength(50)] public string? AppVersion { get; set; }
    [StringLength(100)] public string? OsVersion { get; set; }
    [Range(0, 100)] public int? BatteryPercent { get; set; }
    public bool? IsCharging { get; set; }
    [StringLength(20)] public string? NetworkType { get; set; }
    public DateTime? LastScanAt { get; set; }    // accepted but not persisted — server has its own scan record
    [Range(0, int.MaxValue)] public int? QueuedScansCount { get; set; }
    public DateTime? ClientTimestamp { get; set; } // accepted for diagnostics, not persisted
}
```

**Decisions:**
- `LastScanAt` and `ClientTimestamp` from the request are accepted-but-ignored. They're documented in the story payload (AC1) but the server already knows last-scan time from the `Scan` table, and uses its own UTC clock as the heartbeat timestamp authority (per AC1 acknowledgement-time semantics). Accepting them keeps the contract forward-compatible if we want to surface them later.
- Field truncation is defensive — `[StringLength]` validates bind, but a too-long string still binds without it being null. Truncate before persist.
- Battery is `Math.Clamp(..., 0, 100)` even though `[Range(0,100)]` rejects out-of-range — defence in depth.
- No `AuditService.LogAsync` call. Per AC9, heartbeats do not write audit rows.
- Repeated invalid API keys are logged via `_logger.LogWarning` only (same pattern as `ScansApiController`); no rate limit added in this story.

### Phase 3 — Health computation service + admin API

**New file:** `src/SmartLog.Web/Services/DeviceHealthService.cs`

```csharp
namespace SmartLog.Web.Services;

public interface IDeviceHealthService
{
    DeviceHealthStatus ComputeStatus(DateTime? lastSeenAt, DateTime? nowUtc = null);
    Task<DeviceHealthThresholds> GetThresholdsAsync();
}

public enum DeviceHealthStatus { Online, Stale, Offline }

public record DeviceHealthThresholds(int OnlineWindowSeconds, int StaleWindowSeconds);

public class DeviceHealthService : IDeviceHealthService
{
    private readonly IAppSettingsService _appSettings;
    public DeviceHealthService(IAppSettingsService appSettings) { _appSettings = appSettings; }

    public async Task<DeviceHealthThresholds> GetThresholdsAsync()
    {
        var online = await _appSettings.GetAsync("Health:OnlineWindowSeconds", 120);
        var stale  = await _appSettings.GetAsync("Health:StaleWindowSeconds", 600);
        return new(online, stale);
    }

    public DeviceHealthStatus ComputeStatus(DateTime? lastSeenAt, DateTime? nowUtc = null)
    {
        // Stateless overload — caller passes thresholds via the async path before looping.
        // For the simple case we read defaults; admin pages call GetThresholdsAsync once per render.
        // (Keep this overload for unit tests where defaults are fine.)
        return ComputeStatusInternal(lastSeenAt, nowUtc ?? DateTime.UtcNow, new(120, 600));
    }

    public static DeviceHealthStatus ComputeStatusInternal(
        DateTime? lastSeenAt,
        DateTime nowUtc,
        DeviceHealthThresholds t)
    {
        if (lastSeenAt is null) return DeviceHealthStatus.Offline;
        var ageSec = (nowUtc - lastSeenAt.Value).TotalSeconds;
        if (ageSec <= t.OnlineWindowSeconds) return DeviceHealthStatus.Online;
        if (ageSec <= t.StaleWindowSeconds) return DeviceHealthStatus.Stale;
        return DeviceHealthStatus.Offline;
    }
}
```

**DI registration** — `Program.cs` (next to other `AddScoped` service lines): `services.AddScoped<IDeviceHealthService, DeviceHealthService>();`

**Reusing the service for the admin API** (AC7) — extend `DeviceHeartbeatController` *or* add a sibling controller. **Decision:** add a sibling `[Route("api/v1/devices")]` controller `DevicesApiController` to keep the heartbeat path narrowly scoped and the admin-side path under cookie auth.

```csharp
[ApiController]
[Route("api/v1/devices")]
[Authorize(Policy = "RequireSuperAdmin")]   // matches DevicesModel page-level policy (US0029)
public class DevicesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDeviceHealthService _healthService;

    public DevicesApiController(ApplicationDbContext context, IDeviceHealthService healthService)
    {
        _context = context;
        _healthService = healthService;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var thresholds = await _healthService.GetThresholdsAsync();
        var now = DateTime.UtcNow;
        var devices = await _context.Devices
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new {
                d.Id, d.Name, d.LastSeenAt, d.AppVersion, d.BatteryPercent
            })
            .ToListAsync();

        var result = devices.Select(d => new {
            deviceId = d.Id,
            name = d.Name,
            status = DeviceHealthService.ComputeStatusInternal(d.LastSeenAt, now, thresholds).ToString(),
            lastSeenAt = d.LastSeenAt,
            appVersion = d.AppVersion,
            batteryPercent = d.BatteryPercent
        });
        return Ok(result);
    }
}
```

**Authorization choice:** Match `DevicesModel`'s `[Authorize(Policy = "RequireSuperAdmin")]` (US0029). The story (AC7) said "CanManageUsers" but the existing Devices page is SuperAdmin-only — keeping the API policy looser than the page would let an Admin curl health data they can't see in the UI. Story-vs-page mismatch resolved in favour of the page's tighter scope. If a future dashboard widget needs broader visibility, relax in a follow-up — easier than tightening after deploy.

### Phase 4 — Admin UI

#### 4a — Devices list (`Pages/Admin/Devices.cshtml` + `.cshtml.cs`)

**Page model (`Devices.cshtml.cs`):**
- Inject `IDeviceHealthService _healthService` in ctor.
- In `OnGetAsync`, after loading `Devices`, also compute:
  ```csharp
  var thresholds = await _healthService.GetThresholdsAsync();
  var now = DateTime.UtcNow;
  HealthByDeviceId = Devices.ToDictionary(
      d => d.Id,
      d => DeviceHealthService.ComputeStatusInternal(d.LastSeenAt, now, thresholds));
  Thresholds = thresholds;
  ```
- Add public properties:
  ```csharp
  public Dictionary<Guid, DeviceHealthStatus> HealthByDeviceId { get; set; } = new();
  public DeviceHealthThresholds Thresholds { get; set; } = new(120, 600);
  ```

**Razor (`Devices.cshtml`):**
- Insert new column header before "Last Seen" (line 56):
  ```html
  <th>Health</th>
  ```
- Insert new `<td>` in the row template before the Last Seen cell:
  ```html
  <td>
      @if (!device.IsActive)
      {
          <span class="badge bg-secondary" title="Device revoked">Revoked</span>
      }
      else
      {
          var status = Model.HealthByDeviceId.GetValueOrDefault(device.Id, DeviceHealthStatus.Offline);
          var (cls, label, tip) = status switch
          {
              DeviceHealthStatus.Online => ("bg-success", "Online", FormatLastSeenTooltip(device.LastSeenAt)),
              DeviceHealthStatus.Stale  => ("bg-warning text-dark", "Stale", FormatLastSeenTooltip(device.LastSeenAt)),
              _                         => ("bg-danger", "Offline", FormatLastSeenTooltip(device.LastSeenAt))
          };
          <span class="badge @cls" title="@tip" data-device-health="@device.Id">@label</span>
      }
  </td>
  ```
  `FormatLastSeenTooltip` is a small `@functions` helper defined at the top of the cshtml that returns `"Last seen 12s ago"` or `"Never"`.
- Update the Note text on line 189 from `"Last Seen is updated each time a device submits a scan"` to `"Last Seen is updated by every scan submission and by the periodic heartbeat (every ~60s while the scanner is online)"`.

**Auto-refresh (AC5)** — vanilla JS at the bottom of the cshtml (no new framework):
```html
<script>
(function() {
    const POLL_MS = 30000;
    async function refresh() {
        try {
            const r = await fetch('/api/v1/devices/health', { credentials: 'same-origin' });
            if (!r.ok) return;
            const items = await r.json();
            for (const d of items) {
                const el = document.querySelector(`[data-device-health="${d.deviceId}"]`);
                if (!el) continue;
                el.classList.remove('bg-success','bg-warning','bg-danger','text-dark');
                el.textContent = d.status;
                if (d.status === 'Online') el.classList.add('bg-success');
                else if (d.status === 'Stale') el.classList.add('bg-warning','text-dark');
                else el.classList.add('bg-danger');
                if (d.lastSeenAt) {
                    const ageSec = Math.floor((Date.now() - new Date(d.lastSeenAt).getTime())/1000);
                    el.title = `Last seen ${formatAge(ageSec)} ago`;
                }
            }
        } catch (_) { /* swallow — best effort */ }
    }
    function formatAge(s) {
        if (s < 60) return s + 's';
        if (s < 3600) return Math.floor(s/60) + 'm';
        return Math.floor(s/3600) + 'h';
    }
    setInterval(refresh, POLL_MS);
})();
</script>
```
**Note:** the JS only mutates badge text/colour/tooltip in place — it does **not** rewrite the "Last Seen" `<td>` text. That column stays server-rendered and goes a bit stale between full reloads; acceptable trade since the badge carries the live signal. Adding ago-text reflow to JS is a nice-to-have; flagged as a future tweak if Tony asks.

#### 4b — Device detail health panel (AC6)

**Confirmed during plan review:** `Devices.cshtml` starts with a bare `@page` (no route parameter) and the Pages/Admin folder contains only `Devices.cshtml` and `RegisterDevice.cshtml` — there is no separate detail page. The Health panel must be added inside the list page, either as (a) a Bootstrap modal triggered from a "Details" button in the row Actions cell, or (b) an inline expanded-row section. **Recommend modal** to keep the table compact and avoid changing row layout. The panel fields are:

- Status badge (same colour scheme as list)
- Last Seen (UTC ISO + relative)
- Last Heartbeat At (UTC ISO + relative) — **only here**, not in the list view
- App Version, OS Version
- Network Type
- Battery: `BatteryPercent`% with `(charging)` suffix when `IsCharging == true`; `—` when null
- Queued Scans (with amber pill if > 50, per Edge Case in story)

If `Device.LastHeartbeatAt is null`, render **a single line** "No heartbeat recorded yet" instead of the panel grid (per AC6). Don't fake null fields as zeros.

### Phase 5 — No-Scan-Alert log enrichment (AC8)

**File:** `src/SmartLog.Web/Services/NoScanAlertService.cs`

Modify only the suppression block at lines 247-266. The behaviour change is: **when zero scans today**, additionally inspect heartbeat freshness across all active devices to choose between two log/audit messages.

```csharp
// Resolve health service from the EXISTING scope opened earlier in RunAlertCheckAsync
// (NoScanAlertService is a BackgroundService — singleton — that opens a scope per run
// at line 182. IDeviceHealthService is scoped, so it must be resolved here, not via ctor.)
var healthService = scope.ServiceProvider.GetRequiredService<IDeviceHealthService>();

if (totalScansToday == 0)
{
    var thresholds = await healthService.GetThresholdsAsync();
    var nowUtc = DateTime.UtcNow;
    var activeDevices = await context.Devices
        .Where(d => d.IsActive)
        .Select(d => d.LastSeenAt)
        .ToListAsync(stoppingToken);

    var anyOnlineRecently = activeDevices.Any(ls =>
        ls.HasValue &&
        DeviceHealthService.ComputeStatusInternal(ls, nowUtc, thresholds) != DeviceHealthStatus.Offline);

    string reason;
    string detailsSuffix;
    if (anyOnlineRecently)
    {
        reason = "Scanners were online but no scans recorded — likely operational issue, not connectivity";
        detailsSuffix = "Scanners online but idle (operational issue suspected).";
    }
    else
    {
        reason = "Zero total accepted scans today — possible scanner issue (no scanner online recently)";
        detailsSuffix = "No scanner online recently (connectivity issue suspected).";
    }

    _logger.LogWarning(
        "No-scan alert suppressed: {Reason} ({Date:yyyy-MM-dd})",
        reason, today);

    context.AuditLogs.Add(new AuditLog
    {
        Action = "NO_SCAN_ALERT_SUPPRESSED",
        Details = $"Date: {today:yyyy-MM-dd}. {detailsSuffix}",
        Timestamp = DateTime.UtcNow
    });
    await context.SaveChangesAsync(stoppingToken);
    return 0;
}
```

**DI (corrected):** `NoScanAlertService` is a `BackgroundService` and ctor-injects `IServiceProvider _serviceProvider` only — scoped services are resolved per run via `_serviceProvider.CreateScope()` (existing pattern at `NoScanAlertService.cs:99-100, 182-196`). `IDeviceHealthService` (scoped, transitively depends on DbContext via `IAppSettingsService`) **must not** be added to the ctor — it would break composition root validation. Resolve from the existing scope inside `RunAlertCheckAsync`, alongside the other scoped services on lines 182-196.

**Behaviour preserved:** suppression still happens when scans are zero. The only change is the message text and audit Details — fulfilling AC8's "does NOT change whether the alert is sent (suppression rule unchanged) — it only enriches the admin-facing log".

### Phase 6 — Tests

#### 6a — `DeviceHealthServiceTests.cs` (new)

```csharp
[Theory]
[InlineData(0, "Online")]
[InlineData(60, "Online")]
[InlineData(120, "Online")]
[InlineData(121, "Stale")]
[InlineData(599, "Stale")]
[InlineData(600, "Stale")]
[InlineData(601, "Offline")]
public void ComputeStatus_BoundaryCases(int ageSeconds, string expected)
{
    var now = new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc);
    var lastSeen = now.AddSeconds(-ageSeconds);
    var status = DeviceHealthService.ComputeStatusInternal(lastSeen, now, new(120, 600));
    Assert.Equal(expected, status.ToString());
}

[Fact]
public void ComputeStatus_NullLastSeen_IsOffline()
{
    var s = DeviceHealthService.ComputeStatusInternal(null, DateTime.UtcNow, new(120, 600));
    Assert.Equal(DeviceHealthStatus.Offline, s);
}

[Fact]
public async Task GetThresholdsAsync_UsesAppSettingsWithDefaults()
{
    // stub IAppSettingsService returns 30, 300; verify pass-through.
}
```

#### 6b — `DeviceHeartbeatControllerTests.cs` (new)

- POST without `X-API-Key` → 401
- POST with unknown key hash → 401
- POST against revoked device → 401
- POST happy path → 204 + persisted snapshot fields (battery clamped, app version truncated)
- POST with malformed JSON / out-of-range battery → 400 (model validation)
- POST does **not** create AuditLog row (assert count unchanged)

Test fixture pattern: in-memory `ApplicationDbContext` per `tests/SmartLog.Web.Tests/` conventions; stub `IDeviceService.HashApiKey` is unnecessary if we use the real `DeviceService` (it's pure SHA-256, no I/O).

#### 6c — `DevicesApiControllerTests.cs` (new)

- GET `/api/v1/devices/health` without auth → 401 (covered by integration test fixture if available; otherwise unit-test auth attribute presence)
- GET happy path → returns expected shape, status field is the computed enum string
- GET excludes revoked devices

#### 6d — `NoScanAlertServiceTests.cs` (extend existing)

- New case: `RunOnceAsync_ZeroScans_AnyDeviceOnline` → asserts AuditLog Details contains "Scanners online but idle"
- New case: `RunOnceAsync_ZeroScans_AllDevicesOffline` → asserts AuditLog Details contains "No scanner online recently"
- Existing zero-scan / has-scans / calendar suppression tests remain green (refactor minimally — only the message text changed).

#### 6e — Manual verification checklist

Run against `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"`:

1. **Heartbeat endpoint smoke** — `curl -X POST -H "X-API-Key: <test-key>" -H "Content-Type: application/json" -d '{"appVersion":"1.4.2","batteryPercent":85,"networkType":"WIFI","isCharging":true,"queuedScansCount":0}' http://localhost:5050/api/v1/devices/heartbeat` → 204; SQL `SELECT LastSeenAt, LastHeartbeatAt, AppVersion, BatteryPercent FROM Devices WHERE Id = ...` shows fresh values.
2. **Devices list** — `/Admin/Devices` shows new Health column with green Online for the device just heartbeated.
3. **Stale transition** — stop sending heartbeats; wait 2:01; refresh page → badge flips to Stale (amber). Wait 10:01 → Offline (red).
4. **AppSettings override** — set `Health:OnlineWindowSeconds = 30`, refresh → previously-Online device with last seen 45s ago now shows Stale. Restore default.
5. **Auto-refresh** — open list, observe DOM badge updating without page reload (DevTools network panel shows `/api/v1/devices/health` polled every 30s).
6. **Revoked device** — revoke a device; confirm badge becomes grey "Revoked" (not red Offline). Heartbeat to revoked key returns 401.
7. **Detail panel** — open device detail; verify all snapshot fields render. Wipe `LastHeartbeatAt` via SQL → verify "No heartbeat recorded yet" empty-state.
8. **Battery clamping** — POST with `batteryPercent: 250` → 400 from model validation. Bypass model validation by sending null but stuffing the DB directly with 150 → UI shows raw value (clamping is server-write protection only; UI is permissive).
9. **No-Scan-Alert enrichment** — set system clock just before `Sms:NoScanAlertTime`, ensure zero scans today. Toggle one device to "Online" via heartbeat; trigger run → AuditLog row Details says "Scanners online but idle". With no heartbeat → "No scanner online recently".
10. **Audit volume sanity** — POST 10 heartbeats; `SELECT COUNT(*) FROM AuditLogs WHERE Action LIKE 'Heartbeat%'` returns 0 (AC9).

---

## Risks & Considerations

- **Risk: Write contention on `Device` rows.** With ~5–10 scanners heartbeating every 60s, that's 5–10 `UPDATE` statements/min, each on a single row by PK. Negligible at SmartLog's scale; flagged in case scanner count grows past ~50.
- **Risk: Clock skew between scanner and server.** Server uses its own UTC for `LastSeenAt` / `LastHeartbeatAt`. `ClientTimestamp` is accepted in payload for diagnostics but not persisted in this story. If we later want to correlate, add a column.
- **Risk: Two scanners share an API key (US0119 edge case).** Already documented: `LastSeenAt` reflects the most recent heartbeat regardless of which scanner sent it. Out of scope to detect; flagged in TRD note.
- **Risk: Health column auto-refresh stalls if user backgrounds the tab.** Browser throttles `setInterval` in background tabs — acceptable, when the tab regains focus the next tick refreshes.
- **Risk: `Health:` AppSettings keys could be set to extreme values (e.g. -1, or stale < online).** Service treats them as raw ints; pathological values produce always-Offline or always-Online behaviour but never crash. Validation could be added in a follow-up; not worth the complexity now.
- **Risk: No-Scan-Alert enrichment misclassifies devices that were online earlier in the day but offline now.** AC8 says "online for the bulk of the school day" — the implemented check is "online *right now*" which is a simplification. Document this gap; full historical check requires the deferred `DeviceHeartbeat` history table.
- **Risk: PowerShell/curl sample in manual verification gives test-key in plain text.** Tests run on dev DB only; document not to use production keys.
- **Risk: Heartbeat success could mask a partial failure** (e.g. scanner can heartbeat but can't post scans because of a bug). The "scanners online but idle" log message in AC8 is exactly the canary for this; intentional.

---

## Out of Scope

- `DeviceHeartbeat` history table for trend/uptime charts (deferred per US0119).
- Push alerts (email/SMS) when a scanner goes Offline (deferred per US0119).
- Scanner-side `HeartbeatService` implementation in SmartLogScannerApp — companion story to be drafted in scanner SDLC; this plan only ships the server.
- Refactoring duplicated `AuthenticateDeviceAsync` logic between `ScansApiController`, `HealthController`, and the new `DeviceHeartbeatController` into a shared service.
- Per-device threshold overrides (would need a new column or settings table — global threshold is sufficient for v1).
- Validating that AppSettings `Health:*` values are sane (positive, online < stale).
- Surfacing health on the dashboard home page (the API is in place; widget is a follow-up if Tony wants it).
- "Last Seen" relative-time live ticking in the list (server-rendered; only badge updates).

---

## Estimated Effort

- Phase 1 (entity + migration): ~20 min
- Phase 2 (heartbeat controller + tests setup): ~45 min
- Phase 3 (`DeviceHealthService` + `DevicesApiController`): ~30 min
- Phase 4a (Devices list column + JS auto-refresh): ~45 min
- Phase 4b (detail modal in Devices.cshtml): ~40 min
- Phase 5 (No-Scan-Alert enrichment): ~20 min
- Phase 6a–6c (new unit tests): ~60 min
- Phase 6d (extend NoScanAlert tests): ~30 min
- Phase 6e (manual verification, 10 items): ~40 min
- **Total:** ~5–6 hours

Aligns with the 5-pt estimate in US0119.

---

## Rollout Plan

1. Implement Phase 1 → `dotnet build` → `dotnet ef database update -p src/SmartLog.Web` locally.
2. Implement Phase 2 → run new heartbeat endpoint tests; smoke-test with curl against a seeded device.
3. Implement Phase 3 → unit tests for `DeviceHealthService` boundaries; `/api/v1/devices/health` smoke.
4. Implement Phase 4a → manual visual check of list page; verify auto-refresh in DevTools.
5. Implement Phase 4b → manual visual check of detail panel.
6. Implement Phase 5 → unit tests for No-Scan-Alert split; one manual run with synthetic zero-scan day.
7. Run full `dotnet test`.
8. Manual verification (Phase 6e).
9. Confirm with user before commit (project commit/push policy).
10. Commit on `dev` branch; PR to `main` (project git workflow).
11. Draft companion scanner-side story (`HeartbeatService` in SmartLogScannerApp SDLC) — separate task, not part of this PR.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-28 | Claude (Opus 4.7) | Initial plan drafted |
| 2026-04-28 | Claude (Opus 4.7) | Review fixes: (1) NoScanAlertService is `BackgroundService` with `IServiceProvider`-based scope creation — `IDeviceHealthService` must be resolved per-scope, not ctor-injected; (2) `DevicesApiController` policy switched from `CanManageUsers` to `RequireSuperAdmin` to match `DevicesModel` page-level policy and avoid API/UI privilege mismatch; (3) Phase 4b confirmed: no separate detail page exists, panel goes in Devices.cshtml as a modal |
