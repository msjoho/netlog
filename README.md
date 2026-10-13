# Network Activity Logger

A simple, efficient, solid TUI/CLI tool that remotely sets up recurring **network-activity logging** on Windows hosts. Each host samples its TCP connections, deduplicates and condenses them, and archives a compact per-connection record to a shared run folder. Logging is driven by a Scheduled Task on each host (survives reboots); the controlling app need not stay running. Reopen it on the same run path to see state, duration, and stats.

Windows-first; Linux (Ubuntu/CentOS/RHEL) is prepared-for and deferred to a future version.

## What gets logged

One row per unique connection, fourteen fields:

`Direction` (Incoming/Outgoing) · `Source`/`SourceHost`/`SourcePort` (the initiator) · `Target`/`TargetHost`/`TargetPort` (the acceptor) · `Process` (name)

- **Flow semantics:** a row reads like a firewall rule — `allow Source → Target:TargetPort`. Outgoing: Source is the logging host. Incoming: Source is the remote initiator. (Direction is Incoming when the local port is a listening port.)
- **Hostnames:** the remote peer is resolved on each collector (local DNS cache first — the name the app actually used — then PTR), cached per IP, blank when unresolvable; failed lookups are retried once per archive cycle and backfilled. Full URLs are out of scope (application-layer/TLS). Disable with `--no-resolve-hostnames`.
- **Archive level `Full`:** dedup only. **`Merged`:** also drops the initiator's ephemeral `SourcePort` and collapses.
- **Identity:** `Usernames` and `ProcessPath` are semicolon-separated lists, merged across every sighting of the same connection. `Process` is decorated as `svchost (ServiceName)` when the process is a shared `svchost.exe` host hosting a resolvable service.
- **Persistence counters:** `SeenCount` (poll-sample count; at Merged: max of contributing connections) and `Connections` (distinct initiator source ports; Merged level only) use idempotent max-merges, never accumulated.
- **Timestamps:** `FirstSeen`/`LastSeen` are recorded by default. Create the run with `--no-timestamps`, or switch the wizard's **Timestamps** field off, for a time-free archive.
- **Top users:** when `Usernames` data is present, the dashboard/`netlog status` frame adds a TOP USERS panel alongside TOP TARGETS/PROCESSES.
- **Exclusions:** rules on the process name, the owning username, or the remote peer's host (target for outgoing, source for incoming) drop matching connections on the collector — they never reach the cache, the share, or the dashboard. See *Exclusions* below.
- Old 6- and 8-column runs are upgraded on read (Incoming rows swap ends; hostnames blank; identity/timestamp fields absent).

## Architecture (built in four phases)

| Component | Tech | Role |
|---|---|---|
| `collector/` | PowerShell (`NetLogCollector`) | Per-host loop: poll `Get-NetTCPConnection` → classify → dedup → condense → **cumulative** archive to the share + heartbeat; prunes local cache; survives share outages. |
| `src/NetLog.Core` | C# / .NET 9 | Readers (CSV/JSON, BOM/0-byte tolerant), run-state derivation, stats, report builder, run.json writer, profile store. |
| `src/NetLog.Cli` | C# + Spectre.Console | `netlog` CLI + interactive TUI. |
| `deploy/` | PowerShell (`NetLogDeploy`) | WinRM deploy: copy collector + register the Scheduled Task as a service account. Driven by the CLI. |

The network share **is** the source of truth. `run.json` holds config; each host writes `hosts/<host>/connections.csv` + `_status.json`. State/duration/stats are derived from those.

**Multiple runs in one folder:** "Start another run" (TUI) or re-running `netlog new` on the same path starts a new collection window. `connections.csv` stays the cumulative union of all runs; each window additionally gets its own per-host `run<N>.csv` (`run1.csv`, `run2.csv`, …). A pre-existing first run is snapshotted to `run1.csv` automatically when the second one starts.

## Commands

