using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models.Sms;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

/// <summary>
/// US0084: Program-first targeting — ResolveStudentIdsByFiltersAsync UNION semantics.
/// </summary>
public class ProgramFirstTargetingTests
{
    private static SmsService CreateService(Data.ApplicationDbContext context)
    {
        return new SmsService(
            context,
            new Mock<ISmsTemplateService>().Object,
            new Mock<ISmsSettingsService>().Object,
            new Mock<IAppSettingsService>().Object,
            new Mock<ILogger<SmsService>>().Object);
    }

    private static Student MakeStudent(string id, string program, string grade,
        bool active = true, bool smsEnabled = true, string phone = "09171234567") => new()
    {
        StudentId = id,
        FirstName = "Test",
        LastName = "Student",
        GradeLevel = grade,
        Section = "A",
        Program = program,
        ParentGuardianName = "Parent",
        GuardianRelationship = "Mother",
        ParentPhone = phone,
        IsActive = active,
        SmsEnabled = smsEnabled
    };

    [Fact]
    public async Task EmptyFilters_ReturnsAllActiveAndSmsEnabled()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Students.AddRange(
            MakeStudent("2026-07-0001", "REGULAR", "7"),
            MakeStudent("2026-07-0002", "SPA", "8"),
            MakeStudent("2026-07-0003", "REGULAR", "9", active: false),
            MakeStudent("2026-07-0004", "SPA", "10", smsEnabled: false));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>());

        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public async Task SingleFilter_MatchesProgramAndGrade()
    {
        var ctx = TestDbContextFactory.Create();
        var s1 = MakeStudent("2026-07-0001", "REGULAR", "7");
        var s2 = MakeStudent("2026-07-0002", "REGULAR", "8");
        var s3 = MakeStudent("2026-07-0003", "SPA", "7");
        ctx.Students.AddRange(s1, s2, s3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } }
        });

        Assert.Single(ids);
        Assert.Equal(s1.Id, ids[0]);
    }

    [Fact]
    public async Task MultipleFilters_ReturnsUnion_NoDuplicates()
    {
        var ctx = TestDbContextFactory.Create();
        var s1 = MakeStudent("2026-07-0001", "REGULAR", "7");
        var s2 = MakeStudent("2026-07-0002", "SPA", "8");
        var s3 = MakeStudent("2026-07-0003", "STEM", "9");
        ctx.Students.AddRange(s1, s2, s3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } },
            new() { ProgramCode = "SPA",     GradeLevelCodes = new List<string> { "8" } }
        });

        Assert.Equal(2, ids.Count);
        Assert.Contains(s1.Id, ids);
        Assert.Contains(s2.Id, ids);
    }

    [Fact]
    public async Task Filter_MultipleGrades_MatchesAll()
    {
        var ctx = TestDbContextFactory.Create();
        var s1 = MakeStudent("2026-07-0001", "SPA", "7");
        var s2 = MakeStudent("2026-07-0002", "SPA", "8");
        var s3 = MakeStudent("2026-07-0003", "SPA", "9");
        ctx.Students.AddRange(s1, s2, s3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "SPA", GradeLevelCodes = new List<string> { "7", "8" } }
        });

        Assert.Equal(2, ids.Count);
        Assert.Contains(s1.Id, ids);
        Assert.Contains(s2.Id, ids);
    }

    [Fact]
    public async Task ActiveOnly_False_IncludesInactiveStudents()
    {
        var ctx = TestDbContextFactory.Create();
        var active = MakeStudent("2026-07-0001", "REGULAR", "7", active: true);
        var inactive = MakeStudent("2026-07-0002", "REGULAR", "7", active: false);
        ctx.Students.AddRange(active, inactive);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(
            new List<ProgramGradeFilter>
            {
                new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } }
            },
            activeOnly: false);

        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public async Task SmsEnabledOnly_False_IncludesSmsDisabled()
    {
        var ctx = TestDbContextFactory.Create();
        var enabled = MakeStudent("2026-07-0001", "REGULAR", "7", smsEnabled: true);
        var disabled = MakeStudent("2026-07-0002", "REGULAR", "7", smsEnabled: false);
        ctx.Students.AddRange(enabled, disabled);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(
            new List<ProgramGradeFilter>
            {
                new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } }
            },
            smsEnabledOnly: false);

        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public async Task OverlappingFilters_StudentMatchingBoth_AppearsOnce()
    {
        var ctx = TestDbContextFactory.Create();
        var s = MakeStudent("2026-07-0001", "REGULAR", "7");
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } },
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } }
        });

        Assert.Single(ids);
    }

    [Fact]
    public async Task Filter_NoMatchingStudents_ReturnsEmpty()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Students.Add(MakeStudent("2026-07-0001", "REGULAR", "7"));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "STEM", GradeLevelCodes = new List<string> { "11" } }
        });

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Filter_EmptyGradeLevelCodes_MatchesAllGradesForProgram()
    {
        var ctx = TestDbContextFactory.Create();
        var s1 = MakeStudent("2026-07-0001", "REGULAR", "7");
        var s2 = MakeStudent("2026-07-0002", "REGULAR", "10");
        var s3 = MakeStudent("2026-07-0003", "SPA", "7");
        ctx.Students.AddRange(s1, s2, s3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string>() }
        });

        // Empty grade list means "all grades for this program"
        Assert.Equal(2, ids.Count);
        Assert.Contains(s1.Id, ids);
        Assert.Contains(s2.Id, ids);
    }

    // --- US0107: Non-Graded targeting branch ---

    private static Student MakeNgStudent(string id, string section,
        bool active = true, bool smsEnabled = true) => new()
    {
        StudentId = id,
        FirstName = "NG",
        LastName = "Learner",
        GradeLevel = "NG",
        Section = section,
        Program = null,
        ParentGuardianName = "Parent",
        GuardianRelationship = "Mother",
        ParentPhone = "09171234567",
        IsActive = active,
        SmsEnabled = smsEnabled
    };

    [Fact]
    public async Task OnlyProgramFilter_ExcludesNGStudents()
    {
        var ctx = TestDbContextFactory.Create();
        var graded = MakeStudent("2026-07-0001", "REGULAR", "7");
        var ng = MakeNgStudent("2026-07-1001", "LEVEL 1");
        ctx.Students.AddRange(graded, ng);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new List<string> { "7" } }
        });

        Assert.Single(ids);
        Assert.Equal(graded.Id, ids[0]);
    }

    [Fact]
    public async Task OnlyNGFilter_ReturnsOnlyNGStudents()
    {
        var ctx = TestDbContextFactory.Create();
        var graded = MakeStudent("2026-07-0001", "REGULAR", "7");
        var ng = MakeNgStudent("2026-07-1001", "LEVEL 1");
        ctx.Students.AddRange(graded, ng);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "", GradeLevelCodes = new(), SectionNames = new() { "LEVEL 1" } }
        });

        Assert.Single(ids);
        Assert.Equal(ng.Id, ids[0]);
    }

    [Fact]
    public async Task CombinedProgramAndNG_UnionsResults_NoDuplicates()
    {
        var ctx = TestDbContextFactory.Create();
        var graded = MakeStudent("2026-07-0001", "REGULAR", "7");
        var ng = MakeNgStudent("2026-07-1001", "LEVEL 1");
        ctx.Students.AddRange(graded, ng);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "REGULAR", GradeLevelCodes = new() { "7" } },
            new() { ProgramCode = "",        GradeLevelCodes = new(), SectionNames = new() { "LEVEL 1" } }
        });

        Assert.Equal(2, ids.Count);
        Assert.Contains(graded.Id, ids);
        Assert.Contains(ng.Id, ids);
    }

    [Fact]
    public async Task NGFilterWithSpecificSection_FiltersToThatSection()
    {
        var ctx = TestDbContextFactory.Create();
        var ngL1 = MakeNgStudent("2026-07-1001", "LEVEL 1");
        var ngL2 = MakeNgStudent("2026-07-1002", "LEVEL 2");
        ctx.Students.AddRange(ngL1, ngL2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "", GradeLevelCodes = new(), SectionNames = new() { "LEVEL 2" } }
        });

        Assert.Single(ids);
        Assert.Equal(ngL2.Id, ids[0]);
    }

    [Fact]
    public async Task NGFilter_RespectsActiveAndSmsEnabledFlags()
    {
        var ctx = TestDbContextFactory.Create();
        var ng = MakeNgStudent("2026-07-1001", "LEVEL 1", smsEnabled: false);
        ctx.Students.Add(ng);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ids = await svc.ResolveStudentIdsByFiltersAsync(new List<ProgramGradeFilter>
        {
            new() { ProgramCode = "", GradeLevelCodes = new(), SectionNames = new() { "LEVEL 1" } }
        });

        Assert.Empty(ids);
    }
}
