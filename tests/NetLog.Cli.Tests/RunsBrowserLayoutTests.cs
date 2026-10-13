using NetLog.Cli.Rendering;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>The runs browser under different console sizes: one-line header, the list as a viewport.</summary>
public class RunsBrowserLayoutTests
{
    private const string Root = @"C:\code\claudecode\network-activity-logger\_temp\results";
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static List<RunListing> Runs(int count, string? firstName = null) => Enumerable.Range(0, count)
        .Select(i => new RunListing(i == 0 && firstName is not null ? firstName : $"run{i:00}", Now.AddDays(-i - 1), Now.AddDays(-i), 2)).ToList();

    private static List<string> Lines(int runs, ScreenSize size, int cursor = 0, string? firstName = null)
    {
        var console = new TestConsole();
        console.Profile.Width = Math.Max(size.Width, 200);
        console.Write(RunsBrowserView.Render(new RunsBrowserViewState(Root, Runs(runs, firstName), cursor, Now), TimeZoneInfo.Utc, size));
        var lines = console.Output.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    [Theory]
    [InlineData(76, 24)] [InlineData(80, 24)] [InlineData(120, 30)]
    public void Lines_never_exceed_the_frame(int width, int height)
    {
        var lines = Lines(30, new ScreenSize(width, height));
        Assert.All(lines, l => Assert.True(l.Length <= width, $"{l.Length} > {width}: {l}"));
        Assert.True(lines.Count <= height, $"{lines.Count} lines > {height}");
    }

    [Fact]
    public void Header_right_aligns_the_run_count_and_cuts_the_path_head_when_narrow()
    {
        var wide = Lines(1, new ScreenSize(120, 30))[0];
        Assert.StartsWith("► netlog   " + Root, wide);
        Assert.EndsWith("1 run · root", wide);
        Assert.Equal(120, wide.Length);

        var narrow = Lines(1, new ScreenSize(76, 24))[0];
        Assert.StartsWith("► netlog   …", narrow);
        Assert.EndsWith(@"\_temp\results   1 run · root", narrow);
        Assert.Equal(76, narrow.Length);
    }

    [Fact]
    public void Columns_are_content_sized_with_four_space_gaps()
    {
        var lines = Lines(2, new ScreenSize(120, 30));
        Assert.Equal("  RUN      STATE          WINDOW                         HOSTS", lines[2]);
        Assert.Equal("► run00    √ Completed    Jun 14 12:00 → Jun 15 12:00        2", lines[3]);
        Assert.Equal("  run01    √ Completed    Jun 13 12:00 → Jun 14 12:00        2", lines[4]);
    }

    [Fact]
    public void Gap_shrinks_to_one_space_and_then_the_name_is_cut_when_the_frame_is_tight()
    {
        var name = "audit-2026-q3-branch-offices-and-datacentre-west";   // 48 chars: even 1-space gaps need 93 columns
        var lines = Lines(2, new ScreenSize(76, 24), firstName: name);
        Assert.All(lines, l => Assert.True(l.Length <= 76, $"{l.Length} > 76: {l}"));
        var row = lines[3];
        Assert.StartsWith("► audit-2026-q3-", row);
        Assert.Contains("… √ Completed Jun 14 12:00 → Jun 15 12:00     2", row);   // HOSTS column stays 5 wide
        Assert.Equal(76, row.Length);
    }

    [Fact]
    public void List_is_a_viewport_and_the_footer_stays_visible()
    {
        var lines = Lines(30, new ScreenSize(80, 24));
        Assert.Equal("[r] change root  [q] quit", lines[^1]);
        Assert.Contains("  ▼ 15 more", lines);                 // 24 - 8 chrome rows = 16: 15 runs + marker
        Assert.Contains(lines, l => l.StartsWith("► run00"));
        Assert.DoesNotContain(lines, l => l.Contains("run15"));
    }

    [Fact]
    public void Viewport_follows_the_cursor()
    {
        var lines = Lines(30, new ScreenSize(80, 24), cursor: 29);
        Assert.Contains("  ▲ 15 more", lines);
        Assert.Contains(lines, l => l.StartsWith("► run29"));
    }
}