```
netlog                      # launch the interactive TUI (default)
netlog new    --path \\share\runs\audit --targets S1,S2 --account DOMAIN\svc --duration 7d
netlog start  --path \\share\runs\audit      # deploy + register the task on each target
netlog status --path \\share\runs\audit      # dashboard: state, duration, stats
netlog stop   --path \\share\runs\audit      # stop + unregister the task on each target
```

`netlog new` options: `--start now|<iso>`, `--stop <iso>` or `--duration 7d/12h/90m`, `--archive-level Full|Merged`, `--poll <s>`, `--archive-interval <m>`, `--cache-days <d>` or `--cache-mb <mb>`, `--no-resolve-hostnames`, `--no-timestamps`, `--exclude <rule>` (repeatable), `--no-root-exclusions`.

**Credentials:** `netlog start` prompts for the service-account password (masked) and passes it to the deployment only via the child process environment — never on a command line. The Scheduled Task stores it in Windows' protected store on each host.

## Interactive TUI

`netlog` (no args) opens a self-refreshing, single-screen terminal UI driven by single-key hotkeys:

1. **Root select** — pick the folder that holds your runs (e.g. `\\tdfs\InfraOps\Sources\netlog`); recent roots are remembered. `r` adds a new one.
2. **Runs browser** — every subfolder with a `run.json` is listed as a run (folder name = run name; full paths are never shown) with its window-derived state and host count. `enter` opens one, `c` creates a new one, `d` deletes one, `x` edits exclusions (the root template), `r` changes root.
3. **Live dashboard** — re-reads the run folder every few seconds (state, elapsed, heartbeats, top-10 stats; each top target/process is tagged `in`/`out`/`io`; per-host `excl` column). Arrow through hosts and press `enter` to **focus** one — the stats panels narrow to that host; `enter` on another host switches focus directly, `enter` on the focused host (or `a`/`Esc`) unfocuses. `s` deploy · `x` destroy · `n` new window · `e` edit · `r` refresh · `q` back. A share outage keeps the last good frame and flags it stale instead of crashing.

**Editing a running run** (`e`) — the end (duration/date), poll interval, archive interval and cache lifetime can be changed while a run is live; immutable fields (name, targets, account, archive level, start) are shown greyed. The collector re-reads `run.json` each cycle, so edits take effect within one archive cycle (collectors deployed before this change need one redeploy to pick up the re-read).

**Deleting a run** (`d` on the runs browser) — after a confirmation that spells out that **all collected data** in the run folder goes too, it destroys the collector on every target (stops and unregisters the task, removes the run's local cache/log under `%ProgramData%\NetworkActivityLogger\<run>`, and removes the shared collector binaries only when no other NetLog task remains on that host), then deletes the run folder on the share. If a host fails (unreachable, access denied), the results are shown and you are asked once more whether to delete the share folder anyway. The dashboard's `x` destroy is unchanged: it removes the task but keeps `run.json`, the local cache and the data for a later re-deploy.

**Merging a Full run** (`m`, shown only for `Full` runs) — condenses each host's `connections.csv` to Merged level in place and moves the original Full file to `<run>/_archive/<timestamp>/`, so the raw data is kept. Offered only once collection has stopped (it's blocked while a collector is still live, since the collector would overwrite the merged file). The run stays recorded as `Full`.

The whole flow stays in the themed TUI — the new-window schedule, the masked password, and confirmations are all in-app (no console prompts), and the screen patches in place without flicker. All timestamps render in local time with the offset shown.

The new-run wizard takes a run *name* (the run is created under the selected root); fields edit with a movable caret (arrows/Backspace/Delete, also in the root-path, new-window and password fields) and toggle **Start** (`now` ⇄ date), **End** (duration ⇄ date), and **Timestamps** (`on` ⇄ `off`) with `Tab`. Creating a run **deploys by default** (confirm; choose no to create it without deploying). `[s] deploy` registers the Scheduled Task (honouring the start time); `[x] destroy` removes the task on the targets while keeping `run.json` and the collected data on the share. The dashboard view is shared with `netlog status`, which prints the same frame once (non-interactively).

