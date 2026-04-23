# SmartLog Stakeholder Feedback

**Review Date:** 2026-02-04
**Participants:** Principal Pedro, IT Manager Ivan, Registrar Rosa, PTA Rep Patricia, Security Head Sergio

---

## High Priority Questions - Resolved

### 1. Student ID Format
**Question:** What is the Student ID format?

| Stakeholder | Feedback |
|-------------|----------|
| **Rosa (Registrar)** | "We use LRN (Learner Reference Number) from DepEd, but we also need our own internal ID. Format should be: `{YearEnrolled}-{Grade}-{Sequence}`. Example: `2026-05-0001` for first Grade 5 student enrolled in 2026." |
| **Ivan (IT)** | "Keep it simple - alphanumeric, max 15 characters. Avoid special characters that might cause issues in QR codes." |

✅ **Decision:** Student ID format is `YYYY-GG-NNNN` (e.g., `2026-05-0001`)
- Year of enrollment (4 digits)
- Grade level at enrollment (2 digits)
- Sequence number (4 digits)
- Also store LRN as separate field for DepEd compliance

---

### 2. SMS Gateway Provider
**Question:** Which SMS gateway provider will be used?

| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "Given our offline-first design, a GSM modem makes more sense. USB modem like Huawei E3131 works without internet. We can use Globe/Smart unlimited text promos for ~₱1,500-2,000/month instead of ₱8,000+ for cloud gateway." |
| **Pedro (Principal)** | "Cost is a concern. With 500 students × 2 SMS/day × 20 days = 20,000 SMS/month. At ₱0.40 each, that's ₱8,000/month. Can we absorb this? If GSM modem is cheaper, let's go with that." |
| **Patricia (Parent)** | "As long as we receive notifications reliably, I don't mind which technology is used." |

✅ **Decision:** Use GSM Modem as primary SMS provider
- **Primary:** GSM USB Modem (Huawei E3131 or similar) - ~₱1,500-2,500 one-time
- **SIM:** Globe/Smart prepaid with unlimited text promo - ~₱1,500-2,000/month
- **Fallback:** Semaphore cloud gateway (optional, requires internet)
- **Rationale:** Aligns with offline-first design, significantly lower operating cost
- Build gateway abstraction so we can switch/add providers later

---

### 3. Late Arrival Time
**Question:** What time defines "late" arrival?

| Stakeholder | Feedback |
|-------------|----------|
| **Pedro (Principal)** | "Official class starts at 7:30 AM. Flag ceremony is 7:15 AM. Students should be inside by 7:30 AM." |
| **Rosa (Registrar)** | "We need grace period. Traffic is unpredictable. I suggest 7:45 AM as 'late' threshold." |
| **Sergio (Security)** | "Gates open at 6:00 AM. Most students arrive 6:30-7:15 AM. Rush hour is 7:00-7:30 AM." |

✅ **Decision:** Late arrival threshold is **7:45 AM**
- Present: Entry scan before 7:45 AM
- Late: Entry scan between 7:45 AM - 8:30 AM
- Absent: No entry scan by 8:30 AM
- Make threshold configurable in settings

---

### 4. Teacher-to-Class Assignment
**Question:** How are teachers assigned to classes?

| Stakeholder | Feedback |
|-------------|----------|
| **Rosa (Registrar)** | "Each section has an advisory teacher. Grade 5-A has Mrs. Santos, Grade 5-B has Mr. Reyes. This is set at start of school year." |
| **Pedro (Principal)** | "For Phase 1, let's keep it simple. Teachers can view any class. We'll add restrictions in Phase 2 if needed." |

✅ **Decision:** Phase 1 - Teachers can view all classes
- Add optional "Assigned Sections" field to Faculty record
- Filter dashboard by assigned sections (if set)
- Full restriction enforcement deferred to future enhancement

---

## Medium Priority Questions - Resolved

### 5. 2FA Mandatory for Admins?
| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "Absolutely yes for Super Admin. Optional for regular Admin. Some admin staff struggle with technology." |
| **Pedro (Principal)** | "Make it optional but encouraged. Don't lock out my registrar because she lost her phone." |

✅ **Decision:** 2FA is optional for all users
- Super Admin can enable/disable 2FA per user
- Show security recommendation banner if 2FA not enabled
- Add "Require 2FA for Super Admins" as future security enhancement

---

### 6. Password Complexity Requirements
| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "Minimum 8 characters, at least 1 uppercase, 1 lowercase, 1 number. No special character requirement - too confusing for users." |
| **Rosa (Registrar)** | "Please, not too complicated. My staff forgets passwords constantly." |

✅ **Decision:** Password requirements:
- Minimum 8 characters
- At least 1 uppercase letter
- At least 1 lowercase letter
- At least 1 number
- Special characters allowed but not required
- Password expiry: None (causes more problems than it solves)

