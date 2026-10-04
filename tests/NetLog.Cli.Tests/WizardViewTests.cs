using NetLog.Cli.Rendering;
using NetLog.Cli.Tui;
using Spectre.Console.Testing;

namespace NetLog.Cli.Tests;

public class WizardViewTests
{
    private static string Render(WizardForm form)
    {
        var console = new TestConsole();
        console.Write(WizardView.Render(form, @"\\srv\netlog", @"CORP\mjoho"));
        return console.Output;
    }

    [Fact]
    public void Shows_fields_root_and_target_path()
    {
        var f = WizardForm.Default() with { RunName = "audit-q3", Targets = "H1,H2" };
        var o = Render(f);
        Assert.Contains("audit-q3", o);
        Assert.Contains("H1,H2", o);
        Assert.Contains("Duration", o);
        Assert.Contains(@"\\srv\netlog", o);   // root shown
    }

    [Fact]
    public void Targets_focused_shows_comma_hint()
    {
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.Targets };
        Assert.Contains("comma", Render(f), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void End_focused_in_duration_mode_shows_format_reference_and_tab_to_date()
    {
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.End };
        var o = Render(f);
        Assert.Contains("d days", o);
        Assert.Contains("switch to date", o);
    }

    [Fact]
    public void Account_focused_offers_current_user_in_footer()
    {
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.Account };
        Assert.Contains(@"CORP\mjoho", Render(f));
    }

    [Fact]
    public void Timestamps_focused_shows_default_off_and_switch_on_tab()
    {
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.Timestamps };
        var o = Render(f);
        Assert.Contains("Timestamps", o);
        Assert.Contains("off", o);
        Assert.Contains("switch on", o);
    }
}
