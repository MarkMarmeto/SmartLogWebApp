# SmartLog - Stakeholder Story Review

**Project:** SmartLog School Information Management System
**Review Date:** 2026-02-04
**Total Stories:** 51 | **Total Points:** 142

---

## Review Instructions

For each story, stakeholders should:
1. ✅ **Approve** - Story is complete and ready for development
2. ⚠️ **Revise** - Story needs changes (note required changes)
3. ❓ **Clarify** - Questions need answers before approval

---

## Phase 1: Foundation (27 stories, 74 pts)

### EP0001: Authentication & Authorization (24 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0001 | Admin Login - Users can log in with username/password | 3 | ☐ | |
| US0002 | Account Lockout - Lock after 5 failed attempts for 15 min | 2 | ☐ | |
| US0003 | 2FA Setup - Optional TOTP authenticator setup | 5 | ☐ | |
| US0004 | 2FA Verification - Verify 6-digit code on login | 3 | ☐ | |
| US0005 | Session Management - 8-hour timeout, single session | 3 | ☐ | |
| US0006 | Role-Based Menu - Show menu items based on role | 2 | ☐ | |
| US0007 | Authorization Enforcement - Block unauthorized access | 3 | ☐ | |
| US0008 | Audit Logging - Log all auth events | 3 | ☐ | |

**Questions for Stakeholders:**
- [ ] Is 15-minute lockout duration appropriate?
- [ ] Should 2FA be mandatory for Super Admins?
- [ ] Is 8-hour session timeout acceptable?

---

### EP0002: User Management (14 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0009 | Create User - Admin creates new user accounts | 3 | ☐ | |
| US0010 | Edit User - Admin updates user information | 2 | ☐ | |
| US0011 | Deactivate User - Soft delete, can reactivate | 2 | ☐ | |
| US0012 | User List - Search by name, filter by role/status | 3 | ☐ | |
| US0013 | Assign Role - Change user's role | 2 | ☐ | |
| US0014 | Reset Password - Admin resets user password | 2 | ☐ | |

**Questions for Stakeholders:**
- [ ] Can Admin create other Admin users, or only Super Admin?
- [ ] Should password reset require email confirmation?
- [ ] Should deactivated users be hidden by default in list?

---

### EP0003: Student Management (24 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0015 | Create Student - Add new student with parent info | 3 | ☐ | |
| US0016 | Edit Student - Update student details | 2 | ☐ | |
| US0017 | Deactivate Student - Soft delete for transfers/graduates | 2 | ☐ | |
| US0018 | Student List - Search/filter by grade, section, name | 3 | ☐ | |
| US0019 | Generate QR - Auto-generate HMAC-signed QR on create | 5 | ☐ | |
| US0020 | Regenerate QR - Invalidate old, create new QR | 2 | ☐ | |
| US0021 | Print QR - Print individual student QR code | 2 | ☐ | |
| US0022 | Bulk Print QR - PDF with 6-8 QR codes per page | 5 | ☐ | |

**Questions for Stakeholders:**
- [ ] What is the Student ID format? (e.g., STU-2026-001)
- [ ] Should QR codes have an expiration date?
- [ ] What information should appear on printed QR cards?

---

### EP0004: Faculty Management (12 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0023 | Create Faculty - Add new faculty/staff member | 2 | ☐ | |
| US0024 | Edit Faculty - Update faculty details | 2 | ☐ | |
| US0025 | Deactivate Faculty - Soft delete | 2 | ☐ | |
| US0026 | Faculty List - Search/filter by department | 3 | ☐ | |
| US0027 | Link to User - Connect faculty to system login | 3 | ☐ | |

**Questions for Stakeholders:**
- [ ] What departments should be predefined?
- [ ] Should faculty linking to user be mandatory or optional?
- [ ] Do faculty need QR codes for entry tracking?

---

## Phase 2: Core Functionality (11 stories, 31 pts)

### EP0005: Scanner Integration (17 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0028 | Register Scanner - Add new scanner device with API key | 3 | ☐ | |
| US0029 | Device List - View/revoke scanner devices | 3 | ☐ | |
| US0030 | Scan API - POST endpoint to submit QR scans | 5 | ☐ | |
| US0031 | QR Validation - Verify HMAC signature | 3 | ☐ | |
| US0032 | Duplicate Detection - Reject same scan within 5 min | 2 | ☐ | |
| US0033 | Health Check - API status endpoint | 1 | ☐ | |

