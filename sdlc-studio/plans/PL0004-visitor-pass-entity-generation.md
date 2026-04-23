# PL0004: Visitor Pass Entity & QR Generation — Implementation Plan

> **Status:** Done
> **Story:** [US0072: Visitor Pass Entity & QR Generation](../stories/US0072-visitor-pass-entity-generation.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-04-18
> **Language:** C# / ASP.NET Core 8 + EF Core

## Overview

Create `VisitorPass` and `VisitorScan` entities, generate visitor QR codes with `SMARTLOG-V:` prefix and HMAC-SHA256 signing, implement `VisitorPassService` for pass CRUD and batch generation, and seed `Visitor:MaxPasses` AppSettings key. This is the foundation story — all other EP0012 stories depend on these entities and the service layer.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | VisitorPass Entity | Table with Id, PassNumber, Code, QrPayload, HmacSignature, QrImageBase64, IsActive, IssuedAt, CurrentStatus |
| AC2 | VisitorScan Entity | Table with Id, VisitorPassId, DeviceId, ScanType, ScannedAt, ReceivedAt, Status, AcademicYearId |
| AC3 | QR Payload Format | `SMARTLOG-V:{code}:{unix_timestamp}:{base64_hmac}` with HMAC over `{code}:{timestamp}` |
| AC4 | Generate Passes | Create N passes (VISITOR-001 to VISITOR-N) with HMAC-signed QR and Base64 PNG image |
| AC5 | Increase Pass Count | Generate only new passes when MaxPasses increases |
| AC6 | Decrease Pass Count | Deactivate excess passes (highest numbers first), never delete |
| AC7 | AppSettings Seeded | `Visitor:MaxPasses` seeded with value "20" |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 + EF Core 8.0 + SQL Server
- **Test Framework:** xUnit + Moq + EF Core InMemory

### Existing Patterns
- **Entity conventions:** Guid PK, `[Required][StringLength(N)]` annotations, `DateTime.UtcNow` defaults, virtual navigation properties
- **QR generation:** `QrCodeService.GenerateQrCodeAsync()` — computes HMAC-SHA256 over `{studentId}:{timestamp}`, generates QR image via `QRCoder`, stores as Base64 PNG
- **HMAC key source:** AppSettings DB → env var `SMARTLOG_HMAC_SECRET_KEY` → appsettings.json (priority order)
- **DbContext config:** Fluent API in `OnModelCreating`, indexes on FK columns + composite query indexes, `DeleteBehavior.Restrict` on FKs
- **AppSettings seeding:** Idempotent insert-if-not-exists pattern in `DbInitializer.SeedAppSettingsAsync()`

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Entity/migration code is boilerplate best verified by build + integration tests. The `VisitorPassService` generation logic is the testable core — write it first, then add unit tests for batch generation, increase, decrease, and edge cases.

---

## Implementation Phases

### Phase 1: Entities & Migration
**Goal:** Create database schema for visitor passes and scans

- [ ] Create `src/SmartLog.Web/Data/Entities/VisitorPass.cs`
  - Guid Id (PK), int PassNumber (unique), string Code (max 20, unique), string QrPayload (max 500), string HmacSignature (max 100), string? QrImageBase64, bool IsActive (default true), DateTime IssuedAt, string CurrentStatus (max 20, default "Available")
  - Navigation: `ICollection<VisitorScan> VisitorScans`
- [ ] Create `src/SmartLog.Web/Data/Entities/VisitorScan.cs`
  - Guid Id (PK), Guid VisitorPassId (FK), Guid DeviceId (FK → Device), string ScanType (max 20), DateTime ScannedAt, DateTime ReceivedAt, string Status (max 50), Guid? AcademicYearId (FK)
  - Navigation: `VisitorPass VisitorPass`, `Device Device`, `AcademicYear? AcademicYear`
- [ ] Add `DbSet<VisitorPass>` and `DbSet<VisitorScan>` to `ApplicationDbContext`
- [ ] Configure entity in `OnModelCreating`:
  - VisitorPass: unique index on PassNumber, unique index on Code, index on CurrentStatus
  - VisitorScan: composite index on `(VisitorPassId, ScannedAt)`, index on ScannedAt, index on Status
  - FK: VisitorScan → VisitorPass with `DeleteBehavior.Restrict`
  - FK: VisitorScan → Device with `DeleteBehavior.Restrict`
  - FK: VisitorScan → AcademicYear with `DeleteBehavior.SetNull`
- [ ] Create EF Core migration `AddVisitorPassSystem`

### Phase 2: VisitorPassService
**Goal:** Implement pass generation, increase/decrease, and QR signing

- [ ] Create `src/SmartLog.Web/Services/IVisitorPassService.cs`
  - `Task<List<VisitorPass>> GeneratePassesAsync()` — generate up to MaxPasses
  - `Task<VisitorPass?> GetByCodeAsync(string code)`
  - `Task<List<VisitorPass>> GetAllAsync()`
  - `Task DeactivatePassAsync(Guid passId)`
  - `Task ActivatePassAsync(Guid passId)`
  - `Task SyncPassCountAsync()` — increase or decrease to match MaxPasses
  - `Task<int> GetMaxPassesAsync()`
  - `Task SetMaxPassesAsync(int count)`
- [ ] Create `src/SmartLog.Web/Services/VisitorPassService.cs`
  - Inject: `ApplicationDbContext`, `IQrCodeService` (for HMAC key retrieval), `IAppSettingsService`, `ILogger`
  - `GeneratePassesAsync()`: read MaxPasses, count existing, generate missing passes in batch
  - QR payload: `SMARTLOG-V:{code}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{hmac}`
  - HMAC: `HMAC-SHA256("{code}:{timestamp}")` using same key as student QR
  - QR image: use `QRCoder` to generate PNG, convert to Base64
  - `SyncPassCountAsync()`: if MaxPasses > existing → generate new; if MaxPasses < existing → deactivate highest-numbered excess
- [ ] Register `IVisitorPassService` / `VisitorPassService` in `Program.cs` DI

### Phase 3: AppSettings Seeding
**Goal:** Seed `Visitor:MaxPasses` default on first run

- [ ] Add to `DbInitializer.SeedAppSettingsAsync()`:
  ```
  Key: "Visitor:MaxPasses", Value: "20", Category: "Visitor",
  Description: "Maximum number of visitor passes to generate"
  ```

### Phase 4: QrCodeService Extension
**Goal:** Add visitor-aware QR parsing

- [ ] Add method to `IQrCodeService` / `QrCodeService`:
  - `(string Code, long Timestamp, string Signature)? ParseVisitorQrPayload(string payload)` — splits on `:`, validates `SMARTLOG-V` prefix, returns (code, timestamp, signature) or null
  - `Task<bool> VerifyVisitorQrAsync(string code, long timestamp, string signature)` — computes HMAC over `{code}:{timestamp}`, constant-time comparison

### Phase 5: Testing
**Goal:** Unit tests for VisitorPassService generation logic

- [ ] Create `tests/SmartLog.Web.Tests/Services/VisitorPassServiceTests.cs`
  - Test: GeneratePassesAsync creates correct number of passes
  - Test: Pass codes formatted as VISITOR-001, VISITOR-002, etc.
  - Test: QR payload starts with SMARTLOG-V:
  - Test: Increase pass count generates only new passes
  - Test: Decrease pass count deactivates highest-numbered
  - Test: Generate when all exist is no-op
  - Test: Negative MaxPasses throws ArgumentException
- [ ] Add visitor QR parse/verify tests to `QrCodeServiceTests.cs`

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | HMAC secret not configured | Reuse same `SMARTLOG_HMAC_SECRET_KEY` — `QrCodeService` already handles key resolution | Phase 2 |
| 2 | Pass number gap (e.g., 005 deleted) | Never delete passes; only deactivate. PassNumber is sequential, gaps impossible | Phase 2 |
| 3 | Generate called when all passes exist | Check `existingCount >= maxPasses`, return existing list, log info | Phase 2 |
| 4 | Database error during batch generation | Wrap in single `SaveChangesAsync` call — EF Core transaction auto-rollback on exception | Phase 2 |
| 5 | Very large MaxPasses (>100) | Allow but log warning "Large pass count: {count}. Consider if this many are needed." | Phase 2 |
| 6 | MaxPasses set to negative value | Throw `ArgumentException("Maximum passes must be at least 1")` in `SetMaxPassesAsync` | Phase 2 |
| 7 | Concurrent generate requests | Use `SemaphoreSlim(1,1)` in `VisitorPassService` to serialize generation calls | Phase 2 |

**Coverage:** 7/7 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| QRCoder dependency issue | Medium | Already used for student QR; same NuGet package |
| Large batch generation slow | Low | Max 100 passes; single SaveChangesAsync call |
| HMAC key not available at seed time | Low | Passes generated on-demand via admin UI, not at seed |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] Unit tests written and passing
- [ ] Edge cases handled
- [ ] Migration applies cleanly
- [ ] Build succeeds (0 errors)
