using NetLog.Core.Models;

namespace NetLog.Core.Csv;

public static class CsvConnectionReader
{
    /// <summary>
    /// Reads the per-host ARCHIVE connections.csv. Header-driven: native files have 14
    /// columns with identity fields (Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process,Usernames,ProcessPath,SeenCount,Connections,FirstSeen,LastSeen);
    /// 8-column (hostname-era) and 6-column (original) files are upgraded on read — Incoming
    /// rows swap Source/Target, host columns become blank. Returns empty for a missing,
    /// 0-byte, or header-only file. File.ReadAllLines strips a UTF-8 BOM automatically.
    /// </summary>
    public static IReadOnlyList<ConnectionRecord> ReadFile(string path)
    {
        if (!File.Exists(path)) return Array.Empty<ConnectionRecord>();

        var lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return Array.Empty<ConnectionRecord>();

        var header = CsvParser.ParseLine(lines[0]);
        var legacy = !header.Contains("SourceHost");
        var hasIdentity = header.Contains("Usernames");

        var result = new List<ConnectionRecord>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = CsvParser.ParseLine(lines[i]);
            if (legacy)
            {
                if (f.Length < 6) continue; // skip malformed rows defensively
                // Legacy semantics: Source was always the local host. Flow semantics put
                // the initiator in Source, so Incoming rows swap ends.
                bool swap = f[0] == "Incoming";
                result.Add(swap
                    ? new ConnectionRecord(f[0], f[3], "", f[4], f[1], "", f[2], f[5])
                    : new ConnectionRecord(f[0], f[1], "", f[2], f[3], "", f[4], f[5]));
            }
            else if (hasIdentity)
            {
                if (f.Length < 14) continue;
                result.Add(new ConnectionRecord(
                    f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7],
                    f[8], f[9], f[10], f[11], f[12], f[13]));
            }
            else
            {
                if (f.Length < 8) continue;
                result.Add(new ConnectionRecord(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]));
            }
        }
        return result;
    }
}
