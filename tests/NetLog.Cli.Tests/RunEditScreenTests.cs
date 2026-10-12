using NetLog.Cli.Tui;
using NetLog.Core.Io;
using NetLog.Core.RunSpec;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class RunEditScreenTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);

    private static string WriteRun()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nledit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cfg = RunConfigFactory.Create(@"\\s\audit", "H1", "DOMAIN\\svc", "now", null, "7d", "Full", 10, 60, 7, null, Now);
        RunConfigWriter.WriteFile(Path.Combine(dir, "run.json"), cfg);
        return dir;
    }

    [Fact]
    public void Editing_poll_and_saving_writes_run_json_keeping_identity()
    {
        var dir = WriteRun();
        // Down to Poll, append a 0 (10 -> 100), Enter to save.
        var keys = new ScriptedKeySource(new[] { KeyPress.Down, KeyPress.Of('0'), KeyPress.Enter });
        var config = RunConfigReader.ReadFile(Path.Combine(dir, "run.json"));

        var saved = RunEditScreen.Run(new TestConsole(), keys, dir, config, () => Now.AddHours(1));

        Assert.True(saved);
        var updated = RunConfigReader.ReadFile(Path.Combine(dir, "run.json"));
        Assert.Equal(100, updated.Settings.PollIntervalSeconds);
        Assert.Equal("audit", updated.RunName);                 // identity preserved
        Assert.Equal(Now.AddDays(7), updated.Schedule.StopUtc); // end unchanged (date prefilled)
        Assert.Equal("Full", updated.Settings.ArchiveLevel);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Esc_cancels_without_writing()
    {
        var dir = WriteRun();
        var config = RunConfigReader.ReadFile(Path.Combine(dir, "run.json"));
        var saved = RunEditScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Down, KeyPress.Of('9'), KeyPress.Esc }), dir, config, () => Now);

        Assert.False(saved);
        Assert.Equal(10, RunConfigReader.ReadFile(Path.Combine(dir, "run.json")).Settings.PollIntervalSeconds);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Editing_exclusions_and_saving_writes_them_to_run_json()
    {
        var dir = WriteRun();
        var config = RunConfigReader.ReadFile(Path.Combine(dir, "run.json"));
        var keys = new List<KeyPress> { KeyPress.Down, KeyPress.Down, KeyPress.Down, KeyPress.Down, KeyPress.Tab };   // to Exclusions, open
        keys.Add(KeyPress.Of('a')); keys.AddRange("process:Teams*".Select(KeyPress.Of)); keys.Add(KeyPress.Enter);
        keys.Add(KeyPress.Enter);   // editor done
        keys.Add(KeyPress.Enter);   // save

        var console = new TestConsole();
        var saved = RunEditScreen.Run(console, new ScriptedKeySource(keys), dir, config, () => Now.AddHours(1));

        Assert.True(saved);
        var updated = RunConfigReader.ReadFile(Path.Combine(dir, "run.json"));
        Assert.Equal(new[] { new NetLog.Core.Models.ExclusionRule("process", "Teams*") }, updated.Settings.Exclusions);
        Assert.Equal(10, updated.Settings.PollIntervalSeconds);        // other fields untouched
        Assert.Contains("purge matching rows", console.Output);        // the editor's warning was shown
        Directory.Delete(dir, recursive: true);
    }
}
