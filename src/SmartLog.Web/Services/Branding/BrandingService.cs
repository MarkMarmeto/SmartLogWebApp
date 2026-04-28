namespace SmartLog.Web.Services.Branding;

public class BrandingService : IBrandingService
{
    private readonly IWebHostEnvironment _env;
    private readonly IAppSettingsService _appSettings;
    private readonly ILogger<BrandingService> _logger;

    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".svg"];
    private static readonly string[] AllowedMimeTypes = ["image/png", "image/jpeg", "image/svg+xml"];
    private const long MaxFileSize = 2 * 1024 * 1024;
    private static readonly string[] SvgUnsafePatterns = ["<script", "onload=", "onerror=", "<foreignobject"];

    public BrandingService(
        IWebHostEnvironment env,
        IAppSettingsService appSettings,
        ILogger<BrandingService> logger)
    {
        _env = env;
        _appSettings = appSettings;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateLogoAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, "Please select a file to upload.");

        if (file.Length > MaxFileSize)
            return (false, "Logo must be ≤ 2 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (false, "Allowed types: PNG, JPG, SVG.");

        var mime = file.ContentType.ToLowerInvariant();
        if (!AllowedMimeTypes.Contains(mime))
            return (false, "Allowed types: PNG, JPG, SVG.");

        if (ext == ".svg")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = (await reader.ReadToEndAsync()).ToLowerInvariant();
            foreach (var pattern in SvgUnsafePatterns)
            {
                if (content.Contains(pattern))
                    return (false, "The SVG file contains unsafe content and cannot be uploaded.");
            }
            if (!content.Contains("<svg"))
                return (false, "The file does not appear to be a valid SVG.");
        }
        else
        {
            using var stream = file.OpenReadStream();
            var header = new byte[4];
            _ = await stream.ReadAsync(header);

            var isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            var isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            if (!isPng && !isJpg)
                return (false, "File content does not match the declared image type.");
        }

        return (true, null);
    }

    public async Task<string> UploadLogoAsync(IFormFile file, string? updatedBy)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var brandingDir = Path.Combine(_env.WebRootPath, "branding");
        Directory.CreateDirectory(brandingDir);

        // Remove any prior logo (different extension) before writing new one
        var currentPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
        if (!string.IsNullOrEmpty(currentPath))
        {
            var oldFull = Path.Combine(_env.WebRootPath, currentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(oldFull))
                File.Delete(oldFull);
        }

        var fileName = $"school-logo{ext}";
        var destPath = Path.Combine(brandingDir, fileName);

        using (var stream = new FileStream(destPath, FileMode.Create))
            await file.CopyToAsync(stream);

        var relativePath = $"/branding/{fileName}";
        await _appSettings.SetAsync("Branding:SchoolLogoPath", relativePath, "Branding", updatedBy);

        _logger.LogInformation("School logo uploaded: {Path} by {User}", relativePath, updatedBy);
        return relativePath;
    }

    public async Task RemoveLogoAsync(string? updatedBy)
    {
        var currentPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
        if (!string.IsNullOrEmpty(currentPath))
        {
            var fullPath = Path.Combine(_env.WebRootPath, currentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        await _appSettings.SetAsync("Branding:SchoolLogoPath", null, "Branding", updatedBy);
        _logger.LogInformation("School logo removed by {User}", updatedBy);
    }

    private static bool IsJpeg(byte[] header) =>
        header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
}
