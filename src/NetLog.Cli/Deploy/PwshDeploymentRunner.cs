using System.Diagnostics;
using System.Text.Json;

namespace NetLog.Cli.Deploy;

/// <summary>
/// Shells to `pwsh -File deploy/deploy-entry.ps1`. The service-account password is supplied
/// via the child process environment variable NETLOG_DEPLOY_PASSWORD (never on the command line,
/// so it cannot appear in a process listing). The entry script emits one compact JSON result per
/// target on stdout, which we parse.
/// </summary>
public sealed class PwshDeploymentRunner : IDeploymentRunner
{
    private readonly string _entryScript;
    private readonly string _pwshExe;

    public PwshDeploymentRunner(string? entryScript = null, string? pwshExe = null)
    {
        _entryScript = entryScript ?? ResolveEntryScript();
        _pwshExe = pwshExe ?? "pwsh";
    }

    public IReadOnlyList<DeployResult> Run(DeployRequest request)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pwshExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-File"); psi.ArgumentList.Add(_entryScript);
        psi.ArgumentList.Add("-Action"); psi.ArgumentList.Add(request.Action);
        psi.ArgumentList.Add("-Targets"); psi.ArgumentList.Add(request.Targets);
        psi.ArgumentList.Add("-RunName"); psi.ArgumentList.Add(request.RunName);
        if (request.RunPath is not null) { psi.ArgumentList.Add("-RunPath"); psi.ArgumentList.Add(request.RunPath); }
        if (request.Account is not null) { psi.ArgumentList.Add("-Account"); psi.ArgumentList.Add(request.Account); }
        if (request.CollectorSource is not null) { psi.ArgumentList.Add("-CollectorSource"); psi.ArgumentList.Add(request.CollectorSource); }
        if (request.StartUtc is not null) { psi.ArgumentList.Add("-StartUtc"); psi.ArgumentList.Add(request.StartUtc); }
        if (request.StartNow) psi.ArgumentList.Add("-StartNow");

        if (request.Password is not null)
            psi.Environment["NETLOG_DEPLOY_PASSWORD"] = request.Password;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start pwsh.");
        // Read both streams concurrently to avoid a pipe-buffer deadlock (a verbose stderr while we
        // block on stdout could otherwise hang the child).
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        var results = new List<DeployResult>();
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            DeployResult? r = null;
            try { r = JsonSerializer.Deserialize<DeployResult>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException) { /* ignore non-JSON noise lines */ }
            if (r is not null) results.Add(r);
        }

        if (results.Count == 0 && proc.ExitCode != 0)
            results.Add(new DeployResult(request.Targets, "Failed", string.IsNullOrWhiteSpace(stderr) ? "pwsh exited non-zero" : stderr.Trim()));

        return results;
    }

    private static string ResolveEntryScript()
    {
        var overridePath = Environment.GetEnvironmentVariable("NETLOG_DEPLOY_SCRIPT");
        if (!string.IsNullOrEmpty(overridePath)) return overridePath;
        // Walk up from the executing assembly to find the repo's deploy/deploy-entry.ps1.
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "deploy", "deploy-entry.ps1");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return Path.Combine("deploy", "deploy-entry.ps1");
    }
}
