# US0117: Bilingual Privacy Notice Page at `/Privacy`

> **Status:** Draft
> **Epic:** [EP0016: PII & RA 10173 Compliance — Consent & Notice (Floor)](../epics/EP0016-pii-ra10173-compliance.md)
> **Owner:** TBD
> **Created:** 2026-04-27

## User Story

**As a** Parent / Guardian (and any data subject)
**I want** to read SmartLog's data-processing privacy notice in English or Filipino without needing an account
**So that** I can understand what student information is collected, why, how long it is kept, and exercise informed consent — satisfying RA 10173 §16 transparency requirements.

## Context

### Persona Reference
**Parents (indirect)** — primary readers; reach the page via a footer link from the login screen.
**Admin Amy** — points parents to the URL during enrollment.
**SuperAdmin Tony** — eventually approves the published wording.

### Background
RA 10173 §16 requires that data subjects are informed of processing purposes, scope, retention, recipients, and their rights. SmartLog has no published notice today. This story ships a self-drafted bilingual notice marked **"Draft — pending legal review"**, so the floor of EP0016 is met immediately and SuperAdmin can replace the banner once external counsel confirms wording. Storage-limitation specifics already shipped in EP0017 are referenced from the notice.

The notice is anonymous-accessible (no auth) — a parent without a SmartLog account must be able to read it.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0016 | Legal | RA 10173 §16 — transparency notice required | Page must list purposes, data categories, retention, rights, contact |
| EP0016 | UX | Bilingual EN + FIL | Two language variants accessible from the same URL |
| EP0016 | Auth | Parents have no account | Anonymous access; route must be excluded from `[Authorize]` defaults |
| TRD | Stack | ASP.NET Core 8 + Razor Pages | Root-level Razor page; partial per language |

---

## Acceptance Criteria

### AC1: Anonymous Access
- **Given** I am not logged in
- **When** I navigate to `/Privacy`
- **Then** the page renders without a redirect to login
- **And** no `[Authorize]` attribute is enforced on this route

### AC2: Default Language
- **Given** I navigate to `/Privacy` with no `?lang=` query parameter
- **Then** the page renders in English by default
- **And** if the request `Accept-Language` header indicates Filipino (`fil`, `tl`), the page renders in Filipino instead

### AC3: Language Toggle
- **Given** the page is rendered in either language
- **Then** a clearly labelled toggle ("English | Filipino") at the top of the page allows switching
- **And** clicking the alternate language navigates to `/Privacy?lang=fil` or `/Privacy?lang=en`
- **And** the rendered content updates accordingly

### AC4: Required Sections (RA 10173 §16)
- **Given** the page is rendered (in either language)
- **Then** it contains at least these sections, with localised headings:
  1. **Who we are** — operator / school name placeholder, contact email
  2. **What information we collect** — student name, LRN, grade/section, parent phone, photo, scan timestamps
  3. **Why we collect it (purposes)** — attendance tracking, parent notifications, reporting
  4. **Lawful basis** — consent (RA 10173 §12(a))
  5. **How long we keep it** — references EP0017 retention windows (link to internal retention summary or describe in plain language)
  6. **Who we share it with** — parents/guardians via SMS gateway providers (GSM, Semaphore); no third-party advertising or analytics
  7. **Your rights as a data subject** — access, correction, objection, erasure, portability, lodge a complaint with the NPC (note: workflow not yet implemented in product; contact the school admin)
  8. **How to contact us** — school admin email placeholder
  9. **Last updated** — date the notice content was last edited

### AC5: Draft Banner
- **Given** the page renders
- **Then** a yellow/amber banner at the top reads (localised):
  > "DRAFT — This privacy notice is a self-drafted working version. It is pending review by legal counsel and approval by the SuperAdmin."
- **And** the banner is visible on both languages

### AC6: Footer Link from Login Page
- **Given** I am on `/Account/Login` (or the public landing page)
- **Then** the footer contains a "Privacy" link to `/Privacy`
- **And** the link is keyboard-accessible and screen-reader-labelled

### AC7: No Authentication Side Effects
- **Given** an authenticated session
- **When** I visit `/Privacy`
- **Then** my session is not affected (no logout, no redirect cycle)
- **And** the page does not include admin chrome / nav

