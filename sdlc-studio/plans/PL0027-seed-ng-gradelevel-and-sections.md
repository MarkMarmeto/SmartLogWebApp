# PL0027: Seed Non-Graded Grade Level + LEVEL 1–4 Sections Without Program

> **Status:** Complete
> **Story:** [US0105: Seed Non-Graded Grade Level + LEVEL 1–4 Sections Without Program](../stories/US0105-seed-ng-gradelevel-and-sections.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Add a programmatic seed for the Non-Graded grade level and its four sections (LEVEL 1–4) into `DbInitializer.SeedAsync`. The seed is idempotent (safe to re-run on every app start), creates `GradeLevel { Code="NG", Name="Non-Graded", SortOrder=99 }` and four sections with `ProgramId = null`, and performs a one-time normalisation: any NG sections currently pointing at REGULAR (legacy from the prior EP0010 design) are NULL'd, and any `GradeLevelProgram` rows referencing NG are removed. No new schema; relies on US0103's nullable column.

`DbInitializer` today only seeds Roles, default users, Faculty, and SMS templates — it does **not** seed GradeLevels, Programs, or Sections. We're introducing the first programmatic GradeLevel/Section seed here, scoped narrowly to NG.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | NG GradeLevel seeded | Code="NG", Name="Non-Graded", SortOrder=99, IsActive=true exists after first run |
| AC2 | LEVEL 1–4 sections seeded | Four sections, GradeLevelId=NG, ProgramId=null, default capacity, IsActive=true |
| AC3 | No GradeLevelProgram for NG | Zero junction rows reference NG; legacy ones removed |
| AC4 | Idempotent | Re-run produces no duplicates and no unnecessary mutations |
| AC5 | Stale REGULAR cleanup | Dev DB legacy NG→REGULAR section links get nulled; NG junction rows deleted |
| AC6 | Other grade levels unchanged | K, 1–12 and their REGULAR junctions remain intact |

---

## Technical Context

### Language & Framework
- **Primary:** C# 12 / ASP.NET Core 8.0 + EF Core 8.0
- **Test framework:** xUnit + EF SQLite-in-memory via `TestDbContextFactory`

### Key Existing Files
- `src/SmartLog.Web/Data/DbInitializer.cs` — single static `SeedAsync` method (~line 17), called from `Program.cs:213`. Currently seeds: roles, admin/super-admin/inactive/teacher/security users, faculty rows, SMS templates.
- `src/SmartLog.Web/Data/Entities/GradeLevel.cs`, `Section.cs`, `GradeLevelProgram.cs` — entities.
- `tests/SmartLog.Web.Tests/Helpers/TestDbContextFactory.cs` — test fixture with `SeedGradeLevels` (7-12 only), `SeedPrograms` (REGULAR), `SeedSections`.

### Key Existing Patterns
- `DbInitializer.SeedAsync` is idempotent and uses "lookup-or-create" via direct DbSet queries. New helper functions follow that pattern.
- `Section.ProgramId` is `Guid?` (per US0103, just landed).
- Service-level validation forbids NG sections from having a Program — but `DbInitializer` writes directly through the context, **bypassing** `GradeSectionService.CreateSectionAsync`. That's fine for seed code; it sets `ProgramId = null` directly.

### Current State of NG / REGULAR Seed
There is **no programmatic seed** for GradeLevels or Programs today. They were created by an admin or a one-time migration. So this is a greenfield seed: we're not extending an existing routine; we're introducing the first programmatic GradeLevel/Section seed (scoped narrowly to NG).

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Seed code is mechanical, idempotency is the key invariant, and the four ACs map cleanly to four straightforward integration tests against an in-memory context. TDD adds little here.

---

## Implementation Phases

### Phase 1: Add `SeedNonGradedAsync` to DbInitializer

**Goal:** New private static method that performs the entire NG seed + cleanup, idempotent.

- [ ] In `src/SmartLog.Web/Data/DbInitializer.cs`, add a private static method:
  ```csharp
  private static async Task SeedNonGradedAsync(ApplicationDbContext context, ILogger logger)
  {
      // 1. Ensure NG GradeLevel exists.
      var ng = await context.GradeLevels.FirstOrDefaultAsync(g => g.Code == "NG");
      if (ng == null)
      {
          ng = new GradeLevel
          {
              Code = "NG",
              Name = "Non-Graded",
              SortOrder = 99,
              IsActive = true,
              CreatedAt = DateTime.UtcNow
          };
          context.GradeLevels.Add(ng);
          await context.SaveChangesAsync();
          logger.LogInformation("Seeded Non-Graded GradeLevel (ID: {Id})", ng.Id);
      }

      // 2. Remove any GradeLevelProgram rows referencing NG (legacy from NG→REGULAR design).
      var staleJunction = await context.GradeLevelPrograms
          .Where(j => j.GradeLevelId == ng.Id)
          .ToListAsync();
      if (staleJunction.Count > 0)
      {
          context.GradeLevelPrograms.RemoveRange(staleJunction);
          logger.LogInformation("Removed {Count} legacy GradeLevelProgram rows for NG", staleJunction.Count);
      }

      // 3. Null any NG sections still pointing at a Program (legacy NG→REGULAR).
      var legacyLinkedSections = await context.Sections
          .Where(s => s.GradeLevelId == ng.Id && s.ProgramId != null)
          .ToListAsync();
      foreach (var s in legacyLinkedSections)
      {
          s.ProgramId = null;
      }
      if (legacyLinkedSections.Count > 0)
      {
          logger.LogInformation("Cleared ProgramId on {Count} legacy NG sections", legacyLinkedSections.Count);
      }

      // 4. Ensure LEVEL 1..4 sections exist for NG.
      string[] levelNames = { "LEVEL 1", "LEVEL 2", "LEVEL 3", "LEVEL 4" };
      foreach (var name in levelNames)
      {
          var exists = await context.Sections
              .AnyAsync(s => s.GradeLevelId == ng.Id && s.Name == name);
          if (!exists)
          {
              context.Sections.Add(new Section
              {
                  Name = name,
                  GradeLevelId = ng.Id,
                  ProgramId = null,
                  Capacity = 40,
                  IsActive = true,
                  CreatedAt = DateTime.UtcNow
              });
              logger.LogInformation("Seeded NG section: {Name}", name);
          }
      }

      await context.SaveChangesAsync();
  }
  ```

- [ ] Wrap the entire body in a try/catch that logs and rethrows so a partial failure during seed surfaces clearly. Optional — `DbInitializer.SeedAsync` doesn't currently use this pattern. Skip if it adds noise.

**Files:** `src/SmartLog.Web/Data/DbInitializer.cs`

### Phase 2: Wire the Seed into the Main Seed Flow

**Goal:** Invoke `SeedNonGradedAsync` from `SeedAsync` so app startup performs it.

- [ ] At the end of `DbInitializer.SeedAsync` (after existing role/user/faculty/SMS-template seeds), call:
  ```csharp
  await SeedNonGradedAsync(context, logger);
  ```
- [ ] Place it **before** the existing student-enrollment migration block (the loop starting at `~line 415`) so that block can find NG GradeLevel/Sections if it ever encounters NG students (future-proofing).

**Files:** `src/SmartLog.Web/Data/DbInitializer.cs`

### Phase 3: Tests — Helper Refactor

**Goal:** Make the seed callable from tests without booting the whole `SeedAsync` (which needs UserManager, RoleManager, etc.).

- [ ] In `DbInitializer.cs`, change `SeedNonGradedAsync` from `private static` to `internal static` so the test project (which has `InternalsVisibleTo` if configured, or via the same assembly) can call it directly. If the test project does NOT have InternalsVisibleTo, instead expose a `public static Task SeedNonGradedAsync(ApplicationDbContext, ILogger)` — small surface, well-named, acceptable.
- [ ] Verify by checking `src/SmartLog.Web/SmartLog.Web.csproj` for `<InternalsVisibleTo>` (likely none — go with `public static`).

**Files:** `src/SmartLog.Web/Data/DbInitializer.cs` (visibility change only)

### Phase 4: Tests — Idempotency, Cleanup, and Section Set

**Goal:** Cover all six ACs with focused integration tests.

Add a new test file: `tests/SmartLog.Web.Tests/Data/DbInitializerNonGradedTests.cs` (mirrors the existing `Services/` layout but Data tests are uncommon — using `Data/` keeps things organised; if the test project structure prefers a flat folder, place it in `Services/`).

Tests:
- [ ] **SeedNonGraded_FreshDb_CreatesGradeLevelAndFourSections**
  - Empty DB → `SeedNonGradedAsync` → assert NG exists with correct Code/Name/SortOrder; assert exactly 4 NG sections with names `LEVEL 1..4` and `ProgramId == null`.
- [ ] **SeedNonGraded_RunTwice_NoDuplicates**
  - Run seed twice → assert NG count == 1; section count == 4.
- [ ] **SeedNonGraded_RemovesGradeLevelProgramJunctionForNG**
  - Pre-seed NG GradeLevel + a REGULAR Program + a `GradeLevelProgram { NG.Id, REGULAR.Id }` row → run seed → assert zero junction rows for NG.
- [ ] **SeedNonGraded_NullsProgramIdOnLegacyNgSections**
  - Pre-seed NG + REGULAR + an NG section with `ProgramId = REGULAR.Id` and Name "Bridging A" (custom legacy name) → run seed → assert that section's `ProgramId` is now null **and** the existing custom-name section is preserved (not renamed); LEVEL 1–4 are added alongside.
- [ ] **SeedNonGraded_DoesNotTouchOtherGradeLevels**
  - Pre-seed Grade 7 + Grade 8 + a `GradeLevelProgram { Grade7.Id, REGULAR.Id }` → run seed → assert Grade 7/8 unchanged; their junction row preserved.
- [ ] **SeedNonGraded_RespectsAdminEditedNGName**
  - Pre-seed an NG GradeLevel with Name "Special Education" (admin-renamed) → run seed → assert Name is unchanged (only Code is the identity key).

**Files:** `tests/SmartLog.Web.Tests/Data/DbInitializerNonGradedTests.cs` (new)

### Phase 5: Manual Smoke

- [ ] Run the app: `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"`. On startup, verify the log lines:
  - "Seeded Non-Graded GradeLevel (ID: …)"
  - "Seeded NG section: LEVEL 1" × 4
- [ ] Stop and restart — verify no new "Seeded …" log lines appear (idempotency).
- [ ] Visit `/Admin/Sections` and confirm the four LEVEL sections appear under "Non-Graded".

### Phase 6: Build, Test, Check

- [ ] `dotnet build` — clean.
- [ ] `dotnet test --filter "FullyQualifiedName~DbInitializerNonGraded"` — green.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` — full suite (excluding the known pre-existing NoScanAlert failures).

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Data/DbInitializer.cs` | Modify (new method + invocation + visibility) | 1, 2, 3 |
| `tests/SmartLog.Web.Tests/Data/DbInitializerNonGradedTests.cs` | Create | 4 |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Seed runs against an admin's manually-renamed NG sections, removing customisations | The seed only **creates** the four canonical names if missing — never renames, never deletes. Custom NG sections are preserved. |
| Seed nulls a `ProgramId` on a section that an admin had deliberately reassigned (post-EP0010 design flip) | Acceptable per stakeholder decision (2026-04-26) — pre-release, no production data; the new rule is "NG has no Program" period. Documented in seed log. |
| Concurrent first-run seeds from multiple app instances racing | `DbInitializer.SeedAsync` is the existing pattern and is presumed safe for current deployment topology (single-instance). No change in this story. |
| `InternalsVisibleTo` missing — `internal static` doesn't work | Plan calls for `public static` fallback; small surface, named for the operation. |
| Section.ProgramId still NOT NULL on a DB that hasn't yet applied the US0103 migration | Plan's prerequisite: US0103 migration applied. Seed will throw FK or NOT NULL error otherwise — fail-loud is correct. |

---

## Open Questions

None.

---

## Done Definition

- [ ] All Phase 1-6 tasks checked off.
- [ ] All AC1-AC6 covered by code + test evidence.
- [ ] `dotnet build` clean; new tests green.
- [ ] Manual smoke on app startup confirms idempotency.
- [ ] Story status flipped to Review by `code verify`.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Opus 4.7) | Initial plan drafted. |
