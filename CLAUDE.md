# SmartLog Web Application

## Project Overview

SmartLog is a **School Information Management System** built for Philippine K-12 schools. It tracks student attendance via QR code scanning, sends SMS notifications to parents, and provides administrative tools for managing students, faculty, academic years, and reporting.

**Stack:** ASP.NET Core 8.0 Razor Pages + EF Core + SQL Server + Serilog
**Architecture:** Monolithic web app with REST APIs for scanner device integration
**Deployment:** Docker-ready, designed for offline-first LAN operation

---

## Repository Structure

```
SmartLogWebApp/
├── src/SmartLog.Web/
│   ├── Controllers/Api/          # REST API controllers
│   │   ├── ScansApiController.cs       # QR scan ingestion (scanner devices)
│   │   ├── AttendanceApiController.cs  # Attendance summary/list
│   │   ├── DashboardApiController.cs   # Dashboard stats
│   │   ├── ReportsApiController.cs     # Report exports (CSV/HTML)
│   │   ├── ProfilePictureApiController.cs
│   │   ├── SmsDeliveryReportController.cs  # Webhook for SMS delivery status
│   │   └── HealthController.cs         # Health check for scanners
│   ├── Data/
│   │   ├── ApplicationDbContext.cs     # EF Core DbContext with all entity configs
│   │   └── Entities/                   # All entity classes
│   ├── Services/                       # Business logic layer
│   │   ├── Sms/                        # SMS subsystem (gateways, queue, templates)
│   │   ├── Retention/                  # Data retention subsystem (EP0017)
│   │   └── *.cs                        # All other services
│   ├── Models/
│   │   └── Sms/                        # SMS view/routing models (BroadcastMessageBodies, ProgramGradeFilter, etc.)
│   ├── Pages/                          # Razor Pages UI
│   │   ├── Account/                    # Login, Logout, Profile, ChangePassword
│   │   └── Admin/                      # All admin pages
│   │       ├── Sms/                    # SMS management pages
│   │       ├── Calendar/               # Calendar event management
│   │       ├── Reports/                # Attendance reports
│   │       ├── Settings/               # Retention policy configuration
│   │       └── *.cshtml                # Student, Faculty, Device, QR management
│   ├── Middleware/                      # ForcePasswordChangeMiddleware
│   ├── wwwroot/                        # Static files (CSS, JS, images)
│   ├── Migrations/                     # EF Core migrations
│   ├── Program.cs                      # Startup, DI, middleware, auth config
│   └── appsettings.json                # Configuration
└── tests/SmartLog.Web.Tests/           # xUnit tests (~302 tests across 27 test files)
```

---

