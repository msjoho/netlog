using NetLog.Cli.Rendering;

namespace NetLog.Cli.Tests;

public class TimeFormatTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("t+2", TimeSpan.FromHours(2), "t+2", "t+2");

    [Fact]
    public void Local_renders_in_the_given_zone()
    {
        var utc = new DateTimeOffset(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal("2026-06-12 10:00", TimeFormat.Local(utc, Plus2));
    }

    [Fact]
    public void OffsetLabel_shows_signed_hh_mm()
    {
        var at = new DateTimeOffset(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal("+02:00", TimeFormat.OffsetLabel(Plus2, at));
        Assert.Equal("+00:00", TimeFormat.OffsetLabel(TimeZoneInfo.Utc, at));
    }

    [Theory]
    [InlineData(8, "8s ago")]
    [InlineData(70, "1m ago")]
    [InlineData(370, "6m ago")]
    [InlineData(3700, "1h ago")]
    [InlineData(90000, "1d ago")]
    public void Ago_uses_coarsest_unit(int secondsOld, string expected)
    {
        var now = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var last = now.AddSeconds(-secondsOld);
        Assert.Equal(expected, TimeFormat.Ago(last, now));
    }

    [Fact]
    public void LocalShort_is_month_day_time_invariant_culture()
    {
        var utc = new DateTimeOffset(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal("Jun 12 10:00", TimeFormat.LocalShort(utc, Plus2));
    }

    [Fact]
    public void Duration_pads_minutes_and_shows_days_when_present()
    {
        Assert.Equal("2d 14h 03m", TimeFormat.Duration(new TimeSpan(2, 14, 3, 0)));
        Assert.Equal("0h 05m", TimeFormat.Duration(TimeSpan.FromMinutes(5)));
    }
}
