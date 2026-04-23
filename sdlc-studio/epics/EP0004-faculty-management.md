# EP0004: Faculty Management

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 1

## Summary

Enable administrators to manage faculty and teacher records including creating, editing, and deactivating faculty members. Faculty records can optionally be linked to user accounts for system access.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Data | Employee ID unique within school | Validation rule |
| PRD | Data | Soft delete only | IsActive flag pattern |
| TRD | Architecture | Faculty optionally linked to User | Nullable FK relationship |

---

## Business Context

### Problem Statement
Schools need to track faculty information separately from user accounts. Not all faculty members need system access (e.g., substitute teachers), but their records should still exist for administrative purposes.

**PRD Reference:** [Feature FT-005](../prd.md#3-feature-inventory)

### Value Proposition
- Centralized faculty directory for the school
- Optional linkage to user accounts provides flexibility
- Department organization enables filtering and reporting
- Contact information readily available

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Time to add new faculty | N/A | < 2 minutes | Process timing |
| Time to find faculty record | N/A | < 10 seconds | Search performance |
| Faculty record accuracy | N/A | 100% | Data audit |

---

## Scope

### In Scope
- Create faculty records (name, employee ID, department, contact)
- Edit faculty information
- Activate/deactivate faculty (soft delete)
- Search faculty by name, employee ID, department
- Filter faculty list by department, status
- Link faculty to existing user account (optional)
- Unlink faculty from user account
- View faculty details

### Out of Scope
- Teaching schedule management
- Class assignments
- Performance evaluations
- Salary/HR information
- Faculty QR codes (not needed for entry tracking)

### Affected Personas
- **Admin Amy (Administrator):** Full CRUD access to faculty records
- **Teacher Tina:** View faculty directory (read-only)

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can create a new faculty member with all required fields
- [ ] Employee ID is validated as unique
- [ ] Admin can edit faculty details
- [ ] Admin can deactivate/reactivate faculty
- [ ] Faculty list supports search by name and employee ID
- [ ] Faculty list supports filter by department and status
- [ ] Admin can link a faculty record to an existing user account
- [ ] Admin can unlink a faculty record from a user account
- [ ] Teacher can view faculty directory (read-only)
- [ ] All faculty management actions are audit logged

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0001: Authentication & Authorization | Epic | Not Started | Development |
| EP0002: User Management | Epic | Not Started | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | Faculty is standalone data |

---

## Risks & Assumptions

### Assumptions
- Employee IDs are assigned by school HR before entry into system
- Department structure is predefined
- Not all faculty need user accounts

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Duplicate employee IDs | Medium | Medium | Uniqueness validation |
| Linking to wrong user account | Low | Medium | Confirmation dialog, can unlink |
| Department list becomes outdated | Low | Low | Admin can manage departments (future) |

---

## Technical Considerations

### Architecture Impact
- Faculty entity with optional User FK
- Similar CRUD patterns to User and Student management
- Reusable components (search, filter, pagination)

### Integration Points
- Database: Faculty table with User FK (nullable)
- EP0001: Authorization for role-based access
- EP0002: User accounts for optional linking

---

## Sizing

**Story Points:** 12
**Estimated Story Count:** 5

**Complexity Factors:**
- Standard CRUD operations
- Optional user account linking
- Simpler than Student (no QR codes)

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0023](../stories/US0023-create-faculty.md) | Create Faculty Record | 2 | Done |
| [US0024](../stories/US0024-edit-faculty.md) | Edit Faculty Details | 2 | Done |
| [US0025](../stories/US0025-deactivate-faculty.md) | Deactivate/Reactivate Faculty | 2 | Done |
| [US0026](../stories/US0026-faculty-list.md) | Faculty List with Search and Filter | 3 | Done |
| [US0027](../stories/US0027-link-faculty-user.md) | Link/Unlink Faculty to User Account | 3 | Done |

**Total:** 12 story points across 5 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0004`

---

## Open Questions

- [ ] Should we predefine a list of departments or allow free text? - Owner: Product
- [ ] Should faculty linking to user be mandatory or optional? - Owner: Product

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
