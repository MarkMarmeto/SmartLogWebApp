namespace SmartLog.Web.Services;

/// <summary>
/// Service for generating unique IDs for students and employees.
/// </summary>
public interface IIdGenerationService
{
    /// <summary>
    /// Generates a unique Student ID in format YYYY-GG-NNNN.
    /// Example: 2026-07-0001 for first Grade 7 student in 2026.
    /// </summary>
    /// <param name="gradeCode">Grade code (e.g., 7-12)</param>
    Task<string> GenerateStudentIdAsync(string gradeCode);

    /// <summary>
    /// Generates a unique Employee ID in format EMP-YYYY-NNNN.
    /// Example: EMP-2026-0001 for first employee in 2026.
    /// </summary>
    Task<string> GenerateEmployeeIdAsync();
}