## Exclusions

Noise you never want logged — monitoring agents, backup/AV traffic, admin accounts, the collector's own SMB writes — is declared as **rules**:

| Kind | Matches | Example |
|---|---|---|
| `process` | the process name, or the base name of a decorated `svchost (Service)` | `process:svchost (Dnscache)` · `process:Teams*` |
| `user` | any of the owning usernames (`DOMAIN\name`) | `user:CORP\svc-scom*` |
| `host` | the **remote peer**: hostname, IP, or CIDR | `host:nas01*` · `host:10.20.0.0/16@out` · `host:fe80::/10` · `host:go.microsoft.com@out:443` |

Patterns are case-insensitive, anchored, with `*` and `?` wildcards (`nas01` does not match `nas01.corp.local`; `nas01*` does). A trailing `@in` / `@out` limits a rule to one direction. A **port** after the direction, `@out:445`, `@in:3389`, `@both:443` or just `@445` (either direction), limits a rule of any kind to the **service port**: the remote port of an outgoing connection, the local port of an incoming one. A port spec is a single port, a comma list or a low-high range: `443`, `80,443`, `1024-65535`. So `process:System@in:139,445` hides SMB server traffic without hiding everything the System process does. (A pattern that itself ends in `@digits`, say `user:svc@2024`, would read as a port; append `@both` to keep it literal.) A row is dropped when **any** rule matches.

**Where rules live.** `<root>\exclusions.json` is the team's template (`{ "rules": [ { "kind": "host", "pattern": "nas01*", "direction": "out", "port": "445", "note": "backup" } ] }` — `port` and `note` are optional). Each new run **copies** it into `run.json` → `settings.exclusions`; that copy is the only list the collector reads, so a run documents exactly what it excluded and later template edits never touch it. Re-running `netlog new` (or *Start another run*) over an existing run folder takes a fresh snapshot of the root template; because `connections.csv` is cumulative across windows, rows from earlier windows that match the new rules are purged from it on the first archive cycle (each window's `run<N>.csv` is left as it was).

- **TUI:** the new-run wizard shows `Exclusions   N rules (from root)` — `tab` opens the editor (`a` add a rule in the `kind:pattern[@in|@out][:port]` form, `d` remove, `enter` done, `esc` discard); changes there affect the new run only. `x` on the runs browser edits the root template. `e` on the dashboard → *Exclusions* → `tab` edits a **live** run.
- **CLI:** `netlog new --exclude "process:Teams*" --exclude host:10.0.0.0/8@out --exclude process:System@in:445` (repeatable); the root template is inherited unless `--no-root-exclusions`.

**Live edits purge.** The collector re-reads `run.json` every cycle: new samples are filtered within one poll, and because each archive cycle rewrites `connections.csv` / `run<N>.csv`, rows matching a newly added rule are **removed from the share** (and the local cache) within one archive cycle. The edit screen warns about this.

**Seeing it work.** The dashboard / `netlog status` summary shows `Exclusions   3 rules · 1,204 dropped` and each host row an `excl` count (`n/a` for hosts whose collector predates this feature). `dropped` counts sample rows (one per poll per excluded connection) since the collector last started, so it grows with poll frequency and resets when a host reboots. The collector log prints `exclusions: N rules` at start (and whenever the list changes), `excluded=<n>` per archive cycle, and `WARN exclusion rule skipped` once per malformed rule — a bad rule never stops collection. A corrupt `exclusions.json` is shown as an error wherever it is loaded, never silently ignored.

## Install (use `netlog` anywhere)

```powershell
pwsh -File .\install.ps1
```

Publishes the CLI to `%LOCALAPPDATA%\Programs\NetLog` (with the collector + deploy assets beside the exe, where the CLI expects them) and adds that folder to your user PATH — open a new terminal once, then just run `netlog`. Re-run the script after pulling changes to update. Building from source needs the .NET SDK **and NuGet access** — on locked-down machines use the offline package instead.

