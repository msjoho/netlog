using NetLog.Cli.Tui;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>Drives the themed modal screens on a non-interactive console (plain clear+draw path),
/// which forces the markup to actually render — catching any unbalanced-tag bugs the compiler can't.</summary>
public class ModalScreensTests
{
    [Fact]
    public void Confirm_enter_is_yes_and_n_is_no()
    {
        Assert.True(ConfirmScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Enter }), "Go?"));
        Assert.False(ConfirmScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Of('n') }), "Go?", "detail"));
    }

    [Fact]
    public void Password_collects_typed_chars_and_returns_on_enter()
    {
        var keys = new ScriptedKeySource(new[] { KeyPress.Of('p'), KeyPress.Of('w'), KeyPress.Of('1'), KeyPress.Enter });
        Assert.Equal("pw1", PasswordScreen.Run(new TestConsole(), keys, @"CORPsvc"));
    }

    [Fact]
    public void Password_left_arrow_moves_the_caret_so_typing_inserts_there()
    {
        var keys = new ScriptedKeySource(new[] { KeyPress.Of('p'), KeyPress.Of('w'), KeyPress.Left, KeyPress.Of('1'), KeyPress.Enter });
        Assert.Equal("p1w", PasswordScreen.Run(new TestConsole(), keys, @"CORPsvc"));
    }

    // The reverse-video caret block: Theme.Bg on Theme.Green (#93C572 = 147,197,114).
    private const string GreenBackground = "48;2;147;197;114";

    [Fact]
    public void Password_and_new_window_render_a_block_caret()
    {
        var pw = new TestConsole(); pw.EmitAnsiSequences();
        PasswordScreen.Run(pw, new ScriptedKeySource(new[] { KeyPress.Of('a'), KeyPress.Enter }), @"CORPsvc");
        Assert.Contains(GreenBackground, pw.Output);
        Assert.DoesNotContain("|", pw.Output);

        var nw = new TestConsole(); nw.EmitAnsiSequences();
        NewWindowScreen.Run(nw, new ScriptedKeySource(new[] { KeyPress.Down, KeyPress.Esc }), "audit");   // End field: "7d"
        Assert.Contains(GreenBackground, nw.Output);
    }

    [Fact]
    public void Password_esc_cancels_to_null()
    {
        Assert.Null(PasswordScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Esc }), @"CORPsvc"));
    }

    [Fact]
    public void NewWindow_enter_returns_the_default_now_duration_window()
    {
        var sched = NewWindowScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Enter }), "audit");
        Assert.NotNull(sched);
        Assert.Equal("now", sched!.Value.Start);
        Assert.Equal("7d", sched.Value.Duration);
        Assert.Null(sched.Value.Stop);
    }

    [Fact]
    public void NewWindow_esc_cancels_to_null()
    {
        Assert.Null(NewWindowScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Esc }), "audit"));
    }
}
