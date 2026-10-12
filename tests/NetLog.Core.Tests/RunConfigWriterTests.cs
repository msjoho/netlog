using NetLog.Core.Io;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class RunConfigWriterTests
{
    private static RunConfig Sample() => new(
        "weekly-audit",
        new DateTimeOffset(2026, 6, 4, 8, 0, 0, TimeSpan.Zero),
        new RunSchedule(new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero)),
        new[] { "SERVER1", "SERVER2" },
        "DOMAIN\\svc-netlog",
        new RunSettings("Merged", 10, 60, 7, null));

    [Fact]
    public void Roundtrips_through_reader()
    {
        var path = Path.Combine(Path.GetTempPath(), "nlwrite_" + Guid.NewGuid().ToString("N"), "run.json");
        RunConfigWriter.WriteFile(path, Sample());
        var back = RunConfigReader.ReadFile(path);

        Assert.Equal("weekly-audit", back.RunName);
        Assert.Equal(Sample().Schedule.StartUtc, back.Schedule.StartUtc);
        Assert.Equal(Sample().Schedule.StopUtc, back.Schedule.StopUtc);
        Assert.Equal(new[] { "SERVER1", "SERVER2" }, back.Targets);
        Assert.Equal("DOMAIN\\svc-netlog", back.ServiceAccount);
        Assert.Equal("Merged", back.Settings.ArchiveLevel);
        Assert.Equal(7, back.Settings.CacheLifetimeDays);
        Assert.Null(back.Settings.MaxCacheSizeMb);
        Assert.True(back.Settings.ResolveHostnames);   // default round-trips
        Assert.Equal(1, back.RunSequence);             // default round-trips

        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void Writes_camelCase_keys_and_no_bom()
    {
        var path = Path.Combine(Path.GetTempPath(), "nlwrite_" + Guid.NewGuid().ToString("N") + ".json");
        RunConfigWriter.WriteFile(path, Sample());
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF); // no BOM
        var text = File.ReadAllText(path);
        Assert.Contains("\"runName\"", text);
        Assert.Contains("\"archiveLevel\"", text);
        Assert.Contains("\"maxCacheSizeMb\": null", text);
        File.Delete(path);
    }

    [Fact]
    public void Timestamps_round_trips_through_write_and_read()
    {
        var path = Path.Combine(Path.GetTempPath(), "nlrw_" + Guid.NewGuid().ToString("N"), "run.json");
        var config = new RunConfig(
            "r", new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero),
            new RunSchedule(new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 7, 0, 0, 0, TimeSpan.Zero)),
            new[] { "S1" }, "D\\svc",
            new RunSettings("Merged", 10, 60, 7, null, ResolveHostnames: true, Timestamps: true));
        RunConfigWriter.WriteFile(path, config);
        Assert.True(RunConfigReader.ReadFile(path).Settings.Timestamps);
        Assert.Contains("\"timestamps\": true", File.ReadAllText(path));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void Roundtrips_exclusions_and_reads_absent_as_empty()
    {
        var rules = new[]
        {
            new ExclusionRule("process", "svchost (Dnscache)", "both", "DNS"),
            new ExclusionRule("host", "10.0.0.0/8", "out"),
            new ExclusionRule("host", "nas01*", "in", "smb", "445"),
        };
        var cfg = Sample() with { Settings = Sample().Settings with { Exclusions = rules } };
        var path = Path.Combine(Path.GetTempPath(), "nlwrite_" + Guid.NewGuid().ToString("N"), "run.json");
        RunConfigWriter.WriteFile(path, cfg);

        var text = File.ReadAllText(path);
        Assert.Contains("\"exclusions\"", text);
        Assert.Contains("\"kind\": \"process\"", text);
        Assert.Contains("\"port\": \"445\"", text);

        var back = RunConfigReader.ReadFile(path);
        Assert.Equal(rules, back.Settings.Exclusions);

        // A config without the key (older CLI) reads as no rules.
        RunConfigWriter.WriteFile(path, Sample());
        Assert.Empty(RunConfigReader.ReadFile(path).Settings.Exclusions);
    }
}
