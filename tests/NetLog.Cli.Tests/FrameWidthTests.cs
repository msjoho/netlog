using NetLog.Cli.Rendering;
using NetLog.Cli.Tui;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>The screens that only pick up the frame width: wizard, run edit, root select, and the modals.
/// Rules span the frame, headers are one line with the context right-aligned, footers wrap between hotkeys.</summary>
public class FrameWidthTests
{
    private const string LongRoot = @"\\tdfs\InfraOps\Sources\netlog\some\deeply\nested\team\folder\for\runs\2026";
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static List<string> Lines(Action<TestConsole> write)
    {
        var console = new TestConsole();
        console.Profile.Width = 200;
        write(console);
        var lines = console.Output.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    [Fact]
    public void Wizard_header_spreads_title_and_root_to_the_frame_edges()
    {
        var lines = Lines(c => c.Write(WizardView.Render(WizardForm.Default(), LongRoot, @"CORP\mjoho", new ScreenSize(120, 30))));
        Assert.StartsWith("► netlog   new run", lines[0]);
        Assert.EndsWith(LongRoot, lines[0]);
        Assert.Equal(120, lines[0].Length);
        Assert.Equal(new string('─', 120), lines[1]);
    }

    [Fact]
    public void Wizard_lines_never_exceed_a_narrow_frame_with_a_long_root()
    {
        var lines = Lines(c => c.Write(WizardView.Render(WizardForm.Default() with { RunName = "audit" }, LongRoot, @"CORP\mjoho", new ScreenSize(76, 30))));
        Assert.All(lines, l => Assert.True(l.Length <= 76, $"{l.Length} > 76: {l}"));
        Assert.Contains(lines, l => l.StartsWith("Creates  …") && l.EndsWith(@"\audit"));
    }

    [Fact]
    public void Wizard_footer_wraps_between_hotkeys()
    {
        var form = WizardForm.Default() with { FieldIndex = (int)WizardField.Account };
        var lines = Lines(c => c.Write(WizardView.Render(form, @"\\srv\netlog", @"CORP\mjoho", new ScreenSize(76, 30))));
        Assert.Equal("[esc] cancel", lines[^1]);
        Assert.EndsWith("[enter] create & deploy", lines[^2]);
    }

    [Fact]
    public void Run_edit_header_spreads_title_and_run_name_to_the_frame_edges()
    {
        var cfg = new RunConfig("audit-q2", Now.AddDays(-1),
            new RunSchedule(Now.AddHours(-1), Now.AddDays(1)), new[] { "H1" }, "DOMAIN\\svc",
            new RunSettings("Merged", 10, 60, 7, null));
        var lines = Lines(c => c.Write(RunEditView.Render(cfg, RunEditForm.From(cfg), TimeZoneInfo.Utc, new ScreenSize(120, 30))));
        Assert.StartsWith("► netlog   edit run", lines[0]);
        Assert.EndsWith("audit-q2", lines[0]);
        Assert.Equal(120, lines[0].Length);
        Assert.Equal(new string('─', 120), lines[1]);
    }

    [Fact]
    public void Root_select_header_spreads_and_long_roots_are_cut_at_the_head()
    {
        var state = new RootSelectViewState(new[] { LongRoot, @"C:\temp\netlog" }, 0, false, "");
        var wide = Lines(c => c.Write(RootSelectView.Render(state, new ScreenSize(120, 30))));
        Assert.StartsWith("► netlog", wide[0]);
        Assert.EndsWith("select a root folder", wide[0]);
        Assert.Equal(120, wide[0].Length);
        Assert.Contains("► " + LongRoot, wide);

        var narrow = Lines(c => c.Write(RootSelectView.Render(state, new ScreenSize(76, 30))));
        Assert.All(narrow, l => Assert.True(l.Length <= 76, $"{l.Length} > 76: {l}"));
        Assert.Contains(narrow, l => l.StartsWith("► …") && l.EndsWith(@"\runs\2026"));
    }

    [Fact]
    public void New_window_and_password_modals_use_the_console_width()
    {
        var nw = new TestConsole(); nw.Profile.Width = 100;
        NewWindowScreen.Run(nw, new ScriptedKeySource(new[] { KeyPress.Esc }), "audit");
        var lines = nw.Output.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).ToList();
        Assert.Contains(new string('─', 100), lines);
        var head = lines.First(l => l.Contains("► netlog   new window"));
        head = head[head.IndexOf('►')..];                          // the non-interactive host clears the screen first
        Assert.EndsWith("audit", head);
        Assert.Equal(100, head.Length);

        var pw = new TestConsole(); pw.Profile.Width = 100;
        PasswordScreen.Run(pw, new ScriptedKeySource(new[] { KeyPress.Esc }), @"CORP\svc");
        Assert.Contains(new string('─', 100), pw.Output);
    }
}
