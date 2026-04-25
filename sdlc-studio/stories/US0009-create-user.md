# US0009: Create User Account

> **Status:** Done
> **Epic:** [EP0002: User Management](../epics/EP0002-user-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to create new user accounts for school staff
**So that** they can access SmartLog based on their role

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who onboards new staff members.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

### Background
When new staff join the school, they need system access. Administrators create accounts with appropriate roles so staff can perform their duties.

---

## Acceptance Criteria

### AC1: Access Create User Form
- **Given** I am logged in as Admin Amy
- **When** I navigate to Users > Create New User
- **Then** I see a form with fields: Username, Email, First Name, Last Name, Phone (optional), Role dropdown

### AC2: Successful User Creation
- **Given** I am on the Create User form
- **When** I enter valid data: username "teacher.john", email "john@school.edu", first name "John", last name "Smith", role "Teacher"
- **And** I click "Create User"
- **Then** the user is created with a temporary password
- **And** I see success message "User 'teacher.john' created successfully"
- **And** the temporary password is displayed (or sent via email in future)
- **And** I am redirected to the user list

### AC3: Username Validation
- **Given** I am on the Create User form
- **When** I enter a username that already exists
- **And** I click "Create User"
- **Then** I see error "Username already exists"
- **And** the user is not created

### AC4: Email Validation
- **Given** I am on the Create User form
- **When** I enter an invalid email format "notanemail"
- **And** I click "Create User"
- **Then** I see error "Please enter a valid email address"

### AC5: Role Assignment Restrictions
- **Given** I am logged in as Admin (not Super Admin)
- **When** I view the Role dropdown
- **Then** I see options: Admin, Teacher, Security, Staff
- **And** I do NOT see "Super Admin" option

### AC6: Required Field Validation
- **Given** I am on the Create User form
- **When** I leave Username, Email, First Name, or Last Name empty
- **And** I click "Create User"
- **Then** I see validation errors for each empty required field

---

## Scope

### In Scope
- Create user form UI
- Server-side validation
- Temporary password generation
- Role assignment
- Audit logging

### Out of Scope
- Email notification with credentials
- Bulk user import
- Profile photo upload

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Username with spaces | Replace spaces with dots or reject |
| Very long username (>50 chars) | Show max length error |
| Email already in use | Show "Email already registered" |
| Special characters in name | Allow letters, hyphens, apostrophes |
| Admin creating Super Admin | Role option not visible |
| Network error during save | Show error, preserve form data |
| Concurrent creation of same username | First wins, second gets error |
| Unicode characters in name | Allow international characters |

---

## Test Scenarios

- [ ] Create user with all valid fields succeeds
- [ ] Create user generates temporary password
- [ ] Duplicate username rejected
- [ ] Duplicate email rejected
- [ ] Invalid email format rejected
- [ ] Empty required fields show validation errors
- [ ] Admin cannot assign Super Admin role
- [ ] Super Admin can assign any role
- [ ] Audit log entry created on user creation
- [ ] Phone number is optional

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0007](US0007-authorization-enforcement.md) | Functional | Authorization policies | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
