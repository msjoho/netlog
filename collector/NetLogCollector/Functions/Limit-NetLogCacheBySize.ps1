function Limit-NetLogCacheBySize {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][long]$MaxSizeBytes,
        [int]$BytesPerRecord = 80
    )
    if ($MaxSizeBytes -le 0) { return ,@($Records) }
    $maxCount = [int][Math]::Floor($MaxSizeBytes / $BytesPerRecord)
    if ($maxCount -lt 1) { return ,@($Records) }
    if (@($Records).Count -le $maxCount) { return ,@($Records) }
    $style = [System.Globalization.DateTimeStyles]::RoundtripKind
    ,@(@($Records) |
        Sort-Object { [datetime]::Parse($_.LastSeen, $null, $style) } -Descending |
        Select-Object -First $maxCount)
}
