# PL0038: Decouple AuditLog from AspNetUsers FK

> **Status:** Draft
> **Story:** [US0118: Decouple AuditLog from AspNetUsers FK](../stories/US0118-decouple-auditlog-from-aspnetusers-fk.md)
> **Epic:** [EP0001: Authentication & Authorization](../epics/EP0001-authentication-authorization.md) (also touches [EP0016: PII / RA 10173 Compliance](../epics/EP0016-pii-ra10173-compliance.md))
> **Created:** 2026-04-27
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8 + SQL Server
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Replace `AuditLog`'s two FK relationships to `AspNetUsers` with denormalized username snapshots. Drop the FK constraints, drop the EF nav properties, add `UserName` / `PerformedByUserName` columns (backfilled from `AspNetUsers` in the same migration), make `AuditService` capture usernames at write time, and add a runtime guard that rejects malformed user-id strings before they reach the database. Update four read sites to project from the snapshot columns instead of joining `AspNetUsers`.

The change is mechanical but spans the schema, the service layer, and four read sites. No user-visible behaviour change; the audit viewer / dashboard / CSV export render identically.

---

## Acceptance Criteria Mapping

| AC (US0118) | Phase |
|-------------|-------|
| AC1: FK constraints removed | Phase 2 — migration |
| AC2: Nav properties removed | Phase 1 — entity + DbContext |
| AC3: Snapshot columns added + backfilled | Phase 2 — migration |
| AC4: AuditService captures snapshot on write | Phase 3 — service-layer change |
| AC5: Runtime guard replaces FK-as-canary | Phase 3 — same file |
| AC6: Read sites use snapshot columns | Phase 4 — four read sites |
| AC7: Filter dropdown remains functional | Phase 4 — viewer page |
| AC8: Existing audit functionality unchanged | Phase 5 — manual verification |
| AC9: Tests pass | Phase 5 — test fixture updates + new guard test |

---

## Technical Context

### Current state (verified)

**Entity** — `src/SmartLog.Web/Data/Entities/AuditLog.cs:9-47`
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; }
    public string? UserId { get; set; }                    // FK target (string Identity Id)
    public string? PerformedByUserId { get; set; }         // FK target (string Identity Id)
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public bool LegalHold { get; set; }
    public ApplicationUser? User { get; set; }              // ← remove
    public ApplicationUser? PerformedByUser { get; set; }   // ← remove
}
```

**DbContext config** — `src/SmartLog.Web/Data/ApplicationDbContext.cs:69-85`
```csharp
builder.Entity<AuditLog>(entity =>
{
    entity.HasIndex(e => e.Timestamp);
    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.Action);
    entity.Property(e => e.LegalHold).HasDefaultValue(false);

    entity.HasOne(e => e.User)                              // ← remove block
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.NoAction);

    entity.HasOne(e => e.PerformedByUser)                   // ← remove block
        .WithMany()
        .HasForeignKey(e => e.PerformedByUserId)
        .OnDelete(DeleteBehavior.NoAction);
});
```

**Confirmed FK constraint names** (from `20260402110107_InitialCreate_GuidIds.cs:269,274`):
- `FK_AuditLogs_AspNetUsers_PerformedByUserId`
- `FK_AuditLogs_AspNetUsers_UserId`

**Identity Id type:** `ApplicationUser : IdentityUser` (no generic) → default `string` Id, GUID-formatted at creation. `Guid.TryParse` is the correct validation primitive.

### Read sites (verified via grep)

| File | Lines | Current pattern |
|------|-------|-----------------|
| `Pages/Admin/AuditLogs.cshtml.cs` | 62-63, 100-102 | `.Include(a => a.User).Include(a => a.PerformedByUser)`, projects `a.User.UserName` / `a.PerformedByUser.UserName` |
| `Services/DashboardService.cs` | 216-223 | Same pattern, projects `PerformedByUser.UserName!` with `"System"` fallback |
| `Services/ReportExportService.cs` | 261-301 | Same pattern, exports both names |
| `Pages/Admin/Sms/Index.cshtml.cs` | 73-77 | **Does not use nav props** — only reads `Action`, `Timestamp`, `Details`. **No change required.** |

### Write sites (verified)

| File | Lines | Notes |
|------|-------|-------|
| `Services/AuditService.cs` | 28-39 | Single central writer — modify here |
| `Services/NoScanAlertService.cs` | 230, 258, 399 | Constructs `AuditLog` directly with null `PerformedByUserId` (system action). Will be unaffected by guard since null is allowed. After AC2, must NOT set `User`/`PerformedByUser` nav props (none currently set). |
| `Pages/Admin/AuditLogs.cshtml.cs` | 126-131, 165-171 | LegalHold toggle + bulk action — constructs `AuditLog` directly with real Identity ids from `_userManager.GetUserId(User)`. Safe. |

The system rows in `NoScanAlertService` and the legal-hold actions both construct `AuditLog` objects directly without going through `IAuditService.LogAsync`. **The guard in AC5 only protects the central service path.** This is acceptable scope — those direct-construction sites use trusted system constants or `UserManager.GetUserId(User)` whose contract is "real Identity Id or null". Documented as a limitation; future hardening could route everything through `LogAsync`.

### Filter dropdown (verified)

`AuditLogs.cshtml.cs:177-199` — `LoadFiltersAsync` already does a two-step:
1. `SELECT DISTINCT PerformedByUserId FROM AuditLogs WHERE PerformedByUserId IS NOT NULL`
2. `SELECT Id, UserName FROM AspNetUsers WHERE Id IN (...)`

After this story, step 2 is gone — we pull `(PerformedByUserId, PerformedByUserName)` directly from `AuditLogs` in step 1. Output format `"$"{u.UserName}|{u.Id}"` is preserved so the existing dropdown markup works unchanged.

