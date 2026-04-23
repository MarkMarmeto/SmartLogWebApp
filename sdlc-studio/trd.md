# Technical Requirements Document

**Project:** SmartLog School Information Management System
**Version:** 1.0.0
**Status:** Draft
**Last Updated:** 2026-02-03
**PRD Reference:** [PRD](./prd.md)

---

## 1. Executive Summary

### Purpose
This TRD defines the technical architecture for SmartLog, an offline-first School Information Management System. It covers the Admin Web App component - an ASP.NET Core Razor Pages application for managing users, students, faculty, and secure QR code generation.

### Scope
**In Scope:**
- Phase 1: Authentication, RBAC, User/Student/Faculty management, QR generation
- Docker containerization with SQL Server Express
- Security architecture (HMAC-signed QR, audit logging)

**Out of Scope:**
- WPF Scanner App (separate project)
- SMS integration (Phase 3)
- Mobile applications

### Key Decisions
- Monolithic architecture for simplicity and rapid development
- Docker deployment for consistent environments across schools
- ASP.NET Identity for authentication with optional TOTP 2FA
- EF Core Code-First migrations for schema management
- API Key authentication for future scanner device communication

---

## 2. Project Classification

**Project Type:** Web Application

**Classification Rationale:** SmartLog serves a browser-based admin UI (Razor Pages) with backend logic, database, and background services. It fits the "Web Application" archetype with server-rendered pages.

**Architecture Implications:**
- **Default Pattern:** Monolith (recommended for <10 developers, single deployment)
- **Pattern Used:** Monolith
- **Deviation Rationale:** None - default pattern is appropriate

---

## 3. Architecture Overview

### System Context
SmartLog operates within a school's LAN environment. The Admin Web App is the central system accessed by school staff through browsers. In Phase 2+, WPF Scanner devices will communicate with the web app via REST API for scan ingestion.

```
┌─────────────────────────────────────────────────────────────────┐
│                        School LAN                               │
│                                                                 │
│  ┌──────────┐     ┌─────────────────┐     ┌──────────────────┐ │
│  │ Browser  │────▶│  SmartLog Web   │────▶│  SQL Server      │ │
│  │ (Staff)  │     │  (Docker)       │     │  (Docker)        │ │
│  └──────────┘     └─────────────────┘     └──────────────────┘ │
│                           │                                     │
│                           │ REST API (Phase 2)                  │
│                           ▼                                     │
│                   ┌──────────────┐                              │
│                   │ WPF Scanner  │                              │
│                   │ (Windows PC) │                              │
│                   └──────────────┘                              │
└─────────────────────────────────────────────────────────────────┘
```

### Architecture Pattern
**Monolith** with layered internal structure

**Rationale:**
- Single development team
- Single deployment target (Docker on school server)
- Shared data model across all features
- Simpler debugging, deployment, and maintenance
- Can evolve to modular monolith if needed later

### Component Overview

| Component | Responsibility | Technology |
|-----------|---------------|------------|
| Web UI | Razor Pages, forms, navigation | ASP.NET Core Razor Pages |
| Identity | Authentication, authorization, 2FA | ASP.NET Core Identity |
| Business Logic | Domain services, validation | C# Services |
| Data Access | ORM, queries, migrations | Entity Framework Core |
| QR Service | HMAC signing, QR generation | QRCoder + System.Security.Cryptography |
| Audit Service | Action logging | Custom middleware + EF Core |
| Database | Persistent storage | SQL Server Express |

### Layer Structure

```
SmartLog.Web/
├── Pages/                    # Razor Pages (UI layer)
│   ├── Account/              # Login, 2FA, Profile
│   ├── Admin/                # User management
│   ├── Students/             # Student CRUD
│   ├── Faculty/              # Faculty CRUD
│   └── Shared/               # Layouts, partials
├── Services/                 # Business logic layer
│   ├── IUserService.cs
│   ├── IStudentService.cs
│   ├── IQrCodeService.cs
│   └── IAuditService.cs
├── Data/                     # Data access layer
│   ├── ApplicationDbContext.cs
│   ├── Entities/             # EF Core entities
│   └── Migrations/           # EF migrations
├── Infrastructure/           # Cross-cutting concerns
│   ├── Security/             # HMAC, encryption
│   └── Middleware/           # Audit, error handling
└── Program.cs                # Application entry point
```

