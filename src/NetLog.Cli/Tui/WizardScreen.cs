using NetLog.Cli.Rendering;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Screen 1c — the themed new-run form. Returns the created run path, or null if cancelled.</summary>
public static class WizardScreen
{
    public static string? Run(IAnsiConsole console, IKeySource keys, string root, string currentUser, Func<DateTimeOffset> now)
    {
        var form = WizardForm.Default();
        try { form = form with { Exclusions = ExclusionsStore.Load(root) }; }
        catch (InvalidDataException ex) { form = form with { RootExclusionsError = ex.Message }; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { form = form with { RootExclusionsError = ex.Message }; }

        string? error = null;
        string? created = null;

        while (true)
        {
            var pending = WizardCommand.None;
            ScreenHost.Run(console,
                () => Compose(WizardView.Render(form, root, currentUser), error),
                () =>
                {
                    if (keys.Read(0) is not { } key) return false;
                    error = null;
                    var (next, cmd) = WizardReducer.Reduce(form, currentUser, key);
                    form = next;
                    switch (cmd)
                    {
                        case WizardCommand.Create:
                            try { created = RunWizard.Build(WizardReducer.ToAnswers(form, root), now()); return true; }
                            catch (Exception ex) when (ex is ArgumentException or FormatException) { error = ex.Message; return false; }
                        case WizardCommand.Cancel:
                            return true;
                        case WizardCommand.EditExclusions:
                            pending = cmd; return true;
                        default:
                            return false;
                    }
                });

            if (pending != WizardCommand.EditExclusions) return created;

            var edited = ExclusionsScreen.Run(console, keys, form.Exclusions ?? Array.Empty<ExclusionRule>(), "exclusions · new run", root);
            if (edited is not null) form = form with { Exclusions = edited, ExclusionsEdited = true };
        }
    }

    private static IRenderable Compose(IRenderable form, string? error) =>
        error is null ? form : new Rows(form, new Markup($"[{Theme.Red}]{Markup.Escape(error)}[/]"));
}
