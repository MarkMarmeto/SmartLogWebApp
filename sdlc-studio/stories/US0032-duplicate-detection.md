# US0032: Duplicate Scan Detection

> **Status:** Done
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** System
**I want** to detect and handle duplicate scan submissions
**So that** attendance records are accurate even when scanners retry failed requests

## Context

### Technical Context
Scanner devices may retry submissions when network is unstable. The API must be idempotent - submitting the same scan twice should not create duplicate records.

---

## Acceptance Criteria

### AC1: Duplicate Detection Window
- **Given** a scan is submitted for student "STU-2026-001" from device "Scanner-1"
- **When** the same QR code is scanned on the same device within 5 minutes
- **Then** the second scan is identified as a duplicate

### AC2: Duplicate Response
- **Given** a duplicate scan is detected
- **Then** return 200 OK (not error) with:
  ```json
  {
    "scanId": "original-scan-guid",
    "studentId": "STU-2026-001",
    "studentName": "Maria Santos",
    "grade": "5",
    "section": "A",
    "scanType": "ENTRY",
    "scannedAt": "2026-02-04T08:15:00Z",
    "status": "DUPLICATE",
    "originalScanId": "original-scan-guid",
    "message": "Already scanned. Please proceed."
  }
  ```
- **And** no new scan record is created

### AC2a: Scanner Display Message
- **Given** a duplicate scan is detected
- **Then** the scanner device should display: "Already scanned. Please proceed."
- **And** use a distinct color (e.g., yellow/amber) to differentiate from success (green) or error (red)

### AC3: Different Device Not Duplicate
- **Given** student "STU-2026-001" scans at "Main Gate Scanner"
- **When** the same student scans at "Side Gate Scanner" within 5 minutes
- **Then** both scans are recorded (different devices)
- **And** status is "ACCEPTED" for both

### AC4: Outside Window Not Duplicate
- **Given** student "STU-2026-001" scans at 8:00 AM
- **When** the same student scans again at 8:06 AM (6 minutes later)
- **Then** both scans are recorded (outside 5-minute window)

### AC5: Different Scan Type Not Duplicate
- **Given** student "STU-2026-001" has an ENTRY scan at 8:00 AM
- **When** the same student has an EXIT scan at 8:02 AM
- **Then** both scans are recorded (different scan types)

### AC6: Idempotent Processing
- **Given** a network timeout occurs after the server processes a scan
- **When** the scanner retries the exact same request
- **Then** the duplicate is detected by matching QrPayload + DeviceId + approximate time
- **And** the original scan response is returned

### AC7: Duplicate Detection Index
- **Given** the system needs to check for duplicates quickly
- **Then** a database index exists on (DeviceId, StudentId, ScannedAt)
- **And** duplicate check query executes in < 10ms

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Exact same timestamp | Duplicate (same request retried) |
| 1 second apart, same device | Duplicate (network retry) |
| 4:59 apart, same device | Duplicate (within window) |
| 5:01 apart, same device | Not duplicate (new scan) |
| Same student, different devices | Not duplicate |
| Clock skew between devices | Each device evaluated independently |
| Rapid legitimate scans | Accept all (e.g., scan in/out quickly) |

---

## Test Scenarios

- [ ] Same scan within 5 minutes returns DUPLICATE
- [ ] Original scan ID returned for duplicates
- [ ] No new database record for duplicates
- [ ] Different device creates new record
- [ ] Outside 5-minute window creates new record
- [ ] Different scan type creates new record
- [ ] Duplicate check is performant (< 10ms)
- [ ] Network retry scenario handled correctly
- [ ] Rapid ENTRY then EXIT both recorded
- [ ] 200 OK returned for duplicates (not error)
- [ ] "Already scanned" message included in response
- [ ] Scanner shows distinct color for duplicate (amber)

---

## Technical Notes

### Duplicate Detection Query
```sql
SELECT TOP 1 *
FROM Scans
WHERE DeviceId = @DeviceId
  AND StudentId = @StudentId
  AND ScanType = @ScanType
  AND ScannedAt >= DATEADD(MINUTE, -5, @ScannedAt)
  AND ScannedAt <= DATEADD(MINUTE, 5, @ScannedAt)
ORDER BY ScannedAt DESC
```

### Why 5 Minutes?
- Long enough to handle network retries and temporary outages
- Short enough that legitimate re-scans (forgot something, came back) are recorded
- Configurable via app settings if needed

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0030](US0030-scan-ingestion-api.md) | Integration | Part of scan processing | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Stakeholder Decisions

- [x] Show "Already scanned. Please proceed." message - **Approved by Security Head Sergio**
- [x] Use distinct amber color on scanner for duplicate scans

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Added user-friendly duplicate message |
