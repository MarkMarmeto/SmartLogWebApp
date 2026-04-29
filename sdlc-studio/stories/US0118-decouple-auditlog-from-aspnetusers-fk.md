# US0118: Decouple AuditLog from AspNetUsers FK

> **Status:** Draft
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md) (also touches [EP0016: PII / RA 10173 Compliance](../epics/EP0016-pii-ra10173-compliance.md))
> **Owner:** TBD
> **Created:** 2026-04-27
> **Drafted by:** Claude (Opus 4.7)
> **Related Bug:** [BG0007](../bugs/BG0007-profile-picture-upload-delete-false-failure-alert.md) — surfaced as an FK violation; a typed audit-write contract would have caught it earlier

## User Story

**As a** Tony (IT Admin) maintaining the SmartLog audit trail
**I want** the `AuditLog` table to stop using foreign-key constraints against `AspNetUsers`
**So that** routine application bugs and future user-lifecycle operations (deletes, renames, archival) don't crash audit writes or block user-management actions, while audit history remains human-readable and survives the deletion of the referenced user.

## Context

### Background

`AuditLog` currently has two FKs to `AspNetUsers.Id`:

- `AuditLog.UserId` → user the action was performed *on*
- `AuditLog.PerformedByUserId` → user who *performed* the action

Both are configured with `DeleteBehavior.NoAction` in `ApplicationDbContext.cs:69-85` and have EF navigation properties (`User`, `PerformedByUser`).

This design has produced repeated incidents:

1. **BG0007 (2026-04-27):** A positional-arg mistake in `ProfilePictureApiController` passed a free-text sentence into `PerformedByUserId`. SQL Server rejected the row with an FK violation, the controller returned 500, and the UI showed a false "Upload Failed" alert even though the file write succeeded. PL0037 fixed the call sites but the FK class of failure remains for any future audit-write misuse.
2. **User lifecycle pressure (anticipated):** EP0016 PII work and future user-management changes (account deletion, rename, archival) will hit `NoAction` FKs and either throw or require ceremony to keep audit rows orphan-safe.
3. **Read-side coupling:** Audit rows JOIN to `AspNetUsers` on every viewer load. If a user is later soft-deleted or renamed, historical audit rows lose their human label or expose an inconsistent name.

The industry-standard pattern for audit/event-log tables is **append-only, denormalized, no FKs** — capture the user-name snapshot at write time so the row is meaningful forever, even if the underlying user is gone.

### Why now

PL0037 (BG0007 fix) is mechanical and addresses the symptom. This story addresses the underlying coupling so the same class of bug can't recur, and pre-empts pain when EP0016 / future user-deletion features land.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0001 | Security | Audit trail must remain tamper-evident & complete | Removing FK does not weaken — append-only semantics unchanged |
| EP0016 | Legal | RA 10173 audit retention; rows must outlive the user lifecycle | Snapshot user name on write so rows survive user deletion |
| US0099 | Existing | LegalHold flag protects rows from purge | Unaffected; legal-hold codepath continues to work |
| US0050/US0051 | UI | AuditLog Viewer with filtering | Filter dropdown for "Performed By" must keep working without nav join |
| BG0007 | Bug | Positional-arg misuse silently inserted bad data into FK column | Replace FK-as-canary with an explicit runtime guard in `AuditService.LogAsync` |

---

## Acceptance Criteria

### AC1: FK Constraints Removed

- **Given** the `AuditLog` table
- **Then** the FK constraints `FK_AuditLogs_AspNetUsers_UserId` and `FK_AuditLogs_AspNetUsers_PerformedByUserId` are dropped via EF Core migration
- **And** the `UserId` and `PerformedByUserId` columns remain (as plain `nvarchar(450)` columns matching the Identity Id type)
- **And** existing data in those columns is preserved unchanged

### AC2: Navigation Properties Removed

- **Given** the `AuditLog` entity
- **Then** the `User` and `PerformedByUser` navigation properties are removed from `AuditLog.cs`
- **And** the corresponding `HasOne/WithMany/HasForeignKey` configuration is removed from `ApplicationDbContext.cs`
- **And** the project builds without referencing those nav props anywhere

### AC3: Snapshot Columns Added

- **Given** the `AuditLog` table
- **Then** two new columns are added: `UserName` (`nvarchar(256)`, nullable) and `PerformedByUserName` (`nvarchar(256)`, nullable)
- **And** the same migration backfills these columns for existing rows by JOINing `AspNetUsers` on the matching Id (rows with no match remain null)
- **And** rows where `PerformedByUserId` is null produce `PerformedByUserName = null` (the viewer continues to render `"System"` for null)

### AC4: AuditService Captures Snapshot on Write

