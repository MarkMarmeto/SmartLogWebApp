namespace SmartLog.Web.Models.Sms;

public enum BroadcastLanguageMode { EnglishOnly, FilipinoOnly, Both }

public class BroadcastMessageBodies
{
    public BroadcastLanguageMode Mode { get; set; } = BroadcastLanguageMode.Both;
    public string EnglishBody { get; set; } = string.Empty;
    public string? FilipinoBody { get; set; }

    /// <summary>Returns the correct body for a given student's language preference.</summary>
    public string GetBodyForLanguage(string? smsLanguage) =>
        smsLanguage == "FIL" && !string.IsNullOrWhiteSpace(FilipinoBody) ? FilipinoBody : EnglishBody;

    /// <summary>Returns true when the student should receive this broadcast given the mode.</summary>
    public bool ShouldSendToStudent(string? smsLanguage) => Mode switch
    {
        BroadcastLanguageMode.EnglishOnly => smsLanguage != "FIL",
        BroadcastLanguageMode.FilipinoOnly => smsLanguage != "EN",
        _ => true
    };
}
