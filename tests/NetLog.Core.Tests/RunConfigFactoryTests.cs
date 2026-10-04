using NetLog.Core.RunSpec;

namespace NetLog.Core.Tests;

public class RunConfigFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_builds_config_with_run_name_from_path_and_default_cache()
    {
        var c = RunConfigFactory.Create(
            path: @"\\share\runs\audit", targets: "S1, S2", account: "DOMAIN\\svc",
            start: "now", stop: null, duration: "7d",
            archiveLevel: "Merged", pollSeconds: 10, archiveMinutes: 60,
            cacheDays: null, cacheMb: null, now: Now);

        Assert.Equal("audit", c.RunName);
        Assert.Equal(new[] { "S1", "S2" }, c.Targets);
        Assert.Equal("DOMAIN\\svc", c.ServiceAccount);
        Assert.Equal(Now, c.Schedule.StartUtc);
        Assert.Equal(Now.AddDays(7), c.Schedule.StopUtc);
        Assert.Equal("Merged", c.Settings.ArchiveLevel);
        Assert.Equal(7, c.Settings.CacheLifetimeDays);   // default when neither given
        Assert.Null(c.Settings.MaxCacheSizeMb);
        Assert.Equal(Now, c.CreatedUtc);
    }

    [Fact]
    public void WithEdits_changes_only_end_and_mutable_settings_keeping_identity()
    {
        var original = RunConfigFactory.Create(
            path: @"\\share\runs\audit", targets: "S1,S2", account: "DOMAIN\\svc",
            start: "now", stop: null, duration: "7d",
            archiveLevel: "Full", pollSeconds: 10, archiveMinutes: 60,
            cacheDays: 7, cacheMb: null, now: Now) with { RunSequence = 3 };

        var edited = RunConfigFactory.WithEdits(
            original, stop: null, duration: "3d",
            pollSeconds: 20, archiveMinutes: 30, cacheDays: 14, cacheMb: null, now: Now.AddDays(1));

        // mutable
        Assert.Equal(Now.AddDays(3), edited.Schedule.StopUtc);
        Assert.Equal(20, edited.Settings.PollIntervalSeconds);
        Assert.Equal(30, edited.Settings.ArchiveIntervalMinutes);
        Assert.Equal(14, edited.Settings.CacheLifetimeDays);
        // identity / immutable preserved
        Assert.Equal(Now, edited.Schedule.StartUtc);          // start unchanged
        Assert.Equal("audit", edited.RunName);
        Assert.Equal(new[] { "S1", "S2" }, edited.Targets);
        Assert.Equal("DOMAIN\\svc", edited.ServiceAccount);
        Assert.Equal("Full", edited.Settings.ArchiveLevel);
        Assert.Equal(3, edited.RunSequence);
        Assert.Equal(original.CreatedUtc, edited.CreatedUtc);
    }

    [Fact]
    public void WithEdits_accepts_an_explicit_stop_and_rejects_stop_before_start()
    {
        var original = RunConfigFactory.Create(
            path: @"\\share\runs\a", targets: "S1", account: "svc",
            start: "now", stop: null, duration: "7d", archiveLevel: "Merged",
            pollSeconds: 10, archiveMinutes: 60, cacheDays: 7, cacheMb: null, now: Now);

        var edited = RunConfigFactory.WithEdits(original, stop: "2026-07-01T00:00:00Z", duration: null,
            pollSeconds: 10, archiveMinutes: 60, cacheDays: 7, cacheMb: null, now: Now);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), edited.Schedule.StopUtc);

        Assert.Throws<ArgumentException>(() => RunConfigFactory.WithEdits(
            original, stop: "2026-06-01T00:00:00Z", duration: null,
            pollSeconds: 10, archiveMinutes: 60, cacheDays: 7, cacheMb: null, now: Now));
    }

    [Fact]
    public void Create_uses_cache_mb_when_given_and_leaves_days_null()
    {
        var c = RunConfigFactory.Create(@"\\s\a", "S1", "D\\s", "now", null, "1d", "Full", 5, 30, null, 500, Now);
        Assert.Null(c.Settings.CacheLifetimeDays);
        Assert.Equal(500, c.Settings.MaxCacheSizeMb);
    }

    [Fact]
    public void Create_defaults_resolveHostnames_true_and_honours_false()
    {
        var on = RunConfigFactory.Create(@"\\s\a", "S1", "D\\s", "now", null, "1d", "Full", 5, 30, 7, null, Now);
        Assert.True(on.Settings.ResolveHostnames);
        var off = RunConfigFactory.Create(@"\\s\a", "S1", "D\\s", "now", null, "1d", "Full", 5, 30, 7, null, Now, resolveHostnames: false);
        Assert.False(off.Settings.ResolveHostnames);
    }

    [Fact]
    public void Create_propagates_schedule_validation_errors()
    {
        Assert.Throws<ArgumentException>(() =>
            RunConfigFactory.Create(@"\\s\a", "S1", "D\\s", "now", null, null, "Full", 5, 30, 7, null, Now));
    }

    [Theory]
    [InlineData(@"\\srv\Share", "Share")]            // UNC share root: Path.GetFileName returns "" — must fall back to share name
    [InlineData(@"\\srv\Share\", "Share")]           // trailing separator
    [InlineData(@"\\srv\Share\audit", "audit")]      // sub-folder under the share
    [InlineData(@"C:\runs\audit\", "audit")]
    [InlineData("/mnt/share/audit", "audit")]        // forward slashes
    public void Create_derives_non_empty_run_name(string path, string expected)
    {
        var c = RunConfigFactory.Create(
            path, "S1", "D\\s", "now", null, "7d", "Full", 10, 60, 7, null, Now);
        Assert.Equal(expected, c.RunName);
    }

    [Fact]
    public void WithNewSchedule_replaces_window_and_keeps_everything_else()
    {
        var original = RunConfigFactory.Create(
            @"\\share\runs\audit", "S1, S2", "DOMAIN\\svc", "now", null, "7d",
            "Merged", 10, 60, 7, null, Now);

        var later = Now.AddDays(30);
        var updated = RunConfigFactory.WithNewSchedule(original, "now", null, "3d", later);

        Assert.Equal(later, updated.Schedule.StartUtc);
        Assert.Equal(later.AddDays(3), updated.Schedule.StopUtc);
        Assert.Equal(later, updated.CreatedUtc);
        Assert.Equal(2, updated.RunSequence);   // a new window is a new run
        Assert.Equal(3, RunConfigFactory.WithNewSchedule(updated, "now", null, "1d", later).RunSequence);
        // identity/targets/settings preserved
        Assert.Equal("audit", updated.RunName);
        Assert.Equal(new[] { "S1", "S2" }, updated.Targets);
        Assert.Equal("DOMAIN\\svc", updated.ServiceAccount);
        Assert.Equal("Merged", updated.Settings.ArchiveLevel);
        Assert.Equal(7, updated.Settings.CacheLifetimeDays);
    }

    [Fact]
    public void WithNewSchedule_propagates_schedule_validation_errors()
    {
        var original = RunConfigFactory.Create(
            @"\\share\runs\audit", "S1", "DOMAIN\\svc", "now", null, "7d", "Full", 10, 60, 7, null, Now);
        Assert.Throws<ArgumentException>(() =>
            RunConfigFactory.WithNewSchedule(original, "now", null, null, Now)); // no stop/duration
    }

    [Theory]
    [InlineData(@"\\srv")]   // UNC with no share component — nothing usable to name the run
    [InlineData("")]
    [InlineData(@"\")]
    public void Create_throws_when_no_run_name_can_be_derived(string path)
    {
        Assert.Throws<ArgumentException>(() =>
            RunConfigFactory.Create(path, "S1", "D\\s", "now", null, "7d", "Full", 10, 60, 7, null, Now));
    }
}
