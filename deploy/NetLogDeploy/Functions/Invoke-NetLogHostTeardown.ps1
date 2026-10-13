function Invoke-NetLogHostTeardown {
    <#
    .SYNOPSIS
      Stops and unregisters the run's collector task on one host. With -PurgeLocalState it also
      removes the run's local folder (%ProgramData%\NetworkActivityLogger\<run>: cache + log) and,
      when no other NetLogCollector_* task remains on the host, the shared collector binaries.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$RunName,
        $Credential,
        [switch]$PurgeLocalState,
        [string]$InstallDir = 'C:\ProgramData\NetworkActivityLogger\bin',
        [hashtable]$Operations = (Get-NetLogDefaultOperations)
    )
    $taskName = Get-NetLogTaskName -RunName $RunName
    $session = $null
    try {
        $session = & $Operations.NewSession $Target $Credential
        & $Operations.StopTask $session $taskName
        & $Operations.UnregisterTask $session $taskName
        if ($PurgeLocalState) {
            & $Operations.RemoveLocalRunDir $session $RunName
            # Our own task is already gone, so any NetLogCollector_* task left belongs to another run.
            if (-not (& $Operations.OtherNetLogTasksExist $session $taskName)) {
                & $Operations.RemoveInstallDir $session $InstallDir
            }
        }
        [pscustomobject]@{ Target = $Target; Status = 'Success'; Error = $null }
    }
    catch {
        [pscustomobject]@{ Target = $Target; Status = 'Failed'; Error = $_.Exception.Message }
    }
    finally {
        if ($null -ne $session) {
            try { & $Operations.RemoveSession $session } catch { }
        }
    }
}
