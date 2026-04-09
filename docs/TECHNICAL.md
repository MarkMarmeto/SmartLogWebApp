# SmartLog Web App — Technical Reference

## Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8.0 Razor Pages |
| ORM | Entity Framework Core 8.0 |
| Database | SQL Server (Express for on-premise, full for production) |
| Auth | ASP.NET Core Identity (cookie-based) |
| Logging | Serilog (structured, file + console) |
| QR Generation | QRCoder 1.4.3 |
| SMS (GSM) | System.IO.Ports — AT command serial communication |
| SMS (Cloud) | Semaphore HTTP API (Philippines) |
| Containerization | Docker + docker-compose |

---

## Solution Structure

```
SmartLogWebApp/
├── src/SmartLog.Web/
│   ├── Controllers/Api/           # REST API controllers (scanner + dashboard)
│   │   ├── ScansApiController.cs        # POST /api/v1/scans
│   │   ├── AttendanceApiController.cs   # Attendance summary/list
│   │   ├── DashboardApiController.cs    # Dashboard stats
│   │   ├── ReportsApiController.cs      # CSV/HTML exports
│   │   ├── ProfilePictureApiController.cs
│   │   ├── SmsDeliveryReportController.cs  # Webhook
│   │   └── HealthController.cs          # GET /api/v1/health
│   ├── Data/
│   │   ├── ApplicationDbContext.cs      # EF Core DbContext
│   │   ├── Entities/                    # All domain entities
│   │   └── Migrations/                  # EF Core migration history
│   ├── Services/                        # Business logic
│   │   ├── Sms/                         # SMS gateways, queue, templates
│   │   └── *.cs                         # StudentService, AttendanceService, etc.
│   ├── Pages/                           # Razor Pages UI
│   │   ├── Account/                     # Login, Logout, Profile, ChangePassword
│   │   └── Admin/                       # All admin pages
│   │       ├── Sms/                     # SMS management
│   │       ├── Calendar/                # Calendar events
│   │       └── Reports/                 # Attendance reports
│   ├── Middleware/                      # ForcePasswordChangeMiddleware
│   ├── Validation/                      # Model validators
│   ├── wwwroot/                         # Static assets (CSS, JS, images)
│   ├── Program.cs                       # Startup, DI, middleware, auth config
│   └── appsettings.json                 # Base configuration
├── tests/SmartLog.Web.Tests/            # xUnit test suite
├── deploy/                              # Windows deployment scripts
├── docs/                                # This documentation
├── sdlc-studio/                         # Product & engineering docs (PRD, TRD, stories)
├── docker-compose.yml
└── SmartLogWebApp.sln
```

---

## Data Model

| Entity | Purpose | Key Fields |
|---|---|---|
| `Student` | Student records | `StudentId` (YYYY-GG-NNNN), `LRN`, `GradeLevelId`, `SectionId`, `ParentPhone`, `SmsEnabled`, `SmsLanguage` |
| `Faculty` | Teacher/staff records | `EmployeeId` (EMP-YYYY-NNNN), `Department`, `Position`, `UserId` (ASP.NET Identity link) |
| `Device` | Registered scanner devices | `Name`, `ApiKeyHash` (SHA-256), `IsActive`, `LastSeenAt` |
| `Scan` | Attendance scan records | `DeviceId`, `StudentId`, `QrPayload`, `ScannedAt`, `ReceivedAt`, `ScanType` (ENTRY/EXIT), `Status` |
| `QrCode` | Student QR codes | `Payload` (`SMARTLOG:id:ts:hmac`), `HmacSignature`, `IsValid`, `QrImageBase64` |
| `SmsQueue` | Async SMS send queue | `PhoneNumber`, `Message`, `Status`, `Priority`, `MessageType`, `ScheduledAt`, `RetryCount` |
| `SmsLog` | SMS delivery audit trail | `Provider`, `ProviderMessageId`, `DeliveryStatus`, `ProcessingTimeMs` |
| `SmsTemplate` | Bilingual message templates | `Code` (ENTRY/EXIT/HOLIDAY/etc), `TemplateEn`, `TemplateFil`, `AvailablePlaceholders` |
| `GradeLevel` | K-12 grade definitions | `Code` (K, 1–12), `Name`, `SortOrder` |
| `Section` | Class sections | `Name`, `GradeLevelId`, `AdviserId`, `Capacity` |
| `StudentEnrollment` | Year-to-year enrollment | `StudentId`, `SectionId`, `AcademicYearId`, `IsActive` |
| `AcademicYear` | School year periods | `Name` (2025-2026), `StartDate`, `EndDate`, `IsCurrent` |
| `CalendarEvent` | Holidays/suspensions/events | `EventType`, `AffectsAttendance`, `AffectedGrades` |
| `AuditLog` | Security & change audit | `Action`, `UserId`, `PerformedByUserId`, `IpAddress`, `UserAgent` |
| `AppSettings` | Runtime app configuration | `Key`, `Value`, `Category`, `IsSensitive` |
| `SmsSettings` | Runtime SMS configuration | `Key`, `Value`, `Category` |

### ID Formats

- **Student ID:** `YYYY-GG-NNNN` — e.g., `2026-07-0001` (year-grade-sequence)
- **Employee ID:** `EMP-YYYY-NNNN` — e.g., `EMP-2026-0001`
- **API Key:** `sk_live_` prefix + random hex — displayed once, stored as SHA-256 hash

---

## Authentication & Authorization

