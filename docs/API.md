# SmartLog Web App — API Reference

**Base URL:** `http://{server}:{port}/api/v1`

---

## Authentication

### Scanner Device Authentication (API Key)

All scanner-facing endpoints require the `X-API-Key` header:

```http
X-API-Key: sk_live_xxxxxxxxxxxx
```

API keys are generated in **Admin > Register Device**. The key is shown once and stored as a SHA-256 hash — constant-time comparison prevents timing attacks. Revoked devices are rejected immediately.

### Admin/User Authentication (Cookie)

Dashboard, attendance, report, and profile picture endpoints use cookie-based session auth (10-hour sliding expiration). Log in at `/Account/Login`.

---

## Scanner API

### POST /api/v1/scans

Submit a QR code scan from a scanner device.

**Auth:** `X-API-Key` header

**Request:**
```http
POST /api/v1/scans
X-API-Key: sk_live_xxx
Content-Type: application/json

{
  "qrPayload": "SMARTLOG:2026-07-0001:1739512547:base64HmacSignature==",
  "scannedAt": "2026-02-16T10:30:00Z",
  "scanType": "ENTRY"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `qrPayload` | string | Yes | Full QR code string from student ID card |
| `scannedAt` | datetime (ISO 8601) | Yes | When the scan occurred |
| `scanType` | string | Yes | `"ENTRY"` or `"EXIT"` |

**Responses:**

**200 — Accepted**
```json
{
  "scanId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "studentId": "2026-07-0001",
  "studentName": "Juan Dela Cruz",
  "grade": "Grade 7",
  "section": "Section A",
  "scanType": "ENTRY",
  "scannedAt": "2026-02-16T10:30:00Z",
  "status": "ACCEPTED"
}
```

**200 — Duplicate** (already scanned within 5-minute window)
```json
{
  "scanId": "...",
  "studentId": "2026-07-0001",
  "studentName": "Juan Dela Cruz",
  "grade": "Grade 7",
  "section": "Section A",
  "scanType": "ENTRY",
  "scannedAt": "2026-02-16T10:30:00Z",
  "status": "DUPLICATE",
  "originalScanId": "...",
  "message": "Already scanned. Please proceed."
}
```

**400 / 401 / 404 — Error**
```json
{
  "error": "ErrorCode",
  "message": "Human-readable description",
  "status": "REJECTED",
  "studentId": "2026-07-0001"
}
```

| HTTP | Error Code | Meaning | Scanner Action |
|---|---|---|---|
| 401 | `InvalidApiKey` | Missing or wrong API key | Disable scanner, show critical error |
| 401 | `DeviceRevoked` | Device deactivated by admin | Disable scanner, show critical error |
| 400 | `InvalidQrCode` | Wrong format or HMAC mismatch | Show error — invalid ID |
| 400 | `QrCodeInvalidated` | QR regenerated; old code revoked | Show error — get new ID from office |
| 400 | `StudentInactive` | Student account deactivated | Show error — contact office |
| 400 | `NotSchoolDay` | Holiday, weekend, or suspension | Show info — school closed |
| 404 | `StudentNotFound` | Student ID not in database | Show error — contact office |

**Error display guidance for scanner apps:**

```
Critical (401)     → Full-screen error, disable scanning until reconfigured
Rejection (400/404) → Error screen, allow retry after auto-dismiss
Duplicate          → Amber warning screen, auto-dismiss after 2s
Accepted           → Green success screen, show student info, auto-dismiss after 3s
```

---

### GET /api/v1/health

Check server health. Used by scanner apps on startup and periodic polling.

**Auth:** None

**Response:** `200 OK` with body `Healthy`

---

## Dashboard API

All dashboard endpoints require cookie auth (`CanViewAttendance` policy).

### GET /api/v1/dashboard/summary

Overall attendance summary for today.

**Response:**
```json
{
  "totalStudents": 450,
  "presentToday": 312,
  "absentToday": 138,
  "attendanceRate": 69.3
}
```

---

### GET /api/v1/dashboard/attendance-trend

Attendance trend over the past N days.

**Query params:** `?days=30`

**Response:** Array of `{ date, presentCount, absentCount, attendanceRate }`

---

### GET /api/v1/dashboard/attendance-by-grade

Attendance breakdown by grade for a given date.

**Query params:** `?date=2026-02-16`

**Response:** Array of `{ grade, presentCount, totalStudents, attendanceRate }`

---

### GET /api/v1/dashboard/attendance-by-weekday

Average attendance by day of week over the past N weeks.

**Query params:** `?weeks=4`

**Response:** Array of `{ dayOfWeek, averageAttendanceRate }`

---

### GET /api/v1/dashboard/recent-activity

Most recent scan activity.

**Query params:** `?count=10`

**Response:** Array of recent scan objects.

---

## Attendance API

Requires cookie auth (`CanViewStudents` policy).

### GET /api/v1/attendance/summary

Attendance summary for a date, optionally filtered by grade/section.

**Query params:** `?date=2026-02-16&grade=Grade 7&section=Section A`

---

### GET /api/v1/attendance/list

Paginated list of attendance records.

**Query params:** `?date=&grade=&section=&search=&status=&page=1&pageSize=50`

---

## Reports API

Requires cookie auth (`CanViewStudents` policy). All export endpoints return file downloads.

### GET /api/v1/reports/daily/export

Export daily attendance report.

**Query params:** `?date=2026-02-16&grade=&section=&format=csv`

**Formats:** `csv`, `html`

---

### GET /api/v1/reports/weekly/export

**Query params:** `?startDate=2026-02-10&format=csv`

---

### GET /api/v1/reports/monthly/export

**Query params:** `?year=2026&month=2&format=csv`

---

### GET /api/v1/reports/student/{id}/export

Individual student attendance history.

**Query params:** `?format=csv`

---

### GET /api/v1/reports/audit-logs/export

Export audit log. Requires `RequireSuperAdmin` policy.

---

## Profile Picture API

Requires cookie auth.

### POST /api/v1/profile-picture/user

Upload profile picture for the currently logged-in user.

**Body:** `multipart/form-data` with `file` field.

---

### POST /api/v1/profile-picture/student/{id}

Upload profile picture for a student. Requires `CanManageStudents` policy.

---

### POST /api/v1/profile-picture/faculty/{id}

Upload profile picture for a faculty member. Requires `CanManageFaculty` policy.

---

### DELETE /api/v1/profile-picture/user

Delete the current user's profile picture.

---

### DELETE /api/v1/profile-picture/student/{id}

Delete a student's profile picture. Requires `CanManageStudents`.

---

### DELETE /api/v1/profile-picture/faculty/{id}

Delete a faculty member's profile picture. Requires `CanManageFaculty`.

---

## SMS Webhook

### POST /api/sms/delivery-report

Delivery status callback from SMS providers (Semaphore). Optional `X-Webhook-Secret` header for verification.

**Auth:** `X-Webhook-Secret` header (optional, configured in Admin > Settings)

**Body:** Provider-specific delivery report payload.

---

## QR Code Format Reference

```
SMARTLOG:{studentId}:{timestamp}:{HMAC-SHA256-base64}
```

Example:
```
SMARTLOG:2026-07-0001:1739512547:a7Bx9kL2mN4pQ6rS8tU0vW2xY4zA6bC8==
```

| Part | Description |
|---|---|
| `SMARTLOG` | Fixed prefix — identifies this as a SmartLog QR code |
| `studentId` | Student's ID (format: `YYYY-GG-NNNN`) |
| `timestamp` | Unix epoch seconds at QR generation time |
| `HMAC-SHA256-base64` | `HMAC-SHA256(studentId:timestamp)` signed with `SMARTLOG_HMAC_SECRET_KEY` |

The scanner app validates the HMAC signature locally, then submits the full raw `qrPayload` string to `POST /api/v1/scans`. The server re-validates the HMAC independently.

---

## Security Notes

1. **Store API keys in platform secure storage** — never hardcode in the scanner app.
2. **Use HTTPS in production** to prevent man-in-the-middle attacks.
3. **Log only the first 20 characters** of QR payloads to protect student data.
4. **Rate limit** scan submissions on the scanner side (e.g., 1 per second) to prevent accidental floods.
5. **API key hash comparison** uses constant-time `CryptographicOperations.FixedTimeEquals` — do not implement your own comparison.
