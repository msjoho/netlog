function Test-NetLogDeployInput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Target,
        [Parameter(Mandatory)][AllowEmptyString()][string]$RunPath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Account
    )
    if ([string]::IsNullOrWhiteSpace($Target))  { throw 'Target host name is required.' }
    if ([string]::IsNullOrWhiteSpace($RunPath)) { throw 'RunPath is required.' }
    if ([string]::IsNullOrWhiteSpace($Account)) { throw 'Service account is required.' }
}