---

## Implementation Phases

### Phase 1 — Entity + DbContext (compile-breaking change first)

**Files:**
- `src/SmartLog.Web/Data/Entities/AuditLog.cs`
- `src/SmartLog.Web/Data/ApplicationDbContext.cs`

**Edits:**

1. `AuditLog.cs` — remove `User` and `PerformedByUser` nav props (lines 44-46). Add:
   ```csharp
   [StringLength(256)]
   public string? UserName { get; set; }

   [StringLength(256)]
   public string? PerformedByUserName { get; set; }
   ```
   (`[StringLength(256)]` matches `IdentityUser.UserName` length used elsewhere; `using System.ComponentModel.DataAnnotations;` already present at line 1.)

2. `ApplicationDbContext.cs:76-84` — delete both `entity.HasOne(...).WithMany().HasForeignKey(...).OnDelete(...);` blocks. Keep the three existing `HasIndex` calls and the `LegalHold` default. **Add a new `HasIndex(e => e.PerformedByUserId)`** so the model matches the index that Phase 2 creates (otherwise the next `migrations add` will see it as drift and drop it). The block becomes:
   ```csharp
   builder.Entity<AuditLog>(entity =>
   {
       entity.HasIndex(e => e.Timestamp);
       entity.HasIndex(e => e.UserId);
       entity.HasIndex(e => e.PerformedByUserId);   // new
       entity.HasIndex(e => e.Action);
       entity.Property(e => e.LegalHold).HasDefaultValue(false);
   });
   ```

**Expected outcome:** Build fails. Compiler errors at every `.Include(a => a.User)` / `.Include(a => a.PerformedByUser)` site and every fixture that sets those nav props. This is the desired behaviour — it's our consumer audit. List the errors before moving on; they should match exactly the four read sites in AC6 plus any test fixtures.

### Phase 2 — EF Migration

**Command:**
```bash
dotnet ef migrations add DecoupleAuditLogFromAspNetUsers -p src/SmartLog.Web
```

