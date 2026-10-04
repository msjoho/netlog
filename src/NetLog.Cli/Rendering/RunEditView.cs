using NetLog.Cli.Tui;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Renders the in-run edit form: immutable fields shown greyed (value visible, not editable),
/// the four mutable fields editable with a caret. Pure function of the existing config + form.</summary>
public static class RunEditView
{
    public static IRenderable Render(RunConfig c, RunEditForm f, TimeZoneInfo tz)
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[{Theme.Green}]► netlog[/]   [{Theme.Gray}]edit run[/]                           [{Theme.Gray}]{Markup.Escape(c.RunName)}[/]"),
            Ui.Rule(),
            Locked("Run name", c.RunName),
            Locked("Targets", string.Join(", ", c.Targets)),
            Locked("Service account", c.ServiceAccount),
            new Markup(""),
            Locked("Start", $"{TimeFormat.Local(c.Schedule.StartUtc, tz)} {TimeFormat.OffsetLabel(tz, c.Schedule.StartUtc)}"),
            Editable(f, RunEditField.End, "End", EndPrefix(f), EndEditable(f), "", EndRight(f)),
            Locked("Archive level", c.Settings.ArchiveLevel),
            Editable(f, RunEditField.Poll, "Poll interval", "", f.Poll, "s"),
            Editable(f, RunEditField.ArchiveEvery, "Archive every", "", f.ArchiveEvery, "m"),
            Editable(f, RunEditField.Cache, "Cache lifetime", "", f.Cache, "d"),
            Ui.Rule(),
            new Markup($"[{Theme.Hint}]i  Only the highlighted fields can change during a run; grey values are fixed.[/]"),
            Ui.Rule(),
            new Markup(Footer(f)),
        };
        return new Rows(rows);
    }

    private static IRenderable Locked(string label, string value) =>
        new Markup($"  [{Theme.Dim}]{label,-15}[/] [{Theme.Dim}]{Markup.Escape(value)}[/]");

    private static IRenderable Editable(RunEditForm f, RunEditField field, string label, string prefix, string editable, string suffix, string? right = null)
    {
        var focused = f.Field == field;
        var cursor = focused ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
        var lab = $"[{Theme.Gray}]{label,-15}[/]";
        var value = focused
            ? $"[{Theme.White}]{Markup.Escape(prefix)}[/]{Theme.Caret_(editable, f.Caret)}[{Theme.White}]{Markup.Escape(suffix)}[/]"
            : $"[{Theme.Text}]{Markup.Escape(prefix + editable + suffix)}[/]";
        var line = $"{cursor}{lab} {value}";
        if (focused && right is not null) line += $"                      [{Theme.Dim}]{Markup.Escape(right)}[/]";
        return new Markup(line);
    }

    private static string EndPrefix(RunEditForm f) => f.EndDuration ? "Duration: " : "End date: ";
    private static string EndEditable(RunEditForm f) => f.EndDuration ? f.Duration : f.EndDate;
    private static string? EndRight(RunEditForm f) =>
        f.Field == RunEditField.End && f.EndDuration ? "d days · h hours · m minutes" : null;

    private static string Footer(RunEditForm f)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        var parts = new List<string> { k("↑↓", "field") };
        if (f.Field == RunEditField.End) parts.Add(k("tab", f.EndDuration ? "switch to date" : "switch to duration"));
        parts.Add(k("enter", "save"));
        parts.Add(k("esc", "cancel"));
        return string.Join("  ", parts);
    }
}
