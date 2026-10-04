using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Shared layout constants. A fixed frame width keeps every section the same width (rules
/// no longer stretch to the full terminal), so the stacked sections line up as uniform blocks.</summary>
public static class Ui
{
    public const int Width = 76;

    /// <summary>A full-frame-width separator rule in the dim palette.</summary>
    public static IRenderable Rule() => new Markup($"[{Theme.Dim}]{new string('─', Width)}[/]");
}
