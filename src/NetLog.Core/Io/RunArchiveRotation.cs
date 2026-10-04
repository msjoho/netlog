namespace NetLog.Core.Io;

public static class RunArchiveRotation
{
    /// <summary>
    /// Before a new run window starts, makes sure the finishing run has its per-run file:
    /// copies each host's cumulative connections.csv to run&lt;currentSequence&gt;.csv where that
    /// file does not exist yet. Collectors deployed with per-run support write it themselves
    /// (no copy happens); this migrates runs whose collectors predate the feature.
    /// </summary>
    public static void SnapshotCurrentRun(string runPath, int currentSequence)
    {
        var hostsDir = Path.Combine(runPath, "hosts");
        if (!Directory.Exists(hostsDir)) return;
        foreach (var hostDir in Directory.GetDirectories(hostsDir))
        {
            var cumulative = Path.Combine(hostDir, "connections.csv");
            var perRun = Path.Combine(hostDir, $"run{currentSequence}.csv");
            if (File.Exists(cumulative) && !File.Exists(perRun))
                File.Copy(cumulative, perRun);
        }
    }
}
