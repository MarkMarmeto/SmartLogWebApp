# Epic Registry

**Project:** SmartLog School Information Management System
**Last Updated:** 2026-04-26

---

## Summary

| Metric | Count |
|--------|-------|
| Total Epics | 17 |
| V1 Phase 1 (Foundation) | 4 |
| V1 Phase 2 (Core) | 2 |
| V1 Phase 3 (Enhancement) | 2 |
| V2 Phase 2 (Feature Enhancements) | 6 |
| V3 Phase 3 (Commercial Readiness) | 3 |
| V1 Stories Created | 52 |
| V2 Stories Created | 51 |
| V2 Story Points | ~170 |
| Active Bugs | 3 |

---

## Epic Overview

| ID | Title | Phase | Status | Stories |
|----|-------|-------|--------|---------|
| [EP0001](./EP0001-authentication-authorization.md) | Authentication & Authorization | V1-P1 | Done | 8 |
| [EP0002](./EP0002-user-management.md) | User Management | V1-P1 | Done | 6 |
| [EP0003](./EP0003-student-management.md) | Student Management | V1-P1 | Done | 8 |
| [EP0004](./EP0004-faculty-management.md) | Faculty Management | V1-P1 | Done | 5 |
| [EP0005](./EP0005-scanner-integration.md) | Scanner Integration | V1-P2 | Done | 6 |
| [EP0006](./EP0006-attendance-tracking.md) | Attendance Tracking | V1-P2 | Done | 6 |
| [EP0007](./EP0007-sms-notifications.md) | SMS Notifications | V1-P3 | Done | 6 |
| [EP0008](./EP0008-reporting-analytics.md) | Reporting & Analytics | V1-P3 | Done | 7 |
| [EP0009](./EP0009-sms-strategy-overhaul.md) | SMS Strategy Overhaul | V2-P2 | Done | 11 |
| [EP0010](./EP0010-programs-sections-overhaul.md) | Programs & Sections Overhaul | V2-P2 | Done | 16 |
| [EP0011](./EP0011-multi-camera-scanning.md) | Multi-Camera Scanning | V2-P2 | In Progress | 11 |
| [EP0012](./EP0012-visitor-pass-system.md) | Visitor Pass System | V2-P2 | Done | 5 |
| [EP0013](./EP0013-qr-permanence-card-redesign.md) | QR Code Permanence & Card Redesign | V2-P2 | Done | 5 |
| [EP0014](./EP0014-product-licensing.md) | Product Licensing | V3-P3 | Draft | TBD |
| [EP0015](./EP0015-application-auto-update.md) | Application Auto-Update | V3-P3 | Draft | TBD |
| [EP0016](./EP0016-pii-ra10173-compliance.md) | PII & RA 10173 Compliance | V3-P3 | Draft | TBD |
| [EP0017](./EP0017-data-retention-archival.md) | Data Retention & Archival | V2-P2 | Draft | 9 |

---

## Dependency Graph

```
V1 — Phase 1 (Foundation)
┌─────────────────────────────────────────────────────────────┐
│  EP0001: Auth ──────┬──────> EP0002: Users                 │
│      │              └──────> EP0003: Students ─────────┐   │
│      └─────────────────────> EP0004: Faculty           │   │
└────────────────────────────────────────────────────────┼───┘
                                                         │
V1 — Phase 2 (Core)                                      │
┌────────────────────────────────────────────────────────┼───┐
│  EP0005: Scanner <─────────────────────────────────────┘   │
│      └──────────────────────> EP0006: Attendance           │
└───────────────────────────────────┼────────────────────────┘
                                    │
V1 — Phase 3 (Enhancement)         │
┌───────────────────────────────────┼────────────────────────┐
│  EP0007: SMS <────────────────────┤                        │
│  EP0008: Reports <────────────────┘                        │
└────────────────────────────────────────────────────────────┘

V2 — Phase 2 (Feature Enhancements)
┌────────────────────────────────────────────────────────────┐
│  EP0009: SMS Overhaul ←── EP0007                           │
│  EP0010: Programs & Sections ←── EP0003, EP0006            │
│  EP0011: Multi-Camera ←── EP0005         (ScannerApp)      │
│  EP0012: Visitor Passes ←── EP0005       (Both apps)       │
│  EP0013: QR Permanence ←── EP0003, EP0010                  │
│  EP0017: Data Retention ←── EP0006, EP0007, EP0009         │
└────────────────────────────────────────────────────────────┘

V3 — Phase 3 (Commercial Readiness) — DEFERRED
┌────────────────────────────────────────────────────────────┐
│  EP0014: Licensing ←── All V2 Epics                        │
│  EP0015: Auto-Update ←── EP0014                            │
│  EP0016: PII Compliance ←── EP0003                         │
└────────────────────────────────────────────────────────────┘
```

---

## V1 — Phase 1: Foundation (Must-Have)

Core functionality required for MVP.

| Epic | Description | Personas |
|------|-------------|----------|
| EP0001 | Authentication & Authorization | Tony, Amy |
| EP0002 | User Management | Tony, Amy |
| EP0003 | Student Management | Amy, Tina, Sarah |
| EP0004 | Faculty Management | Amy |

