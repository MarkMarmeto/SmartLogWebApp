# PL0037: Profile Picture Audit Log Fix (BG0007)

> **Status:** Complete
> **Bug:** [BG0007: Profile Picture Upload/Delete — Succeeds on Server but UI Shows "Failed" Alert](../bugs/BG0007-profile-picture-upload-delete-false-failure-alert.md)
> **Epic:** EP0003 (Student Management) + EP0004 (Faculty Management); also touches EP0002 (User Management) for the user-self endpoints
> **Created:** 2026-04-27
> **Language:** C# 12 / ASP.NET Core 8.0
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Replace six positional `_auditService.LogAsync(...)` calls in `ProfilePictureApiController.cs` with named-argument calls, so the audit row passes the FK constraint on `AuditLog.PerformedByUserId` and the controller returns `200` instead of `500`. No DB migration, no JS change, no service-layer change.

---

## Acceptance Criteria Mapping

All AC items come from BG0007. They map to a single change in one file.

| AC (from bug) | Implementation |
|---------------|----------------|
| Upload on Student/Faculty/Profile shows success alert (no false failure) | Phase 1 — fix audit calls in 3 upload endpoints |
| Delete on Student/Faculty/Profile reloads silently (no false failure) | Phase 1 — fix audit calls in 3 delete endpoints |
| `AuditLog` row written with valid `PerformedByUserId`, populated `Details` and `IpAddress` | Phase 1 — correct named-arg mapping |
| No regressions in other audit-logged flows | N/A — change is isolated to one controller |

---

## Technical Context

### Current (broken) call shape

`Controllers/Api/ProfilePictureApiController.cs:113-115` (representative; same pattern at `:69`, `:157`, `:190`, `:224`, `:258`):

```csharp
await _auditService.LogAsync("ProfilePictureUpdated", student.Id.ToString(),
    $"Profile picture updated for student {student.FullName} ({student.StudentId})",
    HttpContext.Connection.RemoteIpAddress?.ToString());
```

`IAuditService.LogAsync(string action, string? userId, string? performedByUserId, string? details, string? ipAddress, string? userAgent)` — `performedByUserId` is FK to `AspNetUsers.Id`, so passing a sentence triggers a SQL FK violation on `SaveChangesAsync` and the controller returns 500.

### Target call shape

```csharp
var performedById = _userManager.GetUserId(User);
await _auditService.LogAsync(
    action: "ProfilePictureUpdated",
    userId: null,                      // student/faculty are not ApplicationUsers
    performedByUserId: performedById,
    details: $"Profile picture updated for student {student.FullName} ({student.StudentId})",
    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
    userAgent: Request.Headers.UserAgent.ToString());
```

For the **user-self** endpoints (`UploadUserProfilePicture`, `DeleteUserProfilePicture`), `userId` is `user.Id` (valid `AspNetUsers.Id`); `performedByUserId` is also `user.Id` (the user is acting on themselves).

### No new dependencies

`UserManager<ApplicationUser>` is already injected (`ProfilePictureApiController.cs:20, 27, 33`). `Request.Headers.UserAgent` is available on `ControllerBase`. No constructor change.

### Pattern source

`Pages/Admin/StudentDetails.cshtml.cs:111-119` (QR regeneration) is the existing correct pattern in this codebase — copy that shape.

---

## Implementation Phases

### Phase 1 — Fix all six call sites

**File:** `src/SmartLog.Web/Controllers/Api/ProfilePictureApiController.cs`

Six edits, one per endpoint. Each edit:
1. Adds a local `var performedById = _userManager.GetUserId(User);` near the top of the `try` block (after the entity lookup).
2. Replaces the positional `LogAsync(...)` call with a named-arg version per the mapping table below.

