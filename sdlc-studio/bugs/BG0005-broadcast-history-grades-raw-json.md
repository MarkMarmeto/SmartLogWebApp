# BG0005: Broadcast History — Grades Column Shows Raw JSON Array

> **Status:** Fixed
> **Severity:** Low
> **Epic:** EP0008 (SMS Broadcast)
> **Reporter:** Mark
> **Created:** 2026-04-26

## Description

The **Grades** column in the Broadcast History table (`/Admin/Sms/Broadcasts`) displays the raw JSON array string stored in the database rather than a human-readable format.

For example, instead of: `7, 8, 9, 10`
It shows: `["7", "8", "9", "10"]`

---

## Steps to Reproduce

1. Send any Announcement or Emergency broadcast targeted at specific grade levels
2. Navigate to **SMS → Broadcast History**
3. Observe the **Grades** column

**Expected:** `7, 8, 9, 10`

**Actual:** `["7", "8", "9", "10"]`

---

## Root Cause

`Broadcast.AffectedGrades` is stored as a JSON-serialised array string (e.g., `["7","8","9","10"]`). The Razor view renders it directly without deserialising:

**File:** `src/SmartLog.Web/Pages/Admin/Sms/Broadcasts.cshtml` (line ~96)

```html
@b.AffectedGrades   ← raw JSON printed as-is
```

---

## Scope of Fix

**File:** `src/SmartLog.Web/Pages/Admin/Sms/Broadcasts.cshtml`

Deserialise and join the grade codes before rendering:

```razor
@{
    var grades = string.IsNullOrEmpty(b.AffectedGrades)
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(b.AffectedGrades);
}
@if (grades == null || !grades.Any())
{
    <span class="text-muted">All</span>
}
else
{
    @string.Join(", ", grades)
}
```

The same pattern should be applied when displaying `AffectedPrograms` if it is ever shown separately.

---

## Acceptance

- [ ] Grades column shows comma-separated grade codes: `7, 8, 9, 10`
- [ ] "All" placeholder is shown when `AffectedGrades` is null (broadcast targeted all grades)
- [ ] No regressions on broadcast list rendering

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial bug logged |