### AC8: Source Stored in Repo (Versioned)
- **Given** the page renders content from two partials (`_PrivacyNotice.en.cshtml`, `_PrivacyNotice.fil.cshtml`)
- **Then** any future content change goes through a normal code review / commit
- **And** the `Last updated` date in AC4 #9 is sourced from a constant in the page model that the editor must update when the partial changes (lint check or comment instruction sufficient — automated enforcement out of scope)

---

## Scope

### In Scope
- New Razor page `Pages/Privacy.cshtml(.cs)` at the application root
- Two content partials: `_PrivacyNotice.en.cshtml`, `_PrivacyNotice.fil.cshtml`
- Self-drafted bilingual notice content covering AC4 sections 1-9, with `[School Name]` / `[Admin Email]` placeholders
- Anonymous-allowed routing
- Language toggle (query-string based)
- Footer link from login page
- Draft banner

### Out of Scope
- Subject-rights request workflow (deferred to future Subject-Rights Epic — notice describes the rights but instructs the user to contact the school admin)
- Storing notice content in `AppSettings` for runtime editing (deferred — see Epic open question; v1 uses a versioned cshtml partial)
- Cookie-based language persistence (query string is enough; browser default + explicit toggle covers the use case)
- PDF download of the notice
- Translation review by a Filipino-language native speaker (the draft is functional, not legally polished)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Unsupported `?lang=` value (e.g. `?lang=es`) | Falls back to English silently |
| Anonymous user follows `/Privacy` link, then tries to access `/Admin/...` | Standard auth challenge — no change to existing flow |
| Notice content updated but `Last updated` date forgotten | Reviewer catches in PR; not enforced by code |
| Browser sends `Accept-Language: fr,en;q=0.5` | Falls back to English (no French variant) |

---

## Test Scenarios

- [ ] `GET /Privacy` returns 200 OK while logged out
- [ ] `GET /Privacy?lang=fil` returns Filipino content
- [ ] `GET /Privacy?lang=en` returns English content
- [ ] Default language with no header is English
- [ ] `Accept-Language: fil` selects Filipino in the absence of `?lang`
- [ ] `Accept-Language: es` falls back to English
- [ ] Login page footer contains a Privacy link with `href="/Privacy"`
- [ ] Toggle on the page swaps language without losing the URL
- [ ] Notice contains all 9 required sections (snapshot test on rendered HTML headings)
- [ ] Draft banner is visible in both language variants
- [ ] Authenticated session is unaffected after visiting `/Privacy`

---

## Technical Notes

### Files to Create
- **New:** `src/SmartLog.Web/Pages/Privacy.cshtml`
- **New:** `src/SmartLog.Web/Pages/Privacy.cshtml.cs`
- **New:** `src/SmartLog.Web/Pages/Shared/_PrivacyNotice.en.cshtml`
- **New:** `src/SmartLog.Web/Pages/Shared/_PrivacyNotice.fil.cshtml`

### Files to Modify
- **Modify:** `src/SmartLog.Web/Program.cs` — confirm `/Privacy` is allowed by the `RazorPages` conventions; add `options.Conventions.AllowAnonymousToPage("/Privacy")` if a global authorization filter exists
- **Modify:** `src/SmartLog.Web/Pages/Account/Login.cshtml` (or layout footer used by Login) — add Privacy link
- **Modify:** `src/SmartLog.Web/Pages/Shared/_Layout.cshtml` if there is an unauthenticated public layout — add the Privacy link to its footer

### Language Resolution
1. If `?lang=fil` or `?lang=en` is present and valid, use it.
2. Else, parse `Accept-Language` for `fil` or `tl` → Filipino.
3. Else, default to English.

Implement as a small helper on the page model — do not introduce `IStringLocalizer` infrastructure for two static partials.

### Draft Notice Content (Author Note)
The two partials should each be ~1-2 pages of plain-language text under the AC4 headings. Use the NPC's standard Privacy Notice template as the structural reference. Mark every `[School Name]` and `[Admin Email]` as a bracketed placeholder; SuperAdmin replaces these per-deployment.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| _None_ — this story is independent within EP0016 and can ship in parallel with US0114-US0116. | | | |

### Blocks

- None. (A future Subject-Rights Epic may evolve the "Your rights" section into actual UI flows.)

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium — code is trivial; the long pole is drafting clear, bilingual notice content under each RA 10173 heading.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft as part of EP0016 V2.1 activation (consent + notice floor) |
