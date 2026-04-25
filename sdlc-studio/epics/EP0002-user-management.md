# EP0002: User Management

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 1

## Summary

Enable administrators to create, edit, activate/deactivate, and manage user accounts for school staff. This includes assigning roles, resetting passwords, and managing 2FA settings for users.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Data | Soft delete only (no physical deletion) | IsActive flag pattern |
| PRD | Security | All changes must be audit logged | Audit trail required |
| TRD | Architecture | EF Core with SQL Server | Data access patterns |
| TRD | Tech Stack | ASP.NET Identity extended | User entity structure |

---

## Business Context

### Problem Statement
School administrators need to manage staff accounts efficiently. New teachers join, staff leave, roles change. Without a user management system, IT would need to handle all account operations manually.

**PRD Reference:** [Feature FT-003](../prd.md#3-feature-inventory)

### Value Proposition
- Administrators can self-serve user account operations
- Reduces IT workload for routine account management
- Clear audit trail for compliance and accountability
- Role-appropriate access prevents accidental privilege escalation

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| IT tickets for user management | N/A | < 2/week | Support system |
| Time to onboard new staff | N/A | < 5 minutes | Process timing |
| User management errors | N/A | 0 | Audit log review |

---

## Scope

### In Scope
- Create new user accounts (username, email, name, phone, role)
- Edit user details
- Activate/deactivate users (soft delete)
- Reset user passwords
- Enable/disable 2FA for users
- Search and filter user list by role, status, name
- View user activity history (audit log entries)

### Out of Scope
- Bulk user import (CSV) - defer to future enhancement
- Self-service password reset (requires email infrastructure)
- User profile photo upload
- User preferences/settings

### Affected Personas
- **Tech-Savvy Tony (Super Admin):** Can manage ALL users, including other Super Admins
- **Admin Amy (Administrator):** Can manage users with roles: Admin, Teacher, Security, Staff

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can create a new user with all required fields
- [ ] Admin can edit any user's name, email, phone, role
- [ ] Admin can deactivate a user (soft delete)
- [ ] Admin can reactivate a previously deactivated user
- [ ] Admin can reset another user's password
- [ ] Admin can enable/disable 2FA for any user
- [ ] User list displays with pagination (20 per page)
- [ ] User list can be searched by name or username
- [ ] User list can be filtered by role and active status
- [ ] Super Admin can manage Admin users; Admin cannot
- [ ] All user management actions are audit logged

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0001: Authentication & Authorization | Epic | Not Started | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0003: Student Management | Epic | Similar CRUD patterns |
| EP0004: Faculty Management | Epic | Similar CRUD patterns |

---

## Risks & Assumptions

### Assumptions
- Usernames are unique across the system
- Email addresses are valid and unique
- Admin role hierarchy is sufficient (no custom role creation)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Admin creates user with wrong role | Medium | Medium | Confirmation dialog, audit log |
| Accidental deactivation of active user | Low | Medium | Soft delete allows reactivation |
| Admin escalates own privileges | Low | High | Cannot edit own role |

---

## Technical Considerations

### Architecture Impact
- User entity extends ASP.NET Identity
- UserService for business logic
- Razor Pages for CRUD operations
- Repository pattern for data access

### Integration Points
- EP0001: Uses authentication and authorization
- Database: User table, audit log entries
- UI: Shared layout, navigation, data tables

---

## Sizing

**Story Points:** 14
**Estimated Story Count:** 6

**Complexity Factors:**
- Standard CRUD operations
- Role hierarchy enforcement
- Search and filtering
- Audit logging integration

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0009](../stories/US0009-create-user.md) | Create User Account | 3 | Done |
| [US0010](../stories/US0010-edit-user.md) | Edit User Account | 2 | Done |
| [US0011](../stories/US0011-deactivate-user.md) | Deactivate/Reactivate User | 2 | Done |
| [US0012](../stories/US0012-user-list.md) | User List with Search and Filter | 3 | Done |
| [US0013](../stories/US0013-assign-role.md) | Assign Role to User | 2 | Done |
| [US0014](../stories/US0014-reset-password.md) | Admin Reset User Password | 2 | Done |

**Total:** 14 story points across 6 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0002`

---

## Open Questions

- [ ] Should there be a limit on how many users can be created? - Owner: Product
- [ ] Should deactivated users be hidden by default in the list? - Owner: UX

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
