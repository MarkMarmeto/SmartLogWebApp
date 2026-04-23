# US0042: SMS Gateway Integration

> **Status:** Done
> **Epic:** [EP0007: SMS Notifications](../epics/EP0007-sms-notifications.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** System
**I want** to integrate with SMS sending capabilities
**So that** messages can be delivered to parent phones

## Context

### Technical Context
The system uses a provider-agnostic interface to send SMS. **GSM modem is the primary provider** (works offline, lower cost), with cloud gateway as optional fallback when internet is available.

### Design Decision: GSM Modem Primary
Given the **offline-first, LAN-only** architecture of SmartLog:
- **Primary:** GSM USB Modem (e.g., Huawei E3131) - No internet required
- **Fallback:** Cloud gateway (Semaphore) - Optional, if internet available
- **Cost Savings:** ~₱1,500-2,000/month (GSM promo) vs ~₱8,000-15,000/month (cloud)

---

## Acceptance Criteria

### AC1: Gateway Interface
- **Given** the system needs to send SMS
- **Then** it uses an `ISmsGateway` interface:
  ```csharp
  public interface ISmsGateway
  {
      Task<SmsResult> SendAsync(string phoneNumber, string message);
      Task<SmsStatus> GetStatusAsync(string messageId);
      bool IsAvailable();
  }
  ```

### AC2: Gateway Configuration
- **Given** I am a Super Admin
- **When** I navigate to Settings > SMS Gateway
- **Then** I can configure:
  - Primary Provider: GSM Modem / Cloud Gateway
  - Fallback Provider: None / Cloud Gateway
  - **GSM Modem Settings:**
    - Serial Port (e.g., /dev/ttyUSB0, COM3)
    - Baud Rate (default: 115200)
    - SIM PIN (optional, encrypted)
  - **Cloud Gateway Settings:**
    - Provider (Semaphore, Twilio, Generic HTTP)
    - API Key / Credentials
    - Sender ID

### AC3: GSM Modem Detection
- **Given** a GSM modem is connected via USB
- **When** I go to Settings > SMS Gateway
- **Then** I see detected modems with:
  - Port name
  - Modem model (if detectable)
  - Signal strength indicator
  - SIM status (Ready, PIN required, No SIM)
- **And** I can select a modem from the list

### AC4: GSM Modem Connection Test
- **Given** I have configured the GSM modem
- **When** I click "Test Connection"
- **Then** the system sends AT commands to verify:
  - Modem responds (AT → OK)
  - SIM is ready (AT+CPIN? → READY)
  - Signal strength (AT+CSQ → 0-31 scale)
- **And** displays result: "Modem ready, signal: 4/5 bars"

### AC5: Send SMS via GSM Modem
- **Given** the worker service sends an SMS
- **When** GSM modem is the active provider
- **Then** the system:
  - Opens serial connection to modem
  - Sets SMS mode: `AT+CMGF=1` (text mode)
  - Sends: `AT+CMGS="{phone}"` followed by message
  - Waits for `+CMGS:` response (message reference)
  - Marks SMS as Sent with modem reference ID

### AC6: GSM Rate Limiting
- **Given** the GSM modem has throughput limits (~10-20 SMS/min)
- **Then** the worker service:
  - Sends one SMS at a time (serial)
  - Waits 3 seconds between messages
  - Queues excess messages for later processing
  - Logs warning if queue grows beyond 100

### AC7: Credential Security
- **Given** gateway credentials (API keys, SIM PIN) are saved
- **Then** they are stored encrypted in the database
- **And** they are not exposed in API responses
- **And** they are masked in the UI

### AC8: Cloud Gateway Fallback
- **Given** GSM modem is configured as primary
- **And** a cloud gateway is configured as fallback
- **When** GSM modem fails (disconnected, no signal)
- **Then** the system:
  - Logs warning "GSM modem unavailable, using fallback"
  - Attempts to send via cloud gateway
  - Only if internet is available
- **And** resumes GSM modem when it becomes available

### AC9: Handle Gateway Responses
- **Given** the gateway (GSM or cloud) responds
- **Then** handle common responses:
  - Success: Mark SMS as Sent with reference ID
  - Invalid Number: Mark as Failed, no retry
  - Modem Busy: Retry after 5 seconds
  - Modem Error: Retry with exponential backoff
  - No Signal: Queue for later, alert admin
  - Rate Limited (cloud): Pause and retry

### AC10: Multiple Gateway Support
- **Given** the school wants flexibility in SMS providers
- **Then** implement provider-specific adapters:
  - **GsmModemGateway** (Primary - Recommended)
  - SemaphoreSmsGateway (Philippines cloud)
  - TwilioSmsGateway (International cloud)
  - GenericHttpSmsGateway (configurable)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| GSM modem not detected | Show "No modem detected. Check USB connection" |
| SIM PIN required | Prompt for PIN, store encrypted |
| No SIM card | Show "No SIM card detected" |
| Low signal (CSQ < 10) | Show warning "Weak signal may affect delivery" |
| Modem busy (in use) | Queue message, retry in 5 seconds |
| Modem disconnected mid-send | Mark as Retry, reconnect and resend |
| USB port changed | Re-detect and prompt to reconfigure |
| Cloud gateway credentials invalid | Log critical, alert admin |
| Phone number format mismatch | Normalize to local format (09xxxxxxxxx) |
| International number | Prefix with country code |
| Docker container restart | Reconnect to modem on startup |

---

## Test Scenarios

- [ ] GSM modem detection works
- [ ] Modem connection test works
- [ ] Signal strength displays correctly
- [ ] SIM status displayed correctly
- [ ] Send SMS via GSM modem works
- [ ] Rate limiting (3 sec delay) works
- [ ] Fallback to cloud gateway works
- [ ] Fallback only when internet available
- [ ] Resume GSM when modem reconnected
- [ ] Configuration UI works
- [ ] Credentials stored encrypted
- [ ] Credentials masked in UI
- [ ] Message reference ID stored on success
- [ ] Invalid number handled
- [ ] Phone numbers normalized
- [ ] Docker device mapping works

---

## Technical Notes

### Docker Configuration for GSM Modem
```yaml
# docker-compose.yml
services:
  smartlog:
    devices:
      - "/dev/ttyUSB0:/dev/ttyUSB0"
    privileged: false
    group_add:
      - dialout
```

### Phone Number Normalization
```csharp
// Convert E.164 to local format for GSM modem
// +639171234567 → 09171234567
public string NormalizeForGsm(string phone)
{
    phone = Regex.Replace(phone, @"[^\d+]", "");
    if (phone.StartsWith("+63"))
        phone = "0" + phone.Substring(3);
    return phone;
}
```

### GSM Modem AT Commands
```csharp
public class GsmModemGateway : ISmsGateway
{
    public async Task<SmsResult> SendAsync(string phone, string message)
    {
        using var port = new SerialPort(_config.Port, _config.BaudRate);
        port.Open();

        // Set text mode
        await SendCommand(port, "AT+CMGF=1");

        // Send SMS
        await SendCommand(port, $"AT+CMGS=\"{phone}\"");
        port.Write(message + "\x1A"); // Ctrl+Z to send

        // Wait for response: +CMGS: <mr>
        var response = await ReadResponse(port);
        var match = Regex.Match(response, @"\+CMGS:\s*(\d+)");

        return match.Success
            ? new SmsResult(match.Groups[1].Value, SmsStatus.Sent)
            : new SmsResult(null, SmsStatus.Failed);
    }

    public bool IsAvailable()
    {
        try {
            using var port = new SerialPort(_config.Port, _config.BaudRate);
            port.Open();
            return SendCommand(port, "AT").Contains("OK");
        } catch { return false; }
    }
}
```

### Recommended Hardware
- **Primary:** Huawei E3131 or E173 USB Modem (~₱1,500-2,500)
- **SIM:** Globe or Smart prepaid with unlimited text promo
- **Monthly Cost:** ~₱1,500-2,000 (vs ₱8,000+ for cloud)

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0041](US0041-sms-queue.md) | Integration | Called by worker | Ready |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| GSM USB Modem (e.g., Huawei E3131) | Hardware | Not Started |
| Prepaid SIM Card (Globe/Smart) | Hardware | Not Started |
| Serial Port Library (System.IO.Ports) | NuGet Package | Not Started |
| Semaphore Account (Optional Fallback) | External | Not Started |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Stakeholder Decisions

- [x] GSM modem as primary SMS provider - **Approved for offline-first design**
- [x] Cloud gateway as optional fallback
- [x] Estimated monthly cost: ₱1,500-2,000 with unlimited text promo

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Updated to GSM modem as primary, cloud as fallback |
