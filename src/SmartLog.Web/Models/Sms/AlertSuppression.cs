namespace SmartLog.Web.Models.Sms;

public class AlertSuppression
{
    public string Reason { get; set; } = string.Empty;

    // Empty list means all grades are suppressed (system-wide).
    public List<string> GradeLevels { get; set; } = new();
}
