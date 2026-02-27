using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class TemplatesModel : PageModel
{
    private readonly ISmsTemplateService _templateService;
    private readonly ILogger<TemplatesModel> _logger;

    public TemplatesModel(
        ISmsTemplateService templateService,
        ILogger<TemplatesModel> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public List<SmsTemplate> Templates { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalTemplates { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var allTemplates = await _templateService.GetAllTemplatesAsync();

        TotalTemplates = allTemplates.Count;
        TotalPages = (int)Math.Ceiling(TotalTemplates / (double)PageSize);

        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        Templates = allTemplates
            .OrderBy(t => t.Code)
            .ThenBy(t => t.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync(
        int templateId,
        string name,
        string templateEn,
        string templateFil,
        bool isActive)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                ErrorMessage = "Template not found.";
                return RedirectToPage();
            }

            template.Name = name;
            template.TemplateEn = templateEn;
            template.TemplateFil = templateFil;
            template.IsActive = isActive;

            await _templateService.UpdateTemplateAsync(template);

            StatusMessage = "Template updated successfully.";
            _logger.LogInformation("SMS template {Code} updated", template.Code);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to update template.";
            _logger.LogError(ex, "Error updating SMS template");
        }

        return RedirectToPage();
    }
}
