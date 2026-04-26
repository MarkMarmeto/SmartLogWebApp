# US0103: Section.ProgramId Nullable — Allow Sections Without Program (NG)

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0026: Section.ProgramId Nullable — Allow Sections Without Program (NG)](../plans/PL0026-section-programid-nullable.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26
> **Marked Done:** 2026-04-26

## User Story

**As a** Tech-Savvy Tony (System Admin)
**I want** `Section.ProgramId` to be nullable so that Non-Graded sections can exist without a Program
**So that** Non-Graded learners (SPED, ALS) are first-class citizens that don't need a fake "REGULAR" Program assignment to satisfy the schema.

## Context

### Persona Reference
**Tech-Savvy Tony** — owns schema integrity and migrations.

### Background
EP0010 currently requires every Section to have a `ProgramId` (NOT NULL), forcing NG sections to be linked to REGULAR. Stakeholder direction (2026-04-26): NG must have no Program at all — neither a real one nor a sentinel. This story relaxes the schema and adds an app-level rule that ProgramId is required for every grade level **except** NG.

No production data exists (system not yet released to schools), so no data preservation work is needed.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0010 | Data | Sections must have a Program for graded levels | Validation must still enforce ProgramId for non-NG grades |
| TRD | Data | EF Core + SQL Server | Migration drops NOT NULL on `Section.ProgramId` |
| Stakeholder | Domain | NG has no Program concept | Schema cannot require ProgramId for NG sections |

---

## Acceptance Criteria

### AC1: Column Becomes Nullable
- **Given** the Sections table after migration
- **Then** `ProgramId` is `uniqueidentifier NULL`
- **And** existing FK to `Programs.Id` is preserved (allowing NULL)

### AC2: Entity Model Updated
- **Given** the `Section` entity class
- **Then** `ProgramId` is declared `Guid?`
- **And** the `Program` navigation property is declared `Program?`

### AC3: Validation Rule — ProgramId Required for Non-NG
- **Given** a Section with GradeLevel ≠ NG
- **When** I attempt to save with `ProgramId = NULL`
- **Then** validation rejects the save with message "Program is required for graded sections"

### AC4: Validation Rule — ProgramId Forbidden for NG
- **Given** a Section with GradeLevel = NG
- **When** I attempt to save with `ProgramId` set
- **Then** validation rejects the save with message "Non-Graded sections must not have a Program"

### AC5: EF Configuration Updated
- **Given** `ApplicationDbContext.OnModelCreating`
- **Then** the FK relationship for `Section.Program` permits NULL (`OnDelete(DeleteBehavior.Restrict)` retained)

### AC6: No Production Data Loss
- **Given** the system is pre-release
- **Then** existing dev/test NG sections currently linked to REGULAR can be NULL'd as part of the seed step (US0105) — no separate data migration script required

---

## Scope

### In Scope
- EF Core migration making `Section.ProgramId` nullable
- `Section` entity model change (`Guid?` + nullable navigation)
- DbContext mapping update
- Service-layer validation rule (Section create/edit path)

### Out of Scope
- UI changes (covered by US0104)
- Re-seeding NG sections (covered by US0105)
- Student.Program denormalisation handling (covered by US0106)
- Updating Reports/Broadcast filters (US0107, US0108)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Admin attempts to delete the Program a non-NG section uses | Existing `Restrict` delete behaviour blocks it (unchanged) |
| Admin moves a section from a graded grade level to NG | Validation must clear ProgramId at the same time the GradeLevel changes; UI handles the prompt (US0104) |
| Admin moves a section from NG back to a graded level | Validation requires ProgramId to be set in the same save; UI prompts (US0104) |

---

## Test Scenarios

- [ ] Migration generated and applies cleanly to a fresh DB
- [ ] Migration applies cleanly to a dev DB that has NG sections pointing at REGULAR
- [ ] Saving a graded section with NULL ProgramId fails validation
- [ ] Saving an NG section with a ProgramId fails validation
- [ ] Saving an NG section with NULL ProgramId succeeds
- [ ] Existing non-NG sections continue to save unchanged

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Data/Entities/Section.cs` — `ProgramId` → `Guid?`, `Program` nav → nullable
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` — FK config for nullable Program
- **New migration:** `dotnet ef migrations add SectionProgramIdNullable -p src/SmartLog.Web`
- **Modify:** Section service / page handler validation (likely `Pages/Admin/CreateSection.cshtml.cs` and `EditSection.cshtml.cs`, or a shared `SectionService`)

### Validation Approach
Use a single helper (e.g. `SectionValidator.ValidateProgramAssignment(section, gradeLevel)`) called from both create and edit handlers. Returns model errors keyed to `ProgramId`.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0060](US0060-section-program-mandatory.md) | Predecessor | Original NOT NULL constraint being relaxed | Done |

### Blocks

- US0104 (UI) — depends on nullable model
- US0105 (Seed) — depends on nullable column
- US0106 (Student.Program) — depends on nullable Section.Program

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium — schema migration + validation rule; small surface area but careful migration testing.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
