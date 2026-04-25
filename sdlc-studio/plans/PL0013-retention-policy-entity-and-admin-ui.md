# PL0013: Retention Policy Entity & Admin UI — Implementation Plan

> **Status:** Complete
> **Story:** [US0094: Retention Policy Entity & Admin UI](../stories/US0094-retention-policy-entity-and-admin-ui.md)
> **Epic:** EP0017: Data Retention & Archival
> **Created:** 2026-04-24
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Lay the data foundation for EP0017: two new entities (`RetentionPolicy` and `RetentionRun`), a migration, idempotent seed data for six entity policies, and the Admin UI page at `/Admin/Settings/Retention` where admins can view and edit retention windows. Subsequent stories (US0095–US0101) depend on this foundation.

**Pre-existing state:**
- `DbInitializer.cs` already seeds reference data (roles, default SMS settings) — extend there.
- `AuditLog` entity + `IAuditLogService` already exist — use for change tracking.
- `ApplicationDbContext` follows standard `OnModelCreating` fluent config pattern.
- Admin Settings nav is in the shared `_AdminLayout.cshtml` or a nav partial — add "Retention" link there.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | RetentionPolicy entity | Table with all listed columns, `EntityName` unique index |
| AC2 | RetentionRun entity | Table with all listed columns |
| AC3 | Seed defaults | Six rows, idempotent (insert-if-missing only) |
| AC4 | Admin UI | Retention page with table, editable fields, action buttons (Run/DryRun disabled) |
| AC5 | Authorization | Page requires RequireAdmin policy |
| AC6 | Min-floor validation | Per-entity floor enforced with descriptive message |
| AC7 | Audit trail | `AuditLog` entry on save; `UpdatedAt`/`UpdatedBy` stamped |
| AC8 | Info link | Header link to inline documentation section on the same page |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
- **Architecture:** Razor Pages + EF Core 8.0 + SQL Server
- **Test Framework:** xUnit + Moq (`tests/SmartLog.Web.Tests`)

### Key Existing Patterns
- **Seed pattern:** `DbInitializer.SeedAsync()` — called from `Program.cs` at startup. Check-then-insert for each row. Extend with a new `SeedRetentionPoliciesAsync` private method.
- **Admin settings page pattern:** See `/Admin/Settings/Sms/` pages — `BindProperty` for each setting, `OnPostAsync` validates + saves + writes AuditLog.
- **AuditLog pattern:** `_db.AuditLogs.Add(new AuditLog { Action = "...", Details = "...", UserId = ..., PerformedByUserId = ..., IpAddress = ... })` — check an existing settings-save handler for exact call-site shape.
- **EF Core config pattern:** `OnModelCreating` uses `modelBuilder.Entity<T>().Property(...)` fluent config; `HasMaxLength`, `HasDefaultValue`, unique index via `HasIndex(...).IsUnique()`.

### Default Retention Windows (from EP0017)

| EntityName | RetentionDays | ArchiveEnabled |
|------------|--------------|----------------|
| SmsQueue | 90 | false |
| SmsLog | 180 | false |
| Broadcast | 365 | false |
| Scan | 730 | false |
| AuditLog | 1095 | true |
| VisitorScan | 365 | false |

### Minimum Retention Floors (from AC6)

| EntityName | Floor (days) | Rationale |
|------------|-------------|-----------|
| SmsQueue | 7 | Active processing window |
| SmsLog | 30 | Billing dispute window |
| Broadcast | 30 | Admin review window |
| Scan | 365 | Annual enrolment audit |
| AuditLog | 365 | Legal/compliance minimum |
| VisitorScan | 7 | Basic safety incident review |

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Schema + seed + Admin UI; no algorithmic complexity. Tests cover seed idempotency, validation, page rendering, audit log write, and authorization.

---

## Implementation Phases

### Phase 1: Entities

**Goal:** Define `RetentionPolicy` and `RetentionRun` entity classes.

- [ ] Create `src/SmartLog.Web/Data/Entities/RetentionPolicy.cs`:
  ```csharp
  public class RetentionPolicy {
      public int Id { get; set; }
      public string EntityName { get; set; } = null!;
      public int RetentionDays { get; set; }
      public bool Enabled { get; set; } = true;
      public bool ArchiveEnabled { get; set; } = false;
      public DateTime? LastRunAt { get; set; }
      public int? LastRowsAffected { get; set; }
      public DateTime UpdatedAt { get; set; }
      public string? UpdatedBy { get; set; }
  }
  ```
