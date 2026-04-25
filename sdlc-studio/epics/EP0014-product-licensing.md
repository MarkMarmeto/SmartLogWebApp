# EP0014: Product Licensing

> **Status:** Draft
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-04-16
> **Target Release:** V3 — Phase 3 (Commercial Readiness) — **DEFERRED**

## Summary

Implement online license activation with RSA-signed JWT tokens, tiered feature gating (Basic → Enterprise), and a 30-day offline grace period. Requires a separate license server (ASP.NET Core API). This epic enables SmartLog to be sold as a commercial product with controlled access to features.

## Inherited Constraints

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| Business | Legal | Must prevent unauthorized use | License key + online activation required |
| TRD | Architecture | Offline-first LAN operation | 30-day offline grace period needed |
| Business | Revenue | Tiered pricing model | Feature gating by license tier |

---

## Business Context

### Problem Statement
SmartLog needs a licensing mechanism to monetize the product. Without licensing, there's no way to control distribution, enforce payment, or gate features by tier.

### Value Proposition
- Enables commercial distribution with controlled access
- Tiered feature gating supports multiple price points
- Offline grace period respects school network constraints
- RSA-signed JWT prevents tampering

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| License activation success | N/A | > 99% | Activation logs |
| Offline grace compliance | N/A | 30-day window | JWT expiry check |

---

## Scope

### In Scope
- License server (separate ASP.NET Core API)
- Online activation: license key → RSA-signed JWT
- Feature gating by tier (Basic, Standard, Enterprise)
- 30-day offline grace period
- License status dashboard in admin UI

### Out of Scope
- Payment processing / billing integration
- License key generation UI (manual for now)
- Hardware fingerprinting / machine binding
- Detailed scope deferred to implementation phase

### Affected Personas
- **SuperAdmin Tony:** Activates license, views status
- **Admin Amy:** Affected by feature gating

---

## Acceptance Criteria (Epic Level)

- [ ] License key can be activated online against license server
- [ ] Activated license returns RSA-signed JWT with tier and expiry
- [ ] Features are gated based on license tier
- [ ] System works offline for up to 30 days after last activation
- [ ] Admin UI shows license status, tier, and expiry
- [ ] Expired/invalid license restricts app to read-only mode

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| All Phase 2 Epics | Epic | Done | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0015: Auto-Update | Epic | License server may proxy updates |

---

## Sizing

**Story Points:** TBD (estimated 8-10 stories)
**Estimated Story Count:** 8-10

---

## Story Breakdown

Stories to be generated when this epic moves to Ready status.

---

## Open Questions

- [ ] Exact feature tiers and what's gated at each level? — Owner: Product/Business
- [ ] License server hosting (cloud provider)? — Owner: Tony
- [ ] Pricing model (per-school annual vs perpetual)? — Owner: Business

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial epic created from V2/V3 brainstorm (deferred) |
