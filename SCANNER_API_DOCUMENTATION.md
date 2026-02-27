# SmartLog Scanner API Documentation

## Overview
This document describes the Scan Submission API endpoint that scanner devices use to submit attendance scans.

**Base URL:** `https://your-server-domain/api/v1`
**Endpoint:** `POST /scans`
**Authentication:** API Key (via `X-API-Key` header)

---

## API Request

### Headers
```http
X-API-Key: your-device-api-key-here
Content-Type: application/json
```

### Request Body
```json
{
  "qrPayload": "SMARTLOG:2024-001:1739512547:base64HmacSignature==",
  "scannedAt": "2026-02-16T10:30:00Z",
  "scanType": "ENTRY"
}
```

#### Field Descriptions
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `qrPayload` | string | Yes | Complete QR code payload from student ID card |
| `scannedAt` | datetime | Yes | ISO 8601 timestamp when scan occurred |
| `scanType` | string | Yes | Either "ENTRY" or "EXIT" |

---

## API Responses

### 1. ✅ SUCCESS - Scan Accepted

**HTTP Status:** `200 OK`

**Response Body:**
```json
{
  "scanId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "studentId": "2024-001",
  "studentName": "Juan Dela Cruz",
  "grade": "Grade 7",
  "section": "Section A",
  "scanType": "ENTRY",
  "scannedAt": "2026-02-16T10:30:00Z",
  "status": "ACCEPTED"
}
```

**Scanner App Action:**
- ✅ Display green success screen
- 🔊 Play success beep/sound
- 📝 Show student name, grade, and section
- ⏱️ Display for 3 seconds

---

### 2. 🔄 DUPLICATE - Already Scanned

**HTTP Status:** `200 OK`

**Response Body:**
```json
{
  "scanId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "studentId": "2024-001",
  "studentName": "Juan Dela Cruz",
  "grade": "Grade 7",
  "section": "Section A",
  "scanType": "ENTRY",
  "scannedAt": "2026-02-16T10:30:00Z",
  "status": "DUPLICATE",
  "originalScanId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "Already scanned. Please proceed."
}
```

**What This Means:**
- Student scanned the same QR code within 5 minutes
- This prevents accidental double-scans
- Student can proceed normally

**Scanner App Action:**
- 🟡 Display yellow/warning screen
- 🔊 Play neutral beep
- 📝 Show "Already scanned - You may proceed"
- ⏱️ Display for 2 seconds

---

## Error Responses (HTTP 400/401/404)

All error responses follow this format:
```json
{
  "error": "ErrorCode",
  "message": "Human-readable error message",
  "status": "REJECTED",
  "studentId": "2024-001"  // Optional: only if student was found
}
```

---

### 3. ❌ Invalid or Missing API Key

**HTTP Status:** `401 Unauthorized`

**Response Body:**
```json
{
  "error": "InvalidApiKey",
  "message": "Invalid or missing API key"
}
```

**Possible Causes:**
- No `X-API-Key` header provided
- Wrong API key
- API key format incorrect

**Scanner App Action:**
- 🔴 Display critical error screen
- 📝 Show "Scanner not authorized - Contact administrator"
- 🚫 Disable scanning until reconfigured
- 📊 Log this error for troubleshooting

---

### 4. ❌ Device Revoked

**HTTP Status:** `401 Unauthorized`

**Response Body:**
```json
{
  "error": "DeviceRevoked",
  "message": "Device has been revoked"
}
```

**What This Means:**
- This scanner device has been disabled by an administrator
- Scanner cannot be used until reactivated

**Scanner App Action:**
- 🔴 Display critical error screen
- 📝 Show "Scanner disabled - Contact administrator"
- 🚫 Disable scanning completely
- 📊 Log this error

---

### 5. ❌ Invalid QR Code Format

**HTTP Status:** `400 Bad Request`

**Response Body:**
```json
{
  "error": "InvalidQrCode",
  "message": "QR code format is invalid",
  "status": "REJECTED"
}
```

**Possible Causes:**
- QR code doesn't match format: `SMARTLOG:studentId:timestamp:signature`
- Not a SmartLog QR code
- Corrupted or damaged QR code
- HMAC signature verification failed (tampered QR code)

