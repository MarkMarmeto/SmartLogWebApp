# US0055: Per-Broadcast Gateway Selection

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to choose between online (Semaphore) and offline (GSM Modem) SMS gateway when creating a broadcast
**So that** I can use the most appropriate delivery method based on internet availability and urgency

## Context

### Background
Currently the SMS gateway is configured globally. This story adds a per-broadcast dropdown so admins can choose the gateway for each broadcast. Default is Online (Semaphore). Emergency broadcasts may prefer GSM Modem for offline reliability.

---

## Acceptance Criteria

### AC1: Gateway Dropdown on Announcement Page
- **Given** I am creating an Announcement broadcast
- **Then** I see a "Send via" dropdown with options:
  - "Online (Semaphore)" — selected by default
  - "Offline (GSM Modem)"

### AC2: Gateway Dropdown on Emergency Page
- **Given** I am creating an Emergency broadcast
- **Then** I see the same "Send via" dropdown
- **And** default is "Online (Semaphore)"

### AC3: Gateway Dropdown on Bulk Send Page
- **Given** I am creating a Bulk SMS
- **Then** I see the same "Send via" dropdown

### AC4: Broadcast Entity Stores Selection
- **Given** I select "Offline (GSM Modem)" and submit the broadcast
- **Then** the Broadcast record saves `PreferredProvider = "GSM_MODEM"`
- **And** all queued SmsQueue entries for this broadcast have `Provider = "GSM_MODEM"`

### AC5: SmsWorkerService Respects Pre-Set Provider
- **Given** an SmsQueue entry has `Provider = "GSM_MODEM"` (pre-set from broadcast)
- **When** the SmsWorkerService processes this entry
- **Then** it uses the GSM_MODEM gateway directly
- **And** does NOT apply the global default gateway selection logic

### AC6: Null Provider Uses Global Default
- **Given** an SmsQueue entry has `Provider = null` (e.g., attendance SMS, no-scan alerts)
- **When** the SmsWorkerService processes this entry
- **Then** it uses the global default gateway from SmsSettings (existing behaviour)

### AC7: Migration
- **Given** existing Broadcast records
- **When** the migration runs
- **Then** `PreferredProvider` is added as nullable column
- **And** existing records have `PreferredProvider = null` (uses global default)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Selected gateway is unavailable | Fall back to other gateway (existing fallback logic) |
| GSM Modem not connected | Show error on send, suggest switching to Online |
| Semaphore API key not configured | Dropdown still shows option; error on send |
| Broadcast cancelled mid-send | Remaining SmsQueue entries cancelled (existing behaviour) |
| Re-send of cancelled broadcast | Uses original PreferredProvider |

---

## Test Scenarios

- [ ] Dropdown appears on Announcement page
- [ ] Dropdown appears on Emergency page
- [ ] Dropdown appears on Bulk Send page
- [ ] Default selection is "Online (Semaphore)"
- [ ] Broadcast.PreferredProvider saved correctly
- [ ] SmsQueue entries get Provider from broadcast
- [ ] SmsWorkerService uses pre-set provider
- [ ] Null provider falls back to global default
- [ ] Migration adds nullable column

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0042](US0042-sms-gateway.md) | Functional | SMS Gateway Integration | Ready |
| [US0041](US0041-sms-queue.md) | Functional | SmsQueue and SmsWorkerService | Ready |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
