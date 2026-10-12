function Select-NetLogValidExclusion {
    <# .SYNOPSIS  Valid rules only; logs one WARN per skipped rule and one summary line. Called only when the rule set changes. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules,
        [Parameter(Mandatory)][AllowEmptyString()][string]$LogPath
    )
    $valid = [System.Collections.Generic.List[object]]::new()
    foreach ($r in @($Rules)) {
        $err = Get-NetLogExclusionError -Rule $r
        if ($err) {
            $port = ''
            if ($null -ne $r.PSObject.Properties['Port'] -and $r.Port) { $port = ':' + [string]$r.Port }
            Write-NetLogLog -Path $LogPath -Message ("WARN exclusion rule skipped ({0}:{1}@{2}{3}): {4}" -f $r.Kind, $r.Pattern, $r.Direction, $port, $err)
        } else { $valid.Add($r) }
    }
    Write-NetLogLog -Path $LogPath -Message ("exclusions: {0} rules" -f $valid.Count)
    ,$valid.ToArray()
}

function ConvertTo-NetLogExclusionKey {
    <# .SYNOPSIS  A stable string for change detection of the live rule set. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules)
    $parts = foreach ($r in @($Rules)) {
        $port = ''
        if ($null -ne $r.PSObject.Properties['Port']) { $port = [string]$r.Port }
        '{0}:{1}@{2}:{3}' -f $r.Kind, $r.Pattern, $r.Direction, $port
    }
    (@($parts) -join "`n")
}

function Remove-NetLogExcludedCacheRecord {
    <# .SYNOPSIS  Retroactive purge of the local cache. Returns the kept records as a true array and
        removes each purged record's key from $SeenKeys in place, so a later rule removal lets the
        connection be recorded again (first-seen-wins would otherwise block it). #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowNull()][AllowEmptyCollection()][object[]]$Cache,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[string]]$SeenKeys
    )
    $kept = [System.Collections.Generic.List[object]]::new()
    foreach ($c in @($Cache)) {
        if ($null -eq $c) { continue }
        if (Test-NetLogExcluded -Record $c -Rules $Rules) { [void]$SeenKeys.Remove((ConvertTo-NetLogRecordKey -Record $c)) }
        else { $kept.Add($c) }
    }
    ,$kept.ToArray()
}

