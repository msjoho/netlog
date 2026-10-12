using NetLog.Cli.Rendering;
using NetLog.Cli.Tui;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class ExclusionsViewTests
{
    private static string Render(ExclusionsState s, string? warning = null)
    {
        var console = new TestConsole();
        console.Write(ExclusionsView.Render(s, "exclusions · new run", @"\\srv\netlog", warning));
        return console.Output;
    }

    private static ExclusionsState State() => ExclusionsState.From(new[]
    {
        new ExclusionRule("process", "svchost (Dnscache)", "both", "DNS client"),
        new ExclusionRule("host", "10.20.0.0/16", "out"),
        new ExclusionRule("process", "System", "in", "smb", "139,445"),
    });

    [Fact]
    public void Shows_the_port_column_and_mentions_it_in_the_syntax_hint()
    {
        var o = Render(State());
        Assert.Contains(":139,445", o);
        var adding = Render(State() with { Adding = true, Buffer = "", Caret = 0 });
        Assert.Contains("[@in|@out][:port]", adding);
    }

    [Fact]
    public void Lists_kind_pattern_direction_note_title_and_context()
    {
        var o = Render(State());
        Assert.Contains("exclusions · new run", o);
        Assert.Contains(@"\\srv\netlog", o);
        Assert.Contains("process", o);
        Assert.Contains("svchost (Dnscache)", o);
        Assert.Contains("DNS client", o);
        Assert.Contains("10.20.0.0/16", o);
        Assert.Contains("out", o);
        Assert.Contains("[a] add", o.Replace("\u001b", ""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_list_says_so()
    {
        Assert.Contains("no exclusions", Render(ExclusionsState.From(Array.Empty<ExclusionRule>())), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Add_line_shows_buffer_syntax_hint_and_error()
    {
        var s = State() with { Adding = true, Buffer = "host:nas01*", Caret = 11 };
        var o = Render(s);
        Assert.Contains("add", o);
        Assert.Contains("host:nas01*", o);
        Assert.Contains("kind:pattern[@in|@out]", o);
        Assert.Contains("Unknown kind 'port'", Render(s with { Error = "Unknown kind 'port' (use process, user or host)." }));
    }

    [Fact]
    public void Warning_is_shown_when_given()
    {
        Assert.Contains("purge matching rows", Render(State(), "changes purge matching rows from the share within one archive cycle"));
        Assert.DoesNotContain("purge", Render(State()));
    }
}