**Questions for Stakeholders:**
- [ ] Is 5-minute duplicate window appropriate?
- [ ] How many scanner devices will be deployed?
- [ ] Who can register new scanner devices? (Super Admin only?)

---

### EP0006: Attendance Tracking (14 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0034 | Attendance Dashboard - School-wide real-time view | 5 | ☐ | |
| US0035 | Class Attendance - Teacher's class-specific view | 3 | ☐ | |
| US0036 | Filter & Search - Filter by grade, section, status | 2 | ☐ | |
| US0037 | Auto-Refresh - Update every 30 seconds | 2 | ☐ | |
| US0038 | Historical View - View past dates | 2 | ☐ | |

**Questions for Stakeholders:**
- [ ] Is 30-second auto-refresh sufficient?
- [ ] How are teachers assigned to classes?
- [ ] What time defines "late" arrival? (e.g., after 7:30 AM)

---

## Phase 3: Enhancement (13 stories, 37 pts)

### EP0007: SMS Notifications (17 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0039 | SMS Templates - Configure entry/exit message templates | 2 | ☐ | |
| US0040 | SMS Rules - Enable/disable, entry only, exit only, both | 2 | ☐ | |
| US0041 | SMS Queue - Background worker for reliable delivery | 5 | ☐ | |
| US0042 | SMS Gateway - Integration with SMS provider | 3 | ☐ | |
| US0043 | SMS History - View sent messages and status | 3 | ☐ | |
| US0044 | SMS Opt-Out - Disable SMS per student | 2 | ☐ | |

**Questions for Stakeholders:**
- [ ] Which SMS gateway provider will be used?
- [ ] What is the monthly SMS budget?
- [ ] Should parents be able to opt-out via SMS reply?
- [ ] What happens if parent has multiple children enrolled?

---

### EP0008: Reporting & Analytics (20 pts)
*Reviewer: ___________________*

| ID | Story | Pts | Status | Notes |
|----|-------|-----|--------|-------|
| US0045 | Daily Report - Attendance for specific date | 3 | ☐ | |
| US0046 | Weekly Report - Week summary with trends | 3 | ☐ | |
| US0047 | Monthly Report - Compliance-ready monthly report | 3 | ☐ | |
| US0048 | Student History - Individual student attendance record | 3 | ☐ | |
| US0049 | Export PDF/Excel - Download reports | 3 | ☐ | |
| US0050 | Audit Log Viewer - View system activity logs | 3 | ☐ | |
| US0051 | Audit Log Search - Filter by user, action, date | 2 | ☐ | |

**Questions for Stakeholders:**
- [ ] What defines "chronically absent"? (e.g., >10% absences)
- [ ] How long should audit logs be retained?
- [ ] Are there specific compliance report formats required?

---

## Summary by Role

### Super Admin (Tony) - Primary Stories
- US0001-US0008: Authentication setup
- US0009-US0014: User management
- US0028-US0029: Device management
- US0050-US0051: Audit logs

### Admin (Amy) - Primary Stories
- US0015-US0022: Student management
- US0023-US0027: Faculty management
- US0034-US0038: Attendance monitoring
- US0039-US0049: SMS and reports

### Teacher (Tina) - Primary Stories
- US0035: Class attendance view
- US0018: Student list (read-only)
- US0026: Faculty list (read-only)

### Security (Gary) - Primary Stories
- US0030: Uses scanner device (indirect)
- Receives scan confirmations on device

---

## Open Questions Summary

### High Priority (Blocking)
1. Student ID format specification
2. SMS gateway provider selection
3. "Late" arrival time definition
4. Teacher-to-class assignment method

### Medium Priority
5. 2FA mandatory for admins?
6. Password complexity requirements
7. Session timeout duration
8. Audit log retention period

### Low Priority
9. QR code expiration
10. Department list customization
11. Report scheduling (future)

---

## Sign-Off

| Stakeholder | Role | Signature | Date |
|-------------|------|-----------|------|
| | Product Owner | | |
| | Technical Lead | | |
| | School Admin Rep | | |
| | IT Admin Rep | | |

---

## Next Steps After Review

1. Address all ⚠️ Revise items
2. Answer all ❓ Clarify questions
3. Update story files with changes
4. Mark approved stories as **Ready**
5. Begin Sprint 1 implementation
