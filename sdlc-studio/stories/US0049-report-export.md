# US0049: Report Export (PDF/Excel)

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to export attendance reports to PDF and Excel
**So that** I can share reports with stakeholders and archive them

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who distributes reports to school leadership.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Export Buttons
- **Given** I am viewing any attendance report (daily, weekly, monthly)
- **Then** I see export options:
  - "Export to PDF" button
  - "Export to Excel" button

### AC2: PDF Export Format
- **Given** I click "Export to PDF"
- **Then** a PDF is generated with:
  - School letterhead/logo
  - Report title and date range
  - Summary statistics
  - Data tables
  - Generated timestamp and user
  - Page numbers

### AC3: PDF Layout
- **Given** the PDF is generated
- **Then** the layout is:
  - A4 paper size
  - Portrait orientation (landscape for wide tables)
  - Professional formatting
  - Tables fit within page margins
  - Long tables span multiple pages

### AC4: Excel Export Format
- **Given** I click "Export to Excel"
- **Then** an Excel file is generated with:
  - Summary sheet with statistics
  - Detail sheet with full data table
  - Proper column headers
  - Data types preserved (dates, numbers)
  - Auto-fit column widths

### AC5: Excel Features
- **Given** the Excel file is generated
- **Then** it includes:
  - Filters enabled on header row
  - Frozen header row
  - Basic conditional formatting (red for absent)
  - Sheet names match report type

### AC6: Download Behavior
- **Given** I export a report
- **Then** the file downloads immediately
- **And** filename includes report type and date:
  - "DailyAttendance_2026-02-04.pdf"
  - "WeeklyAttendance_2026-02-03_2026-02-07.xlsx"

### AC7: Large Report Handling
- **Given** I export a report with 2000+ students
- **Then** the export completes within 30 seconds
- **And** I see a progress indicator: "Generating report..."

### AC8: Export Respects Filters
- **Given** I have filtered the report by Grade 5
- **When** I export
- **Then** the exported file contains only Grade 5 data
- **And** the filename reflects the filter: "DailyAttendance_Grade5_2026-02-04.pdf"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Very large report | Show progress, don't timeout |
| Export fails | Show error, allow retry |
| No data to export | Disable export buttons or show message |
| Special characters in data | Escape properly in both formats |
| Browser blocks download | Show link to download manually |
| Mobile device | Exports work (may open in app) |

---

## Test Scenarios

- [ ] PDF export button works
- [ ] Excel export button works
- [ ] PDF has correct layout and formatting
- [ ] PDF tables paginate correctly
- [ ] Excel has summary and detail sheets
- [ ] Excel filters enabled
- [ ] Excel conditional formatting works
- [ ] Filename format correct
- [ ] Large exports complete successfully
- [ ] Filtered exports contain filtered data
- [ ] Progress indicator shows for large exports
- [ ] Downloaded files open correctly

---

## Technical Notes

### PDF Generation
- Use QuestPDF library for .NET
- Template-based layout for consistency
- Embed school logo from settings

### Excel Generation
- Use ClosedXML or EPPlus library
- Memory-efficient streaming for large files
- Support .xlsx format (not legacy .xls)

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0045](US0045-daily-report.md) | Integration | Reports to export | Draft |
| [US0046](US0046-weekly-report.md) | Integration | Reports to export | Draft |
| [US0047](US0047-monthly-report.md) | Integration | Reports to export | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| QuestPDF or similar | Library | Not Started |
| ClosedXML or EPPlus | Library | Not Started |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
