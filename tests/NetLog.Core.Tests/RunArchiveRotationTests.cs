using NetLog.Core.Io;

namespace NetLog.Core.Tests;

public class RunArchiveRotationTests
{
    private static string MakeRun(params string[] hosts)
    {
        var run = Path.Combine(Path.GetTempPath(), "nlrot_" + Guid.NewGuid().ToString("N"));
        foreach (var h in hosts)
        {
            var dir = Path.Combine(run, "hosts", h);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "connections.csv"), $"header\nrow-{h}\n");
        }
        return run;
    }

    [Fact]
    public void Copies_cumulative_to_per_run_file_when_missing()
    {
        var run = MakeRun("S1", "S2");
        RunArchiveRotation.SnapshotCurrentRun(run, currentSequence: 1);
        Assert.Equal("header\nrow-S1\n", File.ReadAllText(Path.Combine(run, "hosts", "S1", "run1.csv")));
        Assert.True(File.Exists(Path.Combine(run, "hosts", "S2", "run1.csv")));
        Directory.Delete(run, recursive: true);
    }

    [Fact]
    public void Does_not_overwrite_an_existing_per_run_file()
    {
        var run = MakeRun("S1");
        var perRun = Path.Combine(run, "hosts", "S1", "run1.csv");
        File.WriteAllText(perRun, "collector-written\n");
        RunArchiveRotation.SnapshotCurrentRun(run, currentSequence: 1);
        Assert.Equal("collector-written\n", File.ReadAllText(perRun));
        Directory.Delete(run, recursive: true);
    }

    [Fact]
    public void Tolerates_missing_hosts_dir_and_hosts_without_archive()
    {
        var empty = Path.Combine(Path.GetTempPath(), "nlrot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        RunArchiveRotation.SnapshotCurrentRun(empty, 1);   // no hosts dir — no throw

        var run = Path.Combine(empty, "r2");
        Directory.CreateDirectory(Path.Combine(run, "hosts", "S1"));   // host without connections.csv
        RunArchiveRotation.SnapshotCurrentRun(run, 1);
        Assert.False(File.Exists(Path.Combine(run, "hosts", "S1", "run1.csv")));
        Directory.Delete(empty, recursive: true);
    }
}
