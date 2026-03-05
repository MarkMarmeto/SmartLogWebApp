using Microsoft.Extensions.Logging;
using Moq;
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
            () => service.EnrollStudentAsync(9999, section.Id, year.Id));
    }
}
