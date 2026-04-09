# SmartLog Web App — Features

SmartLog is a School Information Management System for Philippine K-12 schools. It runs on the school's local network (LAN-only, offline-capable) and serves as the central hub for attendance, notifications, and records.

---

## User & Access Management

- Role-based access control with 5 roles: **SuperAdmin, Admin, Teacher, Security, Staff**
- Secure login with account lockout (5 failed attempts → 15-minute lockout)
- 10-hour sliding sessions (HttpOnly, Secure, SameSite cookies)
- Per-user profile with profile picture upload
- Password change with forced change on first login
- User activation/deactivation by admins
- Full audit log of user actions (who did what, from which IP)

---

## Student Management

- Add, edit, deactivate, and search students
- Auto-generated Student ID format: `YYYY-GG-NNNN` (year, grade, sequence)
- LRN (Learner Reference Number) tracking
- Assign students to grade level and section
- Store parent/guardian mobile number for SMS notifications
- Per-student SMS language preference (English or Filipino)
- Bulk import students via CSV upload
- Profile picture upload per student
- QR code generation (HMAC-SHA256 signed, printable for ID cards)
- QR code regeneration/invalidation (old code stops working immediately)

---

## Academic Year & Enrollment

- Define and manage academic years (e.g., 2025-2026)
- Mark one year as current; historical years remain accessible
- Manage grade levels (K-12) and sections (name, capacity, adviser)
- Annual batch re-enrollment: promote students, graduate seniors, or skip
- Per-student active enrollment per academic year

---

## Attendance Tracking

- QR code scanning via registered scanner devices (SmartLogScannerApp)
- ENTRY and EXIT scan types
- Duplicate prevention: 5-minute window per student per scan type
- Calendar-aware: scans rejected on holidays, suspensions, and non-school days
- Attendance summary dashboard (by grade, section, date)
- Daily attendance list with search, filter by grade/section/status
- Attendance trend chart (past 30 days)
- Attendance by grade chart
- Recent activity feed

---

## SMS Notifications

- Automated SMS to parents/guardians on student entry/exit
- Bilingual templates: English and Filipino per student preference
- Template codes: `ENTRY`, `EXIT`, `HOLIDAY`, `SUSPENSION`, `EMERGENCY`
- Customizable templates via admin UI
- SMS broadcast: send announcements to filtered groups (all parents, specific grade, section)
- Schedule broadcasts for future delivery
- Cancel pending scheduled broadcasts
- SMS queue with priority ordering and `ScheduledAt` support
- Retry with exponential backoff (2→4→8 minutes, max 3 retries)
- Two gateways:
  - **GSM Modem** — USB serial, offline-capable, ~₱1/SMS
  - **Semaphore** — Cloud API, internet required
- Automatic fallback from GSM Modem to Semaphore on failure
- SMS delivery reports via webhook (Semaphore)
- Full SMS history log with delivery status

---

## Calendar Management

- School calendar with events, holidays, and suspensions
- 13 Philippine national holidays pre-loaded per academic year
- Mark events as "Affects Attendance" to automatically block scans on that day
- Grade-specific events (e.g., Grade 12 activity only)
- Event types: Holiday, School Event, Class Suspension

---

## Scanner Device Management

- Register scanner devices and generate API keys (`sk_live_xxx`)
- API key shown once; stored as SHA-256 hash (cannot be retrieved)
- Activate/deactivate devices; revoked devices immediately rejected
- View last-seen timestamp per device
- Multiple devices supported (one API key each)

---

## Reporting & Export

- **Daily Attendance Report** — Per student for a selected date; export CSV or HTML
- **Weekly Attendance Report** — Week summary; export CSV
- **Monthly Attendance Report** — Month summary; export CSV
- **Student Attendance Report** — Full history for an individual student; export CSV
- **Audit Log Export** — Admin/security action trail (SuperAdmin only); export CSV

---

## Settings & Configuration

- Runtime configuration via **Admin > Settings** (no restart required)
- SMS provider configuration (switch gateway, update API keys, test SMS)
- GSM modem port and baud rate configuration
- HMAC secret rotation (invalidates all existing QR codes)
- Application timezone setting
- QR code settings

---

## System & Security

- Automatic database migration on startup (EF Core)
- Seed data on first run: grade levels, sections, academic year, admin accounts, Philippine national holidays
- CORS configuration for browser-based scanner clients
- HTTPS-ready (configure SSL certificate in production)
- Docker deployment (docker-compose with SQL Server container)
- Windows Service deployment (auto-start on boot, auto-restart on crash)
- Structured logging via Serilog (file + console)
- GitHub Actions CI (build + test on every push)
- GitHub Actions CD (release on version tags)
