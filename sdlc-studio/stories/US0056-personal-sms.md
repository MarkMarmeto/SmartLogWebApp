# US0056: Personal SMS from Student Profile

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to send a freeform SMS to a student's parent directly from the student profile page
**So that** I can communicate urgent or specific messages without creating a broadcast

## Context

### Background
Broadcasts target groups. This feature enables one-on-one communication with a specific parent. The SMS is queued with MessageType "PERSONAL" and sent to both ParentPhone and AlternatePhone (if set).

---

## Acceptance Criteria

### AC1: Send Button on Student Profile
- **Given** I am viewing a student detail page
- **And** the student has a ParentPhone number
- **Then** I see a "Send SMS to Parent" button

### AC2: SMS Modal
- **Given** I click "Send SMS to Parent"
- **Then** a modal dialog appears with:
  - Student name and parent phone displayed (read-only)
  - AlternatePhone displayed if set
  - Message textarea (editable)
  - Character counter: "X / 160 characters"
  - Warning when over 160: "Message may be split into multiple SMS"
  - "Send" and "Cancel" buttons

### AC3: Send SMS
- **Given** I type "Please bring Juan's school uniform tomorrow." (47 chars)
- **When** I click "Send"
- **Then** an SmsQueue entry is created with:
  - PhoneNumber = student's ParentPhone
  - Message = the typed message
  - MessageType = "PERSONAL"
  - StudentId = the student's ID
  - Priority = Normal
- **And** if AlternatePhone is set, a second SmsQueue entry is created for AlternatePhone
- **And** the modal closes with success message "SMS queued for delivery"

### AC4: Empty Message Validation
- **Given** the message textarea is empty
- **When** I click "Send"
- **Then** I see error "Message cannot be empty"

### AC5: No Phone Number
- **Given** a student with no ParentPhone
- **Then** the "Send SMS to Parent" button is disabled
- **And** tooltip shows "No parent phone number on file"

### AC6: SMS Disabled Globally
- **Given** SMS is globally disabled in SmsSettings
- **When** I click "Send SMS to Parent"
- **Then** I see error "SMS is currently disabled. Enable it in SMS Settings."

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Message exactly 160 chars | No warning, counter shows "160 / 160" |
| Message 161+ chars | Warning about multi-SMS, still allow send |
| Student has ParentPhone but no AlternatePhone | Single SMS queued |
| Student deactivated | Button still visible (admin may need to contact parent) |
| Network error on submit | Show error, preserve message text |
| Concurrent send by two admins | Both SMS queued (no dedup for personal) |

---

## Test Scenarios

- [ ] Button visible when student has ParentPhone
- [ ] Button disabled when no ParentPhone
- [ ] Modal opens with correct student info
- [ ] Character counter updates in real-time
- [ ] Over-160 warning displayed
- [ ] SMS queued to ParentPhone on send
- [ ] SMS queued to AlternatePhone if set
- [ ] MessageType is "PERSONAL"
- [ ] Empty message rejected
- [ ] Modal closes on success
- [ ] SMS disabled error shown when globally off

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0041](US0041-sms-queue.md) | Functional | SmsQueue processing | Ready |
| [US0016](US0016-edit-student.md) | Functional | Student detail page | Ready |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
