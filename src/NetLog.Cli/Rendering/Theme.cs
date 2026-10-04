using NetLog.Core.Models;

namespace NetLog.Cli.Rendering;

/// <summary>The single source of truth for the TUI's look — palette + glyphs ported from logwork.
/// Colours are Spectre markup hex tags; callers must escape dynamic text with Markup.Escape.</summary>
public static class Theme
{
    public const string Green = "#93C572";
    public const string Red   = "#cc4444";
    public const string White = "#fdfbf2";
    public const string Text  = "#aca6a3";
    public const string Gray  = "#6f6a66";
    public const string Dim   = "#4a4a4a";
    public const string Amber = "#d6a458";
    public const string Blue  = "#7da3c9";
    public const string Hint  = "#6f8088";
    public const string Bg    = "#0e110e";   // terminal background, for reverse-video carets

    // Glyphs are deliberately restricted to characters present in Consolas/Lucida Console —
    // the TUI must render on plain conhost (servers over RDP), not just Windows Terminal.
    // Verified missing there and therefore banned: ▶ ⓘ ⚠ ◷ ✓ ▏
    public const string Cursor = "►";

    /// <summary>A block text caret: the character at the caret rendered reverse-video (or a green block
    /// at end of text). <paramref name="text"/> must be plain (unescaped).</summary>
    public static string Caret_(string text, int caret)
    {
        var c = Math.Clamp(caret, 0, text.Length);
        string block(string s) => $"[{Bg} on {Green}]{Spectre.Console.Markup.Escape(s)}[/]";
        if (c == text.Length) return Spectre.Console.Markup.Escape(text) + block(" ");
        return Spectre.Console.Markup.Escape(text[..c]) + block(text[c].ToString()) + Spectre.Console.Markup.Escape(text[(c + 1)..]);
    }

    /// <summary>Wrap already-escaped text in a colour.</summary>
    public static string Paint(string color, string text) => $"[{color}]{text}[/]";

    public static string RunStateMarkup(RunState s) => s switch
    {
        RunState.Running   => $"[{Green}]● Running[/]",
        RunState.Scheduled => $"[{Blue}]○ Scheduled[/]",
        RunState.Stalled   => $"[{Amber}]◌ Stalled[/]",
        RunState.Completed => $"[{Gray}]√ Completed[/]",
        _ => Markup_(s.ToString()),
    };

    public static string HostLivenessMarkup(HostLiveness l) => l switch
    {
        HostLiveness.Live        => $"[{Green}]● live[/]",
        HostLiveness.Stalled     => $"[{Amber}]◌ stalled[/]",
        HostLiveness.NoHeartbeat => $"[{Dim}]○ no heartbeat[/]",
        _ => "",
    };

    /// <summary>Run-window-derived state for the runs browser (cheap, no heartbeats).</summary>
    public static string WindowStateMarkup(DateTimeOffset start, DateTimeOffset stop, DateTimeOffset now)
    {
        if (now < start)  return $"[{Blue}]○ Scheduled[/]";
        if (now >= stop)  return $"[{Gray}]√ Completed[/]";
        return $"[{Green}]● Active[/]";
    }

    private static string Markup_(string s) => Spectre.Console.Markup.Escape(s);
}
