namespace SmartLog.Web.Models.Sms;

public class ProgramGradeFilter
{
    public string ProgramCode { get; set; } = string.Empty;
    public List<string> GradeLevelCodes { get; set; } = new();

    // US0107: When ProgramCode is empty and SectionNames is non-empty,
    // this entry denotes a Non-Graded selection. Resolver matches
    // students by GradeLevel == "NG" AND Section IN SectionNames.
    public List<string>? SectionNames { get; set; }
}