**Stories Created:** 27

---

## V1 — Phase 2: Core Functionality (Should-Have)

Enables the primary attendance tracking workflow.

| Epic | Description | Personas |
|------|-------------|----------|
| EP0005 | Scanner Integration | Tony, Gary |
| EP0006 | Attendance Tracking | Amy, Tina |

**Stories Created:** 11

---

## V1 — Phase 3: Enhancement (Should-Have)

Value-add features for complete solution.

| Epic | Description | Personas |
|------|-------------|----------|
| EP0007 | SMS Notifications | Amy |
| EP0008 | Reporting & Analytics | Tony, Amy, Tina |

**Stories Created:** 13

---

## V2 — Phase 2: Feature Enhancements (Priority)

New features for commercial readiness. Builds on V1 foundation.

| Epic | Description | Personas | Project |
|------|-------------|----------|---------|
| EP0009 | SMS Strategy Overhaul — No-Scan Alert, per-broadcast gateway, personal SMS, Program-first targeting, per-language inputs, calendar-aware alert | Amy, Gary | WebApp |
| EP0010 | Programs & Sections Overhaul — Self-referencing hierarchy, mandatory section-program, Non-Graded, Program code on student details | Amy, Tina | WebApp |
| EP0011 | Multi-Camera Scanning — 1-8 cameras, adaptive throttle, device-level scan type, Windows compat, camera identity in scans | Gary, Tony | ScannerApp |
| EP0012 | Visitor Pass System — Reusable anonymous passes, SMARTLOG-V: prefix, no SMS | Amy, Gary | Both |
| EP0013 | QR Code Permanence & Card Redesign — CR80 card, enrollment stickers, invalidation audit | Amy | WebApp |
| EP0017 | Data Retention & Archival — Per-entity retention, AuditLog legal-hold, archive-to-file export | Tony, Amy | WebApp |

**Stories Created:** 30 (118 story points)

---

## V3 — Phase 3: Commercial Readiness (Deferred)

Licensing, distribution, and legal compliance for commercial launch.

| Epic | Description | Personas | Project |
|------|-------------|----------|---------|
| EP0014 | Product Licensing — Online activation, JWT, feature gating | Tony | Both + New |
| EP0015 | Application Auto-Update — GitHub Releases via license server proxy | Tony | Both |
| EP0016 | PII & RA 10173 Compliance — Consent, data retention, privacy policy | Amy, Tony | WebApp |

**Estimated Stories:** 18-24

---

## Status Legend

| Status | Description |
|--------|-------------|
| Draft | Initial creation, needs review |
| Ready | Reviewed and approved for development |
| In Progress | Stories being implemented |
| Done | All stories complete |

---

## Next Steps

1. Run `/sdlc-studio story --epic EP0009` through `EP0013` to generate V2 user stories
2. Review and refine stories with stakeholders
3. Begin implementation with `/sdlc-studio code plan`

---

## Changelog

| Date | Change |
|------|--------|
| 2026-02-03 | Initial epic registry created with 8 epics (EP0001-EP0008) |
| 2026-02-04 | Generated 51 user stories for V1 epics |
| 2026-04-16 | Added 5 V2 Feature Epics (EP0009-EP0013) from brainstorm plan |
| 2026-04-16 | Added 3 V3 Commercial Epics (EP0014-EP0016) — deferred |
| 2026-04-16 | Added Non-Graded Learners to EP0010 scope |
| 2026-04-17 | EP0009 marked Done — all 6 stories implemented |
| 2026-04-17 | EP0010 marked Done — all 8 stories implemented (hierarchy removed, flat Program model) |
| 2026-04-18 | EP0013 marked Done — CR80 card, enrollment stickers, QR audit trail, invalidation modals |
| 2026-04-22 | EP0009 close-out — US0082 & US0083 added later and completed; all 8 stories Done; Story Breakdown table and epic AC synced |
| 2026-04-24 | V2.1 planning wave — EP0009 re-opened (+US0084-86), EP0010 re-opened (+US0087), EP0011 re-opened (+US0088-92), EP0006 re-opened (+US0093), new EP0017 Data Retention & Archival drafted (US0094-102). Bugs BG0001-BG0003 filed for UI polish. Memory `project_v2_1_planning.md` records the session. |
| 2026-04-26 | EP0010 re-opened again — Non-Graded learners now have **no Program** (supersedes earlier NG→REGULAR design). Stories US0103-US0109 drafted to make `Section.ProgramId` nullable, seed NG sections LEVEL 1–4, and propagate NG handling through enrollment, broadcast targeting, reports, student details, ID cards, and stickers. |
| 2026-04-26 | NG-no-program implementation landed — US0103-US0110 all Done (schema migration, NG seed, Section UI toggle, Student.Program null-for-NG, broadcast NG branch, attendance NG filter, student display, enrollment sticker removed). |
| 2026-04-26 | Registry reconciled — US0084-US0087, US0089-US0093 confirmed implemented; EP0009 and EP0010 marked Done. EP0011 remains In Progress (US0088 Windows compat verification pending). |
