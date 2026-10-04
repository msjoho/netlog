namespace NetLog.Core.Models;

/// <summary>A ranked target/process. <see cref="Direction"/> is "in", "out", or "io" (both) —
/// the directions the underlying connections flowed.</summary>
public sealed record TopItem(string Name, int Count, string Direction = "");

public sealed record RunStats(
    int TotalConnections,
    int IncomingCount,
    int OutgoingCount,
    IReadOnlyList<TopItem> TopTargets,
    IReadOnlyList<TopItem> TopProcesses,
    IReadOnlyList<TopItem> TopUsers);
