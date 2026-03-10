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
│   │   └── *.cs                        # All other services
│   ├── Pages/                          # Razor Pages UI
│   │   ├── Account/                    # Login, Logout, Profile, ChangePassword
│   │   └── Admin/                      # All admin pages
│   │       ├── Sms/                    # SMS management pages
│   │       ├── Calendar/               # Calendar event management
│   │       ├── Reports/                # Attendance reports
│   │       └── *.cshtml                # Student, Faculty, Device, QR management
│   ├── Middleware/                      # ForcePasswordChangeMiddleware
│   ├── wwwroot/                        # Static files (CSS, JS, images)
│   ├── Migrations/                     # EF Core migrations
│   ├── Program.cs                      # Startup, DI, middleware, auth config
│   └── appsettings.json                # Configuration
└── tests/SmartLog.Web.Tests/           # Unit tests (19 tests)
```

---

## Data Model (Key Entities)

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| `Student` | Student records | StudentId (YYYY-GG-NNNN), LRN, GradeLevel, Section, ParentPhone, SmsEnabled, SmsLanguage |
| `Faculty` | Teacher/staff records | EmployeeId (EMP-YYYY-NNNN), Department, Position, UserId (link to Identity) |
| `Device` | Scanner device registration | Name, ApiKeyHash (SHA-256), IsActive, LastSeenAt |
| `Scan` | QR code scan records | DeviceId, StudentId, QrPayload, ScannedAt, ReceivedAt, ScanType (ENTRY/EXIT), Status |
| `QrCode` | Student QR codes | Payload (SMARTLOG:id:timestamp:hmac), HmacSignature, IsValid, QrImageBase64 |
| `SmsQueue` | Async SMS queue | PhoneNumber, Message, Status, Priority, MessageType, ScheduledAt, RetryCount |
| `SmsLog` | SMS delivery audit | Provider, ProviderMessageId, DeliveryStatus, ProcessingTimeMs |
| `SmsTemplate` | Bilingual templates | Code (ENTRY/EXIT/HOLIDAY/etc), TemplateEn, TemplateFil, AvailablePlaceholders |
| `GradeLevel` | K-12 grade levels | Code (K,1-12), Name, SortOrder |
| `Section` | Class sections | Name, GradeLevelId, AdviserId, Capacity |
| `StudentEnrollment` | Year-based enrollment | StudentId, SectionId, AcademicYearId, IsActive |
| `AcademicYear` | School years | Name (2025-2026), StartDate, EndDate, IsCurrent |
| `CalendarEvent` | School calendar | EventType (Holiday/Event/Suspension), AffectsAttendance, AffectedGrades |
| `AuditLog` | Security audit trail | Action, UserId, PerformedByUserId, IpAddress, UserAgent |
| `AppSettings` | Dynamic config | Key, Value, Category, IsSensitive |
| `SmsSettings` | SMS-specific config | Key, Value, Category |

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

### 2. SMS Notification Flow

```
Scan Accepted
    │
    ▼
QueueAttendanceNotificationAsync()
    ├─ Check SMS globally enabled
    ├─ Lookup student (phone, SMS language)
    ├─ Render template (ENTRY or EXIT, EN or FIL)
    ├─ Check duplicate (5-min window)
    └─ Insert into SmsQueue (status: Pending)
           │
           ▼
    SmsWorkerService (polls every 5s)
    ├─ Fetch Pending messages (priority-ordered, respects ScheduledAt)
    ├─ Select gateway: GSM_MODEM (default) → SEMAPHORE (fallback)
    ├─ Send via gateway
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

---

## Connection to SmartLogScannerApp

SmartLogScannerApp is the **external scanner client** (separate application) that submits QR code scans to this web app.

### Integration Points

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `POST /api/v1/scans` | Submit QR scan | `X-API-Key` header |
| `GET /api/v1/health` | Check server connectivity | None |

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

### Scanner APIs (Device Auth)
- `POST /api/v1/scans` — Submit QR scan
- `GET /api/v1/health` — Health check (no auth)

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
