using NetLog.Cli.Commands;
using NetLog.Cli.Deploy;
using NetLog.Core.Models;

namespace NetLog.Cli.Tests;

public sealed class FakeRunner : IDeploymentRunner
{
    public DeployRequest? Last;
    public IReadOnlyList<DeployResult> Result = new List<DeployResult>();
    public IReadOnlyList<DeployResult> Run(DeployRequest request) { Last = request; return Result; }
}

public class DeployRequestTests
{
    private static RunConfig Config(string[] targets, string account, DateTimeOffset start, DateTimeOffset stop) =>
        new("audit", DateTimeOffset.UtcNow, new RunSchedule(start, stop), targets, account,
            new RunSettings("Merged", 10, 60, 7, null));

    [Fact]
    public void Start_BuildRequest_maps_config_and_sets_StartNow_when_started()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var cfg = Config(new[] { "S1", "S2" }, "DOMAIN\\svc", now.AddHours(-1), now.AddDays(1));
        var req = StartCommand.BuildRequest(cfg, @"\\share\runs\audit", "pw", @"C:\repo\collector", now);

        Assert.Equal("deploy", req.Action);
        Assert.Equal("S1,S2", req.Targets);
        Assert.Equal("audit", req.RunName);
        Assert.Equal(@"\\share\runs\audit", req.RunPath);
        Assert.Equal("DOMAIN\\svc", req.Account);
        Assert.Equal("pw", req.Password);
        Assert.Equal(@"C:\repo\collector", req.CollectorSource);
        Assert.True(req.StartNow);                  // started 1h ago
        Assert.Null(req.StartUtc);                  // start is past: explicit StartTask, no one-time trigger
    }

    [Fact]
    public void Start_BuildRequest_sets_StartUtc_trigger_only_for_a_future_start()
    {
        var now = new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
        var future = Config(new[] { "S1" }, "D\\s", now.AddHours(3), now.AddDays(1));
        var req = StartCommand.BuildRequest(future, @"\\s\a", "pw", @"C:\c", now);
        Assert.False(req.StartNow);
        Assert.Equal(future.Schedule.StartUtc.UtcDateTime.ToString("o"), req.StartUtc);
    }

    [Fact]
    public void WindowEnded_only_after_the_stop_instant()
    {
        var now = new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
        var active = Config(new[] { "S1" }, "D\\s", now.AddHours(-1), now.AddMinutes(10));
        var ended = Config(new[] { "S1" }, "D\\s", now.AddHours(-2), now.AddMinutes(-1));
        Assert.False(StartCommand.WindowEnded(active, now));
        Assert.True(StartCommand.WindowEnded(ended, now));
    }

    [Fact]
    public void Start_BuildRequest_StartNow_false_before_scheduled_start()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var cfg = Config(new[] { "S1" }, "DOMAIN\\svc", now.AddHours(1), now.AddDays(1));
        var req = StartCommand.BuildRequest(cfg, @"\\s\a", "pw", @"C:\c", now);
        Assert.False(req.StartNow);                 // starts in 1h
    }

    [Fact]
    public void Stop_BuildTeardownRequest_has_no_password()
    {
        var now = DateTimeOffset.UtcNow;
        var cfg = Config(new[] { "S1" }, "DOMAIN\\svc", now.AddHours(-1), now.AddDays(1));
        var req = StopCommand.BuildTeardownRequest(cfg, @"\\s\a");
        Assert.Equal("teardown", req.Action);
        Assert.Null(req.Password);
        Assert.Equal("S1", req.Targets);
    }

    [Fact]
    public void Fake_runner_records_request_and_returns_result()
    {
        var fake = new FakeRunner { Result = new List<DeployResult> { new("S1", "Success", null) } };
        var req = new DeployRequest("deploy", "S1", "audit", @"\\s\a", "D\\s", true, @"C:\c", "pw");
        var results = fake.Run(req);
        Assert.Same(req, fake.Last);
        Assert.Equal("Success", Assert.Single(results).Status);
    }
}
