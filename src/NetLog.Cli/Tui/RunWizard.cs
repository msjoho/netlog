using NetLog.Core.Io;
using NetLog.Core.RunSpec;

namespace NetLog.Cli.Tui;

public sealed record WizardAnswers(
    string Path, string Targets, string Account,
    string Start, string? Stop, string? Duration,
    string ArchiveLevel, int PollSeconds, int ArchiveMinutes,
    int? CacheDays, int? CacheMb, bool Timestamps = false);

public static class RunWizard
{
    /// <summary>Pure: builds the config and writes run.json; returns the run path.</summary>
    public static string Build(WizardAnswers a, DateTimeOffset now)
    {
        var config = RunConfigFactory.Create(
            a.Path, a.Targets, a.Account, a.Start, a.Stop, a.Duration,
            a.ArchiveLevel, a.PollSeconds, a.ArchiveMinutes, a.CacheDays, a.CacheMb, now,
            timestamps: a.Timestamps);
        RunConfigWriter.WriteFile(System.IO.Path.Combine(a.Path, "run.json"), config);
        return a.Path;
    }
}
