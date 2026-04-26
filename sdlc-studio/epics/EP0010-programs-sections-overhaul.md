# EP0010: Programs & Sections Overhaul

> **Status:** In Progress
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V2 — Phase 2 (Feature Enhancements)

## Summary

Introduce a self-referencing `Program` entity with parent-child hierarchy (max 2 levels), mandatory section-program linking for graded levels, a `GradeLevelProgram` junction table, and a seeded "Non-Graded" grade level. Sections in graded levels must belong to a program; schools that don't use special programs assign sections to "REGULAR". **Non-Graded sections have no Program assignment** (re-opened 2026-04-26 — supersedes the earlier NG→REGULAR design). Broadcast messaging, attendance, and reports gain program-based filtering, with NG handled as a sibling branch outside the Program system.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Domain | Philippine K-12 structure (K, 1-12 + Non-Graded) | Seeded programs must match DepEd standards |
| TRD | Data | EF Core + SQL Server | Self-referencing FK, junction table, NOT NULL migration |
| TRD | Architecture | Monolithic Razor Pages | New admin pages for program CRUD |
| PRD | Flexibility | Schools define custom programs | System seeds defaults but allows full customization |

---

## Business Context

### Problem Statement
Philippine K-12 schools organize students by program/strand (e.g., STEM, ABM, TVL specializations, SPA sub-programs, Non-Graded levels). SmartLog currently has no Program concept — sections are just names under grade levels with no programmatic grouping. This makes broadcast targeting, attendance reporting, and enrollment management coarser than schools need.

