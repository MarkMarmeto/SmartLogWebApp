# US0107: Broadcast Targeting — Add Non-Graded Branch Alongside Programs

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0031: Broadcast Targeting — Add Non-Graded Branch Alongside Programs](../plans/PL0031-broadcast-targeting-ng-branch.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26

## User Story

**As a** Admin Amy (Administrator)
**I want** the broadcast targeting UI to expose Non-Graded as a sibling branch with selectable LEVEL 1–4 sections
**So that** I can include or exclude NG learners in announcements, emergencies, and bulk sends without abusing the Program filter.

## Context

### Persona Reference
**Admin Amy** — composes announcements that may target the whole school or specific cohorts including NG.

### Background
US0084 introduced Program-first targeting: admin picks Programs, then Grade Levels nested under each Program. NG has no Program, so it cannot live in that tree. This story adds a sibling **Non-Graded** group at the same level as Programs in the targeting picker. Selecting it expands to checkboxes for LEVEL 1, LEVEL 2, LEVEL 3, LEVEL 4 (the seeded NG sections).

The backend filter shape extends to carry NG selections; the resolver unions Program-targeted students with NG-targeted students.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0084 | UI | Existing Program-first targeting partial | NG is added as a new top-level group, not as a Program |
| US0103 | Data | NG sections have no ProgramId | Resolver path for NG queries Section directly |
| EP0009 | Correctness | Broadcast target set must match selection | Counts and queue inserts include NG when selected |
| Stakeholder | UX | NG is a peer to Programs in the picker (decision 2026-04-26) | Don't bury NG under "Other" — give it equal hierarchy weight |

---

## Acceptance Criteria

### AC1: Non-Graded Group Appears in Targeting UI
- **Given** I open Announcement / Emergency / BulkSend composer
- **Then** the targeting panel shows a "Non-Graded" group as a top-level checkbox at the same hierarchy level as Programs
- **And** it is positioned after the last Program (sort order: SortOrder of NG GradeLevel = 99 keeps it last)

### AC2: Expanding Non-Graded Reveals Sections
- **Given** the Non-Graded group is collapsed
- **When** I tick or expand it
- **Then** it shows nested checkboxes for each active NG section (LEVEL 1, LEVEL 2, LEVEL 3, LEVEL 4)
- **And** ticking the parent selects all NG sections by default

### AC3: Granular NG Section Selection
- **Given** I tick "Non-Graded" with only LEVEL 2 and LEVEL 4 ticked underneath
- **Then** the resolved target includes only students enrolled in those two NG sections

### AC4: Combined with Program Selections
- **Given** I tick "STEM" with Grade 11 and "Non-Graded" with LEVEL 1
- **Then** the resolved target is the union: Grade 11 STEM students + NG LEVEL 1 students

### AC5: "All Programs / All Grades" Shortcut Includes NG
- **Given** the existing top-of-list "All" shortcut from US0084
- **When** I tick it
- **Then** the Non-Graded group and all its sections are also selected
- **And** the shortcut label is updated to "All Students" (or remains "All Programs / All Grades" with a clarifying tooltip — pick the simpler label)

### AC6: Filter Payload Carries NG Selection
- **When** I submit the broadcast form
- **Then** the posted filter includes a separate field for NG, e.g.:
  ```json
  {
    "programGradeFilters": [{ "programId": "...", "gradeLevelIds": ["..."] }],
    "nonGradedSectionIds": ["...", "..."]
  }
  ```
- **And** the backend resolver unions NG-section students into the final target list

### AC7: Preview Count Reflects NG
- **Given** I include NG in the selection
- **Then** the "Sending to N students" preview includes NG students in the count

### AC8: Empty-Selection Validation
- **Given** I submit with neither any Program+Grade nor any NG section selected
- **Then** validation error: "Select at least one target group"

---

## Scope

### In Scope
- Update existing targeting partial: `_ProgramFirstTargeting.cshtml(.cs)` (or whatever filename US0084 produced)
- New repository method to fetch active NG sections
- Backend filter DTO + resolver updates to accept and resolve `nonGradedSectionIds`
- Preview-count endpoint adjustment

### Out of Scope
- Targeting individual graded sections (still grade-level granularity for graded)
- Saving targeting presets
- Per-NG-student selection

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No NG sections exist (admin disabled all four) | Non-Graded group still shown but disabled with tooltip "No active Non-Graded sections" |
| Admin un-ticks all NG sections under a ticked NG parent | Parent moves to indeterminate; resolver excludes NG |
| Backend receives a `nonGradedSectionIds` value referencing a graded section | Resolver rejects with 400 — section must belong to NG GradeLevel |
| Student with no SmsEnabled, in NG | Filtered out by existing SMS-eligibility rules (unchanged) |

---

## Test Scenarios

- [ ] NG group renders as sibling to Programs
- [ ] NG group lists LEVEL 1–4 (active only)
- [ ] Selecting NG-only sends to NG students only
- [ ] Combining STEM Grade 11 + NG LEVEL 1 unions correctly
- [ ] "All" shortcut includes NG
- [ ] Submitting with no selections shows validation error
- [ ] Preview count matches resolved target including NG
- [ ] Posting `nonGradedSectionIds` containing a non-NG section ID is rejected

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/_ProgramFirstTargeting.cshtml(.cs)` — render NG group
- **Modify:** `src/SmartLog.Web/wwwroot/js/sms-broadcast-targeting.js` — track NG selection state
- **Modify:** `src/SmartLog.Web/Services/Sms/BroadcastTargetResolver.cs` — accept and resolve NG sections
- **Modify:** `src/SmartLog.Web/Services/GradeLevelProgramRepository.cs` (or new helper) — `GetActiveNonGradedSections()`
- **Modify:** filter DTO (e.g. `BroadcastFilterDto`) — add `NonGradedSectionIds` list

### Resolver Logic
```csharp
var graded = students.Where(s => programGradeFilters covers s);
var ng = students.Where(s => s.SectionId in nonGradedSectionIds);
return graded.Union(ng).Distinct();
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0084](US0084-broadcast-program-first-targeting.md) | Predecessor | Program-first targeting UI exists | Done |
| [US0105](US0105-seed-ng-gradelevel-and-sections.md) | Data | NG sections exist | Draft |
| [US0106](US0106-student-program-null-for-ng.md) | Data | Student.Program null for NG (used in fast-path queries if any) | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium — UI surface + backend filter shape + resolver branch.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
| 2026-04-26 | Claude | Implementation complete. AC8 deviation confirmed: empty filter list = "all students" (existing US0084 UX) preserved. 5 resolver tests added to ProgramFirstTargetingTests.cs; 202/202 pass. |
