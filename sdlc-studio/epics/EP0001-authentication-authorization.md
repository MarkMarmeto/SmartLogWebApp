# EP0001: Authentication & Authorization

> **Status:** Done (content pending reconstruction)
> **Phase:** V1 — Phase 1 (Foundation)

## Note

The original epic document for EP0001 was accidentally truncated during a
status-reconciliation sweep on 2026-04-22. All 8 stories (US0001–US0008)
remain implemented and marked Done; the summary in `epics/_index.md` is
authoritative while this file is rebuilt.

## Story Breakdown (authoritative)

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0001](../stories/US0001-user-login.md) | Admin Login | 3 | Done |
| [US0002](../stories/US0002-account-lockout.md) | Account Lockout | 2 | Done |
| [US0003](../stories/US0003-2fa-setup.md) | Two-Factor Authentication Setup | 5 | Done |
| [US0004](../stories/US0004-2fa-verification.md) | Two-Factor Authentication Verification | 3 | Done |
| [US0005](../stories/US0005-session-management.md) | Session Management | 3 | Done |
| [US0006](../stories/US0006-role-based-menu.md) | Role-Based Menu Filtering | 2 | Done |
| [US0007](../stories/US0007-authorization-enforcement.md) | Authorization Policy Enforcement | 3 | Done |
| [US0008](../stories/US0008-auth-audit-logging.md) | Authentication Audit Logging | 3 | Done |

**Total:** 24 story points across 8 stories.

## Reconstruction Plan

Use a sibling epic (`EP0002-user-management.md` or `EP0005-scanner-integration.md`)
as the structural template, and backfill Business Context / AC / Risks /
Revision History from the 8 story files above.
