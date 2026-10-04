using NetLog.Core.Csv;
using NetLog.Core.Io;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class ConnectionCondenserTests
{
    private static ConnectionRecord R(string dir, string src, string sport, string tgt, string tport, string proc,
        string shost = "", string thost = "") => new(dir, src, shost, sport, tgt, thost, tport, proc);

    [Fact]
    public void Merged_drops_source_port_and_collapses_rows_that_differ_only_by_it()
    {
        var recs = new[]
        {
            R("Outgoing", "H", "51001", "10.0.0.5", "443", "chrome"),
            R("Outgoing", "H", "51002", "10.0.0.5", "443", "chrome"),   // same flow, different ephemeral port
            R("Outgoing", "H", "51003", "10.0.0.9", "443", "chrome"),   // different target
        };
        var merged = ConnectionCondenser.Condense(recs, "Merged");

        Assert.Equal(2, merged.Count);
        Assert.All(merged, m => Assert.Equal("", m.SourcePort));   // ephemeral port dropped
    }

    [Fact]
    public void Full_keeps_rows_with_distinct_source_ports()
    {
        var recs = new[]
        {
            R("Outgoing", "H", "51001", "10.0.0.5", "443", "chrome"),
            R("Outgoing", "H", "51002", "10.0.0.5", "443", "chrome"),
        };
        Assert.Equal(2, ConnectionCondenser.Condense(recs, "Full").Count);
    }

    [Fact]
    public void Backfills_a_blank_host_column_from_a_later_duplicate()
    {
        var recs = new[]
        {
            R("Outgoing", "H", "", "10.0.0.5", "443", "chrome", thost: ""),
            R("Outgoing", "H", "", "10.0.0.5", "443", "chrome", thost: "srv.corp"),
        };
        var merged = ConnectionCondenser.Condense(recs, "Merged");
        Assert.Equal("srv.corp", Assert.Single(merged).TargetHost);
    }

    [Fact]
    public void Merged_condense_unions_identity_and_counts_distinct_ports()
    {
        var a = new ConnectionRecord("Outgoing", "H", "ME01", "51001", "R", "", "443", "app",
            Usernames: "DOMAIN\\a", ProcessPath: "C:\\x\\app.exe", SeenCount: "5",
            FirstSeen: "2026-08-06T10:00:00.0000000Z", LastSeen: "2026-08-06T11:00:00.0000000Z");
        var b = new ConnectionRecord("Outgoing", "H", "ME01", "51002", "R", "", "443", "app",
            Usernames: "DOMAIN\\b", ProcessPath: "C:\\x\\app.exe", SeenCount: "3",
            FirstSeen: "2026-08-06T09:00:00.0000000Z", LastSeen: "2026-08-06T12:00:00.0000000Z");
        var m = Assert.Single(ConnectionCondenser.Condense(new[] { a, b }, "Merged"));
        Assert.Equal("DOMAIN\\a;DOMAIN\\b", m.Usernames);
        Assert.Equal("5", m.SeenCount);          // max, not sum
        Assert.Equal("2", m.Connections);        // two distinct source ports
        Assert.Equal("2026-08-06T09:00:00.0000000Z", m.FirstSeen);
        Assert.Equal("2026-08-06T12:00:00.0000000Z", m.LastSeen);
    }

    [Fact]
    public void Merged_condense_is_idempotent_when_recondensed_with_inputs()
    {
        var a = new ConnectionRecord("Outgoing", "H", "", "51001", "R", "", "443", "app", SeenCount: "5", Usernames: "DOMAIN\\a");
        var b = new ConnectionRecord("Outgoing", "H", "", "51002", "R", "", "443", "app", SeenCount: "3", Usernames: "DOMAIN\\a");
        var first = ConnectionCondenser.Condense(new[] { a, b }, "Merged");
        var second = ConnectionCondenser.Condense(first.Concat(new[] { a, b }), "Merged");
        Assert.Equal(first[0].SeenCount, Assert.Single(second).SeenCount);
        Assert.Equal(first[0].Connections, second[0].Connections);
        Assert.Equal(first[0].Usernames, second[0].Usernames);
    }

    [Fact]
    public void Full_condense_leaves_connections_blank_and_passes_counters_through()
    {
        var a = new ConnectionRecord("Outgoing", "H", "", "51001", "R", "", "443", "app", SeenCount: "4");
        var m = Assert.Single(ConnectionCondenser.Condense(new[] { a }, "Full"));
        Assert.Equal("4", m.SeenCount);
        Assert.Equal("", m.Connections);
    }

    [Fact]
    public void Writer_emits_14_columns_that_read_back_identically()
    {
        var rec = new ConnectionRecord("Outgoing", "H", "ME01", "", "R", "srv.corp", "443", "svchost (Dnscache,NlaSvc)",
            Usernames: "DOMAIN\\a;NT AUTHORITY\\SYSTEM", ProcessPath: "C:\\Windows\\System32\\svchost.exe",
            SeenCount: "5", Connections: "2",
            FirstSeen: "2026-08-06T09:00:00.0000000Z", LastSeen: "2026-08-06T12:00:00.0000000Z");
        var path = Path.Combine(Path.GetTempPath(), "nlw_" + Guid.NewGuid().ToString("N") + ".csv");
        CsvConnectionWriter.WriteFile(path, new[] { rec });
        var back = Assert.Single(CsvConnectionReader.ReadFile(path));
        Assert.Equal(rec, back);                 // record equality across all 14 fields
        File.Delete(path);
    }
}

public class RunMergerTests
{
    private static ConnectionRecord Out(string sport) => new("Outgoing", "H1", "", sport, "10.0.0.5", "", "443", "chrome");

    [Fact]
    public void Condenses_connections_in_place_and_archives_the_original()
    {
        var run = Path.Combine(Path.GetTempPath(), "nlmerge_" + Guid.NewGuid().ToString("N"));
        var host = Path.Combine(run, "hosts", "H1");
        Directory.CreateDirectory(host);
        var csv = Path.Combine(host, "connections.csv");
        CsvConnectionWriter.WriteFile(csv, new[] { Out("51001"), Out("51002"), Out("51003") });   // 3 Full rows, one flow

        var results = RunMerger.MergeConnections(run, "2026-06-16T0930Z");

        var res = Assert.Single(results);
        Assert.Equal("H1", res.HostName);
        Assert.Equal(3, res.Before);
        Assert.Equal(1, res.After);                                    // collapsed to one Merged row
        Assert.Equal(1, CsvConnectionReader.ReadFile(csv).Count);      // in place = merged
        var archived = Path.Combine(run, "_archive", "2026-06-16T0930Z", "hosts", "H1", "connections.csv");
        Assert.Equal(3, CsvConnectionReader.ReadFile(archived).Count); // original Full preserved

        Directory.Delete(run, recursive: true);
    }

    [Fact]
    public void Missing_hosts_folder_is_a_no_op()
    {
        var run = Path.Combine(Path.GetTempPath(), "nlmerge_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        Assert.Empty(RunMerger.MergeConnections(run, "stamp"));
        Directory.Delete(run, recursive: true);
    }
}
