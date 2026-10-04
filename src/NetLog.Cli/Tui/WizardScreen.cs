using NetLog.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>Screen 1c — the themed new-run form. Returns the created run path, or null if cancelled.</summary>
public static class WizardScreen
{
    public static string? Run(IAnsiConsole console, IKeySource keys, string root, string currentUser, Func<DateTimeOffset> now)
    {
        var form = WizardForm.Default();
        string? error = null;
        string? created = null;

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
                    default:
                        return false;
                }
            });

        return created;
    }

    private static IRenderable Compose(IRenderable form, string? error) =>
        error is null ? form : new Rows(form, new Markup($"[{Theme.Red}]{Markup.Escape(error)}[/]"));
}
