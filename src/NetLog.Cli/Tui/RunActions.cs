using NetLog.Cli.Commands;
using NetLog.Cli.Deploy;
using NetLog.Cli.Rendering;
using NetLog.Core.Io;
using NetLog.Core.RunSpec;
using Spectre.Console;

namespace NetLog.Cli.Tui;

/// <summary>The modal actions a run dashboard can trigger (deploy / destroy / new window). Each runs
/// in the themed, flicker-free TUI (no console prompts) and pauses on its result before the dashboard
/// redraws.</summary>
public sealed class RunActions
{
    private readonly IAnsiConsole _console;
    private readonly IKeySource _keys;
    private readonly IDeploymentRunner _runner;
    private readonly Func<DateTimeOffset> _now;

    public RunActions(IAnsiConsole console, IKeySource keys, IDeploymentRunner runner, Func<DateTimeOffset> now)
    {
        _console = console;
        _keys = keys;
        _runner = runner;
        _now = now;
    }

    public void Deploy(string path)
    {
        var runJson = Path.Combine(path, "run.json");
        if (!File.Exists(runJson)) { Info("[red]No run.json — cannot deploy.[/]"); return; }
        var config = RunConfigReader.ReadFile(runJson);
        if (config.Targets.Count == 0) { Info("[yellow]No targets to deploy.[/]"); return; }
        if (string.IsNullOrWhiteSpace(config.RunName))
        {
            Info("[red]run.json has an empty run name[/] (created at a share root). Recreate the run with a run subfolder.");
            return;
        }
        if (StartCommand.WindowEnded(config, _now()))
        {
            Info($"[red]This run's window already ended[/] ({config.Schedule.StopUtc:u}). Use 'new window' first.");
            return;
        }

        var password = PasswordScreen.Run(_console, _keys, config.ServiceAccount);
        if (password is null) return;   // cancelled

        var request = StartCommand.BuildRequest(config, path, password, DeployPaths.DefaultCollectorSource(), _now());
        ShowResults(_runner.Run(request), "Deploy");
    }

    public void Edit(string path)
    {
        var runJson = Path.Combine(path, "run.json");
        if (!File.Exists(runJson)) { Info("[red]No run.json — cannot edit.[/]"); return; }
        var config = RunConfigReader.ReadFile(runJson);

        if (RunEditScreen.Run(_console, _keys, path, config, _now))
            Info($"[{Theme.Green}]Run updated.[/] Edits apply within one archive cycle on running collectors.");
    }

    public void Merge(string path)
    {
        var runJson = Path.Combine(path, "run.json");
        if (!File.Exists(runJson)) { Info("[red]No run.json — cannot merge.[/]"); return; }
        var config = RunConfigReader.ReadFile(runJson);
        if (config.Settings.ArchiveLevel != "Full")
        {
            Info("[yellow]Merge is only for Full runs[/] — this run already archives at Merged level.");
            return;
        }

        // The collector rewrites connections.csv (Full) each cycle, so a live collector would
        // overwrite the merge — only merge once collection has stopped.
        var report = NetLog.Core.Analysis.ReportBuilder.Build(RunReader.Read(path), _now());
        if (report.Hosts.Any(h => h.Liveness == NetLog.Core.Models.HostLiveness.Live))
        {
            Info("[yellow]A collector is still live.[/] Destroy the run (or wait for the window to end), then merge — otherwise the collector would overwrite the merged file.");
            return;
        }

        var hosts = report.Hosts.Count;
        if (!ConfirmScreen.Run(_console, _keys,
                $"Merge (condense Full → Merged) connections for {hosts} host(s)?",
                "Originals are kept under _archive/<timestamp>/; the run stays recorded as Full."))
            return;

        var stamp = _now().ToString("yyyy-MM-ddTHHmmssZ");
        var results = RunMerger.MergeConnections(path, stamp);
        ShowMergeResults(results, stamp);
    }

    private void ShowMergeResults(IReadOnlyList<MergeResult> results, string stamp)
    {
        _console.Clear();
        var table = new Table().Title("Merge").Border(TableBorder.Rounded);
        table.AddColumn("Host"); table.AddColumn(new TableColumn("Full").RightAligned()); table.AddColumn(new TableColumn("Merged").RightAligned());
        foreach (var r in results) table.AddRow(Markup.Escape(r.HostName), r.Before.ToString("n0"), r.After.ToString("n0"));
        if (results.Count == 0) table.AddRow("[grey]no host data[/]", "", "");
        _console.Write(table);
        _console.MarkupLine($"[{Theme.Gray}]Originals archived under _archive/{Markup.Escape(stamp)}/[/]");
        Pause();
    }

    public void Teardown(string path)
    {
        var runJson = Path.Combine(path, "run.json");
        if (!File.Exists(runJson)) { Info("[red]No run.json — cannot destroy.[/]"); return; }
        var config = RunConfigReader.ReadFile(runJson);

        var ok = ConfirmScreen.Run(_console, _keys,
            $"Destroy the collector task on {config.Targets.Count} target(s)?",
            "run.json and the collected data on the share are kept — you can re-deploy later.");
        if (!ok) return;

        ShowResults(_runner.Run(StopCommand.BuildTeardownRequest(config, path)), "Destroy");
    }

    public void NewWindow(string path)
    {
        var runJson = Path.Combine(path, "run.json");
        if (!File.Exists(runJson)) { Info("[red]No run.json — cannot start a new window.[/]"); return; }
        var existing = RunConfigReader.ReadFile(runJson);

        var sched = NewWindowScreen.Run(_console, _keys, existing.RunName);
        if (sched is null) return;

        try
        {
            var updated = RunConfigFactory.WithNewSchedule(existing, sched.Value.Start, sched.Value.Stop, sched.Value.Duration, _now());
            RunArchiveRotation.SnapshotCurrentRun(path, existing.RunSequence);
            RunConfigWriter.WriteFile(runJson, updated);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Info($"[red]Invalid schedule:[/] {Markup.Escape(ex.Message)}");
            return;
        }

        if (ConfirmScreen.Run(_console, _keys, "Deploy and start this window now?"))
            Deploy(path);
    }

    private void ShowResults(IReadOnlyList<DeployResult> results, string title)
    {
        _console.Clear();
        StartCommand.RenderResults(results, title);
        Pause();
    }

    private void Info(string markup)
    {
        _console.Clear();
        _console.MarkupLine(markup);
        Pause();
    }

    private void Pause()
    {
        _console.MarkupLine($"[{Theme.Gray}](press a key to continue)[/]");
        _keys.Read(0);
    }
}
