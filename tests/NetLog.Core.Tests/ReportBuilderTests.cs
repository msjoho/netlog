using NetLog.Core.Analysis;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class ReportBuilderTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Stop  = new(2026, 6, 12, 0, 0, 0, TimeSpan.Zero);

    private static RunData Data()
    {
        var config = new RunConfig("r1", new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero),
            new RunSchedule(Start, Stop), new[] { "H1" }, "svc",
            new RunSettings("Merged", 10, 60, 7, null));

        var conns = new ConnectionRecord[]
        {
            new("Outgoing", "H1", "", "", "R", "", "443", "app"),
            new("Incoming", "C", "", "", "H1", "", "445", "svc"),
        };
        var host = new HostData("H1", new HostStatus(Start.AddMinutes(30), 1, 2, 3), conns);
        return new RunData(config, new[] { host });
    }

    [Fact]
    public void Builds_report_with_state_stats_and_host_summary()
    {
        var now = Start.AddHours(1);   // in window, heartbeat 30 min old, threshold 180 -> Running
        var r = ReportBuilder.Build(Data(), now);

        Assert.Equal("r1", r.RunName);
        Assert.Equal(RunState.Running, r.State);
        Assert.Equal(Start, r.StartUtc);
        Assert.Equal(Stop, r.StopUtc);
        Assert.Equal(TimeSpan.FromHours(1), r.Elapsed);
        Assert.Equal(Start.AddMinutes(30), r.LastUpdateUtc);
        Assert.Equal(2, r.Stats.TotalConnections);

        var h = Assert.Single(r.Hosts);
        Assert.Equal("H1", h.HostName);
        Assert.Equal(2, h.ConnectionCount);
        Assert.Equal(3, h.TotalPolls);
    }

    [Fact]
    public void Each_host_summary_carries_its_own_stats()
    {
        var conns2 = new ConnectionRecord[]
        {
            new("Outgoing", "H2", "", "", "R", "", "443", "app"),
            new("Outgoing", "H2", "", "", "R", "", "443", "app"),
            new("Incoming", "C", "", "", "H2", "", "80", "web"),
        };
        var data = new RunData(Data().Config, new[]
        {
            Data().Hosts[0],                                            // H1: 1 in / 1 out
            new HostData("H2", new HostStatus(Start, 1, 2, 3), conns2), // H2: 1 in / 2 out
        });

        var r = ReportBuilder.Build(data, Start.AddHours(1));

        var h1 = r.Hosts.Single(h => h.HostName == "H1");
        Assert.Equal(2, h1.Stats.TotalConnections);
        Assert.Equal(1, h1.Stats.IncomingCount);
        Assert.Equal(1, h1.Stats.OutgoingCount);

        var h2 = r.Hosts.Single(h => h.HostName == "H2");
        Assert.Equal(3, h2.Stats.TotalConnections);
        Assert.Equal(1, h2.Stats.IncomingCount);
        Assert.Equal(2, h2.Stats.OutgoingCount);
        Assert.Equal("web", h2.Stats.TopProcesses.First(t => t.Count == 1).Name);
    }

    [Fact]
    public void Host_liveness_reflects_heartbeat_age()
    {
        var config = Data().Config; // stalled threshold = max(3*60, 15) = 180 min
        var live    = new HostData("L", new HostStatus(Start.AddMinutes(30),   1, 2, 3), Array.Empty<ConnectionRecord>());
        var stalled = new HostData("S", new HostStatus(Start.AddMinutes(-600), 1, 2, 3), Array.Empty<ConnectionRecord>());
        var dark    = new HostData("D", null, Array.Empty<ConnectionRecord>());
        var data = new RunData(config, new[] { live, stalled, dark });

        var r = ReportBuilder.Build(data, Start.AddHours(1));

        Assert.Equal(HostLiveness.Live,        r.Hosts.Single(h => h.HostName == "L").Liveness);
        Assert.Equal(HostLiveness.Stalled,     r.Hosts.Single(h => h.HostName == "S").Liveness);
        Assert.Equal(HostLiveness.NoHeartbeat, r.Hosts.Single(h => h.HostName == "D").Liveness);
    }

    [Fact]
    public void Elapsed_is_zero_before_start_and_capped_after_stop()
    {
        Assert.Equal(TimeSpan.Zero, ReportBuilder.Build(Data(), Start.AddMinutes(-5)).Elapsed);
        Assert.Equal(Stop - Start, ReportBuilder.Build(Data(), Stop.AddDays(1)).Elapsed);
    }

    [Fact]
    public void Zero_hosts_yields_empty_hosts_and_zero_stats()
    {
        var config = new RunConfig("r1", new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero),
            new RunSchedule(Start, Stop), Array.Empty<string>(), "svc",
            new RunSettings("Merged", 10, 60, 7, null));
        var data = new RunData(config, Array.Empty<HostData>());
        var r = ReportBuilder.Build(data, Start.AddHours(1));
        Assert.Empty(r.Hosts);
        Assert.Equal(0, r.Stats.TotalConnections);
        Assert.Null(r.LastUpdateUtc);
        Assert.Equal(RunState.Stalled, r.State); // in window, no heartbeat
    }
}
