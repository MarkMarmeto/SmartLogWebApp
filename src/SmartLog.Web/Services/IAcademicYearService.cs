using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for managing academic years.
/// </summary>
public interface IAcademicYearService
{
    /// <summary>
    /// Gets the current active academic year.
    /// </summary>
    Task<AcademicYear?> GetCurrentAcademicYearAsync();

    /// <summary>
    /// Gets all academic years ordered by start date descending.
    /// </summary>
    Task<List<AcademicYear>> GetAllAcademicYearsAsync(bool activeOnly = false);

    /// <summary>
    /// Gets a specific academic year by ID.
    /// </summary>
    Task<AcademicYear?> GetAcademicYearByIdAsync(Guid id);

    /// <summary>
    /// Creates a new academic year.
    /// </summary>
    Task<AcademicYear> CreateAcademicYearAsync(string name, DateTime startDate, DateTime endDate, bool setCurrent = false);

    /// <summary>
    /// Sets a specific academic year as the current one.
    /// Ensures only one academic year is current at a time.
    /// </summary>
    Task SetCurrentAcademicYearAsync(Guid academicYearId);

    /// <summary>
    /// Updates an existing academic year.
    /// </summary>
    Task UpdateAcademicYearAsync(AcademicYear academicYear);

    /// <summary>
    /// Deactivates an academic year (soft delete).
    /// </summary>
    Task DeactivateAcademicYearAsync(Guid academicYearId);
}
