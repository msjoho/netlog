using System.Text.Json;
using NetLog.Cli.Rendering;
using NetLog.Core.Analysis;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetLog.Cli.Commands;

public sealed class StatusCommand : Command<StatusSettings>
{
    protected override int Execute(CommandContext context, StatusSettings settings, CancellationToken cancellationToken)
    {
        var console = AnsiConsole.Console;
        var runJson = System.IO.Path.Combine(settings.Path, "run.json");
        if (!File.Exists(runJson))
        {
            console.MarkupLine($"[red]No run.json found at[/] {Markup.Escape(settings.Path)}");
            return 1;
        }

        RunData data;
        try
        {
            data = RunReader.Read(settings.Path);
        }
        catch (InvalidDataException ex)
        {
            console.MarkupLine($"[red]run.json is invalid:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
        catch (JsonException ex)
        {
            console.MarkupLine($"[red]run.json is malformed:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // FileNotFoundException (TOCTOU after the File.Exists check) is an IOException subclass and is caught here.
            console.MarkupLine($"[red]Cannot read run folder:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }

        var now = DateTimeOffset.UtcNow;
        var report = ReportBuilder.Build(data, now);
        // One-shot static render (no refresh countdown, all hosts) — the same renderer the live TUI uses.
        console.Write(DashboardView.Render(
            new DashboardViewState(report, 0, FocusedIndex: null, now, SecondsToRefresh: null, StaleFor: null),
            TimeZoneInfo.Local));

        // Remember this run's *root* (its parent folder) for the TUI (best-effort; never fail the command).
        try
        {
            var root = System.IO.Path.GetDirectoryName(settings.Path.TrimEnd('\\', '/'));
            if (!string.IsNullOrEmpty(root))
            {
                var store = new ProfileStore(ProfileStore.DefaultPath());
                store.Save(ProfileStore.AddRecent(store.Load(), root));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // profile is non-critical
        }

        return 0;
    }
}
