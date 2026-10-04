function Resolve-ProcessIdentity {
    <#
    .SYNOPSIS
      Resolves a PID to @{ Name; User; Path }. ProcessMap values are either the new
      hashtable shape or a legacy plain name string. svchost gets its hosted service
      names appended - 'svchost (Dnscache,NlaSvc)' - sorted so the decorated name
      (part of the dedup key) is deterministic.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$OwningProcess,
        [Parameter(Mandatory)][hashtable]$ProcessMap,
        [Parameter(Mandatory)][hashtable]$ServiceMap
    )
    $name = ''; $user = ''; $path = ''
    if ($ProcessMap.ContainsKey($OwningProcess)) {
        $v = $ProcessMap[$OwningProcess]
        if ($v -is [hashtable]) {
            $name = [string]$v.Name; $user = [string]$v.User; $path = [string]$v.Path
        } else {
            $name = [string]$v
        }
    }
    if ($name -eq 'svchost' -and $ServiceMap.ContainsKey($OwningProcess)) {
        $services = @($ServiceMap[$OwningProcess]) | Where-Object { $_ } | Sort-Object
        if (@($services).Count -gt 0) {
            $name = 'svchost ({0})' -f (@($services) -join ',')
        }
    }
    @{ Name = $name; User = $user; Path = $path }
}
