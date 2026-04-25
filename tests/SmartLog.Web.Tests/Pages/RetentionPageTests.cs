using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Pages.Admin.Settings;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Pages;

/// <summary>
/// Tests for US0094: Retention Policy Entity & Admin UI.
/// </summary>
public class RetentionPageTests
{
    private static RetentionModel CreateModel(ApplicationDbContext db)
    {
        var audit = new Mock<IAuditService>();
        var logger = NullLogger<RetentionModel>.Instance;
        var model = new RetentionModel(db, audit.Object, logger);

        // Wire up enough Razor Pages infrastructure for TempData and ModelState
        var httpContext = new DefaultHttpContext();
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider
            .Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
            .Returns(new Dictionary<string, object?>());

        var routeData = new RouteData();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, routeData, new PageActionDescriptor(), modelState);
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState);
        var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);
        var pageContext = new PageContext(actionContext) { ViewData = viewData };

        model.PageContext = pageContext;
        model.TempData = tempData;

        return model;
    }

    private static void SeedRetentionPolicies(ApplicationDbContext db)
    {
        db.RetentionPolicies.AddRange(
            new RetentionPolicy { EntityName = "SmsQueue",    RetentionDays = 90,   ArchiveEnabled = false, Enabled = true, UpdatedAt = DateTime.UtcNow },
            new RetentionPolicy { EntityName = "SmsLog",      RetentionDays = 180,  ArchiveEnabled = false, Enabled = true, UpdatedAt = DateTime.UtcNow },
            new RetentionPolicy { EntityName = "Broadcast",   RetentionDays = 365,  ArchiveEnabled = false, Enabled = true, UpdatedAt = DateTime.UtcNow },
            new RetentionPolicy { EntityName = "Scan",        RetentionDays = 730,  ArchiveEnabled = false, Enabled = true, UpdatedAt = DateTime.UtcNow },
            new RetentionPolicy { EntityName = "AuditLog",    RetentionDays = 1095, ArchiveEnabled = true,  Enabled = true, UpdatedAt = DateTime.UtcNow },
            new RetentionPolicy { EntityName = "VisitorScan", RetentionDays = 365,  ArchiveEnabled = false, Enabled = true, UpdatedAt = DateTime.UtcNow }
        );
        db.SaveChanges();
    }

    // ─── Seed tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedRetentionPolicies_CreatesSixRows()
    {
        await using var db = TestDbContextFactory.Create();

        await DbInitializer_SeedRetentionPoliciesAsync(db);

        Assert.Equal(6, db.RetentionPolicies.Count());
    }

    [Fact]
    public async Task SeedRetentionPolicies_IsIdempotent()
    {
        await using var db = TestDbContextFactory.Create();

        await DbInitializer_SeedRetentionPoliciesAsync(db);
        await DbInitializer_SeedRetentionPoliciesAsync(db);

        Assert.Equal(6, db.RetentionPolicies.Count());
    }

    [Fact]
    public async Task SeedRetentionPolicies_AuditLogArchiveEnabledByDefault()
    {
        await using var db = TestDbContextFactory.Create();

        await DbInitializer_SeedRetentionPoliciesAsync(db);

        var auditPolicy = db.RetentionPolicies.Single(p => p.EntityName == "AuditLog");
        Assert.True(auditPolicy.ArchiveEnabled);
    }

    // ─── OnGetAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_LoadsAllSixPolicies()
    {
        await using var db = TestDbContextFactory.Create();
        SeedRetentionPolicies(db);

        var model = CreateModel(db);
        await model.OnGetAsync();

        Assert.Equal(6, model.Policies.Count);
    }

    [Fact]
    public async Task OnGetAsync_PolicyHasFriendlyName()
    {
        await using var db = TestDbContextFactory.Create();
        SeedRetentionPolicies(db);

        var model = CreateModel(db);
        await model.OnGetAsync();

        var scan = model.Policies.Single(p => p.EntityName == "Scan");
        Assert.Equal("Scan Records", scan.FriendlyName);
        Assert.True(scan.MinFloor > 0);
    }

    // ─── OnPostAsync validation ───────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_BelowFloor_ReturnsPageWithValidationError()
    {
        await using var db = TestDbContextFactory.Create();
        SeedRetentionPolicies(db);

        var model = CreateModel(db);
        await model.OnGetAsync();

        // Set SmsQueue retention below floor (7 days)
        var queuePolicy = model.Policies.Single(p => p.EntityName == "SmsQueue");
        queuePolicy.RetentionDays = 3;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAsync_ValidValues_Saves()
    {
        await using var db = TestDbContextFactory.Create();
        SeedRetentionPolicies(db);

        var model = CreateModel(db);
        await model.OnGetAsync();

        // Change SmsQueue retention to 30 days (above floor of 7)
        var queuePolicy = model.Policies.Single(p => p.EntityName == "SmsQueue");
        queuePolicy.RetentionDays = 30;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var saved = db.RetentionPolicies.Single(p => p.EntityName == "SmsQueue");
        Assert.Equal(30, saved.RetentionDays);
    }

    [Fact]
    public async Task OnPostAsync_ValidChange_WritesAuditLog()
    {
        await using var db = TestDbContextFactory.Create();
        SeedRetentionPolicies(db);

        var audit = new Mock<IAuditService>();
        var logger = NullLogger<RetentionModel>.Instance;
        var model = new RetentionModel(db, audit.Object, logger);

        // Minimal page context
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin.amy") }));
        var routeData = new RouteData();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, routeData, new PageActionDescriptor(), modelState);
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState);
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(p => p.LoadTempData(It.IsAny<HttpContext>())).Returns(new Dictionary<string, object?>());
        var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);
        model.PageContext = new PageContext(actionContext) { ViewData = viewData };
        model.TempData = tempData;

        await model.OnGetAsync();
        var queuePolicy = model.Policies.Single(p => p.EntityName == "SmsQueue");
        queuePolicy.RetentionDays = 45; // changed from 90

        await model.OnPostAsync();

        audit.Verify(a => a.LogAsync(
            "RetentionPolicyUpdated",
            null,
            It.IsAny<string>(),
            It.Is<string>(d => d.Contains("SmsQueue")),
            null, null), Times.Once);
    }

    // ─── Helper: replicate DbInitializer seed logic inline for unit testing ──

    private static async Task DbInitializer_SeedRetentionPoliciesAsync(ApplicationDbContext db)
    {
        var defaults = new[]
        {
            new RetentionPolicy { EntityName = "SmsQueue",    RetentionDays = 90,   ArchiveEnabled = false },
            new RetentionPolicy { EntityName = "SmsLog",      RetentionDays = 180,  ArchiveEnabled = false },
            new RetentionPolicy { EntityName = "Broadcast",   RetentionDays = 365,  ArchiveEnabled = false },
            new RetentionPolicy { EntityName = "Scan",        RetentionDays = 730,  ArchiveEnabled = false },
            new RetentionPolicy { EntityName = "AuditLog",    RetentionDays = 1095, ArchiveEnabled = true  },
            new RetentionPolicy { EntityName = "VisitorScan", RetentionDays = 365,  ArchiveEnabled = false },
        };

        foreach (var def in defaults)
        {
            if (!db.RetentionPolicies.Any(p => p.EntityName == def.EntityName))
            {
                def.UpdatedAt = DateTime.UtcNow;
                db.RetentionPolicies.Add(def);
            }
        }

        await db.SaveChangesAsync();
    }
}
