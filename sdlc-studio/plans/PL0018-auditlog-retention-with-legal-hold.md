# PL0018: AuditLog Retention with Legal-Hold Flag — Implementation Plan

> **Status:** Draft
> **Story:** [US0099: AuditLog Retention with Legal-Hold Flag](../stories/US0099-auditlog-retention-with-legal-hold.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0

## Overview

This is the most complex retention handler because it introduces a new `LegalHold` boolean column on `AuditLog`, extends the AuditLog viewer with per-row toggle + bulk action, and implements `AuditLogRetentionHandler` that honours the hold flag. The handler also defaults to `ArchiveEnabled = true` in the seed data (already defined in PL0013 — verify and cross-reference).

The implementation has four distinct parts:
1. Schema: `AuditLog.LegalHold` column + migration
2. Admin UI: per-row toggle + bulk-hold action on the existing AuditLog viewer
3. Handler: purge with `LegalHold = false` filter, archive hook (wired, not stubbed — archive strongly recommended)
4. Retention page: held-count display for the AuditLog row

**Pre-existing state:**
- `AuditLog` entity at `Data/Entities/AuditLog.cs` with existing fields.
- AuditLog Viewer page at `Pages/Admin/AuditLog/Index.cshtml(.cs)` (US0050 — Done).
- `IEntityRetentionHandler` + base types defined (PL0014).
- `AuditLog.ArchiveEnabled = true` seeded in PL0013.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | LegalHold column | `AuditLog.LegalHold` (bool, default false); migration backfills to false |
| AC2 | Handler registered | `EntityName = "AuditLog"` |
| AC3 | Eligibility honours hold | `CreatedAt < cutoff AND LegalHold = false`; held rows never deleted |
| AC4 | Per-row toggle UI | Toggle on AuditLog viewer; writes secondary audit entry |
| AC5 | Bulk hold action | "Apply Legal Hold to Filtered Set" button; writes single summary audit entry |
| AC6 | Standard batch + dry run + archive + logging | Archive not stubbed for AuditLog (strongly paired with US0102) |
| AC7 | Held count on Retention page | Shows "On legal hold: N rows" on AuditLog retention row |
| AC8 | Archive default | Seed sets `ArchiveEnabled = true` for AuditLog — confirmed in PL0013 |

---

## Technical Context

### AuditLog Entity Shape
Check `Data/Entities/AuditLog.cs` for existing fields. Expected: `Id`, `Action`, `UserId`, `PerformedByUserId`, `IpAddress`, `UserAgent`, `Details`, `CreatedAt`. Add: `LegalHold` (bool).

### Secondary Audit Entry on Toggle
When a user toggles `LegalHold` on a row, write a new `AuditLog` entry:
```csharp
new AuditLog {
    Action = holdValue ? "AuditLegalHoldSet" : "AuditLegalHoldCleared",
    UserId = targetRow.UserId,
    PerformedByUserId = currentUserId,
    Details = $"AuditLogId: {targetRowId}",
    CreatedAt = DateTime.UtcNow
}
```
This secondary entry itself is a standard audit row (not automatically held; admin can hold it too).

### Bulk Hold Filter
The AuditLog viewer uses querystring filters (date range, action type, user). "Apply Legal Hold to Filtered Set" batches `UPDATE AuditLog SET LegalHold = 1 WHERE <filter predicates>` in chunks of 1,000, then writes a single summary audit entry with the filter parameters.

### Archive Hook for AuditLog
Unlike other handlers, AuditLog archive is not a stub — the retention page defaults `ArchiveEnabled = true`. When US0102 ships, the `IArchiveService` will be registered. Until then, use the same nullable-inject stub from PL0015 — fail safely if not registered.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Multiple moving parts (schema, UI, handler, admin audit entry) — each piece is testable after implementation.

---

## Implementation Phases

### Phase 1: LegalHold Column + Migration

**Goal:** Add `LegalHold` to `AuditLog` entity; migrate with default false.

- [ ] In `Data/Entities/AuditLog.cs`, add:
  ```csharp
  public bool LegalHold { get; set; } = false;
  ```
- [ ] In `ApplicationDbContext.OnModelCreating`, add:
  ```csharp
  modelBuilder.Entity<AuditLog>()
      .Property(a => a.LegalHold).HasDefaultValue(false);
  ```
- [ ] Create migration: `dotnet ef migrations add AddLegalHoldToAuditLog -p src/SmartLog.Web`
- [ ] Verify `Up()` adds nullable-false column with default `0`; `Down()` drops it.

**Files:** `Data/Entities/AuditLog.cs`, `Data/ApplicationDbContext.cs`, migration file

### Phase 2: Per-Row Toggle on AuditLog Viewer

**Goal:** Each row in the AuditLog viewer has a toggle button (Admin-only).

- [ ] In `Pages/Admin/AuditLog/Index.cshtml.cs`:
  - Add `OnPostToggleLegalHoldAsync(long id)` handler.
  - Load row; toggle `LegalHold`; write secondary audit entry; save.
  - Return `RedirectToPage` (PRG pattern).
  - Authorize: only Admin/SuperAdmin can call this.
- [ ] In `Pages/Admin/AuditLog/Index.cshtml`:
  - Add a "Hold" / "Release" button per row (shown only to Admin+).
  - Form with `asp-page-handler="ToggleLegalHold"` + `asp-route-id="@row.Id"`.
  - Visually distinguish held rows (e.g. badge or row highlight).

**Files:** `Pages/Admin/AuditLog/Index.cshtml(.cs)`

### Phase 3: Bulk Hold Action

**Goal:** "Apply Legal Hold to Filtered Set" button applies hold to all rows matching current filters.

- [ ] In `Index.cshtml.cs`, add `OnPostBulkLegalHoldAsync()` handler.
  - Re-run the filter query (same predicates as `OnGetAsync`) without pagination.
  - Batch-update `LegalHold = true` in 1,000-row chunks:
    ```csharp
    await _db.AuditLogs
        .Where(filteredQuery)
        .ExecuteUpdateAsync(s => s.SetProperty(a => a.LegalHold, true), ct);
    ```
    > If `ExecuteUpdateAsync` is available (EF Core 7+), use it; otherwise raw SQL update.
  - Write single summary audit entry:
    ```csharp
    new AuditLog {
        Action = "AuditBulkLegalHoldApplied",
        PerformedByUserId = currentUserId,
        Details = $"Filters: {JsonSerializer.Serialize(filterParams)}, RowsAffected: {count}",
        CreatedAt = DateTime.UtcNow
    }
    ```
- [ ] Add "Apply Legal Hold to Filtered Set" button with a confirmation dialog (`onclick="return confirm(...)"`) in `Index.cshtml`.
- [ ] Authorize: Admin+.

**Files:** `Pages/Admin/AuditLog/Index.cshtml(.cs)`

### Phase 4: AuditLogRetentionHandler

**Goal:** Handler that honours `LegalHold = false` in the eligibility filter.

- [ ] Create `src/SmartLog.Web/Services/Retention/AuditLogRetentionHandler.cs`.
- [ ] `EntityName => "AuditLog"`.
- [ ] Batch SQL:
  ```sql
  DELETE TOP (1000) FROM AuditLogs
  WHERE CreatedAt < {cutoff}
    AND LegalHold = 0
  ```
- [ ] Archive hook: same nullable-inject stub as PL0015 (fail safely if not registered). Because `ArchiveEnabled = true` by default, the Retention page will show a "waiting for archive service" state until US0102 ships. This is acceptable and documented in the Retention page tooltip.
- [ ] `PreviewAsync`: count eligible rows (`LegalHold = false AND CreatedAt < cutoff`).
- [ ] All other contracts same as prior handlers.

**Files:** `Services/Retention/AuditLogRetentionHandler.cs`

### Phase 5: Held Count on Retention Page

**Goal:** Retention page shows count of held AuditLog rows.

- [ ] In `Retention.cshtml.cs` `OnGetAsync`, for the AuditLog row, load:
  ```csharp
  var heldCount = await _db.AuditLogs.CountAsync(a => a.LegalHold);
  // Attach to the AuditLog RetentionPolicyViewModel
  ```
- [ ] In `Retention.cshtml`, for the AuditLog row, show: `"On legal hold: {heldCount} rows"` as an informational badge.

**Files:** `Pages/Admin/Settings/Retention.cshtml(.cs)`

### Phase 6: DI Registration

- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddScoped<IEntityRetentionHandler, AuditLogRetentionHandler>();
  ```

### Phase 7: Tests

| AC | Test | File |
|----|------|------|
| AC1 | Migration creates column; default = false | via `dotnet ef database update` in CI |
| AC3 | Handler skips rows where `LegalHold = true` | `AuditLogRetentionHandlerTests.cs` |
| AC3 | Handler deletes old rows where `LegalHold = false` | same |
| AC4 | Toggle endpoint flips LegalHold; writes secondary audit row | `AuditLogPageTests.cs` |
| AC4 | Non-admin cannot toggle | same |
| AC5 | Bulk hold applies to filtered set; writes summary audit | same |
| AC7 | Held count shown on Retention page | `RetentionPageTests.cs` |

- [ ] Run `dotnet test`; zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Secondary audit entry for toggle — can it be held? | Yes — it's a normal audit row; admin can hold it manually |
| 2 | Bulk hold on very large filter (millions of rows) | Batched `ExecuteUpdateAsync` in chunks; single summary audit entry with affected count |
| 3 | Admin toggles hold off then handler runs same day | Handler will purge the released row on the next scheduled run (daily guard resets) |
| 4 | Archive service absent + ArchiveEnabled = true | Fail-safe: log warning, do not delete; run logged as Partial; admin sees status on Retention page |

---

## Definition of Done

- [ ] `AuditLog.LegalHold` column + migration added
- [ ] Per-row toggle + bulk hold action on AuditLog viewer (Admin-only)
- [ ] Secondary audit entries written on hold toggle
- [ ] `AuditLogRetentionHandler` with `LegalHold = false` filter
- [ ] Held count displayed on Retention page
- [ ] Registered in DI
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
