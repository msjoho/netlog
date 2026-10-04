namespace NetLog.Core.Models;

public sealed record RunData(
    RunConfig Config,
    IReadOnlyList<HostData> Hosts);