- [ ] Create `src/SmartLog.Web/Data/Entities/RetentionRun.cs`:
  ```csharp
  public class RetentionRun {
      public long Id { get; set; }
      public string EntityName { get; set; } = null!;
      public string RunMode { get; set; } = null!;      // "Scheduled" | "Manual" | "DryRun"
      public DateTime StartedAt { get; set; }
      public DateTime? CompletedAt { get; set; }
      public string Status { get; set; } = null!;       // "Success" | "Failed" | "Partial"
      public int RowsAffected { get; set; }
      public int? DurationMs { get; set; }
      public string? ErrorMessage { get; set; }
      public string? TriggeredBy { get; set; }
  }
  ```

**Files:** `Data/Entities/RetentionPolicy.cs`, `Data/Entities/RetentionRun.cs`

### Phase 2: DbContext Configuration

**Goal:** Register entities and configure field lengths + unique index.

- [ ] In `ApplicationDbContext.cs` add DbSets:
  ```csharp
  public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
  public DbSet<RetentionRun> RetentionRuns => Set<RetentionRun>();
  ```
- [ ] In `OnModelCreating`, add config:
  ```csharp
  modelBuilder.Entity<RetentionPolicy>(e => {
      e.Property(p => p.EntityName).HasMaxLength(50).IsRequired();
      e.HasIndex(p => p.EntityName).IsUnique();
      e.Property(p => p.UpdatedBy).HasMaxLength(256);
  });
  modelBuilder.Entity<RetentionRun>(e => {
      e.Property(r => r.EntityName).HasMaxLength(50).IsRequired();
      e.Property(r => r.RunMode).HasMaxLength(20).IsRequired();
      e.Property(r => r.Status).HasMaxLength(20).IsRequired();
      e.Property(r => r.ErrorMessage).HasMaxLength(4000);
      e.Property(r => r.TriggeredBy).HasMaxLength(256);
  });
  ```

**Files:** `Data/ApplicationDbContext.cs`

### Phase 3: Migration

**Goal:** Create EF Core migration that adds both tables.

- [ ] Run: `dotnet ef migrations add AddRetentionPolicyAndRun -p src/SmartLog.Web`
- [ ] Verify generated `Up()` creates `RetentionPolicies` (with unique index on `EntityName`) and `RetentionRuns`.
- [ ] Verify `Down()` drops both tables cleanly.

**Files:** `Migrations/{ts}_AddRetentionPolicyAndRun.cs`

### Phase 4: Seed Data

**Goal:** Idempotent seed for six default policy rows.

- [ ] In `DbInitializer.cs`, add a private async method `SeedRetentionPoliciesAsync(ApplicationDbContext db)`.
- [ ] For each of the six entities, insert if the `EntityName` row does not yet exist:
  ```csharp
  var defaults = new[] {
      new RetentionPolicy { EntityName = "SmsQueue",    RetentionDays = 90,   ArchiveEnabled = false },
      new RetentionPolicy { EntityName = "SmsLog",      RetentionDays = 180,  ArchiveEnabled = false },
      new RetentionPolicy { EntityName = "Broadcast",   RetentionDays = 365,  ArchiveEnabled = false },
      new RetentionPolicy { EntityName = "Scan",        RetentionDays = 730,  ArchiveEnabled = false },
      new RetentionPolicy { EntityName = "AuditLog",    RetentionDays = 1095, ArchiveEnabled = true  },
      new RetentionPolicy { EntityName = "VisitorScan", RetentionDays = 365,  ArchiveEnabled = false },
  };
  foreach (var def in defaults) {
      if (!await db.RetentionPolicies.AnyAsync(p => p.EntityName == def.EntityName)) {
          def.UpdatedAt = DateTime.UtcNow;
          db.RetentionPolicies.Add(def);
      }
  }
  await db.SaveChangesAsync();
  ```
- [ ] Call `SeedRetentionPoliciesAsync` from `SeedAsync`.

**Files:** `Data/DbInitializer.cs`

### Phase 5: Admin Retention Page

**Goal:** Razor Page at `/Admin/Settings/Retention` — view + edit policies.

