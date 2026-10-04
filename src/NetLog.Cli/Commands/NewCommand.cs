using System.ComponentModel;
using NetLog.Core.Io;
using NetLog.Core.Models;
using NetLog.Core.RunSpec;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetLog.Cli.Commands;

public sealed class NewSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("Run folder (network share). run.json is written here.")]
    public string Path { get; set; } = "";

    [CommandOption("--targets <HOSTS>")]
    [Description("Comma-separated target host names.")]
    public string Targets { get; set; } = "";

    [CommandOption("--account <ACCOUNT>")]
    [Description("Domain service account (DOMAIN\\\\svc) the collector runs as.")]
    public string Account { get; set; } = "";

    [CommandOption("--start <START>")]
    [Description("Start: 'now' or an ISO-8601 UTC instant.")]
    public string Start { get; set; } = "now";

    [CommandOption("--stop <STOP>")]
    [Description("Stop: an ISO-8601 UTC instant (mutually exclusive with --duration).")]
    public string? Stop { get; set; }

    [CommandOption("--duration <DURATION>")]
    [Description("Run length like 7d/12h/90m (mutually exclusive with --stop).")]
    public string? Duration { get; set; }

    [CommandOption("--archive-level <LEVEL>")]
    [Description("Full or Merged.")]
    public string ArchiveLevel { get; set; } = "Merged";

    [CommandOption("--poll <SECONDS>")]
    [Description("Collector poll interval in seconds.")]
    public int PollSeconds { get; set; } = 10;

    [CommandOption("--archive-interval <MINUTES>")]
    [Description("Archive (flush to share) interval in minutes.")]
    public int ArchiveMinutes { get; set; } = 60;

    [CommandOption("--cache-days <DAYS>")]
    [Description("Local cache lifetime in days (mutually exclusive with --cache-mb).")]
    public int? CacheDays { get; set; }

    [CommandOption("--cache-mb <MB>")]
    [Description("Local cache max size in MB (mutually exclusive with --cache-days).")]
    public int? CacheMb { get; set; }

    [CommandOption("--no-resolve-hostnames")]
    [Description("Do not resolve remote hostnames on the collectors (emergency switch for broken DNS).")]
    public bool NoResolveHostnames { get; set; }

    [CommandOption("--timestamps")]
    [Description("Record first/last-seen timestamps per connection (off by default).")]
    public bool Timestamps { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Path)) return ValidationResult.Error("--path is required");
        if (string.IsNullOrWhiteSpace(Account)) return ValidationResult.Error("--account is required");
        if (ArchiveLevel is not ("Full" or "Merged")) return ValidationResult.Error("--archive-level must be Full or Merged");
        if (CacheDays is not null && CacheMb is not null) return ValidationResult.Error("Specify --cache-days or --cache-mb, not both");
        if (string.IsNullOrWhiteSpace(Targets)) return ValidationResult.Error("--targets is required (comma-separated host names)");
        return ValidationResult.Success();
    }
}

public sealed class NewCommand : Command<NewSettings>
{
    protected override int Execute(CommandContext context, NewSettings settings, CancellationToken cancellationToken)
    {
        RunConfig config;
        try
        {
            config = RunConfigFactory.Create(
                settings.Path, settings.Targets, settings.Account,
                settings.Start, settings.Stop, settings.Duration,
                settings.ArchiveLevel, settings.PollSeconds, settings.ArchiveMinutes,
                settings.CacheDays, settings.CacheMb, DateTimeOffset.UtcNow,
                resolveHostnames: !settings.NoResolveHostnames,
                timestamps: settings.Timestamps);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            AnsiConsole.MarkupLine($"[red]Invalid run configuration:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        var runJson = System.IO.Path.Combine(settings.Path, "run.json");
        try
        {
            // Re-running `new` over an existing run folder starts the NEXT run there:
            // continue the sequence (don't reset to 1) and snapshot the finishing run's
            // cumulative archive into its per-run file first.
            if (File.Exists(runJson))
            {
                try
                {
                    var existing = RunConfigReader.ReadFile(runJson);
                    RunArchiveRotation.SnapshotCurrentRun(settings.Path, existing.RunSequence);
                    config = config with { RunSequence = existing.RunSequence + 1 };
                }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
                {
                    // Corrupt run.json: treat as a fresh run folder (sequence stays 1).
                }
            }
            RunConfigWriter.WriteFile(runJson, config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine($"[red]Could not write run.json:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }

        AnsiConsole.MarkupLine($"[green]Created run[/] '{Markup.Escape(config.RunName)}' [grey]->[/] {Markup.Escape(runJson)}");
        AnsiConsole.MarkupLine($"Targets: {Markup.Escape(string.Join(", ", config.Targets))}");
        AnsiConsole.MarkupLine($"Window:  {config.Schedule.StartUtc:u} -> {config.Schedule.StopUtc:u}");
        AnsiConsole.MarkupLine("[grey]Run 'netlog start --path <path>' to deploy.[/]");
        return 0;
    }
}
