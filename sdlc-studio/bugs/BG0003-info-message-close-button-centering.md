# BG0003: Info Message Box — Close ('X') Button Not Horizontally Centred

> **Status:** Fixed
> **Severity:** Low
> **Epic:** Cross-cutting UI (no dedicated Epic)
> **Reporter:** Mark
> **Created:** 2026-04-24

## Description

The dismissible info/success/warning message box (shown after saving changes, updating details, etc.) has a close ('X') button that is not horizontally centred within its click target. The offset is small but visible and inconsistent with Bootstrap's default alert close button placement.

## Steps to Reproduce

1. Sign in as Admin
2. Perform any action that triggers a toast/alert banner — e.g. save a Student profile, update SMS Settings
3. Observe the 'X' close control at the far right of the banner

## Expected Behaviour

'X' glyph is centred both horizontally and vertically inside its clickable area.

## Actual Behaviour

'X' appears offset horizontally within its button box.

## Scope of Fix

- **Investigate:** Shared alert/banner partial — likely `src/SmartLog.Web/Pages/Shared/_StatusMessage.cshtml` or a site-wide CSS rule in `wwwroot/css/site.css`.
- **Modify:** The close button CSS — ensure `display: flex; align-items: center; justify-content: center;` (or equivalent Bootstrap utilities) on the button, and remove any stray padding/margin skewing the glyph.

## Acceptance

- [ ] 'X' glyph visually centred in its clickable area on all alert variants (success, info, warning, danger)
- [ ] Click target size unchanged
- [ ] No regression on mobile

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial bug logged |
