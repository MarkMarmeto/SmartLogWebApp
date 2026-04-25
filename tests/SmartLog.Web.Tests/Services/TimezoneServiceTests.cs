using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Services;

namespace SmartLog.Web.Tests.Services;

public class TimezoneServiceTests
{
    private readonly TimezoneService _service;

    public TimezoneServiceTests()
    {
        var logger = new Mock<ILogger<TimezoneService>>();
        _service = new TimezoneService(logger.Object);
    }

    [Fact]
    public void ToPhilippinesTime_ConvertsFromUtc()
    {
        var utc = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc); // midnight UTC
        var pht = _service.ToPhilippinesTime(utc);

        Assert.Equal(8, pht.Hour); // PHT is UTC+8
        Assert.Equal(12, pht.Day);
    }

    [Fact]
    public void ToUtc_ConvertsFromPhilippinesTime()
    {
        var pht = new DateTime(2026, 4, 12, 8, 0, 0, DateTimeKind.Unspecified); // 8am PHT
        var utc = _service.ToUtc(pht);

        Assert.Equal(0, utc.Hour); // should be midnight UTC
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ToUtc_AlreadyUtc_ReturnsAsIs()
    {
        var utc = new DateTime(2026, 4, 12, 5, 0, 0, DateTimeKind.Utc);
        var result = _service.ToUtc(utc);

        Assert.Equal(utc, result);
    }

    [Fact]
    public void GetCurrentPhilippinesTime_ReturnsReasonableValue()
    {
        var pht = _service.GetCurrentPhilippinesTime();
        var expectedUtcOffset = DateTime.UtcNow.AddHours(8);

        // Should be within a few seconds
        Assert.True(Math.Abs((pht - expectedUtcOffset).TotalSeconds) < 5);
    }

    [Fact]
    public void FormatForDisplay_DefaultFormat_Returns12HourFormat()
    {
        var utc = new DateTime(2026, 4, 12, 5, 30, 0, DateTimeKind.Utc); // 5:30 UTC = 1:30 PM PHT
        var result = _service.FormatForDisplay(utc);

        Assert.Contains("01:30:00 PM", result);
    }

    [Fact]
    public void FormatForDisplay_CustomFormat_UsesFormat()
    {
        var utc = new DateTime(2026, 4, 12, 5, 30, 0, DateTimeKind.Utc);
        var result = _service.FormatForDisplay(utc, "HH:mm");

        Assert.Equal("13:30", result);
    }

    [Fact]
    public void RoundTrip_UtcToPhilippinesAndBack()
    {
        var original = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var pht = _service.ToPhilippinesTime(original);
        var backToUtc = _service.ToUtc(pht);

        Assert.Equal(original.Hour, backToUtc.Hour);
        Assert.Equal(original.Minute, backToUtc.Minute);
    }
}
