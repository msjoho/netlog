function New-NetLogTaskDefinition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$InstallDir,
        [Parameter(Mandatory)][string]$RunPath,
        [Parameter(Mandatory)][string]$Account,
        [string]$StartUtc
    )
    $a = Get-NetLogCollectorAction -InstallDir $InstallDir -RunPath $RunPath
    $action = New-ScheduledTaskAction -Execute $a.Execute -Argument $a.Argument
    # Boot trigger keeps the collector alive across reboots; the one-time trigger fires a
    # future-scheduled run at its start instant (without it, nothing starts the task until
    # a reboot). The collector itself idles until StartUtc, so an early fire is harmless.
    $trigger = @(New-ScheduledTaskTrigger -AtStartup)
    if ($StartUtc) {
        $startLocal = [datetimeoffset]::Parse($StartUtc, [cultureinfo]::InvariantCulture).LocalDateTime
        $trigger += New-ScheduledTaskTrigger -Once -At $startLocal
    }
    $principal = New-ScheduledTaskPrincipal -UserId $Account -LogonType Password -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
        -ExecutionTimeLimit ([TimeSpan]::Zero)

    New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
}
