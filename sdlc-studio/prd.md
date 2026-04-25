# Product Requirements Document

**Project:** SmartLog School Information Management System
**Version:** 1.0.0
**Last Updated:** 2026-02-03
**Status:** Draft

---

## 1. Project Overview

### Product Name
SmartLog - School Information Management System (Admin Web App)

### Purpose
An offline-first, LAN-only School Information Management System designed for schools with unreliable internet connectivity. The system enables secure QR-based attendance tracking, comprehensive user management, and automated SMS notifications to parents.

### Tech Stack
- **Framework:** ASP.NET Core 8.0 with Razor Pages
- **ORM:** Entity Framework Core
- **Database:** SQL Server Express (containerized)
- **Authentication:** ASP.NET Identity with optional 2FA
- **Background Services:** Worker Service for SMS queue processing
- **QR Security:** HMAC-SHA256 signed QR codes
- **Containerization:** Docker with Docker Compose

### Architecture Pattern
- **Pattern:** Monolithic with modular design (future microservices-ready)
- **Deployment:** Docker containers on-premise (Linux or Windows host)
- **Containers:** Web App + SQL Server Express (docker-compose)
- **Data Flow:** Server-authoritative with client offline caching support
- **Security:** Defense-in-depth with audit logging at all layers

---

## 2. Problem Statement

### Problem Being Solved
Schools face multiple challenges with traditional attendance and information management:
1. **Manual attendance tracking** - Paper-based systems are slow, error-prone, and difficult to audit
2. **No offline capability** - Existing digital solutions fail when internet is unreliable
3. **Poor parent communication** - No real-time notifications about student arrivals/departures
4. **Security gaps** - No verifiable identity for students at school gates
5. **Audit compliance** - Difficulty maintaining accountability records

### Target Users
| User Type | Description | Primary Actions |
|-----------|-------------|-----------------|
| Super Admin | System administrator (IT/multi-school) | System config, school management, user provisioning |
| Admin | School administrator | User management, reports, SMS config |
| Teacher | Classroom teachers | View attendance, student info |
| Security | Gate/entry staff | Scan QR codes, verify identity |
| Staff | General school staff | Limited view access |

### Context
This is a greenfield project targeting schools in areas with unreliable internet connectivity. The solution must work entirely within a school's LAN while providing enterprise-grade security and audit capabilities. The WPF Scanner app (separate project) will handle offline QR scanning with SQLite queuing.

---

## 3. Feature Inventory

| Feature | Description | Status | Priority | Phase |
|---------|-------------|--------|----------|-------|
| FT-001: User Authentication | Login with username/password + optional 2FA | Not Started | Must-Have | 1 |
| FT-002: Role-Based Access Control | Five-tier role system with permissions | Not Started | Must-Have | 1 |
| FT-003: User Management | CRUD for Admin, Teacher, Security, Staff accounts | Not Started | Must-Have | 1 |
| FT-004: Student Management | CRUD for student records | Not Started | Must-Have | 1 |
| FT-005: Faculty Management | CRUD for faculty/teacher records | Not Started | Must-Have | 1 |
| FT-006: QR Code Generation | Generate HMAC-signed QR codes for students | Not Started | Must-Have | 1 |
| FT-007: Device Authentication | Register and authenticate scanner devices | Not Started | Should-Have | 2 |
| FT-008: Scan Ingestion API | Idempotent endpoint for receiving scans | Not Started | Should-Have | 2 |
| FT-009: Attendance Dashboard | Real-time attendance monitoring | Not Started | Should-Have | 2 |
| FT-010: SMS Configuration | Templates, contacts, notification rules | Not Started | Should-Have | 3 |
| FT-011: SMS Worker Service | Background processing of SMS queue | Not Started | Should-Have | 3 |
| FT-012: Reporting | Attendance reports, audit logs | Not Started | Should-Have | 3 |

### Feature Details

#### FT-001: User Authentication

**User Story:** As a school staff member, I want to log in securely so that I can access the system based on my role.

**Acceptance Criteria:**
- [ ] Users can log in with username and password
- [ ] Failed login attempts are rate-limited (5 attempts, then 15-minute lockout)
- [ ] Optional TOTP-based 2FA can be enabled per user
- [ ] Password requirements: minimum 8 characters, 1 uppercase, 1 number, 1 special character
- [ ] Sessions expire after 8 hours of inactivity
- [ ] All login attempts are audit logged

**Dependencies:** None
**Status:** Not Started
**Confidence:** [HIGH]

---

#### FT-002: Role-Based Access Control

**User Story:** As an administrator, I want to assign roles to users so that they only access features appropriate to their position.

**Acceptance Criteria:**
- [ ] System supports five roles: Super Admin, Admin, Teacher, Security, Staff
- [ ] Permissions are defined per role (see Permission Matrix below)
- [ ] Role changes are audit logged
- [ ] Super Admin can manage all schools (multi-tenant ready)
- [ ] Admin can only manage users within their school

