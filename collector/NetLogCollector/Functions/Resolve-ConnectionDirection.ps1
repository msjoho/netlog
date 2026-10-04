function Resolve-ConnectionDirection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$LocalPort,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[int]]$ListeningPorts
    )
    if ($ListeningPorts.Contains($LocalPort)) { 'Incoming' } else { 'Outgoing' }
}
