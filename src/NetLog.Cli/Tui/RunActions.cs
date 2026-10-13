using NetLog.Cli.Commands;
using NetLog.Cli.Deploy;
using NetLog.Cli.Rendering;
using NetLog.Core.Io;
using NetLog.Core.Models;
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
            Info($"[{Theme.Green}]Run updated.[/] Edits apply within one poll (exclusions) / one archive cycle on running collectors.");
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

    /// <summary>[d] on the runs browser: destroy the collector on every target and purge the run's
    /// local state there (the shared binaries too when no other NetLog task remains), then delete the
    /// run folder on the share — all collected data included. A failed host does not block the share
    /// deletion outright: the user is asked once more, since a host that is gone or denies access
    /// would otherwise make the run undeletable.</summary>
    public void Delete(string path)
    {
        var runJson = Path.Combine(path, "run.json");
        if (!File.Exists(runJson))
        {
            Info("[red]No run.json — cannot delete.[/] Only run folders are deleted here; remove the folder by hand if it is not a run.");
            return;
        }
        var config = RunConfigReader.ReadFile(runJson);
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (!ConfirmScreen.Run(_console, _keys,
                $"Delete run '{name}' and destroy its collector on {config.Targets.Count} target(s)?",
                "ALL collected data in this run folder on the share will be deleted — this cannot be undone. " +
                "On each host the scheduled task and the run's local cache/log are removed; the shared collector binaries only when no other NetLog task remains."))
            return;

        var results = _runner.Run(StopCommand.BuildTeardownRequest(config, path, purge: true));
        ShowResults(results, "Destroy");

        var failed = results.Count(r => r.Status != "Success");
        if (failed > 0 && !ConfirmScreen.Run(_console, _keys,
                $"Destroy failed on {failed} host(s). Delete the run folder on the share anyway?",
                "The collector task may still exist there — remove it by hand (Task Scheduler: NetLogCollector_<run>) or re-run destroy later."))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
            Info($"[{Theme.Green}]Run '{Markup.Escape(name)}' deleted.[/]");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Info($"[red]Could not delete the run folder:[/] {Markup.Escape(ex.Message)}\n[grey]A collector may still be writing to it — destroy it first, then try again.[/]");
        }
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

    /// <summary>Edit the root's exclusions.json template. Changes affect runs created afterwards only.</summary>
    public void EditRootExclusions(string root)
    {
        IReadOnlyList<ExclusionRule> current;
        try { current = ExclusionsStore.Load(root); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Info($"[red]Cannot read {Markup.Escape(ExclusionsStore.FileName)}:[/] {Markup.Escape(ex.Message)}\n[grey]Fix the file by hand, then try again.[/]");
            return;
        }

        var edited = ExclusionsScreen.Run(_console, _keys, current, "exclusions · root", root);
        if (edited is null) return;

        try
        {
            ExclusionsStore.Save(root, edited);
            Info($"[{Theme.Green}]Root exclusions saved.[/] Runs created under this root from now on start with these {edited.Count} rule(s); existing runs are unchanged.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Info($"[red]Could not write {Markup.Escape(ExclusionsStore.FileName)}:[/] {Markup.Escape(ex.Message)}");
        }
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