EF will generate scaffolding from the model diff (FK drops + new columns). **Hand-edit the generated `Up()` method** to add the backfill SQL between the column adds and any future steps:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropForeignKey(
        name: "FK_AuditLogs_AspNetUsers_PerformedByUserId",
        table: "AuditLogs");

    migrationBuilder.DropForeignKey(
        name: "FK_AuditLogs_AspNetUsers_UserId",
        table: "AuditLogs");

    migrationBuilder.AddColumn<string>(
        name: "UserName",
        table: "AuditLogs",
        type: "nvarchar(256)",
        maxLength: 256,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "PerformedByUserName",
        table: "AuditLogs",
        type: "nvarchar(256)",
        maxLength: 256,
        nullable: true);

    // CreateIndex IX_AuditLogs_PerformedByUserId — auto-generated by EF from the
    // HasIndex added in Phase 1. Supports backfill JOIN below and Phase 4a filter dropdown.
    migrationBuilder.CreateIndex(
        name: "IX_AuditLogs_PerformedByUserId",
        table: "AuditLogs",
        column: "PerformedByUserId");

    // Backfill: existing audit rows get user names from current AspNetUsers.
    // Rows whose referenced user no longer exists are left NULL — viewer falls back to id.
    migrationBuilder.Sql(@"
        UPDATE a SET a.UserName = u.UserName
        FROM AuditLogs a
        INNER JOIN AspNetUsers u ON a.UserId = u.Id
        WHERE a.UserName IS NULL;
    ");

    migrationBuilder.Sql(@"
        UPDATE a SET a.PerformedByUserName = u.UserName
        FROM AuditLogs a
        INNER JOIN AspNetUsers u ON a.PerformedByUserId = u.Id
        WHERE a.PerformedByUserName IS NULL;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex(name: "IX_AuditLogs_PerformedByUserId", table: "AuditLogs");
    migrationBuilder.DropColumn(name: "PerformedByUserName", table: "AuditLogs");
    migrationBuilder.DropColumn(name: "UserName", table: "AuditLogs");

    migrationBuilder.AddForeignKey(
        name: "FK_AuditLogs_AspNetUsers_UserId",
        table: "AuditLogs",
        column: "UserId",
        principalTable: "AspNetUsers",
        principalColumn: "Id",
        onDelete: ReferentialAction.NoAction);

    migrationBuilder.AddForeignKey(
        name: "FK_AuditLogs_AspNetUsers_PerformedByUserId",
        table: "AuditLogs",
        column: "PerformedByUserId",
        principalTable: "AspNetUsers",
        principalColumn: "Id",
        onDelete: ReferentialAction.NoAction);
}
```

**Down() caveat:** Re-adding the FK on `Down()` will fail if any audit row currently references a deleted user. Since this migration is the moment we *stop caring* about that integrity, a clean `Down()` cannot fully restore the old state if data has drifted. Acceptable trade for forward-only deployments; flagged in the migration's XML doc comment.

**Verification:**
```bash
dotnet ef migrations script <previous_migration> DecoupleAuditLogFromAspNetUsers -p src/SmartLog.Web
```
Inspect the generated SQL before applying.

### Phase 3 — AuditService snapshot + guard

**File:** `src/SmartLog.Web/Services/AuditService.cs`

**Changes:**

1. Add `using Microsoft.AspNetCore.Identity;` at the top of the file (not currently imported). Inject `UserManager<ApplicationUser>`:
   ```csharp
   private readonly UserManager<ApplicationUser> _userManager;

   public AuditService(
       ApplicationDbContext context,
       ILogger<AuditService> logger,
       UserManager<ApplicationUser> userManager)
   {
       _context = context;
       _logger = logger;
       _userManager = userManager;
   }
   ```

2. Replace the body of `LogAsync` (lines 20-45):
   ```csharp
   public async Task LogAsync(
       string action,
       string? userId = null,
       string? performedByUserId = null,
       string? details = null,
       string? ipAddress = null,
       string? userAgent = null)
   {
       ValidateIdShape(userId, nameof(userId));
       ValidateIdShape(performedByUserId, nameof(performedByUserId));

       var userName = userId is null ? null : await ResolveUserNameAsync(userId);
       var performedByUserName = performedByUserId is null ? null : await ResolveUserNameAsync(performedByUserId);

       var auditLog = new AuditLog
       {
           Action = action,
           UserId = userId,
           UserName = userName,
           PerformedByUserId = performedByUserId,
           PerformedByUserName = performedByUserName,
           Details = details,
           IpAddress = ipAddress,
           UserAgent = userAgent,
           Timestamp = DateTime.UtcNow
       };

       _context.AuditLogs.Add(auditLog);
       await _context.SaveChangesAsync();

       _logger.LogInformation(
           "Audit: {Action} - UserId: {UserId}, PerformedBy: {PerformedBy}, Details: {Details}",
           action, userId, performedByUserId, details);
   }

   private static void ValidateIdShape(string? id, string paramName)
   {
       if (id is null) return;
       if (!Guid.TryParse(id, out _))
           throw new ArgumentException(
               $"Audit user-id must be a GUID-formatted Identity Id; received: '{id}'",
               paramName);
   }

   private async Task<string?> ResolveUserNameAsync(string id)
   {
       var user = await _userManager.FindByIdAsync(id);
       return user?.UserName;
   }
   ```

**DI:** `UserManager<ApplicationUser>` is already registered by `AddDefaultIdentity`. No `Program.cs` change.

**Why GUID format:** `IdentityUser` default Ids are `Guid.NewGuid().ToString()` — exactly 36 chars with hyphens. `Guid.TryParse` rejects free-text sentences (BG0007 root cause), empty strings, ints, and any other malformed value. The validation is local to `AuditService.LogAsync` so the central service path is hardened without touching direct-construction call sites.

### Phase 4 — Read site updates

#### 4a — `Pages/Admin/AuditLogs.cshtml.cs`

**Lines 60-107 (`OnGetAsync`):**
- Drop `.Include(a => a.User)` and `.Include(a => a.PerformedByUser)` (lines 62-63)
- In the projection (lines 94-107), change:
  - `UserName = a.User != null ? a.User.UserName : null` → `UserName = a.UserName`
  - `PerformedByUserName = a.PerformedByUser != null ? a.PerformedByUser.UserName : "System"` → `PerformedByUserName = a.PerformedByUserName ?? "System"`

**Lines 177-199 (`LoadFiltersAsync`):**

Replace the two-step lookup with a single grouped query:
```csharp
var performers = await _context.AuditLogs
    .Where(a => a.PerformedByUserId != null)
    .GroupBy(a => new { a.PerformedByUserId, a.PerformedByUserName })
    .Select(g => new { g.Key.PerformedByUserId, g.Key.PerformedByUserName })
    .OrderBy(x => x.PerformedByUserName)
    .ToListAsync();

Users = performers
    .Select(u => $"{u.PerformedByUserName ?? "(unknown)"}|{u.PerformedByUserId}")
    .ToList();
```

The `_userManager` injection in `AuditLogsModel` ctor stays — `OnPostToggleLegalHoldAsync` and `OnPostBulkLegalHoldAsync` still call `_userManager.GetUserId(User)` for the *current* user.

**Post-rename behaviour note:** Grouping by `(PerformedByUserId, PerformedByUserName)` means a user who is renamed *after* this story ships will appear as multiple dropdown entries — one per historical username — because audit rows accumulate snapshots over time. **Accepted for the current pre-deployment state** (no production audit history exists yet). If post-deployment we want one entry per id with the most-recent name, change the GroupBy to id-only and project the latest `PerformedByUserName` per group via `OrderByDescending(a => a.Timestamp).First()`.

#### 4b — `Services/DashboardService.cs`

**Lines 216-223:** Drop `.Include(a => a.PerformedByUser)`. Change projection `UserName = a.PerformedByUser != null ? a.PerformedByUser.UserName! : "System"` → `UserName = a.PerformedByUserName ?? "System"`.

#### 4c — `Services/ReportExportService.cs`

**Lines 261-301:** Drop both `.Include` calls. In the CSV row formatter (line 301), change:
- `log.User?.UserName ?? "-"` → `log.UserName ?? "-"`
- `log.PerformedByUser?.UserName ?? "System"` → `log.PerformedByUserName ?? "System"`

#### 4d — `Pages/Admin/Sms/Index.cshtml.cs` — **no change**

Verified during planning: lines 73-77 read only `Action`, `Timestamp`, `Details` from the audit row and pass it to `NoScanAlertRunStatus.FromAuditLog` which also touches only those scalar fields. No `.Include`, no nav prop access. **Confirm during implementation by re-running the build after Phase 1 — if it compiles, this site is unaffected.**

### Phase 5 — Tests + manual verification

#### 5a — Existing tests

`tests/SmartLog.Web.Tests/Services/Retention/AuditLogRetentionHandlerTests.cs` — quick scan during planning showed it constructs `AuditLog` rows with `Timestamp`, `Action`, `LegalHold` only. **Expected to compile and pass without changes.** Run the test project after Phase 1 to confirm; if any fixture sets `User`/`PerformedByUser`, replace with `UserName` / `PerformedByUserName` strings.

#### 5b — New unit test for AC5 guard

Add `tests/SmartLog.Web.Tests/Services/AuditServiceTests.cs` (new file) with three cases:

```csharp
[Fact]
public async Task LogAsync_RejectsMalformedPerformedByUserId()
{
    var sut = BuildAuditService();
    var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
        sut.LogAsync(
            action: "TestAction",
            performedByUserId: "this is a sentence, not an id"));
    Assert.Equal("performedByUserId", ex.ParamName);
}