- **Given** any caller invoking `IAuditService.LogAsync`
- **When** a non-null `userId` or `performedByUserId` is provided
- **Then** `AuditService` looks up the matching `ApplicationUser.UserName` via the existing `UserManager` (one lookup per non-null id, in-memory cache acceptable but not required for v1)
- **And** writes the resolved name into `UserName` / `PerformedByUserName` on the new row
- **And** if the lookup returns null (id doesn't match any user), the snapshot field is set to null and the write still succeeds (no exception)

### AC5: Runtime Guard Replaces FK-as-Canary

- **Given** a caller passes a non-null `userId` or `performedByUserId` to `AuditService.LogAsync`
- **Then** the value is validated against the Identity Id shape (length ≤ 450 and matches the Identity GUID-string format used elsewhere in the codebase)
- **And** if the value fails validation, `LogAsync` throws `ArgumentException` with a message identifying which parameter was malformed
- **And** the exception propagates to the caller (same loud-failure semantics as the previous FK violation, but raised at the application boundary)

### AC6: Read Sites Use Snapshot Columns

The four read sites that currently `.Include` the nav props are updated to project from the snapshot columns directly, with **no JOIN** to `AspNetUsers`:

- `Pages/Admin/AuditLogs.cshtml.cs` (viewer page) — `AuditLogEntry.UserName` / `PerformedByUserName` come from the snapshot columns; the legacy "System" fallback for null `PerformedByUserName` is preserved
- `Services/DashboardService.cs` (recent-activity panel) — same change
- `Services/ReportExportService.cs` (CSV/Excel export) — same change
- `Pages/Admin/Sms/Index.cshtml.cs` (No-Scan Alert run status) — verify unaffected; update only if it currently uses nav

After this change, no production code path performs `.Include(a => a.User)` or `.Include(a => a.PerformedByUser)` on `AuditLog`.

### AC7: Filter Dropdown Remains Functional

- **Given** the AuditLog Viewer's "Performed By" filter dropdown (`AuditLogs.cshtml.cs:177-199`)
- **Then** the dropdown continues to populate from distinct users who have audit entries
- **And** when no longer joining via FK, the dropdown source is the distinct set of `(PerformedByUserId, PerformedByUserName)` pairs from `AuditLogs` itself (not `AspNetUsers`)
- **And** filtering by selected user id still returns the correct rows

### AC8: Existing Audit Functionality Unchanged

- **Given** the LegalHold toggle, bulk-hold, retention purge, and CSV export flows
- **Then** all continue to work end-to-end with no behavioural change visible to the user
- **And** the `AuditLogRetentionHandler` continues to honour `LegalHold = true`

### AC9: Tests Pass

- **Given** the existing test suite
- **Then** `AuditLogRetentionHandlerTests` and any other AuditLog-touching tests pass after the schema change
- **And** any in-memory test fixture that currently sets `auditLog.User = ...` is updated to set the snapshot columns instead

---

## Scope

### In Scope

- EF Core migration: drop both FKs, add `UserName` / `PerformedByUserName` columns, backfill from `AspNetUsers`
- Remove `User` / `PerformedByUser` nav properties from `AuditLog.cs` and corresponding fluent config from `ApplicationDbContext.cs`
- `AuditService.LogAsync`: add `UserManager` dependency, snapshot user names, validate id shape
- Update 4 read sites listed in AC6
- Update tests touched by the schema/nav change

### Out of Scope

- Refactoring `IAuditService.LogAsync` to a typed request object (would prevent positional-arg mistakes more robustly but is a cross-cutting refactor with 20+ call sites — separate story if desired)
- Auditing other tables for similar FK coupling (`Faculty.UserId`, `Device.RegisteredBy`, etc.) — those are not append-only event tables and the FK is appropriate there
- Performance optimisation of the username lookup (e.g. caching layer) — single `UserManager.FindByIdAsync` per write is fine at SmartLog scale
- Revising the AuditLog admin UI — visuals unchanged
- Soft-delete / archival of `AspNetUsers` — separate concern, but this story unblocks it

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| `LogAsync` called with `performedByUserId = null` (system action) | `PerformedByUserName` written as null; viewer renders "System" |
| `LogAsync` called with a valid Identity-id-shaped string that doesn't match any user | Snapshot field set to null; row inserts successfully (no FK to enforce) |
| `LogAsync` called with a malformed value (e.g. a sentence) | `ArgumentException` thrown by AC5 guard; caller sees a 500 (same surface as today, fewer false UI errors because we no longer hit the DB) |
| Migration runs against a DB with audit rows referencing a user that's already gone | Backfill leaves `UserName` null for those rows; rendering falls back to id |
| User renamed after audit row is written | Audit row keeps the old name (snapshot semantics — intentional) |
| User deleted after audit row is written | Audit row unaffected; this is the central reason for the change |
| Legacy code still sets `auditLog.User = someUser` in test fixtures | Compile error after AC2; fixtures updated to set snapshot strings |

---

## Test Scenarios

- [ ] Migration drops both FKs, adds two columns, backfills existing rows from `AspNetUsers`
- [ ] AuditLog row can be inserted with a `PerformedByUserId` that does not exist in `AspNetUsers` (no FK violation)
- [ ] `AuditService.LogAsync` populates `PerformedByUserName` from `UserManager` for a real user id
- [ ] `AuditService.LogAsync` writes null `PerformedByUserName` when the id doesn't resolve, no exception
- [ ] `AuditService.LogAsync` throws `ArgumentException` when `performedByUserId` is a malformed string (BG0007 regression guard)
- [ ] AuditLog viewer page renders user names without `.Include` and without JOIN to `AspNetUsers` (verify SQL via logging)
- [ ] AuditLog viewer "Performed By" filter dropdown still populates correctly
- [ ] Dashboard recent-activity panel renders audit rows without `.Include`
- [ ] CSV export contains `UserName` / `PerformedByUserName` from snapshot columns
- [ ] Retention purge with LegalHold flag continues to work end-to-end
- [ ] Deleting a user (via Identity API at console / future UI) does not throw FK violation on existing audit rows
- [ ] Existing audit history (rows from before the migration) still renders user names correctly post-backfill

---

## Technical Notes

### Files

| File | Change |
|------|--------|
| `src/SmartLog.Web/Data/Entities/AuditLog.cs` | Remove `User`, `PerformedByUser` nav props; add `UserName`, `PerformedByUserName` string fields |
| `src/SmartLog.Web/Data/ApplicationDbContext.cs` (lines 68-85) | Remove `HasOne/WithMany/HasForeignKey` config; keep indexes |
| `src/SmartLog.Web/Migrations/{timestamp}_DecoupleAuditLogFromAspNetUsers.cs` | New migration: drop FKs, add columns, backfill |
| `src/SmartLog.Web/Services/AuditService.cs` | Inject `UserManager<ApplicationUser>`; snapshot names; runtime guard |
| `src/SmartLog.Web/Pages/Admin/AuditLogs.cshtml.cs` (lines 60-107, 177-199) | Remove `.Include`; project from snapshot columns; rebuild filter dropdown |
| `src/SmartLog.Web/Services/DashboardService.cs` (lines 216-223) | Remove `.Include`; project from snapshot |
| `src/SmartLog.Web/Services/ReportExportService.cs` (lines 261-301) | Remove `.Include`; export from snapshot |
| `src/SmartLog.Web/Pages/Admin/Sms/Index.cshtml.cs` (lines 73-77) | Verify; update only if currently uses nav |
| `tests/SmartLog.Web.Tests/Services/Retention/AuditLogRetentionHandlerTests.cs` | Adjust fixtures if they set nav props |

### Migration sketch

```csharp
// Up
migrationBuilder.DropForeignKey("FK_AuditLogs_AspNetUsers_UserId", "AuditLogs");
migrationBuilder.DropForeignKey("FK_AuditLogs_AspNetUsers_PerformedByUserId", "AuditLogs");
migrationBuilder.AddColumn<string>("UserName", "AuditLogs", maxLength: 256, nullable: true);
migrationBuilder.AddColumn<string>("PerformedByUserName", "AuditLogs", maxLength: 256, nullable: true);
migrationBuilder.Sql(@"
    UPDATE a SET a.UserName = u.UserName
    FROM AuditLogs a INNER JOIN AspNetUsers u ON a.UserId = u.Id;
    UPDATE a SET a.PerformedByUserName = u.UserName
    FROM AuditLogs a INNER JOIN AspNetUsers u ON a.PerformedByUserId = u.Id;
");
```

### Validation pattern (AC5)

ASP.NET Core Identity uses GUID-string Ids by default in this project (`Pages/Admin/AuditLogs.cshtml.cs` treats `UserId` / `PerformedByUserId` as strings parsed from Identity). The guard checks `Guid.TryParse(value, out _)` for non-null inputs; reject otherwise.

---

## Dependencies

| Story | Type | Status |
|-------|------|--------|
| [US0050](US0050-audit-log-viewer.md) | UI baseline | Done |
| [US0051](US0051-audit-log-search-filter.md) | UI filters | Done |
| [US0099](US0099-auditlog-retention-with-legal-hold.md) | LegalHold codepath must keep working | Done |
| [BG0007 / PL0037](../plans/PL0037-profile-picture-audit-log-fix.md) | Symptomatic fix in place | Done |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — schema migration + backfill + 4 read-site updates + service-layer change + test fixtures. Mechanical but spans multiple layers.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude (Opus 4.7) | Initial story drafted |
