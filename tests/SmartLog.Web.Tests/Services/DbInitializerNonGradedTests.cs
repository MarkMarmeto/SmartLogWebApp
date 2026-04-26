using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class DbInitializerNonGradedTests
{
    [Fact]
    public async Task SeedNonGraded_FreshDb_CreatesGradeLevelAndFourSections()
    {
        using var context = TestDbContextFactory.Create();

        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var ng = await context.GradeLevels.SingleAsync(g => g.Code == "NG");
        Assert.Equal("Non-Graded", ng.Name);
        Assert.Equal(99, ng.SortOrder);
        Assert.True(ng.IsActive);

        var sections = await context.Sections.Where(s => s.GradeLevelId == ng.Id).ToListAsync();
        Assert.Equal(4, sections.Count);
        Assert.All(sections, s => Assert.Null(s.ProgramId));
        Assert.All(sections, s => Assert.True(s.IsActive));

        var names = sections.Select(s => s.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "LEVEL 1", "LEVEL 2", "LEVEL 3", "LEVEL 4" }, names);
    }

    [Fact]
    public async Task SeedNonGraded_RunTwice_NoDuplicates()
    {
        using var context = TestDbContextFactory.Create();

        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);
        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        Assert.Equal(1, await context.GradeLevels.CountAsync(g => g.Code == "NG"));
        var ng = await context.GradeLevels.SingleAsync(g => g.Code == "NG");
        Assert.Equal(4, await context.Sections.CountAsync(s => s.GradeLevelId == ng.Id));
    }

    [Fact]
    public async Task SeedNonGraded_RemovesGradeLevelProgramJunctionForNG()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedPrograms(context);

        // Pre-seed NG with a stale junction row to REGULAR (legacy state).
        var ng = new GradeLevel { Code = "NG", Name = "Non-Graded", SortOrder = 99, IsActive = true };
        context.GradeLevels.Add(ng);
        await context.SaveChangesAsync();
        var regular = await context.Programs.SingleAsync(p => p.Code == "REGULAR");
        context.GradeLevelPrograms.Add(new GradeLevelProgram { GradeLevelId = ng.Id, ProgramId = regular.Id });
        await context.SaveChangesAsync();

        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var junctionForNg = await context.GradeLevelPrograms.CountAsync(j => j.GradeLevelId == ng.Id);
        Assert.Equal(0, junctionForNg);
    }

    [Fact]
    public async Task SeedNonGraded_NullsProgramIdOnLegacyNgSections()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedPrograms(context);

        var ng = new GradeLevel { Code = "NG", Name = "Non-Graded", SortOrder = 99, IsActive = true };
        context.GradeLevels.Add(ng);
        await context.SaveChangesAsync();
        var regular = await context.Programs.SingleAsync(p => p.Code == "REGULAR");

        // Legacy admin-renamed section pointing at REGULAR.
        var legacySection = new Section
        {
            Name = "Bridging A",
            GradeLevelId = ng.Id,
            ProgramId = regular.Id,
            Capacity = 40,
            IsActive = true
        };
        context.Sections.Add(legacySection);
        await context.SaveChangesAsync();

        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var refreshed = await context.Sections.SingleAsync(s => s.Id == legacySection.Id);
        Assert.Null(refreshed.ProgramId);
        Assert.Equal("Bridging A", refreshed.Name); // custom name preserved

        // LEVEL 1..4 added alongside the custom section (5 total NG sections).
        Assert.Equal(5, await context.Sections.CountAsync(s => s.GradeLevelId == ng.Id));
    }

    [Fact]
    public async Task SeedNonGraded_DoesNotTouchOtherGradeLevels()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedGradeLevels(context); // Grades 7-12
        TestDbContextFactory.SeedPrograms(context);

        var grade7 = await context.GradeLevels.SingleAsync(g => g.Code == "7");
        var regular = await context.Programs.SingleAsync(p => p.Code == "REGULAR");
        context.GradeLevelPrograms.Add(new GradeLevelProgram { GradeLevelId = grade7.Id, ProgramId = regular.Id });
        await context.SaveChangesAsync();

        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        // Grade 7 still exists, junction row preserved.
        Assert.True(await context.GradeLevels.AnyAsync(g => g.Code == "7"));
        Assert.Equal(1, await context.GradeLevelPrograms.CountAsync(j => j.GradeLevelId == grade7.Id));
        // Other grades all preserved.
        Assert.Equal(6, await context.GradeLevels.CountAsync(g => g.Code != "NG"));
    }

    [Fact]
    public async Task SeedNonGraded_RespectsAdminEditedNGName()
    {
        using var context = TestDbContextFactory.Create();
        var ng = new GradeLevel { Code = "NG", Name = "Special Education", SortOrder = 99, IsActive = true };
        context.GradeLevels.Add(ng);
        await context.SaveChangesAsync();

        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var refreshed = await context.GradeLevels.SingleAsync(g => g.Code == "NG");
        Assert.Equal("Special Education", refreshed.Name);
    }
}
