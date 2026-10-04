function Get-ListeningPortSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Connections
    )
    $set = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($c in $Connections) {
        if ($c.State -eq 'Listen') { [void]$set.Add([int]$c.LocalPort) }
    }
    ,$set
}
