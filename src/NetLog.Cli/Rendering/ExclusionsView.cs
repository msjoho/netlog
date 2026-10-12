using NetLog.Cli.Tui;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Pure renderer for the exclusions list editor (wizard / root / edit-during-run share it).</summary>
public static class ExclusionsView
{
    private const int KindW = 9, PatternW = 28, DirW = 6, PortW = 12;

    public static IRenderable Render(ExclusionsState s, string title, string context, string? warning)
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[{Theme.Green}]► netlog[/]   [{Theme.Gray}]{Markup.Escape(title)}[/]{Pad(title)}[{Theme.Gray}]{Markup.Escape(context)}[/]"),
            Ui.Rule(),
        };
        if (warning is not null)
        {
            rows.Add(new Markup($"[{Theme.Amber}]!  {Markup.Escape(warning)}[/]"));
            rows.Add(Ui.Rule());
        }

        if (s.Rules.Count == 0)
            rows.Add(new Markup($"  [{Theme.Dim}]no exclusions — every connection is logged[/]"));
        for (int i = 0; i < s.Rules.Count; i++)
        {
            var r = s.Rules[i];
            var focused = i == s.Cursor && !s.Adding;
            var cursor = focused ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            var main = focused ? Theme.White : Theme.Text;
            var note = r.Note is null ? "" : $"  [{Theme.Dim}]{Markup.Escape(r.Note)}[/]";
            var port = r.Port is null ? "" : ":" + r.Port;
            rows.Add(new Markup(cursor +
                $"[{Theme.Gray}]{Markup.Escape(r.Kind.PadRight(KindW))}[/]" +
                $"[{main}]{Markup.Escape(Fit(r.Pattern, PatternW))}[/]  " +
                $"[{Theme.Text}]{Markup.Escape(r.Direction.PadRight(DirW))}[/]" +
                $"[{Theme.Text}]{Markup.Escape(Fit(port, PortW))}[/]" + note));
        }

        rows.Add(Ui.Rule());
        if (s.Adding)
        {
            rows.Add(new Markup($"  [{Theme.Gray}]add  [/]{Theme.Caret_(s.Buffer, s.Caret)}"));
            rows.Add(s.Error is null
                ? new Markup($"[{Theme.Hint}]i  kind:pattern[[@in|@out]][[:port]] · * and ? wildcards · host matches name, IP or CIDR · port 443, 80,443 or 1024-65535[/]")
                : new Markup($"[{Theme.Red}]{Markup.Escape(s.Error)}[/]"));
        }
        else
        {
            rows.Add(new Markup($"[{Theme.Hint}]i  Rows matching any rule are never written. process: name or 'svchost (Service)' · user: DOMAIN\\name · host: the remote peer.[/]"));
        }
        rows.Add(Ui.Rule());
        rows.Add(new Markup(Footer(s)));
        return new Rows(rows);
    }

    private static string Footer(ExclusionsState s)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        return s.Adding
            ? string.Join("  ", k("enter", "add rule"), k("esc", "close line"))
            : string.Join("  ", k("↑↓", "rule"), k("a", "add"), k("d", "remove"), k("enter", "done"), k("esc", "discard"));
    }

    private static string Fit(string text, int width) =>
        text.Length <= width ? text.PadRight(width) : text[..(width - 1)] + "…";

    private static string Pad(string title) => new string(' ', Math.Max(2, 40 - title.Length));
}
