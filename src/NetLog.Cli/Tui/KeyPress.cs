namespace NetLog.Cli.Tui;

public enum KeyKind { Char, Up, Down, Left, Right, Enter, Esc, Tab, Backspace, Delete, Other }

/// <summary>A terminal keypress, normalised away from Console specifics so reducers stay pure
/// and unit-testable. The IO loop translates ConsoleKeyInfo into one of these.</summary>
public readonly record struct KeyPress(KeyKind Kind, char Char = '\0')
{
    public static KeyPress Of(char c) => new(KeyKind.Char, c);
    public static readonly KeyPress Up = new(KeyKind.Up);
    public static readonly KeyPress Down = new(KeyKind.Down);
    public static readonly KeyPress Left = new(KeyKind.Left);
    public static readonly KeyPress Right = new(KeyKind.Right);
    public static readonly KeyPress Enter = new(KeyKind.Enter);
    public static readonly KeyPress Esc = new(KeyKind.Esc);
    public static readonly KeyPress Tab = new(KeyKind.Tab);
    public static readonly KeyPress Backspace = new(KeyKind.Backspace);
    public static readonly KeyPress Delete = new(KeyKind.Delete);

    public bool Is(char c) => Kind == KeyKind.Char && Char == char.ToLowerInvariant(c);
}
