namespace NetLog.Core.Models;

/// <summary>Heartbeat written by a host's collector (_status.json). The host name is not
/// in the file — it comes from the containing folder (see RunReader).</summary>
public sealed record HostStatus(
    DateTimeOffset LastRunUtc,
    int Pid,
    int TotalConnections,
    int TotalPolls,
    int? ExcludedCount = null);   // absent in status files from collectors without exclusions => rendered "n/a"
