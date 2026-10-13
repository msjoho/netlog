using NetLog.Cli.Rendering;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class RootSelectViewTests
{
    private static string Render(RootSelectViewState state, bool ansi = false)
    {
        var console = new TestConsole();
        if (ansi) console.EmitAnsiSequences();
        console.Write(RootSelectView.Render(state));
        return console.Output;
    }

    // The reverse-video caret block: Theme.Bg on Theme.Green (#93C572 = 147,197,114).
    private const string GreenBackground = "48;2;147;197;114";

    [Fact]
    public void Editing_mode_renders_a_block_caret_at_the_caret_index()
    {
        var o = Render(new RootSelectViewState(Array.Empty<string>(), 0, Editing: true, Buffer: "abc", Caret: 1), ansi: true);
        Assert.Contains(GreenBackground, o);
        Assert.DoesNotContain("|", o);
        Assert.Contains("abc", Render(new RootSelectViewState(Array.Empty<string>(), 0, Editing: true, Buffer: "abc", Caret: 1)));
    }

    [Fact]
    public void Lists_recent_roots()
    {
        var o = Render(new RootSelectViewState(
            new[] { @"\\tdfs\InfraOps\netlog", @"\\fileserver\audits" }, 0, Editing: false, Buffer: ""));
        Assert.Contains(@"\\tdfs\InfraOps\netlog", o);
        Assert.Contains(@"\\fileserver\audits", o);
    }

    [Fact]
    public void Editing_mode_shows_buffer_and_share_hint()
    {
        var o = Render(new RootSelectViewState(
            Array.Empty<string>(), 0, Editing: true, Buffer: @"\\corp\ops\netlog"));
        Assert.Contains(@"\\corp\ops\netlog", o);
        Assert.Contains("share", o, StringComparison.OrdinalIgnoreCase);     // SMB-access hint
        Assert.Contains("modify access", o, StringComparison.OrdinalIgnoreCase);
    }
}
