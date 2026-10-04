namespace NetLog.Core.Csv;

/// <summary>Semicolon-joined value lists (Usernames, ProcessPath): sorted, case-insensitively
/// deduplicated union — mirrors the collector's Merge-NetLogValueList.</summary>
public static class ValueLists
{
    public static string Union(string a, string b)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in (a + ";" + b).Split(';'))
            if (v.Length > 0) set.Add(v);
        return string.Join(';', set);
    }
}
