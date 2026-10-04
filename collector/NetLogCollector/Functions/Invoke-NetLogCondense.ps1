function Invoke-NetLogCondense {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][ValidateSet('Full','Merged')][string]$Level
    )
    # Max of two numeric strings; blank counts as absent, both blank stays blank.
    # Idempotent (with union/min/max below) because each archive cycle re-condenses
    # already-condensed archive rows together with current cache rows - a sum would
    # double-count on every cycle.
    $maxNum = {
        param($a, $b)
        $x = 0L; $y = 0L
        $ha = [long]::TryParse([string]$a, [ref]$x)
        $hb = [long]::TryParse([string]$b, [ref]$y)
        if (-not $ha -and -not $hb) { '' }
        elseif (-not $hb) { [string]$x }
        elseif (-not $ha) { [string]$y }
        else { [string][System.Math]::Max($x, $y) }
    }
    $byKey = @{}
    $ports = @{}
    $out   = [System.Collections.Generic.List[object]]::new()
    foreach ($r in $Records) {
        # Flow semantics: the initiator's port (SourcePort) is the ephemeral one for BOTH
        # directions, so Merged uniformly drops it.
        $sourcePort = if ($Level -eq 'Merged') { '' } else { [string]$r.SourcePort }
        $connections = if ($Level -eq 'Merged') { [string]$r.Connections } else { '' }
        $rec = New-NetLogRecord -Direction $r.Direction `
                                -Source $r.Source -SourceHost ([string]$r.SourceHost) -SourcePort $sourcePort `
                                -Target $r.Target -TargetHost ([string]$r.TargetHost) -TargetPort ([string]$r.TargetPort) `
                                -Process ([string]$r.Process) `
                                -Usernames ([string]$r.Usernames) -ProcessPath ([string]$r.ProcessPath) `
                                -SeenCount ([string]$r.SeenCount) -Connections $connections `
                                -FirstSeen ([string]$r.FirstSeen) -LastSeen ([string]$r.LastSeen)
        $k = ConvertTo-NetLogRecordKey -Record $rec
        if (-not $byKey.ContainsKey($k)) {
            $byKey[$k] = $rec
            $out.Add($rec)
            if ($Level -eq 'Merged') { $ports[$k] = [System.Collections.Generic.HashSet[string]]::new() }
        }
        else {
            $kept = $byKey[$k]
            # First record wins, but blank host columns are enrichment: backfill them from a
            # later duplicate so a name resolved after the first archive write still lands.
            foreach ($col in 'SourceHost', 'TargetHost') {
                if (-not [string]$kept.$col -and [string]$rec.$col) { $kept.$col = [string]$rec.$col }
            }
            $kept.Usernames   = Merge-NetLogValueList -A $kept.Usernames -B $rec.Usernames
            $kept.ProcessPath = Merge-NetLogValueList -A $kept.ProcessPath -B $rec.ProcessPath
            $kept.SeenCount   = & $maxNum $kept.SeenCount $rec.SeenCount
            if ($Level -eq 'Merged') { $kept.Connections = & $maxNum $kept.Connections $rec.Connections }
            if ([string]$rec.FirstSeen -and (-not [string]$kept.FirstSeen -or $rec.FirstSeen -lt $kept.FirstSeen)) {
                $kept.FirstSeen = [string]$rec.FirstSeen
            }
            if ([string]$rec.LastSeen -and (-not [string]$kept.LastSeen -or $rec.LastSeen -gt $kept.LastSeen)) {
                $kept.LastSeen = [string]$rec.LastSeen
            }
        }
        # Distinct initiator ports seen among Full-fidelity contributors (cache rows still
        # carry their SourcePort; already-condensed archive rows contribute blank).
        if ($Level -eq 'Merged' -and [string]$r.SourcePort) {
            [void]$ports[$k].Add([string]$r.SourcePort)
        }
    }
    if ($Level -eq 'Merged') {
        foreach ($k in $ports.Keys) {
            $cnt = $ports[$k].Count
            if ($cnt -gt 0) {
                $byKey[$k].Connections = & $maxNum $byKey[$k].Connections ([string]$cnt)
            }
        }
    }
    ,$out.ToArray()
}
