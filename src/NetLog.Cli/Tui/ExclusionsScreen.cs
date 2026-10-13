using NetLog.Cli.Rendering;
using NetLog.Core.Models;
using Spectre.Console;

namespace NetLog.Cli.Tui;

/// <summary>The exclusions list editor. Returns the edited list on [enter], or null on [esc] (discard).
/// Persisting the result is the caller's job (run.json for a run, exclusions.json for a root).</summary>
public static class ExclusionsScreen
{
    public static IReadOnlyList<ExclusionRule>? Run(IAnsiConsole console, IKeySource keys,
        IReadOnlyList<ExclusionRule> initial, string title, string context, string? warning = null)
    {
        var state = ExclusionsState.From(initial);
        IReadOnlyList<ExclusionRule>? result = null;

        ScreenHost.Run(console,
            () => ExclusionsView.Render(state, title, context, warning, ScreenSize.Of(console)),
            () =>
            {
                if (keys.Read(ScreenHost.PollMs) is not { } key) return false;
                var (next, cmd) = ExclusionsReducer.Reduce(state, key);
                state = next;
                switch (cmd)
                {
                    case ExclusionsCommand.Done:    result = state.Rules; return true;
                    case ExclusionsCommand.Discard: return true;
                    default:                        return false;
                }
            });

        return result;
    }
}
