namespace NetLog.Cli.Tui;

public enum ScheduleCommand { None, Submit, Cancel }

/// <summary>The "new collection window" schedule as a tiny two-field form (Start, End), each with a
/// Tab flip. Mirrors the wizard's Start/End behaviour so the dashboard's [n] new-window flow stays
/// in the themed TUI instead of dropping to console prompts.</summary>
public sealed record ScheduleForm(
    int FieldIndex, bool StartNow, string StartDate, bool EndDuration, string Duration, string EndDate)
{
    public const int FieldCount = 2;
    public static ScheduleForm Default() => new(0, StartNow: true, StartDate: "", EndDuration: true, Duration: "7d", EndDate: "");
    public bool StartFocused => FieldIndex == 0;
}

public static class ScheduleReducer
{
    public static (ScheduleForm Form, ScheduleCommand Cmd) Reduce(ScheduleForm f, KeyPress k)
    {
        switch (k.Kind)
        {
            case KeyKind.Up:    return (f with { FieldIndex = Math.Max(0, f.FieldIndex - 1) }, ScheduleCommand.None);
            case KeyKind.Down:  return (f with { FieldIndex = Math.Min(ScheduleForm.FieldCount - 1, f.FieldIndex + 1) }, ScheduleCommand.None);
            case KeyKind.Enter: return (f, ScheduleCommand.Submit);
            case KeyKind.Esc:   return (f, ScheduleCommand.Cancel);
            case KeyKind.Tab:   return (Flip(f), ScheduleCommand.None);
            case KeyKind.Char:      return (Edit(f, s => s + k.Char), ScheduleCommand.None);
            case KeyKind.Backspace: return (Edit(f, s => s.Length > 0 ? s[..^1] : ""), ScheduleCommand.None);
            default:            return (f, ScheduleCommand.None);
        }
    }

    private static ScheduleForm Flip(ScheduleForm f) =>
        f.StartFocused ? f with { StartNow = !f.StartNow } : f with { EndDuration = !f.EndDuration };

    private static ScheduleForm Edit(ScheduleForm f, Func<string, string> t)
    {
        if (f.StartFocused) return f.StartNow ? f : f with { StartDate = t(f.StartDate) };
        return f.EndDuration ? f with { Duration = t(f.Duration) } : f with { EndDate = t(f.EndDate) };
    }

    public static (string Start, string? Stop, string? Duration) ToSchedule(ScheduleForm f) => (
        Start: f.StartNow ? "now" : f.StartDate,
        Stop: f.EndDuration ? null : f.EndDate,
        Duration: f.EndDuration ? f.Duration : null);
}
