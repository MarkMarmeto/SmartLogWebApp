# Implementation Plans Registry

**Project:** SmartLog School Information Management System
**Last Updated:** 2026-04-25

---

## Summary

| Metric | Count |
|--------|-------|
| Total Plans | 25 |
| Draft | 19 |
| In Progress | 0 |
| Complete | 6 |

---

## Plans by Epic

### EP0001: Authentication & Authorization

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0001](./PL0001-user-login.md) | US0001 | User Login | Complete | 2026-02-04 |

### EP0009: SMS Strategy Overhaul

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0002](./PL0002-no-scan-alert-service.md) | US0052 | End-of-Day No-Scan Alert Service | Complete | 2026-04-16 |
| [PL0003](./PL0003-no-scan-alert-config-dashboard.md) | US0053 | No-Scan Alert Admin Configuration & Dashboard | Draft | 2026-04-16 |

### EP0009: SMS Strategy Overhaul

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0009](./PL0009-no-scan-alert-next-run-label.md) | US0082 | No-Scan Alert Next Run Label | Done | 2026-04-19 |
| [PL0010](./PL0010-sms-settings-restructure.md) | US0083 | SMS Settings Restructure — Alert Toggle, Global Guard & Default Provider | Draft | 2026-04-19 |
| [PL0022](./PL0022-broadcast-program-first-targeting.md) | US0084 | Broadcast Targeting — Program-First with Nested Grade Levels | Draft | 2026-04-25 |
| [PL0023](./PL0023-broadcast-en-fil-message-inputs.md) | US0085 | Broadcast — Separate EN and FIL Message Inputs | Draft | 2026-04-25 |
| [PL0024](./PL0024-no-scan-alert-calendar-integration.md) | US0086 | No-Scan Alert — Calendar-Driven Auto-Disable & Event Prompt | Draft | 2026-04-25 |

### EP0012: Visitor Pass System

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0004](./PL0004-visitor-pass-entity-generation.md) | US0072 | Visitor Pass Entity & QR Generation | Done | 2026-04-18 |
| [PL0005](./PL0005-visitor-scan-processing.md) | US0073 | Visitor QR Routing & Scan Processing | Done | 2026-04-18 |
| [PL0006](./PL0006-visitor-pass-admin-ui.md) | US0074 | Visitor Pass Admin Management | Done | 2026-04-18 |
| [PL0007](./PL0007-visitor-scan-log.md) | US0075 | Visitor Scan Log | Done | 2026-04-18 |
| [PL0008](./PL0008-scanner-visitor-display.md) | US0076 | Scanner Visitor Scan Display | Done | 2026-04-18 |

### EP0006: Attendance Tracking

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0011](./PL0011-scan-logs-camera-column.md) | US0093 | Scan Logs — Record and Display Camera Identity | Complete | 2026-04-24 |

### EP0010: Programs & Sections Overhaul

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0025](./PL0025-student-details-program-code-display.md) | US0087 | Student Details — Display Program Code with Grade & Section | Draft | 2026-04-25 |

### EP0017: Data Retention & Archival

| ID | Story | Title | Status | Created |
|----|-------|-------|--------|---------|
| [PL0013](./PL0013-retention-policy-entity-and-admin-ui.md) | US0094 | Retention Policy Entity & Admin UI | Complete | 2026-04-25 |
| [PL0014](./PL0014-smsqueue-retention-handler.md) | US0095 | SmsQueue Retention Handler | Draft | 2026-04-24 |
| [PL0015](./PL0015-smslog-retention-handler.md) | US0096 | SmsLog Retention Handler | Draft | 2026-04-24 |
| [PL0016](./PL0016-broadcast-retention-handler.md) | US0097 | Broadcast Retention Handler | Draft | 2026-04-24 |
| [PL0017](./PL0017-scan-retention-handler.md) | US0098 | Scan Retention Handler | Draft | 2026-04-24 |
| [PL0018](./PL0018-auditlog-retention-with-legal-hold.md) | US0099 | AuditLog Retention with Legal-Hold Flag | Draft | 2026-04-24 |
| [PL0019](./PL0019-visitorscan-retention-handler.md) | US0100 | VisitorScan Retention Handler | Draft | 2026-04-24 |
| [PL0020](./PL0020-retention-scheduled-service-and-manual-run.md) | US0101 | Scheduled Retention Service + Manual Run | Draft | 2026-04-24 |
| [PL0021](./PL0021-retention-archive-to-file-export.md) | US0102 | Archive-to-File Export Before Purge | Draft | 2026-04-24 |

---

## Status Legend

| Status | Description |
|--------|-------------|
| Draft | Plan created, not yet started |
| In Progress | Implementation underway |
| Complete | All tasks done, AC verified |

---

## Next Steps

- PL0011 (US0093) Complete 2026-04-24.
- PL0013 (US0094) Complete 2026-04-25.
- PL0013–PL0021 (EP0017) all drafted 2026-04-24/25.
- PL0022–PL0025 (EP0009/EP0010 remaining stories) drafted 2026-04-25.
- EP0014, EP0015, EP0016 deferred to V3 — open business questions unresolved; no stories or plans yet.
- Next: implement EP0017 starting with US0095/PL0014 (SmsQueue Retention Handler).
