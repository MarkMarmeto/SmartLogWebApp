# PL0001: User Login - Implementation Plan

> **Status:** Complete
> **Story:** [US0001: User Login](../stories/US0001-user-login.md)
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Created:** 2026-02-04
> **Language:** C# (.NET 8.0)

## Overview

This is the **foundation story** for SmartLog - implementing the user login page and authentication flow. Since this is a greenfield project, we'll need to scaffold the entire ASP.NET Core Razor Pages application structure before implementing the login functionality.

The login system uses ASP.NET Identity with cookie-based authentication. Users enter their username and password, which are validated against the database. Active users are redirected to the dashboard; inactive or invalid credentials show appropriate error messages.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Display Login Page | Show login form with username, password fields, and Sign In button |
| AC2 | Successful Login | Valid active user redirected to Dashboard with welcome message |
| AC3 | Failed Login - Invalid Credentials | Show "Invalid username or password" error |
| AC4 | Failed Login - Inactive User | Show "Your account has been deactivated" error |
| AC5 | Failed Login - Empty Fields | Client-side validation for required fields |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 Razor Pages
- **ORM:** Entity Framework Core 8.0
- **Database:** SQL Server Express (containerized)
- **Test Framework:** xUnit with Playwright (E2E)

### Relevant Best Practices
- Use ASP.NET Identity `SignInManager.PasswordSignInAsync()` for authentication
- Never store plain text passwords - bcrypt with work factor 12
- Secure cookies: HttpOnly, Secure, SameSite=Strict
- Use `[AllowAnonymous]` attribute on login page
- Always check `IsActive` flag before allowing login
- Use parameterized queries (EF Core handles this automatically)
- Apply anti-forgery tokens on forms

### Library Documentation

| Library | Purpose | Key Patterns |
|---------|---------|--------------|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | Authentication | SignInManager, UserManager |
| Microsoft.EntityFrameworkCore.SqlServer | Database | DbContext, migrations |
| Serilog.AspNetCore | Logging | Structured logging to console |

### Existing Patterns
**No existing patterns - greenfield project.** This plan establishes the foundational patterns for all subsequent stories:
- Project structure following TRD Layer Structure
- ASP.NET Identity configuration
- Cookie authentication settings
- Audit logging pattern

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:**
- Greenfield project requires substantial scaffolding before tests can run
- UI-heavy story (login form) - harder to test-first
- Need running application to verify cookie behavior
- Once scaffolding is complete, write comprehensive tests

### Test Priority
1. Successful login redirects to dashboard and sets cookie
2. Invalid credentials return error without setting cookie
3. Inactive user cannot login (shows deactivation message)

---

## Implementation Tasks

| # | Task | File | Depends On | Status |
|---|------|------|------------|--------|
| 1 | Create ASP.NET Core project | `SmartLog.Web.csproj` | - | [x] |
| 2 | Add NuGet packages | `SmartLog.Web.csproj` | 1 | [x] |
| 3 | Create ApplicationDbContext | `Data/ApplicationDbContext.cs` | 2 | [x] |
| 4 | Create ApplicationUser entity | `Data/Entities/ApplicationUser.cs` | 2 | [x] |
| 5 | Configure Program.cs | `Program.cs` | 3, 4 | [x] |
| 6 | Create appsettings.json | `appsettings.json` | - | [x] |
| 7 | Create database migration | `Data/Migrations/` | 3, 4, 5 | Pending (run on first startup) |
| 8 | Create _Layout.cshtml | `Pages/Shared/_Layout.cshtml` | 1 | [x] |
| 9 | Create Login page | `Pages/Account/Login.cshtml` | 8 | [x] |
| 10 | Implement Login handler | `Pages/Account/Login.cshtml.cs` | 5, 9 | [x] |
| 11 | Create Dashboard page | `Pages/Index.cshtml` | 8 | [x] |
| 12 | Create _LoginPartial | `Pages/Shared/_LoginPartial.cshtml` | 5 | [x] |
| 13 | Create Dockerfile | `Dockerfile` | 1 | [x] |
| 14 | Create docker-compose.yml | `docker-compose.yml` | 13 | [x] |
| 15 | Seed admin user | `Data/DbInitializer.cs` | 3, 4, 7 | [x] |
| 16 | Write unit tests | `tests/SmartLog.Tests/` | 10 | Pending (requires .NET SDK) |

