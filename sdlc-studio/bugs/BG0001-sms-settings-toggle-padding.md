# BG0001: SMS Settings — Toggle Padding Too Narrow

> **Status:** Fixed
> **Severity:** Low
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Reporter:** Mark
> **Created:** 2026-04-24

## Description

On `/Admin/Sms/Settings`, the two toggle switches — **Enable SMS Sending** (Global SMS) and **Enable No-Scan Alert** — sit too close to their labels. The gap is cramped enough that the label text visually collides with the toggle handle, making the UI feel tight and reducing scannability.

## Steps to Reproduce

1. Sign in as Admin
2. Navigate to **Admin → SMS → Settings**
3. Observe the "Global SMS" and "No-Scan Alert" sections

## Expected Behaviour

Clear whitespace between each toggle control and its label / description, consistent with the spacing used on other admin forms (e.g. Student Profile switches).

## Actual Behaviour

Toggles are flush against their labels; no breathing room.

## Scope of Fix

- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Settings.cshtml` — add padding/gap utility classes to the toggle + label containers for both Global SMS and No-Scan Alert sections.

## Acceptance

- [ ] "Enable SMS Sending" toggle has visible padding between control and label
- [ ] "Enable No-Scan Alert" toggle matches the same padding
- [ ] Spacing is consistent with other toggle controls in the app
- [ ] No regression on mobile/narrow viewports

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial bug logged |
