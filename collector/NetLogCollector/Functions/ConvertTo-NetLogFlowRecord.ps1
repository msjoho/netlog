function ConvertTo-NetLogFlowRecord {
    <#
    .SYNOPSIS
      Upgrades legacy 6-column records (old local/remote semantics: Source = always the
      local host) to flow semantics (Source = initiator). Legacy Incoming rows swap
      Source/Target and ports; host columns are added blank. Records that already carry
      a SourceHost property pass through unchanged.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records)
    $out = [System.Collections.Generic.List[object]]::new()
    foreach ($r in $Records) {
        if ($null -ne $r.psobject.Properties['SourceHost']) { $out.Add($r); continue }
        $swap = ($r.Direction -eq 'Incoming')
        $rec = New-NetLogRecord -Direction ([string]$r.Direction) `
            -Source ([string]$(if ($swap) { $r.Target } else { $r.Source })) -SourceHost '' `
            -SourcePort ([string]$(if ($swap) { $r.TargetPort } else { $r.SourcePort })) `
            -Target ([string]$(if ($swap) { $r.Source } else { $r.Target })) -TargetHost '' `
            -TargetPort ([string]$(if ($swap) { $r.SourcePort } else { $r.TargetPort })) `
            -Process ([string]$r.Process)
        if ($null -ne $r.psobject.Properties['LastSeen']) {
            Add-Member -InputObject $rec -NotePropertyName LastSeen -NotePropertyValue $r.LastSeen -Force
        }
        $out.Add($rec)
    }
    ,$out.ToArray()
}
