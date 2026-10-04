using System.Text;

namespace NetLog.Core.Csv;

/// <summary>
/// Minimal RFC-4180-style single-line CSV field splitter. Handles quoted fields,
/// embedded commas, and doubled-quote escaping. Assumes no field contains a newline
/// (true for this app's fixed Direction/Source/Port/Process schema).
/// </summary>
public static class CsvParser
{
    /// <remarks>An empty input string returns a single empty field (new[]{""}), per RFC 4180.</remarks>
    public static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