[Fact]
public async Task LogAsync_AcceptsNullPerformedByUserId()
{
    var sut = BuildAuditService();
    await sut.LogAsync(action: "SystemAction", performedByUserId: null);
    // assert row written with PerformedByUserName = null
}

[Fact]
public async Task LogAsync_SnapshotsUserNameAtWriteTime()
{
    var aliceId = Guid.NewGuid().ToString();           // must be GUID-shaped to pass the AC5 guard
    var sut = BuildAuditService(seedUser: (aliceId, "alice"));
    await sut.LogAsync(action: "Test", performedByUserId: aliceId);
    // assert PerformedByUserName == "alice"
}
```

The `BuildAuditService` helper wires up an in-memory `ApplicationDbContext` and a stub/real `UserManager<ApplicationUser>`. Pattern can be borrowed from existing service tests in `tests/SmartLog.Web.Tests/Services/`.

#### 5c — Manual verification checklist

Run against `dotnet run` on `http://localhost:5050` with seeded data:

1. **Audit viewer** — `/Admin/AuditLogs` loads; rows render `User` and `PerformedBy` columns identically to before. Filter dropdowns populate. Date range / search / user-filter all return correct rows.
2. **Recent activity panel** — Dashboard shows the last 10 auth events with the same usernames as before.
3. **CSV export** — `/Admin/AuditLogs` → Export. Open the CSV; `User` and `PerformedBy` columns populated.
4. **Legal hold per-row** — Toggle a row; confirm row shows the lock icon, secondary `AuditLegalHoldSet` row appears with current admin's username in `PerformedByUserName`.
5. **Bulk legal hold** — Filter to a small set, click Apply Legal Hold. Confirm summary row written with admin's username.
6. **Retention** — `/Admin/Settings/Retention`. AuditLog row shows held count. Trigger a DryRun; numbers consistent with held-row exclusion.
7. **BG0007 regression check** — Direct test (curl or browser): hit `POST /api/v1/profile-picture/student/{id}` (with the PL0037 fix already in place). Expect 200, audit row written with snapshot username. The AC5 guard's regression coverage lives in the Phase 5b unit tests, not in this checklist — no need to deliberately break a call site here.
8. **Server log** — Confirm no `DbUpdateException`, no FK violation entries.
9. **No-Scan Alert** — `/Admin/Sms` panel still renders the last-run status from audit log scalar fields.

