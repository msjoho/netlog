namespace NetLog.Cli.Deploy;

public sealed record DeployResult(string Target, string Status, string? Error);

public sealed record DeployRequest(
    string Action,            // "deploy" | "teardown"
    string Targets,           // comma-separated
    string RunName,
    string? RunPath,
    string? Account,
    bool StartNow,
    string? CollectorSource,
    string? Password,         // null for teardown
    string? StartUtc = null); // ISO instant; becomes a one-time task trigger at the run start

public interface IDeploymentRunner
{
    /// <summary>Runs the deploy/teardown for all targets and returns one result per target.</summary>
    IReadOnlyList<DeployResult> Run(DeployRequest request);
}
