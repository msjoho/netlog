function Export-NetLogCache {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][string]$Path
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    @($Records) |
        Select-Object Direction, Source, SourceHost, SourcePort, Target, TargetHost, TargetPort, Process, `
                      Usernames, ProcessPath, SeenCount, Connections, FirstSeen, LastSeen |
        Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}
