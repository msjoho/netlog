using NetLog.Cli.Tui;
using NetLog.Core.Models;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class ExclusionsScreenTests
{
    private static IEnumerable<KeyPress> Type(string s) => s.Select(KeyPress.Of);

    [Fact]
    public void Add_remove_then_enter_returns_the_edited_list()
    {
        var initial = new[] { new ExclusionRule("process", "old") };
        var keys = new List<KeyPress> { KeyPress.Of('a') };
        keys.AddRange(Type("host:nas01*@out"));
        keys.Add(KeyPress.Enter);          // commit the add line
        keys.Add(KeyPress.Up);             // back to "old"
        keys.Add(KeyPress.Of('d'));        // remove it
        keys.Add(KeyPress.Enter);          // done

        var result = ExclusionsScreen.Run(new TestConsole(), new ScriptedKeySource(keys), initial, "exclusions · root", @"\\srv\netlog");

        Assert.NotNull(result);
        var r = Assert.Single(result!);
        Assert.Equal(new ExclusionRule("host", "nas01*", "out"), r);
    }

    [Fact]
    public void Esc_returns_null_and_leaves_the_input_untouched()
    {
        var initial = new[] { new ExclusionRule("process", "old") };
        var result = ExclusionsScreen.Run(new TestConsole(), new ScriptedKeySource(new[] { KeyPress.Of('d'), KeyPress.Esc }), initial, "t", "c");
        Assert.Null(result);
        Assert.Single(initial);
    }
}
