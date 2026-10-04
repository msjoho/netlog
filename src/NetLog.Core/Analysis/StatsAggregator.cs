using NetLog.Core.Models;

namespace NetLog.Core.Analysis;

public static class StatsAggregator
{
    public static RunStats Aggregate(IEnumerable<ConnectionRecord> records)
    {
        var list = records as IReadOnlyCollection<ConnectionRecord> ?? records.ToList();

        int incoming = list.Count(r => r.Direction == "Incoming");
        int outgoing = list.Count(r => r.Direction == "Outgoing");

        return new RunStats(
            list.Count,
            incoming,
            outgoing,
            TopPeers(list),
            Top(list, r => r.Process),
            TopUsers(list));
    }

    /// <summary>
    /// Top remote peers. Flow semantics: the remote end is the Source on Incoming rows
    /// (the initiator) and the Target on Outgoing rows. Grouped by IP; labelled
    /// "name (ip)" using the group's first resolved hostname, bare IP otherwise.
    /// </summary>
    private static IReadOnlyList<TopItem> TopPeers(IEnumerable<ConnectionRecord> list) =>
        list.Select(r => r.Direction == "Incoming"
                ? (Ip: r.Source, Host: r.SourceHost, r.Direction)
                : (Ip: r.Target, Host: r.TargetHost, r.Direction))
            .GroupBy(p => p.Ip)
            .Select(g =>
            {
                var host = g.Select(p => p.Host).FirstOrDefault(h => !string.IsNullOrEmpty(h));
                var label = string.IsNullOrEmpty(host) ? g.Key : $"{host} ({g.Key})";
                return new TopItem(label, g.Count(), Direction(g.Select(p => p.Direction)));
            })
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();

    private static IReadOnlyList<TopItem> Top(IEnumerable<ConnectionRecord> list, Func<ConnectionRecord, string> key) =>
        list.GroupBy(key)
            .Select(g => new TopItem(g.Key, g.Count(), Direction(g.Select(r => r.Direction))))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();

    /// <summary>Top users: each row counts once per user in its semicolon Usernames list; blanks
    /// (legacy rows, unresolved users) are skipped. Windows accounts are case-insensitive — grouped
    /// ignoring case, labelled with the first-seen casing.</summary>
    private static IReadOnlyList<TopItem> TopUsers(IEnumerable<ConnectionRecord> list) =>
        list.SelectMany(r => r.Usernames.Split(';', StringSplitOptions.RemoveEmptyEntries)
                              .Select(u => (User: u, r.Direction)))
            .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopItem(g.First().User, g.Count(), Direction(g.Select(x => x.Direction))))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();

    /// <summary>"in" / "out" / "io" (both) from a group's connection directions.</summary>
    private static string Direction(IEnumerable<string> directions)
    {
        bool incoming = false, outgoing = false;
        foreach (var d in directions)
        {
            if (d == "Incoming") incoming = true;
            else outgoing = true;
            if (incoming && outgoing) break;
        }
        return incoming && outgoing ? "io" : incoming ? "in" : "out";
    }
}
