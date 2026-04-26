using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

public class BatchReenrollmentService : IBatchReenrollmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<BatchReenrollmentService> _logger;

    public BatchReenrollmentService(
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<BatchReenrollmentService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ReenrollmentPreview> GeneratePreviewAsync(Guid sourceYearId, Guid targetYearId)
    {
        if (sourceYearId == targetYearId)
            throw new InvalidOperationException("Source and target academic years must be different.");

        var sourceYear = await _context.AcademicYears.FindAsync(sourceYearId)
            ?? throw new InvalidOperationException("Source academic year not found.");
        var targetYear = await _context.AcademicYears.FindAsync(targetYearId)
            ?? throw new InvalidOperationException("Target academic year not found.");

        var preview = new ReenrollmentPreview();

        // Get all grade levels ordered by sort order
        var gradeLevels = await _context.GradeLevels
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToListAsync();

        // Build grade promotion map: each grade maps to the next one by sort order
        var promotionMap = new Dictionary<string, GradeLevel?>();
        for (int i = 0; i < gradeLevels.Count; i++)
        {
            var nextGrade = (i + 1 < gradeLevels.Count) ? gradeLevels[i + 1] : null;
            promotionMap[gradeLevels[i].Code] = nextGrade;
        }

        // Get all active enrollments for the source year with student and section info
        var sourceEnrollments = await _context.StudentEnrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
                .ThenInclude(s => s.GradeLevel)
            .Where(e => e.AcademicYearId == sourceYearId && e.IsActive)
            .ToListAsync();

        // Check which students already have enrollments in the target year
        var studentsInTargetYear = (await _context.StudentEnrollments
            .Where(e => e.AcademicYearId == targetYearId && e.IsActive)
            .Select(e => e.StudentId)
            .ToListAsync()).ToHashSet();

        // Get section capacities for target year
        var allSections = await _context.Sections
            .Include(s => s.GradeLevel)
            .Where(s => s.IsActive)
            .ToListAsync();

        var targetYearEnrollmentCounts = await _context.StudentEnrollments
            .Where(e => e.AcademicYearId == targetYearId && e.IsActive)
            .GroupBy(e => e.SectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count);

        // Build section capacity info grouped by grade code
        var sectionsByGrade = new Dictionary<string, List<SectionCapacityInfo>>();
        foreach (var section in allSections)
        {
            var gradeCode = section.GradeLevel.Code;
            if (!sectionsByGrade.ContainsKey(gradeCode))
                sectionsByGrade[gradeCode] = new List<SectionCapacityInfo>();

            targetYearEnrollmentCounts.TryGetValue(section.Id, out var currentCount);
            sectionsByGrade[gradeCode].Add(new SectionCapacityInfo
            {
                SectionId = section.Id,
                SectionName = section.Name,
                GradeLevelId = section.GradeLevelId,
                GradeCode = gradeCode,
                Capacity = section.Capacity,
                CurrentCount = currentCount
            });
        }
        preview.SectionsByGrade = sectionsByGrade;

        // Track assigned counts for even distribution during preview
        var previewAssignedCounts = new Dictionary<Guid, int>();

        // Group students by target grade for even section distribution
        var studentsByTargetGrade = new Dictionary<string, List<(StudentEnrollment enrollment, PromotionAction action, string targetGradeCode, string targetGradeName)>>();

        foreach (var enrollment in sourceEnrollments.OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName))
        {
            var student = enrollment.Student;
            var currentGradeCode = enrollment.Section.GradeLevel.Code;
            var currentGradeName = enrollment.Section.GradeLevel.Name;

            // Skip inactive students
            if (!student.IsActive)
            {
                preview.Students.Add(new StudentPromotionItem
                {
                    StudentId = student.Id,
                    StudentDisplayId = student.StudentId,
                    FullName = student.FullName,
                    CurrentGradeCode = currentGradeCode,
                    CurrentGradeName = currentGradeName,
                    CurrentSection = enrollment.Section.Name,
                    Action = PromotionAction.Skip,
                    SkipReason = "Student is inactive"
                });
                continue;
            }

            // Skip students already enrolled in target year
            if (studentsInTargetYear.Contains(student.Id))
            {
                preview.Students.Add(new StudentPromotionItem
                {
                    StudentId = student.Id,
                    StudentDisplayId = student.StudentId,
                    FullName = student.FullName,
                    CurrentGradeCode = currentGradeCode,
                    CurrentGradeName = currentGradeName,
                    CurrentSection = enrollment.Section.Name,
                    Action = PromotionAction.Skip,
                    SkipReason = "Already enrolled in target year"
                });
                continue;
            }

            // Determine promotion action
            if (!promotionMap.ContainsKey(currentGradeCode))
            {
                preview.Students.Add(new StudentPromotionItem
                {
                    StudentId = student.Id,
                    StudentDisplayId = student.StudentId,
                    FullName = student.FullName,
                    CurrentGradeCode = currentGradeCode,
                    CurrentGradeName = currentGradeName,
                    CurrentSection = enrollment.Section.Name,
                    Action = PromotionAction.Skip,
                    SkipReason = "Grade level not found in promotion map"
                });
                continue;
            }

            var nextGrade = promotionMap[currentGradeCode];
            if (nextGrade == null)
            {
                // Highest grade — graduate
                var key = "__graduate__";
                if (!studentsByTargetGrade.ContainsKey(key))
                    studentsByTargetGrade[key] = new();
                studentsByTargetGrade[key].Add((enrollment, PromotionAction.Graduate, "", "Graduated"));
            }
            else
            {
                var key = nextGrade.Code;
                if (!studentsByTargetGrade.ContainsKey(key))
                    studentsByTargetGrade[key] = new();
                studentsByTargetGrade[key].Add((enrollment, PromotionAction.Promote, nextGrade.Code, nextGrade.Name));
            }
        }

        // Process each target grade group for section assignment
        foreach (var (targetGradeKey, students) in studentsByTargetGrade)
        {
            foreach (var (enrollment, action, targetGradeCode, targetGradeName) in students)
            {
                var student = enrollment.Student;
                var currentGradeCode = enrollment.Section.GradeLevel.Code;
                var currentGradeName = enrollment.Section.GradeLevel.Name;

                var item = new StudentPromotionItem
                {
                    StudentId = student.Id,
                    StudentDisplayId = student.StudentId,
                    FullName = student.FullName,
                    CurrentGradeCode = currentGradeCode,
                    CurrentGradeName = currentGradeName,
                    CurrentSection = enrollment.Section.Name,
                    TargetGradeCode = targetGradeCode,
                    TargetGradeName = targetGradeName,
                    Action = action
                };

                if (action == PromotionAction.Promote)
                {
                    // Auto-assign section with most remaining capacity
                    if (sectionsByGrade.TryGetValue(targetGradeCode, out var availableSections) && availableSections.Count > 0)
                    {
                        var bestSection = availableSections
                            .OrderByDescending(s => s.RemainingCapacity - previewAssignedCounts.GetValueOrDefault(s.SectionId, 0))
                            .First();

                        item.AssignedSectionId = bestSection.SectionId;
                        item.AssignedSectionName = bestSection.SectionName;

                        previewAssignedCounts.TryGetValue(bestSection.SectionId, out var count);
                        previewAssignedCounts[bestSection.SectionId] = count + 1;

                        if (bestSection.RemainingCapacity - previewAssignedCounts.GetValueOrDefault(bestSection.SectionId, 0) < 0)
                        {
                            var warning = $"Section {bestSection.SectionName} in {targetGradeName} will be over capacity.";
                            if (!preview.Warnings.Contains(warning))
                                preview.Warnings.Add(warning);
                        }
                    }
                    else
                    {
                        item.Action = PromotionAction.Skip;
                        item.SkipReason = $"No active sections available for {targetGradeName}";
                        var warning = $"No active sections found for {targetGradeName}. Students in this grade cannot be promoted.";
                        if (!preview.Warnings.Contains(warning))
                            preview.Warnings.Add(warning);
                    }
                }

                preview.Students.Add(item);
            }
        }

        // Sort final list: Promote first, then Graduate, then Skip
        preview.Students = preview.Students
            .OrderBy(s => s.Action == PromotionAction.Promote ? 0 : s.Action == PromotionAction.Graduate ? 1 : 2)
            .ThenBy(s => s.TargetGradeName)
            .ThenBy(s => s.FullName)
            .ToList();

        return preview;
    }

    public async Task<ReenrollmentResult> ExecuteReenrollmentAsync(
        Guid sourceYearId, Guid targetYearId,
        List<StudentPromotionAssignment> assignments, string userId)
    {
        if (sourceYearId == targetYearId)
            throw new InvalidOperationException("Source and target academic years must be different.");

        var sw = Stopwatch.StartNew();
        var result = new ReenrollmentResult();

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var targetYear = await _context.AcademicYears.FindAsync(targetYearId)
                ?? throw new InvalidOperationException("Target academic year not found.");

            // Pre-load students and sections we'll need
            var studentIds = assignments.Select(a => a.StudentId).ToList();
            var students = await _context.Students
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            var sectionIds = assignments.Where(a => a.SectionId.HasValue).Select(a => a.SectionId!.Value).Distinct().ToList();
            var sections = await _context.Sections
                .Include(s => s.GradeLevel)
                .Include(s => s.Program)
                .Where(s => sectionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            // Check for existing enrollments in target year
            var existingTargetEnrollments = (await _context.StudentEnrollments
                .Where(e => e.AcademicYearId == targetYearId && e.IsActive && studentIds.Contains(e.StudentId))
                .Select(e => e.StudentId)
                .ToListAsync()).ToHashSet();

            foreach (var assignment in assignments)
            {
                if (!students.TryGetValue(assignment.StudentId, out var student))
                {
                    result.Errors.Add(new ReenrollmentError
                    {
                        StudentId = assignment.StudentId,
                        StudentName = "Unknown",
                        Message = "Student not found"
                    });
                    continue;
                }

                try
                {
                    switch (assignment.Action)
                    {
                        case PromotionAction.Skip:
                            result.SkippedCount++;
                            break;

                        case PromotionAction.Graduate:
                            // Deactivate current enrollment
                            var currentEnrollment = await _context.StudentEnrollments
                                .FirstOrDefaultAsync(e => e.StudentId == student.Id
                                    && e.AcademicYearId == sourceYearId
                                    && e.IsActive);
                            if (currentEnrollment != null)
                            {
                                currentEnrollment.IsActive = false;
                            }
                            student.CurrentEnrollmentId = null;
                            student.UpdatedAt = DateTime.UtcNow;
                            result.GraduatedCount++;
                            break;

                        case PromotionAction.Promote:
                            if (!assignment.SectionId.HasValue)
                            {
                                result.Errors.Add(new ReenrollmentError
                                {
                                    StudentId = student.Id,
                                    StudentName = student.FullName,
                                    Message = "No section assigned for promotion"
                                });
                                continue;
                            }

                            if (existingTargetEnrollments.Contains(student.Id))
                            {
                                result.SkippedCount++;
                                continue;
                            }

                            if (!sections.TryGetValue(assignment.SectionId.Value, out var section))
                            {
                                result.Errors.Add(new ReenrollmentError
                                {
                                    StudentId = student.Id,
                                    StudentName = student.FullName,
                                    Message = "Assigned section not found"
                                });
                                continue;
                            }

                            // Create new enrollment (replicating EnrollStudentAsync logic)
                            var enrollment = new StudentEnrollment
                            {
                                StudentId = student.Id,
                                SectionId = section.Id,
                                AcademicYearId = targetYearId,
                                EnrolledAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            _context.StudentEnrollments.Add(enrollment);

                            // We need to save to get the enrollment ID
                            await _context.SaveChangesAsync();

                            // Update student denormalized fields
                            student.CurrentEnrollmentId = enrollment.Id;
                            student.GradeLevel = section.GradeLevel.Code;
                            student.Section = section.Name;
                            student.Program = section.Program?.Code; // null for Non-Graded sections (US0106)
                            student.UpdatedAt = DateTime.UtcNow;

                            result.PromotedCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ReenrollmentError
                    {
                        StudentId = student.Id,
                        StudentName = student.FullName,
                        Message = ex.Message
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditService.LogAsync(
                action: "BatchReenrollment",
                performedByUserId: userId,
                details: $"Batch re-enrollment: {result.PromotedCount} promoted, {result.GraduatedCount} graduated, {result.SkippedCount} skipped, {result.Errors.Count} errors");

            _logger.LogInformation(
                "Batch re-enrollment completed: {Promoted} promoted, {Graduated} graduated, {Skipped} skipped by user {UserId}",
                result.PromotedCount, result.GraduatedCount, result.SkippedCount, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Batch re-enrollment failed");
            result.Errors.Add(new ReenrollmentError
            {
                StudentId = Guid.Empty,
                StudentName = "System",
                Message = "Re-enrollment failed: " + ex.Message
            });
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }
}