- [ ] Create `src/SmartLog.Web/Pages/Admin/Settings/Retention.cshtml` and `Retention.cshtml.cs`.
- [ ] Page model:
  - `[Authorize(Policy = "RequireAdmin")]` on the class.
  - `OnGetAsync`: load all `RetentionPolicy` rows, order by a fixed display order (SmsQueue, SmsLog, Broadcast, Scan, AuditLog, VisitorScan).
  - `OnPostAsync`: accept a list of updated policy view-models; validate min floor per entity; save; write audit log; return Page() with success message.
  - Expose `List<RetentionPolicyViewModel> Policies` as a property with `[BindProperty]`.
- [ ] `RetentionPolicyViewModel` fields: `Id`, `EntityName`, `FriendlyName`, `Description`, `RetentionDays` (input), `Enabled` (checkbox), `ArchiveEnabled` (checkbox), `LastRunAt`, `LastRowsAffected`.
- [ ] Min-floor validation: implement as server-side `ModelState.AddModelError` per row:
  ```csharp
  private static readonly Dictionary<string, int> MinFloors = new() {
      ["SmsQueue"] = 7, ["SmsLog"] = 30, ["Broadcast"] = 30,
      ["Scan"] = 365, ["AuditLog"] = 365, ["VisitorScan"] = 7
  };
  // In OnPostAsync:
  for (int i = 0; i < Policies.Count; i++) {
      var policy = Policies[i];
      if (MinFloors.TryGetValue(policy.EntityName, out var floor) && policy.RetentionDays < floor)
          ModelState.AddModelError($"Policies[{i}].RetentionDays",
              $"{policy.FriendlyName} requires at least {floor} days (regulatory minimum).");
  }
  ```
- [ ] On successful save: update `RetentionPolicy.UpdatedAt = DateTime.UtcNow`, `UpdatedBy = User.Identity!.Name`; write audit log entry per changed row.
- [ ] Buttons: "Save All", "Run Now" (disabled, `title="Available after US0101"`), "Dry Run" (disabled).
- [ ] Add inline documentation section (AC8): a collapsible `<details>` element below the table explaining each entity's default + RA 10173 rationale. No separate page needed.

**Files:** `Pages/Admin/Settings/Retention.cshtml`, `Pages/Admin/Settings/Retention.cshtml.cs`

### Phase 6: Nav Link

**Goal:** "Retention" link appears in Admin settings sidebar.

- [ ] Find the Admin settings nav partial (check `_AdminLayout.cshtml` or `Pages/Admin/Settings/_Layout.cshtml`). Add link:
  ```html
  <a asp-page="/Admin/Settings/Retention" class="nav-link ...">Retention</a>
  ```

**Files:** relevant layout partial

### Phase 7: Tests

| AC | Test | File |
|----|------|------|
| AC3 | Seed creates 6 rows on empty DB | `DbInitializerTests.cs` (new or extend) |
| AC3 | Seed is idempotent (run twice, still 6 rows) | same |
| AC5 | Non-admin user gets 403 | `RetentionPageTests.cs` (new) |
| AC6 | Below-floor RetentionDays returns validation error | same |
| AC7 | Save writes AuditLog entry | same |

- [ ] Run `dotnet test`; confirm zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Seed run when RetentionPolicies table has user-modified values | Insert-if-missing only; existing rows untouched |
| 2 | `ArchiveEnabled = true` before US0102 ships | Field saved; archive handlers stub until US0102; Retention page shows "(archive coming soon)" tooltip |
| 3 | Future entity added (migration adds a 7th row) | Seed method extended; existing rows unaffected |
| 4 | Admin saves without changing anything | All validation passes; `UpdatedAt` stamped; audit log still written (idempotent from DB perspective) |

---

## Definition of Done

- [ ] `RetentionPolicy` and `RetentionRun` entities created with correct field types
- [ ] Migration `AddRetentionPolicyAndRun` runs cleanly; `Down()` reverses
- [ ] Six default rows seeded on first run; re-run is idempotent
- [ ] `/Admin/Settings/Retention` renders all six policy rows
- [ ] Save validates min-floor, stamps `UpdatedAt`/`UpdatedBy`, writes AuditLog
- [ ] Page inaccessible to non-Admin users
- [ ] Nav link added
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial plan |
