#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }
# Pester 5 syntax (top-level BeforeAll). Run under PowerShell 7 (pwsh), not Windows PowerShell 5.1,
# whose built-in Pester 3.4.0 rejects a top-level BeforeAll.

BeforeAll {
    Import-Module "$PSScriptRoot\..\NetLogCollector\NetLogCollector.psd1" -Force
}

Describe 'Module loads' {
    It 'imports without error' {
        Get-Module NetLogCollector | Should -Not -BeNullOrEmpty
    }
}

Describe 'Resolve-ConnectionDirection' {
    It 'returns Incoming when local port is a listening port' {
        $listen = [System.Collections.Generic.HashSet[int]]::new()
        [void]$listen.Add(445)
        Resolve-ConnectionDirection -LocalPort 445 -ListeningPorts $listen | Should -Be 'Incoming'
    }
    It 'returns Outgoing when local port is not a listening port' {
        $listen = [System.Collections.Generic.HashSet[int]]::new()
        [void]$listen.Add(445)
        Resolve-ConnectionDirection -LocalPort 51001 -ListeningPorts $listen | Should -Be 'Outgoing'
    }
    It 'returns Outgoing when the listening set is empty' {
        $empty = [System.Collections.Generic.HashSet[int]]::new()
        Resolve-ConnectionDirection -LocalPort 445 -ListeningPorts $empty | Should -Be 'Outgoing'
    }
}

Describe 'Get-ListeningPortSet' {
    It 'collects only ports of Listen-state connections' {
        $conns = @(
            [pscustomobject]@{ State='Listen';      LocalPort=445 }
            [pscustomobject]@{ State='Listen';      LocalPort=3389 }
            [pscustomobject]@{ State='Established';  LocalPort=51001 }
        )
        $set = Get-ListeningPortSet -Connections $conns
        $set.Contains(445)   | Should -BeTrue
        $set.Contains(3389)  | Should -BeTrue
        $set.Contains(51001) | Should -BeFalse
    }
    It 'returns an empty set for no connections' {
        $set = Get-ListeningPortSet -Connections @()
        $set.Count | Should -Be 0
    }
    It 'deduplicates a port that appears in multiple Listen connections' {
        $conns = @(
            [pscustomobject]@{ State='Listen'; LocalPort=445 }
            [pscustomobject]@{ State='Listen'; LocalPort=445 }
        )
        (Get-ListeningPortSet -Connections $conns).Count | Should -Be 1
    }
}

Describe 'Resolve-ProcessIdentity' {
    It 'maps a known PID to name, user, and path' {
        $map = @{ 1234 = @{ Name = 'chrome'; User = 'DOMAIN\alice'; Path = 'C:\Program Files\chrome.exe' } }
        $id = Resolve-ProcessIdentity -OwningProcess 1234 -ProcessMap $map -ServiceMap @{}
        $id.Name | Should -Be 'chrome'
        $id.User | Should -Be 'DOMAIN\alice'
        $id.Path | Should -Be 'C:\Program Files\chrome.exe'
    }
    It 'returns blanks for an unknown PID' {
        $id = Resolve-ProcessIdentity -OwningProcess 9999 -ProcessMap @{} -ServiceMap @{}
        $id.Name | Should -Be ''
        $id.User | Should -Be ''
        $id.Path | Should -Be ''
    }
    It 'tolerates the old string-valued process map' {
        $id = Resolve-ProcessIdentity -OwningProcess 4 -ProcessMap @{ 4 = 'System' } -ServiceMap @{}
        $id.Name | Should -Be 'System'
        $id.User | Should -Be ''
    }
    It 'decorates svchost with its sorted service names' {
        $map = @{ 900 = @{ Name = 'svchost'; User = 'NT AUTHORITY\NETWORK SERVICE'; Path = '' } }
        $svc = @{ 900 = @('NlaSvc', 'Dnscache') }
        (Resolve-ProcessIdentity -OwningProcess 900 -ProcessMap $map -ServiceMap $svc).Name |
            Should -Be 'svchost (Dnscache,NlaSvc)'
    }
    It 'leaves svchost undecorated when the PID has no known services' {
        $map = @{ 901 = @{ Name = 'svchost'; User = ''; Path = '' } }
        (Resolve-ProcessIdentity -OwningProcess 901 -ProcessMap $map -ServiceMap @{}).Name | Should -Be 'svchost'
    }
    It 'never decorates a non-svchost process' {
        $map = @{ 800 = @{ Name = 'chrome'; User = ''; Path = '' } }
        (Resolve-ProcessIdentity -OwningProcess 800 -ProcessMap $map -ServiceMap @{ 800 = @('Foo') }).Name | Should -Be 'chrome'
    }
}

