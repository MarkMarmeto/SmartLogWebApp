# US0085: Broadcast — Separate EN and FIL Message Inputs

> **Status:** Done
> **Epic:** [EP0009: SMS Strategy Overhaul](../epics/EP0009-sms-strategy-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Admin Amy (Administrator)
**I want** to enter separate EN and FIL message bodies when I send a broadcast to both English- and Filipino-preferring parents
**So that** each student receives the broadcast in their actual preferred language, not a mismatched mix.

## Context

### Persona Reference
**Admin Amy** — Composes bilingual announcements.

### Background
Every student carries a `SmsLanguage` preference (EN or FIL). The broadcast pipeline wraps the admin's free-text `{Message}` inside a language-specific template:

- EN template: `{SchoolName}: {Message} For concerns, call {SchoolPhone}.`
- FIL template: `{SchoolName}: {Message} Para sa katanungan, tumawag sa {SchoolPhone}.`

Today the composer accepts a single `{Message}` body. If the admin writes that body in Filipino — e.g. *"Suspendido ang mga klase sa lahat ng Grade Levels"* — it gets substituted into BOTH templates. EN-preferring parents then receive an English wrapper around Filipino content (or vice-versa), which is confusing and defeats the point of the per-student language preference.

The fix: when the admin targets a mixed-language audience, require two `{Message}` bodies (one per language); route each student to the template+body pair matching their `SmsLanguage`.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0009 | Data | `Student.SmsLanguage` already drives template selection for entry/exit and no-scan alerts | Reuse the same per-student language routing for broadcasts |
| TRD | Data | `SmsTemplate` stores EN and FIL variants of the wrapper | Both variants must be rendered; admin supplies the `{Message}` for each |
| PRD | UX | Admin may still want a single-language broadcast (e.g. only FIL parents) | UI must support single- or dual-language composition |

---

## Acceptance Criteria

### AC1: Language Selector on Composer
- **Given** I am on a broadcast composer page (Announcement, Emergency, BulkSend)
- **Then** the composer shows a "Message Language" selector with three options: **English only**, **Filipino only**, **Both (EN + FIL)**
- **And** the default is **Both (EN + FIL)**

### AC2: Single-Language Mode — One Message Input
- **Given** I have selected **English only** (or **Filipino only**)
- **Then** a single `{Message}` text area is shown, labelled with the selected language
- **When** I submit
- **Then** only students with `SmsLanguage` matching the selected language are targeted
- **And** each queued message uses the selected language's template + my `{Message}` body
- **And** a warning banner shows: "Students whose language preference does not match will be skipped. N students skipped."

### AC3: Dual-Language Mode — Two Message Inputs
- **Given** I have selected **Both (EN + FIL)**
- **Then** two `{Message}` text areas are shown, clearly labelled "English Message" and "Filipino Message"
- **And** both inputs are required
- **When** I submit
- **Then** for each targeted student:
  - If `SmsLanguage = EN` → queued with EN template + EN message body
  - If `SmsLanguage = FIL` → queued with FIL template + FIL message body

### AC4: Substitution Uses Language-Specific Body
- **Given** EN Message = *"Classes are suspended for all Grade Levels"*
- **And** FIL Message = *"Suspendido ang mga klase sa lahat ng Grade Levels"*
- **And** school = "Pedro E. Diaz High School", phone = "09123456789"
- **When** broadcast is sent to an EN student
- **Then** the final SMS text is: *"Pedro E. Diaz High School: Classes are suspended for all Grade Levels For concerns, call 09123456789."*
- **When** broadcast is sent to a FIL student
- **Then** the final SMS text is: *"Pedro E. Diaz High School: Suspendido ang mga klase sa lahat ng Grade Levels Para sa katanungan, tumawag sa 09123456789."*

### AC5: Character Count Per Language
- **Given** dual-language mode
- **Then** each text area shows its own character count + SMS segment estimate
- **And** a combined "Estimated total SMS: N" summary reflects both EN and FIL volumes based on targeted students' language distribution

### AC6: Preview Shows Both Rendered Messages
- **Given** I have filled in EN and FIL message bodies
- **Then** a preview panel shows both fully-rendered SMS bodies (wrapper + substitution), so I can proof-read before sending

### AC7: Backward Compatibility on Queue
- **Given** a broadcast has been composed in dual-language mode
- **When** rows are written to `SmsQueue`
- **Then** each row carries the correctly-templated, final SMS body per student (no downstream ambiguity)

---

## Scope

### In Scope
- Message composer UI changes on all three broadcast pages
- Language selector (EN-only / FIL-only / Both)
- Per-language body routing into `SmsQueue`
- Rendered preview + character/segment counting per language

### Out of Scope
- Automatic translation between EN and FIL (admin writes both bodies)
- More than two languages (existing system is bilingual by design)
- Retro-changing templates themselves (uses existing EN and FIL wrappers)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Admin picks EN-only but selection includes FIL students | Preview + warning banner: "N FIL students will be skipped"; admin can proceed or switch to Both |
| Admin picks Both but leaves FIL message empty | Form validation error: "Filipino message is required when 'Both' is selected" |
| Student has no `SmsLanguage` set (legacy records) | Default to EN for routing (matches existing no-scan alert behaviour) |
| Template placeholder count differs between EN and FIL bodies | Still allowed — placeholders in `{Message}` are treated as literal text |
| Broadcast payload exceeds SMS segment limit in one language only | Each language's counter flags independently; admin sees per-language warning |

---

## Test Scenarios

- [ ] EN-only broadcast queues only EN-preference students with EN template + EN body
- [ ] FIL-only broadcast queues only FIL-preference students with FIL template + FIL body
- [ ] Dual-language broadcast routes each student to their preferred language's body+template
- [ ] Final SMS text for example case matches the expected EN and FIL strings in AC4
- [ ] Missing FIL body in Both mode blocks submission with validation error
- [ ] Students without SmsLanguage fall back to EN
- [ ] Preview panel shows both fully-rendered bodies
- [ ] Character/segment counts are correct per language
- [ ] Language-skip warning shows when EN-only / FIL-only filters out non-matching students

---

## Technical Notes

### Composer Model
- Replace single `string Message` with `BroadcastMessageBodies { string? EnglishBody; string? FilipinoBody; BroadcastLanguageMode Mode; }`
- `BroadcastLanguageMode { EnglishOnly, FilipinoOnly, Both }`

### Queueing Path
- Broadcast send handler iterates targeted students
- For each student: pick body + template by `SmsLanguage`
- Render final SMS text and insert into `SmsQueue` with `MessageType = BROADCAST`

### Files to Modify
- **New:** `src/SmartLog.Web/Models/BroadcastMessageBodies.cs`
- **Modify:** `Pages/Admin/Sms/Announcement.cshtml(.cs)` — composer + submit logic
- **Modify:** `Pages/Admin/Sms/Emergency.cshtml(.cs)` — same
- **Modify:** `Pages/Admin/Sms/BulkSend.cshtml(.cs)` — same
- **Modify:** `Services/Sms/BroadcastService.cs` (or equivalent) — per-student body+template resolution
- **Modify:** `Services/Sms/SmsTemplateRenderer.cs` (if exists) — expose per-language render helper

### Migration / Rollout
- No DB schema change
- Existing `Broadcast` entity already has language/template context via per-row queue rendering — no retro-migration of past broadcasts needed

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0039](US0039-sms-templates.md) | Data | EN + FIL templates seeded | Done |
| [US0054](US0054-entry-exit-sms-optin.md) | Pattern | Per-student language routing already used for entry/exit | Done |

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium — UI rework on three pages + per-student body resolution, no DB change

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 planning session |
