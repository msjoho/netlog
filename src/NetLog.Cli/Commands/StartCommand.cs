using System.ComponentModel;
using NetLog.Cli.Deploy;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetLog.Cli.Commands;

public sealed class StartSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("Run folder containing run.json.")]
    public string Path { get; set; } = "";

    [CommandOption("--collector-source <DIR>")]
    [Description("Path to the collector folder to deploy (defaults to the repo's collector dir).")]
    public string? CollectorSource { get; set; }

    [CommandOption("--password <PWD>")]
    [Description("Service-account password. WARNING: visible in the process listing — prefer the secure prompt (omit this).")]
    public string? Password { get; set; }

    public override ValidationResult Validate() =>
        string.IsNullOrWhiteSpace(Path) ? ValidationResult.Error("--path is required") : ValidationResult.Success();
}

public sealed class StartCommand : Command<StartSettings>
{
    private readonly IDeploymentRunner _runner;

    public StartCommand() : this(new PwshDeploymentRunner()) { }
    internal StartCommand(IDeploymentRunner runner) { _runner = runner; }

    protected override int Execute(CommandContext context, StartSettings settings, CancellationToken cancellationToken)
    {
        var runJson = System.IO.Path.Combine(settings.Path, "run.json");
        if (!File.Exists(runJson))
        {
            AnsiConsole.MarkupLine($"[red]No run.json found at[/] {Markup.Escape(settings.Path)}");
            return 1;
        }

        var config = RunConfigReader.ReadFile(runJson);
        if (config.Targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No targets configured in run.json — nothing to deploy.[/]");
            return 0;
        }
        if (string.IsNullOrWhiteSpace(config.RunName))
        {
            AnsiConsole.MarkupLine(
                $"[red]run.json has an empty run name[/] (it was created at a share root). Recreate the run with a run subfolder, e.g. {Markup.Escape(@"\\server\share\audit")}.");
            return 1;
        }
        if (WindowEnded(config, DateTimeOffset.UtcNow))
        {
            AnsiConsole.MarkupLine(
                $"[red]This run's window already ended[/] ({config.Schedule.StopUtc:u}) — the collector would exit immediately. Set a new time window first (TUI: 'Start another run').");
            return 1;
        }

        if (settings.Password is not null)
            AnsiConsole.MarkupLine("[yellow]WARNING:[/] --password is visible in the process listing; prefer the secure prompt (omit --password).");

        var password = settings.Password ?? CredentialPrompt.ReadPassword($"Password for {config.ServiceAccount}: ");
        var collectorSource = settings.CollectorSource ?? DeployPaths.DefaultCollectorSource();

        var request = BuildRequest(config, settings.Path, password, collectorSource, DateTimeOffset.UtcNow);
        var results = _runner.Run(request);
        return RenderResults(results, "Deploy");
    }

    /// <summary>
    /// Pure builder (testable without Spectre/CommandContext). A start that is due now (or past)
    /// is kicked off explicitly via StartTask (StartNow); only a genuinely FUTURE start gets a
    /// one-time task trigger — a trigger at "now" would already be in the past once the task is
    /// registered and would never fire.
    /// </summary>
    public static DeployRequest BuildRequest(RunConfig config, string runPath, string? password, string collectorSource, DateTimeOffset now)
    {
        var startIsFuture = now < config.Schedule.StartUtc;
        return new DeployRequest(
            Action: "deploy",
            Targets: string.Join(",", config.Targets),
            RunName: config.RunName,
            RunPath: runPath,
            Account: config.ServiceAccount,
            StartNow: !startIsFuture,
            CollectorSource: collectorSource,
            Password: password,
            StartUtc: startIsFuture ? config.Schedule.StartUtc.UtcDateTime.ToString("o") : null);
    }

    /// <summary>A run whose window already ended would deploy a collector that exits immediately.</summary>
    public static bool WindowEnded(RunConfig config, DateTimeOffset now) => now >= config.Schedule.StopUtc;

    public static int RenderResults(IReadOnlyList<DeployResult> results, string title)
    {
        var table = new Table().Title(title).Border(TableBorder.Rounded);
        table.AddColumn("Target");
        table.AddColumn("Status");
        table.AddColumn("Detail");
        foreach (var r in results)
        {
            var status = r.Status == "Success" ? "[green]Success[/]" : "[red]Failed[/]";
            table.AddRow(Markup.Escape(r.Target), status, Markup.Escape(r.Error ?? ""));
        }
        AnsiConsole.Write(table);
        return results.All(r => r.Status == "Success") ? 0 : 2;
    }
}
