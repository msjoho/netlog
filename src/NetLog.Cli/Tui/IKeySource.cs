namespace NetLog.Cli.Tui;

/// <summary>Source of keypresses for the TUI loop. Abstracted so screens are driven by a scripted
/// source in tests instead of the real console.</summary>
public interface IKeySource
{
    /// <summary>Wait up to <paramref name="timeoutMs"/> for a key (≤0 blocks indefinitely).
    /// Returns null only on timeout.</summary>
    KeyPress? Read(int timeoutMs);
}

/// <summary>Real console key source: non-blocking poll so the dashboard can also tick on a timer.</summary>
public sealed class ConsoleKeySource : IKeySource
{
    public KeyPress? Read(int timeoutMs)
    {
        var blocking = timeoutMs <= 0;
        var deadline = Environment.TickCount64 + timeoutMs;
        while (blocking || Environment.TickCount64 < deadline)
        {
            if (Console.KeyAvailable) return Translate(Console.ReadKey(intercept: true));
            Thread.Sleep(15);
        }
        return null;
    }

    public static KeyPress Translate(ConsoleKeyInfo k) => k.Key switch
    {
        ConsoleKey.UpArrow    => KeyPress.Up,
        ConsoleKey.DownArrow  => KeyPress.Down,
        ConsoleKey.LeftArrow  => KeyPress.Left,
        ConsoleKey.RightArrow => KeyPress.Right,
        ConsoleKey.Enter      => KeyPress.Enter,
        ConsoleKey.Escape     => KeyPress.Esc,
        ConsoleKey.Tab        => KeyPress.Tab,
        ConsoleKey.Backspace  => KeyPress.Backspace,
        ConsoleKey.Delete     => KeyPress.Delete,
        _ when !char.IsControl(k.KeyChar) => KeyPress.Of(k.KeyChar),
        _ => new KeyPress(KeyKind.Other),
    };
}
