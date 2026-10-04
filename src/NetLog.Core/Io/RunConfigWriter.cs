using System.Text.Json;
using System.Text.Json.Serialization;
using NetLog.Core.Models;

namespace NetLog.Core.Io;

public static class RunConfigWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static void WriteFile(string path, RunConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var dto = new Dto
        {
            RunName = config.RunName,
            RunSequence = config.RunSequence,
            CreatedUtc = config.CreatedUtc,
            Schedule = new ScheduleDto { StartUtc = config.Schedule.StartUtc, StopUtc = config.Schedule.StopUtc },
            Targets = config.Targets.ToList(),
            ServiceAccount = config.ServiceAccount,
            Settings = new SettingsDto
            {
                ArchiveLevel = config.Settings.ArchiveLevel,
                PollIntervalSeconds = config.Settings.PollIntervalSeconds,
                ArchiveIntervalMinutes = config.Settings.ArchiveIntervalMinutes,
                CacheLifetimeDays = config.Settings.CacheLifetimeDays,
                MaxCacheSizeMb = config.Settings.MaxCacheSizeMb,
                ResolveHostnames = config.Settings.ResolveHostnames,
                Timestamps = config.Settings.Timestamps
            }
        };

        // BOM-free UTF-8 (System.Text.Json writes no BOM via File.WriteAllText with default UTF8 no-BOM).
        File.WriteAllText(path, JsonSerializer.Serialize(dto, Options));
    }

    private sealed class Dto
    {
        public string RunName { get; set; } = "";
        public int RunSequence { get; set; } = 1;
        public DateTimeOffset CreatedUtc { get; set; }
        public ScheduleDto Schedule { get; set; } = new();
        public List<string> Targets { get; set; } = new();
        public string ServiceAccount { get; set; } = "";
        public SettingsDto Settings { get; set; } = new();
    }

    private sealed class ScheduleDto
    {
        public DateTimeOffset StartUtc { get; set; }
        public DateTimeOffset StopUtc { get; set; }
    }

    private sealed class SettingsDto
    {
        public string ArchiveLevel { get; set; } = "";
        public int PollIntervalSeconds { get; set; }
        public int ArchiveIntervalMinutes { get; set; }
        public int? CacheLifetimeDays { get; set; }
        public int? MaxCacheSizeMb { get; set; }
        public bool ResolveHostnames { get; set; } = true;
        public bool Timestamps { get; set; }
    }
}
