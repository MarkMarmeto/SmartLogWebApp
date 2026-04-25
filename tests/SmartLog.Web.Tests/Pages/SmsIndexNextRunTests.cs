using SmartLog.Web.Pages.Admin.Sms;

namespace SmartLog.Web.Tests.Pages;

public class SmsIndexNextRunTests
{
    [Fact]
    public void SmsEnabled_AlertEnabled_NotRanToday_ShowsTodayAtTime()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, true, false, "18:10");
        Assert.Equal("Today at 6:10 PM", result);
    }

    [Fact]
    public void SmsEnabled_AlertEnabled_RanToday_ShowsTomorrowAtTime()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, true, true, "18:10");
        Assert.Equal("Tomorrow at 6:10 PM", result);
    }

    [Fact]
    public void SmsDisabled_ShowsDisabledSmsOff()
    {
        var result = IndexModel.ComputeNextRunDisplay(false, true, false, "18:10");
        Assert.Equal("Disabled (SMS off)", result);
    }

    [Fact]
    public void SmsDisabled_RanToday_StillShowsDisabledSmsOff()
    {
        var result = IndexModel.ComputeNextRunDisplay(false, true, true, "18:10");
        Assert.Equal("Disabled (SMS off)", result);
    }

    [Fact]
    public void AlertDisabled_ShowsDisabledAlertOff()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, false, false, "18:10");
        Assert.Equal("Disabled (alert off)", result);
    }

    [Fact]
    public void AlertDisabled_SmsDisabled_ShowsSmsOffFirst()
    {
        // SMS off takes precedence over alert off
        var result = IndexModel.ComputeNextRunDisplay(false, false, false, "18:10");
        Assert.Equal("Disabled (SMS off)", result);
    }

    [Fact]
    public void CustomAlertTime_ReflectsInDisplay()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, true, false, "17:30");
        Assert.Equal("Today at 5:30 PM", result);
    }

    [Fact]
    public void InvalidAlertTime_FallsBackToDefault()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, true, false, "99:99");
        Assert.Equal("Today at 6:10 PM", result);
    }

    [Fact]
    public void EmptyAlertTime_FallsBackToDefault()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, true, false, "");
        Assert.Equal("Today at 6:10 PM", result);
    }

    [Fact]
    public void MidnightAlertTime_ShowsCorrectFormat()
    {
        var result = IndexModel.ComputeNextRunDisplay(true, true, false, "00:05");
        Assert.Equal("Today at 12:05 AM", result);
    }
}
