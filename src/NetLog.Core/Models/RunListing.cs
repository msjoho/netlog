namespace NetLog.Core.Models;

/// <summary>A run as seen in the runs browser: one subfolder of a root. The folder name is
/// the run name; the full path is never surfaced. State is derived from the window at render time.</summary>
public sealed record RunListing(
    string FolderName,
    DateTimeOffset StartUtc,
    DateTimeOffset StopUtc,
    int HostCount);