---

## 4. Technology Stack

### Core Technologies

| Category | Technology | Version | Rationale |
|----------|-----------|---------|-----------|
| Language | C# | 12 | Strong typing, enterprise patterns, team expertise |
| Runtime | .NET | 8.0 LTS | Long-term support, performance, cross-platform containers |
| Framework | ASP.NET Core Razor Pages | 8.0 | Server-rendered UI, ideal for admin dashboards, built-in Identity |
| ORM | Entity Framework Core | 8.0 | Code-first migrations, LINQ queries, SQL Server support |
| Database | SQL Server Express | 2022 | ACID compliance, relational integrity for school data |
| Container | Docker | 24+ | Consistent deployment, isolation, easy updates |

### Build & Development

| Tool | Purpose |
|------|---------|
| .NET SDK 8.0 | Build, run, publish |
| Docker Desktop | Local container development |
| Docker Compose | Multi-container orchestration |
| Visual Studio / VS Code | IDE |
| dotnet ef | EF Core CLI for migrations |

### Infrastructure Services

| Service | Provider | Purpose |
|---------|----------|---------|
| SQL Server Express | Microsoft (containerized) | Primary database |
| Docker Engine | Docker Inc | Container runtime |
| Docker Compose | Docker Inc | Service orchestration |

### NuGet Packages (Key Dependencies)

| Package | Purpose |
|---------|---------|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | Authentication/authorization |
| Microsoft.EntityFrameworkCore.SqlServer | SQL Server provider |
| QRCoder | QR code generation |
| Serilog.AspNetCore | Structured logging |
| Serilog.Sinks.Console | Console log output |

---

## 5. API Contracts

### API Style
**REST** (for Phase 2 scanner device communication)

### Authentication
- **Web UI:** Cookie-based session authentication (ASP.NET Identity)
- **Scanner API (Phase 2):** API Key per device in `X-API-Key` header

### Endpoints Overview (Phase 2 - Scanner API)

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | /api/v1/scans | Submit scan data | API Key |
| GET | /api/v1/health | Health check | None |
| POST | /api/v1/devices/register | Register scanner device | Admin Token |

### Error Response Format
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Human-readable description",
    "details": {
      "field": "studentId",
      "reason": "Student not found"
    }
  }
}
```

### Scan Ingestion Contract (Phase 2)

**Request:**
```json
POST /api/v1/scans
X-API-Key: {device-api-key}

