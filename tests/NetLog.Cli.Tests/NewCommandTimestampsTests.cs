using NetLog.Cli.Commands;
using NetLog.Core.Io;
using Spectre.Console.Cli;

namespace NetLog.Cli.Tests;

/// <summary>Drives `netlog new` end to end (parsing included) against a temp run folder.</summary>
public class NewCommandTimestampsTests
{
    private static string TempRun()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nlts_" + Guid.NewGuid().ToString("N"), "audit");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static bool CreateAndReadTimestamps(string runPath, params string[] extraArgs)
    {
        var app = new CommandApp();
        app.Configure(c => { c.AddCommand<NewCommand>("new"); c.PropagateExceptions(); c.Settings.StrictParsing = true; });
        var args = new List<string> { "new", "--path", runPath, "--targets", "S1", "--account", @"D\svc", "--duration", "1d" };
        args.AddRange(extraArgs);
        Assert.Equal(0, app.Run(args));
        return RunConfigReader.ReadFile(Path.Combine(runPath, "run.json")).Settings.Timestamps;
    }

    [Fact]
    public void New_records_timestamps_by_default()
    {
        var run = TempRun();
        Assert.True(CreateAndReadTimestamps(run));
        Directory.Delete(Path.GetDirectoryName(run)!, recursive: true);
    }

    [Fact]
    public void No_timestamps_switches_them_off()
    {
        var run = TempRun();
        Assert.False(CreateAndReadTimestamps(run, "--no-timestamps"));
        Directory.Delete(Path.GetDirectoryName(run)!, recursive: true);
    }

    [Fact]
    public void Legacy_timestamps_flag_is_still_accepted()
    {
        var run = TempRun();
        Assert.True(CreateAndReadTimestamps(run, "--timestamps"));
        Directory.Delete(Path.GetDirectoryName(run)!, recursive: true);
    }
}
