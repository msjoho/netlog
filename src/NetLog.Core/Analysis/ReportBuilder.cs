using NetLog.Core.Models;

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
                Liveness(h.Status?.LastRunUtc, now, threshold)))
            .ToList();

        var stats = StatsAggregator.Aggregate(data.Hosts.SelectMany(h => h.Connections));

        return new RunStatusReport(
            data.Config.RunName,
            StateDeriver.DeriveState(data.Config, data.Hosts, now),
            start,
            stop,
            elapsed,
            StateDeriver.LastUpdate(data.Hosts),
            hostSummaries,
            stats,
            data.Config.Settings.ArchiveLevel);
    }

    private static HostLiveness Liveness(DateTimeOffset? lastRun, DateTimeOffset now, TimeSpan threshold)
    {
        if (lastRun is not { } last) return HostLiveness.NoHeartbeat;
        return (now - last) > threshold ? HostLiveness.Stalled : HostLiveness.Live;
    }
}
