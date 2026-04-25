# US0059: Seed K-12 Programs & Non-Graded Level

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** SuperAdmin Tony (IT Administrator)
**I want** the system to come pre-loaded with Philippine K-12 programs, sub-programs, and a Non-Graded level
**So that** schools can start using programs immediately without manual setup

## Context

### Background
Philippine K-12 includes standard programs/strands. The system seeds these as defaults while allowing schools to add custom programs. A "Non-Graded" grade level with sections Level 1-4 supports SPED/ALS learners.

---

## Acceptance Criteria

### AC1: Seed Missing Grade Levels
- **Given** a fresh or existing database (currently only grades 7-12 seeded)
- **When** DbInitializer runs
- **Then** these grade levels are seeded (if not already present):
  - K (Kindergarten), SortOrder: 0
  - 1-6 (Elementary), SortOrder: 1-6
  - 7-12 (already exist)
  - NG (Non-Graded), SortOrder: 99

### AC2: Seed REGULAR Program
- **Given** DbInitializer runs
- **Then** program `REGULAR` ("Regular Program") is created
- **And** linked to ALL grade levels (K, 1-12, NG) via GradeLevelProgram

### AC3: Seed JHS Programs (Grades 7-10)
- **Given** DbInitializer runs
- **Then** these programs are created and linked to grades 7-10:
  - SPA (with children: SPA-VA, SPA-MUS, SPA-DNC, SPA-THTR)
  - SPE, SPJ, STE (standalone)

### AC4: Seed SHS Programs (Grades 11-12)
- **Given** DbInitializer runs
- **Then** these programs are created and linked to grades 11-12:
  - STEM, ABM, HUMSS, GAS, SPORTS, ADT (standalone)
  - TVL-HE (with children: TVL-HE-CK, TVL-HE-FBS)
  - TVL-ICT (with children: TVL-ICT-CP, TVL-ICT-CSS)
  - TVL-IA (with children: TVL-IA-EIM, TVL-IA-SMAW)
  - TVL-AFA (standalone)

### AC5: Seed Non-Graded Sections
- **Given** the NG grade level is seeded
- **Then** 4 sections are created under NG:
  - Level 1, Level 2, Level 3, Level 4
  - Each assigned to REGULAR program

### AC6: Idempotent Seeding
- **Given** programs and grade levels already exist
- **When** DbInitializer runs again
- **Then** no duplicates are created
- **And** existing records are not modified

### AC7: Grade Level Programs Linked Correctly
- **Given** seeding is complete
- **Then** GradeLevelProgram junction records exist for:
  - REGULAR → K, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, NG
  - SPA, SPE, SPJ, STE → 7, 8, 9, 10
  - STEM, ABM, HUMSS, GAS, SPORTS, ADT → 11, 12
  - TVL-HE, TVL-ICT, TVL-IA, TVL-AFA → 11, 12

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Existing grade levels 7-12 have data | Leave untouched, only add missing levels |
| Program code collision with custom program | Skip seeding that program, log warning |
| Database already has some programs | Only seed missing ones |
| Empty GradeLevel table | Seed all grade levels first, then programs |
| Sub-program's parent not yet created | Seed parents first (ordered insertion) |

---

## Test Scenarios

- [ ] All grade levels seeded (K, 1-12, NG)
- [ ] NG grade level has SortOrder 99
- [ ] REGULAR program linked to all grade levels
- [ ] JHS programs linked to grades 7-10 only
- [ ] SHS programs linked to grades 11-12 only
- [ ] Sub-programs have correct ParentProgramId
- [ ] Non-Graded sections Level 1-4 created
- [ ] Non-Graded sections assigned to REGULAR
- [ ] Re-running seeder creates no duplicates
- [ ] Seeder handles existing partial data gracefully

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program and GradeLevelProgram entities | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
