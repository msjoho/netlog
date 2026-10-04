using NetLog.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Themed, flicker-free modal screens used by the dashboard's actions — replacing the old
/// console prompts so the whole flow stays in the TUI.</summary>
public static class NewWindowScreen
{
    public static (string Start, string? Stop, string? Duration)? Run(IAnsiConsole console, IKeySource keys, string runName)
    {
        var form = ScheduleForm.Default();
        (string Start, string? Stop, string? Duration)? result = null;

        ScreenHost.Run(console,
            () => Render(form, runName),
            () =>
            {
                if (keys.Read(0) is not { } key) return false;
                var (next, cmd) = ScheduleReducer.Reduce(form, key);
                form = next;
                if (cmd == ScheduleCommand.Submit) { result = ScheduleReducer.ToSchedule(form); return true; }
                return cmd == ScheduleCommand.Cancel;
            });

        return result;
    }

    private static IRenderable Render(ScheduleForm f, string runName)
    {
        string field(int idx, string label, string value, string? right)
        {
            var focused = f.FieldIndex == idx;
            var cursor = focused ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            var val = focused ? $"[{Theme.White}]{Markup.Escape(value)}[/]" : $"[{Theme.Text}]{Markup.Escape(value)}[/]";
            var line = $"{cursor}[{Theme.Gray}]{label,-8}[/] {val}";
            if (focused && right is not null) line += $"            [{Theme.Dim}]{Markup.Escape(right)}[/]";
            return line;
        }

        var startVal = f.StartNow ? "now" : "Date: " + Disp(f.StartDate);
        var endVal = f.EndDuration ? "Duration: " + Disp(f.Duration, "7d") : "End date: " + Disp(f.EndDate);
        var endRight = f.FieldIndex == 1 && f.EndDuration ? "d days · h hours · m minutes" : null;

        var tab = f.StartFocused
            ? (f.StartNow ? "switch to date" : "switch to now")
            : (f.EndDuration ? "switch to date" : "switch to duration");

        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";

        return new Rows(
            new Markup($"[{Theme.Green}]► netlog[/]   [{Theme.Gray}]new window[/]                       [{Theme.Gray}]{Markup.Escape(runName)}[/]"),
            Ui.Rule(),
            new Markup($"[{Theme.Hint}]i  Targets and settings are kept; connections accumulate in the same run.[/]"),
            new Markup(""),
            new Markup(field(0, "Start", startVal, f.StartFocused ? tab.Replace("switch", "→") : null)),
            new Markup(field(1, "End", endVal, endRight)),
            Ui.Rule(),
            new Markup(string.Join("  ", k("↑↓", "field"), k("tab", tab), k("enter", "set window"), k("esc", "cancel"))));
    }

    private static string Disp(string v, string ph = "pick a date") => string.IsNullOrWhiteSpace(v) ? $"<{ph}>" : v;
}

/// <summary>Masked password entry (no echo of the actual characters). Returns null on cancel.</summary>
public static class PasswordScreen
{
    public static string? Run(IAnsiConsole console, IKeySource keys, string account)
    {
        var buffer = "";
        string? result = null;

        ScreenHost.Run(console,
            () => new Rows(
                new Markup($"[{Theme.Green}]► netlog[/]   [{Theme.Gray}]deploy[/]"),
                Ui.Rule(),
                new Markup($"[{Theme.Gray}]Password for[/] [{Theme.White}]{Markup.Escape(account)}[/][{Theme.Gray}]:[/]  " +
                           $"[{Theme.White}]{new string('•', buffer.Length)}[/][{Theme.Green}]|[/]"),
                Ui.Rule(),
                new Markup($"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]enter[/][{Theme.Gray}]{Markup.Escape("]")} deploy   " +
                           $"{Markup.Escape("[")}[/][{Theme.Green}]esc[/][{Theme.Gray}]{Markup.Escape("]")} cancel[/]")),
            () =>
            {
                if (keys.Read(0) is not { } key) return false;
                switch (key.Kind)
                {
                    case KeyKind.Char:      buffer += key.Char; return false;
                    case KeyKind.Backspace: buffer = buffer.Length > 0 ? buffer[..^1] : ""; return false;
                    case KeyKind.Enter:     result = buffer; return true;
                    case KeyKind.Esc:       return true;
                    default:                return false;
                }
            });

        return result;
    }
}

/// <summary>A themed yes/no confirm. Enter or 'y' = yes; 'n' or Esc = no.</summary>
public static class ConfirmScreen
{
    public static bool Run(IAnsiConsole console, IKeySource keys, string question, string? detail = null)
    {
        var answer = false;

        ScreenHost.Run(console,
            () =>
            {
                var rows = new List<IRenderable>
                {
                    new Markup($"[{Theme.White}]{Markup.Escape(question)}[/]"),
                };
                if (detail is not null) rows.Add(new Markup($"[{Theme.Gray}]{Markup.Escape(detail)}[/]"));
                rows.Add(new Markup(""));
                rows.Add(new Markup($"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]enter[/][{Theme.Gray}]{Markup.Escape("]")} / [/]" +
                                    $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]y[/][{Theme.Gray}]{Markup.Escape("]")} yes      [/]" +
                                    $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]n[/][{Theme.Gray}]{Markup.Escape("]")} no[/]"));
                return new Rows(rows);
            },
            () =>
            {
                if (keys.Read(0) is not { } key) return false;
                if (key.Kind == KeyKind.Enter || key.Is('y')) { answer = true; return true; }
                if (key.Kind == KeyKind.Esc || key.Is('n')) { answer = false; return true; }
                return false;
            });

        return answer;
    }
}
