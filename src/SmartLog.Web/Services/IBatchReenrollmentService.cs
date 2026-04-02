namespace SmartLog.Web.Services;

public interface IBatchReenrollmentService
{
    Task<ReenrollmentPreview> GeneratePreviewAsync(Guid sourceYearId, Guid targetYearId);
    Task<ReenrollmentResult> ExecuteReenrollmentAsync(Guid sourceYearId, Guid targetYearId, List<StudentPromotionAssignment> assignments, string userId);
}

public enum PromotionAction
{
    Promote,
    Graduate,
    Skip
}

public class StudentPromotionItem
{
    public Guid StudentId { get; set; }
    public string StudentDisplayId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CurrentGradeCode { get; set; } = string.Empty;
    public string CurrentGradeName { get; set; } = string.Empty;
    public string CurrentSection { get; set; } = string.Empty;
    public string TargetGradeCode { get; set; } = string.Empty;
    public string TargetGradeName { get; set; } = string.Empty;
    public Guid? AssignedSectionId { get; set; }
    public string AssignedSectionName { get; set; } = string.Empty;
    public PromotionAction Action { get; set; }
    public string SkipReason { get; set; } = string.Empty;
}

public class StudentPromotionAssignment
{
    public Guid StudentId { get; set; }
    public PromotionAction Action { get; set; }
    public Guid? SectionId { get; set; }
}

public class SectionCapacityInfo
{
    public Guid SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public Guid GradeLevelId { get; set; }
    public string GradeCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int CurrentCount { get; set; }
    public int RemainingCapacity => Capacity - CurrentCount;
}

public class ReenrollmentPreview
{
    public List<StudentPromotionItem> Students { get; set; } = new();
    public Dictionary<string, List<SectionCapacityInfo>> SectionsByGrade { get; set; } = new();
    public int PromoteCount => Students.Count(s => s.Action == PromotionAction.Promote);
    public int GraduateCount => Students.Count(s => s.Action == PromotionAction.Graduate);
    public int SkipCount => Students.Count(s => s.Action == PromotionAction.Skip);
    public List<string> Warnings { get; set; } = new();
}

public class ReenrollmentResult
{
    public int PromotedCount { get; set; }
    public int GraduatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<ReenrollmentError> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class ReenrollmentError
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
