using SmartLog.Web.Data.Entities;
using Entities = SmartLog.Web.Data.Entities;

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
    /// <summary>Permanently deletes a grade level. Throws if it has sections or students.</summary>
    Task DeleteGradeLevelAsync(Guid id);

    // Section Operations
    Task<List<Section>> GetAllSectionsAsync(bool activeOnly = true);
    Task<List<Section>> GetSectionsByGradeAsync(Guid gradeLevelId, bool activeOnly = true);
    Task<Section?> GetSectionByIdAsync(Guid id);
    Task<Section> CreateSectionAsync(Guid gradeLevelId, string name, Guid programId, Guid? adviserId = null, int capacity = 40);
    Task UpdateSectionAsync(Section section);
    Task DeactivateSectionAsync(Guid id);
    /// <summary>Permanently deletes a section. Throws if it has any enrollments.</summary>
    Task DeleteSectionAsync(Guid id);

    /// <summary>
    /// Returns leaf programs (no sub-programs) linked to the given grade level.
    /// Used to populate the Program dropdown on section create/edit pages.
    /// </summary>
    Task<List<Entities.Program>> GetProgramsForGradeAsync(Guid gradeLevelId);

    // Program Operations
    Task<List<Entities.Program>> GetAllProgramsAsync(bool activeOnly = false);
    Task<Entities.Program?> GetProgramByIdAsync(Guid id);
    Task<Entities.Program> CreateProgramAsync(string code, string name, string? description, int sortOrder, IEnumerable<Guid> gradeLevelIds);
    Task UpdateProgramAsync(Entities.Program program, IEnumerable<Guid> gradeLevelIds);
    /// <summary>Permanently deletes a program. Throws if it has sections assigned.</summary>
    Task DeleteProgramAsync(Guid id);

    // Student Enrollment Operations
    Task<StudentEnrollment> EnrollStudentAsync(Guid studentId, Guid sectionId, Guid academicYearId);
    Task<StudentEnrollment> TransferStudentAsync(Guid studentId, Guid newSectionId, Guid academicYearId);
    Task<List<StudentEnrollment>> GetStudentEnrollmentsAsync(Guid studentId);
    Task<StudentEnrollment?> GetCurrentEnrollmentAsync(Guid studentId, Guid academicYearId);
    Task<List<StudentEnrollment>> GetSectionEnrollmentsAsync(Guid sectionId, Guid academicYearId, bool activeOnly = true);
    Task WithdrawStudentAsync(Guid enrollmentId);
}