| Endpoint | Line(s) | `userId` | `performedByUserId` | `details` (existing sentence) |
|----------|---------|----------|---------------------|--------------------------------|
| `UploadUserProfilePicture` | 69-71 | `user.Id` | `user.Id` | "User {UserName} updated their profile picture" |
| `UploadStudentProfilePicture` | 113-115 | `null` | `performedById` | "Profile picture updated for student {FullName} ({StudentId})" |
| `UploadFacultyProfilePicture` | 157-159 | `null` | `performedById` | "Profile picture updated for faculty {FullName} ({EmployeeId})" |
| `DeleteUserProfilePicture` | 190-192 | `user.Id` | `user.Id` | "User {UserName} deleted their profile picture" |
| `DeleteStudentProfilePicture` | 224-226 | `null` | `performedById` | "Profile picture deleted for student {FullName} ({StudentId})" |
| `DeleteFacultyProfilePicture` | 258-260 | `null` | `performedById` | "Profile picture deleted for faculty {FullName} ({EmployeeId})" |

All six calls also gain `userAgent: Request.Headers.UserAgent.ToString()` for completeness (currently missing).

### Phase 2 — Manual verification

No automated tests added. Rationale: there are no existing tests for `ProfilePictureApiController` (`tests/SmartLog.Web.Tests/Controllers/` only contains `ScansApiControllerTests.cs`), and the in-tree fixed bugs BG0001–BG0006 followed the same manual-verification convention. The fix is mechanical and the acceptance test is a UI round-trip.

**Verification checklist** (run against `dotnet run` on `http://localhost:5050`, logged in as SuperAdmin):

1. `Admin → Students → [any] → Details` — click **Upload Photo**, pick a JPG. Expect: **success alert**, image updates in place. Confirm an `AuditLog` row exists with `Action='ProfilePictureUpdated'`, `PerformedByUserId` = current admin's id, `Details` containing student name + StudentId, `IpAddress` populated.
2. Same page — click **Remove**, confirm. Expect: page reload, placeholder shown, no failure alert. Confirm `AuditLog` row with `Action='ProfilePictureDeleted'`.
3. Repeat (1) and (2) on `Admin → Faculty → [any] → Details`.
4. Repeat (1) and (2) on `Account → Profile` (user-self upload/delete) — expect `AuditLog` with both `UserId` and `PerformedByUserId` set to the current user's id.
5. Browser DevTools Network tab: confirm `POST /api/v1/profile-picture/...` returns **200** with `{ path, url }` and `DELETE` returns 200 with `{ message }`.
6. Server log: no `DbUpdateException` / FK violation entries.

---

## Risks & Considerations

- **Risk: `_userManager.GetUserId(User)` returns null.** The controller is `[Authorize]` and all six endpoints further require `CanManageStudents` / `CanManageFaculty` policies (or are user-self). An unauthenticated request would be 401 before reaching the handler, so `User` always represents a real authenticated identity here. Safe to use directly without a null check.
- **Risk: User-self endpoint loses identity context.** Already had it (`user = await _userManager.GetUserAsync(User)`); we just add a UA string. No new failure mode.
- **No DB migration.** The audit row schema is unchanged; we're only writing valid FK values now.
- **No JS change.** The page-side scripts (`StudentDetails.cshtml:362-435`, `FacultyDetails.cshtml:246-315`) already handle the success path correctly — they only reported failure because the server lied.

---

## Out of Scope

- Adding tests for `ProfilePictureApiController` — separate hardening story if desired.
- Refactoring `IAuditService.LogAsync` to take a typed request object (would prevent future positional-arg mistakes, but that's a cross-cutting refactor with ~20+ call sites and belongs in its own story).
- Auditing the rest of the codebase for similar misaligned positional-arg calls — done quickly during review (BG0007 only flagged the controller); a broader sweep can be a separate story if needed.

---

## Estimated Effort

- Phase 1 edits: ~10 minutes
- Phase 2 manual verification: ~15 minutes
- **Total:** ~25 minutes

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude (Opus 4.7) | Initial plan drafted |
