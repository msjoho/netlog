using NetLog.Core.Models;

namespace NetLog.Core.Io;

/// <summary>Lists the runs under a root folder. A run is any immediate subfolder that holds a
/// readable run.json. Foreign or unreadable subfolders are skipped, never thrown on — the browser
/// stays usable even when a root holds unrelated content. Reads run.json only (no host scan).</summary>
public static class RunCatalog
{
    public static IReadOnlyList<RunListing> List(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return Array.Empty<RunListing>();

        var listings = new List<RunListing>();
        foreach (var dir in Directory.GetDirectories(rootPath))
        {
            var runJson = Path.Combine(dir, "run.json");
            if (!File.Exists(runJson)) continue;

            try
            {
                var config = RunConfigReader.ReadFile(runJson);
                listings.Add(new RunListing(
                    Path.GetFileName(dir),
                    config.Schedule.StartUtc,
                    config.Schedule.StopUtc,
                    config.Targets.Count));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
            {
                // Unreadable/foreign run.json — skip it rather than break the whole listing.
            }
        }

        return listings
            .OrderBy(l => l.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
