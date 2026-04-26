# US0105: Seed Non-Graded Grade Level + LEVEL 1–4 Sections Without Program

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0027: Seed Non-Graded Grade Level + LEVEL 1–4 Sections Without Program](../plans/PL0027-seed-ng-gradelevel-and-sections.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26
> **Marked Done:** 2026-04-26

## User Story

**As a** Tech-Savvy Tony (System Admin)
**I want** the database initializer to seed the Non-Graded grade level with sections LEVEL 1–4 and no Program assignment
**So that** every fresh install supports NG learners out-of-the-box without requiring admins to manually configure them.

## Context

### Persona Reference
**Tech-Savvy Tony** — owns DB seeding and consistency across installs.

### Background
EP0010 originally seeded NG linked to REGULAR. After US0103 makes `Section.ProgramId` nullable, the seeder must:
- Ensure `GradeLevel { Code = "NG", Name = "Non-Graded", SortOrder = 99 }` exists
- Seed four sections LEVEL 1, LEVEL 2, LEVEL 3, LEVEL 4 under NG with `ProgramId = null`
- Remove any `GradeLevelProgram` rows that link NG to a Program (NG must not appear in any Program's allowed-grades list)
- On dev DBs that already have NG sections pointing at REGULAR, NULL out their `ProgramId` (acceptable since pre-release per stakeholder)

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0103 | Data | `Section.ProgramId` is nullable | Seeder can write NG sections with null |
| EP0010 | Idempotency | DbInitializer must be re-runnable | NG seed is idempotent on Code/Name match |
| EP0010 | Domain | Each grade level may have many sections | Standard 4-section seed for NG |

---

## Acceptance Criteria

### AC1: NG Grade Level Seeded
- **Given** a fresh database
- **When** `DbInitializer.SeedAsync` runs
- **Then** a `GradeLevel` row exists with `Code = "NG"`, `Name = "Non-Graded"`, `SortOrder = 99`, `IsActive = true`

### AC2: NG Sections Seeded
- **Given** the NG grade level exists
- **Then** four `Section` rows exist with names `LEVEL 1`, `LEVEL 2`, `LEVEL 3`, `LEVEL 4`
- **And** all four are linked to GradeLevel NG
- **And** all four have `ProgramId = null`
- **And** all four have `IsActive = true`, default capacity

### AC3: NG Has No GradeLevelProgram Entries
- **Given** the seeded data
- **Then** there are zero `GradeLevelProgram` rows referencing the NG GradeLevel
- **And** no Program (including REGULAR) lists NG as an allowed grade

### AC4: Idempotent Re-Run
- **Given** seed has already run once
- **When** the seeder runs again
- **Then** no duplicate NG GradeLevel or Section rows are created
- **And** existing rows are not mutated unnecessarily

### AC5: Pre-Release Cleanup of Stale REGULAR Links
- **Given** a dev/test DB where NG sections currently have `ProgramId` pointing to REGULAR
- **When** the updated seeder runs
- **Then** those sections are updated to `ProgramId = null`
- **And** any existing `GradeLevelProgram` row linking NG to any Program is deleted

### AC6: Other Grade Levels Unchanged
- **Given** the seed run
- **Then** Grade Levels K, 1–12 and their `GradeLevelProgram` entries are unchanged
- **And** REGULAR Program continues to be linked to all graded levels (K, 1–12) but NOT to NG

---

## Scope

### In Scope
- `DbInitializer.cs` updates for the NG grade level, sections, and junction cleanup
- Idempotency checks
- One-time normalisation of stale REGULAR→NG links on re-run

### Out of Scope
- Schema migration (US0103)
- UI changes (US0104)
- Student.Program denormalisation (US0106)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Admin previously renamed an NG section (e.g., "LEVEL 1" → "Bridging A") | Seeder respects existing rows; only seeds the missing `LEVEL N` names |
| GradeLevel "NG" exists but with a different Name | Seeder leaves Name alone (admin-edited); only ensures Code/SortOrder consistent |
| Seed fails halfway (DB write error) | Wrap NG seed in a transaction; partial failure rolls back |

---

## Test Scenarios

- [ ] Fresh DB: NG grade + 4 sections + zero junction rows for NG
- [ ] Re-seed: no duplicates, no mutations
- [ ] Dev DB with NG→REGULAR: seeder NULLs ProgramId on those sections + removes junction rows
- [ ] REGULAR Program still linked to K and 1–12 after seed
- [ ] Seeder logs informative entries for each NG action

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs` — add `SeedNonGradedAsync` helper called from main seed flow
- **Possibly modify:** `src/SmartLog.Web/Services/GradeSectionService.cs` — if it has its own seeding/lookup that assumes NG has a Program

### Implementation Sketch
```csharp
private static async Task SeedNonGradedAsync(ApplicationDbContext db, ILogger logger)
{
    var ng = await db.GradeLevels.FirstOrDefaultAsync(g => g.Code == "NG");
    if (ng is null)
    {
        ng = new GradeLevel { Code = "NG", Name = "Non-Graded", SortOrder = 99, IsActive = true };
        db.GradeLevels.Add(ng);
        await db.SaveChangesAsync();
    }

    // Remove any GradeLevelProgram entries for NG (legacy)
    var staleJunction = db.GradeLevelPrograms.Where(j => j.GradeLevelId == ng.Id);
    db.GradeLevelPrograms.RemoveRange(staleJunction);

    // Null any NG sections still pointing at a Program (legacy from REGULAR link)
    var legacySections = db.Sections.Where(s => s.GradeLevelId == ng.Id && s.ProgramId != null);
    foreach (var s in legacySections) s.ProgramId = null;

    string[] names = { "LEVEL 1", "LEVEL 2", "LEVEL 3", "LEVEL 4" };
    foreach (var name in names)
    {
        var exists = await db.Sections.AnyAsync(s => s.GradeLevelId == ng.Id && s.Name == name);
        if (!exists)
        {
            db.Sections.Add(new Section { Name = name, GradeLevelId = ng.Id, ProgramId = null, IsActive = true });
        }
    }
    await db.SaveChangesAsync();
}
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0103](US0103-section-programid-nullable.md) | Data | Section.ProgramId nullable | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low — single-file change with well-defined idempotent logic.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
