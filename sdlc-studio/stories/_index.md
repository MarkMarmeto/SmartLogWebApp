# Story Registry

**Project:** SmartLog School Information Management System
**Last Updated:** 2026-04-26

---

## Summary

| Metric | Count |
|--------|-------|
| Total Stories | 110 |
| V1 Stories (Done) | 51 |
| V2 Stories (Done) | 32 |
| V2.1 Stories (Done) | 8 |
| V2.1 Stories (Draft) | 19 |
| In Progress | 0 |
| Done | 91 |
| Draft | 19 |
| V1 Story Points | 144 |
| V2 Story Points | 122 |
| V2.1 Story Points | ~73 |
| **Total Story Points** | **~339** |

---

## Stories by Epic

### EP0001: Authentication & Authorization (24 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0001](./US0001-user-login.md) | Admin Login | Done | 3 | Admin Amy |
| [US0002](./US0002-account-lockout.md) | Account Lockout | Done | 2 | Tech-Savvy Tony |
| [US0003](./US0003-2fa-setup.md) | Two-Factor Authentication Setup | Done | 5 | Admin Amy |
| [US0004](./US0004-2fa-verification.md) | Two-Factor Authentication Verification | Done | 3 | Admin Amy |
| [US0005](./US0005-session-management.md) | Session Management | Done | 3 | Admin Amy |
| [US0006](./US0006-role-based-menu.md) | Role-Based Menu Filtering | Done | 2 | Admin Amy |
| [US0007](./US0007-authorization-enforcement.md) | Authorization Policy Enforcement | Done | 3 | Admin Amy |
| [US0008](./US0008-auth-audit-logging.md) | Authentication Audit Logging | Done | 3 | Admin Amy |

### EP0002: User Management (14 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0009](./US0009-create-user.md) | Create User Account | Done | 3 | Admin Amy |
| [US0010](./US0010-edit-user.md) | Edit User Details | Done | 2 | Admin Amy |
| [US0011](./US0011-deactivate-user.md) | Deactivate/Reactivate User | Done | 2 | Admin Amy |
| [US0012](./US0012-reset-password.md) | Reset User Password | Done | 2 | Admin Amy |
| [US0013](./US0013-user-list.md) | User List with Search and Filter | Done | 3 | Admin Amy |
| [US0014](./US0014-manage-user-2fa.md) | Manage User 2FA | Done | 2 | Admin Amy |

### EP0003: Student Management (22 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0015](./US0015-create-student.md) | Create Student Record | Done | 3 | Admin Amy |
| [US0016](./US0016-edit-student.md) | Edit Student Details | Done | 2 | Admin Amy |
| [US0017](./US0017-deactivate-student.md) | Deactivate/Reactivate Student | Done | 2 | Admin Amy |
| [US0018](./US0018-student-list.md) | Student List with Search and Filter | Done | 3 | Admin Amy |
| [US0019](./US0019-generate-qr.md) | Generate Student QR Code | Done | 5 | Admin Amy |
| [US0020](./US0020-regenerate-qr.md) | Regenerate Student QR Code | Done | 2 | Admin Amy |
| [US0021](./US0021-print-qr.md) | Print Individual QR Code | Done | 2 | Admin Amy |
| [US0022](./US0022-bulk-print-qr.md) | Bulk Print QR Codes | Done | 5 | Admin Amy |

### EP0004: Faculty Management (10 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0023](./US0023-create-faculty.md) | Create Faculty Record | Done | 2 | Admin Amy |
| [US0024](./US0024-edit-faculty.md) | Edit Faculty Details | Done | 2 | Admin Amy |
| [US0025](./US0025-deactivate-faculty.md) | Deactivate/Reactivate Faculty | Done | 2 | Admin Amy |
| [US0026](./US0026-faculty-list.md) | Faculty List with Search and Filter | Done | 3 | Admin Amy |
| [US0027](./US0027-link-faculty-user.md) | Link/Unlink Faculty to User Account | Done | 3 | Admin Amy |

