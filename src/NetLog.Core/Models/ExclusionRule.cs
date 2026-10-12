namespace NetLog.Core.Models;

/// <summary>One exclusion rule. Kind is process | user | host; Direction is in | out | both; Port is an
/// optional service-port spec ("443", "80,443", "1024-65535") that further gates the rule — the service
/// port is the remote port of an outgoing connection and the local port of an incoming one.
/// Matching happens only on the collector (PowerShell); C# validates syntax and stores rules.</summary>
public sealed record ExclusionRule(string Kind, string Pattern, string Direction = "both", string? Note = null, string? Port = null)
{
    public const string KindProcess = "process";
    public const string KindUser = "user";
    public const string KindHost = "host";

    /// <summary>Lower-cases kind/direction, defaults a blank direction to "both", blanks the port to null
    /// and strips whitespace from it. Use when reading files.</summary>
    public ExclusionRule Normalized() => this with
    {
        Kind = (Kind ?? "").Trim().ToLowerInvariant(),
        Pattern = (Pattern ?? "").Trim(),
        Direction = string.IsNullOrWhiteSpace(Direction) ? "both" : Direction.Trim().ToLowerInvariant(),
        Note = string.IsNullOrWhiteSpace(Note) ? null : Note,
        Port = string.IsNullOrWhiteSpace(Port) ? null : string.Concat(Port.Where(c => !char.IsWhiteSpace(c))),
    };
}
