namespace SmartLog.Web.Services;

/// <summary>
/// Service for handling file uploads, particularly profile pictures.
/// </summary>
public class FileUploadService : IFileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadService> _logger;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/gif" };

    public FileUploadService(
        IWebHostEnvironment environment,
        ILogger<FileUploadService> logger)
    {
        _environment = environment;
        _logger = logger;
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
            var fullPath = Path.Combine(_environment.WebRootPath, cleanPath.Replace("/", Path.DirectorySeparatorChar.ToString()));

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

        // Check file size
        if (file.Length > MaxFileSize)
        {
            _logger.LogWarning("File too large: {Size} bytes", file.Length);
            return false;
        }

        // Check extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
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

        return true;
    }

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
