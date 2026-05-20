using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Pages.Admin;
using SmartLog.Web.Services;

namespace SmartLog.Web.Tests.Pages;

/// <summary>
/// Tests for US0122: single-pass visitor pass print page.
/// </summary>
public class PrintVisitorPassTests
{
    [Fact]
    public async Task OnGetAsync_ValidId_ReturnsPageWithPass()
    {
        var passId = Guid.NewGuid();
        var pass = new VisitorPass
        {
            Id = passId,
            Code = "VISITOR-001",
            PassNumber = 1,
            QrPayload = "SMARTLOG-V:VISITOR-001:1739512547:sig",
            HmacSignature = "sig",
            QrImageBase64 = "iVBORw0KGgo=",
            IsActive = true
        };

        var service = new Mock<IVisitorPassService>();
        service.Setup(s => s.GetByIdAsync(passId)).ReturnsAsync(pass);

        var model = new PrintVisitorPassModel(service.Object);

        var result = await model.OnGetAsync(passId);

        Assert.IsType<PageResult>(result);
        Assert.Equal("VISITOR-001", model.Pass.Code);
        Assert.Equal(passId, model.Pass.Id);
    }

    [Fact]
    public async Task OnGetAsync_UnknownId_ReturnsNotFound()
    {
        var service = new Mock<IVisitorPassService>();
        service.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((VisitorPass?)null);

        var model = new PrintVisitorPassModel(service.Object);

        var result = await model.OnGetAsync(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }
}
