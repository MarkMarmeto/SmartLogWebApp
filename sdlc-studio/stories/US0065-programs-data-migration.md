# US0065: Programs Data Migration

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** SuperAdmin Tony (IT Administrator)
**I want** existing sections to be automatically assigned to the REGULAR program during migration
**So that** the NOT NULL constraint on Section.ProgramId can be enforced without breaking existing data

## Context

### Background
Existing sections have ProgramId = NULL. This migration assigns all unlinked sections to the REGULAR program for their grade level, then enforces the NOT NULL constraint. This is a multi-step migration to avoid data loss.

---

## Acceptance Criteria

### AC1: Migration Step 1 — Add Nullable Column
- **Given** Section table has no ProgramId column
- **When** migration runs
- **Then** `ProgramId` (Guid, nullable) is added to Section table

### AC2: Migration Step 2 — Assign REGULAR
- **Given** sections exist with `ProgramId = NULL`
- **And** the REGULAR program is seeded
- **When** the data migration script runs
- **Then** all sections with `ProgramId = NULL` are updated to the REGULAR program's Id

### AC3: Migration Step 3 — Enforce NOT NULL
- **Given** all sections have a ProgramId value
- **When** the schema migration runs
- **Then** `ProgramId` is altered to NOT NULL with FK constraint

### AC4: Existing Data Preserved
- **Given** sections "Ruby", "Amethyst", "Diamond" exist for Grade 7
- **When** migration completes
- **Then** all three sections retain their names, grade levels, advisers, and capacities
- **And** each has `ProgramId` = REGULAR program Id

### AC5: Student.Program Backfilled
- **Given** existing students enrolled in sections now assigned to REGULAR
- **When** the migration runs
- **Then** `Student.Program` is set to "REGULAR" for all enrolled students

### AC6: Rollback Safety
- **Given** the migration fails at step 3
- **Then** step 1 and 2 changes can be rolled back
- **And** no data is lost

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| REGULAR program not yet seeded | Migration depends on seeder running first; fail with clear error |
| Section with manually set ProgramId | Left unchanged |
| Database with 0 sections | Migration completes without error |
| Large number of sections (1000+) | Batch UPDATE for performance |
| Concurrent section creation during migration | New sections must include ProgramId |
| Student enrolled in multiple sections across years | Only current enrollment determines Student.Program |

---

## Test Scenarios

- [ ] ProgramId column added as nullable first
- [ ] All NULL ProgramId sections updated to REGULAR
- [ ] NOT NULL constraint enforced after data migration
- [ ] Existing section data preserved (name, grade, adviser, capacity)
- [ ] Student.Program backfilled to REGULAR
- [ ] Migration is idempotent (safe to run twice)
- [ ] Migration fails cleanly if REGULAR not seeded

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program entity | Draft |
| [US0059](US0059-seed-k12-programs-nongraded.md) | Data | REGULAR program seeded | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
