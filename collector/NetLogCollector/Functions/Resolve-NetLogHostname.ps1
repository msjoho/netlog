function Resolve-NetLogHostname {
    <#
    .SYNOPSIS
      Resolves an IP to a hostname: DNS client cache first (the name the application
      actually queried, CNAMEs intact), PTR lookup as fallback. Results - including
      failures - are memoized in $Cache so each IP costs at most one lookup attempt
      until the caller prunes negative entries.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$IpAddress,
        [Parameter(Mandatory)][hashtable]$Cache,
        [scriptblock]$DnsCacheProvider = {
            param($ip)
            (Get-DnsClientCache -ErrorAction SilentlyContinue | Where-Object { $_.Data -eq $ip } | Select-Object -First 1).Entry
        },
        [scriptblock]$PtrProvider = {
            param($ip)
            [System.Net.Dns]::GetHostEntry($ip).HostName
        }
    )
    if ([string]::IsNullOrWhiteSpace($IpAddress) -or $IpAddress -in '0.0.0.0', '::') { return '' }
    if ($Cache.ContainsKey($IpAddress)) { return $Cache[$IpAddress] }

    $name = ''
    try { $name = [string](& $DnsCacheProvider $IpAddress) } catch { $name = '' }
    if (-not $name) {
        try { $name = [string](& $PtrProvider $IpAddress) } catch { $name = '' }
    }
    $Cache[$IpAddress] = $name
    $name
}
