using System.Text.Json;
using NetLog.Core.Models;

namespace NetLog.Core.Io;

public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly string _path;

    public ProfileStore(string path) => _path = path;

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetworkActivityLogger",
            "profile.json");

    public Models.Profile Load()
    {
        if (!File.Exists(_path)) return new Models.Profile(Array.Empty<string>());
        var text = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(text)) return new Models.Profile(Array.Empty<string>());
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(text, Options);
            return new Models.Profile(dto?.RecentRootPaths ?? new List<string>());
        }
        catch (JsonException)
        {
            // A corrupt profile.json is treated as empty (best-effort convenience file).
            return new Models.Profile(Array.Empty<string>());
        }
    }

    public void Save(Models.Profile profile)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var dto = new Dto { RecentRootPaths = profile.RecentRootPaths.ToList() };
        File.WriteAllText(_path, JsonSerializer.Serialize(dto, Options));
    }

    /// <summary>Returns a new profile with rootPath at the front, de-duplicated case-insensitively, capped at max.</summary>
    public static Models.Profile AddRecent(Models.Profile profile, string rootPath, int max = 10)
    {
        if (max <= 0) return new Models.Profile(Array.Empty<string>());
        var list = new List<string> { rootPath };
        foreach (var existing in profile.RecentRootPaths)
        {
            if (!string.Equals(existing, rootPath, StringComparison.OrdinalIgnoreCase))
                list.Add(existing);
        }
        if (list.Count > max) list = list.Take(max).ToList();
        return new Models.Profile(list);
    }

    private sealed class Dto
    {
        public List<string> RecentRootPaths { get; set; } = new();
    }
}
