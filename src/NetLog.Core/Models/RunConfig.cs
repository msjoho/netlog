namespace NetLog.Core.Models;

public sealed record RunSchedule(DateTimeOffset StartUtc, DateTimeOffset StopUtc);

public sealed record RunSettings(
    string ArchiveLevel,
    int PollIntervalSeconds,
    int ArchiveIntervalMinutes,
    int? CacheLifetimeDays,
    int? MaxCacheSizeMb,
    bool ResolveHostnames = true,
    bool Timestamps = false)
{
    /// <summary>Rows matching any rule are never written by the collector. Snapshot of the root
    /// template at creation; editable during the run (the collector re-reads run.json each cycle).</summary>
    public IReadOnlyList<ExclusionRule> Exclusions { get; init; } = Array.Empty<ExclusionRule>();
}

public sealed record RunConfig(
    string RunName,
    DateTimeOffset CreatedUtc,
    RunSchedule Schedule,
    IReadOnlyList<string> Targets,
    string ServiceAccount,
    RunSettings Settings,
    int RunSequence = 1);   // increments per collection window ("start another run") — names run<N>.csv
