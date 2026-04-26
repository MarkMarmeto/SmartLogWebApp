using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class BatchReenrollmentServiceTests
{
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<ILogger<BatchReenrollmentService>> _logger = new();

    private BatchReenrollmentService CreateService(SmartLog.Web.Data.ApplicationDbContext context)
    {
        return new BatchReenrollmentService(context, _auditService.Object, _logger.Object);
    }

    [Fact]
    public async Task GeneratePreviewAsync_ThrowsWhenSourceEqualsTarget()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var sameId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GeneratePreviewAsync(sameId, sameId));
    }

    [Fact]
    public async Task GeneratePreviewAsync_PromotesStudentsToNextGrade()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var sourceYear = years[0];
        var targetYear = years[1];

        var grade7Sections = context.Sections
            .Where(s => s.GradeLevel.Code == "7").ToList();

        var student = TestDbContextFactory.CreateStudent(context, "Juan", "Cruz", "2025-07-0001", "7");

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = grade7Sections[0].Id,
            AcademicYearId = sourceYear.Id,
            IsActive = true
        });
        context.SaveChanges();

        var service = CreateService(context);
        var preview = await service.GeneratePreviewAsync(sourceYear.Id, targetYear.Id);

        var promoted = preview.Students.Where(s => s.Action == PromotionAction.Promote).ToList();
        Assert.Single(promoted);
        Assert.Equal("8", promoted[0].TargetGradeCode);
        Assert.NotNull(promoted[0].AssignedSectionId);
    }

    [Fact]
    public async Task GeneratePreviewAsync_SkipsInactiveStudents()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var grade7Section = context.Sections.First(s => s.GradeLevel.Code == "7");

        var student = TestDbContextFactory.CreateStudent(context, "Ana", "Santos", "2025-07-0002", "7", isActive: false);

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = grade7Section.Id,
            AcademicYearId = years[0].Id,
            IsActive = true
        });
        context.SaveChanges();

        var service = CreateService(context);
        var preview = await service.GeneratePreviewAsync(years[0].Id, years[1].Id);

        var skipped = preview.Students.Where(s => s.Action == PromotionAction.Skip).ToList();
        Assert.Single(skipped);
        Assert.Contains("inactive", skipped[0].SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneratePreviewAsync_SkipsAlreadyEnrolledStudents()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var grade7Section = context.Sections.First(s => s.GradeLevel.Code == "7");
        var grade8Section = context.Sections.First(s => s.GradeLevel.Code == "8");

        var student = TestDbContextFactory.CreateStudent(context, "Pedro", "Reyes", "2025-07-0003", "7");

        // Enrolled in source
        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = grade7Section.Id,
            AcademicYearId = years[0].Id,
            IsActive = true
        });
        // Already enrolled in target
        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = grade8Section.Id,
            AcademicYearId = years[1].Id,
            IsActive = true
        });
        context.SaveChanges();

        var service = CreateService(context);
        var preview = await service.GeneratePreviewAsync(years[0].Id, years[1].Id);

        var skipped = preview.Students.Where(s => s.Action == PromotionAction.Skip).ToList();
        Assert.Single(skipped);
        Assert.Contains("Already enrolled", skipped[0].SkipReason);
    }

    [Fact]
    public async Task GeneratePreviewAsync_GraduatesHighestGrade()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var grade12Section = context.Sections.First(s => s.GradeLevel.Code == "12");

        var student = TestDbContextFactory.CreateStudent(context, "Maria", "Dela Cruz", "2025-12-0001", "12");

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = grade12Section.Id,
            AcademicYearId = years[0].Id,
            IsActive = true
        });
        context.SaveChanges();

        var service = CreateService(context);
        var preview = await service.GeneratePreviewAsync(years[0].Id, years[1].Id);

        var graduated = preview.Students.Where(s => s.Action == PromotionAction.Graduate).ToList();
        Assert.Single(graduated);
    }

    [Fact]
    public async Task GeneratePreviewAsync_DistributesSectionsEvenly()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var grade7Section = context.Sections.First(s => s.GradeLevel.Code == "7");

        // Create 4 students to distribute across 2 Grade 8 sections
        for (int i = 1; i <= 4; i++)
        {
            var s = TestDbContextFactory.CreateStudent(context, $"Student{i}", "Test", $"2025-07-{i:D4}", "7");
            context.StudentEnrollments.Add(new StudentEnrollment
            {
                StudentId = s.Id,
                SectionId = grade7Section.Id,
                AcademicYearId = years[0].Id,
                IsActive = true
            });
        }
        context.SaveChanges();

        var service = CreateService(context);
        var preview = await service.GeneratePreviewAsync(years[0].Id, years[1].Id);

        var promoted = preview.Students.Where(s => s.Action == PromotionAction.Promote).ToList();
        Assert.Equal(4, promoted.Count);

        // Should distribute across both sections (2 each)
        var sectionGroups = promoted.GroupBy(s => s.AssignedSectionId).ToList();
        Assert.Equal(2, sectionGroups.Count);
        Assert.All(sectionGroups, g => Assert.Equal(2, g.Count()));
    }

    [Fact]
    public async Task ExecuteReenrollmentAsync_CreatesEnrollmentsAndUpdatesFields()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var grade8Section = context.Sections.First(s => s.GradeLevel.Code == "8");

        var student = TestDbContextFactory.CreateStudent(context, "Jose", "Garcia", "2025-07-0010", "7");

        var service = CreateService(context);
        var assignments = new List<StudentPromotionAssignment>
        {
            new() { StudentId = student.Id, Action = PromotionAction.Promote, SectionId = grade8Section.Id }
        };

        var result = await service.ExecuteReenrollmentAsync(years[0].Id, years[1].Id, assignments, "test-user");

        Assert.Equal(1, result.PromotedCount);
        Assert.Empty(result.Errors);

        // Verify enrollment was created
        var enrollment = context.StudentEnrollments
            .FirstOrDefault(e => e.StudentId == student.Id && e.AcademicYearId == years[1].Id);
        Assert.NotNull(enrollment);
        Assert.True(enrollment.IsActive);

        // Verify denormalized fields updated
        var updatedStudent = context.Students.Find(student.Id)!;
        Assert.Equal("8", updatedStudent.GradeLevel);
        Assert.Equal(grade8Section.Name, updatedStudent.Section);
        Assert.Equal(enrollment.Id, updatedStudent.CurrentEnrollmentId);
    }

    [Fact]
    public async Task ExecuteReenrollmentAsync_GraduatesGrade12()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);

        var years = context.AcademicYears.ToList();
        var grade12Section = context.Sections.First(s => s.GradeLevel.Code == "12");

        var student = TestDbContextFactory.CreateStudent(context, "Grad", "Student", "2025-12-0010", "12");

        // Create source enrollment
        var sourceEnrollment = new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = grade12Section.Id,
            AcademicYearId = years[0].Id,
            IsActive = true
        };
        context.StudentEnrollments.Add(sourceEnrollment);
        context.SaveChanges();
        student.CurrentEnrollmentId = sourceEnrollment.Id;
        context.SaveChanges();

        var service = CreateService(context);
        var assignments = new List<StudentPromotionAssignment>
        {
            new() { StudentId = student.Id, Action = PromotionAction.Graduate }
        };

        var result = await service.ExecuteReenrollmentAsync(years[0].Id, years[1].Id, assignments, "test-user");

        Assert.Equal(1, result.GraduatedCount);

        var updatedStudent = context.Students.Find(student.Id)!;
        Assert.Null(updatedStudent.CurrentEnrollmentId);
    }

    [Fact]
    public async Task ExecuteReenrollmentAsync_ThrowsWhenSourceEqualsTarget()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var sameId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteReenrollmentAsync(sameId, sameId, new List<StudentPromotionAssignment>(), "user"));
    }

    // US0106: Bulk re-enrollment honors NG (Student.Program nullable propagation).

    [Fact]
    public async Task ExecuteReenrollmentAsync_PromoteToNGSection_NullsStudentProgram()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var years = context.AcademicYears.ToList();
        var ngSection = context.Sections.First(s => s.GradeLevel.Code == "NG" && s.Name == "LEVEL 1");

        // Pre-state: student previously in graded section with Program = "REGULAR"
        var student = TestDbContextFactory.CreateStudent(context, "Bridge", "Bound", "2025-07-2001", "7");
        student.Program = "REGULAR";
        context.SaveChanges();

        var service = CreateService(context);
        var assignments = new List<StudentPromotionAssignment>
        {
            new() { StudentId = student.Id, Action = PromotionAction.Promote, SectionId = ngSection.Id }
        };

        var result = await service.ExecuteReenrollmentAsync(years[0].Id, years[1].Id, assignments, "test-user");

        Assert.Equal(1, result.PromotedCount);
        Assert.Empty(result.Errors);

        var updated = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Null(updated.Program);
        Assert.Equal("NG", updated.GradeLevel);
        Assert.Equal("LEVEL 1", updated.Section);
    }

    [Fact]
    public async Task ExecuteReenrollmentAsync_PromoteToGradedSection_SetsStudentProgramCode()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAll(context);
        await DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance);

        var years = context.AcademicYears.ToList();
        var grade8Section = context.Sections.Include(s => s.Program).First(s => s.GradeLevel.Code == "8");

        // Pre-state: student in NG with Program = null
        var student = TestDbContextFactory.CreateStudent(context, "Grad", "Bound", "2025-07-2002", "NG");
        student.Program = null;
        context.SaveChanges();

        var service = CreateService(context);
        var assignments = new List<StudentPromotionAssignment>
        {
            new() { StudentId = student.Id, Action = PromotionAction.Promote, SectionId = grade8Section.Id }
        };

        var result = await service.ExecuteReenrollmentAsync(years[0].Id, years[1].Id, assignments, "test-user");

        Assert.Equal(1, result.PromotedCount);
        Assert.Empty(result.Errors);

        var updated = context.Students.AsNoTracking().Single(s => s.Id == student.Id);
        Assert.Equal("REGULAR", updated.Program);
        Assert.Equal("8", updated.GradeLevel);
    }
}
