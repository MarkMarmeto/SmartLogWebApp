# US0006: Role-Based Menu Filtering

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Teacher Tina (Teacher)
**I want** to see only the menu items relevant to my role
**So that** the interface is simple and I'm not confused by features I can't access

## Context

### Persona Reference
**Teacher Tina** - Classroom Teacher with intermediate technical proficiency who wants a simple, uncluttered interface.
[Full persona details](../personas.md#3-teacher-tina-classroom-teacher)

### Background
Different user roles have different permissions in SmartLog. The navigation menu should only display options that the current user is authorized to access. This reduces confusion and improves usability.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | 5-tier role system | Menu varies by all 5 roles |
| TRD | Architecture | ASP.NET authorization | Use policy-based auth |

---

## Acceptance Criteria

### AC1: Super Admin Full Menu
- **Given** I am logged in as Tech-Savvy Tony (Super Admin)
- **Then** I see the following menu items:
  - Dashboard
  - Users
  - Students
  - Faculty
  - Devices (Phase 2)
  - Attendance (Phase 2)
  - SMS Settings (Phase 3)
  - Reports (Phase 3)
  - Audit Logs
  - Settings

### AC2: Admin Menu
- **Given** I am logged in as Admin Amy (Admin)
- **Then** I see the following menu items:
  - Dashboard
  - Users
  - Students
  - Faculty
  - Devices (Phase 2)
  - Attendance (Phase 2)
  - SMS Settings (Phase 3)
  - Reports (Phase 3)
- **And** I do NOT see:
  - Audit Logs (Super Admin only)
  - Settings (Super Admin only)

### AC3: Teacher Menu
- **Given** I am logged in as Teacher Tina (Teacher)
- **Then** I see the following menu items:
  - Dashboard
  - Students (view only)
  - Faculty (view only)
  - Attendance (Phase 2)
  - Reports (Phase 3 - class reports only)
- **And** I do NOT see:
  - Users
  - Devices
  - SMS Settings
  - Audit Logs
  - Settings

### AC4: Security Menu
- **Given** I am logged in as Guard Gary (Security)
- **Then** I see the following menu items:
  - Dashboard
  - Attendance (Phase 2 - view only)
- **And** I do NOT see:
  - Users
  - Students
  - Faculty
  - Devices
  - SMS Settings
  - Reports
  - Audit Logs
  - Settings

### AC5: Staff Menu
- **Given** I am logged in as Staff Sarah (Staff)
- **Then** I see the following menu items:
  - Dashboard
  - Students (view only - search/lookup)
- **And** I do NOT see:
  - Users
  - Faculty
  - Devices
  - Attendance
  - SMS Settings
  - Reports
  - Audit Logs
  - Settings

### AC6: Menu Reflects Current Role
- **Given** my role is changed from Teacher to Admin
- **When** I refresh the page or navigate
- **Then** I see the Admin menu items
- **And** I no longer see the Teacher-restricted menu

---

## Scope

### In Scope
- Navigation menu component with role filtering
- Menu configuration per role
- Dynamic menu rendering based on current user's role
- Graceful handling of role changes

### Out of Scope
- Sub-menus or nested navigation
- User-customizable menu
- Mobile hamburger menu (responsive design)
- Menu badges/notifications

---

## Technical Notes

### Implementation Approach
- Create `IMenuService` to return menu items for current user's role
- Use ASP.NET `User.IsInRole()` checks in Razor layout
- Menu configuration in `appsettings.json` or code constants
- Shared layout: `_Layout.cshtml` with menu partial

### Menu Configuration
```csharp
public static class MenuConfig
{
    public static Dictionary<string, string[]> RoleMenus = new()
    {
        ["SuperAdmin"] = ["Dashboard", "Users", "Students", "Faculty", "Devices", "Attendance", "SMS", "Reports", "AuditLogs", "Settings"],
        ["Admin"] = ["Dashboard", "Users", "Students", "Faculty", "Devices", "Attendance", "SMS", "Reports"],
        ["Teacher"] = ["Dashboard", "Students", "Faculty", "Attendance", "Reports"],
        ["Security"] = ["Dashboard", "Attendance"],
        ["Staff"] = ["Dashboard", "Students"]
    };
}
```

### Data Requirements
- User's role available from `ClaimsPrincipal`
- No additional database queries for menu

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| User with no role assigned | Show minimal menu (Dashboard only) |
| User with multiple roles | Use highest privilege role for menu |
| Menu item links to non-existent page | Hide item until feature is built |
| Page refreshed during role change | Menu updates on next request |
| JavaScript disabled | Menu still works (server-rendered) |
| User's role deleted while logged in | Show empty menu, prompt re-login |
| Feature flag disables menu item | Hide item for all roles |

---

## Test Scenarios

- [ ] Super Admin sees all menu items
- [ ] Admin sees correct subset of menu items
- [ ] Teacher sees correct subset of menu items
- [ ] Security sees minimal menu items
- [ ] Staff sees read-only student lookup menu
- [ ] User with no role sees only Dashboard
- [ ] Menu updates when role changes (after refresh)
- [ ] Menu items link to correct pages
- [ ] Phase 2/3 items hidden until implemented
- [ ] Mobile-responsive menu works correctly

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | User must be logged in | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| Layout/navigation design | UX | Not Started |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Open Questions

- [ ] Should Phase 2/3 menu items be visible but disabled, or completely hidden? - Owner: UX (Proposed: Hidden)

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
