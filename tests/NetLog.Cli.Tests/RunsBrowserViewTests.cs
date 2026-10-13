using NetLog.Cli.Rendering;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class RunsBrowserViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    private static string Render(params RunListing[] runs)
    {
        var console = new TestConsole();
        var state = new RunsBrowserViewState(@"\\tdfs\InfraOps\netlog", runs, 0, Now);
        console.Write(RunsBrowserView.Render(state, TimeZoneInfo.Utc));
        return console.Output;
    }

    [Fact]
    public void Shows_root_path_and_each_run_with_window_state_and_host_count()
    {
        var active    = new RunListing("audit-2026Q2", Now.AddDays(-3), Now.AddDays(4), 4);
        var scheduled = new RunListing("db-prep",      Now.AddDays(5),  Now.AddDays(12), 6);
        var completed = new RunListing("firewall",     Now.AddDays(-30), Now.AddDays(-20), 12);

        var o = Render(active, scheduled, completed);

        Assert.Contains(@"\\tdfs\InfraOps\netlog", o);
        Assert.Contains("audit-2026Q2", o);
        Assert.Contains("Active", o);
        Assert.Contains("Scheduled", o);
        Assert.Contains("Completed", o);
        Assert.Contains("4", o);   // host count
    }

    [Fact]
    public void Empty_root_shows_guidance()
    {
        var o = Render();
        Assert.Contains("No runs", o, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Footer_offers_d_delete_only_when_there_are_runs()
    {
        Assert.DoesNotContain("delete", Render());
        Assert.Contains("delete", Render(new RunListing("audit", Now.AddDays(-1), Now.AddDays(1), 2)));
    }

    [Fact]
    public void Footer_offers_x_exclusions_with_and_without_runs()
    {
        Assert.Contains("exclusions", Render());
        Assert.Contains("exclusions", Render(new RunListing("audit", Now.AddDays(-1), Now.AddDays(1), 2)));
    }
}
