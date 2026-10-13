using NetLog.Cli.Tui;

namespace NetLog.Cli.Tests;

public class ViewportTests
{
    [Fact]
    public void Everything_fits_shows_all_items_without_markers()
    {
        Assert.Equal(new Window(0, 5, 0, 0), Viewport.Of(count: 5, cursor: 2, rows: 8));
        Assert.Equal(new Window(0, 8, 0, 0), Viewport.Of(count: 8, cursor: 7, rows: 8));
    }

    [Fact]
    public void Cursor_at_the_top_leaves_one_row_for_the_below_marker()
    {
        Assert.Equal(new Window(0, 7, 0, 19), Viewport.Of(count: 26, cursor: 0, rows: 8));
    }

    [Fact]
    public void Cursor_at_the_end_leaves_one_row_for_the_above_marker()
    {
        Assert.Equal(new Window(19, 7, 19, 0), Viewport.Of(count: 26, cursor: 25, rows: 8));
    }

    [Fact]
    public void Cursor_in_the_middle_is_centred_with_both_markers()
    {
        var w = Viewport.Of(count: 26, cursor: 13, rows: 8);
        Assert.Equal(6, w.Count);
        Assert.True(w.Start <= 13 && 13 < w.Start + w.Count);
        Assert.Equal(w.Start, w.Above);
        Assert.Equal(26 - w.Start - w.Count, w.Below);
        Assert.Equal(new Window(10, 6, 10, 10), w);
    }

    [Fact]
    public void Tiny_budget_still_shows_the_cursor_row()
    {
        var w = Viewport.Of(count: 26, cursor: 13, rows: 2);
        Assert.Equal(1, w.Count);
        Assert.Equal(13, w.Start);
    }
}
