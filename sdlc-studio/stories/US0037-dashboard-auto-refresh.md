# US0037: Dashboard Auto-Refresh

> **Status:** Done
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** the attendance dashboard to auto-refresh
**So that** I see updated attendance without manually refreshing the page

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who monitors attendance on a display screen.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Auto-Refresh Every 30 Seconds
- **Given** I am viewing the attendance dashboard
- **Then** the data refreshes automatically every 30 seconds
- **And** the "Last updated" timestamp updates

### AC2: Visual Refresh Indicator
- **Given** the dashboard is refreshing
- **Then** I see a subtle loading indicator (spinner or pulse)
- **And** the indicator does not block the view
- **And** it disappears when refresh completes

### AC3: Preserve Scroll Position
- **Given** I have scrolled down the student list
- **When** auto-refresh occurs
- **Then** my scroll position is preserved
- **And** I don't lose my place

### AC4: Preserve Filters
- **Given** I have filters applied (Grade 5, Absent only)
- **When** auto-refresh occurs
- **Then** filters remain applied
- **And** only filtered results refresh

### AC5: Pause on Interaction
- **Given** I am typing in the search box
- **Then** auto-refresh is paused
- **And** resumes 5 seconds after I stop typing

### AC6: Manual Refresh Button
- **Given** I want immediate updated data
- **When** I click the "Refresh Now" button
- **Then** data refreshes immediately
- **And** the 30-second timer resets

### AC7: Refresh Status Indicator
- **Given** auto-refresh is active
- **Then** I see "Auto-refresh: ON (every 30s)"
- **And** I can toggle auto-refresh off if needed

### AC8: Disable Auto-Refresh Option
- **Given** I toggle auto-refresh to OFF
- **Then** the dashboard stops refreshing automatically
- **And** I see "Auto-refresh: OFF"
- **And** I can still use Manual Refresh

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Network error on refresh | Show subtle error, retry next cycle |
| Server timeout | Show "Unable to refresh", retry |
| Tab not visible (background) | Pause refresh to save resources |
| Multiple tabs open | Each refreshes independently |
| Very slow connection | Show loading state, don't timeout early |
| Refresh takes > 5 seconds | Show progress, don't start new refresh |

---

## Test Scenarios

- [ ] Auto-refresh occurs every 30 seconds
- [ ] Last updated timestamp updates
- [ ] Loading indicator shown during refresh
- [ ] Scroll position preserved
- [ ] Filters preserved after refresh
- [ ] Refresh pauses during text input
- [ ] Manual refresh button works
- [ ] Auto-refresh can be toggled off
- [ ] Background tab pauses refresh
- [ ] Network error handled gracefully
- [ ] Timer resets after manual refresh

---

## Technical Notes

### Implementation Approach
- Use JavaScript `setInterval` for timing
- Fetch API for AJAX requests
- Partial page update (don't reload entire page)
- Use `document.hidden` to detect background tabs

### Refresh Endpoint
```
GET /api/v1/attendance/summary?date=2026-02-04&grade=5&section=A
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0034](US0034-attendance-dashboard.md) | Functional | Dashboard exists | Draft |
| [US0036](US0036-attendance-filter-search.md) | Functional | Filters to preserve | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
