// src/NetLog.Cli/Tui/ExclusionsReducer.cs
using NetLog.Core.Models;
using NetLog.Core.RunSpec;

namespace NetLog.Cli.Tui;

public enum ExclusionsCommand { None, Done, Discard }

/// <summary>The exclusions list editor as pure state: a cursor over the rules, plus an optional
/// "add" line typed in the text form <c>kind:pattern[@in|@out]</c>. Shared by the wizard, the root
/// editor and the edit-during-run screen — only the caller differs in where the result is saved.</summary>
public sealed record ExclusionsState(
    IReadOnlyList<ExclusionRule> Rules, int Cursor, bool Adding, string Buffer, int Caret, string? Error)
{
    public static ExclusionsState From(IReadOnlyList<ExclusionRule> rules) => new(rules.ToList(), 0, false, "", 0, null);
}

public static class ExclusionsReducer
{
    public static (ExclusionsState State, ExclusionsCommand Cmd) Reduce(ExclusionsState s, KeyPress k)
    {
        if (s.Adding) return (ReduceAdding(s, k), ExclusionsCommand.None);

        switch (k.Kind)
        {
            case KeyKind.Up:    return (s with { Cursor = Math.Max(0, s.Cursor - 1) }, ExclusionsCommand.None);
            case KeyKind.Down:  return (s with { Cursor = Math.Min(Math.Max(0, s.Rules.Count - 1), s.Cursor + 1) }, ExclusionsCommand.None);
            case KeyKind.Enter: return (s, ExclusionsCommand.Done);
            case KeyKind.Esc:   return (s, ExclusionsCommand.Discard);
        }
        if (k.Is('a')) return (s with { Adding = true, Buffer = "", Caret = 0, Error = null }, ExclusionsCommand.None);
        if (k.Is('d') && s.Rules.Count > 0)
        {
            var list = s.Rules.ToList();
            list.RemoveAt(Math.Clamp(s.Cursor, 0, list.Count - 1));
            return (s with { Rules = list, Cursor = Math.Clamp(s.Cursor, 0, Math.Max(0, list.Count - 1)) }, ExclusionsCommand.None);
        }
        return (s, ExclusionsCommand.None);
    }

    private static ExclusionsState ReduceAdding(ExclusionsState s, KeyPress k)
    {
        if (TextEdit.Apply(s.Buffer, s.Caret, k) is { } e)
        {
            // Editing the text clears a stale parse error; merely moving the caret keeps it visible.
            var moved = k.Kind is KeyKind.Left or KeyKind.Right;
            return s with { Buffer = e.Text, Caret = e.Caret, Error = moved ? s.Error : null };
        }
        switch (k.Kind)
        {
            case KeyKind.Esc:   return s with { Adding = false, Buffer = "", Caret = 0, Error = null };
            case KeyKind.Enter:
                if (!ExclusionRuleParser.TryParse(s.Buffer, out var rule, out var error))
                    return s with { Error = error };
                var list = s.Rules.ToList();
                list.Add(rule!);
                return s with { Rules = list, Cursor = list.Count - 1, Adding = false, Buffer = "", Caret = 0, Error = null };
            default:
                return s;
        }
    }
}
