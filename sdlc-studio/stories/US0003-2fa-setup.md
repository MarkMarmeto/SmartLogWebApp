# US0003: Two-Factor Authentication Setup

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to enable two-factor authentication on my account
**So that** my account has additional security protection against unauthorized access

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant with access to sensitive student data. Intermediate technical proficiency.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
Two-factor authentication (2FA) provides an additional layer of security by requiring a time-based one-time password (TOTP) in addition to the regular password. Users can set up 2FA using authenticator apps like Google Authenticator, Microsoft Authenticator, or Authy.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | TOTP-based 2FA | Use standard TOTP algorithm |
| TRD | Architecture | ASP.NET Identity | Use Identity 2FA features |

---

## Acceptance Criteria

### AC1: Access 2FA Setup Page
- **Given** I am logged in as Admin Amy
- **When** I navigate to My Profile > Security Settings
- **Then** I see a "Two-Factor Authentication" section
- **And** I see my current 2FA status (Enabled/Disabled)
- **And** I see a button to enable/disable 2FA

### AC2: Generate 2FA Secret Key
- **Given** I am on the Security Settings page
- **And** 2FA is not currently enabled
- **When** I click "Enable Two-Factor Authentication"
- **Then** I see a QR code containing the TOTP secret
- **And** I see the secret key displayed in text format (for manual entry)
- **And** I see instructions to scan the QR code with an authenticator app
- **And** I see an input field to verify the setup

### AC3: Verify and Activate 2FA
- **Given** I have scanned the QR code with my authenticator app
- **And** I see the setup verification page
- **When** I enter the current 6-digit code from my authenticator app
- **And** I click "Verify and Enable"
- **Then** 2FA is enabled on my account
- **And** I see a success message "Two-factor authentication has been enabled"
- **And** I am shown recovery codes (10 codes, each 8 characters)
- **And** I am prompted to save the recovery codes securely

### AC4: Invalid Verification Code
- **Given** I am on the 2FA setup verification page
- **When** I enter an incorrect 6-digit code
- **And** I click "Verify and Enable"
- **Then** I see an error message "Invalid verification code. Please try again."
- **And** 2FA is NOT enabled
- **And** I can try again with a new code

### AC5: Cancel 2FA Setup
- **Given** I am on the 2FA setup page with QR code displayed
- **When** I click "Cancel"
- **Then** I am returned to the Security Settings page
- **And** 2FA remains disabled
- **And** the generated secret key is discarded

### AC6: Audit Log Entry
- **Given** I successfully enable 2FA
- **Then** an audit log entry is created with:
  - Action: "2FAEnabled"
  - UserId: my user ID
  - Timestamp: current time

---

## Scope

### In Scope
- Security settings page with 2FA section
- QR code generation for authenticator apps
- Manual secret key display
- TOTP verification during setup
- Recovery codes generation (10 codes)
- Audit logging of 2FA enable/disable

### Out of Scope
- Admin enabling 2FA for other users (separate permission)
- SMS-based 2FA (TOTP only)
- Hardware security keys (U2F/WebAuthn)
- Recovery code regeneration

---

## Technical Notes

### Implementation Approach
- Use ASP.NET Identity `UserManager.GenerateNewTwoFactorRecoveryCodesAsync()`
- Use `UserManager.GetAuthenticatorKeyAsync()` for TOTP secret
- QR code format: `otpauth://totp/SmartLog:{username}?secret={key}&issuer=SmartLog`
- Use QRCoder library for QR code generation
- Store `TwoFactorEnabled` flag in User table

### API Contracts
N/A - Razor Pages form interactions.

### Data Requirements
- User table fields: TwoFactorEnabled, AuthenticatorKey
- RecoveryCode table: UserId, Code, IsUsed

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| User already has 2FA enabled | Show "Disable 2FA" option instead |
| QR code won't scan | Display manual entry key |
| Verification code expired (30s window) | Try next code, allow ±1 window tolerance |
| User closes browser during setup | Secret discarded, must restart |
| Network error during verification | Show error, allow retry |
| Recovery codes already exist | Regenerate new codes on re-enable |
| Invalid characters in verification input | Only allow 6 digits |
| Session timeout during setup | Require re-login, restart setup |

---

## Test Scenarios

- [ ] 2FA setup page accessible from security settings
- [ ] QR code is generated and displayed correctly
- [ ] Manual secret key is displayed for copy
- [ ] Valid TOTP code enables 2FA successfully
- [ ] Invalid TOTP code shows error and allows retry
- [ ] 10 recovery codes are generated on enable
- [ ] Cancel button discards setup and returns to settings
- [ ] Audit log entry created on 2FA enable
- [ ] QR code contains correct issuer and username
- [ ] TOTP verification allows ±30 second tolerance

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | User must be logged in | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| QRCoder NuGet package | Library | Not Started |
| TOTP library (built into Identity) | Library | Available |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium

---

## Open Questions

- [ ] Should recovery codes be downloadable as a file? - Owner: UX
- [ ] How many recovery codes should be generated? (Proposed: 10) - Owner: Security

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
