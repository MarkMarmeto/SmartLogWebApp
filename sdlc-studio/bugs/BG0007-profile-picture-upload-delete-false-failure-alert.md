# BG0007: Profile Picture Upload/Delete — Succeeds on Server but UI Shows "Failed" Alert

> **Status:** Fixed
> **Severity:** Medium
> **Epic:** EP0003 (Student Management) + EP0004 (Faculty Management) — also affects user self-upload (EP0002)
> **Reporter:** Mark
> **Created:** 2026-04-27

## Description

On the **Student Details** and **Faculty Details** pages, uploading or removing a profile picture **succeeds end-to-end** (file is written/deleted on disk and the entity's `ProfilePicturePath` is updated in the database) but the UI immediately shows an error alert:

- Upload → "Failed to upload profile picture"
- Delete → "Failed to delete profile picture"

Reloading the page confirms the new picture is in place (or removed). The user sees a confusing failure alert despite the underlying operation having completed.

---

## Steps to Reproduce

### Upload (Student)
1. Login as SuperAdmin or Admin
2. Navigate to **Admin → Students → [any student] → Details**
3. Click **Upload Photo**, choose any valid JPG/PNG/GIF under 5MB
4. Observe alert: *"Failed to upload profile picture"*
5. Refresh the page — the new photo is shown

### Upload (Faculty)
1. Navigate to **Admin → Faculty → [any faculty] → Details**
2. Repeat the upload steps above
3. Same false-failure alert; refresh shows new photo

### Delete (Student / Faculty)
1. On Student Details or Faculty Details, click **Remove**, confirm
2. Alert: *"Failed to delete profile picture"*
3. Refresh — the photo is gone (placeholder shown)

**Expected:** Upload shows "Profile picture uploaded successfully!" and delete reloads the page silently. No false-failure alert.

**Actual:** Server returns 500 after the picture has already been saved/deleted; JS shows the failure alert.

---

## Root Cause

`ProfilePictureApiController` writes the file (or deletes it) and saves the entity, then calls `_auditService.LogAsync(...)` to record the action. The audit call uses **positional arguments that misalign with the parameter names** in `IAuditService.LogAsync`:

**Interface (`Services/IAuditService.cs`):**
```csharp
Task LogAsync(
    string action,
    string? userId = null,
    string? performedByUserId = null,
    string? details = null,
    string? ipAddress = null,
    string? userAgent = null);
```

`UserId` and `PerformedByUserId` are both **foreign keys to `AspNetUsers`**, configured in `Data/ApplicationDbContext.cs:76-84`.

**Misaligned call (`Controllers/Api/ProfilePictureApiController.cs:113-115`, student upload):**
```csharp
await _auditService.LogAsync("ProfilePictureUpdated",
    student.Id.ToString(),                         // → userId (Guid string, not an AspNetUsers FK)
    $"Profile picture updated for student ...",    // → performedByUserId (a sentence! never a valid FK)
    HttpContext.Connection.RemoteIpAddress?...);   // → details
```

When `AuditService.LogAsync` calls `SaveChangesAsync()` for the `AuditLog` row, SQL Server rejects with a foreign key violation (the description sentence isn't a valid `AspNetUsers.Id`). The exception bubbles to the controller's outer `catch`, which returns **500** with `{ error: "Failed to upload profile picture" }`. The JS reads `!response.ok` and alerts failure — but the file save and entity `SaveChangesAsync` already committed before the audit step ran.

### All affected call sites in `ProfilePictureApiController.cs`

| Line | Endpoint | Notes |
|------|----------|-------|
| 69-71 | `POST /api/v1/profile-picture/user` | `user.Id` is a valid FK for `userId`, but `performedByUserId` still gets a sentence |
| 113-115 | `POST /api/v1/profile-picture/student/{id}` | Reported (Student Details) |
| 157-159 | `POST /api/v1/profile-picture/faculty/{id}` | Reported (Faculty Details) |
| 190-192 | `DELETE /api/v1/profile-picture/user` | Same misalignment |
| 224-226 | `DELETE /api/v1/profile-picture/student/{id}` | Reported (Student Details) |
| 258-260 | `DELETE /api/v1/profile-picture/faculty/{id}` | Reported (Faculty Details) |

The correct calling pattern already used elsewhere in the codebase (e.g. `Pages/Admin/StudentDetails.cshtml.cs:113-119` for QR regeneration) is:

```csharp
var currentUserId = _userManager.GetUserId(User);
await _auditService.LogAsync(
    action: "...",
    userId: null,                          // entity isn't an ApplicationUser
    performedByUserId: currentUserId,      // the admin who did it
    details: $"...",
    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
    userAgent: Request.Headers.UserAgent.ToString());
```

---

## Scope of Fix

**File:** `src/SmartLog.Web/Controllers/Api/ProfilePictureApiController.cs`

1. Inject/use `UserManager<ApplicationUser>` (already injected) to obtain the current admin's id via `_userManager.GetUserId(User)`.
2. Rewrite all six `_auditService.LogAsync(...)` calls to use **named arguments** with this mapping:
   - `action`: existing action string
   - `userId`: `user.Id` for the user-self endpoints; `null` for student/faculty endpoints (Students/Faculty are not ApplicationUsers)
   - `performedByUserId`: `_userManager.GetUserId(User)` (the admin performing the action)
   - `details`: the descriptive sentence currently in the wrong slot
   - `ipAddress`: `HttpContext.Connection.RemoteIpAddress?.ToString()`
   - `userAgent`: `Request.Headers.UserAgent.ToString()` (newly added — currently missing)
3. No change to `AuditService` or `IAuditService` itself.

No DB migration, no JS change, no other call-site changes.

---

## Acceptance

- [ ] Uploading a photo on Student Details shows "Profile picture uploaded successfully!" (no failure alert) and the preview updates in place
- [ ] Removing a photo on Student Details reloads the page with no failure alert; placeholder shown
- [ ] Same behaviour verified on Faculty Details (upload + remove)
- [ ] Same behaviour verified on Account Profile (user-self upload + remove)
- [ ] An `AuditLog` row is written for each of the six actions with:
  - valid `PerformedByUserId` (the current admin)
  - `Details` containing the descriptive sentence
  - `IpAddress` populated
- [ ] No regressions in other audit-logged flows

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial bug logged |
