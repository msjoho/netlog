using NetLog.Core.RunSpec;

namespace NetLog.Core.Tests;

public class RunSpecParserTests
{
    [Theory]
    [InlineData("7d", 7 * 24 * 60)]
    [InlineData("12h", 12 * 60)]
    [InlineData("90m", 90)]
    public void ParseDuration_parses_units(string input, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), RunSpecParser.ParseDuration(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("7")]
    [InlineData("7x")]
    [InlineData("-3d")]
    public void ParseDuration_rejects_bad_input(string input)
    {
        Assert.Throws<FormatException>(() => RunSpecParser.ParseDuration(input));
    }

    [Fact]
    public void ResolveStart_now_uses_provided_clock()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(now, RunSpecParser.ResolveStart("now", now));
    }

    [Fact]
    public void ResolveStart_parses_iso_as_utc()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
                     RunSpecParser.ResolveStart("2026-06-10T00:00:00Z", now));
    }

    [Fact]
    public void BuildSchedule_from_duration()
    {
        var now = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);
        var s = RunSpecParser.BuildSchedule(start: "now", stop: null, duration: "2d", now: now);
        Assert.Equal(now, s.StartUtc);
        Assert.Equal(now.AddDays(2), s.StopUtc);
    }

    [Fact]
    public void BuildSchedule_from_explicit_stop()
    {
        var now = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);
        var s = RunSpecParser.BuildSchedule(start: "2026-06-10T00:00:00Z", stop: "2026-06-12T00:00:00Z", duration: null, now: now);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), s.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), s.StopUtc);
    }

    [Fact]
    public void BuildSchedule_requires_stop_or_duration()
    {
        var now = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() => RunSpecParser.BuildSchedule("now", null, null, now));
    }

    [Fact]
    public void BuildSchedule_rejects_stop_before_start()
    {
        var now = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() =>
            RunSpecParser.BuildSchedule("2026-06-12T00:00:00Z", "2026-06-10T00:00:00Z", null, now));
    }

    [Fact]
    public void BuildSchedule_rejects_both_stop_and_duration()
    {
        var now = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() =>
            RunSpecParser.BuildSchedule("now", "2026-06-16T00:00:00Z", "7d", now));
    }
}
