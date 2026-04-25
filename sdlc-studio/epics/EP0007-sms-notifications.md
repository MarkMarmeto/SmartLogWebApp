# EP0007: SMS Notifications

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 3

## Summary

Enable automated SMS notifications to parents when students enter or exit the school. Includes SMS template configuration, notification rules, and a background worker service for reliable delivery.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Integration | SMS gateway TBD | Provider abstraction needed |
| TRD | Architecture | Background Worker Service | Hosted service pattern |
| TRD | Data | SMS queue in database | Reliable delivery |

---

## Business Context

### Problem Statement
Parents want to know when their children arrive at and leave school safely. Manual notification is impractical. Automated SMS provides peace of mind and improves parent engagement.

**PRD Reference:** [Features FT-010, FT-011](../prd.md#3-feature-inventory)

### Value Proposition
- Parents receive real-time safety notifications
- Reduces parental anxiety about child's whereabouts
- Automated system requires no staff intervention
- Improves school-parent communication

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| SMS delivery rate | N/A | > 98% | SMS gateway reports |
| SMS delivery latency | N/A | < 60 seconds | Queue processing time |
| Parent satisfaction | N/A | > 90% | Parent survey |

---

## Scope

### In Scope
- SMS template management (entry, exit templates)
- Template variables: {StudentName}, {Time}, {Date}, {SchoolName}
- Notification rules (entry only, exit only, both)
- SMS queue table for reliable delivery
- Background worker service to process queue
- SMS delivery status tracking
- Admin view of SMS history and status
- Opt-out management per parent

### Out of Scope
- SMS provider selection and contract (business decision)
- Two-way SMS (parent replies)
- Push notifications (mobile app)
- Email notifications
- WhatsApp or other messaging platforms

### Affected Personas
- **Admin Amy (Administrator):** Configures templates and rules, views SMS history
- **Parents (Indirect):** Receive SMS notifications

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can create and edit SMS templates
- [ ] Templates support variables: {StudentName}, {Time}, {Date}, {SchoolName}
- [ ] Admin can set notification rules (entry, exit, or both)
- [ ] When student scans, SMS is queued for parent
- [ ] Background worker processes SMS queue
- [ ] Worker retries failed SMS up to 3 times
- [ ] Admin can view SMS history with delivery status
- [ ] Admin can enable/disable SMS for specific students
- [ ] System handles SMS gateway errors gracefully
- [ ] SMS queue survives server restart

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0005: Scanner Integration | Epic | Not Started | Development |
| EP0003: Student Management | Epic | Not Started | Development |
| SMS Gateway Provider | External | Not Started | Business |
| SMS Gateway API Credentials | Configuration | Not Started | Tony |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | SMS is end-of-pipeline feature |

---

## Risks & Assumptions

### Assumptions
- Parent phone numbers are valid mobile numbers
- SMS gateway provides delivery status callbacks
- School has budget for SMS costs
- Single SMS per event (no batching)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| SMS costs exceed budget | Medium | Medium | Monitoring, alerts, opt-out option |
| SMS gateway downtime | Low | Medium | Queue ensures eventual delivery |
| Invalid phone numbers | Medium | Low | Validation, delivery status tracking |
| Spam complaints from parents | Low | Medium | Clear opt-out, reasonable frequency |

---

## Technical Considerations

### Architecture Impact
- SmsQueue table for outbound messages
- SmsTemplate table for configurable templates
- SmsWorkerService (IHostedService)
- ISmsGateway interface for provider abstraction
- Retry logic with exponential backoff

### Integration Points
- EP0005: Triggered by scan events
- EP0003: Parent phone from student record
- External: SMS gateway API
- Database: SmsQueue, SmsTemplate, SmsLog tables

---

## Sizing

**Story Points:** 17
**Estimated Story Count:** 6

**Complexity Factors:**
- External integration (SMS gateway)
- Background worker reliability
- Template variable substitution
- Retry and error handling

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0039](../stories/US0039-sms-templates.md) | SMS Template Management | 2 | Done |
| [US0040](../stories/US0040-sms-rules.md) | SMS Notification Rules | 2 | Done |
| [US0041](../stories/US0041-sms-queue.md) | SMS Queue and Worker Service | 5 | Done |
| [US0042](../stories/US0042-sms-gateway.md) | SMS Gateway Integration | 3 | Done |
| [US0043](../stories/US0043-sms-history.md) | SMS History and Status | 3 | Done |
| [US0044](../stories/US0044-sms-optout.md) | SMS Opt-Out Management | 2 | Done |

**Total:** 17 story points across 6 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0007`

---

## Open Questions

- [ ] Which SMS gateway provider will be used? - Owner: Business
- [ ] What is the SMS budget per month? - Owner: Business
- [ ] Should parents be able to opt-out via SMS reply? - Owner: Product
- [ ] What happens if parent has multiple students? - Owner: Product

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
