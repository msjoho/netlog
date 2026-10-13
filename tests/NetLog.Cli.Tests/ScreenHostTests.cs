using NetLog.Cli.Rendering;
using NetLog.Cli.Tui;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class ScreenHostTests
{
    private const string ClearSequence = "[2J";   // what TestConsole emits for Clear()

    private static int Clears(TestConsole c) => c.Output.Split(ClearSequence).Length - 1;

    [Fact]
    public void Interactive_host_restarts_the_live_frame_when_the_console_size_changes()
    {
        var console = new TestConsole().Interactive();
        console.Profile.Width = 80; console.Profile.Height = 24;
        var widths = new List<int>();
        var steps = 0;

        ScreenHost.Run(console,
            () => { var w = ScreenSize.Of(console).Width; widths.Add(w); return Ui.Rule(w); },
            () =>
            {
                if (++steps == 1) { console.Profile.Width = 100; return false; }   // a resize between two ticks
                return true;
            });

        Assert.Equal(2, Clears(console));                        // initial clear + one restart
        Assert.Contains(100, widths);
        Assert.Contains(new string('─', 100), console.Output);
    }

    [Fact]
    public void Interactive_host_keeps_one_live_frame_while_the_size_is_stable()
    {
        var console = new TestConsole().Interactive();
        console.Profile.Width = 80; console.Profile.Height = 24;
        var steps = 0;

        ScreenHost.Run(console, () => Ui.Rule(ScreenSize.Of(console).Width), () => ++steps == 3);

        Assert.Equal(1, Clears(console));
    }

    [Fact]
    public void Screens_poll_keys_with_a_timeout_so_a_resize_is_noticed_without_a_keypress()
    {
        Assert.InRange(ScreenHost.PollMs, 100, 1000);
    }
}