Describe 'New-NetLogRecord' {
    It 'builds a record with properties in fixed order' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourcePort '51001' `
                              -Target '10.0.0.5' -TargetPort '443' -Process 'chrome'
        $r.Direction  | Should -Be 'Outgoing'
        $r.Source     | Should -Be '10.0.0.1'
        $r.SourcePort | Should -Be '51001'
        $r.Target     | Should -Be '10.0.0.5'
        $r.TargetPort | Should -Be '443'
        $r.Process    | Should -Be 'chrome'
        ($r.PSObject.Properties.Name) -join ',' |
            Should -Be 'Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process,Usernames,ProcessPath,SeenCount,Connections,FirstSeen,LastSeen'
    }
    It 'allows a blank port (for the Merged level)' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourcePort '' `
                              -Target '10.0.0.5' -TargetPort '443' -Process 'chrome'
        $r.SourcePort | Should -Be ''
    }
    It 'defaults the identity columns to blank' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '1' -Target 'R' -TargetPort '443' -Process 'app'
        $r.Usernames   | Should -Be ''
        $r.ProcessPath | Should -Be ''
        $r.SeenCount   | Should -Be ''
        $r.Connections | Should -Be ''
        $r.FirstSeen   | Should -Be ''
        $r.LastSeen    | Should -Be ''
    }
    It 'carries identity values when given' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '1' -Target 'R' -TargetPort '443' -Process 'app' `
                              -Usernames 'DOMAIN\a' -ProcessPath 'C:\x\app.exe' -SeenCount '3' -Connections '2' `
                              -FirstSeen '2026-08-06T00:00:00.0000000Z' -LastSeen '2026-08-06T01:00:00.0000000Z'
        $r.Usernames   | Should -Be 'DOMAIN\a'
        $r.SeenCount   | Should -Be '3'
        $r.Connections | Should -Be '2'
    }
    It 'keeps identity columns out of the record key' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '1' -Target 'R' -TargetPort '443' -Process 'app' `
                              -Usernames 'DOMAIN\a' -SeenCount '3'
        ConvertTo-NetLogRecordKey -Record $r | Should -Be 'Outgoing|H|1|R|443|app'
    }
}

Describe 'ConvertTo-NetLogRecordKey' {
    It 'builds a pipe-delimited key from the six fields' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourcePort '51001' `
                              -Target '10.0.0.5' -TargetPort '443' -Process 'chrome'
        ConvertTo-NetLogRecordKey -Record $r | Should -Be 'Outgoing|10.0.0.1|51001|10.0.0.5|443|chrome'
    }
    It 'ignores a LastSeen property if present' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourcePort '51001' `
                              -Target '10.0.0.5' -TargetPort '443' -Process 'chrome'
        Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue '2026-06-04T00:00:00.0000000Z' -Force
        ConvertTo-NetLogRecordKey -Record $r | Should -Be 'Outgoing|10.0.0.1|51001|10.0.0.5|443|chrome'
    }
}

Describe 'Invoke-NetLogCondense' {
    BeforeAll {
        $script:out1 = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        $script:out2 = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51002' -Target 'R' -TargetPort '443' -Process 'app'
        # Flow semantics: the Incoming initiator (C) is in Source with its ephemeral port;
        # the local service (H:445) is the Target.
        $script:in1  = New-NetLogRecord -Direction 'Incoming' -Source 'C' -SourcePort '60001' -Target 'H' -TargetPort '445' -Process 'svc'
        $script:in2  = New-NetLogRecord -Direction 'Incoming' -Source 'C' -SourcePort '60002' -Target 'H' -TargetPort '445' -Process 'svc'
    }
    It 'Full level dedups identical tuples only' {
        $res = Invoke-NetLogCondense -Records @($out1, $out1, $out2) -Level 'Full'
        $res.Count | Should -Be 2
    }
    It 'Merged level drops SourcePort for Outgoing and collapses' {
        $res = Invoke-NetLogCondense -Records @($out1, $out2) -Level 'Merged'
        $res.Count | Should -Be 1
        $res[0].SourcePort | Should -Be ''
        $res[0].TargetPort | Should -Be '443'
    }
    It 'Merged level drops SourcePort for Incoming too (flow semantics: initiator port is ephemeral)' {
        # Flow semantics: $in1/$in2 model the initiator in Source — same service port in
        # TargetPort, differing ephemeral SourcePorts — so Merged collapses them.
        $res = Invoke-NetLogCondense -Records @($in1, $in2) -Level 'Merged'
        $res.Count | Should -Be 1
        $res[0].SourcePort | Should -Be ''
        $res[0].TargetPort | Should -Be $in1.TargetPort
    }
    It 'returns empty for empty input' {
        (Invoke-NetLogCondense -Records @() -Level 'Full').Count | Should -Be 0
    }
}

Describe 'Merge-NetLogRecordSet' {
    It 'unions existing and new records, deduping' {
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '' -Target 'R' -TargetPort '443' -Process 'app'
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '' -Target 'R' -TargetPort '80'  -Process 'app'
        $res = Merge-NetLogRecordSet -Existing @($a) -New @($a, $b) -Level 'Merged'
        $res.Count | Should -Be 2
    }
    It 'handles empty existing set' {
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '' -Target 'R' -TargetPort '80' -Process 'app'
        (Merge-NetLogRecordSet -Existing @() -New @($b) -Level 'Merged').Count | Should -Be 1
    }
    It 'is idempotent: merging the result with itself yields the same count' {
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '' -Target 'R' -TargetPort '443' -Process 'app'
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '' -Target 'R' -TargetPort '80'  -Process 'app'
        $r  = Merge-NetLogRecordSet -Existing @($a) -New @($b) -Level 'Merged'
        $r2 = Merge-NetLogRecordSet -Existing $r -New $r -Level 'Merged'
        $r2.Count | Should -Be $r.Count
    }
}

Describe 'Get-SeenKeySet' {
    It 'builds a set of full keys from records' {
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51002' -Target 'R' -TargetPort '443' -Process 'app'
        $set = Get-SeenKeySet -Records @($a, $b)
        $set.Count | Should -Be 2
        $set.Contains('Outgoing|H|51001|R|443|app') | Should -BeTrue
    }
    It 'returns empty set for empty input' {
        (Get-SeenKeySet -Records @()).Count | Should -Be 0
    }
}

Describe 'CSV IO' {
    It 'round-trips the cache including LastSeen' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue '2026-06-04T00:00:00.0000000Z' -Force
        $p = Join-Path $TestDrive 'cache.csv'
        Export-NetLogCache -Records @($r) -Path $p
        $back = Import-NetLogCache -Path $p
        $back.Count | Should -Be 1
        $back[0].SourcePort | Should -Be '51001'
        $back[0].LastSeen   | Should -Be '2026-06-04T00:00:00.0000000Z'
    }
    It 'archive export blanks timestamps by default and creates the host directory' {
        $r = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue '2026-06-04T00:00:00.0000000Z' -Force
        $p = Join-Path $TestDrive 'hosts\SERVER1\connections.csv'
        Export-NetLogArchive -Records @($r) -Path $p
        Test-Path $p | Should -BeTrue
        $back = Import-NetLogArchive -Path $p
        ($back[0].PSObject.Properties.Name) -contains 'LastSeen' | Should -BeTrue
        $back[0].LastSeen | Should -Be ''
        $back[0].TargetPort | Should -Be '443'
    }
    It 'importing a missing file returns an empty array' {
        (Import-NetLogCache   -Path (Join-Path $TestDrive 'nope.csv')).Count | Should -Be 0
        (Import-NetLogArchive -Path (Join-Path $TestDrive 'nope2.csv')).Count | Should -Be 0
    }
}

Describe 'Cache pruning' {
    BeforeAll {
        function script:NewCacheRec($port, $lastSeen) {
            $r = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort $port -Target 'R' -TargetPort '443' -Process 'app'
            Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue $lastSeen -Force -PassThru
        }
        $script:now = [datetime]::Parse('2026-06-10T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
    }
    It 'removes records older than the lifetime' {
        $old   = NewCacheRec '1' '2026-06-01T00:00:00.0000000Z'   # 9 days old
        $fresh = NewCacheRec '2' '2026-06-09T00:00:00.0000000Z'   # 1 day old
        $res = Remove-ExpiredCacheRecord -Records @($old, $fresh) -NowUtc $now -LifetimeDays 7
        $res.Count | Should -Be 1
        $res[0].SourcePort | Should -Be '2'
    }
    It 'size limit keeps newest records when over budget' {
        $a = NewCacheRec '1' '2026-06-01T00:00:00.0000000Z'
        $b = NewCacheRec '2' '2026-06-05T00:00:00.0000000Z'
        $c = NewCacheRec '3' '2026-06-09T00:00:00.0000000Z'
        # BytesPerRecord 100, MaxSizeBytes 250 -> keep 2 newest
        $res = Limit-NetLogCacheBySize -Records @($a, $b, $c) -MaxSizeBytes 250 -BytesPerRecord 100
        $res.Count | Should -Be 2
        ($res.SourcePort | Sort-Object) -join ',' | Should -Be '2,3'
    }
    It 'size limit of 0 (disabled) returns all records' {
        $a = NewCacheRec '1' '2026-06-01T00:00:00.0000000Z'
        (Limit-NetLogCacheBySize -Records @($a) -MaxSizeBytes 0 -BytesPerRecord 100).Count | Should -Be 1
    }
    It 'size limit smaller than one record keeps all records (no silent wipe)' {
        $a = NewCacheRec '1' '2026-06-09T00:00:00.0000000Z'
        (Limit-NetLogCacheBySize -Records @($a) -MaxSizeBytes 50 -BytesPerRecord 80).Count | Should -Be 1
    }
    It 'rejects a non-positive lifetime' {
        $a = NewCacheRec '1' '2026-06-09T00:00:00.0000000Z'
        { Remove-ExpiredCacheRecord -Records @($a) -NowUtc $now -LifetimeDays 0 } | Should -Throw
    }
    It 'returns empty for empty input' {
        (Remove-ExpiredCacheRecord -Records @() -NowUtc $now -LifetimeDays 7).Count | Should -Be 0
    }
    It 'size limit keeps all records when count exactly equals the budget' {
        $a = NewCacheRec '1' '2026-06-01T00:00:00.0000000Z'
        $b = NewCacheRec '2' '2026-06-05T00:00:00.0000000Z'
        $c = NewCacheRec '3' '2026-06-09T00:00:00.0000000Z'
        (Limit-NetLogCacheBySize -Records @($a,$b,$c) -MaxSizeBytes 300 -BytesPerRecord 100).Count | Should -Be 3
    }
}

Describe 'Read-NetLogConfig' {
    BeforeAll {
        $script:json = @'
{
  "runName": "weekly-audit",
  "createdUtc": "2026-06-04T08:00:00Z",
  "schedule": { "startUtc": "2026-06-05T00:00:00Z", "stopUtc": "2026-06-12T00:00:00Z" },
  "targets": ["SERVER1"],
  "serviceAccount": "DOMAIN\\svc-netlog",
  "settings": {
    "archiveLevel": "Merged",
    "pollIntervalSeconds": 10,
    "archiveIntervalMinutes": 60,
    "cacheLifetimeDays": 7,
    "maxCacheSizeMb": null
  }
}
'@
    }
    It 'parses settings and schedule' {
        $c = Read-NetLogConfig -Json $json
        $c.RunName                 | Should -Be 'weekly-audit'
        $c.ArchiveLevel            | Should -Be 'Merged'
        $c.PollIntervalSeconds     | Should -Be 10
        $c.ArchiveIntervalMinutes  | Should -Be 60
        $c.CacheLifetimeDays       | Should -Be 7
        $c.MaxCacheSizeMb          | Should -Be $null
        $c.StopUtc                 | Should -Be ([datetime]::Parse('2026-06-12T00:00:00Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind))
    }
    It 'throws a clear error when stopUtc is missing' {
        $bad = '{ "runName":"x", "schedule":{}, "settings":{"archiveLevel":"Full","pollIntervalSeconds":1,"archiveIntervalMinutes":1,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }'
        { Read-NetLogConfig -Json $bad } | Should -Throw '*stopUtc*'
    }
    It 'throws a clear error when archiveLevel is invalid' {
        $bad = '{ "runName":"x", "schedule":{"stopUtc":"2026-06-12T00:00:00Z"}, "settings":{"archiveLevel":"Compact","pollIntervalSeconds":1,"archiveIntervalMinutes":1,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }'
        { Read-NetLogConfig -Json $bad } | Should -Throw '*archiveLevel*'
    }
}

Describe 'Status heartbeat' {
    It 'builds a status object with the expected fields' {
        $now = [datetime]::Parse('2026-06-04T01:02:03.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $s = New-NetLogStatus -LastRunUtc $now -ProcessId 4242 -TotalConnections 12 -TotalPolls 7
        $s.lastRunUtc        | Should -Be $now.ToString('o')
        $s.pid               | Should -Be 4242
        $s.totalConnections  | Should -Be 12
        $s.totalPolls        | Should -Be 7
    }
    It 'writes status JSON to disk, creating the directory' {
        $now = [datetime]::Parse('2026-06-04T01:02:03.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $s = New-NetLogStatus -LastRunUtc $now -ProcessId 4242 -TotalConnections 12 -TotalPolls 7
        $p = Join-Path $TestDrive 'hosts\SERVER1\_status.json'
        Write-NetLogStatus -Status $s -Path $p
        Test-Path $p | Should -BeTrue
        $back = Get-Content -LiteralPath $p -Raw | ConvertFrom-Json
        $back.totalConnections | Should -Be 12
    }
    It 'writes status JSON without a UTF-8 BOM' {
        $now = [datetime]::Parse('2026-06-04T01:02:03.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $s = New-NetLogStatus -LastRunUtc $now -ProcessId 1 -TotalConnections 1 -TotalPolls 1
        $p = Join-Path $TestDrive 'hosts\BOMTEST\_status.json'
        Write-NetLogStatus -Status $s -Path $p
        $bytes = [System.IO.File]::ReadAllBytes($p)
        ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) | Should -BeFalse
    }
}

Describe 'Get-NetLogSample' {
    BeforeAll {
        $script:fakeConns = @(
            [pscustomobject]@{ State='Listen';      LocalAddress='0.0.0.0'; LocalPort=445;   RemoteAddress='0.0.0.0'; RemotePort=0;     OwningProcess=4 }
            [pscustomobject]@{ State='Established';  LocalAddress='10.0.0.1';LocalPort=445;   RemoteAddress='10.0.0.9';RemotePort=60001; OwningProcess=4 }
            [pscustomobject]@{ State='Established';  LocalAddress='10.0.0.1';LocalPort=51001; RemoteAddress='10.0.0.5';RemotePort=443;   OwningProcess=1234 }
        )
        $script:fakeMap = @{ 4 = 'System'; 1234 = 'chrome' }
        $script:now = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
    }
    It 'produces records only for Established connections with correct direction (flow semantics)' {
        $recs = Get-NetLogSample -ConnectionProvider { $fakeConns } -ProcessMapProvider { $fakeMap } -NowUtc $now
        $recs.Count | Should -Be 2
        # Flow semantics: the Incoming initiator (the remote peer) is the Source.
        $incoming = $recs | Where-Object { $_.Direction -eq 'Incoming' }
        $incoming.Source     | Should -Be '10.0.0.9'
        $incoming.SourcePort | Should -Be '60001'
        $incoming.Target     | Should -Be '10.0.0.1'
        $incoming.TargetPort | Should -Be '445'
        $incoming.Process    | Should -Be 'System'
        $outgoing = $recs | Where-Object { $_.Direction -eq 'Outgoing' }
        $outgoing.TargetPort | Should -Be '443'
        $outgoing.Process    | Should -Be 'chrome'
        foreach ($rec in $recs) { $rec.LastSeen | Should -Be $now.ToString('o') }
    }
    It 'returns an empty array when there are no connections' {
        $recs = Get-NetLogSample -ConnectionProvider { @() } -ProcessMapProvider { @{} } -NowUtc $now
        $recs.Count | Should -Be 0
    }
    It 'Outgoing: Source is this host, Target is the remote (with resolved TargetHost)' {
        $s = Get-NetLogSample -ConnectionProvider { $fakeConns } -ProcessMapProvider { $fakeMap } `
                              -HostName 'ME01' -HostnameResolver { param($ip) "host-$ip" } -NowUtc $now
        $o = @($s | Where-Object Direction -eq 'Outgoing')[0]
        $o.Source | Should -Be '10.0.0.1';  $o.SourceHost | Should -Be 'ME01';          $o.SourcePort | Should -Be '51001'
        $o.Target | Should -Be '10.0.0.5';  $o.TargetHost | Should -Be 'host-10.0.0.5'; $o.TargetPort | Should -Be '443'
    }
    It 'Incoming: Source is the remote initiator, Target is this host' {
        $s = Get-NetLogSample -ConnectionProvider { $fakeConns } -ProcessMapProvider { $fakeMap } `
                              -HostName 'ME01' -HostnameResolver { param($ip) "host-$ip" } -NowUtc $now
        $i = @($s | Where-Object Direction -eq 'Incoming')[0]
        $i.Source | Should -Be '10.0.0.9';  $i.SourceHost | Should -Be 'host-10.0.0.9'; $i.SourcePort | Should -Be '60001'
        $i.Target | Should -Be '10.0.0.1';  $i.TargetHost | Should -Be 'ME01';          $i.TargetPort | Should -Be '445'
    }
    It 'resolves only the remote peer (one resolver call per established connection)' {
        $ips = [System.Collections.Generic.List[string]]::new()
        $resolver = { param($ip) $ips.Add($ip); '' }.GetNewClosure()
        Get-NetLogSample -ConnectionProvider { $fakeConns } -ProcessMapProvider { $fakeMap } `
                         -HostName 'ME01' -HostnameResolver $resolver -NowUtc $now | Out-Null
        @($ips) | Should -Be @('10.0.0.9','10.0.0.5')
    }
    It 'stamps identity fields from the process and service maps' {
        $conns = @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=51001; RemoteAddress='10.0.0.5'; RemotePort=443; OwningProcess=900 })
        $pmap  = { @{ 900 = @{ Name = 'svchost'; User = 'NT AUTHORITY\NETWORK SERVICE'; Path = 'C:\Windows\System32\svchost.exe' } } }
        $smap  = { @{ 900 = @('Dnscache') } }
        $now   = [datetime]::SpecifyKind([datetime]'2026-08-06T10:00:00', [System.DateTimeKind]::Utc)
        $s = Get-NetLogSample -ConnectionProvider { $conns }.GetNewClosure() -ProcessMapProvider $pmap `
                              -ServiceMapProvider $smap -HostName 'ME01' -NowUtc $now
        $s.Count          | Should -Be 1
        $s[0].Process     | Should -Be 'svchost (Dnscache)'
        $s[0].Usernames   | Should -Be 'NT AUTHORITY\NETWORK SERVICE'
        $s[0].ProcessPath | Should -Be 'C:\Windows\System32\svchost.exe'
        $s[0].SeenCount   | Should -Be '1'
        $s[0].Connections | Should -Be ''
        $s[0].FirstSeen   | Should -Be $now.ToString('o')
        $s[0].LastSeen    | Should -Be $now.ToString('o')
    }
    It 'leaves identity blank when the process map has only a name (fallback shape)' {
        $conns = @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=51001; RemoteAddress='10.0.0.5'; RemotePort=443; OwningProcess=77 })
        $s = Get-NetLogSample -ConnectionProvider { $conns }.GetNewClosure() -ProcessMapProvider { @{ 77 = 'legacyapp' } } `
                              -ServiceMapProvider { @{} } -HostName 'ME01'
        $s[0].Process   | Should -Be 'legacyapp'
        $s[0].Usernames | Should -Be ''
    }
}

