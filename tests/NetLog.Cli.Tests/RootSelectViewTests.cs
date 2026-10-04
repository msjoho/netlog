using NetLog.Cli.Rendering;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class RootSelectViewTests
{
    private static string Render(RootSelectViewState state)
    {
        var console = new TestConsole();
        console.Write(RootSelectView.Render(state));
        return console.Output;
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
