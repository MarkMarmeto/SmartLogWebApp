# SmartLog Scanner API - Quick Reference

## Endpoint
```
POST https://your-server/api/v1/scans
Header: X-API-Key: {your-device-api-key}
```

## Request Format
```json
{
  "qrPayload": "SMARTLOG:2024-001:1739512547:signature==",
  "scannedAt": "2026-02-16T10:30:00Z",
  "scanType": "ENTRY"
}
```

---

## Response Summary Table

| Status | Code | Error | Display | Color | Sound |
|--------|------|-------|---------|-------|-------|
| 200 | ACCEPTED | - | "Welcome [Name]" | 🟢 Green | ✅ Success beep |
| 200 | DUPLICATE | - | "Already scanned" | 🟡 Yellow | 🔔 Neutral |
| 400 | - | InvalidQrCode | "Invalid QR Code" | 🔴 Red | ❌ Error buzz |
| 400 | - | QrCodeInvalidated | "Get new ID card" | 🔴 Red | ❌ Long buzz |
| 400 | - | StudentInactive | "Account inactive" | 🔴 Red | ❌ Error buzz |
| 400 | - | NotSchoolDay | "School closed: [reason]" | 🟠 Orange | 🔔 Neutral |
| 401 | - | InvalidApiKey | "Scanner not authorized" | 🔴 Red | 🚫 Disable |
| 401 | - | DeviceRevoked | "Scanner disabled" | 🔴 Red | 🚫 Disable |
| 404 | - | StudentNotFound | "Student not found" | 🔴 Red | ❌ Error buzz |

---

## Response Examples

### ✅ Success
```json
{
  "scanId": "abc-123",
  "studentId": "2024-001",
  "studentName": "Juan Dela Cruz",
  "grade": "Grade 7",
  "section": "Section A",
  "scanType": "ENTRY",
  "scannedAt": "2026-02-16T10:30:00Z",
  "status": "ACCEPTED"
}
```

### 🔄 Duplicate
```json
{
  "status": "DUPLICATE",
  "studentName": "Juan Dela Cruz",
  "message": "Already scanned. Please proceed."
}
```

### ❌ Error
```json
{
  "error": "QrCodeInvalidated",
  "message": "QR code has been invalidated. Student needs a new ID card.",
  "status": "REJECTED"
}
```

---

## Critical Errors (Disable Scanner)

These errors mean the scanner cannot function:

1. **InvalidApiKey** (401) → Wrong credentials
2. **DeviceRevoked** (401) → Device disabled by admin

**Action:** Show error, disable scanning, contact administrator

---

## Student-Related Errors

These errors are temporary, allow retry:

1. **QrCodeInvalidated** → Student needs new ID card
2. **StudentInactive** → Student account disabled
3. **StudentNotFound** → Unknown student
4. **InvalidQrCode** → Wrong QR format or tampered
5. **NotSchoolDay** → Holiday/weekend (informational)

**Action:** Show error, allow next scan

---

## Implementation Checklist

- [ ] Send `X-API-Key` header with every request
- [ ] Use HTTPS in production
- [ ] Parse response `status` field (ACCEPTED/DUPLICATE/REJECTED)
- [ ] Handle all error codes in switch/case
- [ ] Display student name on success
- [ ] Show clear error messages
- [ ] Play audio feedback
- [ ] Log all scan attempts locally
- [ ] Implement offline queue (optional)
- [ ] Test with invalid QR codes
- [ ] Test with revoked device
- [ ] Test duplicate scans

---

## Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| 401 InvalidApiKey | Wrong key or missing header | Check API key configuration |
| 401 DeviceRevoked | Admin disabled scanner | Contact admin to reactivate |
| 400 QrCodeInvalidated | QR was regenerated | Tell student to get new ID |
| 400 InvalidQrCode | Wrong format or tampered | Verify QR code is from SmartLog |
| 404 StudentNotFound | Student deleted/not exists | Contact administrator |
| 400 NotSchoolDay | Holiday/weekend | Normal - just inform user |
| Connection refused | Server offline | Check network/server status |

---

## Test QR Payload Format

```
SMARTLOG:{studentId}:{unixTimestamp}:{hmacSignatureBase64}

Example:
SMARTLOG:2024-001:1739512547:Zx8fK3mL9n/Qp+7RsT4Uv==
```

Parts:
1. **Prefix:** Always "SMARTLOG"
2. **Student ID:** School's student identifier
3. **Timestamp:** Unix timestamp (seconds since 1970)
4. **Signature:** HMAC-SHA256 Base64 encoded

---

## Sample Code Snippet

```csharp
// Simple error handler
switch (response.StatusCode)
{
    case 200:
        if (data.Status == "ACCEPTED")
            ShowSuccess(data.StudentName);
        else if (data.Status == "DUPLICATE")
            ShowWarning("Already scanned");
        break;

    case 400:
        var error = ParseError(response);
        if (error.Error == "QrCodeInvalidated")
            ShowError("GET NEW ID CARD FROM OFFICE");
        else if (error.Error == "StudentInactive")
            ShowError("Student account inactive - Contact office");
        else if (error.Error == "NotSchoolDay")
            ShowInfo(error.Message);
        else
            ShowError(error.Message);
        break;

    case 401:
        ShowCritical("Scanner not authorized");
        DisableScanner();
        break;

    case 404:
        ShowError("Student not found");
        break;
}
```

---

**Version:** 1.0 | **Date:** 2026-02-16