---

### 7. Session Timeout Duration
| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "8 hours is fine for a school day. Extend to 10 hours for admin who work late." |
| **Rosa (Registrar)** | "I hate getting logged out in the middle of work. Can we make it longer?" |
| **Sergio (Security)** | "Scanner devices should stay logged in all day. Guards shouldn't need to re-authenticate." |

✅ **Decision:**
- Web session: 10 hours (with sliding expiration)
- Scanner API: No session timeout (API key based)
- Idle timeout: 30 minutes of inactivity triggers re-auth

---

### 8. Audit Log Retention Period
| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "DepEd might audit us. Keep logs for at least 3 years. Archive older logs to save space." |
| **Pedro (Principal)** | "Follow data retention policy. 5 years for student records, same for related logs." |

✅ **Decision:** Audit log retention:
- Active logs: 1 year in main database
- Archived logs: Move to archive table after 1 year
- Total retention: 5 years (then permanent delete)
- Implement archival job in Phase 3

---

### 9. Lockout Duration
| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "15 minutes is standard. Maybe allow Super Admin to unlock manually." |
| **Rosa (Registrar)** | "Can admin unlock a locked user? Sometimes staff panic and try wrong passwords." |

✅ **Decision:**
- Lockout after 5 failed attempts: 15 minutes
- Super Admin can manually unlock any user
- Add "Unlock User" button to user management

---

### 10. Duplicate Scan Window
| Stakeholder | Feedback |
|-------------|----------|
| **Sergio (Security)** | "5 minutes is too long. Sometimes student scans, forgets something, comes back in 2 minutes. Should count as one entry." |
| **Ivan (IT)** | "For network reliability, we need at least 5 minutes to handle retries during connectivity issues." |

✅ **Decision:** Keep 5-minute duplicate window
- Same device + same student + same scan type = duplicate
- Different scan types (ENTRY vs EXIT) within 5 min = allowed
- Add note in scanner display: "Already scanned. Please proceed."

---

## Low Priority Questions - Resolved

### 11. QR Code Expiration
| Stakeholder | Feedback |
|-------------|----------|
| **Ivan (IT)** | "No expiration needed. QR is invalidated when regenerated. Expired QR adds complexity." |
| **Pedro (Principal)** | "Students might use old ID cards. What happens?" |
| **Ivan (IT)** | "Old QR is invalid the moment we regenerate. Scanner rejects it." |

✅ **Decision:** No QR code expiration
- QR remains valid until explicitly regenerated
- Regeneration invalidates old QR immediately
- Add "QR Code Invalidated" message when old code is scanned

---

### 12. Department List
| Stakeholder | Feedback |
|-------------|----------|
| **Rosa (Registrar)** | "Standard departments: Mathematics, Science, English, Filipino, Araling Panlipunan, MAPEH, TLE, Values Education. Plus: Administration, Guidance, Clinic, Maintenance." |

✅ **Decision:** Predefined department list:
1. Mathematics
2. Science
3. English
4. Filipino
5. Araling Panlipunan (Social Studies)
6. MAPEH (Music, Arts, PE, Health)
7. TLE (Technology & Livelihood Education)
8. Values Education
9. Administration
10. Guidance & Counseling
11. School Clinic
12. Maintenance & Security

---

### 13. Multiple Children per Parent
| Stakeholder | Feedback |
|-------------|----------|
| **Patricia (Parent)** | "I have 3 kids in the school. I should get SMS for all of them, but maybe combined? 'Maria arrived 7:15 AM, Juan arrived 7:20 AM' in one message?" |
| **Ivan (IT)** | "Combining is complex. For Phase 1, send separate SMS per child. Parents can opt-out per child if too many." |

✅ **Decision:** Phase 1 - Separate SMS per child
- Each student triggers individual SMS
- Parent can opt-out per student if desired
- Future enhancement: Combined SMS option

---

## Story-Specific Feedback

### EP0001: Authentication (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0001 | Pedro: "Simple login, good." | ✅ Approved |
| US0002 | Ivan: "Add manual unlock option" | ✅ Approved (with change) |
| US0003 | Rosa: "Keep it optional" | ✅ Approved |
| US0004 | No changes | ✅ Approved |
| US0005 | Ivan: "Change to 10 hours" | ✅ Approved (with change) |
| US0006 | No changes | ✅ Approved |
| US0007 | No changes | ✅ Approved |
| US0008 | No changes | ✅ Approved |

### EP0002: User Management (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0009 | Rosa: "Include LRN field for students" | ✅ Approved |
| US0010 | No changes | ✅ Approved |
| US0011 | No changes | ✅ Approved |
| US0012 | Rosa: "Add export to Excel" | ✅ Approved (with change) |
| US0013 | Ivan: "Add confirmation dialog" | ✅ Approved |
| US0014 | No changes | ✅ Approved |

