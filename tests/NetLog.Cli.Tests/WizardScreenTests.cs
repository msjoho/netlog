using NetLog.Cli.Tui;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class WizardScreenTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
    private static IEnumerable<KeyPress> Type(string s) => s.Select(KeyPress.Of);

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlwizscr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Seeds_from_the_root_file_and_wizard_edits_affect_only_the_run()
    {
        var root = TempRoot();
        ExclusionsStore.Save(root, new[] { new ExclusionRule("process", "fromroot") });

        var keys = new List<KeyPress>();
        keys.AddRange(Type("r1"));                     // run name
        keys.Add(KeyPress.Down); keys.AddRange(Type("H1"));    // targets
        keys.Add(KeyPress.Down); keys.AddRange(Type("svc"));   // account
        for (int i = 0; i < 8; i++) keys.Add(KeyPress.Down);   // Start, End, ArchiveLevel, Poll, ArchiveEvery, Cache, Timestamps, Exclusions
        keys.Add(KeyPress.Tab);                                // open editor
        keys.Add(KeyPress.Of('a')); keys.AddRange(Type("host:nas*")); keys.Add(KeyPress.Enter);
        keys.Add(KeyPress.Enter);                              // editor done
        keys.Add(KeyPress.Enter);                              // create

        var created = WizardScreen.Run(new TestConsole(), new ScriptedKeySource(keys), root, "me", () => Now);

        Assert.NotNull(created);
        var cfg = RunConfigReader.ReadFile(Path.Combine(created!, "run.json"));
        Assert.Equal(new[] { new ExclusionRule("process", "fromroot"), new ExclusionRule("host", "nas*") }, cfg.Settings.Exclusions);
        Assert.Equal(new[] { new ExclusionRule("process", "fromroot") }, ExclusionsStore.Load(root));   // root untouched
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Corrupt_root_file_still_allows_creation_with_zero_rules()
    {
        var root = TempRoot();
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ nope");
        var keys = new List<KeyPress>();
        keys.AddRange(Type("r2")); keys.Add(KeyPress.Down); keys.AddRange(Type("H1")); keys.Add(KeyPress.Down); keys.AddRange(Type("svc"));
        keys.Add(KeyPress.Enter);
        var console = new TestConsole();
        var created = WizardScreen.Run(console, new ScriptedKeySource(keys), root, "me", () => Now);
        Assert.NotNull(created);
        Assert.Empty(RunConfigReader.ReadFile(Path.Combine(created!, "run.json")).Settings.Exclusions);
        Assert.Contains("invalid root file", console.Output);
        Directory.Delete(root, recursive: true);
    }
}
