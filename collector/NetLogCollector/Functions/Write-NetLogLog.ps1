function Write-NetLogLog {
    <#
    .SYNOPSIS
      Appends a UTC-timestamped line to the collector's local log file. Logging must never
      take the collector down: an empty path is a no-op and write failures are swallowed.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Path,
        [Parameter(Mandatory)][string]$Message
    )
    if (-not $Path) { return }
    try {
        $dir = Split-Path -Parent $Path
        # -ErrorAction Stop makes provider errors (bad drive, denied) terminating so they hit
        # the catch and are swallowed silently instead of leaking a non-terminating error record.
        if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force -ErrorAction Stop | Out-Null }
        $line = '{0:o}  {1}' -f (Get-Date).ToUniversalTime(), $Message
        Add-Content -LiteralPath $Path -Value $line -Encoding UTF8 -ErrorAction Stop
    }
    catch { }
}
