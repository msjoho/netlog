using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Draws one full-screen frame: clear, then write the renderable. The logwork model —
/// re-render the whole screen each tick/keypress rather than patching in place.</summary>
public static class Frame
{
    public static void Draw(IAnsiConsole console, IRenderable renderable)
    {
        console.Clear();
        console.Write(renderable);
    }
}
