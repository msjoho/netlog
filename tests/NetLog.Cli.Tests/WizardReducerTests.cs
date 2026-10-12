using NetLog.Cli.Tui;

namespace NetLog.Cli.Tests;

public class WizardReducerTests
{
    private const string CurrentUser = @"CORP\mjoho";

    private static WizardForm Form() => WizardForm.Default();
    private static WizardForm At(WizardField f) => Form() with { FieldIndex = (int)f };

    [Fact]
    public void Defaults_are_now_and_duration()
    {
        var f = Form();
        Assert.True(f.StartNow);
        Assert.True(f.EndDuration);
        Assert.Equal("7d", f.Duration);
        Assert.Equal("Merged", f.ArchiveLevel);
    }

    [Fact]
    public void Up_down_moves_field_within_bounds()
    {
        Assert.Equal(1, WizardReducer.Reduce(At(WizardField.RunName), CurrentUser, KeyPress.Down).Form.FieldIndex);
        Assert.Equal(0, WizardReducer.Reduce(At(WizardField.RunName), CurrentUser, KeyPress.Up).Form.FieldIndex);
        Assert.Equal((int)WizardField.Exclusions, WizardReducer.Reduce(At(WizardField.Exclusions), CurrentUser, KeyPress.Down).Form.FieldIndex);
    }