### EP0003: Student Management (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0015 | Rosa: "Add LRN field, Guardian relationship field" | ✅ Approved (with changes) |
| US0016 | No changes | ✅ Approved |
| US0017 | Rosa: "Add deactivation reason dropdown" | ✅ Approved (with change) |
| US0018 | No changes | ✅ Approved |
| US0019 | Ivan: "Confirmed HMAC-SHA256 approach" | ✅ Approved |
| US0020 | No changes | ✅ Approved |
| US0021 | Patricia: "Include photo placeholder on ID card" | ✅ Approved (with change) |
| US0022 | Rosa: "Critical for enrollment season!" | ✅ Approved |

### EP0004: Faculty Management (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0023 | Rosa: "Add Employee Type (Regular/Contractual)" | ✅ Approved (with change) |
| US0024 | No changes | ✅ Approved |
| US0025 | No changes | ✅ Approved |
| US0026 | No changes | ✅ Approved |
| US0027 | No changes | ✅ Approved |

### EP0005: Scanner Integration (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0028 | Ivan: "Add location dropdown (Main Gate, Side Gate)" | ✅ Approved (with change) |
| US0029 | No changes | ✅ Approved |
| US0030 | Sergio: "Must show student photo on device" | ⚠️ Approved (Phase 2) |
| US0031 | No changes | ✅ Approved |
| US0032 | Sergio: "Show 'Already scanned' message" | ✅ Approved (with change) |
| US0033 | No changes | ✅ Approved |

### EP0006: Attendance Tracking (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0034 | Pedro: "Add 'Print' button for emergency roll call" | ✅ Approved (with change) |
| US0035 | No changes | ✅ Approved |
| US0036 | No changes | ✅ Approved |
| US0037 | No changes | ✅ Approved |
| US0038 | No changes | ✅ Approved |

### EP0007: SMS Notifications (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0039 | Patricia: "Add Filipino language template" | ✅ Approved (with change) |
| US0040 | No changes | ✅ Approved |
| US0041 | No changes | ✅ Approved |
| US0042 | Ivan: "Use GSM modem as primary (offline-first), cloud as fallback" | ✅ Approved (with change) |
| US0043 | No changes | ✅ Approved |
| US0044 | Patricia: "Allow opt-out per child" | ✅ Approved |

### EP0008: Reporting & Analytics (All Approved ✅)
| Story | Feedback | Status |
|-------|----------|--------|
| US0045 | Rosa: "Add DepEd SF2 format option" | ⚠️ Approved (Phase 3) |
| US0046 | No changes | ✅ Approved |
| US0047 | Rosa: "Include perfect attendance list" | ✅ Approved (with change) |
| US0048 | No changes | ✅ Approved |
| US0049 | No changes | ✅ Approved |
| US0050 | Pedro: "Limit admin access to relevant logs" | ✅ Approved |
| US0051 | No changes | ✅ Approved |

---

## Summary of Changes to Implement

### Must-Have Changes (Before Development)
1. **US0002:** Add "Unlock User" button for Super Admin
2. **US0005:** Change session timeout to 10 hours
3. **US0015:** Add LRN field and Guardian Relationship dropdown
4. **US0017:** Add Deactivation Reason dropdown (Graduated, Transferred, Dropped)
5. **US0028:** Add Location dropdown for scanner devices
6. **US0032:** Add "Already scanned, please proceed" response
7. **US0039:** Add Filipino language template option
8. **US0042:** GSM modem as primary SMS provider (offline-first), cloud as fallback

### Nice-to-Have Changes (Can Add Later)
1. **US0012:** Export user list to Excel
2. **US0021:** Photo placeholder on ID card
3. **US0023:** Employee Type field
4. **US0034:** Print button for emergency roll call
5. **US0047:** Perfect attendance recognition list

### Deferred to Future Phases
1. **US0030:** Student photo display on scanner (Phase 2)
2. **US0045:** DepEd SF2 format export (Phase 3)
3. Combined SMS for siblings (Future)

---

## Final Approval

| Stakeholder | Stories Reviewed | Approved | Date |
|-------------|------------------|----------|------|
| Principal Pedro | All 51 | ✅ Yes | 2026-02-04 |
| IT Manager Ivan | All 51 | ✅ Yes | 2026-02-04 |
| Registrar Rosa | All 51 | ✅ Yes | 2026-02-04 |
| PTA Rep Patricia | SMS, Student | ✅ Yes | 2026-02-04 |
| Security Head Sergio | Scanner, Attendance | ✅ Yes | 2026-02-04 |

---

## Next Steps

1. ✅ All 51 stories approved
2. → Update story files with approved changes
3. → Mark all stories as "Ready"
4. → Begin Sprint 1 implementation
