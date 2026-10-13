using NetLog.Core.Models;

namespace NetLog.Cli.Tui;

public enum WizardField { RunName, Targets, Account, Start, End, ArchiveLevel, Poll, ArchiveEvery, Cache, Timestamps, Exclusions }
public enum WizardCommand { None, Create, Cancel, EditExclusions }

/// <summary>The new-run wizard as one editable form. Pure state; the reducer drives every transition
/// (field nav, caret text edit, Tab flips) so the behaviour is unit-testable without a terminal.
/// <see cref="Caret"/> is the caret index within the focused field's editable string.</summary>
public sealed record WizardForm(
    int FieldIndex,
    string RunName, string Targets, string Account,
    bool StartNow, string StartDate,
    bool EndDuration, string Duration, string EndDate,
    string ArchiveLevel, string Poll, string ArchiveEvery, string Cache,
    int Caret, bool Timestamps = true,
    IReadOnlyList<ExclusionRule>? Exclusions = null, string? RootExclusionsError = null, bool ExclusionsEdited = false)
{
    public const int FieldCount = 11;

    public static WizardForm Default() => new(
        FieldIndex: 0,
        RunName: "", Targets: "", Account: "",
        StartNow: true, StartDate: "",
        EndDuration: true, Duration: "7d", EndDate: "",
        ArchiveLevel: "Merged", Poll: "10", ArchiveEvery: "60", Cache: "7",
        Caret: 0, Timestamps: true);

    public WizardField Field => (WizardField)FieldIndex;

    /// <summary>The focused field's editable string, or null when the field isn't text (Start-while-now,
    /// ArchiveLevel).</summary>
    public string? Active => Field switch
    {
        WizardField.RunName      => RunName,
        WizardField.Targets      => Targets,
        WizardField.Account      => Account,
        WizardField.Start        => StartNow ? null : StartDate,
        WizardField.End          => EndDuration ? Duration : EndDate,
        WizardField.Poll         => Poll,
        WizardField.ArchiveEvery => ArchiveEvery,
        WizardField.Cache        => Cache,
        WizardField.Timestamps   => null,
        WizardField.Exclusions   => null,
        _ => null,
    };

    public WizardForm WithActive(string v) => Field switch
    {
        WizardField.RunName      => this with { RunName = v },
        WizardField.Targets      => this with { Targets = v },
        WizardField.Account      => this with { Account = v },
        WizardField.Start        => this with { StartDate = v },
        WizardField.End          => EndDuration ? this with { Duration = v } : this with { EndDate = v },
        WizardField.Poll         => this with { Poll = v },
        WizardField.ArchiveEvery => this with { ArchiveEvery = v },
        WizardField.Cache        => this with { Cache = v },
        _ => this,
    };
}

public static class WizardReducer
{
    public static (WizardForm Form, WizardCommand Cmd) Reduce(WizardForm f, string currentUser, KeyPress k)
    {
        switch (k.Kind)
        {
            case KeyKind.Up:    return (Focus(f, Math.Max(0, f.FieldIndex - 1)), WizardCommand.None);
            case KeyKind.Down:  return (Focus(f, Math.Min(WizardForm.FieldCount - 1, f.FieldIndex + 1)), WizardCommand.None);
            case KeyKind.Enter: return (f, WizardCommand.Create);
            case KeyKind.Esc:   return (f, WizardCommand.Cancel);
            case KeyKind.Tab:
                if (f.Field == WizardField.Exclusions) return (f, WizardCommand.EditExclusions);
                return (AtEnd(Flip(f, currentUser)), WizardCommand.None);
        }
        // Non-text fields (Start while now, ArchiveLevel, Timestamps, Exclusions) ignore text keys.
        if (f.Active is { } text && TextEdit.Apply(text, f.Caret, k) is { } e)
            return (f.WithActive(e.Text) with { Caret = e.Caret }, WizardCommand.None);
        return (f, WizardCommand.None);
    }

    private static WizardForm Focus(WizardForm f, int index) => AtEnd(f with { FieldIndex = index });

    /// <summary>Put the caret at the end of the focused field's editable value (0 for non-text fields).</summary>
    private static WizardForm AtEnd(WizardForm f) => f with { Caret = f.Active?.Length ?? 0 };

    private static WizardForm Flip(WizardForm f, string currentUser) => f.Field switch
    {
        WizardField.Account      => f with { Account = currentUser },
        WizardField.Start        => f with { StartNow = !f.StartNow },
        WizardField.End          => f with { EndDuration = !f.EndDuration },
        WizardField.ArchiveLevel => f with { ArchiveLevel = f.ArchiveLevel == "Merged" ? "Full" : "Merged" },
        WizardField.Timestamps   => f with { Timestamps = !f.Timestamps },
        _ => f,
    };

    public static WizardAnswers ToAnswers(WizardForm f, string rootPath) => new(
        Path: System.IO.Path.Combine(rootPath, f.RunName.Trim()),
        Targets: f.Targets,
        Account: f.Account,
        Start: f.StartNow ? "now" : f.StartDate,
        Stop: f.EndDuration ? null : f.EndDate,
        Duration: f.EndDuration ? f.Duration : null,
        ArchiveLevel: f.ArchiveLevel,
        PollSeconds: ParseInt(f.Poll, 10),
        ArchiveMinutes: ParseInt(f.ArchiveEvery, 60),
        CacheDays: ParseNullableInt(f.Cache),
        CacheMb: null,
        Timestamps: f.Timestamps,
        Exclusions: f.Exclusions);

    private static int ParseInt(string s, int fallback) => int.TryParse(s, out var v) ? v : fallback;
    private static int? ParseNullableInt(string s) => int.TryParse(s, out var v) ? v : null;
}
