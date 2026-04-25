using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using Entities = SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of grade level, section, and enrollment management service.
/// </summary>
public class GradeSectionService : IGradeSectionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GradeSectionService> _logger;

    public GradeSectionService(ApplicationDbContext context, ILogger<GradeSectionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Grade Level Operations

    public async Task<List<GradeLevel>> GetAllGradeLevelsAsync(bool activeOnly = true)
    {
        var query = _context.GradeLevels
            .Include(gl => gl.Sections)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(gl => gl.IsActive);
        }

        return await query
            .OrderBy(gl => gl.SortOrder)
            .ToListAsync();
    }

    public async Task<GradeLevel?> GetGradeLevelByIdAsync(Guid id)
    {
        return await _context.GradeLevels
            .Include(gl => gl.Sections)
            .FirstOrDefaultAsync(gl => gl.Id == id);
    }

    public async Task<GradeLevel?> GetGradeLevelByCodeAsync(string code)
    {
        return await _context.GradeLevels
            .FirstOrDefaultAsync(gl => gl.Code == code);
    }

    public async Task<GradeLevel> CreateGradeLevelAsync(string code, string name, int sortOrder)
    {
        var exists = await _context.GradeLevels.AnyAsync(gl => gl.Code == code);
        if (exists)
        {
            throw new InvalidOperationException($"Grade level with code '{code}' already exists.");
        }

        var gradeLevel = new GradeLevel
        {
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.GradeLevels.Add(gradeLevel);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created grade level: {Code} - {Name} (ID: {Id})", code, name, gradeLevel.Id);

        return gradeLevel;
    }

    public async Task UpdateGradeLevelAsync(GradeLevel gradeLevel)
    {
        _context.GradeLevels.Update(gradeLevel);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated grade level: {Code} (ID: {Id})", gradeLevel.Code, gradeLevel.Id);
    }

    public async Task DeactivateGradeLevelAsync(Guid id)
    {
        var gradeLevel = await GetGradeLevelByIdAsync(id);
        if (gradeLevel == null)
            throw new InvalidOperationException($"Grade level with ID {id} not found.");

        gradeLevel.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated grade level: {Code} (ID: {Id})", gradeLevel.Code, id);
    }

    public async Task DeleteGradeLevelAsync(Guid id)
    {
        var gradeLevel = await _context.GradeLevels
            .Include(g => g.Sections)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (gradeLevel == null)
            throw new InvalidOperationException("Grade level not found.");

        if (gradeLevel.Sections.Count > 0)
            throw new InvalidOperationException($"Cannot delete '{gradeLevel.Name}': it has {gradeLevel.Sections.Count} section(s). Remove them first.");

        var hasStudents = await _context.Students.AnyAsync(s => s.GradeLevel == gradeLevel.Code);
        if (hasStudents)
            throw new InvalidOperationException($"Cannot delete '{gradeLevel.Name}': students are assigned to this grade level.");

        _context.GradeLevels.Remove(gradeLevel);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted grade level: {Code} (ID: {Id})", gradeLevel.Code, id);
    }

    #endregion

    #region Section Operations

    public async Task<List<Section>> GetAllSectionsAsync(bool activeOnly = true)
    {
        var query = _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Adviser)
            .Include(s => s.Program)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.GradeLevel.SortOrder)
            .ThenBy(s => s.Program.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<List<Section>> GetSectionsByGradeAsync(Guid gradeLevelId, bool activeOnly = true)
    {
        var query = _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Adviser)
            .Include(s => s.Program)
            .Where(s => s.GradeLevelId == gradeLevelId);

        if (activeOnly)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.Program.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Section?> GetSectionByIdAsync(Guid id)
    {
        return await _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Adviser)
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Section> CreateSectionAsync(Guid gradeLevelId, string name, Guid programId, Guid? adviserId = null, int capacity = 40)
    {
        var gradeLevel = await _context.GradeLevels.FindAsync(gradeLevelId);
        if (gradeLevel == null)
            throw new InvalidOperationException($"Grade level with ID {gradeLevelId} not found.");

        if (adviserId.HasValue)
        {
            var adviser = await _context.Faculties.FindAsync(adviserId.Value);
            if (adviser == null)
                throw new InvalidOperationException($"Faculty with ID {adviserId.Value} not found.");
        }

        var program = await _context.Programs.FindAsync(programId);
        if (program == null)
            throw new InvalidOperationException($"Program with ID {programId} not found.");

        var section = new Section
        {
            Name = name,
            GradeLevelId = gradeLevelId,
            ProgramId = programId,
            AdviserId = adviserId,
            Capacity = capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Sections.Add(section);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created section: {Grade} - {Program} - {Section} (ID: {Id})",
            gradeLevel.Name, program.Code, name, section.Id);

        return section;
    }

    public async Task<List<Entities.Program>> GetProgramsForGradeAsync(Guid gradeLevelId)
    {
        var linkedProgramIds = await _context.GradeLevelPrograms
            .Where(glp => glp.GradeLevelId == gradeLevelId)
            .Select(glp => glp.ProgramId)
            .ToListAsync();

        return await _context.Programs
            .Where(p => linkedProgramIds.Contains(p.Id) && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync();
    }

    public async Task UpdateSectionAsync(Section section)
    {
        _context.Sections.Update(section);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated section: {Section} (ID: {Id})", section.Name, section.Id);
    }

    public async Task DeactivateSectionAsync(Guid id)
    {
        var section = await GetSectionByIdAsync(id);
        if (section == null)
            throw new InvalidOperationException($"Section with ID {id} not found.");

        section.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated section: {Section} (ID: {Id})", section.Name, id);
    }

    public async Task DeleteSectionAsync(Guid id)
    {
        var section = await _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (section == null)
            throw new InvalidOperationException("Section not found.");

        var enrollmentCount = await _context.StudentEnrollments.CountAsync(e => e.SectionId == id);
        if (enrollmentCount > 0)
            throw new InvalidOperationException($"Cannot delete '{section.Name}': it has {enrollmentCount} enrollment record(s). Withdraw students first.");

        _context.Sections.Remove(section);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted section: {Grade} - {Section} (ID: {Id})", section.GradeLevel.Name, section.Name, id);
    }

    public async Task<List<Entities.Program>> GetAllProgramsAsync(bool activeOnly = false)
    {
        var query = _context.Programs
            .Include(p => p.Sections)
            .Include(p => p.GradeLevelPrograms)
                .ThenInclude(glp => glp.GradeLevel)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync();
    }

    public async Task<Entities.Program?> GetProgramByIdAsync(Guid id)
    {
        return await _context.Programs
            .Include(p => p.GradeLevelPrograms)
                .ThenInclude(glp => glp.GradeLevel)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Entities.Program> CreateProgramAsync(string code, string name, string? description,
        int sortOrder, IEnumerable<Guid> gradeLevelIds)
    {
        if (await _context.Programs.AnyAsync(p => p.Code == code))
            throw new InvalidOperationException($"Program code '{code}' is already in use.");

        var program = new Entities.Program
        {
            Code = code.ToUpperInvariant().Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Programs.Add(program);
        await _context.SaveChangesAsync();

        // Add grade level links
        foreach (var glId in gradeLevelIds.Distinct())
        {
            _context.GradeLevelPrograms.Add(new GradeLevelProgram { GradeLevelId = glId, ProgramId = program.Id });
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created program: {Code} (ID: {Id})", program.Code, program.Id);
        return program;
    }

    public async Task UpdateProgramAsync(Entities.Program program, IEnumerable<Guid> gradeLevelIds)
    {
        _context.Programs.Update(program);

        // Replace grade level links
        var existingLinks = await _context.GradeLevelPrograms
            .Where(glp => glp.ProgramId == program.Id)
            .ToListAsync();
        _context.GradeLevelPrograms.RemoveRange(existingLinks);

        foreach (var glId in gradeLevelIds.Distinct())
        {
            _context.GradeLevelPrograms.Add(new GradeLevelProgram { GradeLevelId = glId, ProgramId = program.Id });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated program: {Code} (ID: {Id})", program.Code, program.Id);
    }

    public async Task DeleteProgramAsync(Guid id)
    {
        var program = await _context.Programs
            .Include(p => p.Sections)
            .Include(p => p.GradeLevelPrograms)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (program == null)
            throw new InvalidOperationException("Program not found.");

        if (program.Sections.Count > 0)
            throw new InvalidOperationException($"Cannot delete '{program.Code}': it is assigned to {program.Sections.Count} section(s). Reassign them first.");

        _context.GradeLevelPrograms.RemoveRange(program.GradeLevelPrograms);
        _context.Programs.Remove(program);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted program: {Code} (ID: {Id})", program.Code, id);
    }

    #endregion

    #region Student Enrollment Operations

    public async Task<StudentEnrollment> EnrollStudentAsync(Guid studentId, Guid sectionId, Guid academicYearId)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
        {
            throw new InvalidOperationException($"Student with ID {studentId} not found.");
        }

        var section = await _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.Id == sectionId);
        if (section == null)
        {
            throw new InvalidOperationException($"Section with ID {sectionId} not found.");
        }

        var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
        if (academicYear == null)
        {
            throw new InvalidOperationException($"Academic year with ID {academicYearId} not found.");
        }

        // Check if student already has an active enrollment for this academic year
        var existingEnrollment = await _context.StudentEnrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId
                && e.AcademicYearId == academicYearId
                && e.IsActive);

        if (existingEnrollment != null)
        {
            throw new InvalidOperationException(
                $"Student already has an active enrollment for academic year {academicYear.Name}. " +
                "Use TransferStudentAsync to change sections.");
        }

        var enrollment = new StudentEnrollment
        {
            StudentId = studentId,
            SectionId = sectionId,
            AcademicYearId = academicYearId,
            EnrolledAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.StudentEnrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Update student's current enrollment reference
        student.CurrentEnrollmentId = enrollment.Id;

        // Update denormalized fields for backward compatibility
        student.GradeLevel = section.GradeLevel.Code;
        student.Section = section.Name;
        student.Program = section.Program?.Code;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Enrolled student {StudentId} in section {Section} for {AcademicYear}",
            studentId, section.Name, academicYear.Name);

        return enrollment;
    }

    public async Task<StudentEnrollment> TransferStudentAsync(Guid studentId, Guid newSectionId, Guid academicYearId)
    {
        var currentEnrollment = await _context.StudentEnrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId
                && e.AcademicYearId == academicYearId
                && e.IsActive);

        if (currentEnrollment == null)
        {
            throw new InvalidOperationException(
                "No active enrollment found for this student in the specified academic year. " +
                "Use EnrollStudentAsync instead.");
        }

        // Deactivate current enrollment
        currentEnrollment.IsActive = false;

        // Create new enrollment
        var newSection = await _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.Id == newSectionId);

        if (newSection == null)
        {
            throw new InvalidOperationException($"Section with ID {newSectionId} not found.");
        }

        var newEnrollment = new StudentEnrollment
        {
            StudentId = studentId,
            SectionId = newSectionId,
            AcademicYearId = academicYearId,
            EnrolledAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.StudentEnrollments.Add(newEnrollment);
        await _context.SaveChangesAsync();

        // Update student's current enrollment reference
        var student = await _context.Students.FindAsync(studentId);
        if (student != null)
        {
            student.CurrentEnrollmentId = newEnrollment.Id;
            student.Section = newSection.Name;
            student.Program = newSection.Program?.Code;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Transferred student {StudentId} to section {Section}",
            studentId, newSection.Name);

        return newEnrollment;
    }

    public async Task<List<StudentEnrollment>> GetStudentEnrollmentsAsync(Guid studentId)
    {
        return await _context.StudentEnrollments
            .Include(e => e.Section)
                .ThenInclude(s => s.GradeLevel)
            .Include(e => e.AcademicYear)
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<StudentEnrollment?> GetCurrentEnrollmentAsync(Guid studentId, Guid academicYearId)
    {
        return await _context.StudentEnrollments
            .Include(e => e.Section)
                .ThenInclude(s => s.GradeLevel)
            .Include(e => e.AcademicYear)
            .FirstOrDefaultAsync(e => e.StudentId == studentId
                && e.AcademicYearId == academicYearId
                && e.IsActive);
    }

    public async Task<List<StudentEnrollment>> GetSectionEnrollmentsAsync(Guid sectionId, Guid academicYearId, bool activeOnly = true)
    {
        var query = _context.StudentEnrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Where(e => e.SectionId == sectionId && e.AcademicYearId == academicYearId);

        if (activeOnly)
        {
            query = query.Where(e => e.IsActive);
        }

        return await query
            .OrderBy(e => e.Student.LastName)
            .ThenBy(e => e.Student.FirstName)
            .ToListAsync();
    }

    public async Task WithdrawStudentAsync(Guid enrollmentId)
    {
        var enrollment = await _context.StudentEnrollments.FindAsync(enrollmentId);
        if (enrollment == null)
        {
            throw new InvalidOperationException($"Enrollment with ID {enrollmentId} not found.");
        }

        enrollment.IsActive = false;
        await _context.SaveChangesAsync();

        // Update student's current enrollment reference if this was their current enrollment
        var student = await _context.Students.FindAsync(enrollment.StudentId);
        if (student != null && student.CurrentEnrollmentId == enrollmentId)
        {
            student.CurrentEnrollmentId = null;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Withdrew student {StudentId} from enrollment {EnrollmentId}",
            enrollment.StudentId, enrollmentId);
    }

    #endregion
}
