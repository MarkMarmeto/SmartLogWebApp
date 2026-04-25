# US0039: SMS Template Management

> **Status:** Done
> **Epic:** [EP0007: SMS Notifications](../epics/EP0007-sms-notifications.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to create and manage SMS message templates
**So that** I can customize the notifications parents receive

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who configures school communications.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: View SMS Templates
- **Given** I am logged in as Admin Amy
- **When** I navigate to Settings > SMS Templates
- **Then** I see a list of SMS templates:
  - Entry Notification (English)
  - Entry Notification (Filipino)
  - Exit Notification (English)
  - Exit Notification (Filipino)

### AC2: Edit Template
- **Given** I click "Edit" on the Entry Notification template
- **Then** I see a form with:
  - Template Name (read-only)
  - Message Content (editable textarea)
  - Available Variables list
  - Preview section

### AC3: Template Variables
- **Given** I am editing a template
- **Then** I see available variables:
  - `{StudentName}` - Full name of student
  - `{FirstName}` - Student's first name
  - `{Time}` - Time of scan (e.g., "8:15 AM")
  - `{Date}` - Date of scan (e.g., "Feb 4, 2026")
  - `{SchoolName}` - Name of school
- **And** I can click to insert a variable at cursor position

### AC4: Template Preview
- **Given** I edit the template content
- **Then** the preview section shows the message with sample data:
  - `{StudentName}` → "Maria Santos"
  - `{Time}` → "8:15 AM"
  - `{Date}` → "February 4, 2026"
  - `{SchoolName}` → "ABC School"

### AC5: Character Limit
- **Given** I am editing a template
- **Then** I see a character counter showing "X / 160 characters"
- **And** messages over 160 characters show warning: "Message may be split into multiple SMS"

### AC6: Save Template
- **Given** I edit the Entry Notification template to:
  ```
  {StudentName} arrived at {SchoolName} at {Time} on {Date}.
  ```
- **When** I click "Save"
- **Then** the template is saved
- **And** I see success message "Template updated successfully"

### AC7: Default Templates
- **Given** the system is freshly installed
- **Then** default templates exist:
  - Entry (English): "{StudentName} has arrived at school at {Time}."
  - Entry (Filipino): "Si {StudentName} ay dumating sa paaralan ng {Time}."
  - Exit (English): "{StudentName} has left school at {Time}."
  - Exit (Filipino): "Si {StudentName} ay umalis sa paaralan ng {Time}."

### AC8: Language Selection per Student
- **Given** a student record exists
- **Then** the student record has an "SMS Language" field (dropdown: English, Filipino)
- **And** defaults to Filipino
- **And** when SMS is sent, the appropriate language template is used

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Empty template | Show error "Message cannot be empty" |
| Invalid variable syntax | Show error "Unknown variable: {InvalidVar}" |
| Very long message | Allow, but warn about multiple SMS |
| Special characters | Allow (emojis, etc.) with char count warning |
| Cancel without saving | Confirm "Discard changes?" |
| Network error on save | Show error, preserve edits |

---

## Test Scenarios

- [ ] Template list displays correctly
- [ ] Edit form shows current template
- [ ] Variables list shows all available variables
- [ ] Insert variable at cursor works
- [ ] Preview updates in real-time
- [ ] Character counter is accurate
- [ ] Over 160 chars shows warning
- [ ] Save updates template
- [ ] Default templates exist on fresh install
- [ ] Filipino language templates available
- [ ] English language templates available
- [ ] Language selection on student record
- [ ] Invalid variable shows error
- [ ] Empty template rejected

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-admin-login.md) | Functional | Admin logged in | Ready |
| [US0007](US0007-authorization-enforcement.md) | Functional | Admin role required | Ready |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Stakeholder Decisions

- [x] Add Filipino language templates - **Approved by PTA Rep Patricia**
- [x] Language selection per student (default: Filipino)

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Added Filipino language templates, language selection per student |
