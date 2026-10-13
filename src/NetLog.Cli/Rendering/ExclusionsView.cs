using NetLog.Cli.Tui;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Pure renderer for the exclusions list editor (wizard / root / edit-during-run share it).
/// The list is a viewport that takes the rows the chrome leaves; the note column takes the width that
/// remains after the fixed columns and is cut with an ellipsis.</summary>
public static class ExclusionsView
{
    private const int KindW = 9, PatternW = 28, DirW = 6, PortW = 12;
    private const int NoteCol = 2 + KindW + PatternW + 2 + DirW + PortW + 2;   // column the note starts in

    private const string ListHint = "i  Rows matching any rule are never written. process: name or 'svchost (Service)' · user: DOMAIN\\name · host: the remote peer.";
    private const string AddHint = "i  kind:pattern[@in|@out][:port] · * and ? wildcards · host matches name, IP or CIDR · port 443, 80,443 or 1024-65535";

    public static IRenderable Render(ExclusionsState s, string title, string context, string? warning, ScreenSize size)
    {
        var w = size.Width;
        var left = $"[{Theme.Green}]► netlog[/]   [{Theme.Gray}]{Markup.Escape(title)}[/]";
        var ctx = Ui.HeadTrim(context, Math.Max(8, w - Markup.Remove(left).Length - 3));
        var rows = new List<IRenderable> { new Markup(Ui.Spread(left, $"[{Theme.Gray}]{Markup.Escape(ctx)}[/]", w)), Ui.Rule(w) };
        if (warning is not null)
        {
            rows.Add(new Markup($"[{Theme.Amber}]!  {Markup.Escape(warning)}[/]"));
            rows.Add(Ui.Rule(w));
        }

        var tail = Tail(s, w).ToList();
        var chrome = rows.Count + tail.Count;
        var avail = size.Height == int.MaxValue ? int.MaxValue / 2 : Math.Max(1, size.Height - chrome);

        if (s.Rules.Count == 0)
            rows.Add(new Markup($"  [{Theme.Dim}]no exclusions — every connection is logged[/]"));

        var win = Viewport.Of(s.Rules.Count, s.Cursor, avail);
        if (win.Above > 0) rows.Add(new Markup($"  [{Theme.Amber}]▲ {win.Above} more[/]"));
        for (int i = win.Start; i < win.Start + win.Count; i++)
        {
            var r = s.Rules[i];
            var focused = i == s.Cursor && !s.Adding;
            var cursor = focused ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            var main = focused ? Theme.White : Theme.Text;
            var note = r.Note is null ? "" : $"  [{Theme.Dim}]{Markup.Escape(Ui.Fit(r.Note, Math.Max(1, w - NoteCol)).TrimEnd())}[/]";
            var port = r.Port is null ? "" : ":" + r.Port;
            rows.Add(new Markup(cursor +
                $"[{Theme.Gray}]{Markup.Escape(r.Kind.PadRight(KindW))}[/]" +
                $"[{main}]{Markup.Escape(Ui.Fit(r.Pattern, PatternW))}[/]  " +
                $"[{Theme.Text}]{Markup.Escape(r.Direction.PadRight(DirW))}[/]" +
                $"[{Theme.Text}]{Markup.Escape(Ui.Fit(port, PortW))}[/]" + note));
        }
        if (win.Below > 0) rows.Add(new Markup($"  [{Theme.Amber}]▼ {win.Below} more[/]"));

        rows.AddRange(tail);
        return new Rows(rows);
    }

    /// <summary>Everything under the list: rule, add line or hint, rule, footer — fixed chrome.</summary>
    private static IEnumerable<IRenderable> Tail(ExclusionsState s, int w)
    {
        yield return Ui.Rule(w);
        if (s.Adding)
        {
            yield return new Markup($"  [{Theme.Gray}]add  [/]{Theme.Caret_(s.Buffer, s.Caret)}");
            if (s.Error is null)
                foreach (var line in Ui.Wrap(AddHint, w)) yield return new Markup($"[{Theme.Hint}]{Markup.Escape(line)}[/]");
            else
                yield return new Markup($"[{Theme.Red}]{Markup.Escape(s.Error)}[/]");
        }
        else
        {
            foreach (var line in Ui.Wrap(ListHint, w)) yield return new Markup($"[{Theme.Hint}]{Markup.Escape(line)}[/]");
        }
        yield return Ui.Rule(w);
        foreach (var line in Ui.FooterLines(Footer(s), w)) yield return new Markup(line);
    }

    private static IEnumerable<string> Footer(ExclusionsState s)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        return s.Adding
            ? new[] { k("enter", "add rule"), k("esc", "close line") }
            : new[] { k("↑↓", "rule"), k("a", "add"), k("d", "remove"), k("enter", "done"), k("esc", "discard") };
    }
}
