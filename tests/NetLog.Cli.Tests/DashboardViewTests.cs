using NetLog.Cli.Rendering;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class DashboardViewTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Stop  = new(2026, 6, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now   = Start.AddDays(2);

    private static RunStats Stats(int total, int inc, int outg, (string, int)[] targets, (string, int)[] procs,
        (string, int, string)[]? users = null) =>
        new(total, inc, outg,
            targets.Select(t => new TopItem(t.Item1, t.Item2)).ToList(),
            procs.Select(p => new TopItem(p.Item1, p.Item2)).ToList(),
            (users ?? Array.Empty<(string, int, string)>())
                .Select(u => new TopItem(u.Item1, u.Item2, u.Item3)).ToList());

    private static RunStatusReport Report()
    {
        var h0 = Stats(50, 10, 40, new[] { ("10.0.4.12:1433", 40) }, new[] { ("sqlservr", 50) });
        var h1 = Stats(40, 30, 10, new[] { ("10.9.9.9:443", 30) },   new[] { ("w3wp", 40) });
        var agg = Stats(90, 40, 50,
            new[] { ("10.0.4.12:1433", 40), ("10.0.4.30:443", 30) },
            new[] { ("sqlservr", 50), ("chrome", 30) });
        var hosts = new[]
        {
            new HostSummary("SQL-PRD-01", Now.AddSeconds(-8),  50, 4103, h0, HostLiveness.Live),
            new HostSummary("WEB-PRD-02", Now.AddMinutes(-10), 40, 4101, h1, HostLiveness.Stalled),
        };
        return new RunStatusReport("audit-2026Q2", RunState.Running, Start, Stop,
            TimeSpan.FromDays(2), Now.AddSeconds(-8), hosts, agg);
    }

    private static string Render(int cursor, int? focus, TimeSpan? stale = null)
    {
        var console = new TestConsole();
        var state = new DashboardViewState(Report(), cursor, focus, Now, SecondsToRefresh: 4, StaleFor: stale);
        console.Write(DashboardView.Render(state, TimeZoneInfo.Utc, new ScreenSize(76, 200)));
        return console.Output;
    }

    [Fact]
    public void All_hosts_shows_aggregate_and_both_host_rows()
    {
        var o = Render(cursor: 0, focus: null);
        Assert.Contains("audit-2026Q2", o);
        Assert.Contains("Running", o);
        Assert.Contains("All hosts", o);
        Assert.Contains("SQL-PRD-01", o);
        Assert.Contains("WEB-PRD-02", o);
        Assert.Contains("chrome", o);          // aggregate-only top process
        Assert.DoesNotContain("w3wp", o);      // host-1 only, not in aggregate
    }

    [Fact]
    public void Focused_host_filters_stats_to_that_host()
    {
        var o = Render(cursor: 1, focus: 1);
        Assert.Contains("WEB-PRD-02", o);      // chip names the focused host
        Assert.Contains("w3wp", o);            // host-1's top process
        Assert.Contains("10.9.9.9", o);        // host-1's top target
        Assert.DoesNotContain("chrome", o);    // aggregate-only stat is gone
    }

    [Fact]
    public void Host_rows_show_liveness_glyph_labels()
    {
        var o = Render(cursor: 0, focus: null);
        Assert.Contains("live", o);
        Assert.Contains("stalled", o);
    }

    [Fact]
    public void Stale_frame_shows_a_banner()
    {
        var o = Render(cursor: 0, focus: null, stale: TimeSpan.FromSeconds(22));
        Assert.Contains("stale", o, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Escapes_markup_in_names()
    {
        var console = new TestConsole();
        var agg = Stats(1, 0, 1, new[] { ("t", 1) }, new[] { ("[bold]evil[/]", 1) });
        var report = new RunStatusReport("r", RunState.Running, Start, Stop, TimeSpan.Zero, null,
            Array.Empty<HostSummary>(), agg);
        console.Write(DashboardView.Render(
            new DashboardViewState(report, 0, null, Now, 4, null), TimeZoneInfo.Utc, new ScreenSize(76, 200)));
        Assert.Contains("[bold]evil[/]", console.Output);
    }

    [Fact]
    public void Renders_top_users_as_a_third_panel_when_the_frame_is_wide_enough()
    {
        var users = new[]
        {
            (@"DOMAIN\alice", 47, "out"),
            (@"NT AUTHORITY\SYSTEM", 38, "io"),
            (@"DOMAIN\carol", 29, "out"),
            (@"DOMAIN\dave", 24, "in"),
            (@"DOMAIN\erin", 19, "io"),
            (@"DOMAIN\frank", 63, "out"),
            (@"DOMAIN\gina", 52, "in"),
        };
        var agg = Stats(1, 0, 1, new[] { ("t", 1) }, new[] { ("p", 1) }, users);
        var report = new RunStatusReport("r", RunState.Running, Start, Stop, TimeSpan.Zero, null,
            Array.Empty<HostSummary>(), agg);
        var state = new DashboardViewState(report, 0, null, Now, 4, null);

        var wide = new TestConsole(); wide.Profile.Width = 200;
        wide.Write(DashboardView.Render(state, TimeZoneInfo.Utc, new ScreenSize(120, 200)));
        var lines = wide.Output.Replace("\r\n", "\n").Split('\n');
        var head = Assert.Single(lines, l => l.Contains("TOP TARGETS"));
        Assert.Contains("TOP USERS", head);
        var aliceLine = Assert.Single(lines, l => l.Contains(@"DOMAIN\alice"));
        Assert.Contains("47", aliceLine);
        Assert.DoesNotContain("frank", aliceLine);                 // one user per row, no second column
        Assert.Single(lines, l => l.Contains(@"DOMAIN\frank"));

        // At 76 columns three panels cannot fit at the 18-character floor: users are hidden and the header says so.
        var narrow = new TestConsole(); narrow.Profile.Width = 200;
        narrow.Write(DashboardView.Render(state, TimeZoneInfo.Utc, new ScreenSize(76, 200)));
        Assert.DoesNotContain("TOP USERS", narrow.Output);
        Assert.Contains("· users hidden", narrow.Output);
        Assert.DoesNotContain("alice", narrow.Output);
    }

    [Fact]
    public void Omits_top_users_section_for_legacy_data()
    {
        // Report() builds stats via the Stats() helper with no `users` arg, i.e. an empty TopUsers list —
        // matching legacy (pre-identity) data.
        var o = Render(cursor: 0, focus: null);
        Assert.DoesNotContain("TOP USERS", o);
    }

    private static RunStatusReport ReportWith(int rules, int? total, int? h0, int? h1)
    {
        var s = Stats(0, 0, 0, Array.Empty<(string, int)>(), Array.Empty<(string, int)>());
        var hosts = new[]
        {
            new HostSummary("A", Now, 0, 1, s, HostLiveness.Live, h0),
            new HostSummary("B", Now, 0, 1, s, HostLiveness.Live, h1),
        };
        return new RunStatusReport("r", RunState.Running, Start, Stop, TimeSpan.Zero, Now, hosts, s, "Merged", rules, total);
    }

    private static string RenderReport(RunStatusReport r)
    {
        var console = new TestConsole();
        console.Write(DashboardView.Render(new DashboardViewState(r, 0, null, Now, null, null), TimeZoneInfo.Utc, new ScreenSize(76, 200)));
        return console.Output;
    }

    [Fact]
    public void Exclusions_row_shows_rules_and_dropped_total_and_hosts_show_their_counts()
    {
        var o = RenderReport(ReportWith(rules: 3, total: 1204, h0: 1200, h1: 4));
        Assert.Contains("Exclusions", o);
        Assert.Contains("3 rules", o);
        Assert.Contains($"{1204:n0} dropped", o);
        Assert.Contains("excl", o);
        Assert.Contains(1200.ToString("n0"), o);
    }

    [Fact]
    public void Missing_counts_render_as_na_never_zero()
    {
        var o = RenderReport(ReportWith(rules: 2, total: null, h0: null, h1: null));
        Assert.Contains("n/a dropped", o);
        Assert.DoesNotContain("0 dropped", o);
        Assert.Equal(2, o.Split("n/a").Length - 1 - 1);   // two host rows show n/a (plus the summary's one)
    }

    [Fact]
    public void Row_is_hidden_for_runs_without_rules_or_counts()
    {
        Assert.DoesNotContain("Exclusions", RenderReport(ReportWith(rules: 0, total: null, h0: null, h1: null)));
        Assert.Contains("Exclusions", RenderReport(ReportWith(rules: 0, total: 5, h0: 5, h1: null)));   // collector reports, no rules
    }
}
