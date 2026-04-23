# US0005: Session Management

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** my session to expire after inactivity and be able to log out
**So that** my account is protected if I forget to log out or leave my computer unattended

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who may leave her workstation for meetings or breaks.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
Session management ensures that authenticated sessions expire after a period of inactivity and provides a clear logout mechanism. This protects against unauthorized access if a user walks away from their computer.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | 10-hour session timeout | Configurable timeout duration |
| TRD | Tech Stack | Cookie-based session auth | Sliding expiration window |

---

## Acceptance Criteria

### AC1: Session Cookie Configuration
- **Given** the application is configured
- **Then** authentication cookies have the following properties:
  - HttpOnly: true
  - Secure: true (when using HTTPS)
  - SameSite: Strict
  - Sliding expiration: enabled
  - Expiration: 10 hours

### AC2: Logout Functionality
- **Given** I am logged in as Admin Amy
- **When** I click "Logout" in the navigation menu
- **Then** my session is terminated
- **And** my authentication cookie is cleared
- **And** I am redirected to the login page
- **And** I see message "You have been logged out successfully"

### AC3: Session Timeout Warning
- **Given** I am logged in and active
- **And** 9 hours 50 minutes have passed since my last activity
- **Then** I see a warning banner "Your session will expire in 10 minutes. Click here to stay logged in."
- **When** I click the warning
- **Then** my session is extended for another 10 hours
- **And** the warning disappears

### AC4: Session Expiration
- **Given** I am logged in
- **And** 10 hours have passed with no activity
- **When** I try to access any protected page
- **Then** I am redirected to the login page
- **And** I see message "Your session has expired. Please log in again."

### AC5: Activity Extends Session
- **Given** I am logged in
- **And** 4 hours have passed
- **When** I click any link or submit any form
- **Then** my session expiration is reset to 10 hours from now

### AC8: Idle Timeout
- **Given** I am logged in
- **And** I have been idle (no mouse/keyboard activity) for 30 minutes
- **Then** I see a modal "You've been idle. Click to continue or you'll be logged out in 5 minutes."
- **When** I click "Continue"
- **Then** the modal closes and session continues
- **When** I don't respond within 5 minutes
- **Then** I am logged out and redirected to login page

### AC6: Logout Audit Log
- **Given** I click "Logout"
- **Then** an audit log entry is created with:
  - Action: "Logout"
  - UserId: my user ID
  - Timestamp: current time

### AC7: Session Expiration Audit Log
- **Given** my session expires due to inactivity
- **Then** an audit log entry is created with:
  - Action: "SessionExpired"
  - UserId: my user ID
  - Timestamp: expiration time

---

## Scope

### In Scope
- Logout button in navigation
- Session timeout configuration (10 hours)
- Sliding expiration
- Session expiration warning (10 minutes before)
- Redirect to login on expired session
- Audit logging for logout and expiration
- **Idle timeout after 30 minutes of inactivity**

### Out of Scope
- "Remember me" extended sessions
- Single session enforcement (logout other sessions)
- Session management page (view/terminate sessions)
- Concurrent session limits

---

## Technical Notes

### Implementation Approach
- Configure cookie authentication in `Program.cs`:
  ```csharp
  options.ExpireTimeSpan = TimeSpan.FromHours(10);
  options.SlidingExpiration = true;
  options.Cookie.HttpOnly = true;
  options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
  options.Cookie.SameSite = SameSiteMode.Strict;
  ```
- Idle timeout: JavaScript-based activity detection (30 min idle = warning)
- Logout endpoint: `/Account/Logout` (POST to prevent CSRF)
- Session warning: JavaScript timer checking cookie expiration
- Middleware to check session validity on each request

### API Contracts
- POST `/Account/Logout` - Terminates session, redirects to login

### Data Requirements
- No additional data storage (session managed via cookies)
- Audit log entries for session events

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Logout with multiple tabs open | All tabs show login page on next action |
| Browser closed without logout | Session persists until timeout |
| Logout clicked twice | Idempotent - no error |
| Network error during logout | Best-effort cookie clear, redirect anyway |
| Session expires during form submit | Show expiration message, save form data in localStorage |
| Clock skew between client/server | Use server time for expiration |
| Cookie manually deleted | Treated as logged out |
| Session warning dismissed | Warning reappears after 1 minute if still near expiry |

---

## Test Scenarios

- [ ] Logout button visible when logged in
- [ ] Logout clears authentication cookie
- [ ] Logout redirects to login page with success message
- [ ] Session expires after 10 hours of inactivity
- [ ] Idle timeout warning after 30 minutes
- [ ] Idle timeout logout after 35 minutes (30 + 5 warning)
- [ ] Activity extends session (sliding expiration)
- [ ] Session warning appears 10 minutes before expiry
- [ ] Clicking warning extends session
- [ ] Expired session redirects to login with message
- [ ] Audit log entry created on logout
- [ ] Audit log entry created on session expiration
- [ ] Cookie has correct security attributes

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | Login flow | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| ASP.NET Cookie Authentication | Library | Available |

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium

---

## Stakeholder Decisions

- [x] Session timeout: 10 hours - **Approved by IT Manager Ivan, Registrar Rosa**
- [x] Idle timeout: 30 minutes with 5-minute warning - **Approved by IT Manager Ivan**
- [x] Session warning: Banner (not modal) - **Approved**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Changed timeout to 10 hours, added 30-min idle timeout |
