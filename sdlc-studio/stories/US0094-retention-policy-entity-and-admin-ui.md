# US0094: Retention Policy Entity & Admin UI

> **Status:** Done
> **Epic:** [EP0017: Data Retention & Archival](../epics/EP0017-data-retention-archival.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Admin Amy (Administrator)
**I want** a single Admin page where I can view and edit the retention window for each regulated entity (SmsQueue, SmsLog, Broadcast, Scan, AuditLog, VisitorScan)
**So that** I can tune the retention strategy to our school's compliance needs without editing code or config files.

## Context

### Persona Reference
**Admin Amy** — Sets policy.
**Tony (IT Admin)** — Monitors DB growth; validates new policies.

### Background
EP0017 introduces per-entity retention. This story lays the foundation: the data model (`RetentionPolicy` + `RetentionRun`) and the single Admin UI to view + edit policies. Subsequent stories (US0095-US0100) add the per-entity handlers; US0101 wires the scheduler; US0102 adds archive-to-file.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0017 | Legal | RA 10173 — storage-limitation principle | Defaults must be justified; minimum floor enforced |
| EP0017 | Operational | One policy per table; no per-row overrides | Policy is keyed by `EntityName` string |
| TRD | Security | Only SuperAdmin / Admin should edit retention | Page secured by `CanManageUsers` or equivalent policy |

---

## Acceptance Criteria

### AC1: RetentionPolicy Entity
- **Given** the database model
- **Then** a new `RetentionPolicy` table exists with columns:
  - `Id` (PK, int)
  - `EntityName` (nvarchar(50), unique) — values: `SmsQueue`, `SmsLog`, `Broadcast`, `Scan`, `AuditLog`, `VisitorScan`
  - `RetentionDays` (int, > 0)
  - `Enabled` (bool, default true)
  - `ArchiveEnabled` (bool, default false)
  - `LastRunAt` (datetime2?, nullable)
  - `LastRowsAffected` (int?, nullable)
  - `UpdatedAt` (datetime2)
  - `UpdatedBy` (nvarchar(256)?)

### AC2: RetentionRun Entity
- **Given** the database model
- **Then** a new `RetentionRun` table exists with columns:
  - `Id` (PK, bigint)
  - `EntityName` (nvarchar(50))
  - `RunMode` (nvarchar(20)) — values: `Scheduled`, `Manual`, `DryRun`
  - `StartedAt` (datetime2)
  - `CompletedAt` (datetime2?)
  - `Status` (nvarchar(20)) — values: `Success`, `Failed`, `Partial`
  - `RowsAffected` (int)
  - `DurationMs` (int?)
  - `ErrorMessage` (nvarchar(max)?)
  - `TriggeredBy` (nvarchar(256)?)

### AC3: Seed Defaults on First Run
- **Given** a fresh database
- **When** `DbInitializer` runs
- **Then** six `RetentionPolicy` rows are seeded (one per entity) with the defaults from EP0017's Default Retention Windows table
- **And** seeding is idempotent

### AC4: Admin UI — Retention Page
- **Given** I navigate to `/Admin/Settings/Retention`
- **Then** I see a table with one row per entity showing:
  - Entity name (friendly label + brief description)
  - Retention days (editable number input, min validation per AC6)
  - Enabled toggle
  - Archive-to-file toggle
  - Last run (datetime or "—")
  - Last rows affected (number or "—")
  - Action buttons (Save, Run Now, Dry Run) — Run Now + Dry Run disabled until US0101 ships

### AC5: Page Authorization
- **Given** a user with role other than SuperAdmin or Admin
- **When** they try to access the page
- **Then** they are denied (403 or redirect)

### AC6: Minimum Retention Validation
- **Given** I enter a value below the documented floor per entity (e.g. 7 days for SmsQueue, 30 for SmsLog, 365 for AuditLog/Scan)
- **Then** the form blocks submission with a clear validation message explaining the floor and its rationale

### AC7: Audit Trail on Change
- **Given** I save a change to a retention policy
- **Then** an `AuditLog` entry is written (`Action = "RetentionPolicyUpdated"`, details: entity, old value, new value)
- **And** `RetentionPolicy.UpdatedAt` + `UpdatedBy` reflect the change

### AC8: Retention Policy Info Link
- **Given** the Retention page
- **Then** a header link "View Retention Policy Documentation" opens an internal page (or anchor) summarising each entity's default + RA 10173 rationale

---

## Scope

### In Scope
- Entities, migrations, seed data
- Admin UI (view, edit, save)
- Validation + authorization
- Audit logging on change
- Documentation section (brief, inline — full doc page can be a later tweak)

### Out of Scope
- The actual purge/archive handlers (US0095-US0100)
- Scheduled execution (US0101)
- Archive-to-file mechanics (US0102)
- Per-row retention overrides
- Retention of the `RetentionRun` table itself (self-referential; documented as: keep 365 days)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Duplicate seed run | Upsert behaviour: do not overwrite user-modified values; insert missing rows only |
| Admin sets `Enabled = false` | Policy row exists; retention is a no-op for that entity until re-enabled |
| `ArchiveEnabled = true` before US0102 ships | Field saved; actual archive behaviour is feature-gated and logs a warning until available |
| `EntityName` extended in future (e.g. new table) | Migration adds a new row; existing rows unaffected |

---

## Test Scenarios

- [ ] Migration creates both tables with correct schema
- [ ] Seeding creates six rows with documented defaults
- [ ] Re-run seed is idempotent
- [ ] Page renders policy rows correctly
- [ ] Validation blocks values below floor
- [ ] Save writes audit log entry
- [ ] Non-admin users cannot access page
- [ ] Page is linked from the Admin settings menu

---

## Technical Notes

### Entity Definitions
```csharp
public class RetentionPolicy {
    public int Id { get; set; }
    public string EntityName { get; set; } = null!;  // unique
    public int RetentionDays { get; set; }
    public bool Enabled { get; set; } = true;
    public bool ArchiveEnabled { get; set; } = false;
    public DateTime? LastRunAt { get; set; }
    public int? LastRowsAffected { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class RetentionRun {
    public long Id { get; set; }
    public string EntityName { get; set; } = null!;
    public string RunMode { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = null!;
    public int RowsAffected { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredBy { get; set; }
}
```

### Files to Create / Modify
- **New:** `src/SmartLog.Web/Data/Entities/RetentionPolicy.cs`
- **New:** `src/SmartLog.Web/Data/Entities/RetentionRun.cs`
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs`
- **New migration:** `AddRetentionPolicyAndRun`
- **Modify:** `src/SmartLog.Web/Data/DbInitializer.cs` — idempotent seed
- **New:** `src/SmartLog.Web/Pages/Admin/Settings/Retention.cshtml(.cs)`
- **Modify:** Admin nav / settings menu — add "Retention" link

---

## Dependencies

### Story Dependencies

None within EP0017 (this is the foundation).

---

## Estimation

**Story Points:** 3
**Complexity:** Medium — two new entities, migration, Admin page, validation, audit hook

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 retention planning session |
