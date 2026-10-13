using NetLog.Cli.Rendering;
using NetLog.Core.Io;
using NetLog.Core.Models;
using NetLog.Core.RunSpec;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Tui;

/// <summary>The in-run edit form screen. Writes the edited run.json on save; returns whether it saved.</summary>
public static class RunEditScreen
{
    public static bool Run(IAnsiConsole console, IKeySource keys, string runFolder, RunConfig config, Func<DateTimeOffset> now)
    {
        var form = RunEditForm.From(config);
        string? error = null;
        var saved = false;

        while (true)
        {
            var pending = RunEditCommand.None;
            ScreenHost.Run(console,
                () => Compose(RunEditView.Render(config, form, TimeZoneInfo.Local, ScreenSize.Of(console)), error),
                () =>
                {
                    if (keys.Read(ScreenHost.PollMs) is not { } key) return false;
                    error = null;
                    var (next, cmd) = RunEditReducer.Reduce(form, key);
                    form = next;
                    switch (cmd)
                    {
                        case RunEditCommand.Save:
                            try
                            {
                                var a = RunEditReducer.ToArgs(form);
                                var updated = RunConfigFactory.WithEdits(config, a.Stop, a.Duration, a.Poll, a.Archive, a.Cache,
                                    config.Settings.MaxCacheSizeMb, now(), exclusions: form.Exclusions);
                                RunConfigWriter.WriteFile(System.IO.Path.Combine(runFolder, "run.json"), updated);
                                saved = true;
                                return true;
                            }
                            catch (Exception ex) when (ex is ArgumentException or FormatException)
                            {
                                error = ex.Message;
                                return false;
                            }
                        case RunEditCommand.Cancel:
                            return true;
                        case RunEditCommand.EditExclusions:
                            pending = cmd; return true;
                        default:
                            return false;
                    }
                });

            if (pending != RunEditCommand.EditExclusions) return saved;

            var edited = ExclusionsScreen.Run(console, keys, form.Exclusions, "exclusions · edit run", config.RunName,
                warning: "changes purge matching rows from the share within one archive cycle");
            if (edited is not null) form = form with { Exclusions = edited };
        }
    }

    private static IRenderable Compose(IRenderable view, string? error) =>
        error is null ? view : new Rows(view, new Markup($"[{Theme.Red}]{Markup.Escape(error)}[/]"));
}
