using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Inputs for the root-select frame. In Editing mode the user is typing a new root path.</summary>
public sealed record RootSelectViewState(
    IReadOnlyList<string> RecentRoots,
    int CursorIndex,
    bool Editing,
    string Buffer,
    int Caret = 0);

public static class RootSelectView
{
    public static IRenderable Render(RootSelectViewState s)
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[{Theme.Green}]► netlog[/]                                                [{Theme.Gray}]select a root folder[/]"),
            RuleLine(),
            new Markup($"[{Theme.Gray}]RECENT ROOTS[/]"),
        };

        if (s.RecentRoots.Count == 0)
            rows.Add(new Markup($"[{Theme.Dim}]none yet[/]"));

        for (int i = 0; i < s.RecentRoots.Count; i++)
        {
            var cursor = !s.Editing && i == s.CursorIndex ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            rows.Add(new Markup(cursor + $"[{Theme.Text}]{Markup.Escape(s.RecentRoots[i])}[/]"));
        }

        rows.Add(RuleLine());

        if (s.Editing)
        {
            rows.Add(new Markup($"[{Theme.Green}]New root path[/]  [{Theme.White}]{Theme.Caret_(s.Buffer, s.Caret)}[/]"));
            rows.Add(new Markup($"[{Theme.Hint}]i  This SMB share must be reachable from every target host, with modify access[/]"));
            rows.Add(new Markup($"[{Theme.Hint}]   for the configured service account.[/]"));
            rows.Add(RuleLine());
            rows.Add(new Markup(Footer(editing: true)));
        }
        else
        {
            rows.Add(new Markup($"[{Theme.Hint}]i  A run is any subfolder here that holds a run.json.[/]"));
            rows.Add(RuleLine());
            rows.Add(new Markup(Footer(editing: false)));
        }

        return new Rows(rows);
    }

    private static string Footer(bool editing)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        return editing
            ? string.Join("  ", k("enter", "open"), k("esc", "cancel"))
            : string.Join("  ", k("↑↓", "move"), k("enter", "open root"), k("r", "new root…"), k("q", "quit"));
    }

    private static IRenderable RuleLine() => Ui.Rule();
}