{
  "qrPayload": "SMARTLOG:STU001:1706918400:abc123hmac",
  "scannedAt": "2026-02-03T08:30:00Z",
  "deviceId": "SCANNER-001",
  "scanType": "entry"
}
```

**Response (Success):**
```json
{
  "scanId": "SC-20260203-001",
  "status": "accepted",
  "studentName": "John Doe",
  "message": "Entry recorded"
}
```

**Idempotency:** Duplicate scans (same QR + device + 5-minute window) return the original response with `status: "duplicate"`.

---

## 6. Data Architecture

### Data Models

#### User (ASP.NET Identity Extended)

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | GUID | PK | Unique identifier |
| UserName | string(256) | Unique, Required | Login username |
| Email | string(256) | Unique, Required | User email |
| FirstName | string(100) | Required | First name |
| LastName | string(100) | Required | Last name |
| PhoneNumber | string(20) | Optional | Contact number |
| IsActive | bool | Required, Default: true | Soft delete flag |
| TwoFactorEnabled | bool | Required, Default: false | 2FA status |
| CreatedAt | DateTime | Required | Record creation |
| UpdatedAt | DateTime | Required | Last modification |

#### Student

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | GUID | PK | Unique identifier |
| StudentId | string(50) | Unique, Required | School-assigned ID |
| FirstName | string(100) | Required | First name |
| LastName | string(100) | Required | Last name |
| GradeLevel | string(20) | Required | Grade/Year level |
| Section | string(50) | Required | Class section |
| ParentName | string(200) | Required | Parent/Guardian name |
| ParentPhone | string(20) | Required | Parent contact (SMS) |
| IsActive | bool | Required, Default: true | Soft delete flag |
| CreatedAt | DateTime | Required | Record creation |
| UpdatedAt | DateTime | Required | Last modification |
| CreatedById | GUID | FK -> User | Audit: creator |

#### Faculty

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | GUID | PK | Unique identifier |
| EmployeeId | string(50) | Unique, Required | School-assigned ID |
| FirstName | string(100) | Required | First name |
| LastName | string(100) | Required | Last name |
| Department | string(100) | Required | Department |
| ContactNumber | string(20) | Optional | Contact number |
| UserId | GUID? | FK -> User, Nullable | Linked user account |
| IsActive | bool | Required, Default: true | Soft delete flag |
| CreatedAt | DateTime | Required | Record creation |
| UpdatedAt | DateTime | Required | Last modification |

#### QrCode

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | GUID | PK | Unique identifier |
| StudentId | GUID | FK -> Student, Required | Associated student |
| Payload | string(500) | Required | Full QR string |
| HmacSignature | string(100) | Required | HMAC-SHA256 signature |
| IssuedAt | DateTime | Required | When generated |
| IsValid | bool | Required, Default: true | Invalidation flag |
| InvalidatedAt | DateTime? | Nullable | When invalidated |

#### AuditLog

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | GUID | PK | Unique identifier |
| UserId | GUID? | FK -> User, Nullable | Acting user (null for system) |
| Action | string(50) | Required | Action type (Create, Update, Delete, Login, etc.) |
| EntityType | string(100) | Required | Target entity type |
| EntityId | string(100) | Optional | Target entity ID |
| OldValues | string(max) | Optional | JSON of previous values |
| NewValues | string(max) | Optional | JSON of new values |
| IpAddress | string(50) | Optional | Client IP |
| Timestamp | DateTime | Required | When action occurred |

### Entity Relationships

```
User (1) ────< (M) AspNetUserRoles >──── (1) Role
User (1) ────< (M) AuditLog
User (1) ────< (M) Student [CreatedBy]
User (0..1) ──── (0..1) Faculty [Optional Link]

Student (1) ────< (M) QrCode
Student (1) ────< (M) AuditLog
Faculty (1) ────< (M) AuditLog
```

### Storage Strategy

| Data Type | Storage | Rationale |
|-----------|---------|-----------|
| User accounts | SQL Server | Relational, ACID, Identity integration |
| Student records | SQL Server | Relational, foreign keys, audit |
| QR codes | SQL Server | Link to students, invalidation tracking |
| Audit logs | SQL Server | Compliance, queryable history |
| Session data | In-memory (cookies) | Stateless server, cookie-based auth |

### Migrations
**EF Core Code-First Migrations**
- Migrations stored in `Data/Migrations/`
- Applied automatically on container startup (development)
- Manual review before production deployment
- Rollback supported via `dotnet ef migrations remove`

---

## 7. Integration Patterns

### External Services

| Service | Purpose | Protocol | Auth | Phase |
|---------|---------|----------|------|-------|
| WPF Scanner App | Send scan data | REST/HTTPS | API Key | 2 |
| SMS Gateway | Parent notifications | REST | API Key | 3 |

### Internal Communication
- **Synchronous:** All Phase 1 operations are synchronous request-response
- **Asynchronous (Phase 3):** SMS sending via background worker with queue table

### Event Architecture
Phase 1 does not require event-driven architecture. Future phases may introduce:
- **Scan Received** event -> Trigger SMS notification
- **Student Deactivated** event -> Invalidate QR codes

---

## 8. Infrastructure

### Deployment Topology

```
┌─────────────────────────────────────────────────────┐
│                 Docker Host (School Server)         │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │            docker-compose.yml               │   │
│  │                                             │   │
│  │  ┌─────────────┐    ┌──────────────────┐   │   │
│  │  │ smartlog-web│    │ smartlog-db      │   │   │
│  │  │ (ASP.NET)   │───▶│ (SQL Server)     │   │   │
│  │  │ Port: 8080  │    │ Port: 1433       │   │   │
│  │  └─────────────┘    └──────────────────┘   │   │
│  │        │                    │              │   │
│  │        └────────┬───────────┘              │   │
│  │                 ▼                          │   │
│  │         Docker Volumes                     │   │
│  │    (db-data, app-logs, certs)              │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Docker Compose Structure

