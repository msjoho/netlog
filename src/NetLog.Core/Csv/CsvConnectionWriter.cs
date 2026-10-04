using System.Text;
using NetLog.Core.Models;

namespace NetLog.Core.Csv;

/// <summary>Writes the 14-column connections.csv that <see cref="CsvConnectionReader"/> reads back.
/// Fields containing a comma, quote, or newline are quoted with doubled inner quotes (RFC 4180).</summary>
public static class CsvConnectionWriter
{
    private const string Header =
        "Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process," +
        "Usernames,ProcessPath,SeenCount,Connections,FirstSeen,LastSeen";

    public static void WriteFile(string path, IEnumerable<ConnectionRecord> records)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        foreach (var r in records)
        {
            sb.Append(string.Join(',', new[]
            {
                Q(r.Direction), Q(r.Source), Q(r.SourceHost), Q(r.SourcePort),
                Q(r.Target), Q(r.TargetHost), Q(r.TargetPort), Q(r.Process),
                Q(r.Usernames), Q(r.ProcessPath), Q(r.SeenCount), Q(r.Connections),
                Q(r.FirstSeen), Q(r.LastSeen),
            })).Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }

    private static string Q(string field) =>
        field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
}
