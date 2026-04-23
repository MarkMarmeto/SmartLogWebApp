# US0041: SMS Queue and Worker Service

> **Status:** Done
> **Epic:** [EP0007: SMS Notifications](../epics/EP0007-sms-notifications.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** System
**I want** to queue SMS messages and process them reliably
**So that** notifications are delivered even during temporary failures

## Context

### Technical Context
A background worker service processes queued SMS messages, ensuring reliable delivery with retry logic.

---

## Acceptance Criteria

### AC1: Queue SMS on Scan
- **Given** a student scans at the gate
- **When** SMS notifications are enabled
- **Then** an SMS record is queued with:
  - StudentId
  - ParentPhone (from student record)
  - Message (template with variables replaced)
  - ScanType (ENTRY/EXIT)
  - Status: "Pending"
  - QueuedAt: current timestamp

### AC2: Worker Service Processing
- **Given** there are pending SMS messages in the queue
- **Then** the worker service:
  - Polls the queue every 5 seconds
  - Processes messages in FIFO order
  - Sends via SMS gateway
  - Updates status to "Sent" or "Failed"

### AC3: Retry Failed Messages
- **Given** an SMS fails to send (gateway error)
- **Then** the message status is set to "Retry"
- **And** RetryCount is incremented
- **And** NextRetryAt is set with exponential backoff:
  - 1st retry: 1 minute
  - 2nd retry: 5 minutes
  - 3rd retry: 15 minutes

### AC4: Max Retry Limit
- **Given** an SMS has failed 3 times
- **Then** status is set to "Failed"
- **And** no more retries are attempted
- **And** FailureReason is recorded

### AC5: Queue Survives Restart
- **Given** the server restarts
- **Then** pending SMS messages remain in the queue
- **And** the worker resumes processing on startup

### AC6: Rate-Limited Processing for GSM Modem
- **Given** many messages are queued (e.g., during morning arrival rush)
- **And** GSM modem is the active provider
- **Then** the worker processes:
  - One message at a time (serial port limitation)
  - 3-second delay between messages (~20 SMS/minute max)
  - Priority queue: older messages first (FIFO)
- **And** logs estimated queue clear time during rush periods

### AC7: Skip Invalid Phone Numbers
- **Given** a student's parent phone is empty or invalid
- **Then** the SMS is marked "Skipped"
- **And** SkipReason: "Invalid phone number"
- **And** no gateway call is made

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Gateway timeout | Mark as Retry, exponential backoff |
| Gateway returns invalid number | Mark as Failed, no retry |
| Gateway rate limit exceeded | Pause processing, retry after cooldown |
| GSM modem throughput (~20/min) | Queue processes continuously, log estimated wait |
| Morning rush (500 students) | ~25 min to clear queue, acceptable delay |
| Duplicate scan (same student, same minute) | Only queue one SMS |
| Parent opted out | Skip with reason "Opted out" |
| Server crash during send | Message remains Pending, retried on restart |
| Queue grows very large | Process in batches, log warning |

---

## Test Scenarios

- [ ] SMS queued when student scans
- [ ] Worker processes pending messages
- [ ] Successful send updates status to Sent
- [ ] Failed send triggers retry
- [ ] Exponential backoff timing correct
- [ ] Max 3 retries then Failed
- [ ] Queue survives server restart
- [ ] Batch processing works
- [ ] Invalid phone numbers skipped
- [ ] Opted-out parents skipped
- [ ] Duplicate scans don't duplicate SMS
- [ ] Gateway rate limits respected

---

## Technical Notes

### SMS Queue Table Schema
```sql
CREATE TABLE SmsQueue (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    StudentId UNIQUEIDENTIFIER NOT NULL,
    ParentPhone NVARCHAR(20) NOT NULL,
    Message NVARCHAR(500) NOT NULL,
    ScanType NVARCHAR(10) NOT NULL,
    Status NVARCHAR(20) NOT NULL, -- Pending, Sent, Failed, Retry, Skipped
    QueuedAt DATETIME2 NOT NULL,
    SentAt DATETIME2 NULL,
    RetryCount INT DEFAULT 0,
    NextRetryAt DATETIME2 NULL,
    FailureReason NVARCHAR(500) NULL,
    GatewayMessageId NVARCHAR(100) NULL
)
```

### Worker Service Pattern
```csharp
public class SmsWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingMessagesAsync();
            await Task.Delay(3000, stoppingToken); // 3 sec for GSM rate limit
        }
    }
}
```

### GSM Modem Throughput Calculation
- **Rate:** ~20 SMS/minute (3-second delay between sends)
- **Morning rush:** 500 students arrive over 30 minutes
- **Queue time:** ~25 minutes to send all entry notifications
- **Acceptable:** Parents receive SMS within 30 min of arrival
- **Note:** If faster delivery needed, consider multiple GSM modems or cloud fallback

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0039](US0039-sms-templates.md) | Functional | Message content | Ready |
| [US0042](US0042-sms-gateway.md) | Integration | Send via gateway | Ready |
| [US0030](US0030-scan-ingestion-api.md) | Trigger | Scan events | Ready |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Updated for GSM modem rate limiting (3-sec delay) |
