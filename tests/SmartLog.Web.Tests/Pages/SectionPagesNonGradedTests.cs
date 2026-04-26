using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Pages.Admin;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Pages;

/// <summary>
/// Tests for US0104: Section Create/Edit — Hide Program Dropdown for Non-Graded.
/// Covers page-handler paths for both NG and graded grade levels.
/// </summary>
public class SectionPagesNonGradedTests
{
    private static (CreateSectionModel page, ApplicationDbContext db) CreateCreatePage()
    {
        var db = TestDbContextFactory.Create();
        var service = new GradeSectionService(db, NullLogger<GradeSectionService>.Instance);
        var audit = Mock.Of<IAuditService>();
        var page = new CreateSectionModel(service, db, audit, NullLogger<CreateSectionModel>.Instance);
        WirePageContext(page);
        return (page, db);
    }

    private static (EditSectionModel page, ApplicationDbContext db) CreateEditPage()
    {
        var db = TestDbContextFactory.Create();
        var service = new GradeSectionService(db, NullLogger<GradeSectionService>.Instance);
        var audit = Mock.Of<IAuditService>();
        var page = new EditSectionModel(service, db, audit, NullLogger<EditSectionModel>.Instance);
        WirePageContext(page);
        return (page, db);
    }

    private static void WirePageContext(PageModel page)
    {
        var httpContext = new DefaultHttpContext();
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider
            .Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
            .Returns(new Dictionary<string, object?>());

        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState);
        var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);
        var pageContext = new PageContext(actionContext) { ViewData = viewData };

        page.PageContext = pageContext;
        page.TempData = tempData;
    }

    [Fact]
    public async Task CreateSectionPage_PostNullProgramForGraded_ReturnsPageWithModelError()
    {
        var (page, db) = CreateCreatePage();
        TestDbContextFactory.SeedGradeLevels(db);
        TestDbContextFactory.SeedPrograms(db);
        var grade7 = await db.GradeLevels.SingleAsync(g => g.Code == "7");

        page.Input = new CreateSectionModel.InputModel
        {
            GradeLevelId = grade7.Id,
            ProgramId = null,
            Name = "7-A",
            Capacity = 30
        };

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.True(page.ModelState.ContainsKey(nameof(page.Input.ProgramId)));
        var error = page.ModelState[nameof(page.Input.ProgramId)]!.Errors[0].ErrorMessage;
        Assert.Contains("required for graded", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSectionPage_PostNullProgramForNG_RedirectsToSections()
    {
        var (page, db) = CreateCreatePage();
        await DbInitializer.SeedNonGradedAsync(db, NullLogger.Instance);
        var ng = await db.GradeLevels.SingleAsync(g => g.Code == "NG");

        page.Input = new CreateSectionModel.InputModel
        {
            GradeLevelId = ng.Id,
            ProgramId = null,
            Name = "LEVEL 5",
            Capacity = 30
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Sections", redirect.PageName);
        var saved = await db.Sections.SingleAsync(s => s.Name == "LEVEL 5");
        Assert.Null(saved.ProgramId);
        Assert.Equal(ng.Id, saved.GradeLevelId);
    }

    [Fact]
    public async Task EditSectionPage_OnGetForNGSection_ExposesGradeLevelCodeNG()
    {
        var (page, db) = CreateEditPage();
        await DbInitializer.SeedNonGradedAsync(db, NullLogger.Instance);
        var section = await db.Sections.Include(s => s.GradeLevel)
            .FirstAsync(s => s.GradeLevel.Code == "NG");

        var result = await page.OnGetAsync(section.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("NG", page.GradeLevelCode);
        Assert.True(page.IsNonGraded);
    }

    [Fact]
    public async Task EditSectionPage_PostProgramOnNGSection_ReturnsModelError()
    {
        var (page, db) = CreateEditPage();
        TestDbContextFactory.SeedPrograms(db);
        await DbInitializer.SeedNonGradedAsync(db, NullLogger.Instance);
        var section = await db.Sections.Include(s => s.GradeLevel)
            .FirstAsync(s => s.GradeLevel.Code == "NG");
        var regular = await db.Programs.SingleAsync(p => p.Code == "REGULAR");

        page.Input = new EditSectionModel.InputModel
        {
            Id = section.Id,
            ProgramId = regular.Id, // illegal for NG
            Name = section.Name,
            Capacity = section.Capacity,
            IsActive = section.IsActive
        };

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.True(page.ModelState.ContainsKey(nameof(page.Input.ProgramId)));
        var error = page.ModelState[nameof(page.Input.ProgramId)]!.Errors[0].ErrorMessage;
        Assert.Contains("must not have a Program", error, StringComparison.OrdinalIgnoreCase);
    }
}
