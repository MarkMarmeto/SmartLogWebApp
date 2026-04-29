# EP0016: PII & RA 10173 Compliance — Consent & Notice (Floor)

> **Status:** Ready
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Activated:** 2026-04-27 (V2.1 wave; previously deferred to V3)
> **Target Release:** V2 — Phase 2 (Feature Enhancements)

## Summary

Land the **floor of RA 10173 compliance** for SmartLog: lawful-basis capture (student data-processing consent + timestamp), a published bilingual (EN/FIL) privacy notice, and consent visibility in the admin UI. Storage-limitation and auto-purge — the other RA 10173 pillar — already shipped with EP0017. This Epic intentionally stops at "consent + notice." Subject-rights workflows (access, correction, erasure, objection), consent-withdrawal cascade, and the breach-response playbook are out of scope and tracked in a future Subject-Rights Epic.

## Inherited Constraints

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| Legal | RA 10173 §12 | Lawful processing requires consent or other legal basis | Student record must store explicit consent + date |
| Legal | RA 10173 §16 | Data subjects must be informed of processing purposes | Public privacy notice required, no auth gate |
| Legal | RA 10173 — Bilingual | School communications target Filipino + English audiences | Privacy notice must be available in both languages |
| PRD | Data | Student PII (name, LRN, parent phone, photo) | Consent recorded against the `Student` row |
| EP0017 | Done | Storage-limitation pillar already implemented | This Epic does not re-do retention; it complements it |

---

## Business Context

### Problem Statement
SmartLog processes student PII without recording an explicit lawful basis or publishing a privacy notice. RA 10173 requires both before commercial distribution. Without them, the product cannot be sold to Philippine schools without exposing both the school and the vendor to compliance risk. The retention pillar is solved (EP0017); the lawful-basis + transparency pillar is not.

### Value Proposition
- Establishes a defensible RA 10173 posture (consent + notice) so commercial sales can begin
- Gives admins a clear field to record consent rather than relying on paper-only records
- Publishes a privacy notice parents and admins can reference at any time
- Sets up the data model that a future Subject-Rights Epic will extend (no schema rework needed later)

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Consent coverage | 0% | 100% of newly created students | `Student.DataProcessingConsent` populated at create time |
| Consent coverage (existing students) | 0% | Trackable per-student in admin UI | Filterable list + badge on detail page |
| Privacy notice availability | None | Published EN + FIL at `/Privacy` | Page reachable without authentication |

---

## Scope

### In Scope
- **Student consent fields:** `Student.DataProcessingConsent` (bool, default `false`) + `Student.ConsentDate` (DateTime?, nullable)
- **EF migration + DbContext config:** add the columns; existing rows default to `false`/`null`
- **Consent UI:** checkbox on student create/edit; auto-set `ConsentDate` to `DateTime.UtcNow` when the box is checked; clear `ConsentDate` when unchecked
- **Consent visibility:** badge on student detail page, column on student list, filter on student list (`All / Consent given / Consent not given`)
- **Bilingual privacy notice page:** `/Privacy` Razor page, no auth, EN/FIL language toggle, navigation link in the public footer / login page
- **Audit:** consent change logged to `AuditLog` (action: `Student.ConsentUpdated`)

### Out of Scope (deferred to future Subject-Rights Epic)
- Right-to-be-forgotten / erasure requests
- Right-to-access / data export per data subject
- Right-to-correction request workflow
- Right-to-object workflow (e.g., stop SMS on objection)
- Consent-withdrawal cascade effects (auto-disable SMS on consent revoke, etc.)
- Breach notification playbook / NPC 72-hour notification process
- DPO appointment process + NPC registration
- Encryption-at-rest (TDE — separate infra concern)
- Field-level PII access audit (who viewed which student record)

### Affected Personas
- **Admin Amy** — Records consent during enrollment / edit
- **SuperAdmin Tony** — Reviews consent coverage; approves the published privacy notice
- **Parents (indirect)** — Provide consent; can read the privacy notice without logging in

---

## Acceptance Criteria (Epic Level)

- [ ] `Student` entity has `DataProcessingConsent` (bool) and `ConsentDate` (DateTime?) columns; EF migration applied
- [ ] Student create/edit form exposes a consent checkbox; ticking the box stamps `ConsentDate = UtcNow`; unticking clears `ConsentDate`
- [ ] Student list shows a consent column and a `Consent` filter (All / Given / Not given)
- [ ] Student detail page shows a consent badge with date
- [ ] `/Privacy` page is reachable without authentication and renders both EN and FIL content via a language toggle
- [ ] Privacy notice is linked from the login page footer
- [ ] Consent changes (true→false, false→true) are written to `AuditLog`
- [ ] Privacy notice content is marked **"Draft — pending legal review"** until SuperAdmin approves

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0003: Student Management | Epic | Done | Development |
| EP0017: Data Retention & Archival | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| Future EP00xx: Subject Rights & Breach Response | Epic (planned) | Subject-rights workflows extend the consent fields landed here |
| Commercial launch | Business | RA 10173 floor must be in place to sell to PH schools |

