using NetLog.Core.Io;
using NetLog.Core.Models;

namespace NetLog.Core.Tests;

public class ExclusionsStoreTests
{
    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlexcl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Missing_file_loads_as_empty()
    {
        Assert.Empty(ExclusionsStore.Load(TempRoot()));
    }

    [Fact]
    public void Save_then_load_roundtrips_and_normalises()
    {
        var root = TempRoot();
        var rules = new[]
        {
            new ExclusionRule("process", "Teams*", "both", "chat"),
            new ExclusionRule("host", "10.0.0.0/8", "out"),
            new ExclusionRule("process", "System", "in", null, "139,445"),
        };
        ExclusionsStore.Save(root, rules);
        Assert.True(File.Exists(Path.Combine(root, "exclusions.json")));
        Assert.Equal(rules, ExclusionsStore.Load(root));

        File.WriteAllText(ExclusionsStore.PathFor(root), "{ \"rules\": [ { \"kind\": \"USER\", \"pattern\": \"x\" } ] }");
        var r = Assert.Single(ExclusionsStore.Load(root));
        Assert.Equal("user", r.Kind);
        Assert.Equal("both", r.Direction);
    }

    [Fact]
    public void Corrupt_json_throws_not_empty()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ not json");
        var ex = Assert.Throws<InvalidDataException>(() => ExclusionsStore.Load(root));
        Assert.Contains("exclusions.json", ex.Message);
    }

    [Fact]
    public void Invalid_rule_throws_with_its_index()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ \"rules\": [ { \"kind\": \"host\", \"pattern\": \"ok\" }, { \"kind\": \"port\", \"pattern\": \"443\" } ] }");
        var ex = Assert.Throws<InvalidDataException>(() => ExclusionsStore.Load(root));
        Assert.Contains("rule 2", ex.Message);
        Assert.Contains("Unknown kind", ex.Message);
    }

    [Fact]
    public void Numeric_port_in_file_is_read_as_text()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ \"rules\": [ { \"kind\": \"host\", \"pattern\": \"ok\", \"port\": 445 } ] }");
        var r = Assert.Single(ExclusionsStore.Load(root));
        Assert.Equal("445", r.Port);
    }

    [Fact]
    public void Invalid_port_throws_with_its_index()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ \"rules\": [ { \"kind\": \"host\", \"pattern\": \"ok\", \"port\": \"99999\" } ] }");
        var ex = Assert.Throws<InvalidDataException>(() => ExclusionsStore.Load(root));
        Assert.Contains("rule 1", ex.Message);
        Assert.Contains("Port", ex.Message);
    }

    [Fact]
    public void Null_rule_entry_throws_with_its_index()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ \"rules\": [ null ] }");
        var ex = Assert.Throws<InvalidDataException>(() => ExclusionsStore.Load(root));
        Assert.Contains("rule 1", ex.Message);
    }

    [Fact]
    public void Blank_file_loads_as_empty()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "   ");
        Assert.Empty(ExclusionsStore.Load(root));
    }
}
