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

public static class RunsBrowserView
{
    public static IRenderable Render(RunsBrowserViewState s, TimeZoneInfo tz)
    {
        var runs = s.Runs;
        var header = $"[{Theme.Green}]► netlog[/]   [{Theme.White}]{Markup.Escape(s.RootPath)}[/]   " +
                     $"[{Theme.Gray}]{runs.Count} run{(runs.Count == 1 ? "" : "s")} · root[/]";

        var rows = new List<IRenderable> { new Markup(header), RuleLine() };

        if (runs.Count == 0)
        {
            rows.Add(new Markup($"[{Theme.Gray}]No runs found in this root yet.[/]"));
            rows.Add(new Markup($"[{Theme.Dim}]Create your first run to deploy collectors and start logging.[/]"));
            rows.Add(RuleLine());
            rows.Add(new Markup(Footer(empty: true)));
            return new Rows(rows);
        }

        var grid = new Grid();
        grid.AddColumn(); grid.AddColumn(); grid.AddColumn();
        grid.AddColumn(new GridColumn().RightAligned());
        grid.AddRow($"[{Theme.Gray}]RUN[/]", $"[{Theme.Gray}]STATE[/]", $"[{Theme.Gray}]WINDOW[/]", $"[{Theme.Gray}]HOSTS[/]");

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var cursor = i == s.CursorIndex ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            var window = $"[{Theme.Text}]{TimeFormat.LocalShort(run.StartUtc, tz)} → {TimeFormat.LocalShort(run.StopUtc, tz)}[/]";
            grid.AddRow(
                cursor + $"[{Theme.White}]{Markup.Escape(run.FolderName)}[/]",
                Theme.WindowStateMarkup(run.StartUtc, run.StopUtc, s.Now),
                window,
                $"[{Theme.Text}]{run.HostCount}[/]");
        }

        rows.Add(grid);
        rows.Add(RuleLine());
        rows.Add(new Markup($"[{Theme.Hint}]i  Folder name is the run name. State here is from the schedule window only.[/]"));
        rows.Add(RuleLine());
        rows.Add(new Markup(Footer(empty: false)));
        return new Rows(rows);
    }

    private static string Footer(bool empty)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        return empty
            ? string.Join("  ", k("c", "create the first run"), k("r", "change root"), k("q", "quit"))
            : string.Join("  ", k("↑↓", "move"), k("enter", "open run"), k("c", "new run"), k("r", "change root"), k("q", "quit"));
    }

    private static IRenderable RuleLine() => Ui.Rule();
}