### EP0005: Scanner Integration (14 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0028](./US0028-register-device.md) | Register Scanner Device | Done | 3 | Admin Amy |
| [US0029](./US0029-device-list.md) | Device List and Revocation | Done | 3 | Admin Amy |
| [US0030](./US0030-scan-ingestion-api.md) | Scan Ingestion API | Done | 5 | Scanner Device |
| [US0031](./US0031-qr-validation.md) | QR Code Validation | Done | 3 | Scanner Device |
| [US0032](./US0032-duplicate-detection.md) | Duplicate Scan Detection | Done | 2 | Scanner Device |
| [US0033](./US0033-health-check.md) | Health Check Endpoint | Done | 1 | Scanner Device |

### EP0006: Attendance Tracking (14 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0034](./US0034-attendance-dashboard.md) | School-Wide Attendance Dashboard | Done | 5 | Admin Amy |
| [US0035](./US0035-class-attendance.md) | Class Attendance View | Done | 3 | Admin Amy |
| [US0036](./US0036-attendance-filter.md) | Attendance Filtering and Search | Done | 2 | Admin Amy |
| [US0037](./US0037-dashboard-refresh.md) | Dashboard Auto-Refresh | Done | 2 | Admin Amy |
| [US0038](./US0038-historical-date.md) | Historical Date Selection | Done | 2 | Admin Amy |
| [US0093](./US0093-scan-logs-camera-column.md) | Scan Logs — Record and Display Camera Identity | Draft | 3 | Admin Amy |

### EP0007: SMS Notifications (19 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0039](./US0039-sms-templates.md) | SMS Template Management | Done | 2 | Admin Amy |
| [US0040](./US0040-sms-rules.md) | SMS Notification Rules | Done | 2 | Admin Amy |
| [US0041](./US0041-sms-queue.md) | SMS Queue and Worker Service | Done | 5 | Admin Amy |
| [US0042](./US0042-sms-gateway.md) | SMS Gateway Integration | Done | 5 | Admin Amy |
| [US0043](./US0043-sms-history.md) | SMS History and Status | Done | 3 | Admin Amy |
| [US0044](./US0044-sms-optout.md) | SMS Opt-Out Management | Done | 2 | Admin Amy |

### EP0008: Reporting & Analytics (20 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0045](./US0045-daily-report.md) | Daily Attendance Report | Done | 3 | Admin Amy |
| [US0046](./US0046-weekly-report.md) | Weekly Attendance Summary | Done | 3 | Admin Amy |
| [US0047](./US0047-monthly-report.md) | Monthly Attendance Report | Done | 3 | Admin Amy |
| [US0048](./US0048-student-history.md) | Student Attendance History | Done | 3 | Admin Amy |
| [US0049](./US0049-report-export.md) | Report Export (PDF/Excel) | Done | 3 | Admin Amy |
| [US0050](./US0050-audit-log-viewer.md) | Audit Log Viewer | Done | 3 | Admin Amy |
| [US0051](./US0051-audit-log-search.md) | Audit Log Search and Filter | Done | 2 | Admin Amy |

### EP0009: SMS Strategy Overhaul (22 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0052](./US0052-no-scan-alert-service.md) | End-of-Day No-Scan Alert Service | Done | 5 | Admin Amy |
| [US0053](./US0053-no-scan-alert-config-dashboard.md) | No-Scan Alert Admin Config & Dashboard | Done | 2 | Admin Amy |
| [US0054](./US0054-entry-exit-sms-optin.md) | Entry/Exit SMS Opt-In per Student | Done | 3 | Admin Amy |
| [US0055](./US0055-per-broadcast-gateway.md) | Per-Broadcast Gateway Selection | Done | 3 | Admin Amy |
| [US0056](./US0056-personal-sms.md) | Personal SMS from Student Profile | Done | 3 | Admin Amy |
| [US0057](./US0057-sms-queue-type-filtering.md) | SMS Queue Message Type Filtering | Done | 2 | Admin Amy |
| [US0082](./US0082-no-scan-alert-next-run-label.md) | No-Scan Alert Next Run Label | Done | 1 | Admin Amy |
| [US0083](./US0083-sms-settings-restructure.md) | SMS Settings Restructure — Alert Toggle, Global Guard & Default Provider | Done | 3 | Admin Amy |
| [US0084](./US0084-broadcast-program-first-targeting.md) | Broadcast Targeting — Program-First with Nested Grade Levels | Draft | 5 | Admin Amy |
| [US0085](./US0085-broadcast-per-language-message-inputs.md) | Broadcast — Separate EN and FIL Message Inputs | Draft | 3 | Admin Amy |
| [US0086](./US0086-no-scan-alert-calendar-integration.md) | No-Scan Alert — Calendar-Driven Auto-Disable & Event Prompt | Draft | 3 | Admin Amy |

