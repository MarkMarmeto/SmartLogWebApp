namespace SmartLog.Web.Services;

/// <summary>
/// Service for handling file uploads, particularly profile pictures.
/// </summary>
public class FileUploadService : IFileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<FileUploadService> _logger;
    private static readonly string[] DefaultAllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/gif" };

    public FileUploadService(
        IWebHostEnvironment environment,
        IAppSettingsService appSettingsService,
        ILogger<FileUploadService> logger)
    {
        _environment = environment;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    private async Task<long> GetMaxFileSizeAsync()
    {
        var sizeMb = await _appSettingsService.GetAsync("FileUpload.MaxFileSizeMB", 5);
        return sizeMb * 1024L * 1024L;
    }

    private async Task<string[]> GetAllowedExtensionsAsync()
    {
        var extensions = await _appSettingsService.GetAsync("FileUpload.AllowedExtensions");
        if (string.IsNullOrEmpty(extensions))
            return DefaultAllowedExtensions;
        return extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<string> UploadProfilePictureAsync(IFormFile file, string entityType, string entityId)
    {
        if (!IsValidImage(file))
        {
            throw new InvalidOperationException("Invalid image file");
        }

        // Create upload directory if it doesn't exist
        var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "profile-pictures", entityType);
        Directory.CreateDirectory(uploadDir);

        // Generate unique filename
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{entityId}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDir, fileName);

        try
        {
            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path
            var relativePath = Path.Combine("uploads", "profile-pictures", entityType, fileName)
                .Replace("\\", "/");

            _logger.LogInformation("Profile picture uploaded: {FilePath} for {EntityType} {EntityId}",
                relativePath, entityType, entityId);

            return "/" + relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile picture for {EntityType} {EntityId}",
                entityType, entityId);
            throw;
        }
    }

    public Task DeleteProfilePictureAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            // Remove leading slash if present
            var cleanPath = filePath.TrimStart('/');
            var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, cleanPath.Replace("/", Path.DirectorySeparatorChar.ToString())));

            // Path traversal protection
            if (!fullPath.StartsWith(_environment.WebRootPath))
            {
                _logger.LogWarning("Path traversal attempt detected: {Path}", filePath);
                return Task.CompletedTask;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted profile picture: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile picture: {FilePath}", filePath);
            // Don't throw - deletion failures shouldn't break the application
        }

        return Task.CompletedTask;
    }

    public bool IsValidImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return false;
        }

        // Check file size (use sync-over-async since interface is sync; cached after first call)
        var maxFileSize = GetMaxFileSizeAsync().GetAwaiter().GetResult();
        if (file.Length > maxFileSize)
        {
            _logger.LogWarning("File too large: {Size} bytes (max: {Max})", file.Length, maxFileSize);
            return false;
        }

        // Check extension
        var allowedExtensions = GetAllowedExtensionsAsync().GetAwaiter().GetResult();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            _logger.LogWarning("Invalid file extension: {Extension}", extension);
            return false;
        }

        // Check MIME type
        if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            _logger.LogWarning("Invalid MIME type: {MimeType}", file.ContentType);
            return false;
        }

        // Magic byte validation
        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[8];
            _ = stream.Read(header, 0, 8);
            stream.Position = 0;

            if (!IsJpeg(header) && !IsPng(header) && !IsGif(header))
            {
                _logger.LogWarning("Invalid magic bytes for file: {FileName}", file.FileName);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading file header for validation");
            return false;
        }

        return true;
    }

    private static bool IsJpeg(byte[] header) => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    private static bool IsPng(byte[] header) => header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
    private static bool IsGif(byte[] header) => header.Length >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38;

    public string GetProfilePictureUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            // Return default avatar (we'll use a data URI for a simple SVG)
            return "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='100' height='100'%3E%3Crect width='100' height='100' fill='%230d6efd'/%3E%3Ctext x='50' y='50' font-size='40' fill='white' text-anchor='middle' dy='.3em'%3E?%3C/text%3E%3C/svg%3E";
        }

        return relativePath;
    }
}