## Data Model (Key Entities)

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| `Student` | Student records | StudentId (YYYY-GG-NNNN), LRN, GradeLevel, Section, Program (denormalized), ParentPhone, AlternatePhone, SmsEnabled, EntryExitSmsEnabled (default false), SmsLanguage |
| `Faculty` | Teacher/staff records | EmployeeId (EMP-YYYY-NNNN), Department, Position, UserId (link to Identity) |
| `Device` | Scanner device registration | Name, ApiKeyHash (SHA-256), IsActive, LastSeenAt |
| `Scan` | QR code scan records | DeviceId, StudentId, QrPayload, ScannedAt, ReceivedAt, ScanType (ENTRY/EXIT), Status |
| `QrCode` | Student QR codes | Payload (SMARTLOG:id:timestamp:hmac), HmacSignature, IsValid, InvalidatedAt, QrImageBase64 |
| `Program` | K-12 program/strand | Code (REGULAR/SPA/STEM/ABM/etc), Name, SortOrder, IsActive — mandatory for all Sections |
| `GradeLevelProgram` | Program ↔ GradeLevel allowed mapping | GradeLevelId, ProgramId |
| `VisitorPass` | Reusable anonymous visitor passes | PassCode (VISITOR-NNN), Status (Available/InUse), QrPayload (SMARTLOG-V: prefix), HmacSignature |
| `VisitorScan` | Visitor entry/exit records | VisitorPassId, DeviceId, ScanType, ScannedAt |
| `Broadcast` | One-shot admin SMS broadcasts | Type (Announcement/Emergency/BulkSend), Message, TargetFilter, ScheduledAt, Status |
| `SmsQueue` | Async SMS queue | PhoneNumber, Message, Provider, Status, Priority, MessageType (ENTRY/EXIT/NO_SCAN_ALERT/PERSONAL/BROADCAST), ScheduledAt, RetryCount |
| `SmsLog` | SMS delivery audit | Provider, ProviderMessageId, DeliveryStatus, ProcessingTimeMs |
| `SmsTemplate` | Bilingual templates | Code (ENTRY/EXIT/NO_SCAN_ALERT/HOLIDAY/etc), TemplateEn, TemplateFil, AvailablePlaceholders |
| `GradeLevel` | K-12 grade levels | Code (K,1-12,NG), Name, SortOrder |
| `Section` | Class sections | Name, GradeLevelId, **ProgramId (mandatory)**, AdviserId, Capacity |
| `StudentEnrollment` | Year-based enrollment | StudentId, SectionId, AcademicYearId, IsActive |
| `AcademicYear` | School years | Name (2025-2026), StartDate, EndDate, IsCurrent |
| `CalendarEvent` | School calendar | EventType (Holiday/Event/Suspension), AffectsAttendance, AffectedGrades |
| `AuditLog` | Security audit trail | Action, UserId, PerformedByUserId, IpAddress, UserAgent |
| `AppSettings` | Dynamic config | Key, Value, Category, IsSensitive |
| `SmsSettings` | SMS-specific config | Key, Value, Category (e.g. `Sms:NoScanAlertEnabled`, `Sms:NoScanAlertTime`, `Sms:NoScanAlertProvider`, `Sms:DefaultProvider`) |
| `RetentionPolicy` | Per-entity retention config | EntityName (SmsLog/SmsQueue/Broadcast/Scan/AuditLog/VisitorScan), RetentionDays, Enabled, ArchiveEnabled, LastRunAt, LastRowsAffected |
| `RetentionRun` | Audit log for each retention execution | EntityName, RunMode (Scheduled/Manual/DryRun), StartedAt, CompletedAt, Status (Success/Failed/Partial), RowsAffected, TriggeredBy |

---

## Authentication & Authorization

**Provider:** ASP.NET Core Identity with cookie auth (10hr sliding expiration)

**Roles:** SuperAdmin, Admin, Teacher, Security, Staff

**Policies:**
- `RequireSuperAdmin` — SuperAdmin only
- `RequireAdmin` — SuperAdmin + Admin
- `CanManageUsers` — SuperAdmin + Admin
- `CanViewStudents` — SuperAdmin + Admin + Teacher + Staff
- `CanManageStudents` — SuperAdmin + Admin
- `CanViewAttendance` — SuperAdmin + Admin + Teacher + Security

**API Auth:** Scanner devices use `X-API-Key` header with SHA-256 hashed keys and constant-time comparison.

---

## Core Process Flows

### 1. QR Code Scanning (Attendance)

```
SmartLogScannerApp                    SmartLogWebApp
       │                                    │
       │  POST /api/v1/scans               │
       │  X-API-Key: sk_live_xxx           │
       │  {qrPayload, scannedAt,           │
       │   scanType: ENTRY|EXIT}           │
       ├──────────────────────────────────►│
       │                                    ├─ Authenticate device (API key hash lookup)
       │                                    ├─ Parse QR: SMARTLOG:{studentId}:{timestamp}:{hmac}
       │                                    ├─ Verify HMAC-SHA256 (constant-time comparison)
       │                                    ├─ Lookup student (must be active)
       │                                    ├─ Check calendar (must be school day)
       │                                    ├─ Check duplicate (5-min window)
       │                                    ├─ Save Scan record
       │                                    ├─ Queue SMS notification (background)
       │  {scanId, studentName, grade,     │
       │   section, status: ACCEPTED}      │
       │◄──────────────────────────────────┤
```

**Rejection statuses:** `REJECTED_INVALID_QR`, `REJECTED_QR_INVALIDATED`, `REJECTED_STUDENT_INACTIVE`, `REJECTED_NOT_SCHOOL_DAY`, `DUPLICATE`

