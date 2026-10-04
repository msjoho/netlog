function Invoke-NetLogHostTeardown {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$RunName,
        $Credential,
        [hashtable]$Operations = (Get-NetLogDefaultOperations)
    )
    $taskName = Get-NetLogTaskName -RunName $RunName
    $session = $null
    try {
        $session = & $Operations.NewSession $Target $Credential
        & $Operations.StopTask $session $taskName
        & $Operations.UnregisterTask $session $taskName
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
