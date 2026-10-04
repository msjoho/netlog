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
        $cache  = Update-NetLogCache -Cache $cache -Sample $sample -SeenKeys $seen
        $polls++

        if (($now - $lastArchive).TotalMinutes -ge $config.ArchiveIntervalMinutes) {
            if ($null -ne $config.CacheLifetimeDays) {
                $cache = Remove-ExpiredCacheRecord -Records $cache -NowUtc $now -LifetimeDays $config.CacheLifetimeDays
            }
            if ($null -ne $config.MaxCacheSizeMb) {
                $cache = Limit-NetLogCacheBySize -Records $cache -MaxSizeBytes ([long]$config.MaxCacheSizeMb * 1MB)
            }
            Export-NetLogCache -Records $cache -Path $CachePath
            # Plan 3: a share outage must not kill the collector. Local cache is already written above;
            # if the archive (share) write fails, log nothing fatal and continue to the next cycle.
            try {
                $merged = Invoke-NetLogArchiveCycle -CachePath $CachePath -ArchivePath $archivePath -RunArchivePath $runArchivePath `
                                          -StatusPath $statusPath `
                                          -Level $config.ArchiveLevel -ProcessId $PID -TotalPolls $polls -NowUtc $now `
                                          -IncludeTimestamps:$config.Timestamps
                Write-NetLogLog -Path $LogPath -Message ("archive cycle: cumulative={0} cache={1} polls={2}" -f @($merged).Count, @($cache).Count, $polls)
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
    Export-NetLogCache -Records $cache -Path $CachePath
    try {
        Invoke-NetLogArchiveCycle -CachePath $CachePath -ArchivePath $archivePath -RunArchivePath $runArchivePath `
                                  -StatusPath $statusPath `
                                  -Level $config.ArchiveLevel -ProcessId $PID -TotalPolls $polls -NowUtc (& $Clock) `
                                  -IncludeTimestamps:$config.Timestamps | Out-Null
    }
    catch {
        Write-Warning "NetLog final archive flush failed: $($_.Exception.Message)"
        Write-NetLogLog -Path $LogPath -Message "WARN final archive flush failed: $($_.Exception.Message)"
    }
    $reason = if ($MaxIterations -gt 0 -and $iter -ge $MaxIterations) { 'max iterations reached' } else { 'window ended' }
    Write-NetLogLog -Path $LogPath -Message ("final flush done - exiting ({0}; polls={1})" -f $reason, $polls)
}
