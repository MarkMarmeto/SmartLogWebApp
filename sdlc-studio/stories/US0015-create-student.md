# US0015: Create Student Record

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to create new student records with parent contact information
**So that** students are registered in the system for attendance tracking

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who handles student enrollment.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Access Create Student Form
- **Given** I am logged in as Admin Amy
- **When** I navigate to Students > Create New Student
- **Then** I see a form with fields:
  - Student ID (required, format: YYYY-GG-NNNN)
  - LRN - Learner Reference Number (optional, for DepEd compliance)
  - First Name (required)
  - Last Name (required)
  - Grade Level (required, dropdown)
  - Section (required)
  - Parent/Guardian Name (required)
  - Guardian Relationship (required, dropdown: Mother, Father, Guardian, Other)
  - Parent Phone (required)

### AC2: Successful Student Creation
- **Given** I am on the Create Student form
- **When** I enter valid data:
  - Student ID "2026-05-0001"
  - LRN "123456789012" (optional)
  - First Name "Maria"
  - Last Name "Santos"
  - Grade "5"
  - Section "A"
  - Parent Name "Jose Santos"
  - Guardian Relationship "Father"
  - Parent Phone "09171234567"
- **And** I click "Create Student"
- **Then** the student is created
- **And** a QR code is automatically generated for the student
- **And** I see success message "Student 'Maria Santos' created successfully"
- **And** I am shown the student details page with QR code

### AC2a: Student ID Format
- **Given** I am entering a Student ID
- **Then** the format is enforced as `YYYY-GG-NNNN`:
  - YYYY: Enrollment year (4 digits)
  - GG: Grade level at enrollment (2 digits, 01-12)
  - NNNN: Sequence number (4 digits)
- **Example:** 2026-05-0001 (First Grade 5 student enrolled in 2026)

### AC2b: LRN Validation
- **Given** I enter an LRN
- **Then** it must be exactly 12 digits
- **And** if LRN already exists, show error "LRN already registered to another student"

### AC3: Student ID Validation
- **Given** I am on the Create Student form
- **When** I enter a Student ID that already exists
- **And** I click "Create Student"
- **Then** I see error "Student ID already exists"

### AC4: Parent Phone Validation
- **Given** I am on the Create Student form
- **When** I enter an invalid phone number "123"
- **And** I click "Create Student"
- **Then** I see error "Please enter a valid phone number"

### AC5: Required Fields Validation
- **Given** I am on the Create Student form
- **When** I leave any required field empty
- **And** I click "Create Student"
- **Then** I see validation errors for each empty required field

### AC6: Audit Log Entry
- **Given** I successfully create a student
- **Then** an audit log entry is created with Action: "StudentCreated"

---

## Scope

### In Scope
- Create student form UI
- All field validations
- Automatic QR code generation on create
- Audit logging

### Out of Scope
- Bulk student import
- Student photo upload
- Academic/medical records

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student ID wrong format | Show error with correct format hint |
| LRN not 12 digits | Show error "LRN must be exactly 12 digits" |
| Duplicate LRN | Show error "LRN already registered" |
| Phone number with country code | Accept with or without +63 prefix |
| Very long names | Limit to 100 characters |
| Duplicate parent phone (siblings) | Allow (not unique constraint) |
| Grade level format | Dropdown: K, 1-12 |
| Section format | Free text, school defines convention |
| Network error during save | Show error, preserve form data |
| QR generation fails | Show error, allow retry |

---

## Test Scenarios

- [ ] Create student with all valid fields succeeds
- [ ] QR code generated automatically on create
- [ ] Duplicate Student ID rejected
- [ ] Student ID format validated (YYYY-GG-NNNN)
- [ ] LRN is optional
- [ ] LRN must be 12 digits if provided
- [ ] Duplicate LRN rejected
- [ ] Guardian Relationship dropdown works
- [ ] Invalid phone format rejected
- [ ] Empty required fields show validation errors
- [ ] Audit log entry created
- [ ] Student appears in student list after creation
- [ ] Parent phone accepts various formats
- [ ] Grade and Section saved correctly
- [ ] Success message displayed after creation

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0007](US0007-authorization-enforcement.md) | Functional | Authorization | Draft |
| [US0019](US0019-generate-qr.md) | Functional | QR generation | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Stakeholder Decisions

- [x] Student ID format: YYYY-GG-NNNN - **Approved by Registrar Rosa, IT Manager Ivan**
- [x] Add LRN field for DepEd compliance - **Approved by Registrar Rosa**
- [x] Add Guardian Relationship dropdown - **Approved by Registrar Rosa**

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Added LRN field, Guardian Relationship, Student ID format |
