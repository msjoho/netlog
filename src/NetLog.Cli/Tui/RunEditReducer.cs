using NetLog.Core.Models;

namespace NetLog.Cli.Tui;

public enum RunEditField { End, Poll, ArchiveEvery, Cache }
public enum RunEditCommand { None, Save, Cancel }

/// <summary>The in-run edit form — only the values that can change during a run (the end, plus poll /
/// archive-interval / cache-lifetime). All four fields are text-editable with a caret; the End field
/// also flips between a duration and an explicit date. Immutable fields are shown by the view but not
/// represented here.</summary>
public sealed record RunEditForm(
    int FieldIndex, bool EndDuration, string Duration, string EndDate, string Poll, string ArchiveEvery, string Cache, int Caret)
{
    public const int FieldCount = 4;
    public RunEditField Field => (RunEditField)FieldIndex;

    public static RunEditForm From(RunConfig c)
    {
        var endDate = c.Schedule.StopUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return new RunEditForm(
            FieldIndex: 0, EndDuration: false, Duration: "", EndDate: endDate,
            Poll: c.Settings.PollIntervalSeconds.ToString(),
            ArchiveEvery: c.Settings.ArchiveIntervalMinutes.ToString(),
            Cache: c.Settings.CacheLifetimeDays?.ToString() ?? "",
            Caret: endDate.Length);
    }

    public string Active => Field switch
    {
        RunEditField.End => EndDuration ? Duration : EndDate,
        RunEditField.Poll => Poll,
        RunEditField.ArchiveEvery => ArchiveEvery,
        RunEditField.Cache => Cache,
        _ => "",
    };

    public RunEditForm WithActive(string v) => Field switch
    {
        RunEditField.End => EndDuration ? this with { Duration = v } : this with { EndDate = v },
        RunEditField.Poll => this with { Poll = v },
        RunEditField.ArchiveEvery => this with { ArchiveEvery = v },
        RunEditField.Cache => this with { Cache = v },
        _ => this,
    };
}

public static class RunEditReducer
{
    public static (RunEditForm Form, RunEditCommand Cmd) Reduce(RunEditForm f, KeyPress k)
    {
        switch (k.Kind)
        {
            case KeyKind.Up:    return (Focus(f, Math.Max(0, f.FieldIndex - 1)), RunEditCommand.None);
            case KeyKind.Down:  return (Focus(f, Math.Min(RunEditForm.FieldCount - 1, f.FieldIndex + 1)), RunEditCommand.None);
            case KeyKind.Left:  return (f with { Caret = Math.Max(0, f.Caret - 1) }, RunEditCommand.None);
            case KeyKind.Right: return (f with { Caret = Math.Min(f.Active.Length, f.Caret + 1) }, RunEditCommand.None);
            case KeyKind.Enter: return (f, RunEditCommand.Save);
            case KeyKind.Esc:   return (f, RunEditCommand.Cancel);
            case KeyKind.Tab:   return (f.Field == RunEditField.End ? AtEnd(f with { EndDuration = !f.EndDuration }) : f, RunEditCommand.None);
            case KeyKind.Char:      return (Insert(f, k.Char), RunEditCommand.None);
            case KeyKind.Backspace: return (DeleteBack(f), RunEditCommand.None);
            case KeyKind.Delete:    return (DeleteAt(f), RunEditCommand.None);
            default:            return (f, RunEditCommand.None);
        }
    }

    /// <summary>Maps the form to <see cref="NetLog.Core.RunSpec.RunConfigFactory.WithEdits"/> arguments.</summary>
    public static (string? Stop, string? Duration, int Poll, int Archive, int? Cache) ToArgs(RunEditForm f) => (
        Stop: f.EndDuration ? null : f.EndDate,
        Duration: f.EndDuration ? f.Duration : null,
        Poll: int.TryParse(f.Poll, out var p) ? p : 10,
        Archive: int.TryParse(f.ArchiveEvery, out var a) ? a : 60,
        Cache: int.TryParse(f.Cache, out var c) ? c : null);

    private static RunEditForm Focus(RunEditForm f, int index) => AtEnd(f with { FieldIndex = index });
    private static RunEditForm AtEnd(RunEditForm f) => f with { Caret = f.Active.Length };

    private static RunEditForm Insert(RunEditForm f, char c)
    {
        var s = f.Active;
        var caret = Math.Clamp(f.Caret, 0, s.Length);
        return f.WithActive(s[..caret] + c + s[caret..]) with { Caret = caret + 1 };
    }

    private static RunEditForm DeleteBack(RunEditForm f)
    {
        var s = f.Active;
        var caret = Math.Clamp(f.Caret, 0, s.Length);
        if (caret == 0) return f;
        return f.WithActive(s[..(caret - 1)] + s[caret..]) with { Caret = caret - 1 };
    }

    private static RunEditForm DeleteAt(RunEditForm f)
    {
        var s = f.Active;
        var caret = Math.Clamp(f.Caret, 0, s.Length);
        if (caret >= s.Length) return f;
        return f.WithActive(s[..caret] + s[(caret + 1)..]);
    }
}
