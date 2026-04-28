# US0111: School Branding Settings (Logo, Name, Return Address)

> **Status:** Review
> **Plan:** [PL0034: School Branding Settings](../plans/PL0034-school-branding-settings.md)
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-27

## User Story

**As a** Admin Amy (Administrator)
**I want** to upload my school's logo, confirm the school name, and set a "return address" line for printed ID cards
**So that** student ID cards carry our school's actual branding and a finder knows how to return a lost card

## Context

### Background

The current `PrintQrCode.cshtml` page renders an inline SVG placeholder logo and pulls the school name from `AppSettings` key `System.SchoolName`. There is no admin UI to upload a real school logo — schools cannot replace the placeholder without editing the Razor page directly. There is also no place to set a "return address" line that will appear on the card footer (US0112 AC11).

This story adds a single screen under `/Admin/Settings` for school branding: logo upload, school name confirmation, and a return-address text field. Stored values:

| Key | Type | Purpose |
|-----|------|---------|
| `System.SchoolName` | string (existing) | School name shown in card header |
| `Branding:SchoolLogoPath` | string (new) | Relative path to uploaded logo (e.g. `/branding/school-logo.png`) |
| `Branding:ReturnAddressText` | string (new) | Single-line "If found, please return to..." text shown in card footer |

The uploaded logo is stored as a static file under `wwwroot/branding/` and the path is persisted in `AppSettings`. The redesigned ID card (US0112) and the bulk-print page (US0113) both consume these settings.

This story is a prerequisite for US0112.

---

## Acceptance Criteria

### AC1: Settings Page Exists
- **Given** I am signed in as an Admin or SuperAdmin
- **When** I navigate to `/Admin/Settings/Branding`
- **Then** I see a "School Branding" page with three controls: school logo upload, school name field, return-address text field

### AC2: School Name Editable
- **Given** I am on the School Branding page
- **And** the current value of `AppSettings.System.SchoolName` is "SmartLog School"
- **When** I change the field to "Marmeto National High School" and click Save
- **Then** `AppSettings.System.SchoolName` is updated to "Marmeto National High School"
- **And** subsequent page loads show the new value

### AC3: Logo Upload — Happy Path
- **Given** I am on the School Branding page
- **When** I select a PNG file ≤ 2 MB and click Upload
- **Then** the file is saved to `wwwroot/branding/school-logo.{ext}` (overwriting any prior file)
- **And** `AppSettings.Branding:SchoolLogoPath` is set to `/branding/school-logo.{ext}`
- **And** the page reloads and shows a preview of the uploaded logo

### AC4: Accepted File Types & Size
- **Given** I am uploading a logo
- **Then** only `.png`, `.jpg`, `.jpeg`, `.svg` files are accepted (validated by extension AND content-type)
- **And** files larger than 2 MB are rejected with a clear error
- **And** the upload control's `accept` attribute restricts the native picker

### AC5: Remove Logo
- **Given** a logo has been uploaded
- **When** I click "Remove Logo"
- **Then** `AppSettings.Branding:SchoolLogoPath` is cleared
- **And** the file is deleted from `wwwroot/branding/`
- **And** future ID card prints fall back to the inline SmartLog placeholder SVG

### AC6: Return-Address Text
- **Given** I am on the School Branding page
- **When** I enter "Marmeto National HS · Sample St., Quezon City · (02) 1234-5678" (≤ 120 characters) and click Save
- **Then** `AppSettings.Branding:ReturnAddressText` is updated to that value
- **And** the field accepts plain text only (HTML is escaped on render)
- **And** values longer than 120 characters are rejected with "Return address must be ≤ 120 characters"
- **And** an empty value is allowed — when empty, the card footer simply omits the line (US0112 AC11)

### AC7: Authorization
- **Given** I am signed in as a Teacher, Security, or Staff role
- **When** I navigate to `/Admin/Settings/Branding`
- **Then** I receive a 403 / Access Denied (policy `RequireAdmin`)

### AC8: Audit Trail
- **Given** an admin saves school name, return-address text, or uploads/removes the logo
- **Then** an `AuditLog` row is written with action `SchoolBrandingUpdated` and the changed key(s)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Upload file > 2 MB | Reject with "Logo must be ≤ 2 MB" |
| Wrong file type (e.g. `.pdf`) | Reject with "Allowed types: PNG, JPG, SVG" |
| Upload while disk full | Friendly error, transaction rolled back |
| `wwwroot/branding/` does not exist | Created on first upload |
| Logo path in DB but file missing on disk | ID card falls back to placeholder, no error |
| SVG containing `<script>` tags | Reject — sanitize check on SVG uploads |

---

## Test Scenarios

- [ ] Admin can navigate to `/Admin/Settings/Branding`
- [ ] Saving school name updates `AppSettings`
- [ ] Saving return-address text updates `AppSettings.Branding:ReturnAddressText`
- [ ] Return-address > 120 chars rejected
- [ ] Return-address empty value allowed and persisted as empty
- [ ] Uploading a PNG saves to `wwwroot/branding/` and updates `AppSettings.Branding:SchoolLogoPath`
- [ ] Page preview shows uploaded logo
- [ ] Oversized file rejected
- [ ] Wrong file type rejected
- [ ] SVG with embedded scripts rejected
- [ ] Remove Logo clears file and setting
- [ ] Teacher role denied access
- [ ] Audit log written on save / upload / remove

---

## Technical Notes

### Storage
- Logo file: `wwwroot/branding/school-logo.{png|jpg|svg}` (deterministic name; replaces prior on upload)
- Path stored in `AppSettings`, key `Branding:SchoolLogoPath`
- School name continues to live in `AppSettings`, key `System.SchoolName`

### Why a static file path (not DB blob)
Static files are served by Kestrel's static file middleware with caching headers — no per-request DB read for every card render. The logo is small and changes rarely, so a file on disk is appropriate.

### SVG Safety
Reject any uploaded SVG containing `<script`, `onload=`, `onerror=`, or `<foreignObject` (regex check on file contents before save).

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| None | — | First story in this slice | — |

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium — standard upload flow, small validation surface.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft — branding settings as prerequisite for card redesign |