### Offline install (no .NET, no internet on the target machine)

On a dev machine with SDK + NuGet access:

```powershell
pwsh -File .\package.ps1     # -> dist\NetLog\ and dist\NetLog.zip (self-contained win-x64)
```

Copy `dist\NetLog` (or the zip, extracted) to the target machine and run `.\install.ps1` inside it — the installer detects the prebuilt `netlog.exe` and just copies it into place. The target machine only needs PowerShell 7 (which `netlog` deployments require anyway).

## Build, test, run

```powershell
dotnet build network-activity-logger.sln
dotnet test  network-activity-logger.sln                                   # C# (Core + Cli)
pwsh -File .\run-pester.ps1                                                 # collector + deploy (Pester 5)
dotnet run --project src/NetLog.Cli                                        # the TUI (without installing)
```

> **Run the PowerShell tests from PowerShell 7 (`pwsh`), not Windows PowerShell 5.1.** They use
> Pester 5 syntax; WinPS 5.1 auto-loads its built-in Pester 3.4.0 and fails with *"The BeforeAll
> command may only be used inside a Describe block."* `run-pester.ps1` forces Pester 5 for you.

## Live-deployment prerequisites (host-verified)

The unit/integration suites cover all logic; the actual WinRM deployment must be verified on a real domain (see `docs/superpowers/plans/2026-06-09-plan3-remoting-deployment.md` → *Host-verification checklist*). In short:

1. The **CLI machine** (where you run `netlog`) needs **PowerShell 7 (`pwsh`)** and must allow first-party scripts: `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser` (we deliberately do not bypass the policy for you).
2. **Targets** need only WinRM enabled (`Enable-PSRemoting` / GPO); **no pwsh install is required** — the Scheduled Task runs the collector under the preinstalled **Windows PowerShell 5.1** (`%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`). The service account must have write access to the run share and the "Log on as a batch job" right.
3. **First thing to test:** the Scheduled-Task `CimInstance` surviving the WinRM round-trip — if registration fails on a target, rebuild the task on the remote side from primitives (documented in the checklist).

## Troubleshooting a scheduled run

Each target writes a local log (the share is not involved in logging):

- `C:\ProgramData\NetworkActivityLogger\<run>\collector-run<N>.log` — a normal run: startup config, the run window, each archive cycle with record counts, share-write warnings, and the exit reason.
- `C:\ProgramData\NetworkActivityLogger\collector-error.log` — used when `run.json` could not even be read (the collector never got far enough to know the per-run log path).

If a task "completes" in ~1–2s with **Last Run Result `0x80070001`** (decimal `2147942401`), that is the collector failing **on purpose** (exit 1) — the entry script is fail-loud so a problem shows as a failure instead of a misleading success. Read the log for the `FATAL` line, which also records the security context and layered probes:

| Log line | Meaning |
|---|---|
| `probe exists=False  \\server\share$` | The task's logon session can't even reach the **share** — a logon/auth problem (batch vs interactive, cross-domain Kerberos), not a missing file. The classic tell: the *same account works interactively* (`runas`) but the scheduled task does not. |
| `exists=True` down to the folder, `False` on `run.json` | The share is reachable but the file/folder genuinely isn't there, or a write-permission gap (the account needs **Modify** on NTFS *and* the share to create `hosts\`). |
| `protectedUser=True` | The run-as account is in the **Protected Users** group — batch-logon network auth is restricted. Use a different service account (or a **gMSA**), or remove it from the group. |
| `hasTGT=False` / `hasFileServerTicket=False` | The task session has no Kerberos ticket for the file server; cross-domain share access will fail. Check the trust / `Restrict NTLM` policy. |

After fixing the environment, just re-run the task (or `netlog start`) — no redeploy needed unless you changed the collector.

## Design & plans

Full design and per-phase implementation plans live in `docs/superpowers/`:
`specs/2026-06-04-network-activity-logger-design.md`, and `plans/` Plan 1 (collector) → Plan 4 (TUI).
