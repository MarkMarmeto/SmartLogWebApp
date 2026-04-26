# US0106: Student.Program Denormalisation — Null for Non-Graded Enrollments

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0029: Student.Program Denormalisation — Null for Non-Graded Enrollments](../plans/PL0029-student-program-null-for-ng.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26

## User Story

**As a** Tech-Savvy Tony (System Admin)
**I want** `Student.Program` to be `NULL` whenever a student is enrolled in a Non-Graded section
**So that** downstream features (broadcasts, reports, card printing) can detect NG students reliably without checking via the Section join.

## Context

### Persona Reference
**Tech-Savvy Tony** — keeps denormalised fields consistent.

### Background
`Student.Program` is a denormalised string set on enrollment from the section's leaf Program code (US0064). With NG sections having no Program (US0103+US0105), enrollment must set `Student.Program = NULL` for those students. The promote/transfer flows must respect this in both directions: graded → NG (clear), NG → graded (set from new section).

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0103 | Data | Section.ProgramId nullable | Enrollment lookup must handle null Section.Program |
| US0064 | Data | Student.Program is denormalised on enrollment | Logic now branches on NG vs graded |
| EP0010 | Data | Student.Program is the fast-read field for lists/exports | Must be authoritative for NG = null |

---

## Acceptance Criteria

### AC1: New Enrollment to NG Section
- **Given** I create a new Student or move an existing one into an NG section
- **When** enrollment saves
- **Then** `Student.Program` is `NULL`

### AC2: New Enrollment to Graded Section
- **Given** I enroll a student into a graded section (Program = STEM, e.g.)
- **When** enrollment saves
- **Then** `Student.Program = "STEM"` (unchanged behaviour)

### AC3: Move from Graded to NG
- **Given** an existing student with `Student.Program = "STEM"`
- **When** they are re-enrolled to an NG section
- **Then** `Student.Program` is updated to `NULL`

### AC4: Move from NG to Graded
- **Given** an existing student with `Student.Program = NULL` in an NG section
- **When** they are re-enrolled to a Graded section (Program = ABM, e.g.)
- **Then** `Student.Program` is updated to `"ABM"`

### AC5: Bulk Re-Enrollment Flow Honors NG
- **Given** the annual batch re-enrollment flow processes a student into an NG section
- **Then** `Student.Program` is set to `NULL` for that student
- **And** when processing graded students, the existing logic is unaffected

### AC6: Student Entity Field Already Nullable
- **Given** the `Student` entity
- **Then** the `Program` field is verified as nullable string (`string?`) — adjust if not

---

## Scope

### In Scope
- Enrollment service logic (single + bulk paths)
- `Student.Program` field nullability check / change
- Promote/transfer routines

### Out of Scope
- Display logic for NG students (US0109)
- Filtering logic in attendance/reports (US0108)
- Broadcast targeting (US0107)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student is mid-deactivation when re-enrolled | Standard rules apply: Student.Program reflects new section |
| Section's Program is changed (graded only — not NG) after student is enrolled | Out of scope; covered by existing US0064 reconciliation |
| Bulk import row specifies a graded grade with no Program | Validation error per existing rules — does not affect NG path |

---

## Test Scenarios

- [ ] Enroll new student to NG → `Student.Program = NULL`
- [ ] Enroll new student to Grade 7 STEM → `Student.Program = "STEM"`
- [ ] Promote student STEM → NG → field nulled
- [ ] Promote student NG → ABM → field set to "ABM"
- [ ] Batch re-enroll mix of NG and graded students processes both paths correctly

---

## Technical Notes

### Files to Modify
- **Likely:** `src/SmartLog.Web/Services/StudentEnrollmentService.cs` (or whichever service handles enrollment writes)
- **Possibly:** `src/SmartLog.Web/Pages/Admin/CreateStudent.cshtml.cs` and `EditStudent.cshtml.cs` if they call into enrollment directly
- **Verify:** `src/SmartLog.Web/Data/Entities/Student.cs` — `Program` is `string?`

### Logic
```csharp
student.Program = section.Program?.Code; // null when section has no Program
```

The single-line nullable-propagation handles both new and update paths.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0103](US0103-section-programid-nullable.md) | Data | Section.Program nullable | Draft |
| [US0064](US0064-student-program-denormalization.md) | Predecessor | Existing denormalisation logic | Done |
| [US0105](US0105-seed-ng-gradelevel-and-sections.md) | Data | NG sections exist | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low — small logic adjustment in enrollment service.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
