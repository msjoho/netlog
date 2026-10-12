using System.Text.Json;
using NetLog.Core.Io;

namespace NetLog.Core.Tests;

public class RunConfigReaderTests
{
    private const string Json = """
    {
      "runName": "weekly-audit",
      "createdUtc": "2026-06-04T08:00:00Z",
      "schedule": { "startUtc": "2026-06-05T00:00:00Z", "stopUtc": "2026-06-12T00:00:00Z" },
      "targets": ["SERVER1", "SERVER2"],
      "serviceAccount": "DOMAIN\\svc-netlog",
      "settings": {
        "archiveLevel": "Merged",
        "pollIntervalSeconds": 10,
        "archiveIntervalMinutes": 60,
        "cacheLifetimeDays": 7,
        "maxCacheSizeMb": null
      }
    }
    """;

    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "nlrun_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parses_all_fields()
    {
        var path = TempFile(Json);
        var c = RunConfigReader.ReadFile(path);
        Assert.Equal("weekly-audit", c.RunName);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 8, 0, 0, TimeSpan.Zero), c.CreatedUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero), c.Schedule.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), c.Schedule.StopUtc);
        Assert.Equal(new[] { "SERVER1", "SERVER2" }, c.Targets);
        Assert.Equal("DOMAIN\\svc-netlog", c.ServiceAccount);
        Assert.Equal("Merged", c.Settings.ArchiveLevel);
        Assert.Equal(10, c.Settings.PollIntervalSeconds);
        Assert.Equal(60, c.Settings.ArchiveIntervalMinutes);
        Assert.Equal(7, c.Settings.CacheLifetimeDays);
        Assert.Null(c.Settings.MaxCacheSizeMb);
        File.Delete(path);
    }

    [Fact]
    public void ResolveHostnames_defaults_true_when_absent_and_honours_false()
    {
        var path = TempFile(Json);   // fixture has no resolveHostnames
        Assert.True(RunConfigReader.ReadFile(path).Settings.ResolveHostnames);
        File.Delete(path);

        var path2 = TempFile(Json.Replace("\"maxCacheSizeMb\": null", "\"maxCacheSizeMb\": null, \"resolveHostnames\": false"));
        Assert.False(RunConfigReader.ReadFile(path2).Settings.ResolveHostnames);
        File.Delete(path2);
    }

    [Fact]
    public void RunSequence_defaults_to_1_when_absent_and_reads_explicit_value()
    {
        var path = TempFile(Json);   // fixture has no runSequence
        Assert.Equal(1, RunConfigReader.ReadFile(path).RunSequence);
        File.Delete(path);

        var path2 = TempFile(Json.Replace("\"runName\": \"weekly-audit\",", "\"runName\": \"weekly-audit\", \"runSequence\": 3,"));
        Assert.Equal(3, RunConfigReader.ReadFile(path2).RunSequence);
        File.Delete(path2);
    }

    [Fact]
    public void Missing_file_throws_FileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            RunConfigReader.ReadFile(Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid().ToString("N") + ".json")));
    }

    [Fact]
    public void Null_schedule_throws_InvalidData()
    {
        var bad = """{ "runName":"x","createdUtc":"2026-06-04T08:00:00Z","schedule":null,"targets":[],"serviceAccount":"x","settings":{"archiveLevel":"Full","pollIntervalSeconds":1,"archiveIntervalMinutes":1,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }""";
        var path = TempFile(bad);
        Assert.Throws<InvalidDataException>(() => RunConfigReader.ReadFile(path));
        File.Delete(path);
    }

    [Fact]
    public void Malformed_json_throws_JsonException()
    {
        var path = TempFile("this is not json");
        Assert.Throws<JsonException>(() => RunConfigReader.ReadFile(path));
        File.Delete(path);
    }

    [Fact]
    public void Timestamps_absent_defaults_to_false()
    {
        var path = Path.Combine(Path.GetTempPath(), "nlrc_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """
            { "runName":"r","createdUtc":"2026-08-06T00:00:00Z",
              "schedule":{"startUtc":"2026-08-06T00:00:00Z","stopUtc":"2026-08-07T00:00:00Z"},
              "targets":["S1"],"serviceAccount":"D\\svc",
              "settings":{"archiveLevel":"Merged","pollIntervalSeconds":10,"archiveIntervalMinutes":60} }
            """);
        Assert.False(RunConfigReader.ReadFile(path).Settings.Timestamps);
        File.Delete(path);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[{ \"kind\": \"Host\", \"pattern\": \"nas*\" }]")]
    public void Exclusions_tolerate_null_object_and_missing_direction(string exclusionsJson)
    {
        var json = Json.Replace("\"maxCacheSizeMb\": null", "\"maxCacheSizeMb\": null, \"exclusions\": " + exclusionsJson);
        var path = TempFile(json);
        var c = RunConfigReader.ReadFile(path);
        if (exclusionsJson.StartsWith("["))
        {
            var r = Assert.Single(c.Settings.Exclusions);
            Assert.Equal("host", r.Kind);          // normalised
            Assert.Equal("both", r.Direction);     // defaulted
        }
        else Assert.Empty(c.Settings.Exclusions);
        File.Delete(path);
    }

    [Fact]
    public void Exclusions_read_the_optional_port_and_default_it_to_null()
    {
        var json = Json.Replace("\"maxCacheSizeMb\": null",
            "\"maxCacheSizeMb\": null, \"exclusions\": [ { \"kind\": \"host\", \"pattern\": \"a\", \"port\": \" 80, 443 \" }, { \"kind\": \"host\", \"pattern\": \"b\", \"port\": null }, { \"kind\": \"host\", \"pattern\": \"c\" } ]");
        var path = TempFile(json);
        var ex = RunConfigReader.ReadFile(path).Settings.Exclusions;
        Assert.Equal("80,443", ex[0].Port);   // normalised, not validated
        Assert.Null(ex[1].Port);
        Assert.Null(ex[2].Port);
        File.Delete(path);
    }

    [Fact]
    public void Exclusions_accept_a_numeric_port_from_a_hand_edited_file()
    {
        var json = Json.Replace("\"maxCacheSizeMb\": null",
            "\"maxCacheSizeMb\": null, \"exclusions\": [ { \"kind\": \"host\", \"pattern\": \"a\", \"port\": 445 } ]");
        var path = TempFile(json);
        var r = Assert.Single(RunConfigReader.ReadFile(path).Settings.Exclusions);
        Assert.Equal("445", r.Port);
        File.Delete(path);
    }
}