    [Fact]
    public void Typing_edits_the_focused_text_field()
    {
        var f = At(WizardField.RunName);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Of('Q')).Form;   // preserves case
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Of('3')).Form;
        Assert.Equal("Q3", f.RunName);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Backspace).Form;
        Assert.Equal("Q", f.RunName);
    }

    [Fact]
    public void Tab_on_account_fills_current_user()
    {
        var f = WizardReducer.Reduce(At(WizardField.Account), CurrentUser, KeyPress.Tab).Form;
        Assert.Equal(CurrentUser, f.Account);
    }

    [Fact]
    public void Tab_flips_start_and_end_modes()
    {
        var start = WizardReducer.Reduce(At(WizardField.Start), CurrentUser, KeyPress.Tab).Form;
        Assert.False(start.StartNow);
        var end = WizardReducer.Reduce(At(WizardField.End), CurrentUser, KeyPress.Tab).Form;
        Assert.False(end.EndDuration);
    }

    [Fact]
    public void Tab_toggles_archive_level()
    {
        var f = WizardReducer.Reduce(At(WizardField.ArchiveLevel), CurrentUser, KeyPress.Tab).Form;
        Assert.Equal("Full", f.ArchiveLevel);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Tab).Form;
        Assert.Equal("Merged", f.ArchiveLevel);
    }

    [Fact]
    public void Start_field_is_not_text_editable_while_now()
    {
        var f = At(WizardField.Start);                                    // StartNow = true
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Of('x')).Form;
        Assert.Equal("", f.StartDate);                                    // typing ignored
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Tab).Form;      // now -> date
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Of('2')).Form;
        Assert.Equal("2", f.StartDate);                                   // now editable
    }

    [Fact]
    public void Caret_inserts_in_the_middle_and_arrows_move_it()
    {
        var f = At(WizardField.RunName) with { RunName = "ac", Caret = 2 };
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Left).Form;   // caret 2 -> 1
        Assert.Equal(1, f.Caret);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Of('b')).Form; // insert at 1 -> "abc"
        Assert.Equal("abc", f.RunName);
        Assert.Equal(2, f.Caret);
    }

    [Fact]
    public void Backspace_deletes_before_caret_delete_removes_at_caret()
    {
        var f = At(WizardField.RunName) with { RunName = "abc", Caret = 2 };
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Backspace).Form;  // remove 'b' -> "ac", caret 1
        Assert.Equal("ac", f.RunName);
        Assert.Equal(1, f.Caret);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Delete).Form;     // remove 'c' at caret -> "a"
        Assert.Equal("a", f.RunName);
        Assert.Equal(1, f.Caret);
    }

    [Fact]
    public void Left_clamps_at_start_and_right_clamps_at_end()
    {
        var f = At(WizardField.RunName) with { RunName = "ab", Caret = 0 };
        Assert.Equal(0, WizardReducer.Reduce(f, CurrentUser, KeyPress.Left).Form.Caret);
        f = f with { Caret = 2 };
        Assert.Equal(2, WizardReducer.Reduce(f, CurrentUser, KeyPress.Right).Form.Caret);
    }

    [Fact]
    public void Changing_field_puts_the_caret_at_the_end_of_the_new_value()
    {
        var f = At(WizardField.RunName) with { RunName = "run", Targets = "H1,H2", Caret = 0 };
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Down).Form;   // -> Targets
        Assert.Equal((int)WizardField.Targets, f.FieldIndex);
        Assert.Equal("H1,H2".Length, f.Caret);
    }

    [Fact]
    public void Enter_creates_and_esc_cancels()
    {
        Assert.Equal(WizardCommand.Create, WizardReducer.Reduce(Form(), CurrentUser, KeyPress.Enter).Cmd);
        Assert.Equal(WizardCommand.Cancel, WizardReducer.Reduce(Form(), CurrentUser, KeyPress.Esc).Cmd);
    }

    [Fact]
    public void ToAnswers_duration_mode_builds_path_under_root()
    {
        var f = Form() with { RunName = "audit-q3", Targets = "H1,H2", Account = CurrentUser };
        var a = WizardReducer.ToAnswers(f, @"\\srv\netlog");

        Assert.Equal(System.IO.Path.Combine(@"\\srv\netlog", "audit-q3"), a.Path);
        Assert.Equal("now", a.Start);
        Assert.Equal("7d", a.Duration);
        Assert.Null(a.Stop);
        Assert.Equal(10, a.PollSeconds);
    }

    [Fact]
    public void ToAnswers_date_modes_set_stop_and_explicit_start()
    {
        var f = Form() with
        {
            RunName = "r", StartNow = false, StartDate = "2026-09-01T00:00Z",
            EndDuration = false, EndDate = "2026-09-30T17:00Z",
        };
        var a = WizardReducer.ToAnswers(f, @"\\srv\netlog");

        Assert.Equal("2026-09-01T00:00Z", a.Start);
        Assert.Equal("2026-09-30T17:00Z", a.Stop);
        Assert.Null(a.Duration);
    }

    [Fact]
    public void Tab_flips_timestamps_field()
    {
        var f = At(WizardField.Timestamps);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Tab).Form;
        Assert.True(f.Timestamps);
        f = WizardReducer.Reduce(f, CurrentUser, KeyPress.Tab).Form;
        Assert.False(f.Timestamps);
    }

    [Fact]
    public void ToAnswers_carries_timestamps()
    {
        var f = Form() with { Timestamps = true };
        Assert.True(WizardReducer.ToAnswers(f, @"\\share\runs").Timestamps);
    }

    [Fact]
    public void Exclusions_is_the_last_field_and_tab_requests_the_editor()
    {
        Assert.Equal(11, WizardForm.FieldCount);
        var f = At(WizardField.Exclusions);
        Assert.Null(f.Active);                                            // not a text field
        Assert.Equal(f, WizardReducer.Reduce(f, CurrentUser, KeyPress.Of('x')).Form);   // typing is ignored
        var (next, cmd) = WizardReducer.Reduce(f, CurrentUser, KeyPress.Tab);
        Assert.Equal(WizardCommand.EditExclusions, cmd);
        Assert.Equal(f, next);
        Assert.Equal((int)WizardField.Exclusions, WizardReducer.Reduce(At(WizardField.Timestamps), CurrentUser, KeyPress.Down).Form.FieldIndex);
    }

    [Fact]
    public void ToAnswers_carries_the_exclusion_list()
    {
        var rules = new[] { new NetLog.Core.Models.ExclusionRule("host", "nas*") };
        var a = WizardReducer.ToAnswers(Form() with { RunName = "r", Exclusions = rules }, @"\\s\root");
        Assert.Same(rules, a.Exclusions);
        Assert.Null(WizardReducer.ToAnswers(Form() with { RunName = "r" }, @"\\s\root").Exclusions);
    }
}
