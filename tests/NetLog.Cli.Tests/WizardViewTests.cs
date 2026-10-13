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
    public void Timestamps_focused_shows_default_on_and_switch_off_tab()
    {
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.Timestamps };
        var o = Render(f);
        Assert.Contains("Timestamps", o);
        Assert.Contains("switch off", o);
        Assert.DoesNotContain("switch on", o);
    }

    [Fact]
    public void Exclusions_row_shows_count_origin_and_tab_label()
    {
        var rules = new[] { new NetLog.Core.Models.ExclusionRule("host", "nas*"), new NetLog.Core.Models.ExclusionRule("user", "x") };
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.Exclusions, Exclusions = rules };
        var o = Render(f);
        Assert.Contains("Exclusions", o);
        Assert.Contains("2 rules", o);
        Assert.Contains("(from root)", o);
        Assert.Contains("edit exclusions", o);        // footer tab label
        Assert.DoesNotContain("(from root)", Render(f with { ExclusionsEdited = true }));
        Assert.Contains("0 rules", Render(WizardForm.Default()));
    }

    [Fact]
    public void Invalid_root_file_is_shown_in_red_not_hidden()
    {
        var f = WizardForm.Default() with { FieldIndex = (int)WizardField.Exclusions, RootExclusionsError = "exclusions.json rule 2: Unknown kind 'port'" };
        var o = Render(f);
        Assert.Contains("invalid root file", o);
        Assert.Contains("Unknown kind 'port'", o);
    }

    [Fact]
    public void Edited_rules_under_a_corrupt_root_show_their_count()
    {
        var rules = new[] { new NetLog.Core.Models.ExclusionRule("host", "nas*"), new NetLog.Core.Models.ExclusionRule("user", "x") };
        var f = WizardForm.Default() with
        {
            RootExclusionsError = "exclusions.json rule 2: Unknown kind 'port'",
            ExclusionsEdited = true,
            Exclusions = rules,
        };
        var o = Render(f);
        Assert.Contains("2 rules", o);
        Assert.DoesNotContain("invalid root file", o);
    }
}
