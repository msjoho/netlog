using NetLog.Cli.Tui;

namespace NetLog.Cli.Tests;

public class DashboardReducerTests
{
    private static DashboardNav Nav(int cursor = 0, int? focus = null) => new(cursor, focus);

    [Fact]
    public void Down_then_up_moves_cursor_within_bounds()
    {
        var (a, _) = DashboardReducer.Reduce(Nav(0), hostCount: 3, KeyPress.Down);
        Assert.Equal(1, a.Cursor);
        var (b, _) = DashboardReducer.Reduce(Nav(2), 3, KeyPress.Down);
        Assert.Equal(2, b.Cursor);   // clamped at last
        var (c, _) = DashboardReducer.Reduce(Nav(0), 3, KeyPress.Up);
        Assert.Equal(0, c.Cursor);   // clamped at first
    }

    [Fact]
    public void Enter_focuses_the_cursor_host_and_a_or_esc_clears()
    {
        var (focused, _) = DashboardReducer.Reduce(Nav(2), 4, KeyPress.Enter);
        Assert.Equal(2, focused.Focus);

        Assert.Null(DashboardReducer.Reduce(focused, 4, KeyPress.Of('a')).Nav.Focus);
        Assert.Null(DashboardReducer.Reduce(focused, 4, KeyPress.Esc).Nav.Focus);
    }

    [Fact]
    public void Enter_with_no_hosts_does_not_focus()
    {
        Assert.Null(DashboardReducer.Reduce(Nav(0), hostCount: 0, KeyPress.Enter).Nav.Focus);
    }

    [Fact]
    public void Enter_unfocuses_only_on_the_focused_host_else_refocuses_the_cursor_host()
    {
        Assert.Null(DashboardReducer.Reduce(Nav(2, focus: 2), 4, KeyPress.Enter).Nav.Focus);          // on the focused host -> unfocus
        Assert.Equal(0, DashboardReducer.Reduce(Nav(0, focus: 3), 4, KeyPress.Enter).Nav.Focus);      // on another host -> switch focus
    }

    [Theory]
    [InlineData('s', DashboardCommand.Start)]
    [InlineData('x', DashboardCommand.Stop)]
    [InlineData('n', DashboardCommand.NewWindow)]
    [InlineData('e', DashboardCommand.Edit)]
    [InlineData('m', DashboardCommand.Merge)]
    [InlineData('r', DashboardCommand.Refresh)]
    [InlineData('q', DashboardCommand.Back)]
    public void Hotkeys_map_to_commands(char key, DashboardCommand expected)
    {
        Assert.Equal(expected, DashboardReducer.Reduce(Nav(), 3, KeyPress.Of(key)).Cmd);
    }
}

public class BrowserReducerTests
{
    [Fact]
    public void Arrows_move_cursor_within_bounds()
    {
        Assert.Equal(1, BrowserReducer.Reduce(0, 3, KeyPress.Down).Cursor);
        Assert.Equal(0, BrowserReducer.Reduce(0, 3, KeyPress.Up).Cursor);
        Assert.Equal(2, BrowserReducer.Reduce(2, 3, KeyPress.Down).Cursor);
    }

    [Theory]
    [InlineData('c', BrowserCommand.NewRun)]
    [InlineData('r', BrowserCommand.ChangeRoot)]
    [InlineData('q', BrowserCommand.Quit)]
    public void Hotkeys_map_to_commands(char key, BrowserCommand expected)
    {
        Assert.Equal(expected, BrowserReducer.Reduce(0, 3, KeyPress.Of(key)).Cmd);
    }

    [Fact]
    public void Enter_opens_run_when_present_else_noop()
    {
        Assert.Equal(BrowserCommand.OpenRun, BrowserReducer.Reduce(0, 3, KeyPress.Enter).Cmd);
        Assert.Equal(BrowserCommand.None, BrowserReducer.Reduce(0, 0, KeyPress.Enter).Cmd);
    }

    [Fact]
    public void Browser_x_requests_the_root_exclusions_editor()
    {
        Assert.Equal(BrowserCommand.Exclusions, BrowserReducer.Reduce(0, 0, KeyPress.Of('x')).Cmd);
        Assert.Equal(BrowserCommand.Exclusions, BrowserReducer.Reduce(1, 3, KeyPress.Of('x')).Cmd);
    }
}

public class RootReducerTests
{
    private static RootNav Nav(int cursor = 0, bool editing = false, string buffer = "") => new(cursor, editing, buffer);

    [Fact]
    public void R_enters_edit_mode_with_empty_buffer()
    {
        var (nav, _) = RootReducer.Reduce(Nav(), rootCount: 2, KeyPress.Of('r'));
        Assert.True(nav.Editing);
        Assert.Equal("", nav.Buffer);
    }

    [Fact]
    public void Typing_appends_and_backspace_trims()
    {
        var s = Nav(editing: true, buffer: "");
        s = RootReducer.Reduce(s, 0, KeyPress.Of('a')).Nav;
        s = RootReducer.Reduce(s, 0, KeyPress.Of('b')).Nav;
        Assert.Equal("ab", s.Buffer);
        s = RootReducer.Reduce(s, 0, KeyPress.Backspace).Nav;
        Assert.Equal("a", s.Buffer);
    }

    [Fact]
    public void Enter_in_edit_submits_and_esc_cancels()
    {
        var editing = Nav(editing: true, buffer: @"\\srv\share");
        Assert.Equal(RootCommand.SubmitNewRoot, RootReducer.Reduce(editing, 0, KeyPress.Enter).Cmd);

        var (cancelled, cmd) = RootReducer.Reduce(editing, 0, KeyPress.Esc);
        Assert.Equal(RootCommand.None, cmd);
        Assert.False(cancelled.Editing);
    }

    [Fact]
    public void Enter_in_list_opens_root_and_q_quits()
    {
        Assert.Equal(RootCommand.OpenRoot, RootReducer.Reduce(Nav(), 2, KeyPress.Enter).Cmd);
        Assert.Equal(RootCommand.None, RootReducer.Reduce(Nav(), 0, KeyPress.Enter).Cmd);  // no roots
        Assert.Equal(RootCommand.Quit, RootReducer.Reduce(Nav(), 2, KeyPress.Of('q')).Cmd);
    }
}
