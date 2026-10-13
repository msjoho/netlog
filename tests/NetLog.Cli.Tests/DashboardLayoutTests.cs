using NetLog.Cli.Rendering;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>The dashboard under different console sizes: frame width, right-aligned title and scope,
/// content-sized hosts table, three top panels, long labels, and the height policy.</summary>
public class DashboardLayoutTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Stop  = new(2026, 6, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now   = Start.AddDays(2);

    private const string LongLabel = "fsweuhashared.trd.axusr.net (10.84.4.4)";
    private const string VeryLongLabel = "a104-85-2-183.deploy.static.akamaitechnologies.com (104.85.2.183)";

    private static RunStats Stats(bool longLabels, bool users)
    {
        var targets = Enumerable.Range(0, 10).Select(i => new TopItem(
            longLabels ? (i == 0 ? LongLabel : i == 1 ? VeryLongLabel : $"10.84.4.{i}") : $"10.84.4.{i}", 20 - i, "out")).ToList();
        var procs = Enumerable.Range(0, 10).Select(i => new TopItem(i == 3 ? "MetricsExtension.Native" : $"proc{i}", 20 - i, "out")).ToList();
        var top = users
            ? Enumerable.Range(0, 10).Select(i => new TopItem(i == 0 ? @"AXUSR\svc-arp-np-refresh" : $@"AXUSR\user{i}", 20 - i, "out")).ToList()
            : new List<TopItem>();
        return new RunStats(70, 8, 62, targets, procs, top);
    }

    private static RunStatusReport Report(int hostCount = 2, bool users = false, bool longLabels = false, string? firstHost = null)
    {
        var stats = Stats(longLabels, users);
        var hosts = Enumerable.Range(0, hostCount).Select(i => new HostSummary(
            i == 0 && firstHost is not null ? firstHost : i == 0 ? "SQL-PRD-01" : i == 1 ? "WEB-PRD-02" : $"su195{i:0000}",
            Now.AddSeconds(-8), 50 + i, 4103, stats, i == 1 ? HostLiveness.Stalled : HostLiveness.Live)).ToList();
        return new RunStatusReport("audit-2026Q2", RunState.Running, Start, Stop, TimeSpan.FromDays(2), Now.AddSeconds(-8), hosts, stats);
    }

    private static List<string> Lines(RunStatusReport r, ScreenSize size, int cursor = 0)
    {
        var console = new TestConsole();
        console.Profile.Width = Math.Max(size.Width, 200);
        var state = new DashboardViewState(r, cursor, null, Now, SecondsToRefresh: 4, StaleFor: null);
        console.Write(DashboardView.Render(state, TimeZoneInfo.Utc, size));
        return console.Output.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).ToList();
    }

    [Theory]
    [InlineData(76, 24)] [InlineData(80, 24)] [InlineData(120, 30)] [InlineData(160, 50)]
    public void Lines_never_exceed_the_frame(int width, int height)
    {
        var lines = Lines(Report(hostCount: 12, users: true, longLabels: true), new ScreenSize(width, height));
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        Assert.All(lines, l => Assert.True(l.Length <= width, $"{l.Length} > {width}: {l}"));
        Assert.True(lines.Count <= height, $"{lines.Count} lines > {height}");
    }

    [Fact]
    public void Rules_span_the_frame_width()
    {
        var lines = Lines(Report(), new ScreenSize(120, 30));
        Assert.Equal(new string('─', 120), lines[1]);
    }

    [Fact]
    public void Title_puts_elapsed_and_refresh_timer_at_the_right_edge()
    {
        var title = Lines(Report(), new ScreenSize(120, 30))[0];
        Assert.StartsWith("► netlog   audit-2026Q2    ● Running", title);
        Assert.EndsWith("2d 0h 00m elapsed    ○ 4s ●", title);
        Assert.Equal(120, title.Length);
    }

    [Fact]
    public void Scope_tag_sits_at_the_right_edge_of_the_window_line()
    {
        var window = Lines(Report(), new ScreenSize(120, 30))[2];
        Assert.StartsWith("  Window       2026-06-12 08:00 → 2026-06-19 08:00", window);
        Assert.EndsWith("[ All hosts ]", window);
        Assert.Equal(120, window.Length);
    }

    [Fact]
    public void Hosts_columns_are_content_sized_with_four_space_gaps()
    {
        var lines = Lines(Report(), new ScreenSize(120, 30));
        var polls = 4103.ToString("n0");
        // name 10 + gap 4 + liveness 9 ("◌ stalled") + gap 4, then the right-aligned number columns
        Assert.Equal("  HOSTS                      conns    polls    excl    last heartbeat", lines[6]);
        Assert.Equal($"► SQL-PRD-01    ● live          50    {polls}     n/a    8s ago", lines[7]);
        Assert.Equal($"  WEB-PRD-02    ◌ stalled       51    {polls}     n/a    8s ago", lines[8]);
    }

    [Fact]
    public void Hosts_gap_shrinks_one_space_at_a_time_when_the_frame_is_tight()
    {
        var lines = Lines(Report(firstHost: "fsweuhashared.trd.axusr.net"), new ScreenSize(76, 30));
        Assert.Contains("► fsweuhashared.trd.axusr.net  ● live", lines[7]);   // 4 → 3 → 2 spaces
        Assert.True(lines[7].Length <= 76);
    }

    [Fact]
    public void Top_users_join_the_row_as_a_third_panel()
    {
        var lines = Lines(Report(users: true), new ScreenSize(120, 30));
        var head = lines.Single(l => l.Contains("TOP TARGETS"));
        Assert.Contains("TOP PROCESSES", head);
        Assert.Contains("TOP USERS", head);
        Assert.Contains(@"AXUSR\svc-arp-np-refresh", string.Join("\n", lines));
    }

    [Fact]
    public void Top_users_are_hidden_with_a_tag_when_three_panels_cannot_fit()
    {
        var lines = Lines(Report(users: true), new ScreenSize(80, 30));
        var head = lines.Single(l => l.Contains("TOP TARGETS"));
        Assert.DoesNotContain("TOP USERS", head);
        Assert.EndsWith("· users hidden", head);
        Assert.Equal(80, head.Length);
        Assert.DoesNotContain(@"AXUSR\svc-arp-np-refresh", string.Join("\n", lines));
    }

    [Fact]
    public void Long_target_labels_are_shown_in_full_when_the_frame_allows()
    {
        var o = string.Join("\n", Lines(Report(users: true, longLabels: true), new ScreenSize(160, 50)));
        Assert.Contains(VeryLongLabel, o);
        Assert.Contains("MetricsExtension.Native", o);
    }

    [Fact]
    public void Long_target_labels_are_cut_keeping_the_ip_when_the_frame_is_narrow()
    {
        var o = string.Join("\n", Lines(Report(users: true, longLabels: true), new ScreenSize(120, 30)));
        Assert.DoesNotContain(VeryLongLabel, o);
        Assert.Contains("… (104.85.2.183)", o);
        Assert.Contains("… (10.84.4.4)", o);
    }

    [Fact]
    public void Host_list_scrolls_with_a_marker_when_the_window_is_low()
    {
        var lines = Lines(Report(hostCount: 12), new ScreenSize(120, 30));
        Assert.Contains("  ▼ 4 more", lines);                       // 19 rows left: 10 top rows + 8 hosts + marker
        Assert.Contains(lines, l => l.StartsWith("  out 10.84.4.9"));  // the 10th top row is still there
    }

    [Fact]
    public void Top_lists_shrink_before_the_host_list_in_a_low_window()
    {
        var lines = Lines(Report(hostCount: 12), new ScreenSize(80, 24));
        Assert.Contains("  ▼ 10 more", lines);                      // 2 hosts + marker = the 3-row floor
        Assert.DoesNotContain(lines, l => l.StartsWith("  out 10.84.4.9"));
        Assert.Contains(lines, l => l.StartsWith("  ▼") && l.Contains("more") && l != "  ▼ 10 more");
    }

    [Fact]
    public void Footer_wraps_between_hotkeys_in_a_narrow_frame()
    {
        var lines = Lines(Report(), new ScreenSize(80, 24));
        Assert.Contains("[e] edit  [r] refresh  [q] back", lines);
        Assert.Contains("[↑↓] host  [enter] focus  [a] all  [s] deploy  [x] destroy  [n] new window", lines);
    }

    [Fact]
    public void Unbounded_height_never_crops_the_host_list()
    {
        var lines = Lines(Report(hostCount: 12), new ScreenSize(80, int.MaxValue));
        Assert.Contains(lines, l => l.StartsWith("  su1950011"));
        Assert.DoesNotContain(lines, l => l.Contains("more"));
    }
}
