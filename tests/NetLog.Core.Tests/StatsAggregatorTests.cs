using NetLog.Core.Analysis;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class StatsAggregatorTests
{
    // Flow semantics: for Incoming the remote peer (the interesting end) is the Source.
    private static ConnectionRecord C(string dir, string target, string proc) =>
        dir == "Incoming"
            ? new(dir, target, "", "0", "H", "", "443", proc)
            : new(dir, "H", "", "0", target, "", "443", proc);

    [Fact]
    public void Counts_totals_and_directions()
    {
        var recs = new[]
        {
            C("Outgoing", "a", "p1"),
            C("Outgoing", "a", "p1"),
            C("Incoming", "b", "p2"),
        };
        var s = StatsAggregator.Aggregate(recs);
        Assert.Equal(3, s.TotalConnections);
        Assert.Equal(1, s.IncomingCount);
        Assert.Equal(2, s.OutgoingCount);
    }

    [Fact]
    public void Top_targets_and_processes_ranked_then_name_tiebreak()
    {
        var recs = new[]
        {
            C("Outgoing", "a", "p1"),
            C("Outgoing", "a", "p1"),
            C("Outgoing", "b", "p2"),
            C("Outgoing", "c", "p3"),
            C("Outgoing", "d", "p4"),
        };
        var s = StatsAggregator.Aggregate(recs);

        Assert.Equal(4, s.TopTargets.Count);
        Assert.Equal("a", s.TopTargets[0].Name);
        Assert.Equal(2, s.TopTargets[0].Count);
        // b,c,d all count 1 -> ordinal name tiebreak -> b, c, d
        Assert.Equal("b", s.TopTargets[1].Name);
        Assert.Equal("c", s.TopTargets[2].Name);
        Assert.Equal("d", s.TopTargets[3].Name);

        Assert.Equal("p1", s.TopProcesses[0].Name);
        Assert.Equal(2, s.TopProcesses[0].Count);
    }

    [Fact]
    public void Top_lists_are_capped_at_ten()
    {
        var recs = Enumerable.Range(0, 15)
            .Select(i => C("Outgoing", $"t{i:00}", $"p{i:00}"))
            .ToArray();
        var s = StatsAggregator.Aggregate(recs);
        Assert.Equal(10, s.TopTargets.Count);
        Assert.Equal(10, s.TopProcesses.Count);
    }

    [Fact]
    public void Top_items_carry_direction_in_out_or_both()
    {
        var recs = new[]
        {
            C("Outgoing", "out-only", "p-out"),
            C("Incoming", "in-only",  "p-in"),
            // a peer reached both ways, and a process active both ways
            new ConnectionRecord("Outgoing", "ME", "", "1", "both", "", "443", "p-both"),
            new ConnectionRecord("Incoming", "both", "", "2", "ME", "", "80", "p-both"),
        };
        var s = StatsAggregator.Aggregate(recs);

        Assert.Equal("out", s.TopTargets.Single(t => t.Name == "out-only").Direction);
        Assert.Equal("in",  s.TopTargets.Single(t => t.Name == "in-only").Direction);
        Assert.Equal("io",  s.TopTargets.Single(t => t.Name == "both").Direction);
        Assert.Equal("io",  s.TopProcesses.Single(t => t.Name == "p-both").Direction);
        Assert.Equal("out", s.TopProcesses.Single(t => t.Name == "p-out").Direction);
    }

    [Fact]
    public void Empty_input_yields_zeroes_and_empty_tops()
    {
        var s = StatsAggregator.Aggregate(Array.Empty<ConnectionRecord>());
        Assert.Equal(0, s.TotalConnections);
        Assert.Empty(s.TopTargets);
        Assert.Empty(s.TopProcesses);
        Assert.Empty(s.TopUsers);
    }

    [Fact]
    public void Top_targets_pick_the_remote_peer_per_direction()
    {
        // Outgoing: remote = Target. Incoming: remote = Source (the initiator).
        var records = new[]
        {
            new ConnectionRecord("Outgoing", "10.0.0.1", "ME", "1", "10.9.9.9", "", "443", "p"),
            new ConnectionRecord("Incoming", "10.9.9.9", "", "2", "10.0.0.1", "ME", "8080", "p"),
            new ConnectionRecord("Incoming", "10.9.9.9", "", "3", "10.0.0.1", "ME", "8080", "q"),
        };
        var top = StatsAggregator.Aggregate(records).TopTargets;
        Assert.Equal("10.9.9.9", top[0].Name);   // counted from both directions
        Assert.Equal(3, top[0].Count);
    }

    [Fact]
    public void Top_targets_label_resolved_peers_as_name_ip()
    {
        var records = new[]
        {
            new ConnectionRecord("Outgoing", "10.0.0.1", "ME", "1", "10.0.0.2", "", "443", "p"),
            new ConnectionRecord("Outgoing", "10.0.0.1", "ME", "2", "10.0.0.2", "srv.corp.local", "443", "q"),
        };
        var top = StatsAggregator.Aggregate(records).TopTargets;
        Assert.Equal("srv.corp.local (10.0.0.2)", top[0].Name);   // first non-blank name labels the group
        Assert.Equal(2, top[0].Count);
    }

    [Fact]
    public void Top_users_split_semicolon_lists_and_skip_blanks()
    {
        var recs = new[]
        {
            C("Outgoing", "dummy1", "chrome") with { Usernames = "DOMAIN\\alice;DOMAIN\\bob" },
            C("Outgoing", "dummy2", "app")    with { Usernames = "DOMAIN\\alice" },
            C("Incoming", "dummy3", "svc")    with { Usernames = "" },              // legacy/blank: skipped
        };
        var stats = StatsAggregator.Aggregate(recs);
        Assert.Equal(2, stats.TopUsers.Count);
        Assert.Equal("DOMAIN\\alice", stats.TopUsers[0].Name);
        Assert.Equal(2, stats.TopUsers[0].Count);
        Assert.Equal("out", stats.TopUsers[0].Direction);
        Assert.Equal("DOMAIN\\bob", stats.TopUsers[1].Name);
    }

    [Fact]
    public void Top_users_group_case_insensitively_keeping_first_seen_casing()
    {
        var recs = new[]
        {
            C("Outgoing", "a", "a") with { Usernames = "DOMAIN\\Alice" },
            C("Incoming", "b", "b") with { Usernames = "domain\\alice" },
        };
        var u = Assert.Single(StatsAggregator.Aggregate(recs).TopUsers);
        Assert.Equal("DOMAIN\\Alice", u.Name);
        Assert.Equal(2, u.Count);
        Assert.Equal("io", u.Direction);
    }
}
