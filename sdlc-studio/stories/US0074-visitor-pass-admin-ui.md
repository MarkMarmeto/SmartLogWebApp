# US0074: Visitor Pass Admin Management

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to view, manage, and print visitor passes from the admin UI
**So that** I can maintain the pool of visitor QR cards and monitor their status

## Context

### Background
Admin page lists all visitor passes with live status, allows activation/deactivation, generation of new passes, and printing of QR cards for physical distribution.

---

## Acceptance Criteria

### AC1: Pass List Page
- **Given** I navigate to `/Admin/VisitorPasses`
- **Then** I see a table of all passes:
  - Pass # | Code | Status | Last Entry | Last Exit | Actions
  - Status badges: green "Available", yellow "In Use", red "Deactivated"

### AC2: Generate Passes Button
- **Given** current pass count is 15 and MaxPasses is 20
- **When** I click "Generate Passes"
- **Then** 5 new passes are created (VISITOR-016 to VISITOR-020)
- **And** the list refreshes to show all 20

### AC3: Configure Max Passes
- **Given** I click "Settings" on the passes page
- **Then** I see "Maximum Passes" input (current: 20)
- **When** I change to 25 and Save
- **Then** AppSettings `Visitor:MaxPasses` is updated
- **And** "Generate Passes" button now shows "(5 new passes available)"

### AC4: Deactivate Pass
- **Given** pass VISITOR-005 is Available
- **When** I click Deactivate
- **Then** pass status changes to Deactivated
- **And** scanning this pass returns REJECTED_PASS_INACTIVE

### AC5: Reactivate Pass
- **Given** pass VISITOR-005 is Deactivated
- **When** I click Activate
- **Then** pass status changes to Available

### AC6: Print QR Cards
- **Given** I select passes 1-10 (or "Print All Active")
- **When** I click "Print"
- **Then** a printable page opens with QR card layout:
  - Each card shows: QR code image, "VISITOR-001", "SmartLog Visitor Pass"
  - Cards arranged in grid for easy cutting
  - Card size suitable for lamination

### AC7: Authorization
- **Given** I am logged in as Teacher Tina
- **When** I navigate to `/Admin/VisitorPasses`
- **Then** I receive 403 Forbidden (RequireAdmin policy)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Deactivate InUse pass | Warning: "This pass is currently in use. Deactivate anyway?" |
| All passes in use | No passes show "Available"; highlight shortage |
| MaxPasses set to 0 | Error: "Minimum 1 pass required" |
| Print with no active passes | Warning: "No active passes to print" |
| MaxPasses reduced below current active count | Excess passes deactivated (highest numbers first) |

---

## Test Scenarios

- [ ] Pass list displays with correct status badges
- [ ] Generate creates only missing passes
- [ ] Max passes configuration saves
- [ ] Deactivate changes pass status
- [ ] Reactivate changes pass status
- [ ] Print generates printable QR cards
- [ ] Authorization enforced
- [ ] InUse pass deactivation shows warning

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0072](US0072-visitor-pass-entity-generation.md) | Schema | VisitorPass entity | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
