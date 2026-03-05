using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class IdGenerationServiceTests
{
    private readonly Mock<ILogger<IdGenerationService>> _logger = new();

    [Fact]
    public async Task GenerateStudentIdAsync_ReturnsCorrectFormat()
    {
        using var context = TestDbContextFactory.Create();
        var service = new IdGenerationService(context, _logger.Object);

        var id = await service.GenerateStudentIdAsync("7");

        // Format: YYYY-GG-NNNN
        var parts = id.Split('-');
        Assert.Equal(3, parts.Length);
        Assert.Equal(DateTime.UtcNow.Year.ToString(), parts[0]);
        Assert.Equal("07", parts[1]);
        Assert.Equal("0001", parts[2]);
    }

    [Fact]
    public async Task GenerateStudentIdAsync_SequentialNumbering()
    {
        using var context = TestDbContextFactory.Create();
        var year = DateTime.UtcNow.Year;

        // Pre-seed an existing student with an ID in the same grade/year
        context.Students.Add(new Student
        {
            StudentId = $"{year}-07-0003",
            FirstName = "Existing",
            LastName = "Student",
            GradeLevel = "7",
            Section = "A",
            ParentGuardianName = "Parent",
            GuardianRelationship = "Mother",
            ParentPhone = "09170000000"
        });
        context.SaveChanges();

        var service = new IdGenerationService(context, _logger.Object);
        var id = await service.GenerateStudentIdAsync("7");

        Assert.Equal($"{year}-07-0004", id);
    }

    [Fact]
    public async Task GenerateStudentIdAsync_ThrowsForInvalidGradeCode()
    {
        using var context = TestDbContextFactory.Create();
        var service = new IdGenerationService(context, _logger.Object);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateStudentIdAsync("K"));
    }

    [Fact]
    public async Task GenerateStudentIdAsync_PadsGradeCodeToTwoDigits()
    {
        using var context = TestDbContextFactory.Create();
        var service = new IdGenerationService(context, _logger.Object);

        var id = await service.GenerateStudentIdAsync("12");

        var parts = id.Split('-');
        Assert.Equal("12", parts[1]);
    }

    [Fact]
    public async Task GenerateEmployeeIdAsync_ReturnsCorrectFormat()
    {
        using var context = TestDbContextFactory.Create();
        var service = new IdGenerationService(context, _logger.Object);

        var id = await service.GenerateEmployeeIdAsync();

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"EMP-{year}-0001", id);
    }
}
