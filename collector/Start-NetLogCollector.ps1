<#
.SYNOPSIS
  Scheduled-Task entry point. Imports the module and starts the collector loop.
  Fails LOUD: any error (unreadable run path, missing module, bad config) is written to a
  local log file and the process exits 1, so Task Scheduler shows a failure and applies its
  restart policy - instead of silently completing after two seconds.
.PARAMETER RunPath
  Network share run folder (contains run.json).
.PARAMETER HostName
  This host's name (used for the hosts\<HostName> subfolder).
.PARAMETER CachePath
  Local cache file path (default: %ProgramData%\NetworkActivityLogger\<run>\cache-run<N>.csv).
.PARAMETER LogPath
  Local log file (default: %ProgramData%\NetworkActivityLogger\<run>\collector-run<N>.log;
  falls back to %ProgramData%\NetworkActivityLogger\collector-error.log when the run config
  cannot even be read).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunPath,
    [string]$HostName = $env:COMPUTERNAME,
    [string]$CachePath,
    [string]$LogPath
)

# Without this, a failed Get-Content/Import-Module only aborts its own statement; the script
# then falls through to the end and powershell.exe reports SUCCESS (exit 0) after ~2s.
$ErrorActionPreference = 'Stop'

try {
    Import-Module "$PSScriptRoot\NetLogCollector\NetLogCollector.psd1" -Force

    $cfg = Read-NetLogConfig -Json (Get-Content -LiteralPath (Join-Path $RunPath 'run.json') -Raw)
    $localDir = Join-Path $env:ProgramData (Join-Path 'NetworkActivityLogger' $cfg.RunName)
    if (-not $CachePath) {
        # Per-run cache file: a new run window starts with an empty local buffer, so its
        # run<N>.csv contains only that run's connections (the cumulative archive already
        # holds everything older).
        $CachePath = Join-Path $localDir ('cache-run{0}.csv' -f $cfg.RunSequence)
    }
    if (-not $LogPath) {
        $LogPath = Join-Path $localDir ('collector-run{0}.log' -f $cfg.RunSequence)
    }

    Write-NetLogLog -Path $LogPath -Message "entry: launching collector (runPath=$RunPath host=$HostName user=$env:USERDOMAIN\$env:USERNAME interactive=$([System.Environment]::UserInteractive))"
    Start-NetLogCollector -RunPath $RunPath -HostName $HostName -CachePath $CachePath -LogPath $LogPath
    Write-NetLogLog -Path $LogPath -Message 'entry: collector exited normally'
}
catch {
    # Log the security context: a UNC that "does not exist" only under the task while the
    # same user works interactively usually means a logon/auth difference (batch vs
    # interactive, cross-domain Kerberos, Protected Users), not a truly missing file.
    $msg = "entry: FATAL $($_.Exception.Message) (runPath=$RunPath host=$HostName user=$env:USERDOMAIN\$env:USERNAME interactive=$([System.Environment]::UserInteractive))"

    # When run.json was unreadable we never computed the per-run log path; fall back to a
    # findable global error log. Ensure the module is loaded so the diagnostic helpers exist.
    $failLog = if ($LogPath) { $LogPath } else { Join-Path $env:ProgramData 'NetworkActivityLogger\collector-error.log' }
    try { Import-Module "$PSScriptRoot\NetLogCollector\NetLogCollector.psd1" -Force -ErrorAction SilentlyContinue } catch { }
    try { Write-NetLogLog -Path $failLog -Message $msg } catch { }

    # Best-effort diagnostics so the next run pinpoints WHICH layer is inaccessible and HOW
    # the session authenticates. None of this may take the failure path down.
    try {
        $rj = Join-Path $RunPath 'run.json'
        foreach ($d in (Get-NetLogPathDiagnostic -Path $rj)) {
            Write-NetLogLog -Path $failLog -Message ("  probe exists={0}  {1}" -f $d.Exists, $d.Path)
        }
        Write-NetLogLog -Path $failLog -Message ("  probe [IO.File]::Exists={0}  {1}" -f [System.IO.File]::Exists($rj), $rj)
    } catch { }
    try {
        $groups = (& whoami /groups /fo csv 2>$null | Select-String -SimpleMatch 'Protected Users')
        Write-NetLogLog -Path $failLog -Message ("  identity whoami={0} protectedUser={1}" -f (& whoami 2>$null), [bool]$groups)
    } catch { }
    try {
        $tgt = (& klist 2>$null | Select-String -SimpleMatch 'krbtgt')
        $svc = (& klist 2>$null | Select-String -SimpleMatch 'cifs/')
        Write-NetLogLog -Path $failLog -Message ("  kerberos hasTGT={0} hasFileServerTicket={1}" -f [bool]$tgt, [bool]$svc)
    } catch { }

    Write-Error $msg
    exit 1
}
