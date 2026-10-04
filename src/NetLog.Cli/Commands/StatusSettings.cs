using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetLog.Cli.Commands;

public sealed class StatusSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("Path to the run folder (contains run.json).")]
    public string Path { get; set; } = "";

    public override ValidationResult Validate() =>
        string.IsNullOrWhiteSpace(Path)
            ? ValidationResult.Error("--path is required")
            : ValidationResult.Success();
}
