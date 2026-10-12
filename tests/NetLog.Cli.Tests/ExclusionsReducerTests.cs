// tests/NetLog.Cli.Tests/ExclusionsReducerTests.cs
using NetLog.Cli.Tui;
using NetLog.Core.Models;

namespace NetLog.Cli.Tests;

public class ExclusionsReducerTests
{
    private static ExclusionsState Two() => ExclusionsState.From(new[]
    {
        new ExclusionRule("process", "svchost (Dnscache)"),
        new ExclusionRule("host", "10.20.0.0/16", "out"),
    });

    private static ExclusionsState Feed(ExclusionsState s, params KeyPress[] keys)
    {
        foreach (var k in keys) s = ExclusionsReducer.Reduce(s, k).State;
        return s;
    }

    private static IEnumerable<KeyPress> Type(string text) => text.Select(KeyPress.Of);

    [Fact]
    public void Up_down_moves_cursor_within_bounds()
    {
        Assert.Equal(1, Feed(Two(), KeyPress.Down).Cursor);
        Assert.Equal(1, Feed(Two(), KeyPress.Down, KeyPress.Down).Cursor);
        Assert.Equal(0, Feed(Two(), KeyPress.Up).Cursor);
        Assert.Equal(0, Feed(ExclusionsState.From(Array.Empty<ExclusionRule>()), KeyPress.Down).Cursor);
    }

    [Fact]
    public void A_opens_the_add_line_and_enter_appends_a_parsed_rule()
    {
        var s = Feed(Two(), KeyPress.Of('a'));
        Assert.True(s.Adding);
        s = Feed(s, Type("user:CORP\\svc-*@in").ToArray());
        Assert.Equal("user:CORP\\svc-*@in", s.Buffer);
        s = Feed(s, KeyPress.Enter);
        Assert.False(s.Adding);
        Assert.Equal("", s.Buffer);
        Assert.Equal(3, s.Rules.Count);
        Assert.Equal(new ExclusionRule("user", "CORP\\svc-*", "in"), s.Rules[2]);
        Assert.Equal(2, s.Cursor);          // cursor lands on the new rule
        Assert.Null(s.Error);
    }

    [Fact]
    public void While_adding_letters_a_and_d_are_text_not_hotkeys()
    {
        var s = Feed(Two(), KeyPress.Of('a'), KeyPress.Of('a'), KeyPress.Of('d'));
        Assert.True(s.Adding);
        Assert.Equal("ad", s.Buffer);
        Assert.Equal(2, s.Rules.Count);
    }

    [Fact]
    public void Add_line_edits_with_a_caret()
    {
        var s = Feed(Two(), KeyPress.Of('a'));
        s = Feed(s, Type("host:x").ToArray());
        s = Feed(s, KeyPress.Left, KeyPress.Backspace);         // delete the ':'
        Assert.Equal("hostx", s.Buffer);
        Assert.Equal(4, s.Caret);
        s = Feed(s, KeyPress.Delete);                            // delete the 'x' under the caret
        Assert.Equal("host", s.Buffer);
    }

    [Fact]
    public void Invalid_text_sets_error_and_keeps_the_line_open()
    {
        var s = Feed(Two(), KeyPress.Of('a'));
        s = Feed(s, Type("port:443").Append(KeyPress.Enter).ToArray());
        Assert.True(s.Adding);
        Assert.Contains("Unknown kind", s.Error);
        Assert.Equal(2, s.Rules.Count);
        s = Feed(s, KeyPress.Backspace);                         // any edit clears the error
        Assert.Null(s.Error);
    }

    [Fact]
    public void Esc_while_adding_closes_the_line_without_discarding_the_list()
    {
        var s = Feed(Two(), KeyPress.Of('a'), KeyPress.Of('x'));
        var (next, cmd) = ExclusionsReducer.Reduce(s, KeyPress.Esc);
        Assert.Equal(ExclusionsCommand.None, cmd);
        Assert.False(next.Adding);
        Assert.Equal("", next.Buffer);
        Assert.Equal(2, next.Rules.Count);
    }

    [Fact]
    public void D_removes_the_selected_rule_and_clamps_the_cursor()
    {
        var s = Feed(Two(), KeyPress.Down, KeyPress.Of('d'));
        Assert.Single(s.Rules);
        Assert.Equal("process", s.Rules[0].Kind);
        Assert.Equal(0, s.Cursor);
        s = Feed(s, KeyPress.Of('d'), KeyPress.Of('d'));         // removing past empty is a no-op
        Assert.Empty(s.Rules);
    }

    [Fact]
    public void Enter_is_done_and_esc_is_discard_when_not_adding()
    {
        Assert.Equal(ExclusionsCommand.Done, ExclusionsReducer.Reduce(Two(), KeyPress.Enter).Cmd);
        Assert.Equal(ExclusionsCommand.Discard, ExclusionsReducer.Reduce(Two(), KeyPress.Esc).Cmd);
    }
}