**Permission Matrix:**

| Permission | Super Admin | Admin | Teacher | Security | Staff |
|------------|-------------|-------|---------|----------|-------|
| Manage Schools | Yes | No | No | No | No |
| Manage Users | Yes | Yes | No | No | No |
| Manage Students | Yes | Yes | View | View | No |
| Manage Faculty | Yes | Yes | View | No | No |
| Generate QR Codes | Yes | Yes | No | No | No |
| View Attendance | Yes | Yes | Yes | View Own | No |
| Configure SMS | Yes | Yes | No | No | No |
| View Audit Logs | Yes | Yes | No | No | No |
| Scan QR Codes | No | No | No | Yes | No |

**Dependencies:** FT-001
**Status:** Not Started
**Confidence:** [HIGH]

---

#### FT-003: User Management

**User Story:** As an administrator, I want to create and manage user accounts so that staff can access the system.

**Acceptance Criteria:**
- [ ] Admin can create new users with role assignment
- [ ] Admin can edit user details (name, email, phone, role)
- [ ] Admin can activate/deactivate users (soft delete)
- [ ] Admin can reset user passwords
- [ ] Admin can enable/disable 2FA for users
- [ ] User list supports search and filtering by role
- [ ] All changes are audit logged

**Dependencies:** FT-001, FT-002
**Status:** Not Started
**Confidence:** [HIGH]

---

#### FT-004: Student Management

**User Story:** As an administrator, I want to manage student records so that they can be tracked in the system.

**Acceptance Criteria:**
- [ ] Admin can create student records (name, student ID, grade/section, parent contact)
- [ ] Admin can edit student information
- [ ] Admin can activate/deactivate students (soft delete)
- [ ] Student list supports search by name, ID, grade/section
- [ ] Admin can bulk import students via CSV
- [ ] Each student gets a unique QR code upon creation
- [ ] Parent SMS contact is validated (format check)

**Dependencies:** FT-002, FT-006
**Status:** Not Started
**Confidence:** [HIGH]

---

#### FT-005: Faculty Management

**User Story:** As an administrator, I want to manage faculty records so that teacher information is centralized.

**Acceptance Criteria:**
- [ ] Admin can create faculty records (name, employee ID, department, contact)
- [ ] Admin can edit faculty information
- [ ] Admin can activate/deactivate faculty (soft delete)
- [ ] Faculty list supports search by name, ID, department
- [ ] Faculty can optionally be linked to a user account
- [ ] All changes are audit logged

**Dependencies:** FT-002
**Status:** Not Started
**Confidence:** [HIGH]

---

#### FT-006: QR Code Generation

**User Story:** As an administrator, I want to generate secure QR codes for students so that their identity can be verified at scan points.

**Acceptance Criteria:**
- [ ] QR codes contain: student ID, timestamp, HMAC signature
- [ ] HMAC uses SHA-256 with server-side secret key
- [ ] QR codes can be regenerated (invalidates old codes)
- [ ] QR codes can be printed individually or in bulk (PDF)
- [ ] QR code format: `SMARTLOG:{studentId}:{timestamp}:{hmac}`
- [ ] Invalid/tampered QR codes are rejected by scanner

**Dependencies:** FT-004
**Status:** Not Started
**Confidence:** [HIGH]

---

## 4. Functional Requirements

### Core Behaviours

**Authentication Flow:**
1. User enters credentials
2. System validates against Identity store
3. If 2FA enabled, prompt for TOTP code
4. On success, create session and redirect to dashboard
5. On failure, increment attempt counter and log

**QR Code Signing:**
1. Generate payload: `{studentId}|{issuedAt}`
2. Compute HMAC-SHA256 with secret key
3. Encode as: `SMARTLOG:{studentId}:{timestamp}:{hmacBase64}`
4. Generate QR image from encoded string

### Input/Output Specifications

**User Creation Input:**
```json
{
  "username": "string (required, unique)",
  "email": "string (required, valid email)",
  "firstName": "string (required)",
  "lastName": "string (required)",
  "role": "enum (SuperAdmin|Admin|Teacher|Security|Staff)",
  "phoneNumber": "string (optional)"
}
```

**Student Creation Input:**
```json
{
  "studentId": "string (required, unique)",
  "firstName": "string (required)",
  "lastName": "string (required)",
  "gradeLevel": "string (required)",
  "section": "string (required)",
  "parentName": "string (required)",
  "parentPhone": "string (required, valid phone)"
}
```

### Business Logic Rules

