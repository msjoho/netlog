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
    public static IRenderable Render(RootSelectViewState s, ScreenSize size)
    {
        var w = size.Width;
        var rows = new List<IRenderable>
        {
            new Markup(Ui.Spread($"[{Theme.Green}]► netlog[/]", $"[{Theme.Gray}]select a root folder[/]", w)),
            Ui.Rule(w),
            new Markup($"[{Theme.Gray}]RECENT ROOTS[/]"),
        };

        if (s.RecentRoots.Count == 0)
            rows.Add(new Markup($"[{Theme.Dim}]none yet[/]"));

        for (int i = 0; i < s.RecentRoots.Count; i++)
        {
            var cursor = !s.Editing && i == s.CursorIndex ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            rows.Add(new Markup(cursor + $"[{Theme.Text}]{Markup.Escape(Ui.HeadTrim(s.RecentRoots[i], w - 2))}[/]"));
        }

        rows.Add(Ui.Rule(w));

        if (s.Editing)
        {
            rows.Add(new Markup($"[{Theme.Green}]New root path[/]  [{Theme.White}]{Theme.Caret_(s.Buffer, s.Caret)}[/]"));
            rows.Add(new Markup($"[{Theme.Hint}]i  This SMB share must be reachable from every target host, with modify access[/]"));
            rows.Add(new Markup($"[{Theme.Hint}]   for the configured service account.[/]"));
            rows.Add(Ui.Rule(w));
            rows.AddRange(Ui.FooterLines(Footer(editing: true), w).Select(l => new Markup(l)));
        }
        else
        {
            rows.Add(new Markup($"[{Theme.Hint}]i  A run is any subfolder here that holds a run.json.[/]"));
            rows.Add(Ui.Rule(w));
            rows.AddRange(Ui.FooterLines(Footer(editing: false), w).Select(l => new Markup(l)));
        }

        return new Rows(rows);
    }

    private static IEnumerable<string> Footer(bool editing)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        return editing
            ? new[] { k("enter", "open"), k("esc", "cancel") }
            : new[] { k("↑↓", "move"), k("enter", "open root"), k("r", "new root…"), k("q", "quit") };
    }
}
