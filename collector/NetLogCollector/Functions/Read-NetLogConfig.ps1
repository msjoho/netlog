function Read-NetLogConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Json)
    $o     = $Json | ConvertFrom-Json
    if ($null -eq $o.schedule -or $null -eq $o.schedule.stopUtc) {
        throw 'Read-NetLogConfig: schedule.stopUtc is missing or null.'
    }
    if ($o.settings.archiveLevel -notin 'Full', 'Merged') {
        throw "Read-NetLogConfig: archiveLevel must be 'Full' or 'Merged', got '$($o.settings.archiveLevel)'."
    }
    # AdjustToUniversal matters: run.json carries "+00:00"-offset instants (System.Text.Json);
    # RoundtripKind alone would return LOCAL-kind ticks, which the collector then compares
    # against a UTC clock - skewing the run window by the host's UTC offset.
    # (RoundtripKind cannot be combined with AdjustToUniversal; AssumeUniversal covers
    # offset-less strings, which in run.json are always UTC instants.)
    $style = [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor [System.Globalization.DateTimeStyles]::AssumeUniversal
    # ConvertFrom-Json in PS7 auto-converts ISO date strings to [datetime]; use ToString('o') to
    # normalise back to a round-trip string before re-parsing so the instant survives on both
    # PS 5.1 (string) and PS 7 (datetime).
    $stopRaw  = if ($o.schedule.stopUtc -is [datetime]) { $o.schedule.stopUtc.ToString('o') } else { [string]$o.schedule.stopUtc }
    $startUtc = [datetime]::MinValue
    if ($null -ne $o.schedule.PSObject.Properties['startUtc'] -and $null -ne $o.schedule.startUtc) {
        $startRaw = if ($o.schedule.startUtc -is [datetime]) { $o.schedule.startUtc.ToString('o') } else { [string]$o.schedule.startUtc }
        $startUtc = [datetime]::Parse($startRaw, $null, $style)
    }
    [pscustomobject]@{
        RunName                = $o.runName
        StartUtc               = $startUtc
        RunSequence            = if ($null -ne $o.PSObject.Properties['runSequence'] -and $null -ne $o.runSequence) { [int]$o.runSequence } else { 1 }
        StopUtc                = [datetime]::Parse($stopRaw, $null, $style)
        ArchiveLevel           = $o.settings.archiveLevel
        PollIntervalSeconds    = [int]$o.settings.pollIntervalSeconds
        ArchiveIntervalMinutes = [int]$o.settings.archiveIntervalMinutes
        CacheLifetimeDays      = if ($null -ne $o.settings.cacheLifetimeDays) { [int]$o.settings.cacheLifetimeDays } else { $null }
        MaxCacheSizeMb         = if ($null -ne $o.settings.maxCacheSizeMb)    { [int]$o.settings.maxCacheSizeMb }    else { $null }
        ResolveHostnames       = if ($null -ne $o.settings.resolveHostnames)  { [bool]$o.settings.resolveHostnames } else { $true }
        Timestamps             = if ($null -ne $o.settings.timestamps) { [bool]$o.settings.timestamps } else { $false }
    }
}
