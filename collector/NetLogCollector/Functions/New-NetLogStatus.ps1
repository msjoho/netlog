function New-NetLogStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][datetime]$LastRunUtc,
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][int]$TotalConnections,
        [Parameter(Mandatory)][int]$TotalPolls,
        [int]$ExcludedCount = 0
    )
    [pscustomobject]@{
        lastRunUtc       = $LastRunUtc.ToString('o')
        pid              = $ProcessId
        totalConnections = $TotalConnections
        totalPolls       = $TotalPolls
        excludedCount    = $ExcludedCount
    }
}
