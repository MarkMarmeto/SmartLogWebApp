using SmartLog.Web.Models.Sms;

namespace SmartLog.Web.Tests.Services;

/// <summary>
/// US0085: BroadcastMessageBodies language-mode routing logic.
/// </summary>
public class BroadcastLanguageRoutingTests
{
    // ── ShouldSendToStudent ──────────────────────────────────────────────────

    [Theory]
    [InlineData("EN",  true)]
    [InlineData("FIL", false)]
    [InlineData(null,  true)]
    public void EnglishOnly_SkipsFil_SendsEnAndNull(string? smsLang, bool expected)
    {
        var bodies = new BroadcastMessageBodies { Mode = BroadcastLanguageMode.EnglishOnly };
        Assert.Equal(expected, bodies.ShouldSendToStudent(smsLang));
    }

    [Theory]
    [InlineData("FIL", true)]
    [InlineData("EN",  false)]
    [InlineData(null,  false)]
    public void FilipinoOnly_SkipsEnAndNull_SendsFil(string? smsLang, bool expected)
    {
        var bodies = new BroadcastMessageBodies { Mode = BroadcastLanguageMode.FilipinoOnly };
        Assert.Equal(expected, bodies.ShouldSendToStudent(smsLang));
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("FIL")]
    [InlineData(null)]
    public void BothMode_SendsToAllStudents(string? smsLang)
    {
        var bodies = new BroadcastMessageBodies { Mode = BroadcastLanguageMode.Both };
        Assert.True(bodies.ShouldSendToStudent(smsLang));
    }

    // ── GetBodyForLanguage ───────────────────────────────────────────────────

    [Fact]
    public void GetBodyForLanguage_FilStudent_ReturnsFilipinoBodyWhenSet()
    {
        var bodies = new BroadcastMessageBodies
        {
            Mode = BroadcastLanguageMode.Both,
            EnglishBody = "English text",
            FilipinoBody = "Filipino text"
        };
        Assert.Equal("Filipino text", bodies.GetBodyForLanguage("FIL"));
    }

    [Fact]
    public void GetBodyForLanguage_EnStudent_ReturnsEnglishBody()
    {
        var bodies = new BroadcastMessageBodies
        {
            EnglishBody = "English text",
            FilipinoBody = "Filipino text"
        };
        Assert.Equal("English text", bodies.GetBodyForLanguage("EN"));
    }

    [Fact]
    public void GetBodyForLanguage_NullLanguage_ReturnsEnglishBody()
    {
        var bodies = new BroadcastMessageBodies
        {
            EnglishBody = "English text",
            FilipinoBody = "Filipino text"
        };
        Assert.Equal("English text", bodies.GetBodyForLanguage(null));
    }

    [Fact]
    public void GetBodyForLanguage_FilStudent_FilipinoBodyNull_FallsBackToEnglish()
    {
        var bodies = new BroadcastMessageBodies
        {
            EnglishBody = "English text",
            FilipinoBody = null
        };
        Assert.Equal("English text", bodies.GetBodyForLanguage("FIL"));
    }

    [Fact]
    public void GetBodyForLanguage_FilStudent_FilipinoBodyWhitespace_FallsBackToEnglish()
    {
        var bodies = new BroadcastMessageBodies
        {
            EnglishBody = "English text",
            FilipinoBody = "   "
        };
        Assert.Equal("English text", bodies.GetBodyForLanguage("FIL"));
    }

    // ── Default mode ─────────────────────────────────────────────────────────

    [Fact]
    public void DefaultMode_IsBoth()
    {
        var bodies = new BroadcastMessageBodies();
        Assert.Equal(BroadcastLanguageMode.Both, bodies.Mode);
    }
}
