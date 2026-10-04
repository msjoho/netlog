function Get-NetLogCollectorAction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$InstallDir,
        [Parameter(Mandatory)][string]$RunPath
    )
    $entry = Join-Path $InstallDir 'Start-NetLogCollector.ps1'
    # Use Windows PowerShell 5.1 (preinstalled on every Windows host) rather than pwsh.exe: the
    # Task Scheduler service launches the action via the *system* PATH, where a per-user/freshly
    # installed pwsh.exe often isn't resolvable (fails 0x80070002 "file not found"). powershell.exe
    # lives in System32 and is always present. The collector module is Windows PowerShell 5.1
    # compatible. %SystemRoot% is expanded by Task Scheduler on the target at launch time.
    [pscustomobject]@{
        Execute  = '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe'
        Argument = ('-NoProfile -ExecutionPolicy Bypass -File "{0}" -RunPath "{1}"' -f $entry, $RunPath)
    }
}
