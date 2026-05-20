using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;
using ProgramEntity = SmartLog.Web.Data.Entities.Program;

namespace SmartLog.Web.Tests.Services;

public class BulkImportServiceTests
{
    // ── Fixtures ─────────────────────────────────────────────────────────────

    private static BulkImportService CreateService(ApplicationDbContext context)
    {
        var gradeSectionService = new GradeSectionService(context, NullLogger<GradeSectionService>.Instance);

        var idGenMock = new Mock<IIdGenerationService>();
        int idCounter = 0;
        idGenMock.Setup(x => x.GenerateStudentIdAsync())
            .ReturnsAsync(() => $"2026-07-{++idCounter:D4}");

        var qrMock = new Mock<IQrCodeService>();
        qrMock.Setup(x => x.GenerateQrCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((string sid) => new QrCode
            {
                Payload = $"SMARTLOG:{sid}:1234567890:FAKEHMACSIG",
                HmacSignature = "FAKEHMACSIG",
                IsValid = true,
                IssuedAt = DateTime.UtcNow
            });

        var yearMock = new Mock<IAcademicYearService>();
        yearMock.Setup(x => x.GetCurrentAcademicYearAsync())
            .ReturnsAsync(context.AcademicYears.FirstOrDefault(y => y.IsCurrent));

        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        return new BulkImportService(
            context,
            idGenMock.Object,
            qrMock.Object,
            gradeSectionService,
            yearMock.Object,
            auditMock.Object,
            NullLogger<BulkImportService>.Instance);
    }

    private static MemoryStream BuildXlsx(string[] headers, params object[][] dataRows)
    {
        var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Students");
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            for (int r = 0; r < dataRows.Length; r++)
                for (int c = 0; c < dataRows[r].Length; c++)
                    ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(dataRows[r][c]);
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static string[] NewHeaders => new[]
    {
        "FirstName", "LastName", "MiddleName", "GradeLevel", "Program", "Section",
        "ParentGuardianName", "GuardianRelationship", "ParentPhone",
        "AlternatePhone", "LRN", "SmsLanguage"
    };

    private static string[] LegacyHeaders => new[]
    {
        "FirstName", "LastName", "MiddleName", "GradeLevel", "Section",
        "ParentGuardianName", "GuardianRelationship", "ParentPhone",
        "AlternatePhone", "LRN", "SmsLanguage"
    };

    private static object[] GradedRow(string grade, string program, string section) => new object[]
    {
        "Juan", "Dela Cruz", "", grade, program, section,
        "Maria Dela Cruz", "Mother", "09171234567", "", "", "EN"
    };

    private static object[] NgRow(string section) => new object[]
    {
        "Liza", "Cruz", "", "NG", "", section,
        "Ana Cruz", "Mother", "09451234567", "", "", "EN"
    };

    private static object[] LegacyGradedRow(string grade, string section) => new object[]
    {
        "Juan", "Dela Cruz", "", grade, section,
        "Maria Dela Cruz", "Mother", "09171234567", "", "", "EN"
    };

    // ── Seed helpers ─────────────────────────────────────────────────────────

    private static (GradeLevel grade7, ProgramEntity regular, Section sectionA) SeedBasicGraded(ApplicationDbContext context)
    {
        TestDbContextFactory.SeedAll(context);
        var grade7 = context.GradeLevels.First(g => g.Code == "7");
        var regular = context.Programs.First(p => p.Code == "REGULAR");
        // GradeLevelProgram link is NOT seeded by SeedAll — add it
        if (!context.GradeLevelPrograms.Any(glp => glp.GradeLevelId == grade7.Id && glp.ProgramId == regular.Id))
        {
            context.GradeLevelPrograms.Add(new GradeLevelProgram { GradeLevelId = grade7.Id, ProgramId = regular.Id });
            context.SaveChanges();
        }
        var sectionA = context.Sections.First(s => s.GradeLevelId == grade7.Id && s.Name == "Section A");
        return (grade7, regular, sectionA);
    }

    private static GradeLevel SeedNg(ApplicationDbContext context)
    {
        var ng = new GradeLevel { Code = "NG", Name = "Non-Graded", IsActive = true, SortOrder = 99 };
        context.GradeLevels.Add(ng);
        var ngSection = new Section { Name = "LEVEL 1", GradeLevelId = ng.Id, ProgramId = null, IsActive = true };
        context.Sections.Add(ngSection);
        context.SaveChanges();
        return ng;
    }

    private static void SeedAmbiguousSections(ApplicationDbContext context, GradeLevel grade11)
    {
        var stem = new ProgramEntity { Code = "STEM", Name = "STEM", IsActive = true, SortOrder = 10 };
        var abm  = new ProgramEntity { Code = "ABM",  Name = "ABM",  IsActive = true, SortOrder = 11 };
        context.Programs.AddRange(stem, abm);
        context.GradeLevelPrograms.AddRange(
            new GradeLevelProgram { GradeLevelId = grade11.Id, ProgramId = stem.Id },
            new GradeLevelProgram { GradeLevelId = grade11.Id, ProgramId = abm.Id });
        context.Sections.AddRange(
            new Section { Name = "RUBY", GradeLevelId = grade11.Id, ProgramId = stem.Id, IsActive = true },
            new Section { Name = "RUBY", GradeLevelId = grade11.Id, ProgramId = abm.Id,  IsActive = true });
        context.SaveChanges();
    }

    // ── AC4: Program required for graded rows (new template) ─────────────────

    [Fact]
    public async Task Validate_NewTemplate_GradedRowMissingProgram_FailsAC4()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders,
            new object[] { "Juan", "Dela Cruz", "", "7", "", "Section A", "Maria Dela Cruz", "Mother", "09171234567", "", "", "EN" });

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.Field == "Program" && e.Message.Contains("required for graded"));
    }

    // ── AC5: Program forbidden for NG rows ───────────────────────────────────

    [Fact]
    public async Task Validate_NewTemplate_NgRowWithProgram_FailsAC5()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        SeedNg(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders,
            new object[] { "Liza", "Cruz", "", "NG", "REGULAR", "LEVEL 1", "Ana Cruz", "Mother", "09451234567", "", "", "EN" });

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.Field == "Program" && e.Message.Contains("Non-Graded rows must leave Program blank"));
    }

    // ── AC6: Unknown program code ─────────────────────────────────────────────

    [Fact]
    public async Task Validate_UnknownProgramCode_FailsAC6()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, GradedRow("7", "XYZ", "Section A"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.Field == "Program" && e.Message.Contains("not found or inactive"));
    }

    // ── AC6: Program not linked to the grade ──────────────────────────────────

    [Fact]
    public async Task Validate_ProgramNotLinkedToGrade_FailsAC6()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        // STEM program exists but is NOT linked to grade 7
        var stem = new ProgramEntity { Code = "STEM", Name = "STEM", IsActive = true, SortOrder = 10 };
        context.Programs.Add(stem);
        context.SaveChanges();
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, GradedRow("7", "STEM", "Section A"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.Field == "Program" && e.Message.Contains("not allowed for grade"));
    }

    // ── AC7: Section not found under (grade, program) ────────────────────────

    [Fact]
    public async Task Validate_SectionNotUnderGradeProgram_FailsAC7()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, GradedRow("7", "REGULAR", "NonExistentSection"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.Field == "Section" && e.Message.Contains("not found"));
    }

    // ── AC7: Happy path — graded row resolves correctly ───────────────────────

    [Fact]
    public async Task Validate_NewTemplate_ValidGradedRow_PassesAndSetsProgramCode()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, GradedRow("7", "REGULAR", "Section A"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal("REGULAR", result.ValidStudentRows[0].Row.ProgramCode);
    }

    // ── AC8: NG section not found ─────────────────────────────────────────────

    [Fact]
    public async Task Validate_NgSectionNotFound_FailsAC8()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        SeedNg(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, NgRow("ZZ-DOESNT-EXIST"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.Field == "Section" && e.Message.Contains("—"));
    }

    // ── AC8: Happy path — NG row resolves correctly ───────────────────────────

    [Fact]
    public async Task Validate_NewTemplate_ValidNgRow_PassesWithNullProgram()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        SeedNg(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, NgRow("LEVEL 1"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(1, result.ValidCount);
        Assert.Null(result.ValidStudentRows[0].Row.ProgramCode);
    }

    // ── AC9: Legacy template, unambiguous section — auto-resolves ────────────

    [Fact]
    public async Task Validate_LegacyTemplate_UnambiguousSection_AutoResolvesProgram()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(LegacyHeaders, LegacyGradedRow("7", "Section A"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(1, result.ValidCount);
        // Backfilled from the resolved section's program
        Assert.Equal("REGULAR", result.ValidStudentRows[0].Row.ProgramCode);
    }

    // ── AC9: Legacy template, ambiguous section — fails with disambiguation msg

    [Fact]
    public async Task Validate_LegacyTemplate_AmbiguousSection_FailsWithDisambiguationError()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        var grade11 = context.GradeLevels.First(g => g.Code == "11");
        SeedAmbiguousSections(context, grade11);
        var service = CreateService(context);

        using var stream = BuildXlsx(LegacyHeaders, LegacyGradedRow("11", "RUBY"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e =>
            e.Field == "Program" && e.Message.Contains("multiple programs") && e.Message.Contains("disambiguate"));
    }

    // ── AC10: Import persistence — graded row sets Student.Program ────────────

    [Fact]
    public async Task Import_GradedRow_PersistsStudentProgramFromSection()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        var rows = new List<StudentImportRow>
        {
            new() {
                RowNumber = 2, FirstName = "Juan", LastName = "Dela Cruz",
                GradeLevelCode = "7", ProgramCode = "REGULAR", SectionName = "Section A",
                ParentGuardianName = "Maria Dela Cruz", GuardianRelationship = "Mother",
                ParentPhone = "09171234567", SmsLanguage = "EN"
            }
        };

        var importResult = await service.ImportStudentsAsync(rows, "test-user");

        Assert.Equal(1, importResult.TotalImported);
        var saved = context.Students.Single();
        Assert.Equal("REGULAR", saved.Program);
        Assert.Equal("7", saved.GradeLevel);
    }

    // ── AC10: Import persistence — NG row sets Student.Program to null ────────

    [Fact]
    public async Task Import_NgRow_PersistsStudentProgramAsNull()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        SeedNg(context);
        var service = CreateService(context);

        var rows = new List<StudentImportRow>
        {
            new() {
                RowNumber = 2, FirstName = "Liza", LastName = "Cruz",
                GradeLevelCode = "NG", ProgramCode = null, SectionName = "LEVEL 1",
                ParentGuardianName = "Ana Cruz", GuardianRelationship = "Mother",
                ParentPhone = "09451234567", SmsLanguage = "EN"
            }
        };

        var importResult = await service.ImportStudentsAsync(rows, "test-user");

        Assert.Equal(1, importResult.TotalImported);
        var saved = context.Students.Single();
        Assert.Null(saved.Program);
        Assert.Equal("NG", saved.GradeLevel);
    }

    // ── Case-insensitivity ────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_LowercaseProgramAndSection_Matches()
    {
        using var context = TestDbContextFactory.Create();
        SeedBasicGraded(context);
        var service = CreateService(context);

        using var stream = BuildXlsx(NewHeaders, GradedRow("7", "regular", "section a"));

        var result = await service.ValidateStudentXlsxAsync(stream);

        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(1, result.ValidCount);
    }
}
