namespace SmartLog.Web.Services;

/// <summary>
/// Service for generating unique IDs for students and employees.
/// </summary>
public interface IIdGenerationService
{
    /// <summary>
    /// Generates a unique Student ID in format CODE-YYYY-NNNNN.
    /// Example: MNHS-2026-00001.
    /// School code is read from System.SchoolCode setting.
    /// </summary>
    Task<string> GenerateStudentIdAsync();

    /// <summary>
    /// Generates a unique Employee ID in format EMP-YYYY-NNNN.
    /// Example: EMP-2026-0001 for first employee in 2026.
    /// </summary>
    Task<string> GenerateEmployeeIdAsync();
}