---

## Risks & Assumptions

### Assumptions
- A self-drafted bilingual privacy notice (modeled on the NPC's standard template) is acceptable as a "Draft — pending legal review" until SuperAdmin signs off.
- Existing student rows can default to `DataProcessingConsent = false`; admins backfill on edit. No bulk-backfill is required by this Epic.
- Audit logging the consent change is sufficient evidence; we do not need a separate ConsentHistory table for the floor.

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Admin checks consent without actually obtaining it from parent | Medium | High | Inline help text on the form; SuperAdmin policy + audit trail of who flipped the bit |
| Privacy notice content is inadequate vs. what legal eventually approves | Medium | Medium | Mark as "Draft — pending legal review"; store text in a version-controlled file so updates are reviewable |
| Existing students show 0% consent → looks like a regression | Low | Low | Filter defaults to "All"; documentation explains backfill expectation |
| Confusion with EP0017 retention scope | Low | Low | Epic summary explicitly states retention already shipped; no overlap |

---

## Technical Considerations

### Architecture Impact
- New columns on `Student` entity (additive, nullable / default-false)
- New Razor page `/Privacy` (root-level, anonymous-allowed)
- Privacy content stored as two `.cshtml` partials (one EN, one FIL) or in resource files; toggle via query string `?lang=en|fil` (default = browser-Accept-Language → fallback EN)
- New `AuditLog` action code: `Student.ConsentUpdated`

### Integration Points
- `ApplicationDbContext` — `Student` config gets two new properties
- `DbInitializer` — no seed change required
- Student create/edit Razor pages + page models
- Student list Razor page + filter pipeline
- New root page `Pages/Privacy.cshtml(.cs)` + nav link in `_Layout` / login page

### Key Files to Create/Modify
- **Modify:** `src/SmartLog.Web/Data/Entities/Student.cs`
- **Modify:** `src/SmartLog.Web/Data/ApplicationDbContext.cs` (Student config)
- **New migration:** `src/SmartLog.Web/Migrations/<ts>_AddStudentDataProcessingConsent.cs`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Create.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Edit.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Index.cshtml(.cs)` (column + filter)
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Details.cshtml` (badge)
- **New:** `src/SmartLog.Web/Pages/Privacy.cshtml(.cs)`
- **New:** `src/SmartLog.Web/Pages/_PrivacyNotice.en.cshtml`, `_PrivacyNotice.fil.cshtml`
- **Modify:** login page / footer to link `/Privacy`

---

## Sizing

**Story Points:** 10 across 4 stories
**Estimated Story Count:** 4

**Complexity Factors:**
- Schema additive only; migration is low-risk
- Privacy notice content drafting (not code-heavy) is the long pole
- Public anonymous route needs explicit auth-allow rule

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0114](../stories/US0114-student-consent-fields.md) | Student Consent Fields (entity + migration + EF) | 3 | Draft |
| [US0115](../stories/US0115-consent-capture-on-student-form.md) | Consent Capture on Student Create/Edit Form | 2 | Draft |
| [US0116](../stories/US0116-consent-visibility-list-and-detail.md) | Consent Visibility on Student List & Detail | 2 | Draft |
| [US0117](../stories/US0117-bilingual-privacy-notice-page.md) | Bilingual Privacy Notice Page at `/Privacy` | 3 | Draft |

**Total:** 10 story points across 4 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0016`

Key flows to cover:
- Migration applies cleanly on a DB with existing students; all rows default to `DataProcessingConsent=false`, `ConsentDate=null`
- Toggling consent on the edit form sets/clears `ConsentDate` correctly and writes an `AuditLog` row
- Student list filter returns the correct subset for each filter value
- `/Privacy` is reachable while logged out; `?lang=fil` renders Filipino content; default falls back to EN

---

## Open Questions

- [ ] Final wording of the bilingual privacy notice — self-drafted in US0117 with a "Draft — pending legal review" banner; SuperAdmin sign-off can replace the banner without a code change if the text is moved to `AppSettings`. Decide: cshtml partial (versioned) vs. AppSettings (admin-editable)? **Proposed:** cshtml partial for v1; revisit if legal review demands frequent edits.
- [ ] Should consent toggle on the form trigger a confirmation modal (to discourage accidental flips)? **Proposed:** No modal for v1; rely on audit log + admin training.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2/V3 brainstorm (deferred) |
| 2026-04-27 | Claude | Activated for V2.1; scope narrowed to "consent + notice" (retention removed — covered by EP0017; subject-rights deferred to a future Epic). Drafted 4 stories US0114-US0117. |
