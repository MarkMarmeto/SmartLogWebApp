# US0002: Account Lockout

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** user accounts to be locked after multiple failed login attempts
**So that** brute force attacks are prevented and student data remains secure

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Department Head responsible for system security. Expert technical proficiency.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

### Background
Account lockout is a critical security measure to prevent brute force password attacks. After 5 consecutive failed login attempts, the account should be locked for 15 minutes. This protects against automated attack tools while allowing legitimate users to retry after the lockout period.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | 5 attempts, 15-minute lockout | Exact values enforced |
| TRD | Architecture | ASP.NET Identity | Use built-in lockout features |

---

## Acceptance Criteria

### AC1: Failed Attempt Counter
- **Given** a user account exists with username "admin.amy"
- **And** the account has 0 failed login attempts
- **When** I enter incorrect password for "admin.amy"
- **Then** the failed attempt counter increments to 1
- **And** I see error message "Invalid username or password"

### AC2: Account Lockout Trigger
- **Given** a user account has 4 failed login attempts
- **When** I enter incorrect password (5th attempt)
- **Then** the account becomes locked
- **And** I see error message "Your account has been locked due to multiple failed login attempts. Please try again in 15 minutes."
- **And** the lockout end time is set to current time + 15 minutes

### AC3: Locked Account Login Attempt
- **Given** a user account is locked
- **And** the lockout period has not expired
- **When** I attempt to log in with correct credentials
- **Then** login is denied
- **And** I see error message "Your account is locked. Please try again in X minutes." (showing remaining time)

### AC4: Lockout Expiry
- **Given** a user account was locked 15 minutes ago
- **When** I attempt to log in with correct credentials
- **Then** login succeeds
- **And** the failed attempt counter resets to 0

### AC5: Counter Reset on Success
- **Given** a user account has 3 failed login attempts
- **When** I log in with correct credentials
- **Then** login succeeds
- **And** the failed attempt counter resets to 0

### AC6: Audit Log Entry
- **Given** a user account becomes locked
- **Then** an audit log entry is created with:
  - Action: "AccountLocked"
  - UserId: the locked user's ID
  - Details: "Account locked after 5 failed attempts"
  - Timestamp: current time

### AC7: Manual Unlock by Super Admin
- **Given** a user account is locked
- **And** I am logged in as Super Admin
- **When** I navigate to the locked user's account
- **Then** I see an "Unlock Account" button
- **When** I click "Unlock Account"
- **Then** the account is unlocked immediately
- **And** the failed attempt counter resets to 0
- **And** I see success message "Account unlocked successfully"
- **And** an audit log entry is created with Action: "AccountUnlocked"

---

## Scope

### In Scope
- Tracking failed login attempt count per user
- Locking account after 5 consecutive failures
- 15-minute lockout duration
- Displaying lockout message with remaining time
- Resetting counter on successful login
- Audit logging of lockout events
- **Super Admin manual unlock capability**

### Out of Scope
- Configurable lockout threshold (hardcoded for now)
- IP-based rate limiting (per-account only)
- CAPTCHA after X attempts

---

## Technical Notes

### Implementation Approach
- Configure ASP.NET Identity lockout settings in `Program.cs`:
  ```csharp
  options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
  options.Lockout.MaxFailedAccessAttempts = 5;
  options.Lockout.AllowedForNewUsers = true;
  ```
- Use `SignInManager.PasswordSignInAsync()` with `lockoutOnFailure: true`
- Store `AccessFailedCount` and `LockoutEnd` in User table

### API Contracts
N/A - Lockout is handled internally by ASP.NET Identity.

### Data Requirements
- User table fields: AccessFailedCount, LockoutEnd, LockoutEnabled

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Lockout during login attempt | Current attempt completes, then lockout applies |
| Multiple simultaneous login attempts | Race condition handled by database locks |
| Server restart during lockout | Lockout persists (stored in database) |
| Clock skew between servers | Use UTC timestamps |
| Lockout exactly at 15 minutes | User can log in (inclusive boundary) |
| Failed attempts from multiple IPs | Still counted against account |
| Inactive account + lockout | Show deactivation message (takes priority) |
| 2FA failure after password success | Does not increment lockout counter |

---

## Test Scenarios

- [ ] First failed attempt increments counter to 1
- [ ] Fifth failed attempt triggers lockout
- [ ] Lockout message shows correct remaining time
- [ ] Login denied during lockout period even with correct password
- [ ] Login succeeds after lockout expires
- [ ] Successful login resets failed attempt counter
- [ ] Lockout persists across server restarts
- [ ] Audit log entry created on lockout
- [ ] Remaining time calculation is accurate
- [ ] Inactive account message takes priority over lockout message
- [ ] Super Admin can manually unlock accounts
- [ ] Manual unlock resets failed attempt counter
- [ ] Audit log entry created for manual unlock

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | Login flow implemented | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| ASP.NET Identity lockout configuration | Technical | Not Started |

---

## Estimation

**Story Points:** 2
**Complexity:** Low (mostly configuration)

---

## Stakeholder Decisions

- [x] Super Admin can manually unlock accounts - **Approved by IT Manager Ivan**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Added AC7: Manual unlock by Super Admin |
