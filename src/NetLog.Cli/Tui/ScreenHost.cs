using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Drives a screen's render/input loop. On a real terminal it uses Spectre's <c>Live</c>
/// display, which patches the screen in place (no clear-then-redraw flash) and hides the cursor —
/// fixing both the flicker and the stray blinking cursor. On a non-interactive console (tests) it
/// falls back to a plain clear+draw so behaviour and output capture are unchanged.</summary>
public static class ScreenHost
{
    /// <param name="render">Builds the current frame from screen state.</param>
    /// <param name="step">Runs one input/tick cycle, mutating state; returns true when the screen is done.</param>
    public static void Run(IAnsiConsole console, Func<IRenderable> render, Func<bool> step)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            while (true) { Frame.Draw(console, render()); if (step()) break; }
            return;
        }

        console.Clear();
        console.Live(render())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Bottom)
            .Start(ctx =>
            {
                while (true)
                {
                    ctx.UpdateTarget(render());
                    ctx.Refresh();
                    if (step()) break;
                }
            });
    }
}
