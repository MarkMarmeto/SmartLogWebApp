# US0061: Program Admin CRUD Pages

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to create, view, edit, and deactivate programs through the admin UI
**So that** I can manage my school's specific programs and sub-programs

## Context

### Background
Schools customize programs beyond the seeded defaults (e.g., "TVE 1", "BEC 1"). The admin UI provides a tree view of programs with full CRUD operations. Only admins can manage programs.

---

## Acceptance Criteria

### AC1: Program List (Tree View)
- **Given** I navigate to `/Admin/Programs`
- **Then** I see programs in a tree structure:
  ```
  REGULAR                    K, 1-12, NG    Active
  SPA (Special Program...)   7-10           Active
    ├── SPA-VA (Visual Arts)                Active
    ├── SPA-MUS (Music)                     Active
    └── ...
  STEM                       11-12          Active
  TVL-HE (Home Economics)    11-12          Active
    ├── TVL-HE-CK (Cookery)                Active
    └── TVL-HE-FBS (Food...)               Active
  ```
- **And** each row shows: Code, Name, Linked Grade Levels, Active/Inactive status, Edit button

### AC2: Create Program
- **Given** I click "Create Program"
- **Then** I see a form with:
  - Code (required, max 20 chars)
  - Name (required, max 100 chars)
  - Description (optional, max 500 chars)
  - Parent Program (optional dropdown — only programs without parents shown)
  - Linked Grade Levels (multi-select checkboxes: K, 1-12, NG)
  - Sort Order (number)
- **When** I fill in Code: "TVE-1", Name: "TVE 1", Parent: none, Grade Levels: 7, 8, 9, 10
- **And** click Save
- **Then** the program is created and I'm redirected to the program list

### AC3: Create Sub-Program
- **Given** I click "Create Program"
- **And** I select Parent Program: "SPA"
- **Then** Grade Levels checkboxes are disabled (inherited from parent)
- **And** the form shows "Grade levels inherited from parent: 7, 8, 9, 10"

### AC4: Edit Program
- **Given** I click Edit on program "SPA-VA"
- **Then** I can change Name, Description, Sort Order, and IsActive
- **But** I cannot change Code (read-only after creation)
- **And** I cannot change Parent Program if program has sections assigned

### AC5: Deactivate Program
- **Given** program "SPA-THTR" has no sections assigned
- **When** I toggle IsActive to false and Save
- **Then** the program is marked inactive
- **And** it no longer appears in section program dropdowns

### AC6: Cannot Delete REGULAR
- **Given** I view program "REGULAR"
- **Then** there is no Delete or Deactivate option (protected system program)

### AC7: Authorization
- **Given** I am logged in as Teacher Tina
- **When** I navigate to `/Admin/Programs`
- **Then** I receive 403 Forbidden (RequireAdmin policy)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Deactivate parent with active children | Warning: "This will hide sub-programs from dropdowns too" |
| Create program with existing code | Error: "Program code already exists" |
| Edit program that has enrolled students | Allow edit of name/description; warn about code immutability |
| No grade levels selected for standalone program | Error: "At least one grade level required" |
| Sort order 0 | Allowed (default) |
| Attempt to create child under another child (3 levels) | Error: "Sub-programs cannot have their own sub-programs" |

---

## Test Scenarios

- [ ] Program list loads with tree structure
- [ ] Create standalone program works
- [ ] Create sub-program inherits grade levels from parent
- [ ] Edit program updates name, description, sort order
- [ ] Code is read-only on edit
- [ ] Deactivate program works
- [ ] REGULAR cannot be deactivated
- [ ] Duplicate code rejected
- [ ] Authorization enforced (Admin/SuperAdmin only)
- [ ] Parent program dropdown shows only root programs

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program entity exists | Draft |
| [US0059](US0059-seed-k12-programs-nongraded.md) | Data | Programs seeded | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
