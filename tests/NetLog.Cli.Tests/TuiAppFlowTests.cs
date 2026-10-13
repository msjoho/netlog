using NetLog.Cli.Deploy;
using NetLog.Cli.Tui;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

/// <summary>Scripted key source: feeds a fixed sequence of keypresses, then throws if the screen
/// asks for more than the script provides (so a mis-scripted test fails fast instead of hanging).</summary>
public sealed class ScriptedKeySource : IKeySource
{
    private readonly Queue<KeyPress> _keys;
    public ScriptedKeySource(IEnumerable<KeyPress> keys) => _keys = new Queue<KeyPress>(keys);
    public KeyPress? Read(int timeoutMs) =>
        _keys.Count > 0 ? _keys.Dequeue() : throw new InvalidOperationException("key script exhausted");
}

public class TuiAppFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static ProfileStore TempProfile() =>
        new(Path.Combine(Path.GetTempPath(), "nlflow_" + Guid.NewGuid().ToString("N"), "profile.json"));

    private static IEnumerable<KeyPress> Type(string s) => s.Select(KeyPress.Of);

    [Fact]
    public void Quit_from_root_select_exits()
    {
        var app = new TuiApp(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Of('q') }),
            TempProfile(), new FakeRunner(), () => Now);
        Assert.Equal(0, app.Run());
    }

    [Fact]
    public void Quit_from_root_select_exits_on_the_live_interactive_path()
    {
        // Interactive console -> ScreenHost uses the Spectre Live path; ensure it doesn't throw/hang.
        var app = new TuiApp(new TestConsole().Interactive(), new ScriptedKeySource(new[] { KeyPress.Of('q') }),
            TempProfile(), new FakeRunner(), () => Now);
        Assert.Equal(0, app.Run());
    }

    [Fact]
    public void Typing_a_new_root_remembers_it_then_quitting_browser_exits()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlflow_root_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var profile = TempProfile();

        var keys = new List<KeyPress> { KeyPress.Of('r') };   // enter new-root editing
        keys.AddRange(Type(root));                            // type the path
        keys.Add(KeyPress.Enter);                             // submit -> opens root (empty browser)
        keys.Add(KeyPress.Of('q'));                           // quit the browser

        var app = new TuiApp(new TestConsole(), new ScriptedKeySource(keys), profile, new FakeRunner(), () => Now);

        Assert.Equal(0, app.Run());
        Assert.Contains(root, profile.Load().RecentRootPaths);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Opening_a_run_renders_its_dashboard_then_back_and_quit()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlflow_open_" + Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(root, "audit-q2");
        Directory.CreateDirectory(runDir);
        var cfg = new RunConfig("audit-q2", Now.AddDays(-1),
            new RunSchedule(Now.AddHours(-1), Now.AddDays(1)), new[] { "H1" }, "DOMAIN\\svc",
            new RunSettings("Merged", 10, 60, 7, null));
        RunConfigWriter.WriteFile(Path.Combine(runDir, "run.json"), cfg);

        var profile = TempProfile();
        profile.Save(new Profile(new[] { root }));

        var console = new TestConsole();
        // Enter: open root -> browser. Enter: open run -> dashboard. q: back. q: quit.
        var keys = new[] { KeyPress.Enter, KeyPress.Enter, KeyPress.Of('q'), KeyPress.Of('q') };
        var app = new TuiApp(console, new ScriptedKeySource(keys), profile, new FakeRunner(), () => Now);

        Assert.Equal(0, app.Run());
        Assert.Contains("audit-q2", console.Output);   // dashboard/browser rendered the run

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void D_in_the_browser_offers_to_delete_the_highlighted_run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlflow_del_" + Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(root, "audit-q2");
        Directory.CreateDirectory(runDir);
        var cfg = new RunConfig("audit-q2", Now.AddDays(-1),
            new RunSchedule(Now.AddHours(-1), Now.AddDays(1)), new[] { "H1" }, "DOMAIN\\svc",
            new RunSettings("Merged", 10, 60, 7, null));
        RunConfigWriter.WriteFile(Path.Combine(runDir, "run.json"), cfg);
        var profile = TempProfile();
        profile.Save(new Profile(new[] { root }));

        var console = new TestConsole();
        var runner = new FakeRunner { Result = new[] { new DeployResult("H1", "Success", null) } };
        // Enter: open root. d: delete -> confirm. y: yes -> results pause (Enter) -> deleted info (Enter). q: quit.
        var keys = new[] { KeyPress.Enter, KeyPress.Of('d'), KeyPress.Of('y'), KeyPress.Enter, KeyPress.Enter, KeyPress.Of('q') };
        var app = new TuiApp(console, new ScriptedKeySource(keys), profile, runner, () => Now);

        Assert.Equal(0, app.Run());
        Assert.Equal("remove", runner.Last?.Action);
        Assert.False(Directory.Exists(runDir));
        Assert.True(Directory.Exists(root));            // only the run went, not the root

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void X_in_the_browser_edits_and_saves_the_root_exclusions()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlflow_x_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var keys = new List<KeyPress> { KeyPress.Of('r') };
        keys.AddRange(Type(root)); keys.Add(KeyPress.Enter);          // new root
        keys.Add(KeyPress.Of('x'));                                    // root exclusions
        keys.Add(KeyPress.Of('a')); keys.AddRange(Type("user:CORP\\svc-*")); keys.Add(KeyPress.Enter);
        keys.Add(KeyPress.Enter);                                      // done -> saves
        keys.Add(KeyPress.Enter);                                      // dismiss "saved" info
        keys.Add(KeyPress.Of('q'));                                    // quit browser

        var app = new TuiApp(new TestConsole(), new ScriptedKeySource(keys), TempProfile(), new FakeRunner(), () => Now);
        Assert.Equal(0, app.Run());

        var r = Assert.Single(ExclusionsStore.Load(root));
        Assert.Equal(new ExclusionRule("user", "CORP\\svc-*"), r);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void X_with_a_corrupt_root_file_shows_the_error_and_does_not_overwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlflow_xc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(ExclusionsStore.PathFor(root), "{ nope");
        var keys = new List<KeyPress> { KeyPress.Of('r') };
        keys.AddRange(Type(root)); keys.Add(KeyPress.Enter);
        keys.Add(KeyPress.Of('x'));
        keys.Add(KeyPress.Enter);                                      // dismiss error
        keys.Add(KeyPress.Of('q'));

        var console = new TestConsole();
        new TuiApp(console, new ScriptedKeySource(keys), TempProfile(), new FakeRunner(), () => Now).Run();

        Assert.Contains("exclusions.json", console.Output);
        Assert.Equal("{ nope", File.ReadAllText(ExclusionsStore.PathFor(root)));
        Directory.Delete(root, recursive: true);
    }
}
