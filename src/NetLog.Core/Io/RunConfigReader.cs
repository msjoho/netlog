using System.Text.Json;
using System.Text.Json.Serialization;
using NetLog.Core.Models;

namespace NetLog.Core.Io;

public static class RunConfigReader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static RunConfig ReadFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("run.json not found", path);

        // File.ReadAllText strips a UTF-8 BOM, so System.Text.Json sees clean JSON.
        var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path), Options)
                  ?? throw new InvalidDataException($"run.json is empty or invalid: {path}");

        if (dto.Schedule is null)
            throw new InvalidDataException($"run.json is missing 'schedule': {path}");
        if (dto.Settings is null)
            throw new InvalidDataException($"run.json is missing 'settings': {path}");
        var targets = (IReadOnlyList<string>?)dto.Targets ?? Array.Empty<string>();

        return new RunConfig(
            dto.RunName,
            dto.CreatedUtc,
            new RunSchedule(dto.Schedule.StartUtc, dto.Schedule.StopUtc),
            targets,
            dto.ServiceAccount,
            new RunSettings(
                dto.Settings.ArchiveLevel,
                dto.Settings.PollIntervalSeconds,
                dto.Settings.ArchiveIntervalMinutes,
                dto.Settings.CacheLifetimeDays,
                dto.Settings.MaxCacheSizeMb,
                dto.Settings.ResolveHostnames ?? true,      // absent in configs from older CLIs
                dto.Settings.Timestamps ?? false)           // absent in configs from older CLIs
            {
                // Absent / null / non-array in older or hand-edited configs => no rules. Each rule is
                // normalised (lower-case kind / direction, default "both"); NOT validated here - a bad
                // hand-edited rule must not make the run unreadable; the collector logs and skips it.
                Exclusions = ReadExclusions(dto.Settings.Exclusions)
                    .Select(e => new ExclusionRule(e.Kind, e.Pattern, e.Direction ?? "both", e.Note, e.Port).Normalized())
                    .ToList(),
            },
            dto.RunSequence ?? 1);                         // absent in configs from older CLIs
    }

    private static List<ExclusionDto> ReadExclusions(JsonElement? el)
    {
        if (el is not { ValueKind: JsonValueKind.Array } arr) return new();
        return JsonSerializer.Deserialize<List<ExclusionDto>>(arr.GetRawText(), Options) ?? new();
    }

    private sealed class ExclusionDto
    {
        public string Kind { get; set; } = "";
        public string Pattern { get; set; } = "";
        public string? Direction { get; set; }
        public string? Note { get; set; }
        [JsonConverter(typeof(LenientStringConverter))]
        public string? Port { get; set; }   // a hand-edited "port": 445 (number) must not make the run unreadable
    }

    private sealed class Dto
    {
        public string RunName { get; set; } = "";
        public int? RunSequence { get; set; }
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
        public bool? ResolveHostnames { get; set; }
        public bool? Timestamps { get; set; }
        public JsonElement? Exclusions { get; set; }
    }
}
