function Update-NetLogCache {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Cache,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Sample,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[string]]$SeenKeys
    )
    # NOTE: $SeenKeys is mutated in place - newly-seen tuple keys are added to it.
    # Callers share this set across polling cycles (see Start-NetLogCollector).
    $list  = [System.Collections.Generic.List[object]]::new()
    $byKey = @{}
    foreach ($c in $Cache) {
        $list.Add($c)
        $byKey[(ConvertTo-NetLogRecordKey -Record $c)] = $c
    }
    foreach ($s in $Sample) {
        $k = ConvertTo-NetLogRecordKey -Record $s
        if ($SeenKeys.Add($k)) {
            $list.Add($s)
            $byKey[$k] = $s
        }
        elseif ($byKey.ContainsKey($k)) {
            # Hostname backfill: a record first seen unresolved gets its name once
            # resolution succeeds. First non-blank value wins; never overwritten.
            $existing = $byKey[$k]
            foreach ($col in 'SourceHost', 'TargetHost') {
                # Property-existence guard: records from a not-yet-upgraded legacy cache
                # lack the host columns; skip rather than throw.
                if ($null -ne $existing.psobject.Properties[$col] -and
                    -not [string]$existing.$col -and [string]$s.$col) { $existing.$col = [string]$s.$col }
            }
            # Identity bookkeeping. Add-Member -Force both adds (legacy records from a
            # pre-identity cache) and overwrites (normal path).
            $n = 0L
            [void][long]::TryParse([string]$existing.SeenCount, [ref]$n)
            Add-Member -InputObject $existing -NotePropertyName SeenCount -NotePropertyValue ([string]($n + 1)) -Force
            Add-Member -InputObject $existing -NotePropertyName LastSeen -NotePropertyValue ([string]$s.LastSeen) -Force
            Add-Member -InputObject $existing -NotePropertyName Usernames `
                -NotePropertyValue (Merge-NetLogValueList -A ([string]$existing.Usernames) -B ([string]$s.Usernames)) -Force
            Add-Member -InputObject $existing -NotePropertyName ProcessPath `
                -NotePropertyValue (Merge-NetLogValueList -A ([string]$existing.ProcessPath) -B ([string]$s.ProcessPath)) -Force
            if (-not [string]$existing.psobject.Properties['FirstSeen'].Value) {
                Add-Member -InputObject $existing -NotePropertyName FirstSeen -NotePropertyValue ([string]$s.FirstSeen) -Force
            }
        }
    }
    ,$list.ToArray()
}
