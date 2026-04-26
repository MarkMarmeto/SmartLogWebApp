using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class GradeSectionServiceTests
{
    private readonly Mock<ILogger<GradeSectionService>> _logger = new();

    private GradeSectionService CreateService(SmartLog.Web.Data.ApplicationDbContext context)
    {
        return new GradeSectionService(context, _logger.Object);
    }

    [Fact]
    public async Task EnrollStudentAsync_CreatesEnrollmentAndUpdatesDenormalized()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var student = TestDbContextFactory.CreateStudent(context, "Test", "Student", "2025-07-0001", "7");
        var section = context.Sections.First(s => s.GradeLevel.Code == "8");
        var year = context.AcademicYears.First();

        var service = CreateService(context);
        var enrollment = await service.EnrollStudentAsync(student.Id, section.Id, year.Id);

        Assert.True(enrollment.IsActive);
        Assert.Equal(student.Id, enrollment.StudentId);
        Assert.Equal(section.Id, enrollment.SectionId);

        var updatedStudent = context.Students.Find(student.Id)!;
        Assert.Equal(enrollment.Id, updatedStudent.CurrentEnrollmentId);
        Assert.Equal("8", updatedStudent.GradeLevel);
        Assert.Equal(section.Name, updatedStudent.Section);
    }

    [Fact]
    public async Task EnrollStudentAsync_ThrowsOnDuplicateEnrollment()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var student = TestDbContextFactory.CreateStudent(context, "Dup", "Student", "2025-07-0002", "7");
        var section = context.Sections.First(s => s.GradeLevel.Code == "7");
        var year = context.AcademicYears.First();

        var service = CreateService(context);
        await service.EnrollStudentAsync(student.Id, section.Id, year.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnrollStudentAsync(student.Id, section.Id, year.Id));
        Assert.Contains("already has an active enrollment", ex.Message);
    }

    [Fact]
    public async Task TransferStudentAsync_DeactivatesOldAndCreatesNew()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var student = TestDbContextFactory.CreateStudent(context, "Transfer", "Student", "2025-07-0003", "7");
        var sectionA = context.Sections.First(s => s.GradeLevel.Code == "7" && s.Name == "Section A");
        var sectionB = context.Sections.First(s => s.GradeLevel.Code == "7" && s.Name == "Section B");
        var year = context.AcademicYears.First();

        var service = CreateService(context);

        // First enroll
        await service.EnrollStudentAsync(student.Id, sectionA.Id, year.Id);

        // Then transfer
        var newEnrollment = await service.TransferStudentAsync(student.Id, sectionB.Id, year.Id);

        Assert.True(newEnrollment.IsActive);
        Assert.Equal(sectionB.Id, newEnrollment.SectionId);

        // Old enrollment should be deactivated
        var enrollments = context.StudentEnrollments
            .Where(e => e.StudentId == student.Id).ToList();
        Assert.Equal(2, enrollments.Count);
        Assert.Single(enrollments.Where(e => e.IsActive));
        Assert.Single(enrollments.Where(e => !e.IsActive));

        // Student's current enrollment should point to new one
        var updatedStudent = context.Students.Find(student.Id)!;
        Assert.Equal(newEnrollment.Id, updatedStudent.CurrentEnrollmentId);
        Assert.Equal(sectionB.Name, updatedStudent.Section);
    }

    [Fact]
    public async Task TransferStudentAsync_ThrowsWhenNoActiveEnrollment()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var student = TestDbContextFactory.CreateStudent(context, "NoEnroll", "Student", "2025-07-0004", "7");
        var section = context.Sections.First(s => s.GradeLevel.Code == "7");
        var year = context.AcademicYears.First();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TransferStudentAsync(student.Id, section.Id, year.Id));
        Assert.Contains("No active enrollment", ex.Message);
    }

    [Fact]
    public async Task EnrollStudentAsync_ThrowsForInvalidStudent()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var section = context.Sections.First();
        var year = context.AcademicYears.First();

        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnrollStudentAsync(Guid.NewGuid(), section.Id, year.Id));
    }

    // US0103: NG vs graded ProgramId rules.

    private static GradeLevel SeedNonGradedLevel(SmartLog.Web.Data.ApplicationDbContext context)
    {
        var ng = new GradeLevel { Code = "NG", Name = "Non-Graded", SortOrder = 99, IsActive = true };
        context.GradeLevels.Add(ng);
        context.SaveChanges();
        return ng;
    }

    [Fact]
    public async Task CreateSectionAsync_GradedWithProgram_Succeeds()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedGradeLevels(context);
        TestDbContextFactory.SeedPrograms(context);
        var grade7 = context.GradeLevels.First(g => g.Code == "7");
        var regular = context.Programs.First(p => p.Code == "REGULAR");

        var service = CreateService(context);
        var section = await service.CreateSectionAsync(grade7.Id, "7-A", regular.Id);

        Assert.NotNull(section);
        Assert.Equal(regular.Id, section.ProgramId);
    }

    [Fact]
    public async Task CreateSectionAsync_GradedWithoutProgram_Throws()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedGradeLevels(context);
        var grade7 = context.GradeLevels.First(g => g.Code == "7");

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateSectionAsync(grade7.Id, "7-A", programId: null));
        Assert.Contains("required for graded", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSectionAsync_NonGradedWithProgram_Throws()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedGradeLevels(context);
        TestDbContextFactory.SeedPrograms(context);
        var ng = SeedNonGradedLevel(context);
        var regular = context.Programs.First(p => p.Code == "REGULAR");

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateSectionAsync(ng.Id, "LEVEL 1", regular.Id));
        Assert.Contains("must not have a Program", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSectionAsync_NonGradedWithoutProgram_Succeeds()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedGradeLevels(context);
        var ng = SeedNonGradedLevel(context);

        var service = CreateService(context);
        var section = await service.CreateSectionAsync(ng.Id, "LEVEL 1", programId: null);

        Assert.NotNull(section);
        Assert.Null(section.ProgramId);
        Assert.Equal(ng.Id, section.GradeLevelId);
    }

    [Fact]
    public async Task UpdateSectionAsync_NonGradedWithProgram_Throws()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedGradeLevels(context);
        TestDbContextFactory.SeedPrograms(context);
        var ng = SeedNonGradedLevel(context);
        var regular = context.Programs.First(p => p.Code == "REGULAR");

        var service = CreateService(context);
        var section = await service.CreateSectionAsync(ng.Id, "LEVEL 1", programId: null);

        section.ProgramId = regular.Id;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateSectionAsync(section));
        Assert.Contains("must not have a Program", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSectionAsync_GradedClearsProgram_Throws()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedSections(context);
        var section = context.Sections.AsNoTracking().First(s => s.GradeLevel.Code == "7");

        var service = CreateService(context);
        var detached = new Section
        {
            Id = section.Id,
            Name = section.Name,
            GradeLevelId = section.GradeLevelId,
            ProgramId = null,
            AdviserId = section.AdviserId,
            Capacity = section.Capacity,
            IsActive = section.IsActive,
            CreatedAt = section.CreatedAt
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateSectionAsync(detached));
        Assert.Contains("required for graded", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // US0106: Student.Program denormalisation for NG enrollments.

    [Fact]
    public async Task EnrollStudentAsync_GradedSection_SetsProgramCode()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var student = TestDbContextFactory.CreateStudent(context, "Grace", "Graded", "2025-07-1101", "7");
        var section = context.Sections.Include(s => s.Program).First(s => s.GradeLevel.Code == "7");
        var year = context.AcademicYears.First();

        var service = CreateService(context);
        await service.EnrollStudentAsync(student.Id, section.Id, year.Id);

        var updated = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Equal("REGULAR", updated.Program);
    }

    [Fact]
    public async Task EnrollStudentAsync_NonGradedSection_SetsProgramNull()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var student = TestDbContextFactory.CreateStudent(context, "Nat", "NonGrad", "2025-07-1102", "NG");
        var ngSection = context.Sections.First(s => s.GradeLevel.Code == "NG" && s.Name == "LEVEL 1");
        var year = context.AcademicYears.First();

        var service = CreateService(context);
        await service.EnrollStudentAsync(student.Id, ngSection.Id, year.Id);

        var updated = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Null(updated.Program);
        Assert.Equal("NG", updated.GradeLevel);
        Assert.Equal("LEVEL 1", updated.Section);
    }

    [Fact]
    public async Task TransferStudentAsync_GradedToNG_NullsProgram()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var student = TestDbContextFactory.CreateStudent(context, "Tracey", "Transfer", "2025-07-1103", "7");
        var graded = context.Sections.First(s => s.GradeLevel.Code == "7");
        var ng = context.Sections.First(s => s.GradeLevel.Code == "NG" && s.Name == "LEVEL 2");
        var year = context.AcademicYears.First();

        var service = CreateService(context);
        await service.EnrollStudentAsync(student.Id, graded.Id, year.Id);

        var afterEnroll = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Equal("REGULAR", afterEnroll.Program);

        await service.TransferStudentAsync(student.Id, ng.Id, year.Id);

        var afterTransfer = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Null(afterTransfer.Program);
        Assert.Equal("LEVEL 2", afterTransfer.Section);
    }

    [Fact]
    public async Task TransferStudentAsync_NGToGraded_SetsProgramCode()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var student = TestDbContextFactory.CreateStudent(context, "Ngugi", "Mover", "2025-07-1104", "NG");
        var ng = context.Sections.First(s => s.GradeLevel.Code == "NG" && s.Name == "LEVEL 1");
        var graded = context.Sections.First(s => s.GradeLevel.Code == "8");
        var year = context.AcademicYears.First();

        var service = CreateService(context);
        await service.EnrollStudentAsync(student.Id, ng.Id, year.Id);

        var afterEnroll = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Null(afterEnroll.Program);

        await service.TransferStudentAsync(student.Id, graded.Id, year.Id);

        var afterTransfer = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Equal("REGULAR", afterTransfer.Program);
    }
}
