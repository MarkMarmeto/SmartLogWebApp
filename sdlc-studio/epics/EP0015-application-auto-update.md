# EP0015: Application Auto-Update

> **Status:** Draft
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V3 — Phase 3 (Commercial Readiness) — **DEFERRED**

## Summary

Enable SmartLog applications (WebApp + ScannerApp) to check for and install updates without exposing the source code. Updates are distributed via GitHub Releases (private repo), proxied through the license server to prevent direct repository access. Scanner app uses an updater helper or Velopack; WebApp downloads and restarts the service.

## Inherited Constraints

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| Business | IP Protection | Source code must not be exposed | Binary-only distribution via proxy |
| TRD | Architecture | Offline-first LAN | Manual update option needed |
| Business | Distribution | GitHub Releases (private repo) | License server proxy required |

---

## Business Context

### Problem Statement
Schools need a simple way to update SmartLog without technical expertise. The GitHub repository is private, so clients cannot access releases directly. A proxy mechanism through the license server provides controlled, authenticated access to updates.

### Value Proposition
- One-click update experience for school IT staff
- Source code stays private (binary-only distribution)
- License server validates entitlement before serving update
- Supports both online update and manual download

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Update adoption within 7 days | N/A | > 80% | Update server logs |
| Update success rate | N/A | > 95% | Client update logs |

---

## Scope

### In Scope
- Update check button in admin UI ("Check for Updates")
- License server endpoint to proxy GitHub Releases
- Version comparison (current vs latest)
- Scanner app: updater helper or Velopack integration
- WebApp: download + service restart
- Release notes display before update

### Out of Scope
- Automatic background updates (always user-initiated)
- Rollback mechanism
- Delta/incremental updates
- Detailed scope deferred to implementation phase

### Affected Personas
- **SuperAdmin Tony:** Initiates and monitors updates

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can check for updates from the UI
- [ ] System compares current version against latest available
- [ ] Update downloads through license server proxy (not direct GitHub)
- [ ] Scanner app installs update and restarts
- [ ] WebApp downloads update package and restarts service
- [ ] Release notes displayed before update confirmation
- [ ] Source code is never exposed to the client

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0014: Product Licensing | Epic | Draft | Development (license server) |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| None | - | End-of-pipeline feature |

---

## Sizing

**Story Points:** TBD (estimated 5-7 stories)
**Estimated Story Count:** 5-7

---

## Story Breakdown

Stories to be generated when this epic moves to Ready status.

---

## Open Questions

- [ ] Velopack vs custom updater for Scanner app? — Owner: Development
- [ ] WebApp update mechanism (Docker image vs binary)? — Owner: Tony
- [ ] Minimum supported version policy? — Owner: Product

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2/V3 brainstorm (deferred) |
