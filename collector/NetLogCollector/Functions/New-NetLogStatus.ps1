function New-NetLogStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][datetime]$LastRunUtc,
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][int]$TotalConnections,
        [Parameter(Mandatory)][int]$TotalPolls
    )
    [pscustomobject]@{
        lastRunUtc       = $LastRunUtc.ToString('o')
        pid              = $ProcessId
        totalConnections = $TotalConnections
        totalPolls       = $TotalPolls
    }
}
