using NetLog.Core.Io;

namespace NetLog.Core.Tests;

public class RunReaderTests
{
    private static string MakeRunFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlrunfolder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "run.json"), """
        {
          "runName": "r1",
          "createdUtc": "2026-06-04T08:00:00Z",
          "schedule": { "startUtc": "2026-06-05T00:00:00Z", "stopUtc": "2026-06-12T00:00:00Z" },
          "targets": ["H1","H2"],
          "serviceAccount": "DOMAIN\\svc",
          "settings": { "archiveLevel":"Merged","pollIntervalSeconds":10,"archiveIntervalMinutes":60,"cacheLifetimeDays":7,"maxCacheSizeMb":null }
        }
        """);

        // H1: full host with status + 2 connections
        var h1 = Path.Combine(root, "hosts", "H1");
        Directory.CreateDirectory(h1);
        File.WriteAllText(Path.Combine(h1, "connections.csv"),
            "Direction,Source,SourcePort,Target,TargetPort,Process\nOutgoing,H1,,R,443,app\nIncoming,H1,445,C,,svc\n");
        File.WriteAllText(Path.Combine(h1, "_status.json"),
            """{ "lastRunUtc":"2026-06-06T00:00:00Z","pid":1,"totalConnections":2,"totalPolls":5 }""");

        // H2: folder exists, connections only, NO status yet
        var h2 = Path.Combine(root, "hosts", "H2");
        Directory.CreateDirectory(h2);
        File.WriteAllText(Path.Combine(h2, "connections.csv"),
            "Direction,Source,SourcePort,Target,TargetPort,Process\nOutgoing,H2,,R,80,app\n");

        return root;
    }

    [Fact]
    public void Reads_config_and_all_hosts()
    {
        var root = MakeRunFolder();
        var data = RunReader.Read(root);

        Assert.Equal("r1", data.Config.RunName);
        Assert.Equal(2, data.Hosts.Count);

        var h1 = data.Hosts.Single(h => h.HostName == "H1");
        Assert.NotNull(h1.Status);
        Assert.Equal(5, h1.Status!.TotalPolls);
        Assert.Equal(2, h1.Connections.Count);

        var h2 = data.Hosts.Single(h => h.HostName == "H2");
        Assert.Null(h2.Status);                 // no _status.json yet
        Assert.Single(h2.Connections);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void No_hosts_folder_yields_empty_host_list()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlrunfolder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "run.json"), """
        { "runName":"r","createdUtc":"2026-06-04T08:00:00Z","schedule":{"startUtc":"2026-06-05T00:00:00Z","stopUtc":"2026-06-12T00:00:00Z"},
          "targets":[],"serviceAccount":"x","settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }
        """);
        var data = RunReader.Read(root);
        Assert.Empty(data.Hosts);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Hosts_are_ordered_by_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlorder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "run.json"), """
        { "runName":"r","createdUtc":"2026-06-04T08:00:00Z","schedule":{"startUtc":"2026-06-05T00:00:00Z","stopUtc":"2026-06-12T00:00:00Z"},
          "targets":[],"serviceAccount":"x","settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }
        """);
        foreach (var name in new[] { "Bravo", "alpha", "Charlie" })
            Directory.CreateDirectory(Path.Combine(root, "hosts", name));

        var data = RunReader.Read(root);
        Assert.Equal(new[] { "alpha", "Bravo", "Charlie" }, data.Hosts.Select(h => h.HostName).ToArray());

        Directory.Delete(root, recursive: true);
    }
}