**Scanner App Action:**
- ❌ Display red error screen
- 🔊 Play error beep
- 📝 Show "Invalid QR Code - Not a valid SmartLog ID"
- ⏱️ Display for 3 seconds

---

### 6. ❌ QR Code Invalidated (Revoked)

**HTTP Status:** `400 Bad Request`

**Response Body:**
```json
{
  "error": "QrCodeInvalidated",
  "message": "QR code has been invalidated. Student needs a new ID card.",
  "status": "REJECTED"
}
```

**What This Means:**
- Admin regenerated this student's QR code
- This old QR code can NEVER be used again
- Student must get a new ID card from the office

**Scanner App Action:**
- ❌ Display red error screen
- 🔊 Play error beep (longer/distinct sound)
- 📝 Show "INVALID ID CARD - Get new ID from office"
- ⏱️ Display for 5 seconds
- 📸 Optional: Take photo for verification

---

### 7. ❌ Student Not Found

**HTTP Status:** `404 Not Found`

**Response Body:**
```json
{
  "error": "StudentNotFound",
  "message": "Student not found",
  "status": "REJECTED"
}
```

**Possible Causes:**
- Student ID doesn't exist in database
- Student was deleted
- Wrong student ID in QR code

**Scanner App Action:**
- ❌ Display red error screen
- 🔊 Play error beep
- 📝 Show "Student not found - Contact office"
- ⏱️ Display for 4 seconds

---

### 8. ❌ Student Inactive

**HTTP Status:** `400 Bad Request`

**Response Body:**
```json
{
  "error": "StudentInactive",
  "message": "Student is not active",
  "studentId": "2024-001",
  "status": "REJECTED"
}
```

**What This Means:**
- Student account has been deactivated by administrator
- Common reasons: transferred, graduated, suspended

**Scanner App Action:**
- ❌ Display red error screen
- 🔊 Play error beep
- 📝 Show "Student account inactive - Contact office"
- 📝 Display student ID
- ⏱️ Display for 4 seconds

---

### 9. ❌ Not a School Day

**HTTP Status:** `400 Bad Request`

**Response Body:**
```json
{
  "error": "NotSchoolDay",
  "message": "School is closed: National Holiday",
  "studentId": "2024-001",
  "status": "REJECTED"
}
```

**What This Means:**
- Scan occurred on a non-school day (weekend, holiday, etc.)
- Calendar events like holidays, breaks, or suspensions

**Scanner App Action:**
- 🟠 Display orange info screen
- 🔊 Play neutral beep
- 📝 Show the reason (e.g., "School is closed: National Holiday")
- ⏱️ Display for 4 seconds
- 💡 Optional: Show calendar info

---

## Quick Reference: Response Codes

| HTTP Status | Error Code | Meaning | Action Required |
|------------|------------|---------|-----------------|
| 200 | (status: ACCEPTED) | ✅ Success | Show green screen |
| 200 | (status: DUPLICATE) | 🔄 Already scanned | Show yellow warning |
| 400 | InvalidQrCode | ❌ Wrong format/tampered | Show error - Invalid QR |
| 400 | QrCodeInvalidated | ❌ QR revoked | Show error - Get new ID |
| 400 | StudentInactive | ❌ Account disabled | Show error - Contact office |
| 400 | NotSchoolDay | 🟠 Holiday/weekend | Show info - Not school day |
| 401 | InvalidApiKey | 🔴 Wrong credentials | Disable scanner |
| 401 | DeviceRevoked | 🔴 Device disabled | Disable scanner |
| 404 | StudentNotFound | ❌ Unknown student | Show error - Contact office |

---

## Error Handling Best Practices

### 1. Display Hierarchy
```
Critical Errors (401)     → Full screen, disable scanner
Rejection Errors (400/404) → Error screen, allow retry
Warnings (DUPLICATE)       → Warning screen, auto-dismiss
Success (ACCEPTED)         → Success screen, auto-dismiss
```

### 2. Visual Feedback
- ✅ **Success:** Green background, checkmark icon, student name
- 🔄 **Duplicate:** Yellow/orange background, warning icon
- ❌ **Error:** Red background, X icon, clear message
- 🔴 **Critical:** Red background, lock icon, disable scanning

