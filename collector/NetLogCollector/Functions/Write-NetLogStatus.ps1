function Write-NetLogStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Status,
        [Parameter(Mandatory)][string]$Path
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $json = $Status | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}
