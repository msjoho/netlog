using NetLog.Cli.Tui;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Inputs for the runs-browser frame. State is window-derived at render time (no host scan).</summary>
public sealed record RunsBrowserViewState(
    string RootPath,
    IReadOnlyList<RunListing> Runs,
    int CursorIndex,
    DateTimeOffset Now);

/// <summary>The runs browser: a one-line header (root path, run count right-aligned), the run list as a
/// viewport that takes the rows the chrome leaves, and the footer always visible.</summary>
public static class RunsBrowserView
{
    private const string Hint = "i  Folder name is the run name. State here is from the schedule window only.";
    private const int ColumnGap = 4;

    public static IRenderable Render(RunsBrowserViewState s, TimeZoneInfo tz, ScreenSize size)
    {
        var w = size.Width;
        var runs = s.Runs;
        var right = $"[{Theme.Gray}]{runs.Count} run{(runs.Count == 1 ? "" : "s")} · root[/]";
        var path = Ui.HeadTrim(s.RootPath, Math.Max(8, w - "► netlog   ".Length - 3 - Markup.Remove(right).Length));
        var header = Ui.Spread($"[{Theme.Green}]► netlog[/]   [{Theme.White}]{Markup.Escape(path)}[/]", right, w);

        var rows = new List<IRenderable> { new Markup(header), Ui.Rule(w) };

        if (runs.Count == 0)
        {
            rows.Add(new Markup($"[{Theme.Gray}]No runs found in this root yet.[/]"));
            rows.Add(new Markup($"[{Theme.Dim}]Create your first run to deploy collectors and start logging.[/]"));
            rows.Add(Ui.Rule(w));
            rows.AddRange(Ui.FooterLines(Footer(empty: true), w).Select(l => new Markup(l)));
            return new Rows(rows);
        }

        var tail = new List<IRenderable> { Ui.Rule(w) };
        tail.AddRange(Ui.Wrap(Hint, w).Select(l => new Markup($"[{Theme.Hint}]{Markup.Escape(l)}[/]")));
        tail.Add(Ui.Rule(w));
        tail.AddRange(Ui.FooterLines(Footer(empty: false), w).Select(l => new Markup(l)));

        var chrome = rows.Count + 1 + tail.Count;                      // + the column header
        var avail = size.Height == int.MaxValue ? int.MaxValue / 2 : Math.Max(1, size.Height - chrome);
        var win = Viewport.Of(runs.Count, s.CursorIndex, avail);

        // Content-sized columns with a 4-space gap that shrinks to 1 before the run name is cut (as the hosts table).
        var cells = runs.Select(r => (
            Name: r.FolderName,
            State: Theme.WindowStateMarkup(r.StartUtc, r.StopUtc, s.Now),
            Window: $"{TimeFormat.LocalShort(r.StartUtc, tz)} → {TimeFormat.LocalShort(r.StopUtc, tz)}",
            Hosts: r.HostCount.ToString())).ToList();
        var nameW = Math.Max("RUN".Length, cells.Max(c => c.Name.Length));
        var stateW = Math.Max("STATE".Length, cells.Max(c => Markup.Remove(c.State).Length));
        var winW = Math.Max("WINDOW".Length, cells.Max(c => c.Window.Length));
        var hostsW = Math.Max("HOSTS".Length, cells.Max(c => c.Hosts.Length));
        int Total(int gap, int nw) => 2 + nw + gap + stateW + gap + winW + gap + hostsW;
        var g = ColumnGap;
        while (Total(g, nameW) > w && g > 1) g--;
        if (Total(g, nameW) > w) nameW = Math.Max(8, nameW - (Total(g, nameW) - w));
        var sp = new string(' ', g);

        rows.Add(new Markup($"  [{Theme.Gray}]{"RUN".PadRight(nameW)}{sp}{"STATE".PadRight(stateW)}{sp}{"WINDOW".PadRight(winW)}{sp}{"HOSTS".PadLeft(hostsW)}[/]"));
        if (win.Above > 0) rows.Add(new Markup($"  [{Theme.Amber}]▲ {win.Above} more[/]"));
        for (int i = win.Start; i < win.Start + win.Count; i++)
        {
            var c = cells[i];
            var cursor = i == s.CursorIndex ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            var statePad = new string(' ', stateW - Markup.Remove(c.State).Length);
            rows.Add(new Markup(cursor +
                $"[{Theme.White}]{Markup.Escape(Ui.Fit(c.Name, nameW))}[/]" + sp +
                c.State + statePad + sp +
                $"[{Theme.Text}]{Markup.Escape(c.Window.PadRight(winW))}[/]" + sp +
                $"[{Theme.Text}]{c.Hosts.PadLeft(hostsW)}[/]"));
        }
        if (win.Below > 0) rows.Add(new Markup($"  [{Theme.Amber}]▼ {win.Below} more[/]"));

        rows.AddRange(tail);
        return new Rows(rows);
    }

    private static IEnumerable<string> Footer(bool empty)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        return empty
            ? new[] { k("c", "create the first run"), k("x", "exclusions"), k("r", "change root"), k("q", "quit") }
            : new[] { k("↑↓", "move"), k("enter", "open run"), k("c", "new run"), k("d", "delete"), k("x", "exclusions"), k("r", "change root"), k("q", "quit") };
    }
}
