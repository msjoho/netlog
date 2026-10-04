using NetLog.Core.Models;
using NetLog.Core.Io;

namespace NetLog.Core.Tests;

public class ProfileStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "nlprof_" + Guid.NewGuid().ToString("N"), "profile.json");

    [Fact]
    public void Load_missing_returns_empty_profile()
    {
        var store = new ProfileStore(TempPath());
        Assert.Empty(store.Load().RecentRootPaths);
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var path = TempPath();
        var store = new ProfileStore(path);
        store.Save(new Models.Profile(new[] { @"\\share\runs\a", @"\\share\runs\b" }));
        var p = store.Load();
        Assert.Equal(new[] { @"\\share\runs\a", @"\\share\runs\b" }, p.RecentRootPaths);
    }

    [Fact]
    public void AddRecent_moves_to_front_and_dedups_case_insensitively()
    {
        var p = new Models.Profile(new[] { @"\\share\A", @"\\share\B" });
        var updated = ProfileStore.AddRecent(p, @"\\SHARE\a");   // same as A, different case
        Assert.Equal(new[] { @"\\SHARE\a", @"\\share\B" }, updated.RecentRootPaths);
    }

    [Fact]
    public void AddRecent_caps_at_max()
    {
        var p = new Models.Profile(Enumerable.Range(0, 10).Select(i => $"p{i}").ToList());
        var updated = ProfileStore.AddRecent(p, "new", max: 10);
        Assert.Equal(10, updated.RecentRootPaths.Count);
        Assert.Equal("new", updated.RecentRootPaths[0]);
        Assert.DoesNotContain("p9", updated.RecentRootPaths);   // oldest dropped
    }

    [Fact]
    public void Load_corrupt_json_returns_empty_profile()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not valid json ");
        var store = new ProfileStore(path);
        Assert.Empty(store.Load().RecentRootPaths);
    }

    [Fact]
    public void AddRecent_with_nonpositive_max_returns_empty()
    {
        var p = new Models.Profile(new[] { "a", "b" });
        Assert.Empty(ProfileStore.AddRecent(p, "new", max: 0).RecentRootPaths);
    }
}
