using NetLog.Cli.Tui;

namespace NetLog.Cli.Tests;

/// <summary>The one caret-editing rule set every text field shares.</summary>
public class TextEditTests
{
    [Fact]
    public void Char_inserts_at_the_caret_and_advances_it()
    {
        Assert.Equal(("axb", 2), TextEdit.Apply("ab", 1, KeyPress.Of('x')));
    }

    [Fact]
    public void Backspace_removes_before_the_caret_and_is_a_noop_at_the_start()
    {
        Assert.Equal(("b", 0), TextEdit.Apply("ab", 1, KeyPress.Backspace));
        Assert.Equal(("ab", 0), TextEdit.Apply("ab", 0, KeyPress.Backspace));
    }

    [Fact]
    public void Delete_removes_at_the_caret_and_is_a_noop_at_the_end()
    {
        Assert.Equal(("a", 1), TextEdit.Apply("ab", 1, KeyPress.Delete));
        Assert.Equal(("ab", 2), TextEdit.Apply("ab", 2, KeyPress.Delete));
    }

    [Fact]
    public void Arrows_move_the_caret_within_the_text()
    {
        Assert.Equal(("ab", 0), TextEdit.Apply("ab", 1, KeyPress.Left));
        Assert.Equal(("ab", 0), TextEdit.Apply("ab", 0, KeyPress.Left));
        Assert.Equal(("ab", 2), TextEdit.Apply("ab", 1, KeyPress.Right));
        Assert.Equal(("ab", 2), TextEdit.Apply("ab", 2, KeyPress.Right));
    }

    [Fact]
    public void An_out_of_range_caret_is_clamped_before_editing()
    {
        Assert.Equal(("abx", 3), TextEdit.Apply("ab", 9, KeyPress.Of('x')));
    }

    [Fact]
    public void Non_text_keys_are_not_handled()
    {
        Assert.Null(TextEdit.Apply("ab", 1, KeyPress.Enter));
        Assert.Null(TextEdit.Apply("ab", 1, KeyPress.Tab));
        Assert.Null(TextEdit.Apply("ab", 1, KeyPress.Up));
    }
}
