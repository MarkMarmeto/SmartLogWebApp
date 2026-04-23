# US0028: Register Scanner Device

> **Status:** Done
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Tech-Savvy Tony (Super Admin)
**I want** to register a new scanner device in the system
**So that** it can authenticate with the server and submit student scans

## Context

### Persona Reference
**Tech-Savvy Tony** - IT Administrator who sets up and manages scanner devices.
[Full persona details](../personas.md#1-tech-savvy-tony-super-admin)

---

## Acceptance Criteria

### AC1: Register Device Form
- **Given** I am logged in as Super Admin
- **When** I navigate to Devices > Register New Device
- **Then** I see a form with fields:
  - Device Name (required, e.g., "Main Gate Scanner 1")
  - Location (required, dropdown):
    - Main Gate
    - Side Gate
    - Back Gate
    - Gymnasium
    - Other (with text field)
  - Description (optional)

### AC2: Device Name Validation
- **Given** I am registering a device
- **When** I enter a device name that already exists
- **Then** I see error "Device name already exists"

### AC3: API Key Generation
- **Given** I submit the register device form with valid data
- **When** the device is created successfully
- **Then** a unique API key is generated (32+ random characters)
- **And** the API key is displayed ONCE in a copyable format
- **And** I see warning: "Copy this API key now. It will not be shown again."

### AC4: API Key Storage
- **Given** a device is registered
- **Then** the API key is stored as a secure hash (not plain text)
- **And** only the hashed value is persisted in the database

### AC5: Device Record Created
- **Given** I register a device "Main Gate Scanner 1"
- **Then** a device record is created with:
  - DeviceId (GUID)
  - Name: "Main Gate Scanner 1"
  - Location: "Main Entrance"
  - ApiKeyHash: (hashed key)
  - IsActive: true
  - RegisteredAt: current timestamp
  - RegisteredBy: my user ID

### AC6: Audit Log Entry
- **Given** I register a new device
- **Then** an audit log entry is created with:
  - Action: "DeviceRegistered"
  - DeviceId
  - PerformedBy: my user ID
  - Details: device name and location

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Duplicate device name | Show error, do not register |
| Very long device name | Limit to 100 characters |
| Special characters in name | Allow (e.g., "Scanner #1") |
| Network error during registration | Show error, no API key generated |
| Session expired | Redirect to login |
| Non-Super Admin attempts registration | Access denied (403) |

---

## Test Scenarios

- [ ] Register device form displays correctly
- [ ] Device name uniqueness validated
- [ ] API key generated on successful registration
- [ ] API key shown only once
- [ ] API key stored as hash
- [ ] Device record created with correct fields
- [ ] Audit log entry created
- [ ] Copy API key button works
- [ ] Access restricted to Super Admin only
- [ ] Form validation prevents empty required fields
- [ ] Location dropdown has predefined options
- [ ] "Other" location allows custom text

---

## Technical Notes

### API Key Format
- 32-character alphanumeric string (Base64 encoded random bytes)
- Example: `sk_live_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6`

### API Key Storage
- Hash using SHA-256 or similar
- Store only the hash, never the plain text
- Compare incoming API keys by hashing and comparing

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-admin-login.md) | Functional | Super Admin logged in | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Super Admin role check | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Stakeholder Decisions

- [x] Add Location dropdown instead of free text - **Approved by IT Manager Ivan**
- [x] Predefined locations: Main Gate, Side Gate, Back Gate, Gymnasium, Other

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Changed Location to dropdown with predefined options |
