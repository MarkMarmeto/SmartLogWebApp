# EP0012: Visitor Pass System

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V2 — Phase 2 (Feature Enhancements)
> **Project:** Both (WebApp + ScannerApp)

## Summary

Implement a reusable anonymous visitor QR pass system. Admin configures a set of passes (default 20). Guards hand physical QR cards to visitors on arrival; visitors scan at the gate for entry/exit tracking. Passes cycle through Available → InUse → Available states. No SMS notifications for visitors. Separate QR prefix (`SMARTLOG-V:`) distinguishes visitor scans from student scans.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| TRD | Security | HMAC-SHA256 QR signing | Visitor QR uses same HMAC scheme with different prefix |
| TRD | Architecture | ScansApiController routing | Must branch on QR prefix (SMARTLOG: vs SMARTLOG-V:) |
| TRD | Data | EF Core + SQL Server | New VisitorPass and VisitorScan entities |

---

## Business Context

### Problem Statement
Schools need to track visitor entry and exit for security purposes. Currently, visitors are logged manually on paper or not tracked at all. A QR-based visitor pass system integrates with the existing scanner infrastructure, providing digital visitor logs without SMS overhead.

### Value Proposition
- Digital visitor tracking using existing scanner hardware
- Reusable passes minimize card printing costs
- Entry/exit timestamps provide security audit trail
- No SMS cost for visitor scans
- Pass status (Available/InUse) prevents unauthorized reuse
- Integrates seamlessly with existing scan flow

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Visitor logging | Manual/paper | 100% digital | VisitorScan records |
| Pass turnaround | N/A | < 5 seconds (scan-in/out) | ScannedAt timestamps |
| Pass utilization | N/A | All passes active | VisitorPass.IsActive count |

---

## Scope

### In Scope
- **VisitorPass entity:** Id, PassNumber, Code (VISITOR-001 to VISITOR-N), QrPayload, HmacSignature, QrImageBase64, IsActive, CurrentStatus (Available/InUse/Deactivated)
- **VisitorScan entity:** Id, VisitorPassId, DeviceId, ScanType (ENTRY/EXIT), ScannedAt, ReceivedAt, Status, AcademicYearId
- **QR prefix:** `SMARTLOG-V:{code}:{timestamp}:{hmac}` to distinguish from student QR
- **Scan flow:** ScansApiController parses prefix → routes to visitor scan handler → HMAC verify → lookup pass → check active → check duplicate → create VisitorScan → update pass status
- **Pass status transitions:** Available → InUse (on ENTRY scan), InUse → Available (on EXIT scan)
- **No SMS:** Visitor scans never trigger SMS notifications
- **Admin UI:** `/Admin/VisitorPasses` — list passes with status badges, generate/deactivate passes, print QR cards
- **Admin UI:** `/Admin/VisitorPasses/Log` — visitor scan history with date range filter
- **AppSettings:** `Visitor:MaxPasses` (default 20), admin-configurable
- **Scanner app:** Handle visitor scan response (no student name/grade/section, show "Visitor Pass #N — ENTRY/EXIT")

### Out of Scope
- Visitor identification (name, purpose, photo) — passes are anonymous
- Visitor appointment scheduling
- Visitor notification SMS
- Visitor badge printing with name
- Time-limited passes (auto-expire after N hours)

### Affected Personas
- **Admin Amy (Administrator):** Manages visitor passes, views visitor logs
- **Guard Gary (Security):** Hands out and collects passes, monitors scan results
- **SuperAdmin Tony (IT Admin):** Configures max pass count

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can configure the number of visitor passes (default 20)
- [ ] System generates visitor passes with `SMARTLOG-V:` QR prefix
- [ ] Visitor QR codes are HMAC-SHA256 signed like student QR codes
- [ ] Scanner correctly routes visitor QR scans (prefix detection)
- [ ] ENTRY scan sets pass status to InUse; EXIT scan sets to Available
- [ ] Duplicate visitor scans within 5 minutes are rejected
- [ ] No SMS is queued for visitor scans
- [ ] Admin can view all passes with status (Available / InUse / Deactivated)
- [ ] Admin can activate/deactivate individual passes
- [ ] Admin can print visitor QR cards
- [ ] Visitor scan log shows entry time, exit time, and duration
- [ ] Scanner app displays "Visitor Pass #N — ENTRY/EXIT" for visitor scans
- [ ] Inactive passes are rejected with appropriate status code

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0005: Scanner Integration | Epic | Done | Development |
| EP0003: Student Management | Epic | Done | Development (QrCodeService reuse) |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | Independent feature |

