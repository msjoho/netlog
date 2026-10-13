using NetLog.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Drives a screen's render/input loop. On a real terminal it uses Spectre's <c>Live</c>
/// display, which patches the screen in place (no clear-then-redraw flash) and hides the cursor —
/// fixing both the flicker and the stray blinking cursor. When the console size changes between two
/// ticks the live frame is restarted (clear + new frame), because a frame patched in place assumes the
/// width it was first drawn at. On a non-interactive console (tests) it falls back to a plain
/// clear+draw so behaviour and output capture are unchanged.</summary>
public static class ScreenHost
{
    /// <summary>How long a screen waits for a key before yielding to the host, so a resize is noticed
    /// without a keypress. Screens pass this to <see cref="IKeySource.Read"/>.</summary>
    public const int PollMs = 250;

    /// <param name="render">Builds the current frame from screen state (and the current console size).</param>
    /// <param name="step">Runs one input/tick cycle, mutating state; returns true when the screen is done.</param>
    public static void Run(IAnsiConsole console, Func<IRenderable> render, Func<bool> step)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            while (true) { Frame.Draw(console, render()); if (step()) break; }
            return;
        }

        while (true)
        {
            var size = ScreenSize.Of(console);
            var done = false;

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
                        if (step()) { done = true; return; }
                        if (ScreenSize.Of(console) != size) return;   // resized: restart the live frame
                    }
                });

            if (done) return;
        }
    }
}