---

## Risks & Considerations

- **Risk: Backfill UPDATE locks the AuditLogs table.** SmartLog audit volume is modest (single-school deployment, few thousand rows). The two backfill statements are simple JOIN-UPDATEs with no batching — acceptable for the deployment scale. If a larger deployment surfaces, batch the UPDATE with a `TOP (10000)` loop. Flagged but not implemented — YAGNI for current scale.
- **Risk: User renamed after migration.** Backfill captures the *current* `AspNetUsers.UserName`. If a username is later changed, historical rows show the old name (which is exactly the snapshot semantics we want — no action needed).
- **Risk: Username conflict with `Details` regex parsers.** `NoScanAlertRunStatus.FromAuditLog` parses regex out of `Details` only; not affected.
- **Risk: Guard breaks legitimate non-GUID Identity Ids.** Verified: `ApplicationUser : IdentityUser` (no generic) → string Ids are GUID-formatted. If a future migration switches to int / custom Ids, update `ValidateIdShape`. Documented as a coupling.
- **Risk: Direct-construction audit writes (NoScanAlertService, legal-hold actions) bypass the guard.** Acceptable scope: those sites use trusted system constants or `UserManager.GetUserId(User)` whose contract guarantees a valid Id or null. Future story can route them through `IAuditService` if desired.
- **Risk: Down migration cannot fully restore FK if any audit row references a deleted user post-cutover.** Acknowledged in the migration's XML doc comment. Forward-only deployment — Down() exists for symmetry, not production rollback.
- **Risk: Existing audit rows with NULL UserId but populated Details.** Backfill leaves `UserName` NULL — correct behaviour, viewer falls back to id rendering as before.

