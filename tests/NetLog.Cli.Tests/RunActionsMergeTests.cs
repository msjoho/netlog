using NetLog.Cli.Deploy;
using NetLog.Cli.Tui;
using NetLog.Core.Csv;
using NetLog.Core.Io;
using NetLog.Core.Models;
using NetLog.Core.RunSpec;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class RunActionsMergeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

    private static string WriteFullRun(bool live)
    {
        var dir = Path.Combine(Path.GetTempPath(), "nlam_" + Guid.NewGuid().ToString("N"));
        var host = Path.Combine(dir, "hosts", "H1");
        Directory.CreateDirectory(host);
        // Window already ended (so it's a stopped run) and Full level.
        var cfg = RunConfigFactory.Create(@"\\s\audit", "H1", "svc", "2026-06-10T00:00:00Z", "2026-06-12T00:00:00Z", null, "Full", 10, 60, 7, null, Now);
        RunConfigWriter.WriteFile(Path.Combine(dir, "run.json"), cfg);
        CsvConnectionWriter.WriteFile(Path.Combine(host, "connections.csv"), new[]
        {
            new ConnectionRecord("Outgoing", "H1", "", "51001", "10.0.0.5", "", "443", "chrome"),
            new ConnectionRecord("Outgoing", "H1", "", "51002", "10.0.0.5", "", "443", "chrome"),
        });
        if (live)
            File.WriteAllText(Path.Combine(host, "_status.json"),
                $"{{\"LastRunUtc\":\"{Now:yyyy-MM-ddTHH:mm:ssZ}\",\"Pid\":1,\"TotalConnections\":2,\"TotalPolls\":5}}");
        return dir;
    }

    private static RunActions Actions(Spectre.Console.IAnsiConsole console, IKeySource keys) =>
        new(console, keys, new FakeRunner(), () => Now);

    [Fact]
    public void Merge_condenses_in_place_when_confirmed_and_no_collector_is_live()
    {
        var dir = WriteFullRun(live: false);
        Actions(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Of('y'), KeyPress.Enter /* pause */ })).Merge(dir);

        var csv = Path.Combine(dir, "hosts", "H1", "connections.csv");
        Assert.Equal(1, CsvConnectionReader.ReadFile(csv).Count);   // collapsed
        Assert.True(Directory.Exists(Path.Combine(dir, "_archive")));

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Merge_is_blocked_while_a_collector_is_live()
    {
        var dir = WriteFullRun(live: true);
        // Only a pause key needed — it should bail before the confirm.
        Actions(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Enter })).Merge(dir);

        Assert.Equal(2, CsvConnectionReader.ReadFile(Path.Combine(dir, "hosts", "H1", "connections.csv")).Count);   // untouched
        Assert.False(Directory.Exists(Path.Combine(dir, "_archive")));

        Directory.Delete(dir, recursive: true);
    }
}
