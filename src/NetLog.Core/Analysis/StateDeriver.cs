using NetLog.Core.Models;

namespace NetLog.Core.Analysis;

public static class StateDeriver
{
    public static TimeSpan StalledThreshold(RunConfig config) =>
        TimeSpan.FromMinutes(Math.Max(3 * config.Settings.ArchiveIntervalMinutes, 15));

    public static DateTimeOffset? LastUpdate(IReadOnlyList<HostData> hosts) =>
        hosts.Where(h => h.Status is not null)
             .Select(h => (DateTimeOffset?)h.Status!.LastRunUtc)
             .Max();

    public static RunState DeriveState(RunConfig config, IReadOnlyList<HostData> hosts, DateTimeOffset now)
    {
        if (now < config.Schedule.StartUtc) return RunState.Scheduled;
        if (now >= config.Schedule.StopUtc) return RunState.Completed;

        var last = LastUpdate(hosts);
        if (last is null) return RunState.Stalled;
        return (now - last.Value) > StalledThreshold(config) ? RunState.Stalled : RunState.Running;
    }
}
