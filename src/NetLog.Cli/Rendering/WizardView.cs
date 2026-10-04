using NetLog.Cli.Tui;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Pure renderer for the new-run wizard form. Highlights the focused field with a movable
/// block caret, shows a context hint line that changes per field, and a footer whose [tab] action
/// matches the focused field.</summary>
public static class WizardView
{
    public static IRenderable Render(WizardForm f, string rootPath, string currentUser)
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[{Theme.Green}]► netlog[/]   [{Theme.Gray}]new run[/]                            [{Theme.Gray}]{Markup.Escape(rootPath)}[/]"),
            RuleLine(),
            FieldRow(f, WizardField.RunName,      "Run name",        "", f.RunName,      ""),
            FieldRow(f, WizardField.Targets,      "Targets",         "", f.Targets,      ""),
            FieldRow(f, WizardField.Account,      "Service account", "", f.Account,      ""),
            new Markup(""),
            FieldRow(f, WizardField.Start,        "Start",           StartPrefix(f), StartEditable(f), "", StartEditableMode(f)),
            FieldRow(f, WizardField.End,          "End",             EndPrefix(f),   EndEditable(f),   "", true, EndRight(f)),
            FieldRow(f, WizardField.ArchiveLevel, "Archive level",   f.ArchiveLevel, "", "", textEditable: false),
            FieldRow(f, WizardField.Poll,         "Poll interval",   "", f.Poll,         "s"),
            FieldRow(f, WizardField.ArchiveEvery, "Archive every",   "", f.ArchiveEvery, "m"),
            FieldRow(f, WizardField.Cache,        "Cache lifetime",  "", f.Cache,        "d"),
            FieldRow(f, WizardField.Timestamps,   "Timestamps",      f.Timestamps ? "on" : "off", "", "", textEditable: false),
            RuleLine(),
            new Markup($"[{Theme.Gray}]Creates  [/][{Theme.Dim}]{Markup.Escape(rootPath)}\\[/][{Theme.White}]{Markup.Escape(Display(f.RunName, "<name>"))}[/]"),
            new Markup($"[{Theme.Hint}]i  {Hint(f.Field)}[/]"),
            RuleLine(),
            new Markup(Footer(f, currentUser)),
        };
        return new Rows(rows);
    }

    private static IRenderable FieldRow(WizardForm f, WizardField field, string label, string prefix, string editable,
        string suffix, bool textEditable = true, string? right = null)
    {
        var focused = f.Field == field;
        var cursor = focused ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
        var lab = $"[{Theme.Gray}]{label,-15}[/]";

        string value;
        if (focused && textEditable)
            value = $"[{Theme.White}]{Markup.Escape(prefix)}[/]{Theme.Caret_(editable, f.Caret)}[{Theme.White}]{Markup.Escape(suffix)}[/]";
        else
        {
            var color = focused ? Theme.White : Theme.Text;
            value = $"[{color}]{Markup.Escape(prefix + editable + suffix)}[/]";
        }

        var line = $"{cursor}{lab} {value}";
        if (focused && right is not null) line += $"                      [{Theme.Dim}]{Markup.Escape(right)}[/]";
        return new Markup(line);
    }

    // Start/End are two-mode fields: a prefix label plus an editable string (or a fixed "now").
    private static string StartPrefix(WizardForm f) => f.StartNow ? "" : "Date: ";
    private static string StartEditable(WizardForm f) => f.StartNow ? "now" : f.StartDate;
    private static bool StartEditableMode(WizardForm f) => !f.StartNow;

    private static string EndPrefix(WizardForm f) => f.EndDuration ? "Duration: " : "End date: ";
    private static string EndEditable(WizardForm f) => f.EndDuration ? f.Duration : f.EndDate;
    private static string? EndRight(WizardForm f) =>
        f.Field == WizardField.End && f.EndDuration ? "d days · h hours · m minutes" : null;

    private static string Hint(WizardField field) => field switch
    {
        WizardField.RunName      => "Becomes the run's folder name — keep it short and filesystem-safe.",
        WizardField.Targets      => "One or more host names, comma-separated:  SQL-PRD-01, WEB-PRD-02",
        WizardField.Account      => "Runs the scheduled task on each host; needs share modify access. Password still required at deploy.",
        WizardField.Start        => "When collection begins.",
        WizardField.End          => "When collection ends.",
        WizardField.ArchiveLevel => "Merged drops the initiator's ephemeral port and collapses; Full keeps every unique row.",
        WizardField.Poll         => "How often each host samples its TCP connections (seconds).",
        WizardField.ArchiveEvery => "How often samples are flushed to the share (minutes).",
        WizardField.Cache        => "Local cache lifetime on each host (days).",
        WizardField.Timestamps   => "Record first/last-seen instants per connection (off keeps the archive time-free).",
        _ => "",
    };

    private static string Footer(WizardForm f, string currentUser)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";

        string? tab = f.Field switch
        {
            WizardField.Account      => $"use {currentUser} (current user)",
            WizardField.Start        => f.StartNow ? "switch to date" : "switch to now",
            WizardField.End          => f.EndDuration ? "switch to date" : "switch to duration",
            WizardField.ArchiveLevel => f.ArchiveLevel == "Merged" ? "switch to Full" : "switch to Merged",
            WizardField.Timestamps   => f.Timestamps ? "switch off" : "switch on",
            _ => null,
        };

        var parts = new List<string> { k("↑↓", "field") };
        if (tab is not null) parts.Add(k("tab", tab));
        parts.Add(k("enter", "create & deploy"));
        parts.Add(k("esc", "cancel"));
        return string.Join("  ", parts);
    }

    private static string Display(string value, string placeholder) =>
        string.IsNullOrWhiteSpace(value) ? placeholder : value;

    private static IRenderable RuleLine() => Ui.Rule();
}
