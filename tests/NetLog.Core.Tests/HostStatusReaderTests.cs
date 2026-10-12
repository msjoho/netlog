using NetLog.Core.Io;

namespace NetLog.Core.Tests;

public class HostStatusReaderTests
{
    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "nlstat_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parses_status_fields()
    {
        var json = """{ "lastRunUtc": "2026-06-05T06:20:04.3309330Z", "pid": 94740, "totalConnections": 87, "totalPolls": 2 }""";
        var path = TempFile(json);
        var s = HostStatusReader.ReadFile(path);
        Assert.NotNull(s);
        Assert.Equal(94740, s!.Pid);
        Assert.Equal(87, s.TotalConnections);
        Assert.Equal(2, s.TotalPolls);
        Assert.Equal(new DateTimeOffset(2026, 6, 5, 6, 20, 4, 330, TimeSpan.Zero).AddTicks(9330), s.LastRunUtc);
        File.Delete(path);
    }

    [Fact]
    public void Missing_file_returns_null()
    {
        Assert.Null(HostStatusReader.ReadFile(Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid().ToString("N") + ".json")));
    }

    [Fact]
    public void Malformed_status_returns_null()
    {
        var path = TempFile("{ this is not valid json ");
        Assert.Null(HostStatusReader.ReadFile(path));
        File.Delete(path);
    }

    [Fact]
    public void Json_array_instead_of_object_returns_null()
    {
        var path = TempFile("[1,2,3]");
        Assert.Null(HostStatusReader.ReadFile(path));
        File.Delete(path);
    }

    [Fact]
    public void Excluded_count_is_null_when_absent_and_read_when_present()
    {
        var old = TempFile("""{ "lastRunUtc": "2026-06-05T06:20:04Z", "pid": 1, "totalConnections": 2, "totalPolls": 3 }""");
        Assert.Null(HostStatusReader.ReadFile(old)!.ExcludedCount);
        var @new = TempFile("""{ "lastRunUtc": "2026-06-05T06:20:04Z", "pid": 1, "totalConnections": 2, "totalPolls": 3, "excludedCount": 1204 }""");
        Assert.Equal(1204, HostStatusReader.ReadFile(@new)!.ExcludedCount);
        File.Delete(old); File.Delete(@new);
    }
}
