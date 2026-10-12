using System.Text.Json;
using System.Text.Json.Serialization;
using NetLog.Core.Models;
using NetLog.Core.RunSpec;

namespace NetLog.Core.Io;

/// <summary>The per-root exclusion template: <c>&lt;root&gt;\exclusions.json</c> = { "rules": [...] }.
/// Missing ⇒ no rules. Corrupt or invalid ⇒ InvalidDataException — never silently empty, because
/// silently losing exclusions would start writing noise into new runs.</summary>
public static class ExclusionsStore
{
    public const string FileName = "exclusions.json";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string PathFor(string root) => Path.Combine(root, FileName);

    public static IReadOnlyList<ExclusionRule> Load(string root)
    {
        var path = PathFor(root);
        if (!File.Exists(path)) return Array.Empty<ExclusionRule>();
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<ExclusionRule>();

        Dto? dto;
        try { dto = JsonSerializer.Deserialize<Dto>(text, ReadOptions); }
        catch (JsonException ex) { throw new InvalidDataException($"{FileName} is not valid JSON: {ex.Message}", ex); }

        var rules = new List<ExclusionRule>();
        var i = 0;
        foreach (var e in dto?.Rules ?? new())
        {
            i++;
            if (e is null) throw new InvalidDataException($"{FileName} rule {i}: entry is null.");
            var rule = new ExclusionRule(e.Kind, e.Pattern, e.Direction ?? "both", e.Note, e.Port).Normalized();
            var error = ExclusionRuleParser.Validate(rule);
            if (error is not null) throw new InvalidDataException($"{FileName} rule {i}: {error}");
            rules.Add(rule);
        }
        return rules;
    }

    public static void Save(string root, IReadOnlyList<ExclusionRule> rules)
    {
        Directory.CreateDirectory(root);
        var dto = new Dto
        {
            Rules = rules.Select(r => new RuleDto { Kind = r.Kind, Pattern = r.Pattern, Direction = r.Direction, Note = r.Note, Port = r.Port }).ToList(),
        };
        File.WriteAllText(PathFor(root), JsonSerializer.Serialize(dto, WriteOptions));
    }

    private sealed class Dto { public List<RuleDto> Rules { get; set; } = new(); }

    private sealed class RuleDto
    {
        public string Kind { get; set; } = "";
        public string Pattern { get; set; } = "";
        public string? Direction { get; set; }
        public string? Note { get; set; }
        [JsonConverter(typeof(LenientStringConverter))]
        public string? Port { get; set; }   // accept "port": 445 (number) from a hand-edited template
    }
}
