function Get-NetLogPathDiagnostic {
    <#
    .SYNOPSIS
      Walks a path from the leaf up to its share/drive root, testing each level, so a failure
      pinpoints the FIRST inaccessible layer (server-share vs run folder vs the file itself).
      Pure ASCII, Windows PowerShell 5.1 compatible. Best-effort: never throws.
    .OUTPUTS
      Objects { Path; Exists } ordered root-first (deepest last).
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $results = [System.Collections.Generic.List[object]]::new()
    $current = $Path
    $seen = @{}
    while ($current -and -not $seen.ContainsKey($current)) {
        $seen[$current] = $true
        $exists = $false
        try { $exists = [bool](Test-Path -LiteralPath $current) } catch { $exists = $false }
        $results.Insert(0, [pscustomobject]@{ Path = $current; Exists = $exists })

        $parent = Split-Path -Parent $current
        if (-not $parent -or $parent -eq $current) { break }
        # Stop at the UNC share root: probing the bare \\server level adds nothing and can
        # block on the SMB/RPC timeout when the server is unreachable.
        if ($parent -match '^\\\\[^\\]+\\?$') { break }
        $current = $parent
    }
    ,$results.ToArray()
}
