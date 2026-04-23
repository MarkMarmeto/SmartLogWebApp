# US0008: Authentication Audit Logging

> **Status:** Done
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** all authentication events to be logged
**So that** I can investigate security incidents and maintain compliance

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Department Head responsible for security auditing and incident investigation.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

### Background
Comprehensive audit logging of authentication events is essential for security monitoring, incident investigation, and regulatory compliance. All login attempts, logouts, session events, and access violations must be recorded.

---

## Inherited Constraints

> See Epic for full constraint chain. Key constraints for this story:

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| PRD | Security | All login attempts logged | Success and failure |
| TRD | Data | Audit logs immutable | Append-only, no deletions |

---

## Acceptance Criteria

### AC1: Successful Login Logged
- **Given** Admin Amy logs in successfully
- **Then** an audit log entry is created with:
  - Timestamp: 2026-02-03T08:30:00Z
  - Action: "LoginSuccess"
  - UserId: {Amy's user ID}
  - Username: "admin.amy"
  - IpAddress: {client IP}
  - UserAgent: {browser user agent}
  - Details: null

### AC2: Failed Login Logged
- **Given** someone attempts to log in with invalid credentials
- **Then** an audit log entry is created with:
  - Timestamp: current time
  - Action: "LoginFailed"
  - UserId: null (if username not found) or {user ID} (if username exists)
  - Username: {attempted username}
  - IpAddress: {client IP}
  - UserAgent: {browser user agent}
  - Details: "Invalid credentials"

### AC3: Account Lockout Logged
- **Given** a user's account becomes locked after failed attempts
- **Then** an audit log entry is created with:
  - Action: "AccountLocked"
  - UserId: {user ID}
  - Details: "Locked after 5 failed attempts"

### AC4: Logout Logged
- **Given** Admin Amy clicks logout
- **Then** an audit log entry is created with:
  - Action: "Logout"
  - UserId: {Amy's user ID}

### AC5: Session Expiration Logged
- **Given** Admin Amy's session expires due to inactivity
- **Then** an audit log entry is created with:
  - Action: "SessionExpired"
  - UserId: {Amy's user ID}

### AC6: 2FA Events Logged
- **Given** Admin Amy enables 2FA on her account
- **Then** an audit log entry is created with Action: "2FAEnabled"
- **Given** Admin Amy disables 2FA on her account
- **Then** an audit log entry is created with Action: "2FADisabled"
- **Given** Admin Amy fails 2FA verification
- **Then** an audit log entry is created with Action: "2FAFailed"

### AC7: Unauthorized Access Logged
- **Given** Teacher Tina attempts to access /Admin/Users
- **Then** an audit log entry is created with:
  - Action: "UnauthorizedAccess"
  - UserId: {Tina's user ID}
  - Details: "Attempted access to /Admin/Users"

### AC8: Audit Log Immutability
- **Given** audit log entries exist in the database
- **When** any attempt is made to modify or delete entries
- **Then** the operation is denied (database constraints)
- **And** the original entries remain unchanged

---

## Scope

### In Scope
- Logging all authentication events (listed above)
- IP address capture
- User agent capture
- Timestamp in UTC
- Immutable storage

### Out of Scope
- Audit log viewing UI (EP0008: Reporting)
- Log rotation/archival
- Real-time alerting on suspicious patterns
- Log export functionality

---

## Technical Notes

### Implementation Approach
- Create `IAuditService` with `LogAuthEventAsync()` method
- Inject into authentication handlers
- Use database triggers or constraints to prevent updates/deletes
- Log entry structure:

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}
```

### Action Types
| Action | When Logged |
|--------|-------------|
| LoginSuccess | Successful password + optional 2FA |
| LoginFailed | Invalid username or password |
| Logout | User clicks logout |
| SessionExpired | Session timeout |
| AccountLocked | After max failed attempts |
| AccountUnlocked | Manual unlock or lockout expires |
| 2FAEnabled | User enables 2FA |
| 2FADisabled | User or admin disables 2FA |
| 2FAFailed | Invalid TOTP code entered |
| UnauthorizedAccess | 403 response returned |

### Data Requirements
- AuditLog table with no UPDATE or DELETE permissions for application user
- Consider table partitioning for performance

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Audit logging fails | Log to fallback (file/console), don't block login |
| Very long user agent string | Truncate to 500 characters |
| IP address behind proxy | Use X-Forwarded-For if trusted |
| NULL username on failed login | Store attempted username in Details |
| Concurrent login attempts | Each logged independently |
| Database connection lost | Buffer logs, retry, fallback to file |
| Clock skew between servers | Use UTC, accept minor discrepancies |
| Large volume of failed attempts | Log all (rate limiting handles blocking) |

---

## Test Scenarios

- [ ] Successful login creates audit entry
- [ ] Failed login creates audit entry
- [ ] Logout creates audit entry
- [ ] Session expiration creates audit entry
- [ ] Account lockout creates audit entry
- [ ] 2FA enable/disable creates audit entries
- [ ] 2FA failure creates audit entry
- [ ] Unauthorized access creates audit entry
- [ ] IP address captured correctly
- [ ] User agent captured correctly
- [ ] Timestamps are in UTC
- [ ] Audit entries cannot be modified
- [ ] Audit entries cannot be deleted

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-user-login.md) | Functional | Login events to log | Draft |
| [US0002](US0002-account-lockout.md) | Functional | Lockout events to log | Draft |
| [US0003](US0003-2fa-setup.md) | Functional | 2FA events to log | Draft |
| [US0005](US0005-session-management.md) | Functional | Session events to log | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Access events to log | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| Database schema | Technical | Not Started |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Open Questions

- [ ] Should we log successful 2FA verification separately from LoginSuccess? - Owner: Security (Proposed: Include in LoginSuccess)
- [ ] How long should audit logs be retained? - Owner: Compliance

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
