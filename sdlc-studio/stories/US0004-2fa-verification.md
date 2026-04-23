# US0004: Two-Factor Authentication Verification

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to enter my authenticator code after my password when logging in
**So that** my account is protected even if my password is compromised

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant with 2FA enabled on her account.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
When a user has 2FA enabled, they must complete a second authentication step after entering their password. This story handles the TOTP verification flow and recovery code usage during login.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | TOTP-based 2FA | Standard 6-digit codes |
| TRD | Architecture | ASP.NET Identity | Use Identity 2FA verification |

---

## Acceptance Criteria

### AC1: Redirect to 2FA Page After Password
- **Given** I am on the login page
- **And** I have 2FA enabled on my account
- **When** I enter correct username and password
- **And** I click "Sign In"
- **Then** I am redirected to the 2FA verification page
- **And** I am NOT yet fully authenticated
- **And** I see an input field for the 6-digit code
- **And** I see a link "Use a recovery code instead"

### AC2: Successful TOTP Verification
- **Given** I am on the 2FA verification page
- **When** I enter the current 6-digit code from my authenticator app
- **And** I click "Verify"
- **Then** I am fully authenticated
- **And** I am redirected to the Dashboard
- **And** an authentication cookie is set

### AC3: Invalid TOTP Code
- **Given** I am on the 2FA verification page
- **When** I enter an incorrect 6-digit code
- **And** I click "Verify"
- **Then** I remain on the 2FA verification page
- **And** I see error message "Invalid verification code. Please try again."
- **And** I can try again

### AC4: Recovery Code Login
- **Given** I am on the 2FA verification page
- **And** I don't have access to my authenticator app
- **When** I click "Use a recovery code instead"
- **Then** I see an input field for the recovery code
- **When** I enter a valid, unused recovery code
- **And** I click "Verify"
- **Then** I am fully authenticated
- **And** the recovery code is marked as used
- **And** I am redirected to the Dashboard
- **And** I see a warning "You have X recovery codes remaining"

### AC5: Invalid Recovery Code
- **Given** I am on the recovery code entry page
- **When** I enter an invalid or already-used recovery code
- **And** I click "Verify"
- **Then** I see error message "Invalid or already used recovery code"
- **And** I can try again

### AC6: 2FA Timeout
- **Given** I am on the 2FA verification page
- **And** more than 5 minutes have passed since entering my password
- **When** I enter a valid TOTP code
- **Then** I see error message "Your session has expired. Please log in again."
- **And** I am redirected to the login page

### AC7: Cancel 2FA Verification
- **Given** I am on the 2FA verification page
- **When** I click "Cancel" or navigate away
- **Then** the partial authentication is invalidated
- **And** I must start the login process again

---

## Scope

### In Scope
- 2FA verification page UI
- TOTP code verification
- Recovery code verification
- Recovery code usage tracking
- Session timeout for 2FA step
- Navigation between TOTP and recovery code entry

### Out of Scope
- "Remember this device" option
- Backup phone number for SMS codes
- Recovery code regeneration (separate flow)

---

## Technical Notes

### Implementation Approach
- Use `SignInManager.TwoFactorAuthenticatorSignInAsync()` for TOTP
- Use `SignInManager.TwoFactorRecoveryCodeSignInAsync()` for recovery codes
- Store partial login state in session/cookie
- Razor Page: `/Pages/Account/LoginWith2fa.cshtml`
- Razor Page: `/Pages/Account/LoginWithRecoveryCode.cshtml`

### API Contracts
N/A - Razor Pages form interactions.

### Data Requirements
- RecoveryCode table with IsUsed flag
- Session/cookie to track partial authentication

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Code entered with spaces | Strip spaces before validation |
| Code entered with dashes | Strip dashes before validation |
| Old TOTP code (previous 30s window) | Accept (±1 window tolerance) |
| All recovery codes used | Show message to contact admin |
| Browser back button after 2FA | Require full re-login |
| 2FA page accessed directly (no partial auth) | Redirect to login |
| Account locked during 2FA step | Show lockout message |
| 2FA disabled by admin during login | Login completes without 2FA |
| Network error during verification | Show error, allow retry |
| Multiple 2FA attempts | No lockout on 2FA failures (only password) |

---

## Test Scenarios

- [ ] User with 2FA is redirected to 2FA page after password
- [ ] Valid TOTP code completes login successfully
- [ ] Invalid TOTP code shows error and allows retry
- [ ] Recovery code link navigates to recovery page
- [ ] Valid recovery code completes login
- [ ] Used recovery code is rejected
- [ ] Warning shown when recovery codes are low
- [ ] 2FA session expires after 5 minutes
- [ ] Cancel returns to login page
- [ ] Direct access to 2FA page redirects to login
- [ ] TOTP tolerance allows ±30 second codes

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | Login flow | Draft |
| [US0003](US0003-2fa-setup.md) | Functional | 2FA must be enabled | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| ASP.NET Identity 2FA | Library | Available |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Open Questions

- [ ] What is the timeout for the 2FA step? (Proposed: 5 minutes) - Owner: Security
- [ ] Should failed 2FA attempts contribute to account lockout? - Owner: Security (Proposed: No)

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