### EP0010: Programs & Sections Overhaul (56 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0058](./US0058-program-entity-hierarchy.md) | Program Entity (Flat Model) | Done | 5 | Admin Amy |
| [US0059](./US0059-seed-k12-programs-nongraded.md) | Seed K-12 Programs & Non-Graded Level | Done | 3 | Admin Amy |
| [US0060](./US0060-section-program-linking.md) | Section-Program Mandatory Linking | Done | 3 | Admin Amy |
| [US0061](./US0061-program-admin-crud.md) | Program Admin CRUD Pages | Done | 5 | Admin Amy |
| [US0062](./US0062-broadcast-program-targeting.md) | Broadcast Program Targeting | Done | 5 | Admin Amy |
| [US0063](./US0063-attendance-report-program-filter.md) | Attendance & Report Program Filter | Done | 5 | Admin Amy |
| [US0064](./US0064-student-program-denormalization.md) | Student Program Denormalization | Done | 3 | Admin Amy |
| [US0065](./US0065-programs-data-migration.md) | Programs Data Migration | Done | 3 | Admin Amy |
| [US0087](./US0087-student-details-program-code-display.md) | Student Details — Display Program Code with Grade & Section | Draft | 2 | Admin Amy |
| [US0103](./US0103-section-programid-nullable.md) | Section.ProgramId Nullable — Allow Sections Without Program (NG) | Done | 3 | Tech-Savvy Tony |
| [US0104](./US0104-section-ui-hide-program-for-ng.md) | Section Create/Edit — Hide Program Dropdown for Non-Graded | Done | 2 | Admin Amy |
| [US0105](./US0105-seed-ng-gradelevel-and-sections.md) | Seed Non-Graded Grade Level + LEVEL 1–4 Sections Without Program | Done | 2 | Tech-Savvy Tony |
| [US0106](./US0106-student-program-null-for-ng.md) | Student.Program Denormalisation — Null for Non-Graded Enrollments | Done | 2 | Tech-Savvy Tony |
| [US0107](./US0107-broadcast-targeting-ng-branch.md) | Broadcast Targeting — Add Non-Graded Branch Alongside Programs | Done | 5 | Admin Amy |
| [US0108](./US0108-attendance-reports-ng-handling.md) | Attendance — Non-Graded Filter Handling | Done | 1 | Admin Amy |
| [US0109](./US0109-student-details-card-ng-display.md) | Student Details, List & ID Card — Non-Graded Display | Done | 3 | Admin Amy |

### EP0011: Multi-Camera Scanning (43 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0066](./US0066-multi-camera-manager.md) | MultiCameraManager Service | Done | 8 | Guard |
| [US0067](./US0067-adaptive-decode-throttle.md) | Adaptive Decode Throttle | Done | 5 | Guard |
| [US0068](./US0068-multi-camera-grid-ui.md) | Multi-Camera Grid UI | Done | 5 | Guard |
| [US0069](./US0069-per-camera-scan-type.md) | Per-Camera Scan Type Configuration | Done | 3 | Guard |
| [US0070](./US0070-camera-error-isolation.md) | Camera Error Isolation & Health | Done | 5 | Guard |
| [US0071](./US0071-multi-camera-setup-page.md) | Multi-Camera Setup Page | Done | 5 | Guard |
| [US0088](../../../SmartLogScannerApp/sdlc-studio/stories/US0088-multi-camera-windows-compatibility.md) | Multi-Camera — Windows Platform Compatibility Verification | Draft | 3 | Tony |
| [US0089](../../../SmartLogScannerApp/sdlc-studio/stories/US0089-unify-scan-type-to-device-level.md) | Unify Scan Type to Device-Level (Deprecates US0069) | Draft | 3 | Guard |
| [US0090](../../../SmartLogScannerApp/sdlc-studio/stories/US0090-scan-payload-camera-identity.md) | Scan Payload — Include Camera Index and Camera Name | Draft | 3 | Admin Amy |
| [US0091](../../../SmartLogScannerApp/sdlc-studio/stories/US0091-scanner-section-name-trim-and-program-code.md) | Scanner Tile — Fix Section Name Trimming, Show Program Code | Draft | 2 | Guard |
| [US0092](../../../SmartLogScannerApp/sdlc-studio/stories/US0092-scanner-datetime-prominent-leftmost.md) | Scanner Header — Enlarge Date/Time, Anchor Left-Most | Draft | 1 | Guard |

