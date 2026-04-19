using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static void SeedGradeLevels(ApplicationDbContext context)
    {
        if (context.GradeLevels.Any()) return;

        var grades = new List<GradeLevel>();
        for (int i = 7; i <= 12; i++)
        {
            grades.Add(new GradeLevel
            {
                Code = i.ToString(),
                Name = $"Grade {i}",
                SortOrder = i,
                IsActive = true
            });
        }

        context.GradeLevels.AddRange(grades);
        context.SaveChanges();
    }

    public static void SeedPrograms(ApplicationDbContext context)
    {
        if (context.Programs.Any()) return;

        context.Programs.Add(new Data.Entities.Program
        {
            Code = "REGULAR",
            Name = "Regular Program",
            IsActive = true,
            SortOrder = 0
        });
        context.SaveChanges();
    }

    public static void SeedSections(ApplicationDbContext context)
    {
        SeedGradeLevels(context);
        SeedPrograms(context);

        if (context.Sections.Any()) return;

        var gradeLevels = context.GradeLevels.ToList();
        var regularProgram = context.Programs.First(p => p.Code == "REGULAR");

        foreach (var gl in gradeLevels)
        {
            context.Sections.Add(new Section
            {
                Name = "Section A",
                GradeLevelId = gl.Id,
                ProgramId = regularProgram.Id,
                Capacity = 40,
                IsActive = true
            });
            context.Sections.Add(new Section
            {
                Name = "Section B",
                GradeLevelId = gl.Id,
                ProgramId = regularProgram.Id,
                Capacity = 40,
                IsActive = true
            });
        }

        context.SaveChanges();
    }

    public static void SeedAcademicYears(ApplicationDbContext context)
    {
        if (context.AcademicYears.Any()) return;

        context.AcademicYears.AddRange(
            new AcademicYear
            {
                Name = "2025-2026",
                StartDate = new DateTime(2025, 6, 1),
                EndDate = new DateTime(2026, 3, 31),
                IsCurrent = true,
                IsActive = true
            },
            new AcademicYear
            {
                Name = "2026-2027",
                StartDate = new DateTime(2026, 6, 1),
                EndDate = new DateTime(2027, 3, 31),
                IsCurrent = false,
                IsActive = true
            }
        );

        context.SaveChanges();
    }

    public static Student CreateStudent(ApplicationDbContext context, string firstName, string lastName, string studentId, string gradeLevel = "7", string section = "Section A", bool isActive = true)
    {
        var student = new Student
        {
            StudentId = studentId,
            FirstName = firstName,
            LastName = lastName,
            GradeLevel = gradeLevel,
            Section = section,
            ParentGuardianName = "Parent Test",
            GuardianRelationship = "Mother",
            ParentPhone = "09171234567",
            IsActive = isActive
        };

        context.Students.Add(student);
        context.SaveChanges();
        return student;
    }

    public static void SeedAll(ApplicationDbContext context)
    {
        SeedGradeLevels(context);
        SeedSections(context);
        SeedAcademicYears(context);
    }
}
