# EP0009: SMS Strategy Overhaul

> **Status:** In Progress (re-opened 2026-04-24 — V2.1 additions US0084-US0086)
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V2 — Phase 2 (Feature Enhancements)

## Summary

Overhaul the SMS notification strategy from per-scan entry/exit alerts (high volume, high cost) to an end-of-day no-scan alert model (95% volume reduction). Add per-broadcast gateway selection, per-student entry/exit opt-in, and personal SMS from student profile.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Cost | SMS budget ~₱599/month (SMART SIM) | No-Scan Alert model is default |
| TRD | Architecture | SmsWorkerService (IHostedService) | Reuse existing worker for new message types |
| TRD | Data | SmsQueue table + SmsTemplate table | Add NO_SCAN_ALERT template and PERSONAL message type |
| PRD | Integration | Dual gateway: GSM Modem + Semaphore | Per-broadcast gateway selection |

---

## Business Context

### Problem Statement
The current entry/exit SMS model sends ~11,400 SMS/day for a 1,900-student school (6 scans/student average), costing ₱140,448/month via Semaphore or requiring unsustainable GSM modem throughput. Parents receive 2-6 messages daily, leading to alert fatigue. An end-of-day no-scan alert sends ~570 SMS/day (only students who didn't scan), reducing costs by 95% while delivering a higher-value safety signal.

**PRD Reference:** [SMS Notifications](../prd.md#3-feature-inventory)

### Value Proposition
- 95% reduction in SMS volume (11,400 → ~570/day)
- Monthly cost drops from ₱140,448 to ₱599 (SMART SIM)
- Parents receive a single, actionable alert only when their child is absent
- Schools can still opt individual students into entry/exit SMS if needed
- Per-broadcast gateway choice (online vs offline) for announcements
- Personal SMS enables direct admin-to-parent communication

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Daily SMS volume | ~11,400 | ~570 | SmsQueue daily count |
| Monthly SMS cost | ₱140,448 (Semaphore) | ₱599 (SMART SIM) | Billing |
| Parent complaint rate | Unknown | < 1% | Support tickets |
| No-Scan Alert delivery | N/A | > 98% by 7 PM | SmsLog delivery timestamps |

---

## Scope

### In Scope
- **End-of-Day No-Scan Alert:** Scheduled background job at configurable time (default 18:10), checks for students with zero scans, queues NO_SCAN_ALERT SMS
- **New SMS template:** NO_SCAN_ALERT (bilingual EN/FIL) with placeholders: {StudentFirstName}, {GradeLevel}, {Section}, {Date}, {SchoolPhone}
- **Entry/Exit SMS opt-in:** New `Student.EntryExitSmsEnabled` field (default: false); ScansApiController checks before queuing
- **Per-broadcast gateway:** Dropdown on Announcement/Emergency/BulkSend pages to choose SEMAPHORE or GSM_MODEM; default: SEMAPHORE
- **Personal SMS:** Button on student profile → modal → freeform SMS to ParentPhone + AlternatePhone; MessageType = PERSONAL
- **Admin UI:** No-Scan Alert time config in SMS Settings; last run display on SMS Dashboard; SMS Queue filtering by NO_SCAN_ALERT and PERSONAL types
- **Guards:** School day check, scanner health check (if zero total scans → suppress + alert admin), idempotency (no duplicate NO_SCAN_ALERT per student per day)

### Out of Scope
- Two-way SMS / parent replies
- Push notifications or WhatsApp
- Automatic gateway failover changes (existing fallback logic stays)
- SMS cost tracking / billing dashboard

### Affected Personas
- **Admin Amy (Administrator):** Configures no-scan alert time, selects gateway per broadcast, sends personal SMS
- **Parents (Indirect):** Receive fewer but more meaningful alerts; opt-in to entry/exit if desired
- **Guard Gary (Security):** No direct impact, but scanner health check protects against false alerts

---

## Acceptance Criteria (Epic Level)

- [x] At configurable time (default 18:10), system identifies students with zero accepted scans for the day *(US0052)*
- [x] NO_SCAN_ALERT SMS queued only on school days, only if at least one scan was recorded system-wide (scanner health guard) *(US0052)*
- [x] NO_SCAN_ALERT template seeded in both EN and FIL with correct placeholders *(US0052)*
- [x] Entry/Exit SMS disabled by default for all students; opt-in via `EntryExitSmsEnabled` flag *(US0054)*
- [x] ScansApiController skips SMS queue when `EntryExitSmsEnabled = false` *(US0054)*
- [x] ~~Broadcast creation UI includes gateway dropdown (SEMAPHORE / GSM_MODEM)~~ **Superseded by US0083**: per-broadcast dropdown removed in favor of a single default provider configured in SMS Settings (AC4/AC5 of US0083). The underlying capability to select provider per broadcast remains via the queued message's `Provider` field.
- [x] SmsWorkerService respects pre-set provider on queued messages *(US0055)*
- [x] Personal SMS can be sent from student profile to ParentPhone and AlternatePhone *(US0056)*
- [x] SMS Queue page supports filtering by NO_SCAN_ALERT and PERSONAL message types *(US0057)*
- [x] SMS Dashboard shows last no-scan alert run time and count *(US0053, US0082)*
- [x] No duplicate NO_SCAN_ALERT for the same student on the same day *(US0052)*

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0007: SMS Notifications | Epic | Done | Development |
| EP0006: Attendance Tracking | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | Independent enhancement |

---

## Risks & Assumptions

### Assumptions
- Schools close by 6 PM for students (configurable alert time handles variations)
- At least one scanner device is operational on any given school day
- Parents prefer a single absence alert over multiple entry/exit messages
- GSM modem can handle ~570 SMS within 1-2 hours (at 3s/SMS = ~28 minutes)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Alert time misconfigured (too early) | Low | Medium | Validate against school schedule; admin review |
| Scanner offline → false no-scan alerts | Medium | High | Zero-total-scans guard suppresses batch + alerts admin |
| Parent confusion during transition | Medium | Low | Communication from school; gradual rollout |
| GSM modem overload at alert time | Low | Medium | Stagger sends; Semaphore fallback |

---

## Technical Considerations

### Architecture Impact
- New `NoScanAlertService` (BackgroundService / IHostedService) — scheduled job
- New `SmsTemplate` seed: NO_SCAN_ALERT (EN + FIL)
- New `Student.EntryExitSmsEnabled` column (bool, default false)
- Broadcast entity: add `PreferredProvider` column (nullable string)
- SmsQueue: respect pre-set Provider field when present
- AppSettings: new key `Sms:NoScanAlertTime` (string, "HH:mm")

### Integration Points
- `CalendarService.IsSchoolDayAsync()` — school day guard
- `SmsWorkerService` — processes queued NO_SCAN_ALERT messages
- `ScansApiController` — checks `EntryExitSmsEnabled` before queuing
- `SmsService.QueueCustomSmsAsync()` — personal SMS
- Broadcast pages — gateway dropdown

### Key Files to Modify
- **New:** `src/SmartLog.Web/Services/Sms/NoScanAlertService.cs`
- **Modify:** `src/SmartLog.Web/Data/Entities/Student.cs` (EntryExitSmsEnabled)
- **Modify:** `src/SmartLog.Web/Data/Entities/Broadcast.cs` (PreferredProvider)
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs` (NO_SCAN_ALERT template seed)
- **Modify:** `src/SmartLog.Web/Controllers/Api/ScansApiController.cs` (opt-in check)
- **Modify:** `src/SmartLog.Web/Services/Sms/SmsWorkerService.cs` (respect pre-set provider)
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Settings.cshtml(.cs)` (alert time config)
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml(.cs)` (last run display)
- **Modify:** `src/SmartLog.Web/Program.cs` (register NoScanAlertService)
- **Modify:** Broadcast pages (gateway dropdown)
- **Modify:** Student detail page (personal SMS button + modal)

---

## Sizing

**Story Points:** TBD (estimated 8-10 stories)
**Estimated Story Count:** 8-10

**Complexity Factors:**
- Scheduled background job with multiple guards (school day, scanner health, idempotency)
- Per-student opt-in flag with migration
- Per-broadcast gateway selection threading through queue to worker
- Personal SMS modal with character count

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0052](../stories/US0052-no-scan-alert-service.md) | End-of-Day No-Scan Alert Service | 5 | Done |
| [US0053](../stories/US0053-no-scan-alert-config-dashboard.md) | No-Scan Alert Admin Configuration & Dashboard | 2 | Done |
| [US0054](../stories/US0054-entry-exit-sms-optin.md) | Entry/Exit SMS Opt-In per Student | 3 | Done |
| [US0055](../stories/US0055-per-broadcast-gateway.md) | Per-Broadcast Gateway Selection | 3 | Done |
| [US0056](../stories/US0056-personal-sms.md) | Personal SMS from Student Profile | 3 | Done |
| [US0057](../stories/US0057-sms-queue-type-filtering.md) | SMS Queue Message Type Filtering | 2 | Done |
| [US0082](../stories/US0082-no-scan-alert-next-run-label.md) | No-Scan Alert Next Run Label | 1 | Done |
| [US0083](../stories/US0083-sms-settings-restructure.md) | SMS Settings Restructure — Alert Toggle, Global Guard & Default Provider | 3 | Done |
| [US0084](../stories/US0084-broadcast-program-first-targeting.md) | Broadcast Targeting — Program-First with Nested Grade Levels | 5 | Draft |
| [US0085](../stories/US0085-broadcast-per-language-message-inputs.md) | Broadcast — Separate EN and FIL Message Inputs | 3 | Draft |
| [US0086](../stories/US0086-no-scan-alert-calendar-integration.md) | No-Scan Alert — Calendar-Driven Auto-Disable & Event Prompt | 3 | Draft |

**Total:** 33 story points across 11 stories (8 Done, 3 Draft)

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0009`

---

## Open Questions

- [x] End-of-Day vs Entry/Exit as default? — **Decision: End-of-Day No-Scan Alert is default**
- [x] Per-broadcast or global gateway? — **Decision: Per-broadcast dropdown, default Online (Semaphore)**
- [x] Where does personal SMS live? — **Decision: Button on Student Profile page**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2 feature brainstorm |
| 2026-04-22 | Claude | Closed out — all 8 stories Done; synced Story Breakdown statuses |
| 2026-04-24 | Claude | Re-opened for V2.1 additions: US0084 (Program-first targeting), US0085 (per-language message input), US0086 (calendar-driven alert suppression) |
