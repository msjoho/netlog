function New-NetLogRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Incoming','Outgoing')][string]$Direction,
        [Parameter(Mandatory)][string]$Source,
        [AllowEmptyString()][string]$SourceHost = '',
        [Parameter(Mandatory)][AllowEmptyString()][string]$SourcePort,
        [Parameter(Mandatory)][string]$Target,
        [AllowEmptyString()][string]$TargetHost = '',
        [Parameter(Mandatory)][AllowEmptyString()][string]$TargetPort,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Process,
        [AllowEmptyString()][string]$Usernames = '',
        [AllowEmptyString()][string]$ProcessPath = '',
        [AllowEmptyString()][string]$SeenCount = '',
        [AllowEmptyString()][string]$Connections = '',
        [AllowEmptyString()][string]$FirstSeen = '',
        [AllowEmptyString()][string]$LastSeen = ''
    )
    [pscustomobject][ordered]@{
        Direction   = $Direction
        Source      = $Source
        SourceHost  = $SourceHost
        SourcePort  = $SourcePort
        Target      = $Target
        TargetHost  = $TargetHost
        TargetPort  = $TargetPort
        Process     = $Process
        Usernames   = $Usernames
        ProcessPath = $ProcessPath
        SeenCount   = $SeenCount
        Connections = $Connections
        FirstSeen   = $FirstSeen
        LastSeen    = $LastSeen
    }
}