Describe 'Legacy record upgrade (ConvertTo-NetLogFlowRecord)' {
    It 'swaps Source/Target on legacy Incoming rows and adds blank host columns' {
        $legacy = [pscustomobject]@{ Direction='Incoming'; Source='10.0.0.1'; SourcePort='8080'; Target='10.0.0.9'; TargetPort='50001'; Process='w3wp' }
        $up = @(ConvertTo-NetLogFlowRecord -Records @($legacy))[0]
        $up.Source     | Should -Be '10.0.0.9'   # remote initiator
        $up.SourcePort | Should -Be '50001'
        $up.Target     | Should -Be '10.0.0.1'   # local acceptor
        $up.TargetPort | Should -Be '8080'
        $up.SourceHost | Should -Be ''
        $up.TargetHost | Should -Be ''
    }
    It 'keeps legacy Outgoing rows as-is (plus blank hosts) and preserves LastSeen' {
        $legacy = [pscustomobject]@{ Direction='Outgoing'; Source='10.0.0.1'; SourcePort='49152'; Target='10.0.0.2'; TargetPort='443'; Process='chrome'; LastSeen='2026-06-11T00:00:00Z' }
        $up = @(ConvertTo-NetLogFlowRecord -Records @($legacy))[0]
        $up.Source   | Should -Be '10.0.0.1'
        $up.Target   | Should -Be '10.0.0.2'
        $up.LastSeen | Should -Be '2026-06-11T00:00:00Z'
    }
    It 'leaves records that already have host columns untouched' {
        $new = New-NetLogRecord -Direction Incoming -Source '10.0.0.9' -SourceHost 'peer' -SourcePort '50001' -Target '10.0.0.1' -TargetHost 'ME' -TargetPort '8080' -Process 'w3wp'
        $up = @(ConvertTo-NetLogFlowRecord -Records @($new))[0]
        $up.Source     | Should -Be '10.0.0.9'
        $up.SourceHost | Should -Be 'peer'
    }
    It 'Import-NetLogArchive upgrades a legacy 6-column file on read' {
        $p = Join-Path $TestDrive 'legacy.csv'
        @'
"Direction","Source","SourcePort","Target","TargetPort","Process"
"Incoming","10.0.0.1","8080","10.0.0.9","50001","w3wp"
'@ | Set-Content -LiteralPath $p -Encoding UTF8
        $rows = Import-NetLogArchive -Path $p
        $rows[0].Source     | Should -Be '10.0.0.9'
        $rows[0].TargetHost | Should -Be ''
    }
}

Describe 'Update-NetLogCache (hostname backfill)' {
    It 'fills a blank host on an existing record when a later sample resolved it, without duplicating' {
        $cached = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetHost '' -TargetPort '443' -Process 'p'
        $sample = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetHost 'srv.corp.local' -TargetPort '443' -Process 'p'
        $seen = Get-SeenKeySet -Records @($cached)
        $out = Update-NetLogCache -Cache @($cached) -Sample @($sample) -SeenKeys $seen
        $out.Count | Should -Be 1
        $out[0].TargetHost | Should -Be 'srv.corp.local'
    }
    It 'does not overwrite an already-resolved host' {
        $cached = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetHost 'first-seen' -TargetPort '443' -Process 'p'
        $sample = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetHost 'other' -TargetPort '443' -Process 'p'
        $seen = Get-SeenKeySet -Records @($cached)
        (Update-NetLogCache -Cache @($cached) -Sample @($sample) -SeenKeys $seen)[0].TargetHost | Should -Be 'first-seen'
    }
}

Describe 'Update-NetLogCache' {
    It 'appends only unseen tuples and updates the seen set' {
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $a -NotePropertyName LastSeen -NotePropertyValue '2026-06-04T00:00:00.0000000Z' -Force
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51002' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $b -NotePropertyName LastSeen -NotePropertyValue '2026-06-04T00:00:05.0000000Z' -Force
        $seen = Get-SeenKeySet -Records @($a)
        $res = Update-NetLogCache -Cache @($a) -Sample @($a, $b) -SeenKeys $seen
        $res.Count | Should -Be 2
        $seen.Count | Should -Be 2
    }
}

Describe 'Invoke-NetLogArchiveCycle' {
    It 'condenses the cache, unions into the archive, and writes status' {
        $now = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        # Seed a cache file with two outgoing tuples differing only by source port.
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $a -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51002' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $b -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force
        $cachePath   = Join-Path $TestDrive 'cache.csv'
        Export-NetLogCache -Records @($a, $b) -Path $cachePath
        $archivePath = Join-Path $TestDrive 'hosts\SERVER1\connections.csv'
        $statusPath  = Join-Path $TestDrive 'hosts\SERVER1\_status.json'

        $res = Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -StatusPath $statusPath `
                                         -Level 'Merged' -ProcessId 4242 -TotalPolls 3 -NowUtc $now
        # Merged collapses the two into one.
        $res.Count | Should -Be 1
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 1
        $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $status.totalConnections | Should -Be 1
        $status.totalPolls       | Should -Be 3
    }
    It 'is idempotent across cycles (re-union does not duplicate)' {
        $now = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $a -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force
        $cachePath   = Join-Path $TestDrive 'cache2.csv'
        Export-NetLogCache -Records @($a) -Path $cachePath
        $archivePath = Join-Path $TestDrive 'hosts\S2\connections.csv'
        $statusPath  = Join-Path $TestDrive 'hosts\S2\_status.json'
        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -StatusPath $statusPath -Level 'Merged' -ProcessId 1 -TotalPolls 1 -NowUtc $now | Out-Null
        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -StatusPath $statusPath -Level 'Merged' -ProcessId 1 -TotalPolls 2 -NowUtc $now | Out-Null
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 1
        $status2 = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $status2.totalConnections | Should -Be 1
        $status2.totalPolls       | Should -Be 2
    }
}

Describe 'Start-NetLogCollector (integration)' {
    It 'runs N iterations and produces archive + status for the host' {
        # Arrange a run folder with run.json copied from the fixture.
        $runPath = Join-Path $TestDrive 'run'
        New-Item -ItemType Directory -Path $runPath -Force | Out-Null
        Copy-Item -Path "$PSScriptRoot\fixtures\run.json" -Destination (Join-Path $runPath 'run.json')
        $cachePath = Join-Path $TestDrive 'local\cache.csv'

        $fakeConns = @(
            [pscustomobject]@{ State='Listen';     LocalAddress='0.0.0.0'; LocalPort=445;   RemoteAddress='0.0.0.0'; RemotePort=0;     OwningProcess=4 }
            [pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1';LocalPort=51001; RemoteAddress='10.0.0.5';RemotePort=443;   OwningProcess=1234 }
        )
        $fakeMap = @{ 4='System'; 1234='chrome' }
        $clock   = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)

        # Act
        Start-NetLogCollector -RunPath $runPath -HostName 'SERVER1' -CachePath $cachePath `
            -ConnectionProvider { $fakeConns } -ProcessMapProvider { $fakeMap } `
            -Clock { $clock } -MaxIterations 2

        # Assert
        $archivePath = Join-Path $runPath 'hosts\SERVER1\connections.csv'
        $statusPath  = Join-Path $runPath 'hosts\SERVER1\_status.json'
        Test-Path $archivePath | Should -BeTrue
        Test-Path $statusPath  | Should -BeTrue
        $rows = Import-NetLogArchive -Path $archivePath
        $rows.Count            | Should -Be 1
        $rows[0].Direction     | Should -Be 'Outgoing'
        $rows[0].SourcePort    | Should -Be ''        # Merged drops ephemeral source port
        $rows[0].TargetPort    | Should -Be '443'
        $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $status.totalPolls     | Should -BeGreaterThan 0
    }

    It 'stops immediately when now is past the stop time' {
        $runPath = Join-Path $TestDrive 'run2'
        New-Item -ItemType Directory -Path $runPath -Force | Out-Null
        # Write a run.json whose stop time is in the past.
        $past = @'
{ "runName":"r","schedule":{"startUtc":"2020-01-01T00:00:00Z","stopUtc":"2020-01-02T00:00:00Z"},
  "settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }
'@
        Set-Content -Path (Join-Path $runPath 'run.json') -Value $past -Encoding UTF8
        $clock = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        Start-NetLogCollector -RunPath $runPath -HostName 'S' -CachePath (Join-Path $TestDrive 'l2\cache.csv') `
            -ConnectionProvider { @() } -ProcessMapProvider { @{} } -Clock { $clock } -MaxIterations 5
        # Past stop time -> no poll happens, but a final flush writes an (empty) archive + status.
        Test-Path (Join-Path $runPath 'hosts\S\_status.json') | Should -BeTrue
        $st = Get-Content -LiteralPath (Join-Path $runPath 'hosts\S\_status.json') -Raw | ConvertFrom-Json
        $st.totalPolls | Should -Be 0
        (Import-NetLogArchive -Path (Join-Path $runPath 'hosts\S\connections.csv')).Count | Should -Be 0
    }

    It 're-reads run.json each cycle so a shortened window ends the run mid-flight' {
        $runPath = Join-Path $TestDrive 'runlive'
        New-Item -ItemType Directory -Path $runPath -Force | Out-Null
        $jsonPath = Join-Path $runPath 'run.json'
        # Start with a far-future stop so it would otherwise run every iteration.
        Set-Content -Path $jsonPath -Encoding UTF8 -Value @'
{ "runName":"r","schedule":{"startUtc":"2026-06-01T00:00:00Z","stopUtc":"2027-01-01T00:00:00Z"},
  "settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }
'@
        $shortJson = @'
{ "runName":"r","schedule":{"startUtc":"2026-06-01T00:00:00Z","stopUtc":"2026-06-03T00:00:00Z"},
  "settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"maxCacheSizeMb":null} }
'@
        $clock = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        # The first poll shortens the window to a past stop; the next cycle's re-read must pick it up and exit.
        $shorten = { Set-Content -Path $jsonPath -Value $shortJson -Encoding UTF8; @() }
        Start-NetLogCollector -RunPath $runPath -HostName 'S' -CachePath (Join-Path $TestDrive 'live\cache.csv') `
            -ConnectionProvider $shorten -ProcessMapProvider { @{} } -Clock { $clock } -MaxIterations 10
        $st = Get-Content -LiteralPath (Join-Path $runPath 'hosts\S\_status.json') -Raw | ConvertFrom-Json
        $st.totalPolls | Should -Be 1   # only the first cycle sampled; the live re-read then ended the run
    }

    It 'rebuilds the seen-set on restart so a second run adds no duplicate rows' {
        $runPath = Join-Path $TestDrive 'run3'
        New-Item -ItemType Directory -Path $runPath -Force | Out-Null
        Copy-Item -Path "$PSScriptRoot\fixtures\run.json" -Destination (Join-Path $runPath 'run.json')
        $cachePath = Join-Path $TestDrive 'local3\cache.csv'
        $fakeConns = @(
            [pscustomobject]@{ State='Listen';     LocalAddress='0.0.0.0'; LocalPort=445;   RemoteAddress='0.0.0.0'; RemotePort=0;   OwningProcess=4 }
            [pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1';LocalPort=51001; RemoteAddress='10.0.0.5';RemotePort=443; OwningProcess=1234 }
        )
        $fakeMap = @{ 4='System'; 1234='chrome' }
        $clock   = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $callArgs = @{
            RunPath=$runPath; HostName='SERVER1'; CachePath=$cachePath
            ConnectionProvider={ $fakeConns }; ProcessMapProvider={ $fakeMap }; Clock={ $clock }; MaxIterations=2
        }
        Start-NetLogCollector @callArgs       # first run
        Start-NetLogCollector @callArgs       # restart against the SAME cache + archive
        $archivePath = Join-Path $runPath 'hosts\SERVER1\connections.csv'
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 1   # no duplicate from the restart
    }
}

Describe 'Get-NetLogPathDiagnostic' {
    It 'reports each ancestor and pinpoints the first missing level' {
        $base = Join-Path $TestDrive 'a\b'
        New-Item -ItemType Directory -Path $base -Force | Out-Null
        $missing = Join-Path $base 'run.json'   # folder exists, file does not
        $diag = Get-NetLogPathDiagnostic -Path $missing
        $byPath = @{}; foreach ($d in $diag) { $byPath[$d.Path] = $d.Exists }
        $byPath[$base]    | Should -BeTrue
        $byPath[$missing] | Should -BeFalse
        $diag[-1].Path    | Should -Be $missing   # deepest entry is the file itself
    }
    It 'marks all levels present when the file exists' {
        $base = Join-Path $TestDrive 'c\d'
        New-Item -ItemType Directory -Path $base -Force | Out-Null
        $f = Join-Path $base 'run.json'; 'x' | Set-Content -LiteralPath $f
        $diag = Get-NetLogPathDiagnostic -Path $f
        @($diag | Where-Object { -not $_.Exists }).Count | Should -Be 0
    }
    It 'does not ascend past a UNC share root (avoids the slow bare-server probe)' {
        # Pure parsing: an unreachable server path still returns quickly because the bare
        # \\server level is never tested. The topmost level is the share root.
        $diag = Get-NetLogPathDiagnostic -Path '\\nosuchserver.invalid\share$\runs\test\run.json'
        $diag[0].Path | Should -Be '\\nosuchserver.invalid\share$'
        @($diag | Where-Object { $_.Path -eq '\\nosuchserver.invalid' }).Count | Should -Be 0
    }
}

Describe 'Write-NetLogLog' {
    It 'appends timestamped lines and creates the directory' {
        $p = Join-Path $TestDrive 'logs\collector.log'
        Write-NetLogLog -Path $p -Message 'first'
        Write-NetLogLog -Path $p -Message 'second'
        $lines = Get-Content $p
        $lines.Count | Should -Be 2
        $lines[0] | Should -Match '^\d{4}-\d{2}-\d{2}T.*Z?\s+first$'
        $lines[1] | Should -Match 'second$'
    }
    It 'is a no-op for an empty path and never throws on an unwritable one' {
        { Write-NetLogLog -Path '' -Message 'x' } | Should -Not -Throw
        { Write-NetLogLog -Path 'Q:\no\such\drive>\x.log' -Message 'x' } | Should -Not -Throw
    }
}

Describe 'Start-NetLogCollector (logging)' {
    It 'logs startup, archive cycles, and the exit reason' {
        $run = Join-Path $TestDrive 'logrun'
        New-Item -ItemType Directory -Path $run -Force | Out-Null
        Copy-Item -Path "$PSScriptRoot\fixtures\run.json" -Destination (Join-Path $run 'run.json')
        $log = Join-Path $TestDrive 'logrun-local\collector.log'
        $conns = {
            @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=49152; RemoteAddress='10.0.0.5'; RemotePort=443; OwningProcess=1 })
        }
        Start-NetLogCollector -RunPath $run -HostName 'L1' -CachePath (Join-Path $TestDrive 'logrun-local\cache.csv') `
            -ConnectionProvider $conns -ProcessMapProvider { @{1='p'} } -MaxIterations 2 -LogPath $log
        $text = Get-Content $log -Raw
        $text | Should -Match 'collector starting'
        $text | Should -Match 'run=test-run'
        $text | Should -Match 'archive cycle'
        $text | Should -Match 'exiting'
    }
}

Describe 'Start-NetLogCollector.ps1 entry script (fail loud)' {
    It 'exits 1 and logs FATAL when the run path is unreadable' {
        $log = Join-Path $TestDrive 'entry-fail.log'
        $entry = Join-Path (Split-Path $PSScriptRoot -Parent) 'Start-NetLogCollector.ps1'
        & pwsh -NoProfile -File $entry -RunPath (Join-Path $TestDrive 'no-such-run') -LogPath $log 2>$null
        $LASTEXITCODE | Should -Be 1
        (Get-Content $log -Raw) | Should -Match 'FATAL'
    }
}

Describe 'Start-NetLogCollector (waits for the window start)' {
    It 'does not sample before StartUtc and samples once inside the window' {
        $run = Join-Path $TestDrive 'waitrun'
        New-Item -ItemType Directory -Path $run -Force | Out-Null
        $start = '2026-06-12T10:00:00Z'
        $stop  = '2026-06-12T11:00:00Z'
        ('{"runName":"wr","schedule":{"startUtc":"' + $start + '","stopUtc":"' + $stop + '"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"resolveHostnames":false}}') |
            Set-Content -LiteralPath (Join-Path $run 'run.json') -Encoding UTF8
        # Clock: first tick before the window, second tick inside it.
        $script:tick = 0
        $clock = {
            $script:tick++
            if ($script:tick -le 1) { [datetime]::new(2026, 6, 12, 9, 59, 0, [System.DateTimeKind]::Utc) }
            else                    { [datetime]::new(2026, 6, 12, 10, 0, 5, [System.DateTimeKind]::Utc) }
        }
        $script:sampleCalls = 0
        $conns = {
            $script:sampleCalls++
            @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=49152; RemoteAddress='10.0.0.2'; RemotePort=443; OwningProcess=100 })
        }
        Start-NetLogCollector -RunPath $run -HostName 'W1' -CachePath (Join-Path $TestDrive 'wait-cache.csv') `
            -ConnectionProvider $conns -ProcessMapProvider { @{100='p'} } -Clock $clock -MaxIterations 2
        $script:sampleCalls | Should -Be 1   # the pre-window iteration must not sample
        (Import-NetLogArchive -Path (Join-Path $run 'hosts\W1\connections.csv')).Count | Should -Be 1
    }
}

Describe 'Start-NetLogCollector (hostname resolution lifecycle)' {
    It 'retries negative resolutions after an archive cycle (backfill path)' {
        $run = Join-Path $TestDrive 'resolverrun'
        New-Item -ItemType Directory -Path $run -Force | Out-Null
        $stop = (Get-Date).ToUniversalTime().AddDays(1).ToString('o')
        ('{"runName":"rr","schedule":{"stopUtc":"' + $stop + '"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7}}') |
            Set-Content -LiteralPath (Join-Path $run 'run.json') -Encoding UTF8
        $conns = {
            @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=49152; RemoteAddress='10.0.0.2'; RemotePort=443; OwningProcess=100 })
        }
        # First resolution attempt fails; the second (after the negative cache is pruned
        # by the archive cycle) succeeds and must backfill the cached record.
        $script:ptrAttempts = 0
        $cachePath = Join-Path $TestDrive 'resolver-cache.csv'
        Start-NetLogCollector -RunPath $run -HostName 'ME01' -CachePath $cachePath `
            -ConnectionProvider $conns -ProcessMapProvider { @{100='p'} } -MaxIterations 2 `
            -DnsCacheProvider { param($ip) $null } `
            -PtrProvider { param($ip) $script:ptrAttempts++; if ($script:ptrAttempts -lt 2) { throw 'transient' } else { 'late.corp.local' } }
        $rows = Import-Csv (Join-Path $run 'hosts\ME01\connections.csv')
        $rows[0].TargetHost | Should -Be 'late.corp.local'
    }
    It 'leaves host columns blank (except its own name) when resolveHostnames is false' {
        $run = Join-Path $TestDrive 'noresolverun'
        New-Item -ItemType Directory -Path $run -Force | Out-Null
        $stop = (Get-Date).ToUniversalTime().AddDays(1).ToString('o')
        ('{"runName":"nr","schedule":{"stopUtc":"' + $stop + '"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"resolveHostnames":false}}') |
            Set-Content -LiteralPath (Join-Path $run 'run.json') -Encoding UTF8
        $conns = {
            @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=49152; RemoteAddress='10.0.0.2'; RemotePort=443; OwningProcess=100 })
        }
        Start-NetLogCollector -RunPath $run -HostName 'ME01' -CachePath (Join-Path $TestDrive 'nores-cache.csv') `
            -ConnectionProvider $conns -ProcessMapProvider { @{100='p'} } -MaxIterations 1 `
            -PtrProvider { param($ip) throw 'must not be called' } -DnsCacheProvider { param($ip) throw 'must not be called' }
        $rows = Import-Csv (Join-Path $run 'hosts\ME01\connections.csv')
        $rows[0].SourceHost | Should -Be 'ME01'
        $rows[0].TargetHost | Should -Be ''
    }
}

