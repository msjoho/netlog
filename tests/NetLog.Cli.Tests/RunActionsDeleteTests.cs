using NetLog.Cli.Deploy;
using NetLog.Cli.Tui;
using NetLog.Core.Io;
using NetLog.Core.RunSpec;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>[d] delete on the runs browser: destroy + purge on every target, then delete the run
/// folder on the share. Key scripts: confirm → results pause → (second confirm on failure) → info pause.</summary>
public class RunActionsDeleteTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A run folder named like the run, under a throwaway root (deleted by <see cref="Cleanup"/>).</summary>
    private static string WriteRun()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nlad_" + Guid.NewGuid().ToString("N"), "audit");
        Directory.CreateDirectory(Path.Combine(dir, "hosts", "H1"));
        File.WriteAllText(Path.Combine(dir, "hosts", "H1", "connections.csv"), "x");
        var cfg = RunConfigFactory.Create(@"\\s\audit", "H1,H2", "svc", "2026-06-10T00:00:00Z", "2026-06-30T00:00:00Z", null, "Merged", 10, 60, 7, null, Now);
        RunConfigWriter.WriteFile(Path.Combine(dir, "run.json"), cfg);
        return dir;
    }

    private static void Cleanup(string runDir) => Directory.Delete(Path.GetDirectoryName(runDir)!, recursive: true);

    private static (RunActions Actions, FakeRunner Runner, TestConsole Console) Actions(IEnumerable<KeyPress> keys, params DeployResult[] results)
    {
        var runner = new FakeRunner { Result = results };
        var console = new TestConsole();
        return (new RunActions(console, new ScriptedKeySource(keys), runner, () => Now), runner, console);
    }

    [Fact]
    public void Confirm_names_the_run_and_warns_that_all_collected_data_is_deleted()
    {
        var dir = WriteRun();
        var (actions, runner, console) = Actions(new[] { KeyPress.Of('n') });
        actions.Delete(dir);

        Assert.Contains("audit", console.Output);
        Assert.Contains("all collected data", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 target", console.Output);
        Assert.Null(runner.Last);                       // declined: nothing touched
        Assert.True(Directory.Exists(dir));
        Cleanup(dir);
    }

    [Fact]
    public void Confirmed_delete_runs_the_remove_action_then_deletes_the_run_folder()
    {
        var dir = WriteRun();
        var (actions, runner, _) = Actions(new[] { KeyPress.Of('y'), KeyPress.Enter /* results */, KeyPress.Enter /* deleted info */ },
            new DeployResult("H1", "Success", null), new DeployResult("H2", "Success", null));
        actions.Delete(dir);

        Assert.NotNull(runner.Last);
        Assert.Equal("remove", runner.Last!.Action);
        Assert.Equal("H1,H2", runner.Last.Targets);
        Assert.Null(runner.Last.Password);
        Assert.False(Directory.Exists(dir));
        Cleanup(dir);
    }

    [Fact]
    public void A_failed_host_asks_again_and_no_keeps_the_folder()
    {
        var dir = WriteRun();
        var (actions, runner, console) = Actions(new[] { KeyPress.Of('y'), KeyPress.Enter /* results */, KeyPress.Of('n') },
            new DeployResult("H1", "Success", null), new DeployResult("H2", "Failed", "Access is denied"));
        actions.Delete(dir);

        Assert.Equal("remove", runner.Last!.Action);
        Assert.Contains("anyway", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(dir));
        Cleanup(dir);
    }

    [Fact]
    public void A_failed_host_asks_again_and_yes_deletes_the_folder_anyway()
    {
        var dir = WriteRun();
        var (actions, _, _) = Actions(new[] { KeyPress.Of('y'), KeyPress.Enter /* results */, KeyPress.Of('y'), KeyPress.Enter /* deleted info */ },
            new DeployResult("H1", "Failed", "Access is denied"));
        actions.Delete(dir);

        Assert.False(Directory.Exists(dir));
        Cleanup(dir);
    }

    [Fact]
    public void Missing_run_json_is_reported_and_nothing_is_deleted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nlad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (actions, runner, console) = Actions(new[] { KeyPress.Enter });
        actions.Delete(dir);

        Assert.Null(runner.Last);
        Assert.Contains("run.json", console.Output);
        Assert.True(Directory.Exists(dir));
        Directory.Delete(dir, recursive: true);
    }
}
