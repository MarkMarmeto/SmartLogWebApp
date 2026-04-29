# US0119: Scanner Health Monitoring

> **Status:** Draft
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** TBD
> **Created:** 2026-04-28

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** to see real-time health and connectivity status of every registered scanner from the WebApp
**So that** I can detect offline, stale, or misbehaving scanners before they cause missed attendance and silent No-Scan-Alert misfires

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Administrator responsible for keeping the gate scanners operational.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

### Problem
Today the only signal of a scanner being alive is `Device.LastScanAt` (surfaced in US0029 as "Last Scan"). That conflates *connectivity* with *activity* — an idle but online scanner looks the same as an unplugged one, and the End-of-Day No-Scan Alert (`NoScanAlertService`) can suppress alerts incorrectly when scanners go dark mid-day. US0033 gave the scanner a way to ping the server; this story is the inverse — the scanner pushes a periodic heartbeat that the admin UI can render as live health.

### Technical Context
- `Device` already has a `LastSeenAt` column (currently unused for monitoring purposes).
- Scanner side: SmartLogScannerApp (MAUI) is online whenever it has LAN reachability to the WebApp.
- Network: LAN-first deployment (see project_lan_setup memory) — heartbeats are cheap on LAN; battery is not a concern (scanners are typically mains-powered tablets, but field includes battery for visibility).

---

## Acceptance Criteria

### AC1: Scanner Heartbeat Endpoint
- **Given** a registered scanner with a valid API key
- **When** it sends `POST /api/v1/devices/heartbeat` with `X-API-Key` header and JSON body:
  ```json
  {
    "appVersion": "1.4.2",
    "osVersion": "Android 13",
    "batteryPercent": 87,
    "isCharging": true,
    "networkType": "WIFI",
    "lastScanAt": "2026-04-28T08:32:11Z",
    "queuedScansCount": 0,
    "clientTimestamp": "2026-04-28T08:32:45Z"
  }
  ```
- **Then** return `204 No Content` on success
- **And** persist the latest heartbeat snapshot against the `Device` row
- **And** update `Device.LastSeenAt` to the server-side received timestamp

### AC2: Heartbeat Authentication
- **Given** a request to `/api/v1/devices/heartbeat`
- **Then** the same `X-API-Key` device authentication used by `/api/v1/scans` applies
- **And** revoked / inactive devices receive `401 Unauthorized`

### AC3: Heartbeat Cadence (Scanner Side)
- **Given** the scanner is running
- **Then** it sends a heartbeat every 60 seconds while online
- **And** it retries with exponential backoff (max 5 min) if the server is unreachable
- **And** heartbeats are best-effort (not queued for replay if the scanner has been offline)

### AC4: Health Status Computation
- **Given** the WebApp evaluates a device's health
- **Then** status is derived from age of `LastSeenAt`:
  - `Online` — last seen within 2 minutes
  - `Stale` — last seen within 2–10 minutes
  - `Offline` — last seen more than 10 minutes ago, or never
- **And** thresholds are configurable via `AppSettings` keys `Health:OnlineWindowSeconds` (default 120) and `Health:StaleWindowSeconds` (default 600)

### AC5: Devices List Health Column
- **Given** I am on `/Admin/Devices`
- **Then** a new "Health" column is shown left of "Last Scan", with a colored badge: Online (green), Stale (amber), Offline (red), or Revoked (grey, takes precedence over health)
- **And** rows auto-refresh every 30 seconds without a full page reload
- **And** a tooltip on the badge shows the relative time ("Last seen 12s ago")

### AC6: Device Detail Health Panel
- **Given** I open a device's detail page
- **Then** I see a Health panel with: current status, last seen (UTC + relative), app version, OS version, network type, battery %, charging state, and queued scan count
- **And** if the device has never sent a heartbeat, the panel shows "No heartbeat recorded yet" rather than fake zero values

### AC7: Health API for Dashboard / Scripts
- **Given** I call `GET /api/v1/devices/health` with cookie auth (CanManageUsers policy)
- **Then** the response is a list of `{deviceId, name, status, lastSeenAt, appVersion, batteryPercent}` for all non-revoked devices
- **And** the response is suitable for embedding in the admin dashboard or polling from an external monitor

