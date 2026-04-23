# US0058: Program Entity & Self-Referencing Hierarchy

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** a Program entity with parent-child hierarchy support
**So that** school programs and sub-programs can be organized and managed in the system

## Context

### Background
Philippine K-12 schools organize students by programs/strands (STEM, ABM, TVL specializations, SPA sub-programs). Programs can have sub-programs (e.g., TVL-HE → Cookery, Food & Beverage). Max depth is 2 levels (parent → child). Only leaf programs (no children) can be assigned to sections.

---

## Acceptance Criteria

### AC1: Program Entity Created
- **Given** the database schema
- **Then** a `Program` table exists with columns:
  - `Id` (Guid, PK)
  - `ParentProgramId` (Guid?, FK → Program, nullable)
  - `Code` (string, max 20, required, unique)
  - `Name` (string, max 100, required)
  - `Description` (string, max 500, nullable)
  - `IsActive` (bool, default: true)
  - `SortOrder` (int, default: 0)
  - `CreatedAt` (DateTime)

### AC2: Self-Referencing Navigation
- **Given** the Program entity
- **Then** EF Core navigation properties exist:
  - `ParentProgram` (Program?, inverse of ParentProgramId)
  - `SubPrograms` (ICollection\<Program\>)

### AC3: GradeLevelProgram Junction
- **Given** the database schema
- **Then** a `GradeLevelProgram` junction table exists with:
  - `GradeLevelId` (Guid, FK → GradeLevel, composite PK)
  - `ProgramId` (Guid, FK → Program, composite PK)

### AC4: Max Depth Validation
- **Given** a program "TVL-HE" has ParentProgramId pointing to "TVL"
- **When** I attempt to create a child program under "TVL-HE" (grandchild of "TVL")
- **Then** validation rejects with "Programs cannot be nested more than 2 levels deep"

### AC5: Leaf Program Validation
- **Given** program "TVL" has children (TVL-HE, TVL-ICT)
- **When** I attempt to assign "TVL" directly to a section
- **Then** validation rejects with "Only leaf programs (without sub-programs) can be assigned to sections"

### AC6: Unique Code Constraint
- **Given** program "STEM" exists
- **When** I attempt to create another program with code "STEM"
- **Then** validation rejects with "Program code must be unique"

### AC7: ApplicationDbContext Configuration
- **Given** the DbContext
- **Then** entity configurations exist for:
  - Program (with self-referencing FK, cascade delete restricted)
  - GradeLevelProgram (composite PK, cascading deletes)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Delete parent program with children | Reject: "Cannot delete program with sub-programs" |
| Delete program assigned to sections | Reject: "Cannot delete program assigned to sections" |
| Deactivate parent program | Children remain active (soft delete is independent) |
| Circular reference (A → B → A) | EF Core FK constraint prevents; validated at service level |
| Code with special characters | Allow alphanumeric, hyphen, underscore only |
| Empty code | Validation error |

---

## Test Scenarios

- [ ] Program entity creates in database
- [ ] Self-referencing FK works (parent-child)
- [ ] GradeLevelProgram junction creates
- [ ] Max depth 2 enforced
- [ ] Leaf program validation works
- [ ] Unique code constraint enforced
- [ ] Delete parent with children rejected
- [ ] Delete program with sections rejected
- [ ] Navigation properties load correctly (Include)

---

## Dependencies

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| EF Core migration | Technical | Required |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
