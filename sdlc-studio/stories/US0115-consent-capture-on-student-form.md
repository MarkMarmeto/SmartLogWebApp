# US0115: Consent Capture on Student Create/Edit Form

> **Status:** Draft
> **Epic:** [EP0016: PII & RA 10173 Compliance — Consent & Notice (Floor)](../epics/EP0016-pii-ra10173-compliance.md)
> **Owner:** TBD
> **Created:** 2026-04-27

## User Story

**As an** Admin Amy (Administrator)
**I want** a clear consent checkbox on the student create/edit form that automatically stamps the date when ticked
**So that** I can record an RA 10173-compliant lawful basis for processing each student's PII without juggling a separate date field, and so the system has a defensible audit trail of when consent was given.

## Context

### Persona Reference
**Admin Amy** — primary user; ticks the box during enrollment after collecting the parent's signed consent form.

### Background
US0114 added `DataProcessingConsent` and `ConsentDate` to `Student`. This story makes them admin-editable from the existing Create / Edit pages and writes the change to `AuditLog` so we have evidence of who flipped the bit and when. The UI deliberately enforces the invariant "if consent is checked, ConsentDate is set; if unchecked, ConsentDate is cleared" — see US0114 AC notes on why this lives in the UI rather than the schema.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0016 | Legal | RA 10173 §12 — consent must be demonstrable + dated | Date must be auto-stamped, not editable |
| EP0016 | Audit | Consent changes must be logged | Both true→false and false→true write to AuditLog |
| US0114 | Data | Fields exist on `Student` | Form binds to the entity directly |
| TRD | UI | Razor Pages + tag helpers | No SPA layer; standard model binding |

---

## Acceptance Criteria

### AC1: Checkbox on Create Form
- **Given** I navigate to `/Admin/Students/Create`
- **Then** I see a labelled "Data Processing Consent (RA 10173)" checkbox in the form
- **And** the checkbox is unchecked by default
- **And** there is inline help text: "Tick only after the parent/guardian has signed the consent form. The date is recorded automatically."

### AC2: Checkbox on Edit Form
- **Given** I navigate to `/Admin/Students/Edit/{id}` for an existing student
- **Then** the checkbox reflects the student's current `DataProcessingConsent` value
- **And** if consent was previously given, the consent date is shown as read-only text below the checkbox: "Consent recorded on yyyy-MM-dd"
- **And** if consent was never given, no date is shown

### AC3: Auto-Stamp on Tick
- **Given** I tick a previously unchecked consent checkbox and submit the form
- **When** the page model saves the student
- **Then** `Student.DataProcessingConsent` is `true`
- **And** `Student.ConsentDate` is set to `DateTime.UtcNow` at save time

### AC4: Auto-Clear on Untick
- **Given** I untick a previously checked consent checkbox and submit the form
- **When** the page model saves the student
- **Then** `Student.DataProcessingConsent` is `false`
- **And** `Student.ConsentDate` is `null`

### AC5: No Change When Untouched
- **Given** I edit a student without changing the consent checkbox
- **When** I submit the form
- **Then** neither `DataProcessingConsent` nor `ConsentDate` is modified (the existing `ConsentDate` is preserved)

### AC6: AuditLog Entry on Change
- **Given** the consent value changes during a save (true→false or false→true)
- **Then** an `AuditLog` row is written with:
  - `Action = "Student.ConsentUpdated"`
  - `PerformedByUserId = current admin's user id`
  - `Description` includes the student id, the previous value, the new value, and the new ConsentDate (or `null`)
- **And** if the consent value did not change, **no** `AuditLog` row is written

### AC7: Authorization
- **Given** a user without `CanManageStudents` permission
- **When** they attempt to access the Edit page or POST a form modifying consent
- **Then** they are redirected / 403'd by the existing authorization policy (no new policy needed)

---

## Scope

### In Scope
- Add the consent checkbox to `Pages/Admin/Students/Create.cshtml` and `Edit.cshtml`
- Update `Create.cshtml.cs` and `Edit.cshtml.cs` page models to bind, set/clear `ConsentDate`, and emit the `AuditLog` row on change
- New `AuditLog` action code: `Student.ConsentUpdated`
- Help text + read-only date display

### Out of Scope
- Bulk-backfill of consent for existing students (admin-driven, one-at-a-time via Edit)
- Visibility on student list / detail (US0116)
- Privacy notice page (US0117)
- Confirmation modal on toggle (deferred per EP0016 open question — not required for v1)
- Consent withdrawal cascade (deferred to future Subject-Rights Epic)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Admin ticks the box, then unticks before submitting | No change persisted, no AuditLog row |
| Admin saves with form-level validation error elsewhere | Consent value re-binds correctly on re-render; no false AuditLog row |
| Two admins edit the same student concurrently and both flip consent | Last writer wins (existing behaviour); both AuditLog rows are written |
| Admin un-ticks consent and the student already had `ConsentDate` set | `ConsentDate` is cleared; AuditLog records previous date in the description for traceability |

---

## Test Scenarios

- [ ] Create form shows unchecked checkbox by default; submitting unchecked saves false/null
- [ ] Create form with checkbox ticked saves true and stamps `ConsentDate ≈ UtcNow` (within 5s)
- [ ] Edit form pre-fills from existing values and shows the recorded date
- [ ] Toggling consent true→false on edit clears `ConsentDate` and writes one AuditLog row
- [ ] Toggling consent false→true on edit stamps `ConsentDate` and writes one AuditLog row
- [ ] Editing a student without changing consent writes zero consent-related AuditLog rows
- [ ] Non-admin user cannot access the Edit page

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Create.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Edit.cshtml(.cs)`
- **Modify:** existing audit logging helper / `IAuditLogger` (whichever pattern the codebase uses) to emit `Student.ConsentUpdated`
- **No** new entity / migration

### Implementation Pattern
- Capture pre-save `(oldConsent, oldConsentDate)` before model binding overwrites the entity
- Compare to `(newConsent)` post-bind
- If changed: set `ConsentDate = newConsent ? DateTime.UtcNow : null`; emit AuditLog row
- If unchanged: leave `ConsentDate` alone (do not overwrite to existing value)

### UI Layout
Place the consent block at the bottom of the form, above the submit row, in its own bordered fieldset titled "Data Privacy" — visually separate from biographical fields so admins do not mistake it for a routine option.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0114](US0114-student-consent-fields.md) | Predecessor | Consent fields on `Student` | Draft |

### Blocks

- US0116 (visibility) — assumes the field is admin-editable

---

## Estimation

**Story Points:** 2
**Complexity:** Low — UI + page-model logic only; AuditLog plumbing is established.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft as part of EP0016 V2.1 activation (consent + notice floor) |
