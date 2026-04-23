# US0030: Scan Ingestion API

> **Status:** Done
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Scanner Device
**I want** to submit student QR code scans to the server via REST API
**So that** attendance can be tracked in real-time

## Context

### Persona Reference
**Guard Gary** - Security guard who operates the scanner device (indirect user).
[Full persona details](../personas.md#4-guard-gary-security-personnel)

---

## Acceptance Criteria

### AC1: Scan Submission Endpoint
- **Given** a registered scanner device
- **When** it sends POST to `/api/v1/scans` with:
  ```json
  {
    "qrPayload": "SMARTLOG:STU-2026-001:1706918400:abc123hmac",
    "scannedAt": "2026-02-04T08:15:00Z",
    "scanType": "ENTRY"
  }
  ```
- **Then** the scan is processed and response returned

### AC2: API Key Authentication
- **Given** a request to `/api/v1/scans`
- **When** the `X-API-Key` header is missing or invalid
- **Then** return 401 Unauthorized with:
  ```json
  {
    "error": "InvalidApiKey",
    "message": "Invalid or missing API key"
  }
  ```

### AC3: Successful Scan Response
- **Given** a valid scan submission
- **When** the QR code is valid and student exists
- **Then** return 200 OK with:
  ```json
  {
    "scanId": "guid-here",
    "studentId": "STU-2026-001",
    "studentName": "Maria Santos",
    "grade": "5",
    "section": "A",
    "scanType": "ENTRY",
    "scannedAt": "2026-02-04T08:15:00Z",
    "status": "ACCEPTED"
  }
  ```

### AC4: Scan Record Created
- **Given** a successful scan submission
- **Then** a scan record is created with:
  - ScanId (GUID)
  - DeviceId (from API key lookup)
  - StudentId (from QR payload)
  - QrPayload
  - ScannedAt (from request)
  - ReceivedAt (server timestamp)
  - ScanType (ENTRY/EXIT)
  - Status (ACCEPTED)

### AC5: Invalid QR Code Response
- **Given** a scan with tampered or invalid QR code
- **Then** return 400 Bad Request with:
  ```json
  {
    "error": "InvalidQrCode",
    "message": "QR code signature is invalid",
    "status": "REJECTED"
  }
  ```
- **And** scan is logged with Status = REJECTED_INVALID_QR

### AC6: Deactivated Student Response
- **Given** a scan for a deactivated student
- **Then** return 400 Bad Request with:
  ```json
  {
    "error": "StudentInactive",
    "message": "Student is not active",
    "studentId": "STU-2026-001",
    "status": "REJECTED"
  }
  ```

### AC7: Invalidated QR Code Response
- **Given** a scan with a QR code that has been regenerated (old code)
- **Then** return 400 Bad Request with:
  ```json
  {
    "error": "QrCodeInvalidated",
    "message": "QR code has been invalidated. Student needs a new ID card.",
    "status": "REJECTED"
  }
  ```

### AC8: Rate Limiting
- **Given** a device submits more than 60 requests per minute
- **Then** return 429 Too Many Requests
- **And** include `Retry-After` header

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Missing required fields | 400 Bad Request with field errors |
| Invalid JSON body | 400 Bad Request |
| Device revoked | 401 Unauthorized |
| Student not found | 404 Not Found |
| Future scannedAt timestamp | Accept (device clock may be off) |
| Very old scannedAt timestamp | Accept but flag for review |
| Network timeout | Client should retry |
| Server error | 500 with error reference ID |

---

## Test Scenarios

- [ ] Valid scan returns 200 with student info
- [ ] Missing API key returns 401
- [ ] Invalid API key returns 401
- [ ] Revoked device returns 401
- [ ] Invalid QR code returns 400
- [ ] Deactivated student returns 400
- [ ] Invalidated QR code returns 400
- [ ] Scan record created in database
- [ ] Rate limiting enforced
- [ ] Missing required fields return 400
- [ ] Invalid JSON returns 400
- [ ] ENTRY and EXIT scan types accepted

---

## Technical Notes

### Request Headers
```
POST /api/v1/scans
Content-Type: application/json
X-API-Key: sk_live_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
```

### Scan Types
- `ENTRY` - Student entering school
- `EXIT` - Student leaving school

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0028](US0028-register-scanner.md) | Functional | Registered devices | Draft |
| [US0019](US0019-generate-qr.md) | Functional | QR code format | Draft |
| [US0015](US0015-create-student.md) | Functional | Students exist | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