1. **Username uniqueness** - Usernames must be unique across the system
2. **Student ID uniqueness** - Student IDs must be unique within a school
3. **Soft delete only** - Records are deactivated, never physically deleted
4. **Audit trail mandatory** - All create/update/delete operations must be logged
5. **Password history** - Users cannot reuse last 5 passwords

---

## 5. Non-Functional Requirements

### Performance
- Page load time: < 2 seconds on LAN
- Search results: < 500ms for up to 10,000 records
- QR code generation: < 100ms per code
- Bulk operations: Process 1,000 records within 30 seconds

### Security
- All passwords hashed with bcrypt (work factor 12)
- HTTPS enforced (self-signed cert acceptable for LAN)
- CSRF protection on all forms
- Input validation and SQL injection prevention via parameterized queries
- XSS prevention via output encoding
- HMAC secret key stored in secure configuration (not in code)
- Session tokens: cryptographically random, 256-bit

### Scalability
- Support up to 50 concurrent users
- Support up to 10,000 students per school
- Support up to 500 faculty/staff per school
- Database optimized with appropriate indexes

### Availability
- Target: 99.5% uptime during school hours (6 AM - 8 PM)
- Graceful degradation: Read-only mode if database connection issues
- Automatic session recovery on brief network interruptions
- Docker restart policies: `unless-stopped` for automatic recovery
- Health checks: Container health endpoints for monitoring

---

## 6. AI/ML Specifications

> Not applicable for Phase 1. Future consideration for anomaly detection in attendance patterns.

---

## 7. Data Architecture

### Data Models

**Core Entities:**
- `User` - System users (staff accounts)
- `Role` - User roles and permissions
- `Student` - Student records
- `Faculty` - Faculty/teacher records
- `QrCode` - Generated QR codes with signatures
- `AuditLog` - All system changes

### Relationships and Constraints

```
User (1) ----< (M) UserRole >---- (1) Role
User (1) ----< (M) AuditLog
Student (1) ----< (M) QrCode
Student (1) ----< (M) AuditLog
Faculty (1) ----< (M) AuditLog
Faculty (0..1) ---- (0..1) User
```

### Storage Mechanisms
- **Primary:** SQL Server Express (containerized via `mcr.microsoft.com/mssql/server`)
- **Volumes:** Persistent Docker volumes for database files
- **Connection:** Environment variables injected via docker-compose
- **Backups:** Volume snapshots or SQL backup scripts (automated daily recommended)

---

## 8. Integration Map

### External Services

| Service | Purpose | Phase |
|---------|---------|-------|
| SMS Gateway | Send notifications to parents | Phase 3 |
| WPF Scanner App | Receive scan data via REST API | Phase 2 |

### Authentication Methods
- **Internal:** ASP.NET Identity with cookie authentication
- **API (future):** JWT tokens for scanner devices

### Third-Party Dependencies
- None for Phase 1 (fully self-contained)
- SMS provider TBD for Phase 3

---

## 9. Configuration Reference

### Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | Yes | - |
| `Security__HmacSecretKey` | 256-bit key for QR signing | Yes | - |
| `Security__LockoutThreshold` | Failed login attempts before lockout | No | 5 |
| `Security__LockoutDurationMinutes` | Lockout duration | No | 15 |
| `Security__SessionTimeoutHours` | Session expiry | No | 8 |
| `MSSQL_SA_PASSWORD` | SQL Server SA password (Docker) | Yes | - |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | No | Production |
| `ASPNETCORE_URLS` | Listen URLs for Kestrel | No | http://+:80 |

### Feature Flags
- `Features__Enable2FA` - Enable/disable 2FA option (default: true)
- `Features__EnableBulkImport` - Enable CSV import (default: true)

---

## 10. Quality Assessment

### Tested Functionality
- To be defined after implementation

### Untested Areas
- To be defined after implementation

### Technical Debt
- None (greenfield project)

---

## 11. Open Questions

- **Q:** What SMS gateway provider will be used?
  **Context:** Needed for Phase 3 SMS integration
  **Options:** Twilio, local telco API, or generic SMPP gateway

- **Q:** Should the system support multiple schools from day one?
  **Context:** Multi-tenancy adds complexity but Super Admin role suggests it
  **Options:** Single school for MVP vs full multi-tenant

- **Q:** What is the QR code validity period?
  **Context:** Should QR codes expire and require regeneration?
  **Options:** Never expire, yearly renewal, or configurable

- **Q:** What backup/restore strategy is required?
  **Context:** On-premise deployment needs data protection
  **Options:** Manual, scheduled SQL backups, or external backup service

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| 2026-02-03 | 1.0.1 | Added Docker containerization to tech stack |
| 2026-02-03 | 1.0.0 | Initial PRD created |

---

> **Confidence Markers:** [HIGH] clear requirement | [MEDIUM] inferred from context | [LOW] speculative
>
> **Status Values:** Complete | Partial | Stubbed | Broken | Not Started
