using NetLog.Cli.Tui;

namespace NetLog.Cli.Tests;

public class ScheduleReducerTests
{
    private static ScheduleForm At(int field) => ScheduleForm.Default() with { FieldIndex = field };

    [Fact]
    public void Defaults_are_now_and_duration_7d()
    {
        var f = ScheduleForm.Default();
        Assert.True(f.StartNow);
        Assert.True(f.EndDuration);
        Assert.Equal("7d", f.Duration);
    }

    [Fact]
    public void Up_down_moves_between_the_two_fields()
    {
        Assert.Equal(1, ScheduleReducer.Reduce(At(0), KeyPress.Down).Form.FieldIndex);
        Assert.Equal(1, ScheduleReducer.Reduce(At(1), KeyPress.Down).Form.FieldIndex); // clamped
        Assert.Equal(0, ScheduleReducer.Reduce(At(1), KeyPress.Up).Form.FieldIndex);
    }

    [Fact]
    public void Tab_flips_the_focused_field()
    {
        Assert.False(ScheduleReducer.Reduce(At(0), KeyPress.Tab).Form.StartNow);
        Assert.False(ScheduleReducer.Reduce(At(1), KeyPress.Tab).Form.EndDuration);
    }

    [Fact]
    public void Typing_edits_duration_but_not_start_while_now()
    {
        var start = ScheduleReducer.Reduce(At(0), KeyPress.Of('x')).Form;
        Assert.Equal("", start.StartDate);                              // now => not editable

        var end = At(1);
        end = ScheduleReducer.Reduce(end, KeyPress.Backspace).Form;     // "7d" -> "7"
        end = ScheduleReducer.Reduce(end, KeyPress.Backspace).Form;     // "7" -> ""
        end = ScheduleReducer.Reduce(end, KeyPress.Of('3')).Form;
        end = ScheduleReducer.Reduce(end, KeyPress.Of('h')).Form;
        Assert.Equal("3h", end.Duration);
    }

    [Fact]
    public void Enter_submits_and_esc_cancels()
    {
        Assert.Equal(ScheduleCommand.Submit, ScheduleReducer.Reduce(At(0), KeyPress.Enter).Cmd);
        Assert.Equal(ScheduleCommand.Cancel, ScheduleReducer.Reduce(At(0), KeyPress.Esc).Cmd);
    }

    [Fact]
    public void ToSchedule_maps_modes()
    {
        var dur = ScheduleReducer.ToSchedule(ScheduleForm.Default());
        Assert.Equal("now", dur.Start);
        Assert.Equal("7d", dur.Duration);
        Assert.Null(dur.Stop);

        var dated = ScheduleReducer.ToSchedule(ScheduleForm.Default() with
        {
            StartNow = false, StartDate = "2026-09-01T00:00Z",
            EndDuration = false, EndDate = "2026-09-30T17:00Z",
        });
        Assert.Equal("2026-09-01T00:00Z", dated.Start);
        Assert.Equal("2026-09-30T17:00Z", dated.Stop);
        Assert.Null(dated.Duration);
    }
}
