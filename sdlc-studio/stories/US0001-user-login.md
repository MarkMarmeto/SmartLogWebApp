# US0001: User Login

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to log in with my username and password
**So that** I can access the SmartLog system to manage student and staff records

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages student enrollment and records. Intermediate technical proficiency, needs clear interfaces.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
The login page is the entry point for all users accessing SmartLog. It must be simple, secure, and provide clear feedback on success or failure. This is the foundation for all authenticated functionality.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | Passwords hashed with bcrypt (work factor 12) | Never store plain text |
| TRD | Architecture | ASP.NET Identity | Use Identity SignInManager |
| TRD | Tech Stack | Cookie-based session auth | Secure, HttpOnly cookies |

---

## Acceptance Criteria

### AC1: Display Login Page
- **Given** I am an unauthenticated user
- **When** I navigate to the application root URL or /Account/Login
- **Then** I see a login form with username field, password field, and "Sign In" button

### AC2: Successful Login
- **Given** I am on the login page
- **And** I have a valid, active user account with username "admin.amy" and password "SecurePass1!"
- **When** I enter "admin.amy" in the username field
- **And** I enter "SecurePass1!" in the password field
- **And** I click "Sign In"
- **Then** I am redirected to the Dashboard page
- **And** I see a welcome message with my name
- **And** a secure authentication cookie is set

### AC3: Failed Login - Invalid Credentials
- **Given** I am on the login page
- **When** I enter "admin.amy" in the username field
- **And** I enter "wrongpassword" in the password field
- **And** I click "Sign In"
- **Then** I remain on the login page
- **And** I see an error message "Invalid username or password"
- **And** no authentication cookie is set

### AC4: Failed Login - Inactive User
- **Given** I am on the login page
- **And** my account has been deactivated (IsActive = false)
- **When** I enter valid credentials
- **And** I click "Sign In"
- **Then** I remain on the login page
- **And** I see an error message "Your account has been deactivated. Please contact an administrator."

### AC5: Failed Login - Empty Fields
- **Given** I am on the login page
- **When** I leave the username field empty
- **And** I click "Sign In"
- **Then** I see a validation error "Username is required"
- **When** I leave the password field empty
- **And** I click "Sign In"
- **Then** I see a validation error "Password is required"

---

## Scope

### In Scope
- Login form UI (username, password, submit button)
- Server-side credential validation
- Redirect to dashboard on success
- Error message display on failure
- Client-side validation for empty fields
- Secure cookie creation

### Out of Scope
- "Remember me" checkbox (open question in epic)
- "Forgot password" link (no email infrastructure)
- Social login / OAuth
- 2FA verification (separate story US0004)
- Account lockout (separate story US0002)

---

## Technical Notes

### Implementation Approach
- Use ASP.NET Identity `SignInManager.PasswordSignInAsync()`
- Razor Page: `/Pages/Account/Login.cshtml`
- Add check for `IsActive` flag before allowing login
- Use `[AllowAnonymous]` attribute on login page

### API Contracts
N/A - This is a Razor Pages form POST, not an API endpoint.

### Data Requirements
- User table with: UserName, PasswordHash, IsActive
- ASP.NET Identity tables configured

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Username with leading/trailing spaces | Trim spaces before validation |
| Username case sensitivity | Case-insensitive username matching |
| Password with spaces | Allow spaces in password (don't trim) |
| SQL injection in username | Parameterized queries prevent injection |
| XSS in username field | Input sanitization, output encoding |
| Concurrent login from same account | Allow (no single-session restriction) |
| Login while already logged in | Redirect to dashboard |
| Browser back button after login | Dashboard loads (not cached login page) |

---

## Test Scenarios

- [ ] Successful login with valid credentials redirects to dashboard
- [ ] Failed login with wrong password shows error message
- [ ] Failed login with non-existent username shows generic error
- [ ] Failed login with inactive account shows deactivation message
- [ ] Empty username shows validation error
- [ ] Empty password shows validation error
- [ ] Login sets secure, HttpOnly cookie
- [ ] Login page accessible without authentication
- [ ] Already authenticated user redirected to dashboard
- [ ] Username trimmed of whitespace before validation

---

## Dependencies

### Story Dependencies
None - This is the first story in the epic.

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| ASP.NET Identity setup | Technical | Not Started |
| Database schema | Technical | Not Started |

---

## Estimation

**Story Points:** 3
**Complexity:** Low

---

## Open Questions

- [ ] Should we show different error messages for invalid username vs invalid password? (Security consideration: probably not) - Owner: Security Review

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
