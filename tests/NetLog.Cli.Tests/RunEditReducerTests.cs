using NetLog.Cli.Tui;
using NetLog.Core.Models;
using NetLog.Core.RunSpec;

namespace NetLog.Cli.Tests;

public class RunEditReducerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);

    private static RunConfig Config() => RunConfigFactory.Create(
        @"\\s\run", "H1", "svc", "now", null, "7d", "Full", 10, 60, 7, null, Now);

    [Fact]
    public void From_prefills_mutable_values_and_dates_the_current_end()
    {
        var f = RunEditForm.From(Config());
        Assert.False(f.EndDuration);                       // shows current stop as a date
        Assert.Equal("2026-06-17T00:00:00Z", f.EndDate);   // start + 7d
        Assert.Equal("10", f.Poll);
        Assert.Equal("60", f.ArchiveEvery);
        Assert.Equal("7", f.Cache);
    }

    [Fact]
    public void Navigates_four_fields_and_tab_flips_only_end()
    {
        var f = RunEditForm.From(Config());
        Assert.Equal((int)RunEditField.Poll, RunEditReducer.Reduce(f, KeyPress.Down).Form.FieldIndex);
        Assert.True(RunEditReducer.Reduce(f, KeyPress.Tab).Form.EndDuration);                       // date -> duration
        Assert.False(RunEditReducer.Reduce(f with { EndDuration = true }, KeyPress.Tab).Form.EndDuration);  // duration -> date

        var poll = f with { FieldIndex = (int)RunEditField.Poll };
        Assert.Equal(poll, RunEditReducer.Reduce(poll, KeyPress.Tab).Form);        // Tab is a no-op off the End field
    }

    [Fact]
    public void Typing_edits_the_focused_field_with_a_caret()
    {
        var f = (RunEditForm.From(Config()) with { FieldIndex = (int)RunEditField.Poll, Poll = "10", Caret = 2 });
        f = RunEditReducer.Reduce(f, KeyPress.Of('0')).Form;   // append -> "100"
        Assert.Equal("100", f.Poll);
        f = RunEditReducer.Reduce(f, KeyPress.Backspace).Form;
        Assert.Equal("10", f.Poll);
    }

    [Fact]
    public void ToArgs_maps_date_and_duration_modes()
    {
        var dated = RunEditForm.From(Config());
        Assert.Equal("2026-06-17T00:00:00Z", RunEditReducer.ToArgs(dated).Stop);
        Assert.Null(RunEditReducer.ToArgs(dated).Duration);

        var dur = dated with { EndDuration = true, Duration = "3d" };
        Assert.Equal("3d", RunEditReducer.ToArgs(dur).Duration);
        Assert.Null(RunEditReducer.ToArgs(dur).Stop);
    }

    [Fact]
    public void Enter_saves_esc_cancels()
    {
        var f = RunEditForm.From(Config());
        Assert.Equal(RunEditCommand.Save, RunEditReducer.Reduce(f, KeyPress.Enter).Cmd);
        Assert.Equal(RunEditCommand.Cancel, RunEditReducer.Reduce(f, KeyPress.Esc).Cmd);
    }
}