Describe 'Start-NetLogCollector share resilience' {
    It 'keeps running and preserves the local cache when the archive path is unwritable' {
        $runPath = Join-Path $TestDrive 'resrun'
        New-Item -ItemType Directory -Path $runPath -Force | Out-Null
        Copy-Item -Path "$PSScriptRoot\fixtures\run.json" -Destination (Join-Path $runPath 'run.json')
        $cachePath = Join-Path $TestDrive 'reslocal\cache.csv'

        $fakeConns = @(
            [pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=51001; RemoteAddress='10.0.0.5'; RemotePort=443; OwningProcess=1234 }
        )
        $fakeMap = @{ 1234='chrome' }
        $clock = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)

        # Make the archive write fail: a host folder name containing a char that is illegal in a
        # Windows path segment ('>'), so Export-NetLogArchive's New-Item -ItemType Directory throws.
        $badHost = 'BADHOST>'

        { Start-NetLogCollector -RunPath $runPath -HostName $badHost -CachePath $cachePath `
            -ConnectionProvider { $fakeConns } -ProcessMapProvider { $fakeMap } -Clock { $clock } -MaxIterations 2 } |
            Should -Not -Throw

        # The local cache must still have been written despite the archive failure.
        Test-Path $cachePath | Should -BeTrue
        (Import-NetLogCache -Path $cachePath).Count | Should -BeGreaterThan 0
    }
}

Describe 'Read-NetLogConfig (UTC normalisation)' {
    # run.json written by the CLI carries "+00:00" offsets (System.Text.Json). Parsing those
    # with RoundtripKind alone yields LOCAL-kind ticks, which the collector then compares
    # against a UTC clock — skewing the window by the host's UTC offset.
    It 'normalises an offset-format stopUtc to UTC ticks' {
        $json = '{"runName":"r","schedule":{"startUtc":"2026-06-12T09:00:00+00:00","stopUtc":"2026-06-12T09:10:00+00:00"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        $c = Read-NetLogConfig -Json $json
        $c.StopUtc.Kind | Should -Be 'Utc'
        $c.StopUtc      | Should -Be ([datetime]::new(2026, 6, 12, 9, 10, 0, [System.DateTimeKind]::Utc))
    }
    It 'converts a non-zero offset to the equivalent UTC instant' {
        $json = '{"runName":"r","schedule":{"stopUtc":"2026-06-12T11:10:00+02:00"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        (Read-NetLogConfig -Json $json).StopUtc | Should -Be ([datetime]::new(2026, 6, 12, 9, 10, 0, [System.DateTimeKind]::Utc))
    }
    It 'exposes StartUtc in UTC, defaulting to MinValue when absent' {
        $json = '{"runName":"r","schedule":{"startUtc":"2026-06-12T11:00:00+02:00","stopUtc":"2026-06-12T09:10:00Z"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        (Read-NetLogConfig -Json $json).StartUtc | Should -Be ([datetime]::new(2026, 6, 12, 9, 0, 0, [System.DateTimeKind]::Utc))
        $json2 = '{"runName":"r","schedule":{"stopUtc":"2026-06-12T09:10:00Z"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        (Read-NetLogConfig -Json $json2).StartUtc | Should -Be ([datetime]::MinValue)
    }
}

Describe 'Read-NetLogConfig (runSequence)' {
    It 'defaults to 1 when absent (existing runs)' {
        $json = '{"runName":"r","schedule":{"stopUtc":"2026-07-01T00:00:00Z"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        (Read-NetLogConfig -Json $json).RunSequence | Should -Be 1
    }
    It 'reads an explicit sequence' {
        $json = '{"runName":"r","runSequence":3,"schedule":{"stopUtc":"2026-07-01T00:00:00Z"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        (Read-NetLogConfig -Json $json).RunSequence | Should -Be 3
    }
}

Describe 'Invoke-NetLogArchiveCycle (per-run archive)' {
    It 'writes the cycle to both the per-run file and the cumulative file' {
        $now = [datetime]::Parse('2026-06-12T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R2' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $a -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force
        $cachePath = Join-Path $TestDrive 'prr\cache.csv'
        Export-NetLogCache -Records @($a) -Path $cachePath
        $hostDir     = Join-Path $TestDrive 'prr\hosts\S1'
        $archivePath = Join-Path $hostDir 'connections.csv'
        $runPath     = Join-Path $hostDir 'run2.csv'
        # Pre-existing cumulative content from run 1 (a different connection).
        $r1 = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '50000' -Target 'R1' -TargetPort '443' -Process 'old'
        Export-NetLogArchive -Records @($r1) -Path $archivePath

        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -RunArchivePath $runPath `
            -StatusPath (Join-Path $hostDir '_status.json') -Level 'Full' -ProcessId 1 -TotalPolls 1 -NowUtc $now | Out-Null

        # Per-run file holds ONLY this run's connection.
        $runRows = Import-NetLogArchive -Path $runPath
        ($runRows).Count    | Should -Be 1
        $runRows[0].Target  | Should -Be 'R2'
        # Cumulative holds both runs.
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 2
    }
    It 'per-run file is cumulative within the run (idempotent across cycles)' {
        $now = [datetime]::Parse('2026-06-12T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target 'R' -TargetPort '443' -Process 'app'
        Add-Member -InputObject $a -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force
        $cachePath = Join-Path $TestDrive 'prr2\cache.csv'
        Export-NetLogCache -Records @($a) -Path $cachePath
        $hostDir = Join-Path $TestDrive 'prr2\hosts\S1'
        $cycleArgs = @{
            CachePath = $cachePath; ArchivePath = (Join-Path $hostDir 'connections.csv')
            RunArchivePath = (Join-Path $hostDir 'run1.csv'); StatusPath = (Join-Path $hostDir '_status.json')
            Level = 'Full'; ProcessId = 1; NowUtc = $now
        }
        Invoke-NetLogArchiveCycle @cycleArgs -TotalPolls 1 | Out-Null
        Invoke-NetLogArchiveCycle @cycleArgs -TotalPolls 2 | Out-Null
        (Import-NetLogArchive -Path (Join-Path $hostDir 'run1.csv')).Count | Should -Be 1
    }
}

Describe 'Read-NetLogConfig (resolveHostnames)' {
    It 'defaults to true when the setting is absent (configs from older CLIs)' {
        $json = '{"runName":"r","schedule":{"stopUtc":"2026-07-01T00:00:00Z"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60}}'
        (Read-NetLogConfig -Json $json).ResolveHostnames | Should -BeTrue
    }
    It 'honours an explicit false' {
        $json = '{"runName":"r","schedule":{"stopUtc":"2026-07-01T00:00:00Z"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":10,"archiveIntervalMinutes":60,"resolveHostnames":false}}'
        (Read-NetLogConfig -Json $json).ResolveHostnames | Should -BeFalse
    }
}

Describe 'Exports carry host columns' {
    It 'Export-NetLogArchive writes the 14-column header with blank timestamps by default' {
        $p = Join-Path $TestDrive 'exp-a.csv'
        $r = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '' -Target '10.0.0.2' -TargetHost 'srv' -TargetPort '443' -Process 'p'
        Export-NetLogArchive -Records @($r) -Path $p
        (Get-Content $p -TotalCount 1) | Should -Be '"Direction","Source","SourceHost","SourcePort","Target","TargetHost","TargetPort","Process","Usernames","ProcessPath","SeenCount","Connections","FirstSeen","LastSeen"'
    }
    It 'Export-NetLogCache writes all 14 columns with timestamps' {
        $p = Join-Path $TestDrive 'exp-c.csv'
        $r = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetPort '443' -Process 'p'
        Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue '2026-06-12T00:00:00Z' -Force
        Export-NetLogCache -Records @($r) -Path $p
        (Get-Content $p -TotalCount 1) | Should -Be '"Direction","Source","SourceHost","SourcePort","Target","TargetHost","TargetPort","Process","Usernames","ProcessPath","SeenCount","Connections","FirstSeen","LastSeen"'
    }
}

Describe 'Resolve-NetLogHostname' {
    It 'prefers the DNS client cache entry (the name the app used)' {
        $cache = @{}
        $r = Resolve-NetLogHostname -IpAddress '10.1.2.3' -Cache $cache `
            -DnsCacheProvider { param($ip) 'api.contoso.com' } `
            -PtrProvider      { param($ip) 'ptr.contoso.com' }
        $r | Should -Be 'api.contoso.com'
    }
    It 'falls back to PTR when the DNS cache has nothing' {
        $r = Resolve-NetLogHostname -IpAddress '10.1.2.3' -Cache @{} `
            -DnsCacheProvider { param($ip) $null } `
            -PtrProvider      { param($ip) 'srv01.corp.local' }
        $r | Should -Be 'srv01.corp.local'
    }
    It 'negative-caches failures and does not retry on the next call' {
        $calls = [ref]0
        $cache = @{}
        $ptr = { param($ip) $calls.Value++; throw 'no PTR' }.GetNewClosure()
        Resolve-NetLogHostname -IpAddress '10.9.9.9' -Cache $cache -DnsCacheProvider { param($ip) $null } -PtrProvider $ptr | Should -Be ''
        Resolve-NetLogHostname -IpAddress '10.9.9.9' -Cache $cache -DnsCacheProvider { param($ip) $null } -PtrProvider $ptr | Should -Be ''
        $calls.Value | Should -Be 1
    }
    It 'serves positive hits from the cache without re-resolving' {
        $calls = [ref]0
        $cache = @{}
        $dns = { param($ip) $calls.Value++; 'db.corp.local' }.GetNewClosure()
        Resolve-NetLogHostname -IpAddress '10.0.0.5' -Cache $cache -DnsCacheProvider $dns -PtrProvider { param($ip) '' }
        Resolve-NetLogHostname -IpAddress '10.0.0.5' -Cache $cache -DnsCacheProvider $dns -PtrProvider { param($ip) '' } | Should -Be 'db.corp.local'
        $calls.Value | Should -Be 1
    }
    It 'returns blank for unresolvable pseudo addresses without calling providers' {
        $r = Resolve-NetLogHostname -IpAddress '0.0.0.0' -Cache @{} `
            -DnsCacheProvider { param($ip) throw 'must not be called' } `
            -PtrProvider      { param($ip) throw 'must not be called' }
        $r | Should -Be ''
    }
}

Describe 'New-NetLogRecord (hostnames)' {
    It 'carries SourceHost and TargetHost in column order' {
        $r = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '50000' `
                              -Target '10.0.0.2' -TargetHost 'srv.corp.local' -TargetPort '443' -Process 'chrome'
        @($r.psobject.Properties.Name) | Should -Be @('Direction','Source','SourceHost','SourcePort','Target','TargetHost','TargetPort','Process','Usernames','ProcessPath','SeenCount','Connections','FirstSeen','LastSeen')
        $r.SourceHost | Should -Be 'ME'
        $r.TargetHost | Should -Be 'srv.corp.local'
    }
    It 'defaults host columns to blank for legacy-style calls' {
        $r = New-NetLogRecord -Direction Incoming -Source '10.0.0.9' -SourcePort '50001' -Target '10.0.0.1' -TargetPort '443' -Process 'w3wp'
        $r.SourceHost | Should -Be ''
        $r.TargetHost | Should -Be ''
    }
}

Describe 'ConvertTo-NetLogRecordKey (hostnames are not part of the key)' {
    It 'yields identical keys for records differing only in host columns' {
        $a = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourceHost '' -SourcePort '5' -Target '10.0.0.2' -TargetHost '' -TargetPort '443' -Process 'p'
        $b = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '5' -Target '10.0.0.2' -TargetHost 'srv' -TargetPort '443' -Process 'p'
        (ConvertTo-NetLogRecordKey -Record $a) | Should -Be (ConvertTo-NetLogRecordKey -Record $b)
    }
}

Describe 'Invoke-NetLogCondense (flow semantics)' {
    It 'Merged always blanks SourcePort (the initiator ephemeral port) for both directions' {
        $recs = @(
            (New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '49152' -Target '10.0.0.2' -TargetPort '443' -Process 'chrome'),
            (New-NetLogRecord -Direction Incoming -Source '10.0.0.9' -SourcePort '49999' -Target '10.0.0.1' -TargetPort '8080' -Process 'w3wp')
        )
        $out = Invoke-NetLogCondense -Records $recs -Level Merged
        $out[0].SourcePort | Should -Be ''
        $out[0].TargetPort | Should -Be '443'
        $out[1].SourcePort | Should -Be ''
        $out[1].TargetPort | Should -Be '8080'
    }
    It 'passes host columns through at both levels' {
        $r = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '49152' `
                              -Target '10.0.0.2' -TargetHost 'srv' -TargetPort '443' -Process 'p'
        (Invoke-NetLogCondense -Records @($r) -Level Merged)[0].TargetHost | Should -Be 'srv'
        (Invoke-NetLogCondense -Records @($r) -Level Full)[0].SourceHost   | Should -Be 'ME'
    }
    It 'backfills a blank host on the kept record when a duplicate carries the name (archive merge path)' {
        $old = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetHost '' -TargetPort '443' -Process 'p'
        $new = New-NetLogRecord -Direction Outgoing -Source '10.0.0.1' -SourcePort '5' -Target '10.0.0.2' -TargetHost 'srv.corp.local' -TargetPort '443' -Process 'p'
        $res = Invoke-NetLogCondense -Records @($old, $new) -Level Full
        $res.Count | Should -Be 1
        $res[0].TargetHost | Should -Be 'srv.corp.local'
    }
}

Describe 'Merge-NetLogValueList' {
    It 'unions two semicolon lists, sorted and deduplicated' {
        Merge-NetLogValueList -A 'DOMAIN\bob;DOMAIN\alice' -B 'DOMAIN\alice;NT AUTHORITY\SYSTEM' |
            Should -Be 'DOMAIN\alice;DOMAIN\bob;NT AUTHORITY\SYSTEM'
    }
    It 'deduplicates case-insensitively' {
        Merge-NetLogValueList -A 'DOMAIN\Alice' -B 'domain\alice' | Should -Be 'DOMAIN\Alice'
    }
    It 'handles blank sides' {
        Merge-NetLogValueList -A '' -B 'x' | Should -Be 'x'
        Merge-NetLogValueList -A 'x' -B '' | Should -Be 'x'
        Merge-NetLogValueList -A '' -B ''  | Should -Be ''
    }
}

Describe 'Update-NetLogCache (identity bookkeeping)' {
    BeforeEach {
        $script:seen = [System.Collections.Generic.HashSet[string]]::new()
        $script:t0 = '2026-08-06T10:00:00.0000000Z'
        $script:t1 = '2026-08-06T10:00:10.0000000Z'
        $script:mk = {
            param($user, $stamp)
            New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourceHost 'ME01' -SourcePort '51001' `
                             -Target 'R' -TargetPort '443' -Process 'app' `
                             -Usernames $user -ProcessPath 'C:\x\app.exe' -SeenCount '1' `
                             -FirstSeen $stamp -LastSeen $stamp
        }
    }
    It 'increments SeenCount and advances LastSeen on a re-seen key' {
        $cache = Update-NetLogCache -Cache @() -Sample @(& $mk 'DOMAIN\a' $t0) -SeenKeys $seen
        $cache = Update-NetLogCache -Cache $cache -Sample @(& $mk 'DOMAIN\a' $t1) -SeenKeys $seen
        $cache.Count        | Should -Be 1
        $cache[0].SeenCount | Should -Be '2'
        $cache[0].FirstSeen | Should -Be $t0
        $cache[0].LastSeen  | Should -Be $t1
    }
    It 'unions usernames across sightings' {
        $cache = Update-NetLogCache -Cache @() -Sample @(& $mk 'DOMAIN\b' $t0) -SeenKeys $seen
        $cache = Update-NetLogCache -Cache $cache -Sample @(& $mk 'DOMAIN\a' $t1) -SeenKeys $seen
        $cache[0].Usernames | Should -Be 'DOMAIN\a;DOMAIN\b'
    }
    It 'upgrades a legacy cache record (no identity properties) without throwing' {
        # Simulates a record imported from a pre-identity cache file.
        $legacy = [pscustomobject][ordered]@{
            Direction='Outgoing'; Source='H'; SourceHost='ME01'; SourcePort='51001'
            Target='R'; TargetHost=''; TargetPort='443'; Process='app'; LastSeen=$t0
        }
        [void]$seen.Add((ConvertTo-NetLogRecordKey -Record $legacy))
        $cache = Update-NetLogCache -Cache @($legacy) -Sample @(& $mk 'DOMAIN\a' $t1) -SeenKeys $seen
        $cache[0].SeenCount | Should -Be '1'      # blank treated as 0, then incremented
        $cache[0].Usernames | Should -Be 'DOMAIN\a'
        $cache[0].LastSeen  | Should -Be $t1
        $cache[0].FirstSeen | Should -Be $t1      # unknown -> first known sighting
    }
}

Describe 'Invoke-NetLogCondense (identity merge rules)' {
    BeforeAll {
        $script:mkFull = {
            param($port, $user, $seen, $first, $last)
            New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourceHost 'ME01' -SourcePort $port `
                             -Target 'R' -TargetPort '443' -Process 'app' `
                             -Usernames $user -ProcessPath 'C:\x\app.exe' -SeenCount $seen `
                             -FirstSeen $first -LastSeen $last
        }
    }
    It 'Merged: counts distinct source ports as Connections and takes max SeenCount' {
        $recs = @(
            (& $mkFull '51001' 'DOMAIN\a' '5' '2026-08-06T10:00:00.0000000Z' '2026-08-06T11:00:00.0000000Z'),
            (& $mkFull '51002' 'DOMAIN\b' '3' '2026-08-06T09:00:00.0000000Z' '2026-08-06T12:00:00.0000000Z')
        )
        $res = Invoke-NetLogCondense -Records $recs -Level 'Merged'
        $res.Count          | Should -Be 1
        $res[0].Connections | Should -Be '2'
        $res[0].SeenCount   | Should -Be '5'
        $res[0].Usernames   | Should -Be 'DOMAIN\a;DOMAIN\b'
        $res[0].FirstSeen   | Should -Be '2026-08-06T09:00:00.0000000Z'
        $res[0].LastSeen    | Should -Be '2026-08-06T12:00:00.0000000Z'
    }
    It 'Merged: is idempotent - re-condensing archive rows with the same cache yields identical counts' {
        $cacheRows = @(
            (& $mkFull '51001' 'DOMAIN\a' '5' '' ''),
            (& $mkFull '51002' 'DOMAIN\a' '3' '' '')
        )
        $first  = Invoke-NetLogCondense -Records $cacheRows -Level 'Merged'
        $second = Invoke-NetLogCondense -Records (@($first) + $cacheRows) -Level 'Merged'
        $second.Count          | Should -Be 1
        $second[0].Connections | Should -Be $first[0].Connections
        $second[0].SeenCount   | Should -Be $first[0].SeenCount
    }
    It 'Full: passes counters through and leaves Connections blank' {
        $res = Invoke-NetLogCondense -Records @((& $mkFull '51001' 'DOMAIN\a' '4' '' '')) -Level 'Full'
        $res[0].SeenCount   | Should -Be '4'
        $res[0].Connections | Should -Be ''
    }
    It 'tolerates legacy records without identity properties' {
        $legacy = [pscustomobject][ordered]@{
            Direction='Outgoing'; Source='H'; SourceHost=''; SourcePort='51001'
            Target='R'; TargetHost=''; TargetPort='443'; Process='app'
        }
        $res = Invoke-NetLogCondense -Records @($legacy) -Level 'Full'
        $res[0].SeenCount | Should -Be ''
        $res[0].Usernames | Should -Be ''
    }
}

Describe 'Exports carry identity columns' {
    BeforeEach {
        $script:tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("nl_id_" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tmp | Out-Null
        $script:rec = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourceHost 'ME01' -SourcePort '' `
            -Target 'R' -TargetPort '443' -Process 'app' -Usernames 'DOMAIN\a' -ProcessPath 'C:\x\app.exe' `
            -SeenCount '5' -Connections '2' -FirstSeen '2026-08-06T09:00:00.0000000Z' -LastSeen '2026-08-06T12:00:00.0000000Z'
    }
    AfterEach { Remove-Item -Recurse -Force $tmp }
    It 'archive: writes all 14 columns and blanks timestamps by default' {
        $p = Join-Path $tmp 'connections.csv'
        Export-NetLogArchive -Records @($rec) -Path $p
        $rows = @(Import-Csv -LiteralPath $p)
        ($rows[0].PSObject.Properties.Name) -join ',' |
            Should -Be 'Direction,Source,SourceHost,SourcePort,Target,TargetHost,TargetPort,Process,Usernames,ProcessPath,SeenCount,Connections,FirstSeen,LastSeen'
        $rows[0].Usernames | Should -Be 'DOMAIN\a'
        $rows[0].SeenCount | Should -Be '5'
        $rows[0].FirstSeen | Should -Be ''
        $rows[0].LastSeen  | Should -Be ''
    }
    It 'archive: -IncludeTimestamps writes the stamps' {
        $p = Join-Path $tmp 'connections.csv'
        Export-NetLogArchive -Records @($rec) -Path $p -IncludeTimestamps
        (Import-Csv -LiteralPath $p)[0].FirstSeen | Should -Be '2026-08-06T09:00:00.0000000Z'
    }
    It 'cache: always keeps real timestamps (pruning needs LastSeen)' {
        $p = Join-Path $tmp 'cache.csv'
        Export-NetLogCache -Records @($rec) -Path $p
        $row = (Import-Csv -LiteralPath $p)[0]
        $row.LastSeen  | Should -Be '2026-08-06T12:00:00.0000000Z'
        $row.Usernames | Should -Be 'DOMAIN\a'
    }
    It 'archive cycle: identity survives a cache -> archive -> re-archive round trip unchanged (idempotence)' {
        $cachePath = Join-Path $tmp 'cache.csv'
        $archPath  = Join-Path $tmp 'connections.csv'
        $statPath  = Join-Path $tmp '_status.json'
        $full = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourceHost 'ME01' -SourcePort '51001' `
            -Target 'R' -TargetPort '443' -Process 'app' -Usernames 'DOMAIN\a' -SeenCount '5' `
            -FirstSeen '2026-08-06T09:00:00.0000000Z' -LastSeen '2026-08-06T12:00:00.0000000Z'
        Export-NetLogCache -Records @($full) -Path $cachePath
        $now = [datetime]::SpecifyKind([datetime]'2026-08-06T13:00:00', [System.DateTimeKind]::Utc)
        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archPath -StatusPath $statPath `
            -Level 'Merged' -ProcessId 1 -TotalPolls 1 -NowUtc $now | Out-Null
        $first = (Import-Csv -LiteralPath $archPath)[0]
        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archPath -StatusPath $statPath `
            -Level 'Merged' -ProcessId 1 -TotalPolls 2 -NowUtc $now | Out-Null
        $second = (Import-Csv -LiteralPath $archPath)[0]
        $second.SeenCount   | Should -Be $first.SeenCount
        $second.Connections | Should -Be $first.Connections
        $second.Usernames   | Should -Be $first.Usernames
    }
}

Describe 'Read-NetLogConfig (timestamps)' {
    It 'defaults timestamps to false when absent' {
        $json = '{ "runName":"r","schedule":{"stopUtc":"2026-08-07T00:00:00Z"},"settings":{"archiveLevel":"Merged","pollIntervalSeconds":10,"archiveIntervalMinutes":60} }'
        (Read-NetLogConfig -Json $json).Timestamps | Should -BeFalse
    }
    It 'reads timestamps=true' {
        $json = '{ "runName":"r","schedule":{"stopUtc":"2026-08-07T00:00:00Z"},"settings":{"archiveLevel":"Merged","pollIntervalSeconds":10,"archiveIntervalMinutes":60,"timestamps":true} }'
        (Read-NetLogConfig -Json $json).Timestamps | Should -BeTrue
    }
}

Describe 'Start-NetLogCollector (identity enrichment wiring)' {
    It 'writes usernames and seen counts into the archive' {
        $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("nl_wire_" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $tmp 'run') -Force | Out-Null
        $stop = (Get-Date).ToUniversalTime().AddMinutes(5).ToString('o')
        @"
{ "runName":"r","schedule":{"stopUtc":"$stop"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"timestamps":true} }
"@ | Set-Content -LiteralPath (Join-Path $tmp 'run\run.json') -Encoding UTF8
        $conns = @([pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=51001; RemoteAddress='10.0.0.5'; RemotePort=443; OwningProcess=42 })
        Start-NetLogCollector -RunPath (Join-Path $tmp 'run') -HostName 'ME01' `
            -CachePath (Join-Path $tmp 'cache.csv') -MaxIterations 1 `
            -ConnectionProvider { $conns }.GetNewClosure() `
            -ProcessMapProvider { @{ 42 = @{ Name = 'app'; User = 'DOMAIN\a'; Path = 'C:\x\app.exe' } } } `
            -ServiceMapProvider { @{} }
        $row = (Import-Csv -LiteralPath (Join-Path $tmp 'run\hosts\ME01\connections.csv'))[0]
        $row.Usernames   | Should -Be 'DOMAIN\a'
        $row.ProcessPath | Should -Be 'C:\x\app.exe'
        $row.SeenCount   | Should -Be '1'
        $row.FirstSeen   | Should -Not -Be ''
        Remove-Item -Recurse -Force $tmp
    }
    It 'logs a warning when -IncludeUserName is unavailable' {
        Mock -ModuleName NetLogCollector Get-Process { throw 'requires elevated user rights' } `
             -ParameterFilter { $IncludeUserName }
        $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("nl_elev_" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $tmp 'run') -Force | Out-Null
        $stop = (Get-Date).ToUniversalTime().AddMinutes(5).ToString('o')
        @"
{ "runName":"r","schedule":{"stopUtc":"$stop"},"settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":60} }
"@ | Set-Content -LiteralPath (Join-Path $tmp 'run\run.json') -Encoding UTF8
        $log = Join-Path $tmp 'collector.log'
        Start-NetLogCollector -RunPath (Join-Path $tmp 'run') -HostName 'ME01' `
            -CachePath (Join-Path $tmp 'cache.csv') -MaxIterations 1 -LogPath $log `
            -ConnectionProvider { @() } `
            -ProcessMapProvider { @{} } -ServiceMapProvider { @{} }
        (Get-Content -LiteralPath $log -Raw) | Should -Match 'WARN Get-Process -IncludeUserName unavailable'
        Remove-Item -Recurse -Force $tmp
    }
}

Describe 'Read-NetLogConfig (exclusions)' {
    BeforeAll {
        $script:base = '{ "runName":"x", "schedule":{"stopUtc":"2999-01-01T00:00:00Z"}, "settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"maxCacheSizeMb":null EXCL } }'
    }
    It 'defaults to an empty array when absent' {
        $c = Read-NetLogConfig -Json ($base -replace 'EXCL', '')
        @($c.Exclusions).Count | Should -Be 0
    }
    It 'reads rules, lower-casing kind/direction and defaulting direction to both' {
        $json = $base -replace 'EXCL', ', "exclusions": [ { "kind":"Process", "pattern":"svchost (Dnscache)" }, { "kind":"host", "pattern":"10.0.0.0/8", "direction":"OUT", "note":"n" } ]'
        $c = Read-NetLogConfig -Json $json
        @($c.Exclusions).Count      | Should -Be 2
        $c.Exclusions[0].Kind       | Should -Be 'process'
        $c.Exclusions[0].Pattern    | Should -Be 'svchost (Dnscache)'
        $c.Exclusions[0].Direction  | Should -Be 'both'
        $c.Exclusions[1].Direction  | Should -Be 'out'
    }
    It 'reads the optional port with whitespace stripped and leaves it empty when absent or null' {
        $json = $base -replace 'EXCL', ', "exclusions": [ { "kind":"host", "pattern":"a", "port":" 80, 443 " }, { "kind":"host", "pattern":"b", "port":null }, { "kind":"host", "pattern":"c" } ]'
        $c = Read-NetLogConfig -Json $json
        $c.Exclusions[0].Port | Should -Be '80,443'
        $c.Exclusions[1].Port | Should -BeNullOrEmpty
        $c.Exclusions[2].Port | Should -BeNullOrEmpty
    }
    It 'treats null or a non-array as no rules' {
        @((Read-NetLogConfig -Json ($base -replace 'EXCL', ', "exclusions": null')).Exclusions).Count | Should -Be 0
        @((Read-NetLogConfig -Json ($base -replace 'EXCL', ', "exclusions": {}')).Exclusions).Count   | Should -Be 0
    }
    It 'keeps a single rule as an array' {
        $c = Read-NetLogConfig -Json ($base -replace 'EXCL', ', "exclusions": [ { "kind":"user", "pattern":"a" } ]')
        @($c.Exclusions).Count | Should -Be 1
        $c.Exclusions[0].Kind  | Should -Be 'user'
    }
    It 'defaults a whitespace-only direction to both' {
        $c = Read-NetLogConfig -Json ($base -replace 'EXCL', ', "exclusions": [ { "kind":"user", "pattern":"a", "direction":"  " } ]')
        $c.Exclusions[0].Direction | Should -Be 'both'
    }
}

Describe 'Test-NetLogCidrMatch' {
    It 'matches IPv4 inside and outside the prefix' {
        Test-NetLogCidrMatch -IpAddress '10.20.5.7'  -Cidr '10.20.0.0/16' | Should -BeTrue
        Test-NetLogCidrMatch -IpAddress '10.21.5.7'  -Cidr '10.20.0.0/16' | Should -BeFalse
        Test-NetLogCidrMatch -IpAddress '192.168.1.130' -Cidr '192.168.1.128/25' | Should -BeTrue   # non-octet prefix
        Test-NetLogCidrMatch -IpAddress '192.168.1.127' -Cidr '192.168.1.128/25' | Should -BeFalse
    }
    It 'matches IPv6 and tolerates a zone id on the address' {
        Test-NetLogCidrMatch -IpAddress 'fe80::1%12' -Cidr 'fe80::/10'     | Should -BeTrue
        Test-NetLogCidrMatch -IpAddress '2001:db8::5' -Cidr '2001:db8::/32' | Should -BeTrue
        Test-NetLogCidrMatch -IpAddress '2001:db9::5' -Cidr '2001:db8::/32' | Should -BeFalse
    }
    It 'returns false (never throws) on garbage or family mismatch' {
        Test-NetLogCidrMatch -IpAddress '10.0.0.1' -Cidr 'fe80::/10'   | Should -BeFalse
        Test-NetLogCidrMatch -IpAddress 'nope'     -Cidr '10.0.0.0/8'  | Should -BeFalse
        Test-NetLogCidrMatch -IpAddress '10.0.0.1' -Cidr '10.0.0.0/33' | Should -BeFalse
        Test-NetLogCidrMatch -IpAddress '10.0.0.1' -Cidr '10.0.0.0'    | Should -BeFalse
        Test-NetLogCidrMatch -IpAddress ''         -Cidr '10.0.0.0/8'  | Should -BeFalse
    }
}

Describe 'Test-NetLogExcluded' {
    BeforeAll {
        function script:Rule([string]$k, [string]$p, [string]$d = 'both', [string]$port = '') { [pscustomobject]@{ Kind = $k; Pattern = $p; Direction = $d; Port = $port } }
        $script:outRec = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '51000' `
            -Target '10.20.5.7' -TargetHost 'nas01.corp.local' -TargetPort '445' -Process 'svchost (Dnscache,NlaSvc)' `
            -Usernames 'CORP\alice;NT AUTHORITY\SYSTEM'
        $script:inRec  = New-NetLogRecord -Direction 'Incoming' -Source '10.9.9.9' -SourceHost 'scanner' -SourcePort '40000' `
            -Target '10.0.0.1' -TargetHost 'ME' -TargetPort '3389' -Process 'svchost' -Usernames ''
        $script:blankHost = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '1' `
            -Target 'fe80::1%12' -TargetHost '' -TargetPort '80' -Process 'app'
    }
    It 'returns false with no rules' {
        Test-NetLogExcluded -Record $outRec -Rules @() | Should -BeFalse
    }
    It 'port: gates any kind on the service port (TargetPort), honouring lists and ranges' {
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01*' 'both' '445')      | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01*' 'both' '446')      | Should -BeFalse
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01*' 'both' '80,445')   | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01*' 'both' '1-1000')   | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01*' 'both' '1024-65535') | Should -BeFalse
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'user' 'CORP\al*' 'out' '445')     | Should -BeTrue
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'process' 'svchost' 'in' '3389')   | Should -BeTrue
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'process' 'svchost' 'in' '40000')  | Should -BeFalse  # the client port is not the service port
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'svchost' 'in' '445')    | Should -BeFalse  # direction still gates
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'svchost' 'both' '')     | Should -BeTrue   # empty port = any port
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'process' 'svchost' 'in' '3000-4000')   | Should -BeTrue   # range against TargetPort ...
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'process' 'svchost' 'in' '40000-40001') | Should -BeFalse  # ... not SourcePort
    }
    It 'port: a record without a numeric service port never matches a port-qualified rule' {
        $noPort  = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '1' -Target '10.2.2.2' -TargetHost 'x' -TargetPort '' -Process 'app'
        $starPort = New-NetLogRecord -Direction 'Outgoing' -Source '10.0.0.1' -SourceHost 'ME' -SourcePort '1' -Target '10.2.2.2' -TargetHost 'x' -TargetPort '*' -Process 'app'
        Test-NetLogExcluded -Record $noPort   -Rules @(Rule 'process' 'app' 'both' '1-65535') | Should -BeFalse
        Test-NetLogExcluded -Record $starPort -Rules @(Rule 'process' 'app' 'both' '1-65535') | Should -BeFalse
        Test-NetLogExcluded -Record $noPort   -Rules @(Rule 'process' 'app')                  | Should -BeTrue   # no port on the rule: still matches
    }
    It 'process: matches the decorated name, the base name, wildcards, case-insensitively' {
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'svchost')                    | Should -BeTrue   # base name
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'SVCHOST (dnscache,nlasvc)') | Should -BeTrue   # decorated, case
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'svchost (Dnscache)')        | Should -BeFalse  # other decoration
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'svc*')                      | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'svc')                       | Should -BeFalse  # anchored
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'process' 'svchost')                   | Should -BeTrue   # undecorated row
    }
    It 'user: matches any entry of the semicolon list' {
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'user' 'nt authority\system') | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'user' 'CORP\al*')            | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'user' 'CORP\bob')            | Should -BeFalse
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'user' '*')                   | Should -BeFalse  # blank cell never matches
    }
    It 'host: peer is target for Outgoing and source for Incoming; name, IP text, or CIDR' {
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01*')        | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'nas01')         | Should -BeFalse  # anchored: no FQDN match
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' '10.20.5.7')     | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' '10.20.0.0/16')  | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' 'ME')            | Should -BeFalse  # never the local side
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'host' 'scanner')       | Should -BeTrue
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'host' '10.9.0.0/16')   | Should -BeTrue
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'host' 'ME')            | Should -BeFalse
    }
    It 'host: a blank hostname still matches by IP / CIDR, including IPv6 with a zone id' {
        Test-NetLogExcluded -Record $blankHost -Rules @(Rule 'host' 'fe80::/10') | Should -BeTrue
        Test-NetLogExcluded -Record $blankHost -Rules @(Rule 'host' '')          | Should -BeFalse  # empty pattern never matches
    }
    It 'direction gates the rule' {
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' '*' 'in')  | Should -BeFalse
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'host' '*' 'out') | Should -BeTrue
        Test-NetLogExcluded -Record $inRec  -Rules @(Rule 'host' '*' 'in')  | Should -BeTrue
    }
    It 'any rule hitting excludes (OR), unknown kinds are ignored, odd patterns never throw' {
        Test-NetLogExcluded -Record $outRec -Rules @((Rule 'user' 'nobody'), (Rule 'host' 'nas01*')) | Should -BeTrue
        Test-NetLogExcluded -Record $outRec -Rules @(Rule 'port' '445')          | Should -BeFalse
        { Test-NetLogExcluded -Record $outRec -Rules @(Rule 'process' 'app[1]') } | Should -Not -Throw
    }
}

