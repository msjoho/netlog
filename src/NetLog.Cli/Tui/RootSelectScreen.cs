using NetLog.Cli.Rendering;
using NetLog.Core.Io;
using Spectre.Console;

namespace NetLog.Cli.Tui;

/// <summary>Screen 0 — choose the root folder that holds the runs. Returns the chosen root, or null
/// if the user quit.</summary>
public static class RootSelectScreen
{
    public static string? Run(IAnsiConsole console, IKeySource keys, ProfileStore profile)
    {
        var roots = profile.Load().RecentRootPaths;
        var nav = new RootNav(0, Editing: false, Buffer: "");
        string? result = null;

        ScreenHost.Run(console,
            () => RootSelectView.Render(new RootSelectViewState(roots, nav.Cursor, nav.Editing, nav.Buffer)),
            () =>
            {
                if (keys.Read(0) is not { } key) return false;
                var (next, cmd) = RootReducer.Reduce(nav, roots.Count, key);
                nav = next;
                switch (cmd)
                {
                    case RootCommand.OpenRoot when roots.Count > 0:
                        result = roots[Math.Clamp(nav.Cursor, 0, roots.Count - 1)];
                        return true;
                    case RootCommand.SubmitNewRoot:
                        var p = nav.Buffer.Trim();
                        if (p.Length > 0) { result = p; return true; }
                        nav = nav with { Editing = false, Buffer = "" };
                        return false;
                    case RootCommand.Quit:
                        return true;
                    default:
                        return false;
                }
            });

        return result;
    }
}
