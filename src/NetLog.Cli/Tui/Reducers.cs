namespace NetLog.Cli.Tui;

// ── Dashboard ────────────────────────────────────────────────────────────────
public enum DashboardCommand { None, Start, Stop, NewWindow, Edit, Merge, Refresh, Back }
public sealed record DashboardNav(int Cursor, int? Focus);

public static class DashboardReducer
{
    public static (DashboardNav Nav, DashboardCommand Cmd) Reduce(DashboardNav nav, int hostCount, KeyPress k)
    {
        switch (k.Kind)
        {
            case KeyKind.Up:    return (nav with { Cursor = Math.Max(0, nav.Cursor - 1) }, DashboardCommand.None);
            case KeyKind.Down:  return (nav with { Cursor = Math.Min(Math.Max(0, hostCount - 1), nav.Cursor + 1) }, DashboardCommand.None);
            // Enter on the focused host unfocuses back to All; on any other host it (re)focuses
            // that host directly — no unfocus/refocus round-trip when switching hosts.
            case KeyKind.Enter: return (nav.Focus == nav.Cursor ? nav with { Focus = null }
                                        : hostCount > 0 ? nav with { Focus = nav.Cursor } : nav, DashboardCommand.None);
            case KeyKind.Esc:   return (nav with { Focus = null }, DashboardCommand.None);
        }
        if (k.Is('a')) return (nav with { Focus = null }, DashboardCommand.None);
        if (k.Is('s')) return (nav, DashboardCommand.Start);
        if (k.Is('x')) return (nav, DashboardCommand.Stop);
        if (k.Is('n')) return (nav, DashboardCommand.NewWindow);
        if (k.Is('e')) return (nav, DashboardCommand.Edit);
        if (k.Is('m')) return (nav, DashboardCommand.Merge);
        if (k.Is('r')) return (nav, DashboardCommand.Refresh);
        if (k.Is('q')) return (nav, DashboardCommand.Back);
        return (nav, DashboardCommand.None);
    }
}

// ── Runs browser ─────────────────────────────────────────────────────────────
public enum BrowserCommand { None, OpenRun, NewRun, ChangeRoot, Quit, Exclusions, DeleteRun }

public static class BrowserReducer
{
    public static (int Cursor, BrowserCommand Cmd) Reduce(int cursor, int runCount, KeyPress k)
    {
        switch (k.Kind)
        {
            case KeyKind.Up:    return (Math.Max(0, cursor - 1), BrowserCommand.None);
            case KeyKind.Down:  return (Math.Min(Math.Max(0, runCount - 1), cursor + 1), BrowserCommand.None);
            case KeyKind.Enter: return (cursor, runCount > 0 ? BrowserCommand.OpenRun : BrowserCommand.None);
        }
        if (k.Is('c')) return (cursor, BrowserCommand.NewRun);
        if (k.Is('r')) return (cursor, BrowserCommand.ChangeRoot);
        if (k.Is('x')) return (cursor, BrowserCommand.Exclusions);
        if (k.Is('d')) return (cursor, runCount > 0 ? BrowserCommand.DeleteRun : BrowserCommand.None);
        if (k.Is('q')) return (cursor, BrowserCommand.Quit);
        return (cursor, BrowserCommand.None);
    }
}

// ── Root select ──────────────────────────────────────────────────────────────
public enum RootCommand { None, OpenRoot, SubmitNewRoot, Quit }
public sealed record RootNav(int Cursor, bool Editing, string Buffer, int Caret = 0);

public static class RootReducer
{
    public static (RootNav Nav, RootCommand Cmd) Reduce(RootNav nav, int rootCount, KeyPress k)
    {
        if (nav.Editing)
        {
            if (TextEdit.Apply(nav.Buffer, nav.Caret, k) is { } e)
                return (nav with { Buffer = e.Text, Caret = e.Caret }, RootCommand.None);
            switch (k.Kind)
            {
                case KeyKind.Enter:     return (nav, RootCommand.SubmitNewRoot);
                case KeyKind.Esc:       return (nav with { Editing = false, Buffer = "", Caret = 0 }, RootCommand.None);
                default:                return (nav, RootCommand.None);
            }
        }

        switch (k.Kind)
        {
            case KeyKind.Up:    return (nav with { Cursor = Math.Max(0, nav.Cursor - 1) }, RootCommand.None);
            case KeyKind.Down:  return (nav with { Cursor = Math.Min(Math.Max(0, rootCount - 1), nav.Cursor + 1) }, RootCommand.None);
            case KeyKind.Enter: return (nav, rootCount > 0 ? RootCommand.OpenRoot : RootCommand.None);
        }
        if (k.Is('r')) return (nav with { Editing = true, Buffer = "", Caret = 0 }, RootCommand.None);
        if (k.Is('q')) return (nav, RootCommand.Quit);
        return (nav, RootCommand.None);
    }
}