Describe 'Select-NetLogIncluded' {
    It 'drops excluded records and always returns an array' {
        $a = New-NetLogRecord -Direction 'Outgoing' -Source 'h' -SourcePort '1' -Target 'r' -TargetPort '443' -Process 'keep'
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'h' -SourcePort '2' -Target 'r' -TargetPort '443' -Process 'drop'
        $rules = @([pscustomobject]@{ Kind = 'process'; Pattern = 'drop'; Direction = 'both' })
        $kept = Select-NetLogIncluded -Records @($a, $b) -Rules $rules
        ($kept -is [array]) | Should -BeTrue
        $kept.Count         | Should -Be 1
        $kept[0].Process    | Should -Be 'keep'
        $none = Select-NetLogIncluded -Records @($b) -Rules $rules
        ($none -is [array]) | Should -BeTrue
        $none.Count         | Should -Be 0
        $all = Select-NetLogIncluded -Records @($a, $b) -Rules @()
        ($all -is [array])  | Should -BeTrue
        $all.Count          | Should -Be 2
        $noneNoRules = Select-NetLogIncluded -Records @() -Rules @()
        ($noneNoRules -is [array]) | Should -BeTrue
    }
    It 'the zero-kept result binds to a Mandatory AllowEmptyCollection object[] parameter' {
        function script:Sink { param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records) $Records.Count }
        $b = New-NetLogRecord -Direction 'Outgoing' -Source 'h' -SourcePort '2' -Target 'r' -TargetPort '443' -Process 'drop'
        $rules = @([pscustomobject]@{ Kind = 'process'; Pattern = 'drop'; Direction = 'both' })
        $none = Select-NetLogIncluded -Records @($b) -Rules $rules
        { Sink -Records $none } | Should -Not -Throw
        Sink -Records $none | Should -Be 0
    }
}

