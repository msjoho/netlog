# collector/NetLogCollector/Functions/Test-NetLogCidrMatch.ps1
function Test-NetLogCidrMatch {
    <#
    .SYNOPSIS
      True when IpAddress lies inside the Cidr prefix (IPv4 or IPv6, prefix-length byte compare).
      Any parse problem or address-family mismatch is simply "no match" — the matcher must never throw.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$IpAddress,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Cidr
    )
    if (-not $IpAddress -or -not $Cidr) { return $false }
    $parts = $Cidr -split '/', 2
    if (@($parts).Count -ne 2) { return $false }
    $net = $null; $ip = $null; $prefix = 0
    if (-not [System.Net.IPAddress]::TryParse($parts[0], [ref]$net)) { return $false }
    # Zone ids (fe80::1%12) are accepted by TryParse; strip anyway so the bytes are the plain address.
    $plain = ($IpAddress -split '%', 2)[0]
    if (-not [System.Net.IPAddress]::TryParse($plain, [ref]$ip)) { return $false }
    if (-not [int]::TryParse($parts[1], [ref]$prefix)) { return $false }
    if ($net.AddressFamily -ne $ip.AddressFamily) { return $false }
    $nb = $net.GetAddressBytes(); $ib = $ip.GetAddressBytes()
    if ($prefix -lt 0 -or $prefix -gt ($nb.Length * 8)) { return $false }
    $full = [int][math]::Floor($prefix / 8)
    for ($i = 0; $i -lt $full; $i++) { if ($nb[$i] -ne $ib[$i]) { return $false } }
    $rem = $prefix % 8
    if ($rem -gt 0) {
        $mask = (0xFF -shl (8 - $rem)) -band 0xFF
        if (($nb[$full] -band $mask) -ne ($ib[$full] -band $mask)) { return $false }
    }
    $true
}