```yaml
# docker-compose.yml (reference structure)
services:
  smartlog-web:
    build: .
    ports:
      - "8080:8080"
      - "8443:8443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=...
      - Security__HmacSecretKey=...
    depends_on:
      - smartlog-db
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  smartlog-db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=...
    volumes:
      - db-data:/var/opt/mssql
    restart: unless-stopped

volumes:
  db-data:
```

### Environment Strategy

| Environment | Purpose | Characteristics |
|-------------|---------|-----------------|
| Development | Local development | Docker Compose, SQLite optional, hot reload |
| Staging | Pre-deployment testing | Docker Compose, SQL Server, test data |
| Production | Live system | Docker Compose, SQL Server, backups enabled |

### Scaling Strategy
**Vertical Scaling** - Single instance with adequate resources:
- Minimum: 2 CPU cores, 4GB RAM
- Recommended: 4 CPU cores, 8GB RAM
- SQL Server Express limit: 1GB RAM, 10GB database

Horizontal scaling not required for expected load (<50 concurrent users, <10,000 students).

---

## 9. Security Considerations

### Threat Model

| Threat | Likelihood | Impact | Mitigation |
|--------|-----------|--------|------------|
| Unauthorized access | Medium | High | Identity + RBAC, session timeout, 2FA |
| QR code forgery | Medium | High | HMAC-SHA256 signatures |
| SQL injection | Low | Critical | EF Core parameterized queries |
| XSS attacks | Low | Medium | Razor automatic encoding |
| CSRF attacks | Low | Medium | Anti-forgery tokens |
| Data breach | Low | Critical | Encryption at rest, audit logs |
| Brute force login | Medium | Medium | Rate limiting, account lockout |

### Security Controls

| Control | Implementation |
|---------|----------------|
| Authentication | ASP.NET Identity, bcrypt password hashing (work factor 12) |
| Authorization | Role-based (5 roles), policy-based for fine-grained |
| Encryption at rest | SQL Server TDE (Transparent Data Encryption) |
| Encryption in transit | HTTPS (TLS 1.2+), self-signed cert for LAN |
| Input validation | Data annotations, FluentValidation |
| Output encoding | Razor automatic HTML encoding |
| CSRF protection | Anti-forgery tokens on all forms |
| Session management | Secure cookies, HttpOnly, SameSite=Strict |
| Audit logging | All CRUD operations, login attempts logged |
| Secret management | Environment variables, not in code |

### Data Classification

| Data Type | Classification | Protection |
|-----------|----------------|------------|
| Passwords | Sensitive | Hashed (bcrypt), never stored plain |
| HMAC secret | Secret | Environment variable only |
| Student PII | Confidential | Access control, audit logged |
| Parent phone | Confidential | Access control, used for SMS only |
| Audit logs | Internal | Immutable, admin-only access |

---

## 10. Performance Requirements

### Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Page load (p50) | < 500ms | Browser DevTools, LAN conditions |
| Page load (p95) | < 2s | Browser DevTools, LAN conditions |
| Search response | < 500ms | Server-side timing for 10K records |
| QR generation | < 100ms | Per code generation |
| Bulk import | 1000 records/30s | CSV import timing |
| Concurrent users | 50 | Load test simulation |
| Database size | < 10GB | SQL Server Express limit |

### Capacity Planning

