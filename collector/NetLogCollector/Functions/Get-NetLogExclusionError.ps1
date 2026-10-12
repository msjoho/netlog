# collector/NetLogCollector/Functions/Get-NetLogExclusionError.ps1
function Get-NetLogExclusionError {
    <# .SYNOPSIS  '' when the rule is usable, otherwise a one-line reason (mirrors ExclusionRuleParser.Validate in C#). #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Rule)
    $kind = [string]$Rule.Kind; $pattern = [string]$Rule.Pattern; $dir = [string]$Rule.Direction
    if ($kind -notin 'process', 'user', 'host') { return "unknown kind '$kind' (use process, user or host)" }
    if (-not $pattern) { return 'pattern is empty' }
    if ($pattern.IndexOfAny([char[]]@('[', ']')) -ge 0) { return "pattern '$pattern' must not contain [ or ]" }
    if ($dir -notin 'in', 'out', 'both') { return "unknown direction '$dir' (use in, out or both)" }
    if ($kind -eq 'host' -and $pattern.Contains('/')) {
        $parts = $pattern -split '/', 2
        $net = $null; $prefix = 0
        $ok = [System.Net.IPAddress]::TryParse($parts[0], [ref]$net) -and [int]::TryParse($parts[1], [ref]$prefix)
        if ($ok -and $net.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and @($parts[0] -split '\.').Count -ne 4) { $ok = $false }
        if ($ok) { $max = 32; if ($net.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) { $max = 128 }; if ($prefix -lt 0 -or $prefix -gt $max) { $ok = $false } }
        if (-not $ok) { return "'$pattern' is not a valid CIDR" }
    }
    $port = ''
    if ($null -ne $Rule.PSObject.Properties['Port']) { $port = [string]$Rule.Port }
    if ($port) {
        foreach ($item in ($port -split ',')) {
            $bounds = $item -split '-', 2
            $low = 0; $high = 0
            # NumberStyles.None = digits only (no sign / whitespace), the same strictness as the C# validator.
            $none = [System.Globalization.NumberStyles]::None; $inv = [System.Globalization.CultureInfo]::InvariantCulture
            $ok = [int]::TryParse($bounds[0], $none, $inv, [ref]$low) -and $low -ge 1 -and $low -le 65535
            if ($ok -and $bounds.Count -eq 2) { $ok = [int]::TryParse($bounds[1], $none, $inv, [ref]$high) -and $high -ge $low -and $high -le 65535 }
            if (-not $ok) { return "port '$port' is not valid (use 1-65535 as a single port, a comma list or a low-high range)" }
        }
    }
    ''
}
