using NetLog.Core.Models;

namespace NetLog.Core.Csv;

/// <summary>Condenses connection records the same way the collector does (port of Invoke-NetLogCondense):
/// dedup by the flow key (Merged drops the initiator's ephemeral SourcePort), backfill blank host columns,
/// and merge identity fields idempotently — union (Usernames, ProcessPath), max (SeenCount, Connections),
/// min/max (FirstSeen/LastSeen). Connections at Merged additionally counts distinct contributor SourcePorts.
/// Never sum: archive rows get re-condensed together with their inputs.</summary>
public static class ConnectionCondenser
{
    public static IReadOnlyList<ConnectionRecord> Condense(IEnumerable<ConnectionRecord> records, string level)
    {
        var merged = level == "Merged";
        var byKey = new Dictionary<string, ConnectionRecord>();
        var order = new List<string>();
        var ports = new Dictionary<string, HashSet<string>>();

        foreach (var r in records)
        {
            var rec = merged ? r with { SourcePort = "" } : r with { Connections = "" };
            var key = $"{rec.Direction}|{rec.Source}|{rec.SourcePort}|{rec.Target}|{rec.TargetPort}|{rec.Process}";
            if (!byKey.TryGetValue(key, out var kept))
            {
                byKey[key] = rec;
                order.Add(key);
                if (merged) ports[key] = new HashSet<string>();
            }
            else
            {
                if (string.IsNullOrEmpty(kept.SourceHost) && !string.IsNullOrEmpty(rec.SourceHost))
                    kept = kept with { SourceHost = rec.SourceHost };
                if (string.IsNullOrEmpty(kept.TargetHost) && !string.IsNullOrEmpty(rec.TargetHost))
                    kept = kept with { TargetHost = rec.TargetHost };
                kept = kept with
                {
                    Usernames = ValueLists.Union(kept.Usernames, rec.Usernames),
                    ProcessPath = ValueLists.Union(kept.ProcessPath, rec.ProcessPath),
                    SeenCount = MaxNum(kept.SeenCount, rec.SeenCount),
                    Connections = merged ? MaxNum(kept.Connections, rec.Connections) : "",
                    FirstSeen = MinStamp(kept.FirstSeen, rec.FirstSeen),
                    LastSeen = MaxStamp(kept.LastSeen, rec.LastSeen),
                };
                byKey[key] = kept;
            }
            if (merged && r.SourcePort.Length > 0)
                ports[key].Add(r.SourcePort);
        }

        if (merged)
            foreach (var (key, set) in ports)
                if (set.Count > 0)
                    byKey[key] = byKey[key] with { Connections = MaxNum(byKey[key].Connections, set.Count.ToString()) };

        return order.Select(k => byKey[k]).ToList();
    }

    // Blank counts as absent; both-blank stays blank (unknown, never "0").
    private static string MaxNum(string a, string b)
    {
        var ha = long.TryParse(a, out var x);
        var hb = long.TryParse(b, out var y);
        if (!ha && !hb) return "";
        if (!hb) return x.ToString();
        if (!ha) return y.ToString();
        return Math.Max(x, y).ToString();
    }

    // ISO-8601 'o' UTC stamps compare correctly as ordinal strings; blanks are skipped.
    private static string MinStamp(string a, string b) =>
        a.Length == 0 ? b : b.Length == 0 ? a : string.CompareOrdinal(b, a) < 0 ? b : a;
    private static string MaxStamp(string a, string b) =>
        a.Length == 0 ? b : b.Length == 0 ? a : string.CompareOrdinal(b, a) > 0 ? b : a;
}
