using NetLog.Core.Models;

namespace NetLog.Core.RunSpec;

public static class RunConfigFactory
{
    /// <summary>
    /// Centralized run-config construction shared by `netlog new` and the TUI wizard.
    /// Throws ArgumentException/FormatException on invalid schedule input.
    /// </summary>
    public static RunConfig Create(
        string path, string targets, string account,
        string start, string? stop, string? duration,
        string archiveLevel, int pollSeconds, int archiveMinutes,
        int? cacheDays, int? cacheMb, DateTimeOffset now,
        bool resolveHostnames = true,
        bool timestamps = false,
        IReadOnlyList<ExclusionRule>? exclusions = null)
    {
        var schedule = RunSpecParser.BuildSchedule(start, stop, duration, now);

        var targetList = targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cacheLifetime = (cacheDays is null && cacheMb is null) ? 7 : cacheDays;
        var runName = DeriveRunName(path);

        return new RunConfig(
            runName, now, schedule, targetList, account,
            new RunSettings(archiveLevel, pollSeconds, archiveMinutes, cacheLifetime, cacheMb, resolveHostnames, timestamps)
            { Exclusions = exclusions ?? Array.Empty<ExclusionRule>() });
    }

    /// <summary>
    /// Produces a copy of an existing run config with a new collection window (start + stop/duration),
    /// keeping the same targets, account, and settings. Used to start another run over the same run
    /// folder — connections accumulate cumulatively into the same archive. Throws on invalid schedule input.
    /// </summary>
    public static RunConfig WithNewSchedule(
        RunConfig existing, string start, string? stop, string? duration, DateTimeOffset now)
    {
        var schedule = RunSpecParser.BuildSchedule(start, stop, duration, now);
        // A new window is a new run: the bumped sequence makes collectors write run<N>.csv
        // for this window while still unioning into the cumulative connections.csv.
        return existing with { Schedule = schedule, CreatedUtc = now, RunSequence = existing.RunSequence + 1 };
    }

    /// <summary>
    /// Produces a copy with the in-run-editable values changed: the collection end (stop/duration,
    /// relative to the unchanged start) and the mutable settings (poll interval, archive interval,
    /// cache lifetime) and the exclusion list (null keeps the current one). Identity and immutable settings (name, targets, account, archive level, start,
    /// run sequence) are preserved. Throws on invalid schedule input.
    /// </summary>
    public static RunConfig WithEdits(
        RunConfig existing, string? stop, string? duration,
        int pollSeconds, int archiveMinutes, int? cacheDays, int? cacheMb, DateTimeOffset now,
        IReadOnlyList<ExclusionRule>? exclusions = null)
    {
        var schedule = RunSpecParser.BuildSchedule(existing.Schedule.StartUtc.ToString("o"), stop, duration, now);
        var settings = existing.Settings with
        {
            PollIntervalSeconds = pollSeconds,
            ArchiveIntervalMinutes = archiveMinutes,
            CacheLifetimeDays = cacheDays,
            MaxCacheSizeMb = cacheMb,
            Exclusions = exclusions ?? existing.Settings.Exclusions,
        };
        return existing with { Schedule = schedule, Settings = settings };
    }

    /// <summary>
    /// Derives the run name from the trailing path segment. Unlike Path.GetFileName, this also
    /// works when the path is a UNC share root (e.g. <c>\\server\Share</c>), which the framework
    /// treats as all-root and would otherwise yield an empty name. Splits on both separators and
    /// takes the last non-empty segment so that "Share" is used as the run name in that case.
    /// </summary>
    private static string DeriveRunName(string path)
    {
        var trimmed = (path ?? string.Empty).TrimEnd('\\', '/');
        var runName = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(runName))
        {
            // GetFileName yields "" for a UNC share root (\\server\share), which the framework
            // treats as all-root. Fall back to the share segment, but a bare UNC server (\\server)
            // has no share to name the run after.
            var isUnc = trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal);
            var segments = trimmed.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (isUnc && segments.Length >= 2)
                runName = segments[^1];
        }
        if (string.IsNullOrWhiteSpace(runName))
            throw new ArgumentException(
                $"Cannot derive a run name from path '{path}'. Use a run folder such as \\\\server\\share\\<run>.",
                nameof(path));
        return runName;
    }
}