### Parallel Execution Groups

| Group | Tasks | Prerequisite |
|-------|-------|--------------|
| A - Project Setup | 1, 2, 6 | None |
| B - Data Layer | 3, 4, 7 | Group A |
| C - Configuration | 5, 13, 14 | Group B |
| D - UI Layer | 8, 9, 11, 12 | Group A |
| E - Login Logic | 10, 15 | Group B, D |
| F - Testing | 16 | Group E |

---

## Implementation Phases

### Phase 1: Project Scaffolding
**Goal:** Create the foundational ASP.NET Core Razor Pages project with Identity

- [ ] Create new .NET 8 Razor Pages project
- [ ] Add required NuGet packages:
  - Microsoft.AspNetCore.Identity.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.SqlServer
  - Microsoft.EntityFrameworkCore.Tools
  - Serilog.AspNetCore
  - Serilog.Sinks.Console
- [ ] Create `ApplicationUser` entity extending `IdentityUser` with:
  - `FirstName`, `LastName` (string, required)
  - `IsActive` (bool, default: true)
  - `CreatedAt`, `UpdatedAt` (DateTime)
- [ ] Create `ApplicationDbContext` extending `IdentityDbContext<ApplicationUser>`
- [ ] Configure `Program.cs`:
  - Add DbContext with SQL Server
  - Add Identity with cookie authentication
  - Configure password policy (8+ chars, uppercase, lowercase, digit)
  - Configure lockout (5 attempts, 15 minutes) - for US0002
  - Configure cookie settings (10-hour expiry, HttpOnly, Secure, SameSite=Strict)
- [ ] Create `appsettings.json` and `appsettings.Development.json`

**Files:**
- `SmartLog.Web/SmartLog.Web.csproj` - Project file with packages
- `SmartLog.Web/Program.cs` - Application entry point
- `SmartLog.Web/appsettings.json` - Configuration
- `SmartLog.Web/Data/ApplicationDbContext.cs` - EF Core context
- `SmartLog.Web/Data/Entities/ApplicationUser.cs` - User entity

### Phase 2: Docker & Database Setup
**Goal:** Containerize the application with SQL Server

- [ ] Create `Dockerfile` for ASP.NET Core 8
- [ ] Create `docker-compose.yml` with:
  - smartlog-web service (port 8080)
  - smartlog-db service (SQL Server 2022)
  - Volume for database persistence
- [ ] Create initial EF Core migration
- [ ] Create `DbInitializer.cs` to seed:
  - 5 roles: SuperAdmin, Admin, Teacher, Security, Staff
  - Default admin user (admin.amy / SecurePass1!)
- [ ] Run migration and verify database schema

**Files:**
- `SmartLog.Web/Dockerfile` - Container build
- `docker-compose.yml` - Service orchestration
- `SmartLog.Web/Data/Migrations/` - EF migrations
- `SmartLog.Web/Data/DbInitializer.cs` - Seed data

### Phase 3: UI Layer
**Goal:** Create Razor Pages for login and dashboard

- [ ] Create `_Layout.cshtml` with:
  - Bootstrap 5 styling
  - Navigation bar with login partial
  - Footer with copyright
- [ ] Create `_LoginPartial.cshtml` showing:
  - Username when logged in
  - Logout link when logged in
  - Login link when not logged in
- [ ] Create `Login.cshtml` with:
  - Username input (required)
  - Password input (required)
  - Sign In button
  - Error message area
  - Anti-forgery token
- [ ] Create `Login.cshtml.cs` (PageModel) with:
  - `[AllowAnonymous]` attribute
  - InputModel with Username, Password
  - OnGet: redirect to dashboard if already authenticated
  - OnPostAsync: validate, check IsActive, sign in, redirect
