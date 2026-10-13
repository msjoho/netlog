using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>The frame a screen is rendered into: the console's width (never below <see cref="MinWidth"/>)
/// and its height. Built fresh for every frame so a resized console is picked up on the next render.</summary>
public readonly record struct ScreenSize(int Width, int Height)
{
    /// <summary>The layout's minimum width; narrower consoles get this width and wrap, as before.</summary>
    public const int MinWidth = 76;

    public static ScreenSize Of(IAnsiConsole console) =>
        new(Math.Max(MinWidth, console.Profile.Width), Math.Max(1, console.Profile.Height));

    /// <summary>For one-shot output (<c>netlog status</c>): the console's width, no height cropping.</summary>
    public static ScreenSize Unbounded(IAnsiConsole console) =>
        new(Math.Max(MinWidth, console.Profile.Width), int.MaxValue);
}

/// <summary>Shared layout helpers. Every section of a screen is bound to the same frame width, so the
/// stacked sections line up as uniform blocks whatever the console size.</summary>
public static class Ui
{
    /// <summary>A separator rule in the dim palette, exactly <paramref name="width"/> wide.</summary>
    public static IRenderable Rule(int width) => new Markup($"[{Theme.Dim}]{new string('─', width)}[/]");

    /// <summary>One line with <paramref name="leftMarkup"/> at the left edge and <paramref name="rightMarkup"/>
    /// ending at the right edge. Both are markup; widths are measured on the plain text. When the two cannot
    /// share the line they are joined with three spaces (callers trim their context first, see <see cref="HeadTrim"/>).</summary>
    public static string Spread(string leftMarkup, string rightMarkup, int width)
    {
        var gap = width - Markup.Remove(leftMarkup).Length - Markup.Remove(rightMarkup).Length;
        return leftMarkup + new string(' ', Math.Max(3, gap)) + rightMarkup;
    }

    /// <summary>Cut the head of a plain string so it fits in <paramref name="max"/> columns, keeping the tail
    /// (the informative end of a path) behind an ellipsis.</summary>
    public static string HeadTrim(string plain, int max) =>
        plain.Length <= max ? plain : "…" + plain[^(max - 1)..];

    /// <summary>Pad plain text to <paramref name="width"/>, or cut it with a trailing ellipsis.</summary>
    public static string Fit(string plain, int width) =>
        plain.Length <= width ? plain.PadRight(width) : plain[..(width - 1)] + "…";

    /// <summary>Like <see cref="Fit"/>, but a "host (ip)" label keeps its "(ip)" and shortens the host part,
    /// so a cut label still identifies the peer.</summary>
    public static string FitLabel(string plain, int width)
    {
        if (plain.Length <= width) return plain.PadRight(width);
        var open = plain.LastIndexOf(" (", StringComparison.Ordinal);
        if (open > 0 && plain.EndsWith(')'))
        {
            var ip = plain[(open + 1)..];                 // "(10.84.4.4)"
            var hostRoom = width - ip.Length - 2;         // host + "… " + ip
            if (hostRoom >= 1) return plain[..hostRoom] + "… " + ip;
        }
        return Fit(plain, width);
    }

    /// <summary>Hotkey footer: groups ("[q] back") flow left to right separated by two spaces and wrap only
    /// between groups, so a key never separates from its label. Each returned line is markup.</summary>
    public static IReadOnlyList<string> FooterLines(IEnumerable<string> groupsMarkup, int width)
    {
        var lines = new List<string>();
        string? current = null; var currentLen = 0;
        foreach (var g in groupsMarkup)
        {
            var len = Markup.Remove(g).Length;
            if (current is not null && currentLen + 2 + len <= width) { current += "  " + g; currentLen += 2 + len; continue; }
            if (current is not null) lines.Add(current);
            current = g; currentLen = len;
        }
        if (current is not null) lines.Add(current);
        return lines;
    }

    /// <summary>Word-wrap plain text to <paramref name="width"/>; continuation lines are indented to sit
    /// under the text of an "i  " hint.</summary>
    public static IReadOnlyList<string> Wrap(string plain, int width, string continuationIndent = "   ")
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in plain.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (candidate.Length <= width || current.Length == 0) { current = candidate; continue; }
            lines.Add(current);
            current = continuationIndent + word;
        }
        lines.Add(current);
        return lines;
    }
}
