# BG0004: FilipinoOnly Broadcast — No SMS Sent and Broadcast History Not Meaningfully Created

> **Status:** Fixed
> **Severity:** High
> **Epic:** EP0008 (SMS Broadcast)
> **Reporter:** Mark
> **Created:** 2026-04-26

## Description

When the **Filipino Only** language mode is selected in either the General Announcement or Emergency broadcast pages, submitting the form results in:

1. **No SMS messages are delivered** to any parent/guardian.
2. **Broadcast history shows a "ghost" entry** — a row is created but with `TotalRecipients = 0` and status stuck on `Sending` (non-bulk path), giving the appearance that nothing happened.

Both the Emergency and General Announcement pages are affected identically.

---

## Steps to Reproduce

1. Sign in as Admin
2. Navigate to **SMS → New Announcement** (or **Emergency**)
3. Select the **Filipino Only** radio button
4. Enter a Filipino message body (leave the English field blank)
5. Choose any grade/section targeting
6. Click **Send** (or **Queue Announcement**)
7. Observe: page redirects to Broadcast History

**Expected:** A broadcast entry is created with `TotalRecipients > 0`; SMS messages are queued and delivered in Filipino to all targeted students.

**Actual:** Broadcast entry shows `TotalRecipients = 0`. No SmsQueue rows are created. No SMS is ever delivered.

---

## Root Cause Analysis

Two separate defects combine to cause this behaviour.

### Defect A — Wrong message body stored on Broadcast record
**File:** `src/SmartLog.Web/Services/Sms/SmsService.cs`

In both `QueueEmergencyAnnouncementAsync` (~line 318) and `QueueAnnouncementAsync` (~line 403):

```csharp
var broadcast = new Broadcast
{
    Message = bodies.EnglishBody,   // ← ALWAYS uses EnglishBody
```

When `Mode == FilipinoOnly`, the admin does not fill in the English textarea; `bodies.EnglishBody` is an empty string `""`. The `Broadcast.Message` column is `[Required]` (NOT NULL). An empty string passes the NOT NULL DB constraint, so the record is saved — but with an empty `Message` field instead of the Filipino body.

**Fix:** Assign `Message` based on the active mode:
```csharp
Message = bodies.Mode == BroadcastLanguageMode.FilipinoOnly
    ? (bodies.FilipinoBody ?? string.Empty)
    : (bodies.EnglishBody ?? string.Empty),
```

The same incorrect default `{ "Message", bodies.EnglishBody }` appears in the `basePlaceholders` dictionary in both methods — it is eventually overridden per-language inside `ExecuteBroadcastAsync`, so it does not affect the rendered SMS text; only the stored Broadcast record is wrong.

### Defect B — ShouldSendToStudent skips all students without explicit FIL language preference
**File:** `src/SmartLog.Web/Models/Sms/BroadcastMessageBodies.cs` (line 19)

```csharp
public bool ShouldSendToStudent(string? smsLanguage) => Mode switch
{
    BroadcastLanguageMode.FilipinoOnly => smsLanguage == "FIL",   // ← only "FIL" students pass
    _ => true
};
```

This check is applied inside `ExecuteBroadcastAsync` for every student. In a school where no student (or very few) has `Student.SmsLanguage` explicitly set to `"FIL"`, **all students are skipped**. `renderedByLanguage` has a valid "FIL" entry, but the skip logic prevents anyone from receiving it.

The expected behaviour of **FilipinoOnly** is: *send the Filipino message to all targeted students* (the admin is overriding individual preferences). This mirrors how **EnglishOnly** works — it reaches everyone who isn't an explicit FIL-preference student, effectively reaching most students.

**Fix:** Remove the skip check for FilipinoOnly (the message should go to all students; it is already rendered in Filipino):
```csharp
BroadcastLanguageMode.FilipinoOnly => true,   // send to all; content is already in Filipino
```

Or, equivalently, redefine the intent: FilipinoOnly means "render in Filipino and send to everyone" — no skip needed.

---

## Impact

- Filipino-language broadcasts are completely non-functional.
- Admin sees a false "success" redirect with a Broadcast history entry showing 0 recipients.
- No SMS is delivered to parents.

---

## Scope of Fix

| File | Change |
|------|--------|
| `src/SmartLog.Web/Services/Sms/SmsService.cs` | Fix `Message = bodies.EnglishBody` to use correct body per mode (both `QueueEmergencyAnnouncementAsync` and `QueueAnnouncementAsync`) |
| `src/SmartLog.Web/Models/Sms/BroadcastMessageBodies.cs` | Fix `ShouldSendToStudent` — `FilipinoOnly` should return `true` for all students |

---

## Acceptance

- [ ] Submitting an Emergency or Announcement with FilipinoOnly mode creates a Broadcast record with the Filipino body stored in `Message`
- [ ] All targeted students (regardless of `SmsLanguage` preference) receive the SMS in Filipino
- [ ] Broadcast History shows correct `TotalRecipients` count
- [ ] Existing EnglishOnly and Both modes are unaffected
- [ ] Unit tests: `BroadcastLanguageRoutingTests.cs` updated to cover FilipinoOnly sends to null/EN students

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial bug logged |
