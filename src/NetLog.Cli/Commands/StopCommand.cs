using System.ComponentModel;
using NetLog.Cli.Deploy;
using NetLog.Core.Io;
using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetLog.Cli.Commands;

public sealed class StopSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("Run folder containing run.json.")]
    public string Path { get; set; } = "";

    public override ValidationResult Validate() =>
        string.IsNullOrWhiteSpace(Path) ? ValidationResult.Error("--path is required") : ValidationResult.Success();
}

public sealed class StopCommand : Command<StopSettings>
{
    private readonly IDeploymentRunner _runner;

    public StopCommand() : this(new PwshDeploymentRunner()) { }
    internal StopCommand(IDeploymentRunner runner) { _runner = runner; }

    protected override int Execute(CommandContext context, StopSettings settings, CancellationToken cancellationToken)
    {
        var runJson = System.IO.Path.Combine(settings.Path, "run.json");
        if (!File.Exists(runJson))
        {
            AnsiConsole.MarkupLine($"[red]No run.json found at[/] {Markup.Escape(settings.Path)}");
            return 1;
        }

        var config = RunConfigReader.ReadFile(runJson);
        var request = BuildTeardownRequest(config, settings.Path);
        var results = _runner.Run(request);
        return StartCommand.RenderResults(results, "Teardown");
    }

    /// <summary>Pure builder (testable without Spectre/CommandContext): teardown carries no password.</summary>
    public static DeployRequest BuildTeardownRequest(RunConfig config, string runPath) =>
        new DeployRequest(
            Action: "teardown",
            Targets: string.Join(",", config.Targets),
            RunName: config.RunName,
            RunPath: runPath,
            Account: config.ServiceAccount,
            StartNow: false,
            CollectorSource: null,
            Password: null);
}
