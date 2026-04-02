using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for managing grade levels, sections, and student enrollments.
/// </summary>
public interface IGradeSectionService
{
    // Grade Level Operations
    Task<List<GradeLevel>> GetAllGradeLevelsAsync(bool activeOnly = true);
    Task<GradeLevel?> GetGradeLevelByIdAsync(Guid id);
    Task<GradeLevel?> GetGradeLevelByCodeAsync(string code);
    Task<GradeLevel> CreateGradeLevelAsync(string code, string name, int sortOrder);
    Task UpdateGradeLevelAsync(GradeLevel gradeLevel);
    Task DeactivateGradeLevelAsync(Guid id);

    // Section Operations
    Task<List<Section>> GetAllSectionsAsync(bool activeOnly = true);
    Task<List<Section>> GetSectionsByGradeAsync(Guid gradeLevelId, bool activeOnly = true);
    Task<Section?> GetSectionByIdAsync(Guid id);
    Task<Section> CreateSectionAsync(Guid gradeLevelId, string name, Guid? adviserId = null, int capacity = 40);
    Task UpdateSectionAsync(Section section);
    Task DeactivateSectionAsync(Guid id);

    // Student Enrollment Operations
    Task<StudentEnrollment> EnrollStudentAsync(Guid studentId, Guid sectionId, Guid academicYearId);
    Task<StudentEnrollment> TransferStudentAsync(Guid studentId, Guid newSectionId, Guid academicYearId);
    Task<List<StudentEnrollment>> GetStudentEnrollmentsAsync(Guid studentId);
    Task<StudentEnrollment?> GetCurrentEnrollmentAsync(Guid studentId, Guid academicYearId);
    Task<List<StudentEnrollment>> GetSectionEnrollmentsAsync(Guid sectionId, Guid academicYearId, bool activeOnly = true);
    Task WithdrawStudentAsync(Guid enrollmentId);
}
