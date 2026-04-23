# US0033: Health Check Endpoint

> **Status:** Done
> **Epic:** [EP0005: Scanner Integration](../epics/EP0005-scanner-integration.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Scanner Device
**I want** to check if the server is healthy and available
**So that** I know when to queue scans locally vs submit immediately

## Context

### Technical Context
Scanner devices need to know if the server is reachable and functioning before attempting to submit scans. This enables graceful degradation to offline mode.

---

## Acceptance Criteria

### AC1: Health Check Endpoint
- **Given** the server is running
- **When** a GET request is made to `/api/v1/health`
- **Then** return 200 OK with:
  ```json
  {
    "status": "healthy",
    "timestamp": "2026-02-04T08:00:00Z",
    "version": "1.0.0"
  }
  ```

### AC2: No Authentication Required
- **Given** a request to `/api/v1/health`
- **Then** no API key or authentication is required
- **And** the endpoint is publicly accessible on the LAN

### AC3: Database Connectivity Check
- **Given** the health check is called
- **When** the database is unreachable
- **Then** return 503 Service Unavailable with:
  ```json
  {
    "status": "unhealthy",
    "timestamp": "2026-02-04T08:00:00Z",
    "error": "Database connection failed"
  }
  ```

### AC4: Fast Response
- **Given** a health check request
- **Then** response time is < 100ms under normal conditions
- **And** the check does minimal work (simple query)

### AC5: Detailed Health Check (Authenticated)
- **Given** a GET request to `/api/v1/health/details` with valid API key
- **Then** return detailed health information:
  ```json
  {
    "status": "healthy",
    "timestamp": "2026-02-04T08:00:00Z",
    "version": "1.0.0",
    "database": {
      "status": "healthy",
      "latencyMs": 5
    },
    "uptime": "2d 5h 30m",
    "activeScanners": 3,
    "scansToday": 1250
  }
  ```

### AC6: Scanner Connectivity Test
- **Given** a scanner device starts up
- **When** it calls the health check endpoint
- **Then** it can determine if the server is reachable
- **And** if healthy, begin submitting queued scans

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Server starting up | May return 503 briefly |
| Database slow | Include in response, don't fail |
| High load | Health check still responds quickly |
| Network partition | Scanner sees timeout, goes offline |
| Invalid endpoint path | 404 Not Found |
| POST instead of GET | 405 Method Not Allowed |

---

## Test Scenarios

- [ ] Health check returns 200 when healthy
- [ ] Health check returns 503 when database down
- [ ] No authentication required for basic health
- [ ] Response time under 100ms
- [ ] Detailed health requires API key
- [ ] Detailed health shows database latency
- [ ] Version number included in response
- [ ] Timestamp is current UTC time
- [ ] Uptime calculated correctly
- [ ] Active scanner count accurate

---

## Technical Notes

### Health Check Query
```sql
SELECT 1  -- Simple connectivity test
```

### Response Headers
```
Cache-Control: no-cache, no-store
X-Response-Time: 5ms
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| None | - | Standalone endpoint | - |

---

## Estimation

**Story Points:** 1
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
