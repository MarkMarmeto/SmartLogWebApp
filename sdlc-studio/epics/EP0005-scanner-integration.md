# EP0005: Scanner Integration

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 2

## Summary

Enable WPF scanner devices to authenticate with the server and submit scan data via REST API. This includes device registration, API key management, and idempotent scan ingestion endpoints.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Security | Device authentication via API keys | Per-device key management |
| PRD | Data | Idempotent scan ingestion | Duplicate detection logic |
| TRD | Architecture | REST API with JSON | API design patterns |
| TRD | Security | HTTPS required | TLS configuration |

---

## Business Context

### Problem Statement
WPF scanner devices at school gates need to communicate scan data to the central server. The system must handle offline scenarios (scanner queues locally) and prevent duplicate entries when connectivity is restored.

**PRD Reference:** [Features FT-007, FT-008](../prd.md#3-feature-inventory)

### Value Proposition
- Automated attendance tracking via QR scans
- Offline-capable scanners ensure reliability
- Idempotent API prevents duplicate records
- Per-device authentication enables revocation if device is compromised

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Scan ingestion latency | N/A | < 200ms | API metrics |
| Duplicate scan rejection rate | N/A | 100% | API logs |
| Device uptime | N/A | > 99% | Monitoring |

---

## Scope

### In Scope
- Device registration endpoint (admin-authenticated)
- Device list management (view, revoke)
- API key generation and storage
- Scan ingestion endpoint (POST /api/v1/scans)
- QR code validation (HMAC verification)
- Duplicate scan detection (same QR + device + 5-min window)
- Health check endpoint
- API error responses in standard format

### Out of Scope
- WPF Scanner application (separate project)
- Real-time scan notifications (use polling or Phase 3)
- Scanner firmware updates
- Offline queue management (handled by scanner app)

### Affected Personas
- **Tech-Savvy Tony (Super Admin):** Registers and manages scanner devices
- **Guard Gary (Security):** Uses scanner device (indirect interaction with API)

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can register a new scanner device
- [ ] System generates unique API key for each device
- [ ] Admin can view list of registered devices
- [ ] Admin can revoke a device's API key
- [ ] Scanner can submit scans via POST /api/v1/scans with API key
- [ ] API validates QR code HMAC signature
- [ ] API rejects invalid/tampered QR codes with clear error
- [ ] API detects and rejects duplicate scans (returns original response)
- [ ] API returns student name on successful scan
- [ ] Health check endpoint returns 200 OK
- [ ] All API calls are logged for audit

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0001: Authentication & Authorization | Epic | Not Started | Development |
| EP0003: Student Management | Epic | Not Started | Development |
| HMAC Secret Key Distribution | Process | Not Started | Tony |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0006: Attendance Tracking | Epic | Scans feed attendance dashboard |
| WPF Scanner App | External | Needs API to send scans |

---

## Risks & Assumptions

### Assumptions
- Scanner devices have network access to server (LAN)
- HMAC secret key can be securely deployed to scanners
- 5-minute duplicate window is appropriate

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| API key leaked | Low | High | Key revocation, audit logs |
| High volume during peak times | Medium | Medium | Optimize API, consider async |
| Network connectivity issues | Medium | Medium | Scanner offline queue |

---

## Technical Considerations

### Architecture Impact
- New API controller for scanner endpoints
- Device entity for registered scanners
- Scan entity for ingested scan records
- API key storage (hashed like passwords)

### Integration Points
- EP0003: QR code validation uses HMAC key
- Database: Device, Scan tables
- WPF Scanner: REST API consumer

---

## Sizing

**Story Points:** 17
**Estimated Story Count:** 6

**Complexity Factors:**
- API design and documentation
- HMAC verification logic
- Idempotency implementation
- API key security

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0028](../stories/US0028-register-scanner.md) | Register Scanner Device | 3 | Done |
| [US0029](../stories/US0029-device-list.md) | Device List and Revocation | 3 | Done |
| [US0030](../stories/US0030-scan-ingestion-api.md) | Scan Ingestion API | 5 | Done |
| [US0031](../stories/US0031-qr-validation.md) | QR Code Validation | 3 | Done |
| [US0032](../stories/US0032-duplicate-detection.md) | Duplicate Scan Detection | 2 | Done |
| [US0033](../stories/US0033-health-check.md) | Health Check Endpoint | 1 | Done |

**Total:** 17 story points across 6 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0005`

---

## Open Questions

- [ ] What is the exact duplicate detection window (5 minutes proposed)? - Owner: Product
- [ ] Should we support batch scan submission? - Owner: Development
- [ ] How will HMAC key be distributed to scanner devices? - Owner: Tony

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
