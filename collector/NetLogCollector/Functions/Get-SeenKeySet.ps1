function Get-SeenKeySet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records
    )
    $set = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($r in $Records) { [void]$set.Add((ConvertTo-NetLogRecordKey -Record $r)) }
    ,$set
}
