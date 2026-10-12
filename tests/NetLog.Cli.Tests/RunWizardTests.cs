using NetLog.Cli.Tui;
using NetLog.Core.Io;

namespace NetLog.Cli.Tests;

public class RunWizardTests
{
    [Fact]
    public void Build_writes_run_json_and_returns_path()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nlwiz_" + Guid.NewGuid().ToString("N"), "audit");
        var answers = new WizardAnswers(
            Path: dir, Targets: "S1,S2", Account: "DOMAIN\\svc",
            Start: "now", Stop: null, Duration: "7d",
            ArchiveLevel: "Merged", PollSeconds: 10, ArchiveMinutes: 60,
            CacheDays: 7, CacheMb: null);
        var now = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero);

        var written = RunWizard.Build(answers, now);

        Assert.Equal(dir, written);
        var cfg = RunConfigReader.ReadFile(Path.Combine(dir, "run.json"));
        Assert.Equal("audit", cfg.RunName);
        Assert.Equal(new[] { "S1", "S2" }, cfg.Targets);
        Assert.Equal(now.AddDays(7), cfg.Schedule.StopUtc);

        Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
    }

    [Fact]
    public void Build_passes_timestamps_into_run_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nlwiz_" + Guid.NewGuid().ToString("N"), "audit");
        var answers = new WizardAnswers(
            Path: dir, Targets: "S1", Account: "DOMAIN\\svc",
            Start: "now", Stop: null, Duration: "7d",
            ArchiveLevel: "Merged", PollSeconds: 10, ArchiveMinutes: 60,
            CacheDays: 7, CacheMb: null, Timestamps: true);
        RunWizard.Build(answers, new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero));
        Assert.True(RunConfigReader.ReadFile(Path.Combine(dir, "run.json")).Settings.Timestamps);
        Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
    }

    [Fact]
    public void Build_writes_exclusions_into_run_json()
    {
        var root = Path.Combine(Path.GetTempPath(), "nlwiz_" + Guid.NewGuid().ToString("N"));
        var rules = new[] { new NetLog.Core.Models.ExclusionRule("process", "Teams*") };
        var a = new WizardAnswers(Path.Combine(root, "audit"), "H1", "svc", "now", null, "7d", "Merged", 10, 60, 7, null, Exclusions: rules);
        var created = RunWizard.Build(a, new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(rules, NetLog.Core.Io.RunConfigReader.ReadFile(Path.Combine(created, "run.json")).Settings.Exclusions);
        Directory.Delete(root, recursive: true);
    }
}
