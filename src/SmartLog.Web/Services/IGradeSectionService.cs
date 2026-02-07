using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for managing grade levels, sections, and student enrollments.
/// </summary>
public interface IGradeSectionService
{
    // Grade Level Operations
    Task<List<GradeLevel>> GetAllGradeLevelsAsync(bool activeOnly = true);
    Task<GradeLevel?> GetGradeLevelByIdAsync(int id);
    Task<GradeLevel?> GetGradeLevelByCodeAsync(string code);
    Task<GradeLevel> CreateGradeLevelAsync(string code, string name, int sortOrder);
    Task UpdateGradeLevelAsync(GradeLevel gradeLevel);
    Task DeactivateGradeLevelAsync(int id);

    // Section Operations
    Task<List<Section>> GetAllSectionsAsync(bool activeOnly = true);
    Task<List<Section>> GetSectionsByGradeAsync(int gradeLevelId, bool activeOnly = true);
    Task<Section?> GetSectionByIdAsync(int id);
    Task<Section> CreateSectionAsync(int gradeLevelId, string name, int? adviserId = null, int capacity = 40);
    Task UpdateSectionAsync(Section section);
    Task DeactivateSectionAsync(int id);

    // Student Enrollment Operations
    Task<StudentEnrollment> EnrollStudentAsync(int studentId, int sectionId, int academicYearId);
    Task<StudentEnrollment> TransferStudentAsync(int studentId, int newSectionId, int academicYearId);
    Task<List<StudentEnrollment>> GetStudentEnrollmentsAsync(int studentId);
    Task<StudentEnrollment?> GetCurrentEnrollmentAsync(int studentId, int academicYearId);
    Task<List<StudentEnrollment>> GetSectionEnrollmentsAsync(int sectionId, int academicYearId, bool activeOnly = true);
    Task WithdrawStudentAsync(int enrollmentId);
}
