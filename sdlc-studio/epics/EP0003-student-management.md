# EP0003: Student Management

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 1

## Summary

Enable administrators to manage student records including creating, editing, and deactivating students. Each student automatically receives a unique, HMAC-signed QR code for identity verification at school entry points.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Data | Student ID unique within school | Validation rule |
| PRD | Data | Parent phone required, format validated | Input validation |
| PRD | Security | QR codes use HMAC-SHA256 signing | Cryptographic implementation |
| TRD | Tech Stack | QRCoder library for generation | Dependency |

---

## Business Context

### Problem Statement
Schools need a centralized system to manage student records. Each student needs a verifiable identity (QR code) for secure entry/exit tracking. Manual paper-based systems are slow, error-prone, and don't support automated attendance.

**PRD Reference:** [Features FT-004, FT-006](../prd.md#3-feature-inventory)

### Value Proposition
- Centralized student database replaces paper records
- Secure QR codes enable automated attendance tracking
- Parent contact information readily available for emergencies
- Searchable records speed up administrative tasks

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Time to register new student | N/A | < 3 minutes | Process timing |
| Time to find student record | N/A | < 10 seconds | Search performance |
| QR code verification failures | N/A | 0 (valid codes) | Scanner logs |

---

## Scope

### In Scope
- Create student records (name, student ID, grade, section, parent info)
- Edit student information
- Activate/deactivate students (soft delete)
- Search students by name, ID, grade, section
- Filter student list by grade, section, status
- Generate HMAC-signed QR code on student creation
- Regenerate QR code (invalidates old code)
- Print QR code (individual)
- Bulk print QR codes (PDF for class/section)
- View student details (read-only for Teacher, Staff)

### Out of Scope
- Bulk student import (CSV) - consider for future
- Student photos
- Academic records (grades, transcripts)
- Medical information
- Attendance history view (separate epic EP0006)

### Affected Personas
- **Admin Amy (Administrator):** Full CRUD access, QR generation/printing
- **Teacher Tina:** View student info, view class roster
- **Staff Sarah:** View-only student lookup

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can create a new student with all required fields
- [ ] Student ID is validated as unique
- [ ] Parent phone is validated for format
- [ ] QR code is automatically generated on student creation
- [ ] QR code format: `SMARTLOG:{studentId}:{timestamp}:{hmacBase64}`
- [ ] Admin can regenerate QR code (old code becomes invalid)
- [ ] Admin can print individual student QR code
- [ ] Admin can bulk print QR codes for a class/section as PDF
- [ ] Admin can edit student details
- [ ] Admin can deactivate/reactivate students
- [ ] Student list supports search by name and student ID
- [ ] Student list supports filter by grade, section, status
- [ ] Teacher can view student list and details (read-only)
- [ ] Staff can search and view student details (read-only)
- [ ] All student management actions are audit logged

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0001: Authentication & Authorization | Epic | Not Started | Development |
| HMAC Secret Key Configuration | Infrastructure | Not Started | Tony |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0005: Scanner Integration | Epic | Scanners verify student QR codes |
| EP0006: Attendance Tracking | Epic | Attendance links to students |

---

## Risks & Assumptions

### Assumptions
- Student IDs are assigned by school before entry into system
- Grade/section structure is consistent across the school
- Parent phone numbers are valid local mobile numbers

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| HMAC key compromised | Low | Critical | Secure key storage, key rotation plan |
| Duplicate student IDs entered | Medium | Medium | Uniqueness validation, clear error |
| QR codes printed incorrectly | Low | Medium | Preview before print, test prints |

---

## Technical Considerations

### Architecture Impact
- Student entity with QrCode relationship
- QrCodeService for HMAC signing and generation
- PDF generation for bulk printing (QuestPDF or similar)
- Secure storage for HMAC secret key (environment variable)

### Integration Points
- Database: Student, QrCode tables
- EP0001: Authorization for role-based access
- EP0005: QR codes used by scanner devices
- Printing: Browser print or server-side PDF

---

## Sizing

**Story Points:** 24
**Estimated Story Count:** 8

**Complexity Factors:**
- HMAC cryptographic signing
- QR code generation and display
- PDF generation for bulk printing
- Role-based view permissions (Admin vs Teacher vs Staff)

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0015](../stories/US0015-create-student.md) | Create Student Record | 3 | Done |
| [US0016](../stories/US0016-edit-student.md) | Edit Student Details | 2 | Done |
| [US0017](../stories/US0017-deactivate-student.md) | Deactivate/Reactivate Student | 2 | Done |
| [US0018](../stories/US0018-student-list.md) | Student List with Search and Filter | 3 | Done |
| [US0019](../stories/US0019-generate-qr.md) | Generate Student QR Code | 5 | Done |
| [US0020](../stories/US0020-regenerate-qr.md) | Regenerate Student QR Code | 2 | Done |
| [US0021](../stories/US0021-print-qr.md) | Print Individual QR Code | 2 | Done |
| [US0022](../stories/US0022-bulk-print-qr.md) | Bulk Print QR Codes | 5 | Done |

**Total:** 24 story points across 8 stories

*Note: View Student Details (Read-Only) functionality is included in US0018 (Student List) with role-based access controls.*

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0003`

---

## Open Questions

- [ ] Should QR codes have an expiration date? - Owner: Product
- [ ] What QR code size/format is optimal for scanning? - Owner: Development
- [ ] Should we support custom student ID formats per school? - Owner: Product

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
