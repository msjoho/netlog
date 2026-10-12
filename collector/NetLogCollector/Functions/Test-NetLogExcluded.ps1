# collector/NetLogCollector/Functions/Test-NetLogExcluded.ps1
function Test-NetLogExcluded {
    <#
    .SYNOPSIS
      True when any rule matches the record (OR). Rules are the Read-NetLogConfig shape
      (@{Kind; Pattern; Direction}) and are assumed valid (see Get-NetLogExclusionError);
      an unknown kind or a PowerShell wildcard parse problem is treated as "no match".
      Semantics (spec): -like (case-insensitive, * and ?, anchored);
        process: decorated name OR base name with the ' (…)' suffix stripped
        user:    any entry of the ';'-joined Usernames cell
        host:    the PEER by direction (Outgoing → Target*, Incoming → Source*):
                 hostname, then IP text, then CIDR when the pattern contains '/'
        direction 'in'/'out' gates the rule; 'both' always applies.
        port (optional, any kind): gates on the SERVICE port = the record's TargetPort (the remote
                 port of an outgoing connection, the local port of an incoming one); a comma list of
                 ports / low-high ranges; '' or a missing member = any port.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Record,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules
    )
    $recDir = [string]$Record.Direction
    foreach ($rule in @($Rules)) {
        if ($null -eq $rule) { continue }
        $dir = [string]$rule.Direction
        if ($dir -eq 'in'  -and $recDir -ne 'Incoming') { continue }
        if ($dir -eq 'out' -and $recDir -ne 'Outgoing') { continue }
        $pattern = [string]$rule.Pattern
        if (-not $pattern) { continue }
        $portSpec = ''
        if ($null -ne $rule.PSObject.Properties['Port']) { $portSpec = [string]$rule.Port }
        if ($portSpec -and -not (Test-NetLogPortSpecMatch -Port ([string]$Record.TargetPort) -Spec $portSpec)) { continue }
        try {
            switch ([string]$rule.Kind) {
                'process' {
                    $name = [string]$Record.Process
                    $base = $name -replace '\s*\(.*\)\s*$', ''
                    if (($name -like $pattern) -or ($base -like $pattern)) { return $true }
                }
                'user' {
                    foreach ($u in (([string]$Record.Usernames) -split ';')) {
                        if ($u -and ($u -like $pattern)) { return $true }
                    }
                }
                'host' {
                    if ($recDir -eq 'Outgoing') { $peerHost = [string]$Record.TargetHost; $peerIp = [string]$Record.Target }
                    else                        { $peerHost = [string]$Record.SourceHost; $peerIp = [string]$Record.Source }
                    if ($peerHost -and ($peerHost -like $pattern)) { return $true }
                    if ($peerIp -and ($peerIp -like $pattern)) { return $true }
                    if ($pattern.Contains('/') -and (Test-NetLogCidrMatch -IpAddress $peerIp -Cidr $pattern)) { return $true }
                }
            }
        }
        catch { }   # a malformed wildcard must never take the collector down
    }
    $false
}

function Test-NetLogPortSpecMatch {
    <# .SYNOPSIS  True when the numeric port is covered by the spec ("443", "80,443", "1024-65535").
       A non-numeric port or a malformed item never matches (rules are validated up front anyway). #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Port,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Spec
    )
    $none = [System.Globalization.NumberStyles]::None; $inv = [System.Globalization.CultureInfo]::InvariantCulture
    $p = 0
    if (-not [int]::TryParse($Port, $none, $inv, [ref]$p)) { return $false }
    foreach ($item in ($Spec -split ',')) {
        $bounds = $item -split '-', 2
        $low = 0; $high = 0
        if (-not [int]::TryParse($bounds[0], $none, $inv, [ref]$low)) { continue }
        if ($bounds.Count -eq 2) { if (-not [int]::TryParse($bounds[1], $none, $inv, [ref]$high)) { continue } } else { $high = $low }
        if ($p -ge $low -and $p -le $high) { return $true }
    }
    $false
}
