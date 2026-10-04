namespace NetLog.Core.Models;

/// <summary>
/// One logged connection in flow semantics: Source = initiator, Target = acceptor.
/// Ports are strings (Merged level leaves SourcePort blank). Host names and the
/// identity fields (Usernames, ProcessPath, SeenCount, Connections, FirstSeen,
/// LastSeen) are enrichment and may be blank — blank counters mean "unknown"
/// (legacy file), never zero.
/// </summary>
public sealed record ConnectionRecord(
    string Direction,
    string Source,
    string SourceHost,
    string SourcePort,
    string Target,
    string TargetHost,
    string TargetPort,
    string Process,
    string Usernames = "",
    string ProcessPath = "",
    string SeenCount = "",
    string Connections = "",
    string FirstSeen = "",
    string LastSeen = "");
