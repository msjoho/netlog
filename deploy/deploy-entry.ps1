<#
.SYNOPSIS
  Entry point the netlog CLI shells to. Deploys, tears down, or removes (teardown + purge of the
  run's local state and, when no other NetLog task remains, the shared binaries) the collector on
  each target.
  The service-account password is read from $env:NETLOG_DEPLOY_PASSWORD (never passed on argv).
  Emits one JSON line per target result to stdout.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('deploy','teardown','remove')][string]$Action,
    [Parameter(Mandatory)][string]$Targets,           # comma-separated
    [Parameter(Mandatory)][string]$RunName,
    [string]$RunPath,
    [string]$Account,
    [switch]$StartNow,
    [string]$StartUtc,
    [string]$CollectorSource,
    [string]$InstallDir = 'C:\ProgramData\NetworkActivityLogger\bin'
)

Import-Module "$PSScriptRoot\NetLogDeploy\NetLogDeploy.psd1" -Force

if ($Action -eq 'deploy' -and -not $env:NETLOG_DEPLOY_PASSWORD) {
    throw 'NETLOG_DEPLOY_PASSWORD environment variable is required for the deploy action.'
}

$cred = $null
if ($Account -and $env:NETLOG_DEPLOY_PASSWORD) {
    $secure = ConvertTo-SecureString $env:NETLOG_DEPLOY_PASSWORD -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential($Account, $secure)
}
$plainPassword = $env:NETLOG_DEPLOY_PASSWORD

$targetList = $Targets -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
foreach ($t in $targetList) {
    if ($Action -eq 'deploy') {
        $r = Invoke-NetLogHostDeploy -Target $t -RunPath $RunPath -RunName $RunName -Account $Account `
            -Password $plainPassword -CollectorSource $CollectorSource -InstallDir $InstallDir `
            -StartNow:$StartNow -StartUtc $StartUtc -Credential $cred
    } elseif ($Action -eq 'remove') {
        $r = Invoke-NetLogHostTeardown -Target $t -RunName $RunName -Credential $cred -PurgeLocalState -InstallDir $InstallDir
    } else {
        $r = Invoke-NetLogHostTeardown -Target $t -RunName $RunName -Credential $cred
    }
    $r | ConvertTo-Json -Compress
}