### 3. Audio Feedback
- ✅ **Success:** Pleasant beep (e.g., 400Hz, 200ms)
- 🔄 **Duplicate:** Neutral tone (e.g., 500Hz, 150ms)
- ❌ **Error:** Error buzz (e.g., 200Hz, 300ms)
- 🔴 **Critical:** Distinct error (e.g., 200Hz, 500ms, or two beeps)

### 4. Logging
Log all scan attempts locally:
- Timestamp
- QR payload (first 20 chars only)
- Response status
- Error code (if any)
- Student ID (if available)

This helps troubleshooting and provides offline backup.

---

## Example Implementation (Pseudocode)

```csharp
async Task ProcessScanAsync(string qrPayload)
{
    try
    {
        var request = new ScanRequest
        {
            QrPayload = qrPayload,
            ScannedAt = DateTime.UtcNow,
            ScanType = currentScanMode // "ENTRY" or "EXIT"
        };

        var response = await httpClient.PostAsync(
            "/api/v1/scans",
            request,
            headers: new { "X-API-Key", deviceApiKey }
        );

        if (response.IsSuccessStatusCode)
        {
            var data = await response.ReadAsAsync<ScanResponse>();

            if (data.Status == "ACCEPTED")
            {
                ShowSuccessScreen(data.StudentName, data.Grade, data.Section);
                PlaySuccessSound();
            }
            else if (data.Status == "DUPLICATE")
            {
                ShowDuplicateWarning(data.StudentName);
                PlayNeutralSound();
            }
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var error = await response.ReadAsAsync<ErrorResponse>();

            if (error.Error == "InvalidApiKey")
            {
                ShowCriticalError("Scanner not authorized");
                DisableScanning();
            }
            else if (error.Error == "DeviceRevoked")
            {
                ShowCriticalError("Scanner has been disabled");
                DisableScanning();
            }
        }
        else if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.ReadAsAsync<ErrorResponse>();

            switch (error.Error)
            {
                case "QrCodeInvalidated":
                    ShowError("INVALID ID CARD",
                             "Please get new ID from office");
                    PlayLongErrorSound();
                    break;

                case "StudentInactive":
                    ShowError("Student Inactive",
                             "Contact office");
                    PlayErrorSound();
                    break;

                case "NotSchoolDay":
                    ShowInfo("Not School Day", error.Message);
                    PlayNeutralSound();
                    break;

                case "InvalidQrCode":
                    ShowError("Invalid QR Code",
                             "Not a valid SmartLog ID");
                    PlayErrorSound();
                    break;
            }
        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var error = await response.ReadAsAsync<ErrorResponse>();
            ShowError("Student Not Found", "Contact office");
            PlayErrorSound();
        }

        // Log scan attempt
        await LogScanAttempt(qrPayload, response);
    }
    catch (HttpRequestException ex)
    {
        ShowNetworkError("Cannot connect to server");
        await LogOffline(qrPayload); // Queue for retry
    }
}
```

---

## Testing Checklist

Test each scenario with your scanner app:

- [ ] ✅ Successful scan (ENTRY)
- [ ] ✅ Successful scan (EXIT)
- [ ] 🔄 Duplicate scan (scan same QR twice within 5 min)
- [ ] ❌ Invalid API key
- [ ] ❌ Revoked device
- [ ] ❌ Invalid QR code format
- [ ] ❌ Invalidated QR code (regenerated)
- [ ] ❌ Student not found
- [ ] ❌ Inactive student
- [ ] 🟠 Not a school day
- [ ] 📡 Network error / timeout
- [ ] 🔌 Server offline

---

## Security Notes

1. **Store API Key Securely:** Never hardcode the API key in the app. Store it in secure storage.

2. **HTTPS Only:** Always use HTTPS endpoints in production to prevent man-in-the-middle attacks.

3. **Certificate Validation:** Validate SSL/TLS certificates. Don't disable certificate validation in production.

4. **Rate Limiting:** Implement local rate limiting to prevent abuse (e.g., max 1 scan per second).

5. **QR Payload Logging:** Only log first 20 characters of QR payload for privacy.

---

## Support

For API issues or questions:
- Check server logs for detailed error information
- Verify network connectivity
- Ensure scanner device is registered and active in admin panel
- Contact system administrator with error codes and timestamps

---

**Document Version:** 1.0
**Last Updated:** 2026-02-16
**API Version:** v1