---

## Risks & Assumptions

### Assumptions
- Visitor passes are anonymous (no personal data collected)
- Guards reliably collect passes on visitor departure
- 20 concurrent visitor passes is sufficient for most schools
- Physical QR cards are durable enough for daily reuse

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Pass not returned (lost/stolen) | Medium | Low | Admin can deactivate; generate replacement |
| Guard forgets to collect pass | Medium | Low | InUse status visible on admin dashboard |
| Visitor scans wrong pass | Low | Low | Each pass has unique number; visual confirmation on scanner |
| Physical card wear/damage | Medium | Low | Laminated cards; easy to reprint |

---

## Technical Considerations

### Architecture Impact
- New entities: `VisitorPass`, `VisitorScan`
- Modified: `ScansApiController` — prefix-based routing (SMARTLOG: vs SMARTLOG-V:)
- Modified: `QrCodeService` — visitor QR generation with SMARTLOG-V: prefix
- New service: `VisitorPassService` — pass CRUD, status management, scan processing
- New admin pages: `/Admin/VisitorPasses`, `/Admin/VisitorPasses/Log`
- Scanner app: Handle absence of student data in scan response

### Integration Points
- `ScansApiController` — QR prefix routing
- `QrCodeService` — HMAC signing for visitor QR
- `ApplicationDbContext` — new entity configurations
- `DbInitializer` — seed default passes and AppSettings
- Scanner `MainViewModel` — handle visitor scan result display

### Key Files to Modify
- **New:** `src/SmartLog.Web/Data/Entities/VisitorPass.cs`
- **New:** `src/SmartLog.Web/Data/Entities/VisitorScan.cs`
- **New:** `src/SmartLog.Web/Services/VisitorPassService.cs`
- **New:** `src/SmartLog.Web/Pages/Admin/VisitorPasses/` (Index, Log)
- **Modify:** `src/SmartLog.Web/Controllers/Api/ScansApiController.cs` (visitor branch)
- **Modify:** `src/SmartLog.Web/Services/QrCodeService.cs` (visitor QR generation)
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` (new entities)
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs` (seed passes + AppSettings)
- **Modify:** Scanner `MainViewModel` (visitor scan result handling)

---

## Sizing

**Story Points:** TBD (estimated 5-7 stories)
**Estimated Story Count:** 5-7

**Complexity Factors:**
- QR prefix routing in scan API
- Pass status state machine (Available → InUse → Available)
- Dual-project changes (WebApp + ScannerApp)
- Admin UI with status badges and print functionality

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0072](../stories/US0072-visitor-pass-entity-generation.md) | Visitor Pass Entity & QR Generation | 5 | Done |
| [US0073](../stories/US0073-visitor-scan-processing.md) | Visitor QR Routing & Scan Processing | 5 | Done |
| [US0074](../stories/US0074-visitor-pass-admin-ui.md) | Visitor Pass Admin Management | 5 | Done |
| [US0075](../stories/US0075-visitor-scan-log.md) | Visitor Scan Log | 3 | Done |
| [US0076](../stories/US0076-scanner-visitor-display.md) | Scanner Visitor Scan Display | 3 | Done |

**Total:** 21 story points across 5 stories (Note: originally scoped 6 — pass config merged into US0074)

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0012`

---

## Open Questions

- [x] Visitor model? — **Decision: Reusable anonymous passes (VISITOR-001 to VISITOR-N)**
- [x] QR prefix? — **Decision: SMARTLOG-V: to distinguish from student SMARTLOG:**
- [x] SMS for visitors? — **Decision: No SMS notifications**
- [x] Pass count? — **Decision: Admin-configurable, default 20**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2 feature brainstorm |
