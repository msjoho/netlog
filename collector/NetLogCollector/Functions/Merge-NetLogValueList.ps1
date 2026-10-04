function Merge-NetLogValueList {
    <#
    .SYNOPSIS
      Unions two semicolon-joined value lists into one sorted, case-insensitively
      deduplicated semicolon list. Blank inputs contribute nothing.
    #>
    [CmdletBinding()]
    param(
        [AllowEmptyString()][string]$A = '',
        [AllowEmptyString()][string]$B = ''
    )
    $set = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($v in (@($A -split ';') + @($B -split ';'))) {
        if ($v) { [void]$set.Add($v) }
    }
    ($set -join ';')
}