**Visitor QR branch:** If the payload starts with `SMARTLOG-V:` it is routed to `VisitorPassService` — looks up `VisitorPass` by code, verifies HMAC, records a `VisitorScan`, returns neutral visitor info (no student data, no SMS).

### 2. SMS Notification Flow (V2 — No-Scan Alert default)

Default attendance SMS strategy since EP0009 is **end-of-day No-Scan Alert**, not per-scan. Entry/Exit SMS is opt-in per student via `Student.EntryExitSmsEnabled = true` (default false).

```
A. Per-scan (opt-in only)
Scan Accepted
    │
    ▼
QueueAttendanceNotificationAsync()
    ├─ Check SMS globally enabled (Sms:Enabled)
    ├─ Check Student.EntryExitSmsEnabled == true (else skip)
    ├─ Lookup student (phone, SMS language)
    ├─ Render template (ENTRY or EXIT, EN or FIL)
    ├─ Check duplicate (5-min window)
    └─ Insert into SmsQueue (MessageType=ENTRY|EXIT, status: Pending)

B. End-of-Day No-Scan Alert (default broadcast)
NoScanAlertService (IHostedService, runs daily at Sms:NoScanAlertTime, default 18:10)
    ├─ Check global SMS enabled + Sms:NoScanAlertEnabled
    ├─ Check today is a school day (CalendarService)
    ├─ Scanner-health guard: require ≥1 accepted scan system-wide today (else suppress + alert admin)
    ├─ Query students with zero accepted scans for today
    ├─ Idempotency guard: skip students already alerted today
    └─ Queue NO_SCAN_ALERT SMS with Provider = Sms:NoScanAlertProvider

C. Admin broadcasts & personal SMS
Announcement / Emergency / BulkSend / Personal (from student profile)
    └─ Insert into SmsQueue (MessageType=BROADCAST|PERSONAL, Provider = Sms:DefaultProvider)
           │
           ▼
    SmsWorkerService (polls every 5s)
    ├─ Fetch Pending messages (priority-ordered, respects ScheduledAt)
    ├─ Respect pre-set Provider on message; else use Sms:DefaultProvider
    ├─ Send via gateway; Sms:FallbackEnabled toggles GSM→Semaphore fallback
    ├─ On success: status=Sent, log to SmsLog with ProviderMessageId
    └─ On failure: retry with exponential backoff (2, 4, 8 min), max 3 retries
```

**SMS Gateways:**
- **GSM Modem** — Serial port AT commands, offline-capable, ~P1/SMS
- **Semaphore** — Cloud HTTP API (api.semaphore.co), requires internet

### 3. Student Enrollment Flow

```
Admin creates Student
    ├─ Auto-generate StudentId (YYYY-GG-NNNN)
    ├─ Save to Students table
    └─ Generate QR code (HMAC-SHA256 signed)

Annual Batch Re-enrollment
    ├─ Select source → target academic year
    ├─ Preview all students with promotion options
    ├─ Admin assigns: Promote / Graduate / Skip
    └─ Execute: deactivate old enrollment, create new enrollment
```

### 4. Visitor Pass Flow (EP0012)

Reusable anonymous passes issued by admin; no PII, no SMS. Admin-configurable pool size (default 20).

```
Admin creates pool → VisitorPass rows (PassCode=VISITOR-001..N, Status=Available, HMAC-signed SMARTLOG-V: QR)
    │
    ▼
Guard hands pass to visitor → Scanner scans SMARTLOG-V: QR
    ├─ ScansApiController routes to VisitorPassService
    ├─ Record VisitorScan (ENTRY)
    └─ Pass stays Available (not tied to a specific person)

Visitor returns pass → Guard scans at EXIT
    ├─ Record VisitorScan (EXIT)
    └─ Pass ready for next visitor
```

**QR prefix `SMARTLOG-V:`** is what distinguishes visitor passes from student QRs. QR generation for passes uses the same HMAC secret as student QRs but a different payload format.

### 5. SMS Broadcast Language Routing

`BroadcastMessageBodies` (in `Models/Sms/`) holds the language mode and both message bodies for a broadcast. Two key methods:

