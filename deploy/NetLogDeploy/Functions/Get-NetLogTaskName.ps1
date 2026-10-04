function Get-NetLogTaskName {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RunName)
    $safe = ($RunName -replace '[\\/:*?"<>|]', '_')
    "NetLogCollector_$safe"
}