Describe 'Get-NetLogExclusionError' {
    It 'returns empty for a valid rule and a reason otherwise' {
        function MkRule($k, $p, $d = 'both', $port = '') { [pscustomobject]@{ Kind = $k; Pattern = $p; Direction = $d; Port = $port } }
        Get-NetLogExclusionError -Rule (MkRule 'host' '10.0.0.0/8')      | Should -Be ''
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '80,443,1024-2048') | Should -Be ''
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '99999')  | Should -Match 'port'
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '80,')    | Should -Match 'port'
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '443-80') | Should -Match 'port'
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '0')      | Should -Match 'port'
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '+80')    | Should -Match 'port'   # same strictness as the C# validator
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'out' '80-+90') | Should -Match 'port'
        Get-NetLogExclusionError -Rule ([pscustomobject]@{ Kind = 'host'; Pattern = 'x'; Direction = 'out' }) | Should -Be ''   # no Port member at all
        Get-NetLogExclusionError -Rule (MkRule 'process' 'x' 'in')       | Should -Be ''
        Get-NetLogExclusionError -Rule (MkRule 'port' '443')             | Should -Match 'kind'
        Get-NetLogExclusionError -Rule (MkRule 'host' '')                | Should -Match 'pattern'
        Get-NetLogExclusionError -Rule (MkRule 'host' '10.0.0.0/33')     | Should -Match 'CIDR'
        Get-NetLogExclusionError -Rule (MkRule 'host' 'x' 'sideways')    | Should -Match 'direction'
        Get-NetLogExclusionError -Rule (MkRule 'process' 'a[1]')         | Should -Match '\['
    }
}

