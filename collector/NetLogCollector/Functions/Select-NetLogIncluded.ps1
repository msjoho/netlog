# collector/NetLogCollector/Functions/Select-NetLogIncluded.ps1
function Select-NetLogIncluded {
    <# .SYNOPSIS  Records that are NOT excluded by any rule. Always returns a true array (assign it; do not @()-wrap the call). #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules
    )
    if (@($Rules).Count -eq 0) { return ,@($Records) }
    $out = [System.Collections.Generic.List[object]]::new()
    foreach ($r in @($Records)) {
        if (-not (Test-NetLogExcluded -Record $r -Rules $Rules)) { $out.Add($r) }
    }
    ,$out.ToArray()
}
