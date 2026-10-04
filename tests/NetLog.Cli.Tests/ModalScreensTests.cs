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
        Assert.Equal("pw1", PasswordScreen.Run(new TestConsole(), keys, @"CORP\svc"));
    }

    [Fact]
    public void Password_esc_cancels_to_null()
    {
        Assert.Null(PasswordScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Esc }), @"CORP\svc"));
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
