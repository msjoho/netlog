namespace NetLog.Cli.Rendering;

/// <summary>Pure time formatting for the TUI. All absolute timestamps are stored UTC and rendered
/// in a target timezone (the controller's local zone by default), with the offset shown so the
/// reading is unambiguous regardless of where each collector host lives.</summary>
public static class TimeFormat
{
    /// <summary>"yyyy-MM-dd HH:mm" in the given zone.</summary>
    public static string Local(DateTimeOffset utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTime(utc, tz).ToString("yyyy-MM-dd HH:mm");

    /// <summary>"MMM dd HH:mm" in the given zone (invariant culture for stable month names).</summary>
    public static string LocalShort(DateTimeOffset utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTime(utc, tz).ToString("MMM dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Signed "+HH:mm" offset of the zone at the given instant (handles DST).</summary>
    public static string OffsetLabel(TimeZoneInfo tz, DateTimeOffset at)
    {
        var off = tz.GetUtcOffset(at);
        var sign = off < TimeSpan.Zero ? "-" : "+";
        var abs = off.Duration();
        return $"{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }

    /// <summary>Coarsest-unit relative age: "8s ago" / "6m ago" / "1h ago" / "3d ago".</summary>
    public static string Ago(DateTimeOffset last, DateTimeOffset now)
    {
        var d = now - last;
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        if (d.TotalSeconds < 60) return $"{(int)d.TotalSeconds}s ago";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m ago";
        if (d.TotalHours   < 24) return $"{(int)d.TotalHours}h ago";
        return $"{(int)d.TotalDays}d ago";
    }

    /// <summary>"2d 14h 03m" when ≥1 day, otherwise "14h 03m". Minutes zero-padded.</summary>
    public static string Duration(TimeSpan d)
    {
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        return d.Days >= 1
            ? $"{d.Days}d {d.Hours}h {d.Minutes:00}m"
            : $"{d.Hours}h {d.Minutes:00}m";
    }
}