**Provider:** ASP.NET Core Identity
**Session:** Cookie auth, 10-hour sliding expiration, HttpOnly + Secure + SameSite=Strict
**Lockout:** 5 failed attempts → 15-minute lockout

### Roles

| Role | Description |
|---|---|
| `SuperAdmin` | Full system access, manages settings, users, and all data |
| `Admin` | Day-to-day administration: students, faculty, devices |
| `Teacher` | View attendance and student records |
| `Security` | Gate scanning operations, view attendance |
| `Staff` | View student records |

### Authorization Policies

| Policy | Allowed Roles |
|---|---|
| `RequireSuperAdmin` | SuperAdmin |
| `RequireAdmin` | SuperAdmin, Admin |
| `CanManageUsers` | SuperAdmin, Admin |
| `CanViewStudents` | SuperAdmin, Admin, Teacher, Staff |
| `CanManageStudents` | SuperAdmin, Admin |
| `CanViewAttendance` | SuperAdmin, Admin, Teacher, Security |

### Scanner Device Authentication

Scanner devices authenticate via the `X-API-Key` request header. The submitted key is SHA-256 hashed and compared against `Device.ApiKeyHash` using constant-time comparison to prevent timing attacks.

---

## Core Process Flows

### QR Code Scanning (Attendance)

```
SmartLogScannerApp                       SmartLogWebApp
       │                                       │
       │  POST /api/v1/scans                   │
       │  X-API-Key: sk_live_xxx               │
       │  { qrPayload, scannedAt, scanType }   │
       ├──────────────────────────────────────►│
       │                                       ├─ Authenticate device (API key hash)
       │                                       ├─ Parse QR: SMARTLOG:{id}:{ts}:{hmac}
       │                                       ├─ Verify HMAC-SHA256 (constant-time)
       │                                       ├─ Look up active student
       │                                       ├─ Check calendar (must be school day)
       │                                       ├─ Check duplicate (5-minute window)
       │                                       ├─ Save Scan record
       │                                       ├─ Queue SMS (background)
       │  { scanId, studentName, grade,        │
       │    section, status: ACCEPTED }        │
       │◄──────────────────────────────────────┤
```

**Rejection statuses:** `REJECTED_INVALID_QR`, `REJECTED_QR_INVALIDATED`, `REJECTED_STUDENT_INACTIVE`, `REJECTED_NOT_SCHOOL_DAY`, `DUPLICATE`

### SMS Notification Flow

```
Scan Accepted
    │
    ▼
QueueAttendanceNotificationAsync()
    ├─ Check SMS globally enabled
    ├─ Look up student (phone, language preference)
    ├─ Render template (ENTRY or EXIT, EN or FIL)
    ├─ Check 5-min duplicate window
    └─ Insert into SmsQueue (Pending)
           │
           ▼
    SmsWorkerService (polls every 5s)
    ├─ Fetch Pending messages (priority order, respects ScheduledAt)
    ├─ Select gateway: GSM_MODEM → SEMAPHORE (fallback)
    ├─ Send via gateway
    ├─ Success: status=Sent, log to SmsLog
    └─ Failure: exponential backoff (2→4→8 min), max 3 retries
```

**SMS Gateways:**
- **GSM Modem** — USB serial AT commands; offline-capable; ~₱1/SMS
- **Semaphore** — Cloud HTTP API (api.semaphore.co); requires internet

### Student Enrollment Flow

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

## QR Code Format

```
SMARTLOG:{studentId}:{timestamp}:{HMAC-SHA256-base64}
```

Example: `SMARTLOG:2026-07-0001:1739512547:a7Bx9kL2mN4pQ6rS8tU0==`

- **Prefix:** always `SMARTLOG`
- **studentId:** matches `Student.StudentId`
- **timestamp:** Unix epoch seconds at QR generation time
- **HMAC:** Base64-encoded `HMAC-SHA256(studentId:timestamp)` using `SMARTLOG_HMAC_SECRET_KEY`

The HMAC secret is shared between the Web App (QR generation) and optionally the Scanner App (local pre-validation). The timestamp in the QR code does not expire — deduplication is server-side.

---

## Configuration

### Environment Variables

| Variable | Purpose |
|---|---|
| `SMARTLOG_DB_CONNECTION` | SQL Server connection string |
| `SMARTLOG_HMAC_SECRET_KEY` | QR code HMAC signing secret (keep confidential) |
| `SMARTLOG_SEED_PASSWORD` | Initial password for all seeded admin accounts |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |
| `ASPNETCORE_URLS` | Listen URL/port (e.g., `http://+:5050`) |
| `Cors__AllowedOrigins__0` | CORS origin for browser-based scanner clients |

### appsettings.json (SMS section)

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

### Runtime Database Config

- `AppSettings` table — General config (timezone, QR settings, etc.)
- `SmsSettings` table — SMS provider config (modifiable via Admin > Settings without restart)

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

### Create & Apply Migration

```bash
dotnet ef migrations add <MigrationName> -p src/SmartLog.Web
dotnet ef database update -p src/SmartLog.Web
```

### Docker

```bash
# Start (builds and applies migrations automatically)
docker-compose up --build -d

# View logs
docker-compose logs -f smartlog-web

# Stop
docker-compose down

# Full reset (destroys data)
docker-compose down -v && docker-compose up --build -d
```

---

## Key NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0 | ORM + SQL Server driver |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 8.0 | Auth + user management |
| `QRCoder` | 1.4.3 | QR code image generation |
| `Serilog.AspNetCore` | 8.0 | Structured logging |
| `System.IO.Ports` | 8.0 | GSM modem serial communication |
