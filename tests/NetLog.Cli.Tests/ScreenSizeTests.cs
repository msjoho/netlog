using NetLog.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class ScreenSizeTests
{
    [Fact]
    public void Layout_of_console_uses_its_width_and_height_with_a_floor_of_76_columns()
    {
        var console = new TestConsole();
        console.Profile.Width = 120; console.Profile.Height = 30;
        Assert.Equal(new ScreenSize(120, 30), ScreenSize.Of(console));

        console.Profile.Width = 60;
        Assert.Equal(76, ScreenSize.Of(console).Width);
    }

    [Fact]
    public void Unbounded_layout_keeps_the_width_but_never_crops_by_height()
    {
        var console = new TestConsole();
        console.Profile.Width = 100;
        Assert.Equal(new ScreenSize(100, int.MaxValue), ScreenSize.Unbounded(console));
    }

    [Fact]
    public void Rule_spans_the_given_width()
    {
        var console = new TestConsole();
        console.Profile.Width = 120;
        console.Write(Ui.Rule(100));
        Assert.Equal(new string('─', 100), console.Output.TrimEnd());
    }

    [Fact]
    public void Spread_right_aligns_the_second_part_to_the_width_ignoring_markup_tags()
    {
        var line = Ui.Spread("[green]► netlog[/]   run", "[gray]1 run · root[/]", 40);
        Assert.Equal(40, Markup.Remove(line).Length);
        Assert.EndsWith("1 run · root", Markup.Remove(line));
        Assert.StartsWith("► netlog   run", Markup.Remove(line));
    }

    [Fact]
    public void Spread_falls_back_to_a_three_space_gap_when_both_parts_do_not_fit()
    {
        var line = Ui.Spread("left part", "right part", 12);
        Assert.Equal("left part   right part", Markup.Remove(line));
    }

    [Fact]
    public void HeadTrim_keeps_the_tail_and_marks_the_cut()
    {
        Assert.Equal(@"…\_temp\results", Ui.HeadTrim(@"C:\code\claudecode\network-activity-logger\_temp\results", 15));
        Assert.Equal(@"C:\short", Ui.HeadTrim(@"C:\short", 15));
    }

    [Fact]
    public void Fit_pads_short_text_and_cuts_long_text_with_an_ellipsis()
    {
        Assert.Equal("abc   ", Ui.Fit("abc", 6));
        Assert.Equal("abcde…", Ui.Fit("abcdefgh", 6));
    }

    [Fact]
    public void FitLabel_keeps_the_ip_of_a_host_ip_label_and_shortens_the_host()
    {
        Assert.Equal("fsweuhashared.trd.a… (10.84.4.4)", Ui.FitLabel("fsweuhashared.trd.axusr.net (10.84.4.4)", 32));
        Assert.Equal("10.84.4.4   ", Ui.FitLabel("10.84.4.4", 12));
    }

    [Fact]
    public void Footer_wraps_only_between_hotkey_groups()
    {
        var groups = new[] { "[↑↓] host", "[enter] focus", "[a] all", "[s] deploy", "[x] destroy", "[n] new window", "[e] edit", "[r] refresh", "[q] back" }
            .Select(Markup.Escape).ToList();
        var lines = Ui.FooterLines(groups, 80);
        Assert.Equal(2, lines.Count);
        Assert.Equal("[↑↓] host  [enter] focus  [a] all  [s] deploy  [x] destroy  [n] new window", Markup.Remove(lines[0]));
        Assert.Equal("[e] edit  [r] refresh  [q] back", Markup.Remove(lines[1]));
        Assert.Single(Ui.FooterLines(groups, 120));
    }

    [Fact]
    public void Wrap_breaks_plain_text_at_word_boundaries_and_indents_continuations()
    {
        var lines = Ui.Wrap("i  Rows matching any rule are never written. process: name or 'svchost (Service)' · user: DOMAIN\\name · host: the remote peer.", 80);
        Assert.Equal(2, lines.Count);
        Assert.Equal("i  Rows matching any rule are never written. process: name or 'svchost", lines[0]);
        Assert.Equal("   (Service)' · user: DOMAIN\\name · host: the remote peer.", lines[1]);
        Assert.All(lines, l => Assert.True(l.Length <= 80));
    }
}
