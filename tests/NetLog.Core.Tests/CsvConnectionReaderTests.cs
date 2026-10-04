using System.Text;
using NetLog.Core.Csv;

namespace NetLog.Core.Tests;

public class CsvConnectionReaderTests
{
    private static string TempFile(string content, bool bom)
    {
        var path = Path.Combine(Path.GetTempPath(), "nlc_" + Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom));
        return path;
    }

    [Fact]
    public void Reads_rows_skipping_header()
    {
        var csv = "Direction,Source,SourcePort,Target,TargetPort,Process\n" +
                  "Outgoing,10.0.0.1,51001,10.0.0.5,443,chrome\n" +
                  "Incoming,10.0.0.1,445,10.0.0.9,60001,System\n";
        var path = TempFile(csv, bom: false);
        var rows = CsvConnectionReader.ReadFile(path);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Outgoing", rows[0].Direction);
        Assert.Equal("443", rows[0].TargetPort);
        Assert.Equal("System", rows[1].Process);
        File.Delete(path);
    }

    [Fact]
    public void Tolerates_utf8_bom()
    {
        var csv = "Direction,Source,SourcePort,Target,TargetPort,Process\n" +
                  "Outgoing,H,,R,443,app\n";
        var path = TempFile(csv, bom: true);
        var rows = CsvConnectionReader.ReadFile(path);
        Assert.Single(rows);
        Assert.Equal("Outgoing", rows[0].Direction);   // not "﻿Outgoing"
        Assert.Equal("", rows[0].SourcePort);
        File.Delete(path);
    }

    [Fact]
    public void Empty_file_returns_no_rows()
    {
        var path = TempFile("", bom: false);
        Assert.Empty(CsvConnectionReader.ReadFile(path));
        File.Delete(path);
    }

    [Fact]
    public void Header_only_file_returns_no_rows()
    {
        var path = TempFile("Direction,Source,SourcePort,Target,TargetPort,Process\n", bom: false);
        Assert.Empty(CsvConnectionReader.ReadFile(path));
        File.Delete(path);
    }

    [Fact]
    public void Missing_file_returns_no_rows()
    {
        Assert.Empty(CsvConnectionReader.ReadFile(Path.Combine(Path.GetTempPath(), "does_not_exist_" + Guid.NewGuid().ToString("N") + ".csv")));
    }

    [Fact]
    public void Reads_8_column_files_with_host_columns()
    {
        var csv = "Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process\n" +
                  "Outgoing,10.0.0.1,ME01,49152,10.0.0.2,srv.corp.local,443,chrome\n";
        var path = TempFile(csv, bom: false);
        var r = Assert.Single(CsvConnectionReader.ReadFile(path));
        Assert.Equal("ME01", r.SourceHost);
        Assert.Equal("srv.corp.local", r.TargetHost);
        Assert.Equal("443", r.TargetPort);
        File.Delete(path);
    }

    [Fact]
    public void Upgrades_legacy_6_column_incoming_rows_by_swapping_source_and_target()
    {
        var csv = "Direction,Source,SourcePort,Target,TargetPort,Process\n" +
                  "Incoming,10.0.0.1,8080,10.0.0.9,50001,w3wp\n";   // legacy: Source = local
        var path = TempFile(csv, bom: false);
        var r = Assert.Single(CsvConnectionReader.ReadFile(path));
        Assert.Equal("10.0.0.9", r.Source);       // initiator (was Target)
        Assert.Equal("50001", r.SourcePort);
        Assert.Equal("10.0.0.1", r.Target);
        Assert.Equal("8080", r.TargetPort);
        Assert.Equal("", r.SourceHost);
        File.Delete(path);
    }

    [Fact]
    public void Legacy_outgoing_rows_keep_their_orientation()
    {
        var csv = "Direction,Source,SourcePort,Target,TargetPort,Process\n" +
                  "Outgoing,10.0.0.1,49152,10.0.0.2,443,chrome\n";
        var path = TempFile(csv, bom: false);
        var r = Assert.Single(CsvConnectionReader.ReadFile(path));
        Assert.Equal("10.0.0.1", r.Source);
        Assert.Equal("10.0.0.2", r.Target);
        File.Delete(path);
    }

    [Fact]
    public void Reads_14_column_files_with_identity_columns()
    {
        var csv = "Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process,Usernames,ProcessPath,SeenCount,Connections,FirstSeen,LastSeen\n" +
                  "Outgoing,10.0.0.1,ME01,,10.0.0.2,srv.corp.local,443,\"svchost (Dnscache)\",DOMAIN\\alice;DOMAIN\\bob,C:\\Windows\\System32\\svchost.exe,5,2,2026-08-06T09:00:00.0000000Z,2026-08-06T12:00:00.0000000Z\n";
        var path = TempFile(csv, bom: false);
        var r = Assert.Single(CsvConnectionReader.ReadFile(path));
        Assert.Equal("svchost (Dnscache)", r.Process);
        Assert.Equal("DOMAIN\\alice;DOMAIN\\bob", r.Usernames);
        Assert.Equal("C:\\Windows\\System32\\svchost.exe", r.ProcessPath);
        Assert.Equal("5", r.SeenCount);
        Assert.Equal("2", r.Connections);
        Assert.Equal("2026-08-06T09:00:00.0000000Z", r.FirstSeen);
        Assert.Equal("2026-08-06T12:00:00.0000000Z", r.LastSeen);
        File.Delete(path);
    }

    [Fact]
    public void Eight_column_files_read_with_blank_identity_fields()
    {
        var csv = "Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process\n" +
                  "Outgoing,10.0.0.1,ME01,49152,10.0.0.2,srv.corp.local,443,chrome\n";
        var path = TempFile(csv, bom: false);
        var r = Assert.Single(CsvConnectionReader.ReadFile(path));
        Assert.Equal("", r.Usernames);
        Assert.Equal("", r.SeenCount);
        File.Delete(path);
    }
}
