namespace NetLog.Cli.Tui;

public enum ScheduleCommand { None, Submit, Cancel }

/// <summary>The "new collection window" schedule as a tiny two-field form (Start, End), each with a
/// Tab flip. Mirrors the wizard's Start/End behaviour so the dashboard's [n] new-window flow stays
/// in the themed TUI instead of dropping to console prompts.</summary>
public sealed record ScheduleForm(
    int FieldIndex, bool StartNow, string StartDate, bool EndDuration, string Duration, string EndDate, int Caret = 0)
{
    public const int FieldCount = 2;
    public static ScheduleForm Default() => new(0, StartNow: true, StartDate: "", EndDuration: true, Duration: "7d", EndDate: "", Caret: 2);
    public bool StartFocused => FieldIndex == 0;

    /// <summary>The focused field's editable text, or null while Start is "now".</summary>
    public string? Active => StartFocused ? (StartNow ? null : StartDate) : (EndDuration ? Duration : EndDate);
}

public static class ScheduleReducer
{
    public static (ScheduleForm Form, ScheduleCommand Cmd) Reduce(ScheduleForm f, KeyPress k)
    {
        switch (k.Kind)
        {
            case KeyKind.Up:    return (AtEnd(f with { FieldIndex = Math.Max(0, f.FieldIndex - 1) }), ScheduleCommand.None);
            case KeyKind.Down:  return (AtEnd(f with { FieldIndex = Math.Min(ScheduleForm.FieldCount - 1, f.FieldIndex + 1) }), ScheduleCommand.None);
            case KeyKind.Enter: return (f, ScheduleCommand.Submit);
            case KeyKind.Esc:   return (f, ScheduleCommand.Cancel);
            case KeyKind.Tab:   return (AtEnd(Flip(f)), ScheduleCommand.None);
        }
        if (f.Active is { } text && TextEdit.Apply(text, f.Caret, k) is { } e)
            return (WithActive(f, e.Text) with { Caret = e.Caret }, ScheduleCommand.None);
        return (f, ScheduleCommand.None);
    }

    private static ScheduleForm Flip(ScheduleForm f) =>
        f.StartFocused ? f with { StartNow = !f.StartNow } : f with { EndDuration = !f.EndDuration };

    private static ScheduleForm AtEnd(ScheduleForm f) => f with { Caret = f.Active?.Length ?? 0 };

    private static ScheduleForm WithActive(ScheduleForm f, string v)
    {
        if (f.StartFocused) return f with { StartDate = v };
        return f.EndDuration ? f with { Duration = v } : f with { EndDate = v };
    }

    public static (string Start, string? Stop, string? Duration) ToSchedule(ScheduleForm f) => (
        Start: f.StartNow ? "now" : f.StartDate,
        Stop: f.EndDuration ? null : f.EndDate,
        Duration: f.EndDuration ? f.Duration : null);
}
