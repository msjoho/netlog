function Remove-ExpiredCacheRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][datetime]$NowUtc,
        [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$LifetimeDays
    )
    $cutoff = $NowUtc.AddDays(-$LifetimeDays)
    $style  = [System.Globalization.DateTimeStyles]::RoundtripKind
    ,@(@($Records) | Where-Object {
        ([datetime]::Parse($_.LastSeen, $null, $style)) -ge $cutoff
    })
}
