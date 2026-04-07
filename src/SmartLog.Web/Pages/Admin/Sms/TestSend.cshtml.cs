using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;
using SmartLog.Web.Validation;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class TestSendModel : PageModel
{
    private readonly ISmsTemplateService _templateService;
    private readonly ISmsService _smsService;
    private readonly ILogger<TestSendModel> _logger;

    public TestSendModel(
        ISmsTemplateService templateService,
        ISmsService smsService,
        ILogger<TestSendModel> logger)
    {
        _templateService = templateService;
        _smsService = smsService;
        _logger = logger;
    }

    public List<SmsTemplate> Templates { get; set; } = new();

    [BindProperty]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public Guid? SelectedTemplateId { get; set; }

    [BindProperty]
    public Dictionary<string, string> Placeholders { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Templates = await _templateService.GetAllTemplatesAsync();
    }

    public async Task<IActionResult> OnGetPreviewAsync(Guid templateId, [FromQuery] Dictionary<string, string>? placeholders)
    {
        var template = await _templateService.GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            return new JsonResult(new { error = "Template not found" });
        }

        var availablePlaceholders = template.AvailablePlaceholders?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? new List<string>();

        // Render previews with provided placeholders or placeholder names
        var previewPlaceholders = new Dictionary<string, string>();
        foreach (var ph in availablePlaceholders)
        {
            var key = ph.Trim('{', '}');
            previewPlaceholders[key] = placeholders?.GetValueOrDefault(key) ?? ph;
        }

        var previewEn = template.TemplateEn;
        var previewFil = template.TemplateFil;

        foreach (var kvp in previewPlaceholders)
        {
            previewEn = previewEn.Replace($"{{{kvp.Key}}}", kvp.Value);
            previewFil = previewFil.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        return new JsonResult(new
        {
            availablePlaceholders = availablePlaceholders.Select(p => p.Trim('{', '}')).ToList(),
            previewEn,
            previewFil,
            templateEn = template.TemplateEn,
            templateFil = template.TemplateFil
        });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            ErrorMessage = "Phone number is required.";
            return RedirectToPage();
        }

        if (!PhMobileAttribute.IsValidPhMobile(PhoneNumber))
        {
            ErrorMessage = "Please enter a valid Philippine mobile number (e.g. 09171234567).";
            return RedirectToPage();
        }

        if (!SelectedTemplateId.HasValue)
        {
            ErrorMessage = "Please select a template.";
            return RedirectToPage();
        }

        try
        {
            var template = await _templateService.GetTemplateByIdAsync(SelectedTemplateId.Value);
            if (template == null)
            {
                ErrorMessage = "Template not found.";
                return RedirectToPage();
            }

            // Render with EN language for test
            var message = await _templateService.RenderTemplateAsync(
                template.Code,
                "EN",
                Placeholders);

            if (string.IsNullOrWhiteSpace(message))
            {
                ErrorMessage = "Failed to render template.";
                return RedirectToPage();
            }

            await _smsService.QueueCustomSmsAsync(
                PhoneNumber,
                message,
                SmsPriority.Normal,
                "TEST");

            StatusMessage = $"Test SMS queued to {PhoneNumber}.";
            _logger.LogInformation("Test SMS sent to {Phone} using template {Template}", PhoneNumber, template.Code);

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to send test SMS.";
            _logger.LogError(ex, "Error sending test SMS");
            return RedirectToPage();
        }
    }
}
