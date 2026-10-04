namespace NetLog.Core.Models;

public sealed record RunSchedule(DateTimeOffset StartUtc, DateTimeOffset StopUtc);

public sealed record RunSettings(
    string ArchiveLevel,
    int PollIntervalSeconds,
    int ArchiveIntervalMinutes,
    int? CacheLifetimeDays,
    int? MaxCacheSizeMb,
    bool ResolveHostnames = true,
    bool Timestamps = false);

public sealed record RunConfig(
    string RunName,
    DateTimeOffset CreatedUtc,
    RunSchedule Schedule,
    IReadOnlyList<string> Targets,
    string ServiceAccount,
    RunSettings Settings,
    int RunSequence = 1);   // increments per collection window ("start another run") — names run<N>.csv
