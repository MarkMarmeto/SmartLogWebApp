# US0019: Generate Student QR Code

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** a secure QR code to be automatically generated for each student
**So that** their identity can be verified at school entry points

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages student identity credentials.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Automatic QR Generation on Student Create
- **Given** I create a new student "Maria Santos" with ID "STU-2026-001"
- **When** the student is saved successfully
- **Then** a QR code is automatically generated
- **And** the QR code is displayed on the student details page

### AC2: QR Code Format
- **Given** a QR code is generated for student "STU-2026-001"
- **Then** the QR code contains payload: `SMARTLOG:STU-2026-001:1706918400:abc123hmac`
- **Where** format is: `SMARTLOG:{studentId}:{issuedTimestamp}:{hmacSignature}`

### AC3: HMAC Signature
- **Given** a QR code is generated
- **Then** the HMAC is computed as: HMAC-SHA256(studentId + ":" + timestamp, secretKey)
- **And** the signature is Base64 encoded
- **And** the signature can be verified by the scanner

### AC4: QR Code Stored in Database
- **Given** a QR code is generated for a student
- **Then** the QR code record is stored with:
  - StudentId (FK)
  - Payload (full QR string)
  - HmacSignature
  - IssuedAt (timestamp)
  - IsValid (true)

### AC5: View QR Code
- **Given** I am on the student details page for "Maria Santos"
- **Then** I see the QR code image
- **And** I see the Student ID and student name
- **And** I see the issue date

---

## Scope

### In Scope
- QR code generation on student creation
- HMAC-SHA256 signing
- QR code storage
- QR code display on student details

### Out of Scope
- QR code scanning/verification (EP0005)
- QR code expiration
- Bulk QR generation

---

## Technical Notes

### QR Code Service
```csharp
public class QrCodeService : IQrCodeService
{
    public QrCodeResult GenerateQrCode(string studentId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{studentId}:{timestamp}";
        var hmac = ComputeHmac(payload, _secretKey);
        var qrContent = $"SMARTLOG:{studentId}:{timestamp}:{hmac}";
        var qrImage = GenerateQrImage(qrContent);
        return new QrCodeResult(qrContent, hmac, qrImage);
    }
}
```

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| HMAC key not configured | Fail with clear error message |
| QR generation library fails | Show error, allow retry |
| Very long Student ID | Truncate or reject at validation |
| Special characters in ID | Encode safely in QR |
| Database save fails after QR generated | Transaction rollback |
| Concurrent creation same student | First wins, second gets duplicate error |

---

## Test Scenarios

- [ ] QR code generated on student creation
- [ ] QR payload format is correct
- [ ] HMAC signature is valid and verifiable
- [ ] QR code stored in database
- [ ] QR code displayed on student details
- [ ] QR image is scannable
- [ ] HMAC uses correct secret key
- [ ] Timestamp is Unix epoch format
- [ ] IsValid flag set to true
- [ ] Audit log records QR generation

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Functional | Student creation flow | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| QRCoder NuGet package | Library | Not Started |
| HMAC Secret Key | Configuration | Not Started |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium-High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
