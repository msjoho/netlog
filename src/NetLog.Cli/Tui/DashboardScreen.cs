using System.Text.Json;
using NetLog.Cli.Rendering;
using NetLog.Core.Analysis;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Screen 2 — the live, self-refreshing run dashboard. Re-reads the run folder every
/// <paramref name="refreshSeconds"/> and on demand; survives share outages by keeping the last good
/// frame and flagging it stale. Modal actions (deploy/destroy/new window) run outside the live frame.
/// Returns when the user goes back.</summary>
public static class DashboardScreen
{
    public static void Run(IAnsiConsole console, IKeySource keys, string runPath,
        RunActions actions, Func<DateTimeOffset> now, int refreshSeconds = 5)
    {
        var tz = TimeZoneInfo.Local;
        var nav = new DashboardNav(0, null);
        RunStatusReport? report = null;
        DateTimeOffset lastGood = now();
        TimeSpan? staleFor = null;
        int countdown = refreshSeconds;

        void Load()
        {
            try
            {
                report = ReportBuilder.Build(RunReader.Read(runPath), now());
                staleFor = null;
                lastGood = now();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                if (report is not null) staleFor = now() - lastGood;   // keep the last good frame
            }
        }

        Load();
        while (true)
        {
            var pending = DashboardCommand.None;

            ScreenHost.Run(console,
                () =>
                {
                    if (report is null) return Fallback(runPath);
                    nav = nav with { Cursor = Math.Clamp(nav.Cursor, 0, Math.Max(0, report.Hosts.Count - 1)) };
                    return DashboardView.Render(
                        new DashboardViewState(report, nav.Cursor, nav.Focus, now(), countdown, staleFor), tz);
                },
                () =>
                {
                    if (keys.Read(1000) is not { } key)
                    {
                        if (--countdown <= 0) { Load(); countdown = refreshSeconds; }
                        return false;
                    }
                    var (next, cmd) = DashboardReducer.Reduce(nav, report?.Hosts.Count ?? 0, key);
                    nav = next;
                    switch (cmd)
                    {
                        case DashboardCommand.Refresh: Load(); countdown = refreshSeconds; return false;
                        case DashboardCommand.None:    return false;
                        default:                       pending = cmd; return true;   // Start/Stop/NewWindow/Back
                    }
                });

            switch (pending)
            {
                case DashboardCommand.Back:      return;
                case DashboardCommand.Start:     actions.Deploy(runPath); break;
                case DashboardCommand.Stop:      actions.Teardown(runPath); break;
                case DashboardCommand.NewWindow: actions.NewWindow(runPath); break;
                case DashboardCommand.Edit:      actions.Edit(runPath); break;
                case DashboardCommand.Merge:     actions.Merge(runPath); break;
            }
            Load();
            countdown = refreshSeconds;
        }
    }

    private static IRenderable Fallback(string runPath) => new Markup(
        $"[{Theme.Red}]Could not read run at[/] {Markup.Escape(runPath)}\n\n" +
        $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]r[/][{Theme.Gray}]{Markup.Escape("]")} retry   [/]" +
        $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]q[/][{Theme.Gray}]{Markup.Escape("]")} back[/]");
}
