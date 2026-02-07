namespace SmartLog.Web.Services;

/// <summary>
/// Service for handling file uploads, particularly profile pictures.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Upload a profile picture and return the relative path.
    /// </summary>
    /// <param name="file">The uploaded file</param>
    /// <param name="entityType">Type of entity (user, student, faculty)</param>
    /// <param name="entityId">ID of the entity</param>
    /// <returns>Relative path to the uploaded file</returns>
    Task<string> UploadProfilePictureAsync(IFormFile file, string entityType, string entityId);

    /// <summary>
    /// Delete a profile picture.
    /// </summary>
    /// <param name="filePath">Relative path to the file</param>
    Task DeleteProfilePictureAsync(string? filePath);

    /// <summary>
    /// Validate if the file is a valid image.
    /// </summary>
    /// <param name="file">The file to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValidImage(IFormFile file);

    /// <summary>
    /// Get the full URL for a profile picture path.
    /// </summary>
    /// <param name="relativePath">Relative path to the file</param>
    /// <returns>Full URL or default avatar URL</returns>
    string GetProfilePictureUrl(string? relativePath);
}