- [ ] Create Dashboard `Index.cshtml` with:
  - Welcome message showing user's name
  - `[Authorize]` attribute

**Files:**
- `SmartLog.Web/Pages/Shared/_Layout.cshtml`
- `SmartLog.Web/Pages/Shared/_LoginPartial.cshtml`
- `SmartLog.Web/Pages/Account/Login.cshtml`
- `SmartLog.Web/Pages/Account/Login.cshtml.cs`
- `SmartLog.Web/Pages/Index.cshtml`
- `SmartLog.Web/Pages/Index.cshtml.cs`

### Phase 4: Testing & Validation
**Goal:** Verify all acceptance criteria

| AC | Verification Method | File Evidence | Status |
|----|---------------------|---------------|--------|
| AC1 | Navigate to /Account/Login shows form | `Pages/Account/Login.cshtml` | Pending |
| AC2 | Login with admin.amy/SecurePass1! redirects to dashboard | `Pages/Account/Login.cshtml.cs:OnPostAsync` | Pending |
| AC3 | Login with wrong password shows error | `Pages/Account/Login.cshtml.cs:OnPostAsync` | Pending |
| AC4 | Login with inactive user shows deactivation message | `Pages/Account/Login.cshtml.cs:OnPostAsync` | Pending |
| AC5 | Empty fields show validation errors | `Pages/Account/Login.cshtml` (client validation) | Pending |

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Username with leading/trailing spaces | Trim in OnPostAsync before validation | Phase 3 |
| 2 | Username case sensitivity | Use UserManager.FindByNameAsync (case-insensitive by default) | Phase 3 |
| 3 | Password with spaces | Do NOT trim password - pass as-is | Phase 3 |
| 4 | SQL injection in username | EF Core parameterized queries (automatic) | Phase 2 |
| 5 | XSS in username field | Razor automatic HTML encoding | Phase 3 |
| 6 | Concurrent login from same account | Allow - no single-session restriction | Phase 3 |
| 7 | Login while already logged in | Redirect to dashboard in OnGet | Phase 3 |
| 8 | Browser back button after login | Add cache-control headers | Phase 3 |

**Coverage:** 8/8 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| SQL Server container slow to start | Docker Compose ready check may timeout | Add healthcheck with retries, depends_on with condition |
| Migration fails on first run | Application crashes | Run migrations in Program.cs startup with retry |
| Seed data runs multiple times | Duplicate users | Use idempotent seeding (check if exists) |
| Cookie not set in HTTPS-only mode | Login fails in development | Use Secure=None in Development environment |

---

## Open Questions

- [ ] Should we show different error messages for invalid username vs invalid password? (Security consideration: probably not) - Owner: Security Review

**Decision:** Use generic "Invalid username or password" for both cases to prevent username enumeration attacks.

---

## Documentation Updates Required

| Document | Section | Update |
|----------|---------|--------|
| README.md | Getting Started | Add Docker setup instructions |
| README.md | Development | Add connection string configuration |

---

## Definition of Done

- [x] All acceptance criteria implemented
- [ ] Application runs in Docker containers (requires `docker compose up`)
- [ ] Database migrations applied successfully (auto-runs on startup)
- [ ] Admin user can login with seeded credentials
- [ ] Invalid credentials show error message
- [ ] Inactive user cannot login
- [ ] Empty fields show validation errors
- [x] Cookie set with proper security attributes (configured in Program.cs)
- [x] Code follows ASP.NET Core best practices
- [ ] No compiler warnings or errors (requires build)

---

## Notes

This is the **foundation plan** for SmartLog. The project structure, patterns, and configurations established here will be reused by all subsequent stories. Key decisions:

1. **Project structure** follows TRD Layer Structure exactly
2. **Identity configuration** includes lockout settings even though US0002 is separate - they share the same configuration
3. **Session timeout** (10 hours) and **idle timeout** (30 min) are configured per stakeholder decisions
4. **Docker setup** enables consistent development and deployment environments

Next story to implement: US0002 (Account Lockout) - builds on the Identity configuration from this story.
