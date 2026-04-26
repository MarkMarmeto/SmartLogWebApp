# US0110: Remove Enrollment Sticker Feature

> **Status:** Done
> **Marked Done:** 2026-04-26
> **Plan:** [PL0032: Remove Enrollment Sticker Feature](../plans/PL0032-remove-enrollment-sticker.md)
> **Epic:** [EP0013: QR Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-26

## User Story

**As a** Product Owner
**I want** the enrollment sticker feature removed from the application
**So that** we eliminate a feature no longer needed, reduce dead code, and simplify the student actions UI.

## Context

### Background

The enrollment sticker (US0078) was a print page (`PrintEnrollmentSticker.cshtml`) that generated a label showing S.Y., Grade, Program, and Section — intended to be printed on the back of the student ID card each academic year. The feature has been superseded; it is unused in production and adds maintenance surface. This story removes it entirely.

The feature consists of:
- **Page:** `src/SmartLog.Web/Pages/Admin/PrintEnrollmentSticker.cshtml` (print layout, no shared layout)
- **Handler:** `src/SmartLog.Web/Pages/Admin/PrintEnrollmentSticker.cshtml.cs` — three GET handlers (`?handler=Student`, `?handler=Section`, `?handler=Grade`)
- **Entry point:** "Print Enrollment Sticker" button in `StudentDetails.cshtml` (lines 157–161)

No other pages link to this route. No tests exist for it.

---

## Acceptance Criteria

### AC1: Sticker Page Deleted
- **Given** the application is built
- **Then** `PrintEnrollmentSticker.cshtml` and `PrintEnrollmentSticker.cshtml.cs` no longer exist in the repository

### AC2: StudentDetails Button Removed
- **Given** I open any student's details page
- **Then** the "Print Enrollment Sticker" button is absent
- **And** the "Print ID Card" button and other actions remain intact

### AC3: Route Returns 404
- **Given** the page has been deleted
- **When** a user navigates to `/Admin/PrintEnrollmentSticker?handler=Student&id=...`
- **Then** the application returns 404 (framework default — no code change required)

### AC4: Build Clean
- **Given** the files are deleted
- **Then** `dotnet build` produces zero errors and zero warnings

---

## Scope

### In Scope
- Delete `PrintEnrollmentSticker.cshtml` + `PrintEnrollmentSticker.cshtml.cs`
- Remove the sticker button block from `StudentDetails.cshtml`
- Amend US0109 scope to remove "enrollment sticker" references

### Out of Scope
- Removing the `StickerEntry` record if it is inlined in the deleted `.cshtml.cs` (it is — deleted with the file)
- Any database migration (no DB schema involved)
- Removing sticker from US0078 retroactively (US0078 stays as historical record)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| A bookmarked sticker URL is visited | 404 — no redirect needed |
| Section / Grade bulk-print URLs visited | 404 — no external surfaces known |

---

## Test Scenarios

- [ ] `dotnet build` clean after deletion
- [ ] StudentDetails page renders without sticker button (manual or snapshot test)
- [ ] No other pages reference `PrintEnrollmentSticker` (grep check in CI)

---

## Technical Notes

### Files to Delete
- `src/SmartLog.Web/Pages/Admin/PrintEnrollmentSticker.cshtml`
- `src/SmartLog.Web/Pages/Admin/PrintEnrollmentSticker.cshtml.cs`

### Files to Modify
- `src/SmartLog.Web/Pages/Admin/StudentDetails.cshtml` — remove lines 157–161 (the `<a>` button for sticker)

### Files to Amend (SDLC)
- `sdlc-studio/stories/US0109-student-details-card-ng-display.md` — remove "enrollment sticker" from User Story sentence and scope

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| None | — | No blocking dependencies | — |

---

## Estimation

**Story Points:** 1
**Complexity:** Trivial — pure deletion, one button removal.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft — remove enrollment sticker feature |
