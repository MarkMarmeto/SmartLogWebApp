namespace SmartLog.Web.Services.Branding;

public interface IBrandingService
{
    Task<string> UploadLogoAsync(IFormFile file, string? updatedBy);
    Task RemoveLogoAsync(string? updatedBy);
    Task<(bool IsValid, string? ErrorMessage)> ValidateLogoAsync(IFormFile file);
}
