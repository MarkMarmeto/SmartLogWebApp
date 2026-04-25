# US0007: Authorization Policy Enforcement

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** unauthorized access attempts to be blocked with a 403 error
**So that** users cannot access features beyond their role's permissions

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Department Head responsible for system security.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

### Background
Even if menu items are hidden, users could potentially access pages directly via URL. Server-side authorization must enforce role-based access control on every protected endpoint.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | Role-based access control | All pages protected |
| TRD | Architecture | ASP.NET authorization policies | Use [Authorize] attributes |

---

## Acceptance Criteria

### AC1: Unauthenticated Access Blocked
- **Given** I am not logged in
- **When** I navigate directly to /Admin/Users
- **Then** I am redirected to the login page
- **And** the original URL is preserved for post-login redirect

### AC2: Unauthorized Role Access Blocked
- **Given** I am logged in as Teacher Tina (Teacher role)
- **When** I navigate directly to /Admin/Users
- **Then** I see a 403 Forbidden error page
- **And** the page displays "You do not have permission to access this page"
- **And** a "Return to Dashboard" link is provided

### AC3: Super Admin Full Access
- **Given** I am logged in as Tech-Savvy Tony (Super Admin)
- **When** I navigate to any page in the system
- **Then** the page loads successfully

### AC4: Admin Role Restrictions
- **Given** I am logged in as Admin Amy (Admin role)
- **When** I navigate to /Admin/Settings or /Admin/AuditLogs
- **Then** I see a 403 Forbidden error page

### AC5: Teacher Role Restrictions
- **Given** I am logged in as Teacher Tina (Teacher role)
- **When** I navigate to /Admin/Users or /Admin/Students/Create
- **Then** I see a 403 Forbidden error page
- **But** when I navigate to /Admin/Students (list/view)
- **Then** the page loads successfully (read-only access)

### AC6: Security Role Restrictions
- **Given** I am logged in as Guard Gary (Security role)
- **When** I navigate to /Admin/Students or /Admin/Users
- **Then** I see a 403 Forbidden error page

### AC7: Staff Role Restrictions
- **Given** I am logged in as Staff Sarah (Staff role)
- **When** I navigate to /Admin/Students/Create or /Admin/Faculty
- **Then** I see a 403 Forbidden error page
- **But** when I navigate to /Admin/Students (search/view)
- **Then** the page loads with read-only access

### AC8: Audit Log for Unauthorized Access
- **Given** I am logged in as Teacher Tina
- **When** I attempt to access /Admin/Users
- **Then** an audit log entry is created with:
  - Action: "UnauthorizedAccess"
  - UserId: my user ID
  - Details: "Attempted access to /Admin/Users"
  - Timestamp: current time

---

## Scope

### In Scope
- Authorization policies for all roles
- [Authorize] attributes on all Razor Pages
- Custom 403 error page
- Audit logging for unauthorized access attempts
- Policy-based authorization for granular control

### Out of Scope
- Resource-based authorization (e.g., "can only view own students")
- API authorization (separate in Phase 2)
- Dynamic permission changes without role change

---

## Technical Notes

### Implementation Approach
- Define authorization policies in `Program.cs`:
  ```csharp
  builder.Services.AddAuthorization(options =>
  {
      options.AddPolicy("RequireSuperAdmin", policy =>
          policy.RequireRole("SuperAdmin"));
      options.AddPolicy("RequireAdmin", policy =>
          policy.RequireRole("SuperAdmin", "Admin"));
      options.AddPolicy("CanManageUsers", policy =>
          policy.RequireRole("SuperAdmin", "Admin"));
      options.AddPolicy("CanViewStudents", policy =>
          policy.RequireRole("SuperAdmin", "Admin", "Teacher", "Staff"));
      options.AddPolicy("CanManageStudents", policy =>
          policy.RequireRole("SuperAdmin", "Admin"));
  });
  ```
- Apply to pages: `@attribute [Authorize(Policy = "RequireAdmin")]`
- Custom 403 page: `/Pages/Error/AccessDenied.cshtml`

### Authorization Matrix

| Page | Super Admin | Admin | Teacher | Security | Staff |
|------|-------------|-------|---------|----------|-------|
| /Admin/Users | Yes | Yes | No | No | No |
| /Admin/Students | Yes | Yes | View | No | View |
| /Admin/Students/Create | Yes | Yes | No | No | No |
| /Admin/Faculty | Yes | Yes | View | No | No |
| /Admin/Settings | Yes | No | No | No | No |
| /Admin/AuditLogs | Yes | No | No | No | No |
| /Dashboard | Yes | Yes | Yes | Yes | Yes |

### Data Requirements
- User's role claims available in `ClaimsPrincipal`
- Audit log entries for access violations

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| User's role removed while logged in | Next request returns 403 |
| API endpoint accessed with browser | 403 JSON response |
| Deep link to protected page | Redirect to login, then to page if authorized |
| Admin tries to access Super Admin page | 403, not 404 (security by obscurity is not used) |
| Direct POST to protected action | 403 with CSRF validation |
| Cached page after role change | Fresh auth check on server |
| Role with typo (e.g., "Admn") | Treated as no role, minimal access |
| Multiple roles on user | Highest privilege wins |

---

## Test Scenarios

- [ ] Unauthenticated user redirected to login
- [ ] Teacher cannot access /Admin/Users (gets 403)
- [ ] Teacher can access /Admin/Students (read-only)
- [ ] Admin cannot access /Admin/Settings (gets 403)
- [ ] Admin can access /Admin/Users
- [ ] Super Admin can access all pages
- [ ] Security can only access Dashboard and Attendance
- [ ] Staff can only search/view students
- [ ] 403 page displays helpful message
- [ ] 403 page has "Return to Dashboard" link
- [ ] Audit log entry created on 403

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | Authentication | Draft |
| [US0006](US0006-role-based-menu.md) | Related | Menu and pages exist | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| ASP.NET Authorization | Library | Available |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Open Questions

None currently.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
