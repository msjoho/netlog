namespace NetLog.Cli.Tui;

/// <summary>The visible slice of a list: items [Start, Start+Count) are shown; Above/Below are the hidden
/// counts for the "▲ n more" / "▼ n more" marker rows.</summary>
public readonly record struct Window(int Start, int Count, int Above, int Below);

/// <summary>Fits a list into a row budget. Stateless: the window is derived from the cursor alone (kept
/// centred while scrolling), so screens need no scroll state. <paramref name="rows"/> includes the marker
/// rows — a marker takes one of them whenever items are hidden on that side.</summary>
public static class Viewport
{
    public static Window Of(int count, int cursor, int rows)
    {
        if (count <= rows) return new Window(0, count, 0, 0);
        cursor = Math.Clamp(cursor, 0, count - 1);

        // Assume both markers, centre the cursor, then give a marker's row back when that side has nothing hidden.
        var items = Math.Max(1, rows - 2);
        var start = Math.Clamp(cursor - items / 2, 0, count - items);
        if (start == 0)
        {
            items = Math.Max(1, rows - 1);
        }
        else if (start + items >= count)
        {
            items = Math.Max(1, rows - 1);
            start = count - items;
        }
        if (cursor >= start + items) start = cursor - items + 1;   // tiny budgets: keep the cursor row visible
        if (cursor < start) start = cursor;
        return new Window(start, items, start, count - start - items);
    }
}
