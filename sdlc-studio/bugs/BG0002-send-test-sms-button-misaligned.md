# BG0002: SMS Settings — "Send Test SMS" Button Misaligned with Phone Input

> **Status:** Fixed
> **Severity:** Low
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Reporter:** Mark
> **Created:** 2026-04-24

## Description

On `/Admin/Sms/Settings`, the **Send Test SMS via `<Provider>`** button does not align vertically with the Phone Number text input it belongs to. The button sits above or below the input baseline, breaking the row layout.

## Steps to Reproduce

1. Sign in as Admin
2. Navigate to **Admin → SMS → Settings**
3. Scroll to the Test Send section
4. Compare the "Send Test SMS via `<Provider>`" button position against the Phone Number input

## Expected Behaviour

Button is vertically centred on the same row as the Phone Number input, with consistent gutter spacing between input and button.

## Actual Behaviour

Button is misaligned — sits offset from the input's baseline.

## Scope of Fix

- **Modify:** `src/SmartLog.Web/Pages/Admin/Sms/Settings.cshtml` — wrap the Phone Number input and Send Test SMS button in an input-group or flex row so they align along the same axis.

## Acceptance

- [ ] Phone Number input and Send Test button share a baseline
- [ ] Gutter spacing between input and button matches Bootstrap input-group convention
- [ ] No regression on narrow viewports (button wraps cleanly below input when space is tight)

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial bug logged |
