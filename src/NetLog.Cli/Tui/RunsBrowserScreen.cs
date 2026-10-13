using NetLog.Cli.Rendering;
using NetLog.Core.Io;
using Spectre.Console;

namespace NetLog.Cli.Tui;

public enum BrowserResult { Quit, ChangeRoot, OpenRun, NewRun, Exclusions, DeleteRun }

/// <summary>Screen 1 — browse the runs (subfolders) under a root. Re-lists each frame so a freshly
/// created run shows up. Returns what the user chose to do next (and the run folder for OpenRun).</summary>
public static class RunsBrowserScreen
{
    public static (BrowserResult Result, string? RunFolder) Run(
        IAnsiConsole console, IKeySource keys, string root, Func<DateTimeOffset> now)
    {
        int cursor = 0;
        var result = BrowserResult.Quit;
        string? folder = null;

        ScreenHost.Run(console,
            () =>
            {
                var runs = RunCatalog.List(root);
                cursor = Math.Clamp(cursor, 0, Math.Max(0, runs.Count - 1));
                return RunsBrowserView.Render(new RunsBrowserViewState(root, runs, cursor, now()), TimeZoneInfo.Local, ScreenSize.Of(console));
            },
            () =>
            {
                if (keys.Read(ScreenHost.PollMs) is not { } key) return false;
                var runs = RunCatalog.List(root);
                var (nextCursor, cmd) = BrowserReducer.Reduce(cursor, runs.Count, key);
                cursor = nextCursor;
                switch (cmd)
                {
                    case BrowserCommand.OpenRun when runs.Count > 0:
                        result = BrowserResult.OpenRun; folder = runs[Math.Clamp(cursor, 0, runs.Count - 1)].FolderName; return true;
                    case BrowserCommand.DeleteRun when runs.Count > 0:
                        result = BrowserResult.DeleteRun; folder = runs[Math.Clamp(cursor, 0, runs.Count - 1)].FolderName; return true;
                    case BrowserCommand.NewRun:
                        result = BrowserResult.NewRun; return true;
                    case BrowserCommand.ChangeRoot:
                        result = BrowserResult.ChangeRoot; return true;
                    case BrowserCommand.Exclusions:
                        result = BrowserResult.Exclusions; return true;
                    case BrowserCommand.Quit:
                        result = BrowserResult.Quit; return true;
                    default:
                        return false;
                }
            });

        return (result, folder);
    }
}
