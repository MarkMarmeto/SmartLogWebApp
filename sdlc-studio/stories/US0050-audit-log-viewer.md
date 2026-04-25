# US0050: Audit Log Viewer

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** to view system audit logs
**So that** I can investigate issues and ensure accountability

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Administrator who monitors system activity and investigates incidents.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

---

## Acceptance Criteria

### AC1: Audit Log Page
- **Given** I am logged in as Super Admin
- **When** I navigate to System > Audit Logs
- **Then** I see a list of audit log entries with columns:
  - Timestamp
  - User
  - Action
  - Entity Type
  - Entity ID
  - Details (truncated)
  - IP Address

### AC2: Default View
- **Given** I open the audit log page
- **Then** entries are sorted by timestamp (newest first)
- **And** showing last 24 hours by default
- **And** paginated (50 per page)

### AC3: View Log Entry Details
- **Given** I click on an audit log entry
- **Then** I see full details in a modal:
  - Full timestamp with timezone
  - User who performed action
  - Action type (e.g., "StudentCreated")
  - Entity type and ID
  - Full details/changes (JSON)
  - IP address
  - User agent (browser info)
  - Request ID (for correlation)

### AC4: Action Categories
- **Given** the audit log viewer
- **Then** I see actions categorized:
  - Authentication: Login, Logout, LoginFailed, 2FAVerified
  - User Management: UserCreated, UserUpdated, UserDeactivated
  - Student Management: StudentCreated, StudentUpdated, QrCodeGenerated
  - Faculty Management: FacultyCreated, FacultyLinked
  - System: ConfigurationChanged, DeviceRegistered
  - Scanner: ScanReceived, ScanRejected

### AC5: Change Details for Updates
- **Given** an "Updated" action entry
- **Then** details show what changed:
  ```json
  {
    "changes": {
      "Email": { "from": "old@email.com", "to": "new@email.com" },
      "Phone": { "from": "0917xxx", "to": "0918xxx" }
    }
  }
  ```

### AC6: Admin Access
- **Given** I am logged in as Admin Amy (not Super Admin)
- **When** I access Audit Logs
- **Then** I see a limited view:
  - Only logs related to my actions
  - No system-level logs
  - No other user's activities

### AC7: Immutable Logs
- **Given** audit log entries exist
- **Then** there is no option to edit or delete them
- **And** logs are append-only

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No logs in range | Show "No audit entries found" |
| Very large log volume | Efficient pagination, date range required |
| Sensitive data in logs | Mask passwords, tokens, etc. |
| Deleted user's logs | Show "User ID: xxx (deleted)" |
| System-generated actions | Show "System" as user |
| Log viewing is logged | Yes, create audit entry for viewing |

---

## Test Scenarios

- [ ] Audit log page loads
- [ ] Entries sorted by newest first
- [ ] Default shows last 24 hours
- [ ] Pagination works
- [ ] Detail modal shows full info
- [ ] Change details show old/new values
- [ ] Super Admin sees all logs
- [ ] Admin sees limited logs
- [ ] No edit/delete options
- [ ] Sensitive data masked
- [ ] Large log volume handled
- [ ] Export works (if implemented)

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0008](US0008-auth-audit-logging.md) | Functional | Audit logs exist | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Super Admin check | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