function Start-NetLogCollector {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RunPath,
        [Parameter(Mandatory)][string]$HostName,
        [Parameter(Mandatory)][string]$CachePath,
        [scriptblock]$ConnectionProvider = { Get-NetTCPConnection -ErrorAction Stop },
        [scriptblock]$ProcessMapProvider = {
            $m = @{}
            $procs = @()
            # -IncludeUserName needs elevation; fall back silently (Start-NetLogCollector
            # logs the elevation state once at startup).
            try { $procs = @(Get-Process -IncludeUserName -ErrorAction Stop) }
            catch { $procs = @(Get-Process -ErrorAction SilentlyContinue) }
            foreach ($p in $procs) {
                $m[$p.Id] = @{
                    Name = [string]$p.ProcessName
                    User = [string]$p.PSObject.Properties['UserName'].Value
                    Path = [string]$p.Path
                }
            }
            $m
        },
        [scriptblock]$ServiceMapProvider = {
            $m = @{}
            foreach ($s in @(Get-CimInstance -ClassName Win32_Service -Filter "State='Running'" -ErrorAction SilentlyContinue)) {
                $svcPid = [int]$s.ProcessId
                if ($svcPid -le 0) { continue }
                if ($m.ContainsKey($svcPid)) { $m[$svcPid] = @($m[$svcPid]) + @([string]$s.Name) }
                else { $m[$svcPid] = @([string]$s.Name) }
            }
            $m
        },
        [scriptblock]$Clock = { (Get-Date).ToUniversalTime() },
        [scriptblock]$DnsCacheProvider,
        [scriptblock]$PtrProvider,
        [int]$MaxIterations = 0,
        [string]$LogPath = ''
    )

    $config      = Read-NetLogConfig -Json (Get-Content -LiteralPath (Join-Path $RunPath 'run.json') -Raw)

    $resolverCache = @{}
    if ($config.ResolveHostnames) {
        $resolveArgs = @{ Cache = $resolverCache }
        if ($DnsCacheProvider) { $resolveArgs.DnsCacheProvider = $DnsCacheProvider }
        if ($PtrProvider)      { $resolveArgs.PtrProvider      = $PtrProvider }
        $resolver = { param($ip) Resolve-NetLogHostname -IpAddress $ip @resolveArgs }.GetNewClosure()
    } else {
        $resolver = { param($ip) '' }
    }
    $archivePath    = Join-Path $RunPath (Join-Path 'hosts' (Join-Path $HostName 'connections.csv'))
    $runArchivePath = Join-Path $RunPath (Join-Path 'hosts' (Join-Path $HostName ("run{0}.csv" -f $config.RunSequence)))
    $statusPath     = Join-Path $RunPath (Join-Path 'hosts' (Join-Path $HostName '_status.json'))

    Write-NetLogLog -Path $LogPath -Message ("collector starting: run={0} seq={1} host={2} window={3:o}..{4:o} poll={5}s archive={6}m level={7} resolve={8} cache={9}" -f `
        $config.RunName, $config.RunSequence, $HostName, $config.StartUtc, $config.StopUtc, `
        $config.PollIntervalSeconds, $config.ArchiveIntervalMinutes, $config.ArchiveLevel, $config.ResolveHostnames, $CachePath)

    $userNamesAvailable = $true
    try { Get-Process -Id $PID -IncludeUserName -ErrorAction Stop | Out-Null }
    catch { $userNamesAvailable = $false }
    if (-not $userNamesAvailable) {
        Write-NetLogLog -Path $LogPath -Message 'WARN Get-Process -IncludeUserName unavailable (task not elevated?) - Usernames will be blank'
    }
    Write-NetLogLog -Path $LogPath -Message ("identity enrichment: usernames={0} timestamps={1}" -f $userNamesAvailable, $config.Timestamps)

    $exclusionsKey = ConvertTo-NetLogExclusionKey -Rules $config.Exclusions
    # Assign directly: the helper returns a true array via ,$arr. Wrapping the call in @( ) nests
    # it one level deeper, and a nested rule array matches nothing (its .Kind enumerates to
    # 'process host ...') - except by accident with exactly one rule.
    $exclusions    = Select-NetLogValidExclusion -Rules $config.Exclusions -LogPath $LogPath
    $excluded      = 0

    $cache       = Import-NetLogCache -Path $CachePath
    # NOTE: $seen grows for the life of the run and is never pruned (first-seen-wins; see plan deferral note).
    # On very high-churn hosts this can accumulate keys; revisit only if memory becomes a problem.
    $seen        = Get-SeenKeySet -Records $cache
    $polls       = 0
    $lastArchive = [datetime]::SpecifyKind([datetime]::MinValue, [System.DateTimeKind]::Utc)
    $iter        = 0
    $waitLogged  = $false

    while ($true) {
        $now = & $Clock
        # Live config: re-read the mutable fields each cycle so TUI edits (end, poll interval,
        # archive interval, cache lifetime) take effect during the run. Identity/immutable fields
        # (name, sequence, paths, archive level, start) keep their startup values. A transient read
        # failure (e.g. a share blip) keeps the last good values so config I/O never kills the loop.
        try {
            $live = Read-NetLogConfig -Json (Get-Content -LiteralPath (Join-Path $RunPath 'run.json') -Raw)
            $config.StopUtc                = $live.StopUtc
            $config.PollIntervalSeconds    = $live.PollIntervalSeconds
            $config.ArchiveIntervalMinutes = $live.ArchiveIntervalMinutes
            $config.CacheLifetimeDays      = $live.CacheLifetimeDays
            $config.MaxCacheSizeMb         = $live.MaxCacheSizeMb
            $liveKey = ConvertTo-NetLogExclusionKey -Rules $live.Exclusions
            if ($liveKey -ne $exclusionsKey) {
                $exclusions    = Select-NetLogValidExclusion -Rules $live.Exclusions -LogPath $LogPath   # no @( ): see startup note
                $exclusionsKey = $liveKey
                # Rebuild like a restart: keys of records already expired from the cache may have been purged from the archive.
                $seen          = Get-SeenKeySet -Records $cache
            }
        }
        catch {
            Write-NetLogLog -Path $LogPath -Message ("WARN live config re-read failed (keeping current): {0}" -f $_.Exception.Message)
        }
        if ($now -ge $config.StopUtc) { break }
        if ($now -lt $config.StartUtc) {
            # Window not open yet (e.g. the boot trigger fired before a future start):
            # idle without sampling or archiving.
            if (-not $waitLogged) {
                Write-NetLogLog -Path $LogPath -Message ("window opens at {0:o} - idling" -f $config.StartUtc)
                $waitLogged = $true
            }
            $iter++
            if ($MaxIterations -gt 0 -and $iter -ge $MaxIterations) { break }
            if ($config.PollIntervalSeconds -gt 0) { Start-Sleep -Seconds $config.PollIntervalSeconds }
            continue
        }

        $sample = Get-NetLogSample -ConnectionProvider $ConnectionProvider -ProcessMapProvider $ProcessMapProvider `
                                   -ServiceMapProvider $ServiceMapProvider `
                                   -HostName $HostName -HostnameResolver $resolver -NowUtc $now
        if (@($exclusions).Count -gt 0) {
            # Excluded rows never enter the cache or the seen-set - they are simply not observed.
            $kept = [System.Collections.Generic.List[object]]::new()
            foreach ($s in @($sample)) {
                if (Test-NetLogExcluded -Record $s -Rules $exclusions) { $excluded++ } else { $kept.Add($s) }
            }
            $sample = $kept.ToArray()
        }
        $cache  = Update-NetLogCache -Cache $cache -Sample $sample -SeenKeys $seen
        $polls++

        if (($now - $lastArchive).TotalMinutes -ge $config.ArchiveIntervalMinutes) {
            if ($null -ne $config.CacheLifetimeDays) {
                $cache = Remove-ExpiredCacheRecord -Records $cache -NowUtc $now -LifetimeDays $config.CacheLifetimeDays
            }
            if ($null -ne $config.MaxCacheSizeMb) {
                $cache = Limit-NetLogCacheBySize -Records $cache -MaxSizeBytes ([long]$config.MaxCacheSizeMb * 1MB)
            }
            if (@($exclusions).Count -gt 0) {
                $cache = Remove-NetLogExcludedCacheRecord -Cache $cache -Rules $exclusions -SeenKeys $seen
            }
            Export-NetLogCache -Records $cache -Path $CachePath
            # Plan 3: a share outage must not kill the collector. Local cache is already written above;
            # if the archive (share) write fails, log nothing fatal and continue to the next cycle.
            try {
                $merged = Invoke-NetLogArchiveCycle -CachePath $CachePath -ArchivePath $archivePath -RunArchivePath $runArchivePath `
                                          -StatusPath $statusPath `
                                          -Level $config.ArchiveLevel -ProcessId $PID -TotalPolls $polls -NowUtc $now `
                                          -IncludeTimestamps:$config.Timestamps `
                                          -Exclusions $exclusions -ExcludedCount $excluded
                Write-NetLogLog -Path $LogPath -Message ("archive cycle: cumulative={0} cache={1} polls={2} excluded={3}" -f @($merged).Count, @($cache).Count, $polls, $excluded)
            }
            catch {
                Write-Warning "NetLog archive cycle failed (will retry next interval): $($_.Exception.Message)"
                Write-NetLogLog -Path $LogPath -Message "WARN archive cycle failed (will retry next interval): $($_.Exception.Message)"
            }
            $lastArchive = $now
            # Negative-cache retry: drop failed resolutions once per archive cycle so the
            # next polls re-attempt them; successful names then backfill blank host columns.
            foreach ($k in @($resolverCache.Keys)) {
                if ($resolverCache[$k] -eq '') { $resolverCache.Remove($k) }
            }
        }

        $iter++
        if ($MaxIterations -gt 0 -and $iter -ge $MaxIterations) { break }
        if ($config.PollIntervalSeconds -gt 0) { Start-Sleep -Seconds $config.PollIntervalSeconds }
    }

    # Final flush so the archive + status reflect the last buffer state.
    if (@($exclusions).Count -gt 0) {
        $cache = Remove-NetLogExcludedCacheRecord -Cache $cache -Rules $exclusions -SeenKeys $seen
    }
    Export-NetLogCache -Records $cache -Path $CachePath
    try {
        Invoke-NetLogArchiveCycle -CachePath $CachePath -ArchivePath $archivePath -RunArchivePath $runArchivePath `
                                  -StatusPath $statusPath `
                                  -Level $config.ArchiveLevel -ProcessId $PID -TotalPolls $polls -NowUtc (& $Clock) `
                                  -IncludeTimestamps:$config.Timestamps `
                                  -Exclusions $exclusions -ExcludedCount $excluded | Out-Null
    }
    catch {
        Write-Warning "NetLog final archive flush failed: $($_.Exception.Message)"
        Write-NetLogLog -Path $LogPath -Message "WARN final archive flush failed: $($_.Exception.Message)"
    }
    $reason = if ($MaxIterations -gt 0 -and $iter -ge $MaxIterations) { 'max iterations reached' } else { 'window ended' }
    Write-NetLogLog -Path $LogPath -Message ("final flush done - exiting ({0}; polls={1})" -f $reason, $polls)
}
