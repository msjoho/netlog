function Merge-NetLogRecordSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Existing,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$New,
        [Parameter(Mandatory)][ValidateSet('Full','Merged')][string]$Level
    )
    Invoke-NetLogCondense -Records (@($Existing) + @($New)) -Level $Level
}
