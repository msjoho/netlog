using NetLog.Cli.Deploy;
using NetLog.Cli.Tui;
using NetLog.Core.Io;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetLog.Cli.Commands;

// NOTE: The non-generic Command base in Spectre.Console.Cli 0.55.0 uses
// Execute(CommandContext, CancellationToken) — confirmed via the XML docs.
public sealed class TuiCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine("[yellow]The interactive TUI needs a terminal.[/] Use [green]netlog status --path <run>[/] for a one-shot view.");
            return 1;
        }

        var profile = new ProfileStore(ProfileStore.DefaultPath());
        var app = new TuiApp(AnsiConsole.Console, new ConsoleKeySource(), profile, new PwshDeploymentRunner(), () => DateTimeOffset.UtcNow);
        return app.Run();
    }
}
