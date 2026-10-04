using NetLog.Core.Analysis;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class StateDeriverTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Stop  = new(2026, 6, 12, 0, 0, 0, TimeSpan.Zero);

    private static RunConfig Config(int archiveMins = 60) => new(
        "r", new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero),
        new RunSchedule(Start, Stop), new[] { "H" }, "svc",
        new RunSettings("Merged", 10, archiveMins, 7, null));

    private static HostData Host(DateTimeOffset? last) =>
        new("H", last is null ? null : new HostStatus(last.Value, 1, 0, 1), Array.Empty<ConnectionRecord>());

    [Fact]
    public void Before_start_is_Scheduled()
    {
        var s = StateDeriver.DeriveState(Config(), new[] { Host(null) }, Start.AddMinutes(-1));
        Assert.Equal(RunState.Scheduled, s);
    }

    [Fact]
    public void After_stop_is_Completed()
    {
        var s = StateDeriver.DeriveState(Config(), new[] { Host(Stop.AddMinutes(-1)) }, Stop.AddSeconds(1));
        Assert.Equal(RunState.Completed, s);
    }

    [Fact]
    public void In_window_with_recent_heartbeat_is_Running()
    {
        var now = Start.AddHours(1);
        var s = StateDeriver.DeriveState(Config(archiveMins: 60), new[] { Host(now.AddMinutes(-10)) }, now);
        Assert.Equal(RunState.Running, s);   // threshold = max(180,15)=180 min; 10 < 180
    }

    [Fact]
    public void In_window_with_stale_heartbeat_is_Stalled()
    {
        var now = Start.AddHours(10);
        var s = StateDeriver.DeriveState(Config(archiveMins: 60), new[] { Host(now.AddMinutes(-200)) }, now);
        Assert.Equal(RunState.Stalled, s);   // 200 > 180
    }

    [Fact]
    public void In_window_with_no_heartbeat_is_Stalled()
    {
        var now = Start.AddHours(1);
        var s = StateDeriver.DeriveState(Config(), new[] { Host(null) }, now);
        Assert.Equal(RunState.Stalled, s);
    }

    [Fact]
    public void Stalled_threshold_floors_at_15_minutes()
    {
        var now = Start.AddHours(1);
        // archiveMins=0 -> threshold = max(0,15)=15 min. 10 min stale -> Running, 20 -> Stalled.
        Assert.Equal(RunState.Running, StateDeriver.DeriveState(Config(0), new[] { Host(now.AddMinutes(-10)) }, now));
        Assert.Equal(RunState.Stalled, StateDeriver.DeriveState(Config(0), new[] { Host(now.AddMinutes(-20)) }, now));
    }

    [Fact]
    public void LastUpdate_is_max_heartbeat_or_null()
    {
        var a = Host(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero));
        var b = Host(new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero), StateDeriver.LastUpdate(new[] { a, b }));
        Assert.Null(StateDeriver.LastUpdate(new[] { Host(null) }));
    }

    [Fact]
    public void LastUpdate_ignores_null_status_hosts()
    {
        var a = Host(null);
        var b = Host(new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero), StateDeriver.LastUpdate(new[] { a, b }));
    }

    [Fact]
    public void Future_heartbeat_is_treated_as_Running_not_Stalled()
    {
        var now = Start.AddHours(1);
        var s = StateDeriver.DeriveState(Config(), new[] { Host(now.AddMinutes(30)) }, now); // heartbeat in the future (clock skew)
        Assert.Equal(RunState.Running, s);
    }
}
