# BG0006: Broadcast History — Non-Graded Targeting Not Visible in Grades Column

> **Status:** Fixed
> **Severity:** Low
> **Epic:** EP0008 (SMS Broadcast)
> **Reporter:** Mark
> **Created:** 2026-04-26

## Description

When a broadcast is sent targeting **Non-Graded sections** (alongside or instead of regular grades), the **Grades** column in the Broadcast History page only shows the regular grade codes (e.g., `7, 8, 9, 10`). There is no indication that Non-Graded students were also included as recipients.

---

## Steps to Reproduce

1. Navigate to **SMS → New Announcement** (or Emergency)
2. Select some regular grades (e.g., 7–10) AND select one or more Non-Graded sections
3. Send the broadcast
4. Navigate to **Broadcast History**
5. Observe the **Grades** column for the new broadcast

**Expected:** Something like `7, 8, 9, 10, Non-Graded` or `7, 8, 9, 10 + Non-Graded` to indicate Non-Graded students were targeted.

**Actual:** Only `7, 8, 9, 10` is shown. Non-Graded targeting is invisible.

---

## Root Cause

Non-Graded sections have no grade level code (their grade level is `"NG"` but they are not part of any Program). When targeting is built in the page model:

**File:** `src/SmartLog.Web/Pages/Admin/Sms/Emergency.cshtml.cs` (line ~185) and `Announcement.cshtml.cs` (line ~173):

```csharp
var historyGrades = filters.SelectMany(f => f.GradeLevelCodes).Distinct().ToList();
```

Non-Graded filters have empty `GradeLevelCodes`, so they contribute nothing to `historyGrades`. The Non-Graded targeting is separately tracked in `historyPrograms` (which adds the string `"Non-Graded"`), which is stored in `Broadcast.AffectedPrograms`.

In `Broadcasts.cshtml`, the Grades column only reads `b.AffectedGrades` (the JSON-serialised grade codes). It never reads `b.AffectedPrograms`, so the Non-Graded info — which lives in a different column — is never surfaced.

---

## Scope of Fix

**File:** `src/SmartLog.Web/Pages/Admin/Sms/Broadcasts.cshtml`

After deserialising `AffectedGrades` (per BG0005 fix), also parse `AffectedPrograms` and append "Non-Graded" to the display if it is present:

```razor
@{
    var grades = string.IsNullOrEmpty(b.AffectedGrades)
        ? new List<string>()
        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(b.AffectedGrades) ?? new();

    var programs = string.IsNullOrEmpty(b.AffectedPrograms)
        ? new List<string>()
        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(b.AffectedPrograms) ?? new();

    var hasNonGraded = programs.Contains("Non-Graded", StringComparer.OrdinalIgnoreCase);

    var displayParts = new List<string>();
    if (grades.Any()) displayParts.Add(string.Join(", ", grades));
    if (hasNonGraded) displayParts.Add("Non-Graded");
}
@if (!displayParts.Any())
{
    <span class="text-muted">All</span>
}
else
{
    @string.Join(", ", displayParts)
}
```

This fix is best implemented together with BG0005 since they both touch the same Grades cell.

---

## Acceptance

- [ ] Broadcast targeting only Non-Graded shows `Non-Graded` in the Grades column
- [ ] Broadcast targeting grades 7–10 + Non-Graded shows `7, 8, 9, 10, Non-Graded`
- [ ] Broadcast targeting all grades (no filter) still shows `All`
- [ ] No regressions on existing broadcasts

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial bug logged |
