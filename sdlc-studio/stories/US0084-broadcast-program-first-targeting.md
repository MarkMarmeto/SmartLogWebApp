# US0084: Broadcast Targeting — Program-First with Nested Grade Levels

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Admin Amy (Administrator)
**I want** broadcast targeting to start from Programs (with nested Grade Levels per Program) instead of Grade Levels alone
**So that** I can easily send a single announcement to program-specific cohorts like "Grade 7 BEC only", "Grade 8 SPA only", or "Grade 9 TVE and BEC" without building complex filters.

## Context

### Persona Reference
**Admin Amy** — Administrative Assistant who composes school-wide and cohort-specific announcements.

### Background
Current broadcast targeting (US0062) exposes Programs as a secondary filter after a Grade Level selection. With the flat Program model (EP0010) and the existing `GradeLevelProgram` junction already capturing which programs exist at which grade, we can flip the mental model: admin picks Programs first, then ticks the Grade Levels (within each Program's allowed set) they want to target.

This story does not change the target-resolution logic server-side (Program × GradeLevel → Students) — it restructures the UI so admins compose the filter the way they think about it.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0010 | Data | `GradeLevelProgram` junction defines which grades are valid for each Program | Nested Grade Level checkboxes must be driven by this junction |
| EP0009 | Correctness | Target set must match current behaviour given equivalent selection | Backend target resolution unchanged; only UI composition differs |
| PRD | UX | Admin Amy sometimes sends to a single program strand across multiple grades | Program-first UI reduces clicks for this case |

---

## Acceptance Criteria

### AC1: Program-First Targeting UI
- **Given** I am on any broadcast composer page (Announcement, Emergency, BulkSend)
- **Then** the targeting panel shows a list of active Programs as top-level checkboxes (REGULAR, SPA, STEM, ABM, HUMSS, TVL, SPORTS, BEC, TVE, etc.)
- **And** each Program row is expandable and shows its allowed Grade Levels (from `GradeLevelProgram`) as nested checkboxes

### AC2: Selecting a Program Auto-Expands Its Grades
- **Given** the Programs list is collapsed
- **When** I tick a Program checkbox
- **Then** its nested Grade Level list expands
- **And** all nested Grade Level checkboxes are selected by default (targeting all grades in that Program)

### AC3: Granular Grade Selection Within a Program
- **Given** I have ticked Program "BEC"
- **When** I un-tick Grade 9 under BEC but keep Grade 7 and 8 ticked
- **Then** the resulting target includes only Grade 7 BEC students and Grade 8 BEC students — not Grade 9 BEC

### AC4: Multiple Programs with Different Grade Sets
- **Given** I tick "BEC" with Grade 7 only, and "SPA" with Grade 8 only
- **Then** the target includes Grade 7 BEC students + Grade 8 SPA students (union of per-Program selections)

### AC5: "All Programs" Shortcut
- **Given** the Programs list
- **Then** a top-of-list "All Programs / All Grades" checkbox is available
- **When** I tick it
- **Then** every Program and every nested Grade Level is selected (equivalent to school-wide broadcast)

### AC6: Preview Count Reflects Selection
- **Given** I have made a Program+Grade selection
- **Then** the preview label "Sending to N students" updates to reflect the resolved target set
- **And** the count matches what the backend target-resolver would return for the same filter

### AC7: Filter Payload Posted to Backend
- **When** I submit the broadcast form
- **Then** the form posts a filter structure shaped like `{ programId, gradeLevelIds[] }[]` (one entry per selected Program, with its selected grades)
- **And** the existing target-resolver converts this into the student list for queueing

---

## Scope

### In Scope
- Targeting component shared across Announcement, Emergency, BulkSend composer pages
- Driven by `GradeLevelProgram` junction (already seeded in EP0010)
- Replaces the existing Grade Level → Program filter UI
- Updated preview / count endpoint call

### Out of Scope
- Saving broadcast targeting presets / favourites
- Per-section targeting (below grade-level granularity) — future story if needed
- Student-ID-list upload targeting (BulkSend already supports this separately)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Program has no grades in `GradeLevelProgram` junction | Program row shown but non-expandable with a tooltip "No grades configured for this program"; cannot be selected |
| Admin un-ticks all grades under a ticked Program | Program checkbox moves to an indeterminate state; target count re-computes to exclude that Program |
| Program marked inactive (IsActive=false) | Not shown in the list |
| No Programs selected on submit | Form validation error: "Select at least one Program + Grade Level" |

---

## Test Scenarios

- [ ] Ticking a Program defaults-selects all its configured grades
- [ ] Nested grade checkboxes respect `GradeLevelProgram` junction
- [ ] Selection shape `[{programId, gradeLevelIds[]}, ...]` posts correctly
- [ ] Backend resolves the posted filter to the same student list as the prior Grade→Program filter for equivalent inputs
- [ ] Preview count matches resolved student count
- [ ] "All Programs / All Grades" shortcut selects everything
- [ ] Inactive Programs are not shown
- [ ] Programs with no junction rows render disabled

---

## Technical Notes

### Backend
- Target resolver should accept the new filter shape: `List<ProgramGradeFilter> { ProgramId, GradeLevelIds }`
- Resolve each entry: `Students WHERE Program = X AND GradeLevel IN (Y1, Y2, ...)` then UNION results
- Preserve backward-compat: the resolver can accept the existing shape for any caller not yet migrated

### Frontend
- New Razor partial: `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml`
- Data source for nested render: `GradeLevelProgramRepository.GetActiveProgramsWithGrades()`
- JS component to track checkbox state, tri-state indeterminate on Program row, and post the nested structure

### Files to Modify
- **New:** `src/SmartLog.Web/Pages/Admin/Sms/_ProgramFirstTargeting.cshtml(.cs)`
- **New:** `src/SmartLog.Web/wwwroot/js/sms-broadcast-targeting.js`
- **Modify:** `Pages/Admin/Sms/Announcement.cshtml(.cs)` — replace existing targeting section with new partial
- **Modify:** `Pages/Admin/Sms/Emergency.cshtml(.cs)` — same
- **Modify:** `Pages/Admin/Sms/BulkSend.cshtml(.cs)` — same
- **Modify:** `Services/Sms/BroadcastTargetResolver.cs` (or equivalent) — accept new filter shape
- **Modify:** `Services/GradeLevelProgramRepository.cs` — query helper for active programs with nested grades

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Functional | Program entity exists | Done |
| [US0062](US0062-broadcast-program-targeting.md) | Predecessor | Current Grade→Program filter (being replaced) | Done |
| [US0065](US0065-programs-data-migration.md) | Data | `GradeLevelProgram` junction seeded | Done |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium — new UI component + backend filter-shape support + three composer pages to update

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 planning session |
