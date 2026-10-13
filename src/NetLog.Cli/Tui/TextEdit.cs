namespace NetLog.Cli.Tui;

/// <summary>The caret-editing rules every text field shares: insert at the caret, Backspace before
/// it, Delete under it, ←/→ to move. Pure, so each reducer stays a thin mapping onto its own state.</summary>
public static class TextEdit
{
    /// <summary>Applies <paramref name="k"/> to <paramref name="text"/>; null when the key is not a
    /// text-editing key (the caller handles it). An out-of-range caret is clamped first.</summary>
    public static (string Text, int Caret)? Apply(string text, int caret, KeyPress k)
    {
        var c = Math.Clamp(caret, 0, text.Length);
        return k.Kind switch
        {
            KeyKind.Char      => (text[..c] + k.Char + text[c..], c + 1),
            KeyKind.Backspace => c == 0 ? (text, c) : (text[..(c - 1)] + text[c..], c - 1),
            KeyKind.Delete    => c >= text.Length ? (text, c) : (text[..c] + text[(c + 1)..], c),
            KeyKind.Left      => (text, Math.Max(0, c - 1)),
            KeyKind.Right     => (text, Math.Min(text.Length, c + 1)),
            _ => null,
        };
    }
}
