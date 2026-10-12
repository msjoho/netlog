using NetLog.Core.Models;
using NetLog.Core.RunSpec;

namespace NetLog.Core.Analysis;

public static class ReportBuilder
{
    public static RunStatusReport Build(RunData data, DateTimeOffset now)
    {
        var start = data.Config.Schedule.StartUtc;
        var stop = data.Config.Schedule.StopUtc;

        TimeSpan elapsed =
            now < start ? TimeSpan.Zero :
            now >= stop ? stop - start :
            now - start;

        var threshold = StateDeriver.StalledThreshold(data.Config);
        var hostSummaries = data.Hosts
            .Select(h => new HostSummary(
                h.HostName, h.Status?.LastRunUtc, h.Connections.Count, h.Status?.TotalPolls,
                StatsAggregator.Aggregate(h.Connections),
                Liveness(h.Status?.LastRunUtc, now, threshold),
                h.Status?.ExcludedCount))
            .ToList();

        var stats = StatsAggregator.Aggregate(data.Hosts.SelectMany(h => h.Connections));

        var reported = data.Hosts.Select(h => h.Status?.ExcludedCount).Where(c => c is not null).Select(c => c!.Value).ToList();
        int? excludedTotal = reported.Count == 0 ? null : reported.Sum();

        return new RunStatusReport(
            data.Config.RunName,
            StateDeriver.DeriveState(data.Config, data.Hosts, now),
            start,
            stop,
            elapsed,
            StateDeriver.LastUpdate(data.Hosts),
            hostSummaries,
            stats,
            data.Config.Settings.ArchiveLevel,
            // Only the rules the collector applies (it skips invalid ones).
            data.Config.Settings.Exclusions.Count(r => ExclusionRuleParser.Validate(r) is null),
            excludedTotal);
    }

    private static HostLiveness Liveness(DateTimeOffset? lastRun, DateTimeOffset now, TimeSpan threshold)
    {
        if (lastRun is not { } last) return HostLiveness.NoHeartbeat;
        return (now - last) > threshold ? HostLiveness.Stalled : HostLiveness.Live;
    }
}
