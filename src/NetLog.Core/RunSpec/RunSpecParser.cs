using System.Globalization;
using NetLog.Core.Models;

namespace NetLog.Core.RunSpec;

public static class RunSpecParser
{
    /// <summary>Parses a duration like "7d", "12h", "90m" into a positive TimeSpan.</summary>
    public static TimeSpan ParseDuration(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length < 2)
            throw new FormatException($"Invalid duration '{input}'. Use forms like 7d, 12h, 90m.");

        var unit = char.ToLowerInvariant(input[^1]);
        var numberPart = input[..^1];
        if (!int.TryParse(numberPart, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new FormatException($"Invalid duration '{input}'. The amount must be a positive integer.");

        return unit switch
        {
            'd' => TimeSpan.FromDays(value),
            'h' => TimeSpan.FromHours(value),
            'm' => TimeSpan.FromMinutes(value),
            _ => throw new FormatException($"Invalid duration unit in '{input}'. Use d, h, or m.")
        };
    }

    /// <summary>Resolves a start spec: "now" -> the provided clock; otherwise an ISO-8601 instant.</summary>
    public static DateTimeOffset ResolveStart(string start, DateTimeOffset now)
    {
        if (string.Equals(start, "now", StringComparison.OrdinalIgnoreCase)) return now;
        return DateTimeOffset.Parse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    /// <summary>Builds a schedule from a start spec plus EITHER an explicit stop OR a duration.</summary>
    public static RunSchedule BuildSchedule(string start, string? stop, string? duration, DateTimeOffset now)
    {
        var startUtc = ResolveStart(start, now);

        DateTimeOffset stopUtc;
        if (!string.IsNullOrWhiteSpace(stop) && !string.IsNullOrWhiteSpace(duration))
            throw new ArgumentException("Specify either --stop or --duration, not both.");
        else if (!string.IsNullOrWhiteSpace(stop))
            stopUtc = DateTimeOffset.Parse(stop, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        else if (!string.IsNullOrWhiteSpace(duration))
            stopUtc = startUtc + ParseDuration(duration);
        else
            throw new ArgumentException("A run needs an end: pass --stop <iso> or --duration <7d>.");

        if (stopUtc <= startUtc)
            throw new ArgumentException("Stop time must be after start time.");

        return new RunSchedule(startUtc, stopUtc);
    }
}
