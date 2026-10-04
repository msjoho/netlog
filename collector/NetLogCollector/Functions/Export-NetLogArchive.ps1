function Export-NetLogArchive {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][string]$Path,
        [switch]$IncludeTimestamps
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $cols = @('Direction','Source','SourceHost','SourcePort','Target','TargetHost','TargetPort','Process',
              'Usernames','ProcessPath','SeenCount','Connections')
    if ($IncludeTimestamps) {
        $cols += 'FirstSeen','LastSeen'
    } else {
        # settings.timestamps off: schema stays 14-wide, cells stay blank.
        $cols += @{ Name = 'FirstSeen'; Expression = { '' } }, @{ Name = 'LastSeen'; Expression = { '' } }
    }
    @($Records) |
        Select-Object $cols |
        Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}
