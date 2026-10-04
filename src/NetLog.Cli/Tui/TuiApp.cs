using NetLog.Cli.Deploy;
using NetLog.Core.Io;
using Spectre.Console;

namespace NetLog.Cli.Tui;

/// <summary>Orchestrates the screen flow: root select → runs browser → dashboard / new-run wizard.
/// Pure navigation; each screen owns its own loop. Input comes from an <see cref="IKeySource"/> so
/// the flow is testable with a scripted source.</summary>
public sealed class TuiApp
{
    private readonly IAnsiConsole _console;
    private readonly IKeySource _keys;
    private readonly ProfileStore _profile;
    private readonly IDeploymentRunner _runner;
    private readonly Func<DateTimeOffset> _now;
    private readonly int _refreshSeconds;

    public TuiApp(IAnsiConsole console, IKeySource keys, ProfileStore profile,
        IDeploymentRunner runner, Func<DateTimeOffset> now, int refreshSeconds = 5)
    {
        _console = console;
        _keys = keys;
        _profile = profile;
        _runner = runner;
        _now = now;
        _refreshSeconds = refreshSeconds;
    }

    public int Run()
    {
        var actions = new RunActions(_console, _keys, _runner, _now);
        var currentUser = CurrentUser();

        while (true)
        {
            var root = RootSelectScreen.Run(_console, _keys, _profile);
            if (root is null) return 0;
            _profile.Save(ProfileStore.AddRecent(_profile.Load(), root));

            var changeRoot = false;
            while (!changeRoot)
            {
                var (result, folder) = RunsBrowserScreen.Run(_console, _keys, root, _now);
                switch (result)
                {
                    case BrowserResult.Quit:
                        return 0;
                    case BrowserResult.ChangeRoot:
                        changeRoot = true;
                        break;
                    case BrowserResult.OpenRun:
                        DashboardScreen.Run(_console, _keys, Path.Combine(root, folder!), actions, _now, _refreshSeconds);
                        break;
                    case BrowserResult.NewRun:
                        var created = WizardScreen.Run(_console, _keys, root, currentUser, _now);
                        if (created is not null)
                        {
                            // Default action is "new & deploy"; pressing [n] creates the run only.
                            if (ConfirmScreen.Run(_console, _keys,
                                    $"Deploy '{Path.GetFileName(created)}' to its targets now?",
                                    "Choose no to just create it — you can deploy later with [s] on the dashboard."))
                                actions.Deploy(created);
                            DashboardScreen.Run(_console, _keys, created, actions, _now, _refreshSeconds);
                        }
                        break;
                }
            }
        }
    }

    private static string CurrentUser()
    {
        var domain = Environment.UserDomainName;
        var user = Environment.UserName;
        return string.IsNullOrEmpty(domain) ? user : $"{domain}\\{user}";
    }
}