---

## Out of Scope

- Refactoring `IAuditService.LogAsync` to a typed request object (separate hardening story; would prevent positional-arg confusion at compile time)
- Routing direct-construction audit writes through `IAuditService` to extend guard coverage (separate story)
- Username caching / batched lookups (premature; SmartLog scale doesn't justify it)
- Auditing `Faculty.UserId`, `Device.RegisteredBy`, `QrCode.ReplacedByQrCodeId` for similar coupling — those aren't append-only event tables, FK is appropriate
- Full RA 10173 hold-with-cases workflow — deferred to EP0016
- UI changes to the audit viewer — visuals unchanged

---

## Estimated Effort

- Phase 1 (entity + DbContext): ~10 min
- Phase 2 (migration + backfill SQL hand-edit): ~25 min
- Phase 3 (AuditService): ~20 min
- Phase 4 (4 read-site updates): ~25 min
- Phase 5a (existing test verification): ~10 min
- Phase 5b (new guard tests): ~30 min
- Phase 5c (manual verification, 10 items): ~30 min
- **Total:** ~2.5 hours

---

## Rollout Plan

1. Implement Phase 1 → confirm build break list matches AC6's four files (minus `Sms/Index.cshtml.cs` which should not appear)
2. Implement Phases 2–4 in order; run `dotnet build` after each
3. Run `dotnet test` after Phase 4
4. Apply migration locally: `dotnet ef database update -p src/SmartLog.Web`
5. Manual verification (Phase 5c)
6. Commit on dev branch (per project convention — confirm with user before pushing)
7. PR to main (per project convention — never direct-push to main)

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude (Opus 4.7) | Initial plan drafted |
| 2026-04-27 | Claude (Opus 4.7) | Review fixes: corrected AuditService ctor signature; fixed test seed to use real GUID; added `using Microsoft.AspNetCore.Identity;` note; dropped fragile manual-verify step 8 (covered by unit test); added `HasIndex(e => e.PerformedByUserId)` in Phase 1 + matching `IX_AuditLogs_PerformedByUserId` in migration; documented filter-dropdown post-rename behaviour as accepted pre-deployment |
