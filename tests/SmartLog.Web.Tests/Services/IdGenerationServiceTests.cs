using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class IdGenerationServiceTests
{
    private readonly Mock<ILogger<IdGenerationService>> _logger = new();
    private readonly Mock<IAppSettingsService> _appSettings = new();

    public IdGenerationServiceTests()
    {
        _appSettings.Setup(s => s.GetAsync("System.SchoolCode"))
            .ReturnsAsync("MNHS");
    }

    [Fact]
    public async Task GenerateStudentIdAsync_ReturnsCorrectFormat()
    {
        using var context = TestDbContextFactory.Create();
        var service = new IdGenerationService(context, _appSettings.Object, _logger.Object);

        var id = await service.GenerateStudentIdAsync();

        // Format: CODE-YYYY-NNNNN
        var parts = id.Split('-');
        Assert.Equal(3, parts.Length);
        Assert.Equal("MNHS", parts[0]);
        Assert.Equal(DateTime.UtcNow.Year.ToString(), parts[1]);
        Assert.Equal("00001", parts[2]);
    }

    [Fact]
    public async Task GenerateStudentIdAsync_SequentialNumbering()
    {
        using var context = TestDbContextFactory.Create();
        var year = DateTime.UtcNow.Year;

        // Pre-seed an existing student with an ID in the same code/year
        context.Students.Add(new Student
        {
            StudentId = $"MNHS-{year}-00003",
            FirstName = "Existing",
            LastName = "Student",
            GradeLevel = "7",
            Section = "A",
            ParentGuardianName = "Parent",
            GuardianRelationship = "Mother",
            ParentPhone = "09170000000"
        });
        context.SaveChanges();

        var service = new IdGenerationService(context, _appSettings.Object, _logger.Object);
        var id = await service.GenerateStudentIdAsync();

        Assert.Equal($"MNHS-{year}-00004", id);
    }

    [Fact]
    public async Task GenerateStudentIdAsync_UsesDefaultCodeWhenSettingMissing()
    {
        using var context = TestDbContextFactory.Create();
        var emptySettings = new Mock<IAppSettingsService>();
        emptySettings.Setup(s => s.GetAsync("System.SchoolCode"))
            .ReturnsAsync((string?)null);

        var service = new IdGenerationService(context, emptySettings.Object, _logger.Object);
        var id = await service.GenerateStudentIdAsync();

        Assert.StartsWith("SL-", id);
    }

    [Fact]
    public async Task GenerateEmployeeIdAsync_ReturnsCorrectFormat()
    {
        using var context = TestDbContextFactory.Create();
        var service = new IdGenerationService(context, _appSettings.Object, _logger.Object);

        var id = await service.GenerateEmployeeIdAsync();

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"EMP-{year}-0001", id);
    }
}