**PRD Reference:** [Student Management](../prd.md#3-feature-inventory)

### Value Proposition
- Sections grouped by program enables targeted broadcasts (e.g., "all TVL students")
- Hierarchical programs (parent → children) support real-world school organization
- Non-Graded learners (SPED, ALS) are first-class citizens in the system
- Reports can filter and group by program within grade
- Enrollment sticker includes program info for permanent ID cards

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Broadcast targeting precision | Grade-level only | Grade + Program | Feature availability |
| Section-program coverage | 0% | 100% | All sections have ProgramId |
| Non-Graded learner support | None | Full parity | NG grade level with sections |

---

## Scope

### In Scope
- **Program entity:** Self-referencing hierarchy (ParentProgramId), max 2 levels, Code/Name/Description/IsActive/SortOrder
- **GradeLevelProgram junction:** Many-to-many linking programs to applicable grade levels
- **Section.ProgramId REQUIRED:** Every section must belong to a leaf program
- **Non-Graded grade level:** Code "NG", Name "Non-Graded", SortOrder 99, with sections Level 1-4
- **Seeded programs:** REGULAR (K-12 + NG), SPA (with sub-programs), SPE, SPJ, STE, STEM, ABM, HUMSS, GAS, TVL-HE/ICT/IA/AFA (with sub-programs), SPORTS, ADT
- **Student.Program denormalized field:** Auto-set from Section's leaf program code on enrollment
- **Broadcast.AffectedPrograms:** JSON array for program-targeted broadcasts; parent program includes all children
- **Attendance/Report filtering:** Program filter on APIs, dashboard, and report pages
- **Admin UI:** Program tree view, CRUD pages, section create/edit with required program dropdown
- **Data migration:** Seed missing grade levels (K, 1-6, NG), seed REGULAR program, assign all existing sections to REGULAR, enforce NOT NULL

### Out of Scope
- More than 2 levels of program hierarchy
- Program-specific curriculum or subject management
- Automatic program assignment based on student grades
- Inter-school program transfers

### Affected Personas
- **Admin Amy (Administrator):** Manages programs, assigns sections to programs, targets broadcasts by program
- **Teacher Tina (Teacher):** Sees program in class attendance view
- **Parents (Indirect):** Receive program-targeted broadcasts

---

## Acceptance Criteria (Epic Level)

- [ ] Program entity supports self-referencing parent-child hierarchy (max 2 levels)
- [ ] Only leaf programs (no children) can be assigned to sections
- [ ] Every section has a required ProgramId (NOT NULL after migration)
- [ ] "REGULAR" program is seeded and linked to all grade levels including NG
- [ ] Non-Graded grade level (Code: NG) is seeded with sections Level 1-4
- [ ] GradeLevelProgram junction correctly restricts which programs appear for each grade
- [ ] All DepEd standard programs/strands seeded (STEM, ABM, HUMSS, GAS, TVL strands, SPA sub-programs, etc.)
- [ ] Admin can create, edit, and deactivate programs via UI
- [ ] Section create/edit shows program dropdown filtered by grade level (leaf programs only)
- [ ] Broadcast targeting supports program selection; parent program includes children
- [ ] Attendance API accepts `?program=` filter parameter
- [ ] Reports include program filter and group by program within grade
- [ ] Student.Program is auto-set from section's leaf program on enrollment
- [ ] Existing sections migrated to REGULAR program without data loss
- [ ] **Non-Graded sections have no Program** — `Section.ProgramId` is nullable; required for graded levels, forbidden for NG (re-open 2026-04-26)
- [ ] NG seeded with 4 sections: LEVEL 1, LEVEL 2, LEVEL 3, LEVEL 4 — all with `ProgramId = null`
- [ ] No `GradeLevelProgram` rows reference the NG GradeLevel
- [ ] `Student.Program` is `null` for NG-enrolled students; auto-set/cleared on enrollment
- [ ] Section create/edit UI hides Program dropdown when GradeLevel = NG
- [ ] Broadcast targeting exposes Non-Graded as a sibling branch with LEVEL 1–4 selectable sections
- [ ] Attendance/Report `?program=` filter excludes NG; `?gradeLevel=NG` shortcut returns NG only; reports show "—" for Program column on NG rows
- [ ] Student Details, list, ID card, and enrollment sticker render NG students without a Program token

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0003: Student Management | Epic | Done | Development |
| EP0006: Attendance Tracking | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0009: SMS Strategy Overhaul | Epic | Broadcast program filtering |
| EP0013: QR Code Permanence & Card Redesign | Epic | Enrollment sticker includes Program |

---

## Risks & Assumptions

### Assumptions
- Schools will adopt the REGULAR program for sections without a specific track
- Two levels of hierarchy (parent → child) is sufficient for Philippine K-12
- Non-Graded sections follow the same attendance/SMS/reporting workflows as graded sections
- Existing sections can safely be assigned to REGULAR during migration

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Migration breaks existing sections | Low | High | Seed REGULAR first, assign all NULL → REGULAR, test thoroughly |
| Schools confused by mandatory program | Medium | Medium | Default to REGULAR; clear documentation |
| Hierarchy depth insufficient | Low | Low | Revisit if schools request grandchild programs |
| Seeded programs don't match school's needs | Low | Low | Schools can add/edit/deactivate programs freely |

---

## Technical Considerations

### Architecture Impact
- New entities: `Program` (with self-referencing FK), `GradeLevelProgram` (junction)
- Modified entities: `Section` (ProgramId required), `Student` (Program denormalized), `Broadcast` (AffectedPrograms)
- New admin pages: `/Admin/Programs`, `/Admin/Programs/Create`, `/Admin/Programs/Edit`
- Modified pages: Section create/edit, Student pages, Broadcast pages, Report pages
- Multi-step migration: seed grade levels → seed programs → seed GradeLevelPrograms → assign sections → enforce NOT NULL

### Integration Points
- `GradeSectionService` — extended with program CRUD
- `AttendanceService` / `AttendanceApiController` — program filter
- `SmsService` — program filter for broadcasts
- `DbInitializer` — seed programs, grade levels, junction records
- Broadcast pages — program tree checkbox UI
- Report pages / export APIs — program filter parameter

### Key Files to Modify
- **New:** `src/SmartLog.Web/Data/Entities/Program.cs`
- **New:** `src/SmartLog.Web/Data/Entities/GradeLevelProgram.cs`
- **New:** `src/SmartLog.Web/Pages/Admin/Programs/` (Index, Create, Edit)
- **Modify:** `src/SmartLog.Web/Data/Entities/Section.cs` (ProgramId required)
- **Modify:** `src/SmartLog.Web/Data/Entities/Student.cs` (Program field)
- **Modify:** `src/SmartLog.Web/Data/Entities/Broadcast.cs` (AffectedPrograms)
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` (entity configs)
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs` (seed programs, NG grade level, sections)
- **Modify:** `src/SmartLog.Web/Services/GradeSectionService.cs` (program logic)
- **Modify:** `src/SmartLog.Web/Services/AttendanceService.cs` (program filter)
- **Modify:** `src/SmartLog.Web/Services/Sms/SmsService.cs` (broadcast program filter)
- **Modify:** `src/SmartLog.Web/Controllers/Api/AttendanceApiController.cs`
- **Modify:** Broadcast, Section, Report pages
- **Migration:** New tables + data migration + NOT NULL enforcement

---

## Sizing

**Story Points:** TBD (estimated 8-10 stories)
**Estimated Story Count:** 8-10

**Complexity Factors:**
- Self-referencing entity with hierarchy validation
- Multi-step data migration (seed → assign → enforce NOT NULL)
- Cascade impact on broadcasts, attendance, reports, enrollment
- Admin UI with tree view for program hierarchy
- Grade-level-filtered program dropdowns

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0058](../stories/US0058-program-entity-hierarchy.md) | Program Entity & Self-Referencing Hierarchy | 5 | Done |
| [US0059](../stories/US0059-seed-k12-programs-nongraded.md) | Seed K-12 Programs & Non-Graded Level | 3 | Done |
| [US0060](../stories/US0060-section-program-mandatory.md) | Section-Program Mandatory Linking | 3 | Done |
| [US0061](../stories/US0061-program-admin-crud.md) | Program Admin CRUD Pages | 5 | Done |
| [US0062](../stories/US0062-broadcast-program-targeting.md) | Broadcast Program Targeting | 5 | Done |
| [US0063](../stories/US0063-attendance-report-program-filter.md) | Attendance & Report Program Filter | 5 | Done |
| [US0064](../stories/US0064-student-program-denormalization.md) | Student Program Denormalization | 3 | Done |
| [US0065](../stories/US0065-programs-data-migration.md) | Programs Data Migration | 3 | Done |
| [US0087](../stories/US0087-student-details-program-code-display.md) | Student Details — Display Program Code with Grade & Section | 2 | Draft |

**Total:** 34 story points across 9 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0010`

---

## Open Questions

- [x] Program model? — **Decision: Self-referencing (parent-child), max 2 levels**
- [x] Section-Program linking? — **Decision: REQUIRED — every section must have a program**
- [x] Non-Graded support? — **Decision: GradeLevel "NG" with sections Level 1-4, linked to REGULAR**
- [x] Seeded programs? — **Decision: DepEd K-12 standards + school-customizable**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2 feature brainstorm |
| 2026-04-24 | Claude | Added US0087 — Student Details Program Code display (V2.1 UX enhancement) |
