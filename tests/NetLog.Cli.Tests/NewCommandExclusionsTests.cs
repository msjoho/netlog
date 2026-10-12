using NetLog.Cli.Commands;
using NetLog.Core.Io;
using NetLog.Core.Models;

namespace NetLog.Cli.Tests;

public class NewCommandExclusionsTests
{
    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlnew_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Inherits_root_rules_then_appends_cli_rules()
    {
        var root = TempRoot();
        ExclusionsStore.Save(root, new[] { new ExclusionRule("process", "fromroot") });
        var rules = NewCommand.ResolveExclusions(Path.Combine(root, "audit"), new[] { "host:nas01*@out" }, noRoot: false);
        Assert.Equal(new[] { new ExclusionRule("process", "fromroot"), new ExclusionRule("host", "nas01*", "out") }, rules);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void No_root_exclusions_skips_the_template()
    {
        var root = TempRoot();
        ExclusionsStore.Save(root, new[] { new ExclusionRule("process", "fromroot") });
        var rules = NewCommand.ResolveExclusions(Path.Combine(root, "audit"), new[] { "user:x" }, noRoot: true);
        Assert.Equal(new[] { new ExclusionRule("user", "x") }, rules);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Bad_cli_rule_throws_format_and_corrupt_root_throws_invalid_data()
    {
        var root = TempRoot();
        Assert.Throws<FormatException>(() => NewCommand.ResolveExclusions(Path.Combine(root, "a"), new[] { "port:443" }, false));
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ nope");
        Assert.Throws<InvalidDataException>(() => NewCommand.ResolveExclusions(Path.Combine(root, "a"), Array.Empty<string>(), false));
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Unc_share_root_has_no_parent_and_is_skipped_quietly()
    {
        var rules = NewCommand.ResolveExclusions(@"\\server\share", new[] { "host:x" }, noRoot: false);
        Assert.Equal(new[] { new ExclusionRule("host", "x") }, rules);
    }

    [Fact]
    public void Trailing_separator_on_path_still_finds_the_root()
    {
        var root = TempRoot();
        ExclusionsStore.Save(root, new[] { new ExclusionRule("user", "u") });
        var rules = NewCommand.ResolveExclusions(Path.Combine(root, "audit") + "\\", Array.Empty<string>(), false);
        Assert.Single(rules);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Bare_relative_path_still_finds_the_root()
    {
        var root = TempRoot();
        ExclusionsStore.Save(root, new[] { new ExclusionRule("user", "u") });
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            var rules = NewCommand.ResolveExclusions("audit", Array.Empty<string>(), false);
            Assert.Single(rules);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
        Directory.Delete(root, recursive: true);
    }
}
