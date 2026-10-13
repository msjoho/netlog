function Get-NetLogDefaultOperations {
    [CmdletBinding()]
    param()
    @{
        NewSession    = { param($Target, $Credential)
            if ($Credential) { New-PSSession -ComputerName $Target -Credential $Credential -ErrorAction Stop }
            else             { New-PSSession -ComputerName $Target -ErrorAction Stop } }
        EnsureDir     = { param($Session, $Dir)
            Invoke-Command -Session $Session -ScriptBlock { param($d) New-Item -ItemType Directory -Path $d -Force | Out-Null } -ArgumentList $Dir }
        CopyCollector = { param($Session, $Source, $Dest)
            Copy-Item -Path (Join-Path $Source '*') -Destination $Dest -ToSession $Session -Recurse -Force }
        RegisterTask  = { param($Session, $TaskName, $Definition, $Account, $Password)
            Invoke-Command -Session $Session -ScriptBlock {
                param($n, $d, $a, $p)
                Register-ScheduledTask -TaskName $n -InputObject $d -User $a -Password $p -Force | Out-Null
            } -ArgumentList $TaskName, $Definition, $Account, $Password }
        StartTask     = { param($Session, $TaskName)
            Invoke-Command -Session $Session -ScriptBlock { param($n) Start-ScheduledTask -TaskName $n } -ArgumentList $TaskName }
        StopTask      = { param($Session, $TaskName)
            Invoke-Command -Session $Session -ScriptBlock { param($n) Stop-ScheduledTask -TaskName $n -ErrorAction SilentlyContinue } -ArgumentList $TaskName }
        UnregisterTask= { param($Session, $TaskName)
            Invoke-Command -Session $Session -ScriptBlock { param($n) Unregister-ScheduledTask -TaskName $n -Confirm:$false -ErrorAction SilentlyContinue } -ArgumentList $TaskName }
        # Purge operations (delete run): the run's local cache/log folder, then the shared binaries
        # only when no other NetLogCollector_* task is left on the host.
        RemoveLocalRunDir = { param($Session, $RunName)
            Invoke-Command -Session $Session -ScriptBlock {
                param($n)
                $dir = Join-Path $env:ProgramData (Join-Path 'NetworkActivityLogger' $n)
                if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop }
            } -ArgumentList $RunName }
        OtherNetLogTasksExist = { param($Session, $TaskName)
            [bool](Invoke-Command -Session $Session -ScriptBlock {
                param($n)
                @(Get-ScheduledTask -TaskName 'NetLogCollector_*' -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -ne $n }).Count -gt 0
            } -ArgumentList $TaskName) }
        RemoveInstallDir = { param($Session, $InstallDir)
            Invoke-Command -Session $Session -ScriptBlock {
                param($d)
                if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction Stop }
            } -ArgumentList $InstallDir }
        RemoveSession = { param($Session) Remove-PSSession -Session $Session -ErrorAction SilentlyContinue }
    }
}
