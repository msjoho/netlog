namespace NetLog.Cli.Deploy;

public static class DeployPaths
{
    /// <summary>Locates the repo's collector folder relative to the running assembly (or env override).</summary>
    public static string DefaultCollectorSource()
    {
        var overridePath = Environment.GetEnvironmentVariable("NETLOG_COLLECTOR_SOURCE");
        if (!string.IsNullOrEmpty(overridePath)) return overridePath;
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "collector");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return "collector";
    }
}
