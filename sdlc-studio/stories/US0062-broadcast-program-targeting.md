# US0062: Broadcast Program Targeting

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to target broadcasts by program (in addition to grade level)
**So that** I can send announcements to specific program groups (e.g., all TVL students)

## Context

### Background
Broadcasts currently target by grade level only. Adding program targeting enables more precise communication. Selecting a parent program (e.g., TVL-HE) includes all students in child programs (TVL-HE-CK, TVL-HE-FBS).

---

## Acceptance Criteria

### AC1: Broadcast Entity Extended
- **Given** the Broadcast entity in the database schema
- **When** the migration is applied
- **Then** a new field `AffectedPrograms` (string?, nullable) exists
- **And** it stores a JSON array of program codes (e.g., `["STEM","ABM"]`)
- **And** null means "all programs" (no filter)

### AC2: Program Selection UI
- **Given** I am creating an Announcement broadcast
- **And** I select grade levels "11, 12"
- **Then** a "Programs" section appears showing a program tree:
  - [ ] All Programs (default checked)
  - [ ] STEM
  - [ ] ABM
  - [ ] HUMSS
  - [ ] GAS
  - [ ] TVL-HE
    - [ ] TVL-HE-CK (Cookery)
    - [ ] TVL-HE-FBS (Food & Beverage)
  - [ ] ...

### AC3: Parent Program Includes Children
- **Given** I check "TVL-HE" (parent)
- **Then** all children (TVL-HE-CK, TVL-HE-FBS) are automatically checked
- **And** unchecking a child unchecks the parent (partial selection)

### AC4: Recipient Calculation
- **Given** I target grades 11-12 and programs STEM, ABM
- **When** recipients are calculated
- **Then** only students enrolled in sections with Program = STEM or ABM in grades 11-12 are included

### AC5: Program-Based Recipient for No-Scan Alert
- **Given** the no-scan alert runs
- **Then** it does NOT filter by program (alerts all students with no scans regardless of program)

### AC6: Migration
- **Given** existing Broadcast records
- **When** the migration runs
- **Then** `AffectedPrograms` is added as nullable column
- **And** existing records have `AffectedPrograms = null` (all programs)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No grade selected | Program section hidden |
| Grade selected but no programs for that grade | Show only REGULAR |
| "All Programs" checked | AffectedPrograms stored as null |
| Selected programs yield 0 recipients | Warning: "No students match this criteria" |
| Program deactivated after broadcast created | Broadcast still references code; delivery unaffected |

---

## Test Scenarios

- [ ] AffectedPrograms field exists on Broadcast entity
- [ ] Program tree appears after grade selection
- [ ] Tree filtered to programs for selected grades
- [ ] Parent check auto-checks children
- [ ] Recipient calculation respects program filter
- [ ] "All Programs" stores null
- [ ] No-scan alert ignores program filter
- [ ] Migration adds nullable column

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program entity exists | Draft |
| [US0060](US0060-section-program-mandatory.md) | Data | Sections have ProgramId | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