| Resource | Expected | Limit |
|----------|----------|-------|
| Students | 5,000 | 10,000 |
| Users | 100 | 500 |
| QR codes | 5,000 | 20,000 |
| Audit logs | 100K/year | Archival after 2 years |

---

## 11. Architecture Decision Records

### ADR-001: Monolith Architecture

**Status:** Accepted

**Context:** Need to choose between monolith, modular monolith, or microservices for SmartLog web application.

**Decision:** Use monolithic architecture with layered internal structure.

**Consequences:**
- Positive: Simpler deployment, debugging, and maintenance
- Positive: Lower operational complexity for school IT staff
- Positive: Faster initial development
- Negative: Must refactor if scaling requirements change significantly

---

### ADR-002: Docker Containerization

**Status:** Accepted

**Context:** Need consistent deployment across different school server environments.

**Decision:** Containerize application and database using Docker Compose.

**Consequences:**
- Positive: Consistent environments, easy updates via image pulls
- Positive: Isolation from host OS differences
- Positive: Simplified backup (volume snapshots)
- Negative: Requires Docker knowledge for IT staff
- Negative: Slight resource overhead

---

### ADR-003: HMAC-SHA256 for QR Code Signing

**Status:** Accepted

**Context:** QR codes must be tamper-proof to prevent unauthorized entries.

**Decision:** Sign QR codes with HMAC-SHA256 using a server-side secret key.

**Consequences:**
- Positive: Cryptographically secure, tamper detection
- Positive: Fast verification on scanner
- Positive: No network required for verification
- Negative: Secret key must be securely distributed to scanners
- Negative: Key rotation requires re-issuing all QR codes

---

### ADR-004: API Key Authentication for Scanner Devices

**Status:** Accepted

**Context:** Scanner devices need to authenticate when sending scan data to the server.

**Decision:** Use per-device API keys passed in `X-API-Key` header.

**Consequences:**
- Positive: Simple implementation, no token refresh complexity
- Positive: Each device independently revocable
- Positive: Works offline after initial registration
- Negative: Keys must be securely stored on devices
- Negative: No automatic expiration (manual rotation required)

---

### ADR-005: EF Core Code-First Migrations

**Status:** Accepted

**Context:** Need a strategy for managing database schema changes.

**Decision:** Use Entity Framework Core Code-First migrations.

**Consequences:**
- Positive: Version-controlled schema changes
- Positive: Automated migration application
- Positive: Rollback capability
- Negative: Complex migrations may need manual SQL review
- Negative: Limited control over exact SQL generated

---

### ADR-006: Structured Console Logging

**Status:** Accepted

**Context:** Need logging strategy for containerized deployment.

**Decision:** Use Serilog with structured JSON logging to stdout.

**Consequences:**
- Positive: Docker captures all logs automatically
- Positive: Structured format enables parsing/querying
- Positive: No file management in containers
- Negative: Requires log aggregation for long-term analysis
- Negative: Logs lost if container removed without export

---

## 12. Open Technical Questions

- [ ] **Q:** Should database connections use Always Encrypted for sensitive columns?
  **Context:** Additional protection for PII fields (phone numbers)

- [ ] **Q:** What is the QR code expiration policy?
  **Context:** Should codes expire annually or remain valid until manually invalidated?

- [ ] **Q:** How will the HMAC secret key be distributed to scanner devices?
  **Context:** Scanners need the key for offline verification

- [ ] **Q:** Should we implement audit log archival?
  **Context:** SQL Server Express 10GB limit, audit logs will grow over time

---

## 13. Implementation Constraints

### Must Have
- All pages must work on Chrome, Edge, Firefox (latest 2 versions)
- Mobile-responsive admin UI (tablets for administrators)
- All operations must be audit logged
- Soft delete only (no physical deletion of records)
- Docker Compose deployment

### Won't Have (This Version)
- Real-time updates (WebSocket)
- Mobile native app
- Multi-school/tenant support (single school per deployment)
- Offline web app capability
- Active Directory integration

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| 2026-02-03 | 1.0.0 | Initial TRD created |
