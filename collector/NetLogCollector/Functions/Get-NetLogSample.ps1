function Get-NetLogSample {
    [CmdletBinding()]
    param(
        [scriptblock]$ConnectionProvider = { Get-NetTCPConnection -ErrorAction Stop },
        [scriptblock]$ProcessMapProvider = {
            $m = @{}
            $procs = @()
            # -IncludeUserName needs elevation; fall back silently (Start-NetLogCollector
            # logs the elevation state once at startup).
            try { $procs = @(Get-Process -IncludeUserName -ErrorAction Stop) }
            catch { $procs = @(Get-Process -ErrorAction SilentlyContinue) }
            foreach ($p in $procs) {
                $m[$p.Id] = @{
                    Name = [string]$p.ProcessName
                    User = [string]$p.PSObject.Properties['UserName'].Value
                    Path = [string]$p.Path
                }
            }
            $m
        },
        [scriptblock]$ServiceMapProvider = {
            $m = @{}
            foreach ($s in @(Get-CimInstance -ClassName Win32_Service -Filter "State='Running'" -ErrorAction SilentlyContinue)) {
                $svcPid = [int]$s.ProcessId
                if ($svcPid -le 0) { continue }
                if ($m.ContainsKey($svcPid)) { $m[$svcPid] = @($m[$svcPid]) + @([string]$s.Name) }
                else { $m[$svcPid] = @([string]$s.Name) }
            }
            $m
        },
        [string]$HostName = $env:COMPUTERNAME,
        [scriptblock]$HostnameResolver = { param($ip) '' },
        [datetime]$NowUtc = ((Get-Date).ToUniversalTime())
    )
    $conns  = @(& $ConnectionProvider)
    $listen = Get-ListeningPortSet -Connections $conns
    $pmap   = & $ProcessMapProvider
    $smap   = & $ServiceMapProvider
    $stamp  = $NowUtc.ToString('o')
    $out    = [System.Collections.Generic.List[object]]::new()
    foreach ($c in $conns) {
        if ($c.State -ne 'Established') { continue }
        $dir        = Resolve-ConnectionDirection -LocalPort ([int]$c.LocalPort) -ListeningPorts $listen
        $identity   = Resolve-ProcessIdentity -OwningProcess ([int]$c.OwningProcess) -ProcessMap $pmap -ServiceMap $smap
        $remoteHost = [string](& $HostnameResolver ([string]$c.RemoteAddress))
        $common = @{
            Direction = $dir;            Process   = $identity.Name
            Usernames = $identity.User;  ProcessPath = $identity.Path
            SeenCount = '1';             Connections = ''
            FirstSeen = $stamp;          LastSeen    = $stamp
        }
        # Flow semantics: Source = initiator, Target = acceptor.
        if ($dir -eq 'Outgoing') {
            $rec = New-NetLogRecord @common `
                -Source ([string]$c.LocalAddress)  -SourceHost $HostName   -SourcePort ([string]$c.LocalPort) `
                -Target ([string]$c.RemoteAddress) -TargetHost $remoteHost -TargetPort ([string]$c.RemotePort)
        } else {
            $rec = New-NetLogRecord @common `
                -Source ([string]$c.RemoteAddress) -SourceHost $remoteHost -SourcePort ([string]$c.RemotePort) `
                -Target ([string]$c.LocalAddress)  -TargetHost $HostName   -TargetPort ([string]$c.LocalPort)
        }
        $out.Add($rec)
    }
    ,$out.ToArray()
}
