# US0051: Audit Log Search and Filter

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** to search and filter audit logs
**So that** I can quickly find specific events during investigations

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Administrator who investigates security incidents.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

---

## Acceptance Criteria

### AC1: Filter by Date Range
- **Given** I am on the Audit Logs page
- **When** I select date range "Feb 1 - Feb 4, 2026"
- **Then** I see only logs within that range
- **And** quick options: Today, Yesterday, Last 7 Days, Last 30 Days, Custom

### AC2: Filter by User
- **Given** I am on the Audit Logs page
- **When** I select user "Admin Amy" from the dropdown
- **Then** I see only logs of actions performed by Admin Amy

### AC3: Filter by Action Type
- **Given** I am on the Audit Logs page
- **When** I select action type "Login" from the dropdown
- **Then** I see only login-related entries

### AC4: Filter by Entity Type
- **Given** I am on the Audit Logs page
- **When** I select entity type "Student"
- **Then** I see only logs related to student records

### AC5: Search by Entity ID
- **Given** I want to find all actions on a specific student
- **When** I enter "STU-2026-001" in the search box
- **Then** I see all audit entries where Entity ID matches

### AC6: Search by IP Address
- **Given** I suspect unauthorized access from an IP
- **When** I enter "192.168.1.100" in the search box
- **Then** I see all audit entries from that IP address

### AC7: Combined Filters
- **Given** I set:
  - User: "Admin Amy"
  - Action: "Updated"
  - Entity: "Student"
  - Date: "Last 7 Days"
- **Then** I see only matching entries (AND logic)

### AC8: Save Filter Preset
- **Given** I have configured a complex filter
- **When** I click "Save Filter"
- **Then** I can name and save the preset
- **And** load it later from "Saved Filters" dropdown

### AC9: Export Filtered Results
- **Given** I have filtered audit logs
- **When** I click "Export"
- **Then** only filtered results are exported
- **And** available in CSV format

### AC10: Clear All Filters
- **Given** I have multiple filters applied
- **When** I click "Clear All"
- **Then** all filters reset to default
- **And** full log list is shown

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No matching results | Show "No logs match your filters" |
| Invalid date range | Show error "End date must be after start date" |
| Search term too short | Require minimum 3 characters |
| Very broad filter (millions of results) | Require date range, warn about performance |
| Deleted user in filter | Show "(deleted)" next to name |
| Special characters in search | Escape safely |

---

## Test Scenarios

- [ ] Date range filter works
- [ ] Quick date options work (Today, etc.)
- [ ] User filter works
- [ ] Action type filter works
- [ ] Entity type filter works
- [ ] Search by entity ID works
- [ ] Search by IP address works
- [ ] Combined filters work (AND logic)
- [ ] Save filter preset works
- [ ] Load saved preset works
- [ ] Export filtered results works
- [ ] Clear all filters works
- [ ] Empty results show message
- [ ] Performance acceptable with large datasets

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0050](US0050-audit-log-viewer.md) | Functional | Audit log viewer | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
