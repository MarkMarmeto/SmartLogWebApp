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

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Templates = await _templateService.GetAllTemplatesAsync();
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
