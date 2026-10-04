using NetLog.Core.Io;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class RunCatalogTests
{
    private static readonly DateTimeOffset S = new(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);

    private static string TempRoot()
    {
        var p = Path.Combine(Path.GetTempPath(), "nlroot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    private static void WriteRun(string root, string folder, DateTimeOffset start, DateTimeOffset stop, params string[] targets)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        var config = new RunConfig(folder, start.AddDays(-1), new RunSchedule(start, stop), targets, "svc",
            new RunSettings("Merged", 10, 60, 7, null));
        RunConfigWriter.WriteFile(Path.Combine(dir, "run.json"), config);
    }

    [Fact]
    public void Lists_each_subfolder_with_a_run_json()
    {
        var root = TempRoot();
        WriteRun(root, "audit-a", S, S.AddDays(7), "H1", "H2", "H3");
        WriteRun(root, "audit-b", S.AddDays(1), S.AddDays(8), "H1");

        var listings = RunCatalog.List(root);

        Assert.Equal(2, listings.Count);
        var a = listings.Single(l => l.FolderName == "audit-a");
        Assert.Equal(S, a.StartUtc);
        Assert.Equal(S.AddDays(7), a.StopUtc);
        Assert.Equal(3, a.HostCount);
    }

    [Fact]
    public void Skips_foreign_subfolders_and_loose_files()
    {
        var root = TempRoot();
        Directory.CreateDirectory(Path.Combine(root, "not-a-run"));   // no run.json
        File.WriteAllText(Path.Combine(root, "loose.txt"), "x");
        WriteRun(root, "real", S, S.AddDays(1), "H1");

        Assert.Equal("real", Assert.Single(RunCatalog.List(root)).FolderName);
    }

    [Fact]
    public void Skips_unreadable_run_json()
    {
        var root = TempRoot();
        var bad = Path.Combine(root, "broken");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "run.json"), "{ not valid ");
        WriteRun(root, "good", S, S.AddDays(1), "H1");

        Assert.Equal("good", Assert.Single(RunCatalog.List(root)).FolderName);
    }

    [Fact]
    public void Missing_root_returns_empty()
    {
        Assert.Empty(RunCatalog.List(Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void Ordered_by_folder_name_case_insensitively()
    {
        var root = TempRoot();
        WriteRun(root, "Zebra", S, S.AddDays(1), "H1");
        WriteRun(root, "alpha", S, S.AddDays(1), "H1");

        Assert.Equal(new[] { "alpha", "Zebra" }, RunCatalog.List(root).Select(l => l.FolderName).ToArray());
    }
}