> Stories US0088-US0092 primary files live in the ScannerApp registry (cross-project shadowing — same pattern as US0066-US0071). Links above point there.

### EP0012: Visitor Pass System (21 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0072](./US0072-visitor-pass-entity-generation.md) | Visitor Pass Entity & QR Generation | Done | 5 | Guard |
| [US0073](./US0073-visitor-scan-processing.md) | Visitor QR Routing & Scan Processing | Done | 5 | Guard |
| [US0074](./US0074-visitor-pass-admin-ui.md) | Visitor Pass Admin Management | Done | 5 | Admin Amy |
| [US0075](./US0075-visitor-scan-log.md) | Visitor Scan Log | Done | 3 | Admin Amy |
| [US0076](./US0076-scanner-visitor-display.md) | Scanner Visitor Scan Display | Done | 3 | Guard |

### EP0013: QR Code Permanence & Card Redesign (16 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0077](./US0077-cr80-card-template.md) | CR80 Card Template Redesign | Done | 5 | Admin Amy |
| [US0078](./US0078-enrollment-sticker-print.md) | Enrollment Sticker Print Page | Done | 3 | Admin Amy |
| [US0079](./US0079-qr-invalidation-audit.md) | QR Invalidation & Audit Trail | Done | 3 | Admin Amy |
| [US0080](./US0080-qr-regeneration-dialog.md) | QR Regeneration with Confirmation Dialog | Done | 3 | Admin Amy |
| [US0081](./US0081-invalidate-without-regen.md) | Invalidate QR Without Regeneration | Done | 2 | Admin Amy |
| [US0110](./US0110-remove-enrollment-sticker.md) | Remove Enrollment Sticker Feature | Done | 1 | Admin Amy |

### EP0017: Data Retention & Archival (24 pts)

| ID | Title | Status | Points | Persona |
|----|-------|--------|--------|---------|
| [US0094](./US0094-retention-policy-entity-and-admin-ui.md) | Retention Policy Entity & Admin UI | Draft | 3 | Admin Amy |
| [US0095](./US0095-smsqueue-retention-handler.md) | SmsQueue Retention Handler | Draft | 3 | Tony |
| [US0096](./US0096-smslog-retention-handler.md) | SmsLog Retention Handler | Draft | 3 | Tony |
| [US0097](./US0097-broadcast-retention-handler.md) | Broadcast Retention Handler | Draft | 2 | Tony |
| [US0098](./US0098-scan-retention-handler.md) | Scan Retention Handler | Draft | 3 | Tony |
| [US0099](./US0099-auditlog-retention-with-legal-hold.md) | AuditLog Retention with Legal Hold | Draft | 3 | Tony |
| [US0100](./US0100-visitorscan-retention-handler.md) | VisitorScan Retention Handler | Draft | 2 | Tony |
| [US0101](./US0101-retention-scheduled-service-and-dry-run.md) | Scheduled Retention Service + Manual Run + Dry-Run | Draft | 3 | Tony |
| [US0102](./US0102-retention-archive-to-file-export.md) | Archive-to-File Export Before Purge | Draft | 2 | Tony |

---

## Status Legend

| Status | Description |
|--------|-------------|
| Draft | Created, not yet reviewed |
| Ready | Review passed, implementation ready |
| Planned | Plan created |
| In Progress | Implementation underway |
| Review | AC verified, pending check |
| Done | Implemented, tested, checked |
