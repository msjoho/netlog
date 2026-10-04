using NetLog.Cli.Commands;
using Spectre.Console.Cli;

// Legacy consoles (conhost with an OEM codepage, e.g. on servers over RDP) transcode output
// through that codepage and turn every glyph outside it into '?'. Forcing UTF-8 makes the
// theme glyphs survive; the glyph set itself is restricted to Consolas coverage (see Theme).
try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
catch (System.IO.IOException) { /* no console attached (redirected output) — irrelevant then */ }

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("netlog");
    config.AddCommand<TuiCommand>("tui").WithDescription("Launch the interactive TUI (default).");
    config.AddCommand<StatusCommand>("status")
          .WithDescription("Show state, duration, and stats for a run folder.")
          .WithExample("status", "--path", @"\\share\runs\weekly-audit");
    config.AddCommand<NewCommand>("new")
          .WithDescription("Create a run (write run.json to the run folder).")
          .WithExample("new", "--path", @"\\share\runs\audit", "--targets", "S1,S2", "--account", @"DOMAIN\svc", "--duration", "7d");
    config.AddCommand<StartCommand>("start")
          .WithDescription("Deploy the collector to each target and register the Scheduled Task.")
          .WithExample("start", "--path", @"\\share\runs\audit");
    config.AddCommand<StopCommand>("stop")
          .WithDescription("Stop and unregister the collector task on each target.")
          .WithExample("stop", "--path", @"\\share\runs\audit");
});
app.SetDefaultCommand<TuiCommand>();
return app.Run(args);
