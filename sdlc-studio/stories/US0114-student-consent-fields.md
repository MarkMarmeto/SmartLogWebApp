# US0114: Student Consent Fields — Entity, EF Config, Migration

> **Status:** Draft
> **Epic:** [EP0016: PII & RA 10173 Compliance — Consent & Notice (Floor)](../epics/EP0016-pii-ra10173-compliance.md)
> **Owner:** TBD
> **Created:** 2026-04-27

## User Story

**As a** Tech-Savvy Tony (System Admin)
**I want** the `Student` entity to record explicit data-processing consent and the date it was given
**So that** SmartLog has a defensible RA 10173 lawful basis for processing student PII, and the rest of EP0016 has a stable foundation to build on.

## Context

### Persona Reference
**Tony (Tech Admin)** — owns schema integrity, runs migrations.
**Admin Amy** — eventual consumer (uses the field via UI in US0115/US0116).

### Background
EP0016 establishes the RA 10173 floor: lawful basis (consent) + transparency (privacy notice). This story lands the data model. UI work follows in US0115 (capture) and US0116 (visibility). The columns are deliberately additive and nullable / default-false so existing student rows do not break — admins backfill on edit, no bulk migration is required by the floor scope.

The fields are designed so a future Subject-Rights Epic can layer withdrawal and history on top without schema rework. We do **not** add a separate `ConsentHistory` table in this Epic — `AuditLog` captures changes (US0115).

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0016 | Legal | RA 10173 §12 — consent must be demonstrable | Must store both the consent flag and the date it was given |
| EP0016 | Data | Existing student rows must not break | Column defaults to `false` / `null`; no backfill required by this story |
| TRD | Data | EF Core 8 + SQL Server | Standard EF migration, no raw SQL |

---

## Acceptance Criteria

### AC1: Entity Properties Added
- **Given** the `Student` entity class
- **Then** it declares two new properties:
  - `bool DataProcessingConsent` (non-nullable, default `false`)
  - `DateTime? ConsentDate` (nullable)

### AC2: DbContext Configuration
- **Given** `ApplicationDbContext.OnModelCreating`
- **Then** `Student.DataProcessingConsent` is configured `IsRequired()` with default value `false`
- **And** `Student.ConsentDate` is configured optional (nullable `datetime2`)

### AC3: EF Migration Generated
- **Given** the codebase after the entity + DbContext changes
- **When** `dotnet ef migrations add AddStudentDataProcessingConsent -p src/SmartLog.Web` is run
- **Then** a migration file is produced that adds two columns to the `Students` table:
  - `DataProcessingConsent bit NOT NULL DEFAULT 0`
  - `ConsentDate datetime2 NULL`

### AC4: Migration Applies Cleanly
- **Given** an existing dev/test DB with student rows
- **When** the migration is applied
- **Then** all existing rows have `DataProcessingConsent = 0` and `ConsentDate = NULL`
- **And** no existing column or constraint is altered
- **And** no FK or index is dropped

### AC5: Round-Trip Through EF
- **Given** the Student entity after migration
- **When** a student is loaded, modified (consent toggled true), and saved
- **Then** the persisted row reflects `DataProcessingConsent = 1` and the `ConsentDate` value provided
- **And** no other Student column is unintentionally written

### AC6: Backwards-Compatible Read
- **Given** existing pages and APIs that read `Student` (Details, list, scan ingestion)
- **Then** they continue to function unchanged
- **And** consent fields are not yet displayed (display lands in US0116)

---

## Scope

### In Scope
- `Student.cs` entity — two new properties
- `ApplicationDbContext` — Student configuration update
- EF migration `AddStudentDataProcessingConsent`
- Verifying existing reads do not regress

### Out of Scope
- Consent capture UI (US0115)
- Consent visibility (column / badge / filter) (US0116)
- Privacy notice page (US0117)
- AuditLog action code wiring — added with the UI in US0115
- Bulk-backfill of existing students (admin-driven via edit form; not required by this Epic)
- Consent withdrawal cascade (deferred to future Subject-Rights Epic)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Migration runs against a DB that already has the columns | EF should detect already-applied state; if hand-applied, document `dotnet ef migrations remove` recovery path |
| Student saved with `DataProcessingConsent = false` and `ConsentDate` set | Allowed at the data layer (no DB-level constraint); the UI in US0115 enforces "clear ConsentDate when unchecked" |
| Student saved with `DataProcessingConsent = true` and `ConsentDate = null` | Allowed at the data layer; UI enforces stamping `ConsentDate` when the box is ticked |

> Note: data-layer permissiveness is intentional — UI enforcement keeps the schema simple and lets a future Subject-Rights Epic add stricter invariants if needed.

---

## Test Scenarios

- [ ] Migration applies cleanly on a fresh DB
- [ ] Migration applies cleanly on a dev DB containing existing students
- [ ] After migration, all pre-existing students have `DataProcessingConsent = false`, `ConsentDate = null`
- [ ] Saving a Student with both consent fields round-trips correctly
- [ ] Existing Student-related tests (`StudentService`, scan ingestion, list page) still pass
- [ ] Migration is reversible via `dotnet ef migrations remove` before merge

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Data/Entities/Student.cs`
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` (Student configuration block)
- **New migration:** `src/SmartLog.Web/Migrations/<ts>_AddStudentDataProcessingConsent.cs`

### Naming
Use the property names exactly as specified — `DataProcessingConsent` and `ConsentDate`. Don't shorten to `Consent` to avoid ambiguity with future per-feature consent flags (e.g. SMS opt-in is already a separate field).

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Predecessor | Existing Student entity | Done |

### Blocks

- US0115 (Consent capture UI) — needs the fields
- US0116 (Consent visibility) — needs the fields

---

## Estimation

**Story Points:** 3
**Complexity:** Low — additive schema change, standard EF migration, no data backfill.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft as part of EP0016 V2.1 activation (consent + notice floor) |