- **`ShouldSendToStudent(smsLanguage)`** — controls which students receive the message based on `BroadcastLanguageMode`:
  - `EnglishOnly` → skip students with explicit `"FIL"` preference (null = include)
  - `FilipinoOnly` → skip students with explicit `"EN"` preference (null = include)
  - `Both` → send to all students
- **`GetBodyForLanguage(smsLanguage)`** — returns `FilipinoBody` if student is FIL-preference and `FilipinoBody` is non-empty; otherwise returns `EnglishBody`

`BroadcastLanguageMode` enum: `EnglishOnly`, `FilipinoOnly`, `Both` (default).

### 6. Data Retention (EP0017)

`RetentionService` (`BackgroundService`) runs all enabled `IEntityRetentionHandler` implementations daily at a configurable UTC time stored in `AppSettings` key `Retention:RunTime`. A per-entity daily idempotency guard (checked via `RetentionRun`) prevents double-deletion on Scheduled runs.

**Entities with retention handlers:** `SmsLog`, `SmsQueue`, `Broadcast`, `Scan`, `AuditLog`, `VisitorScan`

Each handler:
1. Reads its `RetentionPolicy` row to get `RetentionDays` and `ArchiveEnabled`
2. If `ArchiveEnabled`, calls `IArchiveService.ArchiveBatchAsync<T>()` (implemented by `CsvArchiveService`) — writes RFC-4180 CSV + schema JSON to the archive directory
3. Batch-deletes rows older than the retention window (1,000 rows/batch for SmsLog/SmsQueue, 100 for others; 50ms yield between batches)
4. Writes a `RetentionRun` record with status, row count, and duration

Admins can view policy settings, trigger Manual or DryRun runs, and see per-entity last-run status at `/Admin/Settings/Retention`.

Archive files are written to `Retention:ArchiveDirectory` (from `AppSettings`), organized as `{ArchiveDirectory}/{entity}/{yyyy-MM}/{entity}-{yyyy-MM-dd}.csv`. See `docs/retention-archive-restore.md` for the restore guide.

### 7. QR Regeneration & Invalidation (EP0013)

Student `StudentId` is permanent — the QR is regenerated only when a card is lost or re-issued.

```
Regenerate QR
    ├─ Mark old QrCode.IsValid = false, set InvalidatedAt (keeps row for audit)
    └─ Create new QrCode row with fresh timestamp + HMAC

Invalidate without regeneration
    └─ Mark IsValid = false, InvalidatedAt set; future scans of old QR return REJECTED_QR_INVALIDATED
```

---

## Connection to SmartLogScannerApp

SmartLogScannerApp is the **external scanner client** (separate application) that submits QR code scans to this web app.

### Integration Points

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `POST /api/v1/scans` | Submit QR scan | `X-API-Key` header |
| `POST /api/v1/devices/heartbeat` | Scanner vitals push | `X-API-Key` header |
| `GET /api/v1/health` | Liveness / setup-validation / clock sync | Optional `X-API-Key` (auth-aware response) |

### Scanner Device Setup

1. Admin registers device at `/Admin/RegisterDevice` → gets API key (`sk_live_xxx`)
2. Scanner app stores API key and server URL
3. Scanner reads QR code from student ID card
4. Scanner POSTs to `/api/v1/scans` with QR payload
5. Server responds with student info + acceptance status
6. Scanner displays result to student/guard

### QR Code Format

```
SMARTLOG:2026-07-0001:1739512547:BASE64_HMAC_SIGNATURE
   │          │            │              │
   prefix     studentId    timestamp      HMAC-SHA256(studentId:timestamp)
```

**HMAC Secret:** Set via `SMARTLOG_HMAC_SECRET_KEY` env var (shared between web app and QR generation). The scanner app does NOT need this secret — it just reads and forwards the QR payload.

### CORS Configuration

Scanner origins configured in `appsettings.json` under `Cors:AllowedOrigins` or via env var `Cors__AllowedOrigins__0`. Only GET and POST methods allowed.

---

## API Reference

