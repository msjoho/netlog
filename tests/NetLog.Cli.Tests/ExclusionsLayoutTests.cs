using NetLog.Cli.Rendering;
using NetLog.Cli.Tui;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>The exclusions editor under different console sizes: header on one line, note column,
/// the list as a viewport with the footer always visible.</summary>
public class ExclusionsLayoutTests
{
    private const string Root = @"C:\code\claudecode\network-activity-logger\_temp\results";

    private static ExclusionsState Rules(int count) => ExclusionsState.From(Enumerable.Range(0, count).Select(i => i switch
    {
        0 => new ExclusionRule("process", "svchost (Dnscache)", "both", "DNS client"),
        14 => new ExclusionRule("user", @"AXUSR\svc-arp-np-refresh", "both", "the collector itself"),
        _ => new ExclusionRule("host", $"10.20.{i}.0/24", "out", $"net {i}"),
    }).ToList());

    private static List<string> Lines(ExclusionsState s, ScreenSize size, int cursor = 0)
    {
        var console = new TestConsole();
        console.Profile.Width = Math.Max(size.Width, 200);
        console.Write(ExclusionsView.Render(s with { Cursor = cursor }, "exclusions · root", Root, null, size));
        var lines = console.Output.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    [Theory]
    [InlineData(76, 24)] [InlineData(80, 24)] [InlineData(120, 30)] [InlineData(160, 50)]
    public void Lines_never_exceed_the_frame(int width, int height)
    {
        var lines = Lines(Rules(26), new ScreenSize(width, height));
        Assert.All(lines, l => Assert.True(l.Length <= width, $"{l.Length} > {width}: {l}"));
        Assert.True(lines.Count <= height, $"{lines.Count} lines > {height}");
    }

    [Fact]
    public void Header_keeps_the_context_on_the_title_line_and_cuts_its_head_when_narrow()
    {
        var wide = Lines(Rules(3), new ScreenSize(120, 30))[0];
        Assert.StartsWith("► netlog   exclusions · root", wide);
        Assert.EndsWith(Root, wide);
        Assert.Equal(120, wide.Length);

        var narrow = Lines(Rules(3), new ScreenSize(80, 24))[0];
        Assert.StartsWith("► netlog   exclusions · root   …", narrow);
        Assert.EndsWith(@"\network-activity-logger\_temp\results", narrow);
        Assert.Equal(80, narrow.Length);
    }

    [Fact]
    public void Note_column_takes_the_remaining_width_and_is_cut_with_an_ellipsis()
    {
        Assert.Contains(Lines(Rules(26), new ScreenSize(120, 30)), l => l.EndsWith("the collector itself"));
        Assert.Contains(Lines(Rules(26), new ScreenSize(80, 30)), l => l.EndsWith("the collector itse…"));
    }

    [Fact]
    public void List_is_a_viewport_and_the_footer_stays_visible()
    {
        var lines = Lines(Rules(26), new ScreenSize(80, 24));
        Assert.Equal("[↑↓] rule  [a] add  [d] remove  [enter] done  [esc] discard", lines[^1]);
        Assert.Contains("  ▼ 10 more", lines);                 // 24 - 7 chrome rows = 17: 16 rules + marker
        Assert.Equal(16, lines.Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^(► |  )(process|host|user) ")));
    }

    [Fact]
    public void Viewport_follows_the_cursor()
    {
        var lines = Lines(Rules(26), new ScreenSize(80, 24), cursor: 25);
        Assert.Contains("  ▲ 10 more", lines);
        Assert.Contains(lines, l => l.StartsWith("► host     10.20.25.0/24"));
    }

    [Fact]
    public void Hint_wraps_inside_the_frame()
    {
        var lines = Lines(Rules(3), new ScreenSize(80, 24));
        var hint = lines.FindIndex(l => l.StartsWith("i  Rows matching"));
        Assert.True(hint > 0);
        Assert.StartsWith("   ", lines[hint + 1]);              // continuation indented under the text
        Assert.Single(Lines(Rules(3), new ScreenSize(160, 50)), l => l.StartsWith("i  Rows matching") && l.EndsWith("the remote peer."));
    }
}
