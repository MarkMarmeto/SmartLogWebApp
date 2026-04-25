namespace SmartLog.Web.Models.Sms;

public class ProgramGradeFilter
{
    public string ProgramCode { get; set; } = string.Empty;
    public List<string> GradeLevelCodes { get; set; } = new();
}