Describe 'ConvertTo-NetLogExclusionKey (live change detection)' {
    It 'changes when only a port changes, so a live port edit is picked up' {
        $m = Get-Module NetLogCollector
        $a = & $m { param($r) ConvertTo-NetLogExclusionKey -Rules $r } @([pscustomobject]@{ Kind='host'; Pattern='x'; Direction='out'; Port='445' })
        $b = & $m { param($r) ConvertTo-NetLogExclusionKey -Rules $r } @([pscustomobject]@{ Kind='host'; Pattern='x'; Direction='out'; Port='446' })
        $c = & $m { param($r) ConvertTo-NetLogExclusionKey -Rules $r } @([pscustomobject]@{ Kind='host'; Pattern='x'; Direction='out' })
        $a | Should -Not -Be $b
        $a | Should -Not -Be $c
    }
}

Describe 'Status heartbeat (excludedCount)' {
    It 'writes excludedCount (default 0)' {
        $now = [datetime]::Parse('2026-06-04T01:02:03.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        (New-NetLogStatus -LastRunUtc $now -ProcessId 1 -TotalConnections 1 -TotalPolls 1).excludedCount                    | Should -Be 0
        (New-NetLogStatus -LastRunUtc $now -ProcessId 1 -TotalConnections 1 -TotalPolls 1 -ExcludedCount 42).excludedCount  | Should -Be 42
    }
}

Describe 'Invoke-NetLogArchiveCycle (exclusion purge)' {
    It 'removes already-archived rows that match a rule from both archive files and reports the count' {
        $now = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $keep = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target '10.1.1.1' -TargetPort '443' -Process 'app'
        $drop = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51002' -Target '10.2.2.2' -TargetHost 'nas01' -TargetPort '445' -Process 'System'
        foreach ($r in $keep, $drop) { Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force }
        $cachePath   = Join-Path $TestDrive 'purge\cache.csv'
        $archivePath = Join-Path $TestDrive 'purge\hosts\S\connections.csv'
        $runPath     = Join-Path $TestDrive 'purge\hosts\S\run1.csv'
        $statusPath  = Join-Path $TestDrive 'purge\hosts\S\_status.json'
        Export-NetLogCache -Records @($keep, $drop) -Path $cachePath

        # Cycle 1: no rules - both rows land on the share.
        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -RunArchivePath $runPath -StatusPath $statusPath `
                                  -Level 'Full' -ProcessId 1 -TotalPolls 1 -NowUtc $now | Out-Null
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 2
        (Import-NetLogArchive -Path $runPath).Count     | Should -Be 2

        # Cycle 2: a host rule appears - the matching row is purged from both files.
        $rules = @([pscustomobject]@{ Kind = 'host'; Pattern = 'nas01'; Direction = 'out' })
        $res = Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -RunArchivePath $runPath -StatusPath $statusPath `
                                         -Level 'Full' -ProcessId 1 -TotalPolls 2 -NowUtc $now -Exclusions $rules -ExcludedCount 7
        @($res).Count | Should -Be 1
        $res[0].Process | Should -Be 'app'
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 1
        (Import-NetLogArchive -Path $runPath).Count     | Should -Be 1
        $st = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $st.excludedCount    | Should -Be 7
        $st.totalConnections | Should -Be 1
    }

    It 'purges matching rows from both archive files at the Merged level too' {
        $now = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $keep = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51001' -Target '10.1.1.1' -TargetPort '443' -Process 'app'
        $drop = New-NetLogRecord -Direction 'Outgoing' -Source 'H' -SourcePort '51002' -Target '10.2.2.2' -TargetHost 'nas01' -TargetPort '445' -Process 'System'
        foreach ($r in $keep, $drop) { Add-Member -InputObject $r -NotePropertyName LastSeen -NotePropertyValue $now.ToString('o') -Force }
        $cachePath   = Join-Path $TestDrive 'mpurge\cache.csv'
        $archivePath = Join-Path $TestDrive 'mpurge\hosts\S\connections.csv'
        $runPath     = Join-Path $TestDrive 'mpurge\hosts\S\run1.csv'
        $statusPath  = Join-Path $TestDrive 'mpurge\hosts\S\_status.json'
        Export-NetLogCache -Records @($keep, $drop) -Path $cachePath

        Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -RunArchivePath $runPath -StatusPath $statusPath `
                                  -Level 'Merged' -ProcessId 1 -TotalPolls 1 -NowUtc $now | Out-Null
        (Import-NetLogArchive -Path $archivePath).Count | Should -Be 2
        (Import-NetLogArchive -Path $runPath).Count     | Should -Be 2

        $rules = @([pscustomobject]@{ Kind = 'host'; Pattern = 'nas01'; Direction = 'out' })
        $res = Invoke-NetLogArchiveCycle -CachePath $cachePath -ArchivePath $archivePath -RunArchivePath $runPath -StatusPath $statusPath `
                                         -Level 'Merged' -ProcessId 1 -TotalPolls 2 -NowUtc $now -Exclusions $rules
        @($res).Count   | Should -Be 1
        $res[0].Process | Should -Be 'app'
        $arch = Import-NetLogArchive -Path $archivePath
        $runr = Import-NetLogArchive -Path $runPath
        @($arch).Count | Should -Be 1
        @($runr).Count | Should -Be 1
        @($arch | Where-Object { $_.Process -eq 'System' }).Count | Should -Be 0
        @($runr | Where-Object { $_.Process -eq 'System' }).Count | Should -Be 0
    }
}

Describe 'Start-NetLogCollector (exclusions)' {
    BeforeAll {
        $script:xClock = [datetime]::Parse('2026-06-04T00:00:00.0000000Z', $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
        $script:xConns = @(
            [pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=51001; RemoteAddress='10.0.0.5';  RemotePort=443; OwningProcess=1234 }
            [pscustomobject]@{ State='Established'; LocalAddress='10.0.0.1'; LocalPort=51002; RemoteAddress='10.20.5.7'; RemotePort=445; OwningProcess=4 }
        )
        $script:xMap = @{ 4 = 'System'; 1234 = 'chrome' }
        function script:XJson([string]$exclusions) {
            '{ "runName":"x","schedule":{"startUtc":"2026-06-01T00:00:00Z","stopUtc":"2999-01-01T00:00:00Z"},' +
            '"settings":{"archiveLevel":"Full","pollIntervalSeconds":0,"archiveIntervalMinutes":0,"cacheLifetimeDays":7,"maxCacheSizeMb":null,"resolveHostnames":false' +
            $(if ($exclusions) { ',"exclusions":' + $exclusions } else { '' }) + '} }'
        }
    }

    It 'drops matching samples before the cache and reports excludedCount in the heartbeat' {
        $run = Join-Path $TestDrive 'xrun1'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        Set-Content -Path (Join-Path $run 'run.json') -Encoding UTF8 -Value (XJson '[{"kind":"host","pattern":"10.20.0.0/16","direction":"out"}]')
        $log = Join-Path $TestDrive 'xrun1-local\collector.log'
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath (Join-Path $TestDrive 'xrun1-local\cache.csv') `
            -ConnectionProvider { $xConns } -ProcessMapProvider { $xMap } -Clock { $xClock } -MaxIterations 2 -LogPath $log
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count    | Should -Be 1
        $rows[0].Process  | Should -Be 'chrome'
        $st = Get-Content -LiteralPath (Join-Path $run 'hosts\S\_status.json') -Raw | ConvertFrom-Json
        $st.excludedCount | Should -Be 2       # one dropped sample per iteration
        (Get-Content $log -Raw) | Should -Match 'exclusions: 1 rules'
        (Get-Content $log -Raw) | Should -Match 'excluded=2'
    }

    It 'applies every rule when more than one is configured (regression: nested rule array)' {
        # Bug: @(Select-NetLogValidExclusion ...) nested the rule array; with one rule member
        # enumeration still stringified to the right values, with two or more nothing matched.
        $run = Join-Path $TestDrive 'xrun1b'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        Set-Content -Path (Join-Path $run 'run.json') -Encoding UTF8 -Value (XJson '[{"kind":"process","pattern":"chrome","direction":"out"},{"kind":"process","pattern":"System","direction":"both"}]')
        $log = Join-Path $TestDrive 'xrun1b-local\collector.log'
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath (Join-Path $TestDrive 'xrun1b-local\cache.csv') `
            -ConnectionProvider { $xConns } -ProcessMapProvider { $xMap } -Clock { $xClock } -MaxIterations 2 -LogPath $log
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count | Should -Be 0
        $st = Get-Content -LiteralPath (Join-Path $run 'hosts\S\_status.json') -Raw | ConvertFrom-Json
        $st.excludedCount | Should -Be 4       # two dropped samples per iteration
        (Get-Content $log -Raw) | Should -Match 'exclusions: 2 rules'
    }

    It 'a port-qualified rule drops only connections on that service port' {
        $run = Join-Path $TestDrive 'xrun1c'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        # System on 445 is dropped; chrome on 443 survives because its rule names port 444.
        Set-Content -Path (Join-Path $run 'run.json') -Encoding UTF8 -Value (XJson '[{"kind":"process","pattern":"System","direction":"out","port":"139,445"},{"kind":"process","pattern":"chrome","port":"444"}]')
        $log = Join-Path $TestDrive 'xrun1c-local\collector.log'
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath (Join-Path $TestDrive 'xrun1c-local\cache.csv') `
            -ConnectionProvider { $xConns } -ProcessMapProvider { $xMap } -Clock { $xClock } -MaxIterations 2 -LogPath $log
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count   | Should -Be 1
        $rows[0].Process | Should -Be 'chrome'
        $st = Get-Content -LiteralPath (Join-Path $run 'hosts\S\_status.json') -Raw | ConvertFrom-Json
        $st.excludedCount | Should -Be 2
    }

    It 'a rule added mid-run purges the archive and the local cache on the next cycle' {
        $run = Join-Path $TestDrive 'xrun2'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        $jsonPath = Join-Path $run 'run.json'
        Set-Content -Path $jsonPath -Encoding UTF8 -Value (XJson '')
        $cachePath = Join-Path $TestDrive 'xrun2-local\cache.csv'
        $script:n = 0
        # Iteration 1 samples normally; iteration 2 adds a process rule before the live re-read of iteration 3.
        $provider = { $script:n++; if ($script:n -eq 2) { Set-Content -Path $jsonPath -Encoding UTF8 -Value (XJson '[{"kind":"process","pattern":"System"}]') }; $xConns }
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath $cachePath `
            -ConnectionProvider $provider -ProcessMapProvider { $xMap } -Clock { $xClock } -MaxIterations 3
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count   | Should -Be 1
        $rows[0].Process | Should -Be 'chrome'
        # Import-NetLogCache returns a comma-wrapped array: assign first, then enumerate (piping the
        # call directly would hand Where-Object the whole array as one object and pass vacuously).
        $cacheRows = Import-NetLogCache -Path $cachePath
        @($cacheRows).Count | Should -Be 1
        @($cacheRows | Where-Object { $_.Process -eq 'System' }).Count | Should -Be 0
    }

    It 'removing a rule lets a previously purged connection be recorded again (seen-set forgets purged keys)' {
        $run = Join-Path $TestDrive 'xrun3'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        $jsonPath = Join-Path $run 'run.json'
        Set-Content -Path $jsonPath -Encoding UTF8 -Value (XJson '')
        $script:n = 0
        # it 1: both rows recorded. it 2: rule appears -> System purged from cache/archive AND its key leaves $seen.
        # it 3: rule removed again (takes effect at it 4's re-read). it 4: System must be recorded once more.
        $provider = {
            $script:n++
            if ($script:n -eq 1) { Set-Content -Path $jsonPath -Encoding UTF8 -Value (XJson '[{"kind":"process","pattern":"System"}]') }
            if ($script:n -eq 3) { Set-Content -Path $jsonPath -Encoding UTF8 -Value (XJson '') }
            $xConns
        }
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath (Join-Path $TestDrive 'xrun3-local\cache.csv') `
            -ConnectionProvider $provider -ProcessMapProvider { $xMap } -Clock { $xClock } -MaxIterations 4
        # Assign first: @(Import-NetLogArchive ...) would wrap the comma-returned array and count 1.
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count | Should -Be 2
    }

    It 'a connection that expired from the cache before its rule was added is recorded again after the rule is removed' {
        $run = Join-Path $TestDrive 'xrun5'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        $jsonPath = Join-Path $run 'run.json'
        $life1 = { param($excl) (XJson $excl) -replace '"cacheLifetimeDays":7', '"cacheLifetimeDays":1' }
        Set-Content -Path $jsonPath -Encoding UTF8 -Value (& $life1 '')
        $script:n = 0
        $script:x5Clock = $xClock
        # The clock is read at the top of each iteration; the provider advances it by 2 days at the end
        # of its call, so iteration i runs at T0 + 2(i-1) days. Lifetime is 1 day; archive every iteration.
        # it 1 (T0):    no rules. Both rows sampled -> cache {chrome,System}, seen {chrome,System}, archive 2.
        #               (provider returns both)
        # it 2 (T0+2d): no rules. Provider returns chrome only (System idle) and writes the System rule.
        #               Expiry: System LastSeen T0 < cutoff T0+1d -> dropped from the cache, but its key
        #               stays in seen. Archive still 2 (cumulative).
        # it 3 (T0+4d): re-read picks up the rule. Provider returns both -> System dropped (excluded=1)
        #               and writes the empty rule set. Cache purge: System not in the cache, so the
        #               helper cannot remove its key from seen. Archive purge: System row deleted -> 1.
        # it 4 (T0+6d): re-read: rules removed. Without the seen-set rebuild, System's key is still in
        #               seen and absent from the cache, so Update-NetLogCache silently drops it -> archive 1.
        #               With the rebuild (seen = keys of cache = {chrome}), System is recorded -> archive 2.
        $provider = {
            $script:n++
            $out = $xConns
            if ($script:n -eq 2) {
                Set-Content -Path $jsonPath -Encoding UTF8 -Value (& $life1 '[{"kind":"process","pattern":"System"}]')
                $out = @($xConns | Where-Object { $_.OwningProcess -ne 4 })
            }
            if ($script:n -eq 3) { Set-Content -Path $jsonPath -Encoding UTF8 -Value (& $life1 '') }
            $script:x5Clock = $script:x5Clock.AddDays(2)
            $out
        }
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath (Join-Path $TestDrive 'xrun5-local\cache.csv') `
            -ConnectionProvider $provider -ProcessMapProvider { $xMap } -Clock { $script:x5Clock } -MaxIterations 4
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count | Should -Be 2
        @($rows | Where-Object { $_.Process -eq 'System' }).Count | Should -Be 1
        $st = Get-Content -LiteralPath (Join-Path $run 'hosts\S\_status.json') -Raw | ConvertFrom-Json
        $st.excludedCount | Should -BeGreaterThan 0
    }

    It 'skips a malformed rule with one warning and keeps collecting with the valid ones' {
        $run = Join-Path $TestDrive 'xrun4'; New-Item -ItemType Directory -Path $run -Force | Out-Null
        Set-Content -Path (Join-Path $run 'run.json') -Encoding UTF8 -Value (XJson '[{"kind":"host","pattern":"10.0.0.0/99"},{"kind":"process","pattern":"chrome"}]')
        $log = Join-Path $TestDrive 'xrun4-local\collector.log'
        Start-NetLogCollector -RunPath $run -HostName 'S' -CachePath (Join-Path $TestDrive 'xrun4-local\cache.csv') `
            -ConnectionProvider { $xConns } -ProcessMapProvider { $xMap } -Clock { $xClock } -MaxIterations 3 -LogPath $log
        $rows = Import-NetLogArchive -Path (Join-Path $run 'hosts\S\connections.csv')
        @($rows).Count   | Should -Be 1
        $rows[0].Process | Should -Be 'System'
        $text = Get-Content $log -Raw
        ([regex]::Matches($text, 'WARN exclusion rule skipped')).Count | Should -Be 1   # once, not once per cycle
        $text | Should -Match 'exclusions: 1 rules'
    }
}
