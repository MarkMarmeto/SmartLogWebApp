namespace SmartLog.Web.Models.Sms;

public class ProgramWithGrades
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<GradeLevelItem> Grades { get; set; } = new();
}

public class GradeLevelItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
