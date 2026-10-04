namespace NetLog.Core.Models;

/// <summary>Per-host heartbeat health, derived from the heartbeat age vs the stalled threshold.</summary>
public enum HostLiveness { Live, Stalled, NoHeartbeat }

public sealed record HostSummary(
    string HostName,
    DateTimeOffset? LastRunUtc,
    int ConnectionCount,
    int? TotalPolls,
    RunStats Stats,
    HostLiveness Liveness);

public sealed record RunStatusReport(
    string RunName,
    RunState State,
    DateTimeOffset StartUtc,
    DateTimeOffset StopUtc,
    TimeSpan Elapsed,
    DateTimeOffset? LastUpdateUtc,
    IReadOnlyList<HostSummary> Hosts,
    RunStats Stats,
    string ArchiveLevel = "Merged");