### Scanner APIs
- `POST /api/v1/scans` — Submit QR scan (X-API-Key required)
- `POST /api/v1/devices/heartbeat` — Scanner vitals push (X-API-Key required)
- `GET  /api/v1/health` — **Auth-aware** (US0121):
  - No `X-API-Key` → 200 with minimal liveness (`status`, `serverTime`, `version`); no DB hit
  - Valid `X-API-Key` → 200 with full payload (`database`, `scanners`); updates `Device.LastSeenAt`
  - Present-but-invalid `X-API-Key` → 401 `InvalidApiKey`
  - DB unreachable on auth path → 503 with `serverTime`/`version` still populated
- `GET  /api/v1/health/details` *(deprecated — shim delegating to /health; will be removed after scanner US0132 rollout)*
- `GET  /api/v1/health/time` *(deprecated — `serverTime` now in /health response; will be removed after scanner US0132 rollout)*

### Dashboard APIs (Cookie Auth)
- `GET /api/v1/dashboard/summary`
- `GET /api/v1/dashboard/attendance-trend?days=30`
- `GET /api/v1/dashboard/attendance-by-grade?date=`
- `GET /api/v1/dashboard/attendance-by-weekday?weeks=4`
- `GET /api/v1/dashboard/recent-activity?count=10`

### Attendance APIs (Cookie Auth, CanViewStudents)
- `GET /api/v1/attendance/summary?date=&grade=&section=`
- `GET /api/v1/attendance/list?date=&grade=&section=&search=&status=&page=1&pageSize=50`

### Report APIs (Cookie Auth, CanViewStudents)
- `GET /api/v1/reports/daily/export?date=&grade=&section=&format=csv`
- `GET /api/v1/reports/weekly/export?startDate=&format=csv`
- `GET /api/v1/reports/monthly/export?year=&month=&format=csv`
- `GET /api/v1/reports/student/{id}/export?format=csv`
- `GET /api/v1/reports/audit-logs/export` (SuperAdmin only)

### Profile Picture APIs (Cookie Auth)
- `POST /api/v1/profile-picture/user`
- `POST /api/v1/profile-picture/student/{id}` (CanManageStudents)
- `POST /api/v1/profile-picture/faculty/{id}` (CanManageFaculty)
- `DELETE` variants for each

### SMS Webhook (Public)
- `POST /api/sms/delivery-report` — Provider delivery callback (optional `X-Webhook-Secret`)

---

## Configuration

### Environment Variables (highest priority)
- `SMARTLOG_DB_CONNECTION` — SQL Server connection string
- `SMARTLOG_HMAC_SECRET_KEY` — QR code HMAC signing secret
- `SMARTLOG_SEED_PASSWORD` — Initial admin password on first run
- `ASPNETCORE_ENVIRONMENT` — Development or Production
- `Cors__AllowedOrigins__0` — Scanner device CORS origin

### appsettings.json
```json
{
  "Sms": {
    "Enabled": true,
    "DefaultProvider": "GSM_MODEM",
    "FallbackEnabled": true,
    "GsmModem": { "PortName": "COM3", "BaudRate": 9600, "SendDelayMs": 3000 },
    "Semaphore": { "ApiKey": "", "SenderName": "SmartLog" },
    "Queue": { "MaxRetries": 3, "PollingIntervalSeconds": 5 }
  }
}
```

### Database-Driven Config
- `AppSettings` table — General app config (timezone, QR settings, etc.)
- `SmsSettings` table — SMS provider config (can be changed at runtime via admin UI)

---

## Development

### Build & Run
```bash
dotnet build
dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"
```

### Run Tests
```bash
dotnet test
```

### Create Migration
```bash
dotnet ef migrations add MigrationName -p src/SmartLog.Web
dotnet ef database update -p src/SmartLog.Web
```

### Default Roles Seeded on Startup
SuperAdmin, Admin, Teacher, Security, Staff

### Key NuGet Packages
- `Microsoft.EntityFrameworkCore.SqlServer` (8.0)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (8.0)
- `QRCoder` (1.4.3) — QR code image generation
- `Serilog.AspNetCore` (8.0) — Structured logging
- `System.IO.Ports` (8.0) — GSM modem serial communication
