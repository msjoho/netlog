using NetLog.Core.Csv;

namespace NetLog.Core.Io;

public sealed record MergeResult(string HostName, int Before, int After);

/// <summary>Merges a run's per-host <c>connections.csv</c> (Full → Merged) in place, moving the original
/// Full file to <c>&lt;run&gt;/_archive/&lt;stamp&gt;/hosts/&lt;host&gt;/connections.csv</c> so the raw data is kept.
/// Only <c>connections.csv</c> is touched; per-window <c>run&lt;N&gt;.csv</c> files are left as-is.</summary>
public static class RunMerger
{
    public static IReadOnlyList<MergeResult> MergeConnections(string runFolder, string archiveStamp)
    {
        var results = new List<MergeResult>();
        var hostsRoot = Path.Combine(runFolder, "hosts");
        if (!Directory.Exists(hostsRoot)) return results;

        foreach (var dir in Directory.GetDirectories(hostsRoot).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var host = Path.GetFileName(dir);
            var csv = Path.Combine(dir, "connections.csv");
            if (!File.Exists(csv)) continue;

            var records = CsvConnectionReader.ReadFile(csv);
            var merged = ConnectionCondenser.Condense(records, "Merged");

            var archiveDir = Path.Combine(runFolder, "_archive", archiveStamp, "hosts", host);
            Directory.CreateDirectory(archiveDir);
            File.Move(csv, Path.Combine(archiveDir, "connections.csv"));

            CsvConnectionWriter.WriteFile(csv, merged);
            results.Add(new MergeResult(host, records.Count, merged.Count));
        }

        return results;
    }
}