### AC8: No-Scan-Alert Cross-Check
- **Given** the No-Scan-Alert scanner-health guard runs (existing flow in process #2.B)
- **When** it would otherwise suppress alerts due to zero scans
- **Then** it additionally inspects scanner heartbeat status
- **And** if at least one scanner was `Online` for the bulk of the school day but produced no scans, it logs a distinct warning ("Scanners were online but no scans recorded — likely operational issue, not connectivity") instead of the generic suppression message
- **And** this does NOT change whether the alert is sent (suppression rule unchanged) — it only enriches the admin-facing log

### AC9: Audit / Logging
- **Given** a heartbeat is received
- **Then** it is **not** written to `AuditLog` (high volume, low security value)
- **And** repeated authentication failures on the heartbeat endpoint follow the same rate-limit / log-once pattern as `/api/v1/scans`

### AC10: Storage Strategy
- **Given** heartbeats arrive every 60s per device
- **Then** only the **latest snapshot** is retained on the `Device` row (overwrite, not append)
- **And** no separate `DeviceHeartbeat` history table is added in this story (deferred — see "Out of Scope")

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Heartbeat from revoked device | `401 Unauthorized`, no `LastSeenAt` update |
| Clock skew (clientTimestamp far from server time) | Use server-received time as authoritative; record `clientTimestamp` for diagnostics only |
| Malformed JSON body | `400 Bad Request`, do not update `LastSeenAt` |
| Scanner offline for hours then reconnects | First heartbeat flips status to `Online`; no backfill of missed time |
| Server restart | All devices appear `Offline` until next heartbeat (≤60s later) — acceptable |
| Two scanners share an API key (misconfig) | Both update the same `Device` row; status reflects whichever pinged last (existing limitation, surfaced in TRD) |
| Battery / network fields missing or null | Render as "—" in UI, do not error |
| Unusually high `queuedScansCount` (e.g. >50) | UI highlights the field amber as a soft warning |

---

## Test Scenarios

- [ ] Heartbeat endpoint accepts valid payload and returns 204
- [ ] Heartbeat endpoint rejects unknown / revoked API keys with 401
- [ ] `Device.LastSeenAt` updates on each successful heartbeat
- [ ] Status computation: Online / Stale / Offline boundaries respect configured thresholds
- [ ] Devices list shows Health column with correct color badges
- [ ] Devices list auto-refreshes without full page reload
- [ ] Device detail page renders all heartbeat fields
- [ ] Device with no heartbeat shows "No heartbeat recorded yet"
- [ ] `/api/v1/devices/health` returns expected shape and respects auth policy
- [ ] No-Scan-Alert log message differentiates connectivity vs operational suppression
- [ ] Malformed heartbeat payload returns 400 and does not corrupt device state
- [ ] Heartbeat traffic does not generate AuditLog rows
- [ ] Threshold AppSettings changes take effect without app restart

---

## Out of Scope (Deferred)

- Historical heartbeat trend / uptime chart (would require a `DeviceHeartbeat` table — propose as a follow-up story under a future Monitoring epic).
- Push alerts (email/SMS to admin) when a scanner goes Offline. This story only surfaces health in the UI.
- Remote-control actions (reboot scanner, force re-sync) — separate concern.
- Per-scanner SLAs / uptime reporting.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0028](US0028-register-scanner.md) | Functional | Device registration + API keys | Done |
| [US0029](US0029-device-list.md) | Functional | Devices list page to extend with Health column | Done |
| [US0033](US0033-health-check.md) | Reference | Existing `/api/v1/health` (server health) — distinct from this | Done |

### Cross-Repo Dependencies

| Repo | Change | Notes |
|------|--------|-------|
| SmartLogScannerApp | Add `HeartbeatService` (timer + HTTP client) | Sends every 60s while app is in foreground or background-allowed; companion story to be drafted in scanner SDLC |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium

Rough split if this is decomposed during plan phase:
- Heartbeat endpoint + Device snapshot fields: 2 pts
- Admin UI (list column + detail panel + auto-refresh): 2 pts
- No-Scan-Alert log enrichment + tests: 1 pt

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-28 | Claude | Initial draft |
