function Import-NetLogCache {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return ,@() }
    # ConvertTo-NetLogFlowRecord already returns a comma-wrapped array; wrapping it again
    # would nest the array one level deeper.
    ConvertTo-NetLogFlowRecord -Records @(Import-Csv -LiteralPath $Path)
}
