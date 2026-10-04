function Invoke-NetLogHostDeploy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$RunPath,
        [Parameter(Mandatory)][string]$RunName,
        [Parameter(Mandatory)][string]$Account,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Password,
        [Parameter(Mandatory)][string]$CollectorSource,
        [Parameter(Mandatory)][string]$InstallDir,
        [switch]$StartNow,
        [string]$StartUtc,
        $Credential,
        [hashtable]$Operations = (Get-NetLogDefaultOperations)
    )

    Test-NetLogDeployInput -Target $Target -RunPath $RunPath -Account $Account
    $taskName = Get-NetLogTaskName -RunName $RunName
    $definition = New-NetLogTaskDefinition -InstallDir $InstallDir -RunPath $RunPath -Account $Account -StartUtc $StartUtc

    $session = $null
    try {
        $session = & $Operations.NewSession $Target $Credential
        & $Operations.EnsureDir $session $InstallDir
        & $Operations.CopyCollector $session $CollectorSource $InstallDir
        & $Operations.RegisterTask $session $taskName $definition $Account $Password
        if ($StartNow) { & $Operations.StartTask $session $taskName }
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
