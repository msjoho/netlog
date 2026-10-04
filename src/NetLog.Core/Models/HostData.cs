namespace NetLog.Core.Models;

public sealed record HostData(
    string HostName,
    HostStatus? Status,
    IReadOnlyList<ConnectionRecord> Connections);
