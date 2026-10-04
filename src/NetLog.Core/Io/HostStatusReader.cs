using System.Text.Json;
using NetLog.Core.Models;

namespace NetLog.Core.Io;

public static class HostStatusReader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Reads _status.json. Returns null if the file is absent (host deployed but not archived yet).</summary>
    public static HostStatus? ReadFile(string path)
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(text, Options);
            if (dto is null) return null;
            return new HostStatus(dto.LastRunUtc, dto.Pid, dto.TotalConnections, dto.TotalPolls);
        }
        catch (JsonException)
        {
            // A corrupt / half-written heartbeat is treated like a missing one (best-effort file).
            return null;
        }
    }

    private sealed class Dto
    {
        public DateTimeOffset LastRunUtc { get; set; }
        public int Pid { get; set; }
        public int TotalConnections { get; set; }
        public int TotalPolls { get; set; }
    }
}
