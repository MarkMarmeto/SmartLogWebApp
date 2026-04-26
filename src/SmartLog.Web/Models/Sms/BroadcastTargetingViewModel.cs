namespace SmartLog.Web.Models.Sms;

public class BroadcastTargetingViewModel
{
    public List<ProgramWithGrades> ProgramsWithGrades { get; set; } = new();
    public List<NonGradedSectionItem> NonGradedSections { get; set; } = new();
}

public class NonGradedSectionItem
{
    public string Name { get; set; } = string.Empty;
}
