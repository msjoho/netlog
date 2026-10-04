<#
.SYNOPSIS
  Diagnoses "collector cannot find run.json" by reproducing the EXACT context the collector runs
  in: a Scheduled Task on the target, logged on as the service account (stored password ->
  network identity). The collector runs fine LOCALLY but every UNC access fails with
  "path does not exist" — the signature of a task logon session that has NO network credentials.

  This does NOT assume a permissions/DFS cause. It gathers the discriminating evidence:
    * the LogonType the task ACTUALLY registered as (Password vs S4U — S4U has no network creds),
    * whether the task session holds a Kerberos ticket (klist),
    * the WindowsIdentity auth type / impersonation level inside the task,
    * the DisableDomainCreds GPO state (blocks stored-password network auth),
    * Test-Path at several path levels (run path, parent, share root) so we see WHERE it breaks.

  No part of the product is changed. The probe task is transient and is removed afterwards.

.EXAMPLE
  pwsh -File .\deploy\Test-NetLogShareAccess.ps1 `
       -Target su1955161 -Account AXUSR\svc-arp-np-refresh `
       -RunPath \\tdfs\InfraOps\Sources\netlog\newtest1\test3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Target,
    [Parameter(Mandatory)][string]$Account,
    [Parameter(Mandatory)][string]$RunPath,
    # Optional: the resolved DFS target (the real \\server\share\... path the DFS link points to).
    # When supplied, the probe walks it level-by-level too, so we can tell whether the task can
    # reach the underlying file server directly at all.
    [string]$DirectPath
)

# Build the chain of ancestor UNC paths from the share root down to the full path, so the probe
# can report the EXACT level (and the EXACT error) where traversal stops.
function Get-UncAncestors {
    param([string]$Path)
    if (-not $Path) { return @() }
    $cur = $Path.TrimEnd('\')
    $list = @()
    while ($cur -match '^\\\\[^\\]+\\[^\\]+') {   # keep down to \\server\share
        $list = , $cur + $list
        $parent = Split-Path $cur -Parent
        if (-not $parent -or $parent -eq $cur) { break }
        $cur = $parent
    }
    $list
}

$levels = @(Get-UncAncestors -Path $RunPath)
if ($DirectPath) { $levels += @('--- direct target ---') + @(Get-UncAncestors -Path $DirectPath) }
# Emit as a PowerShell array literal we can bake into the probe script (single-quote escaped).
$pathsLiteral = '@(' + (($levels | ForEach-Object { "'" + ($_ -replace "'", "''") + "'" }) -join ',') + ')'

# Host names for the targeted Kerberos / DNS / TCP checks. The namespace server is the host part
# of the run path; the file server is the host part of the direct target path.
$nsServer = $null; $directServer = $null; $directShare = $null
if ($RunPath -match '^\\\\([^\\]+)') { $nsServer = $Matches[1] }
if ($DirectPath -and $DirectPath -match '^\\\\([^\\]+)\\([^\\]+)') {
    $directServer = $Matches[1]; $directShare = $Matches[2]
}

# Extra probe steps that need the direct file server name. Run AFTER the path walk so they don't
# prime caches/tickets and contaminate it.
#  * klist get cifs/<server>  — the last run showed NO ticket for the file server in the task's
#    cache (only namespace-server tickets), i.e. the failure happens BEFORE/WITHOUT Kerberos.
#    Forcing a TGS request tells us the KDC's answer directly: success -> Kerberos path is fine
#    and the problem is pure SMB; a KDC error code -> Kerberos realm routing is the problem.
#  * net use — the raw Win32 "System error NN" verdict for a plain session setup.
$extraProbe = ''
if ($directServer) {
    $extraProbe = @"
`$lines += "----- klist get cifs/$directServer (force a Kerberos ticket for the file server) -----"
try { `$lines += (klist get cifs/$directServer 2>&1 | Out-String).Trim() } catch { `$lines += "klist get error: `$(`$_.Exception.Message)" }
`$lines += "----- net use \\$directServer\$directShare (raw Win32 verdict) -----"
`$lines += (net use "\\$directServer\$directShare" 2>&1 | Out-String).Trim()
net use "\\$directServer\$directShare" /delete 2>&1 | Out-Null
"@
}

$pw    = Read-Host "Password for $Account" -AsSecureString
$cred  = [System.Management.Automation.PSCredential]::new($Account, $pw)
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw))

Write-Host "Connecting to $Target as $Account ..." -ForegroundColor Cyan
$session = New-PSSession -ComputerName $Target -Credential $cred -ErrorAction Stop
try {
    $result = Invoke-Command -Session $session -ScriptBlock {
        param($runPath, $account, $plain, $pathsLiteral, $extraProbe)

        $probe = Join-Path $env:TEMP 'netlog-probe.ps1'
        $out   = Join-Path $env:TEMP 'netlog-probe.out'
        Remove-Item $out -ErrorAction SilentlyContinue

        # The probe body runs INSIDE the task (as the service account, the collector's context).
        # It walks every path level with raw .NET I/O — NOT the PowerShell provider — because
        # Get-Item/Test-Path collapse every UNC failure (logon-failure, bad-netpath, access-denied)
        # into the same "does not exist" ItemNotFoundException, hiding the real cause. .NET throws
        # the true exception type + HRESULT (Win32 code), which is the whole diagnosis.
        $body = @"
`$id = [Security.Principal.WindowsIdentity]::GetCurrent()
`$lines = @()
`$lines += "identity      = `$(`$id.Name)"
`$lines += "interactive   = `$([Environment]::UserInteractive)"
`$lines += "authType      = `$(`$id.AuthenticationType)"
`$lines += "impersonation = `$(`$id.ImpersonationLevel)"
`$lines += "isSystem      = `$(`$id.IsSystem)"
`$lines += "os            = `$([Environment]::OSVersion.VersionString)"
`$lines += "----- klist BEFORE access -----"
try { `$lines += (klist 2>&1 | Out-String).Trim() } catch { `$lines += "klist error: `$(`$_.Exception.Message)" }
`$lines += "----- path walk (RAW .NET error + HRESULT per level) -----"
`$paths = $pathsLiteral
foreach (`$p in `$paths) {
    if (`$p -like '--- *') { `$lines += `$p; continue }
    try {
        `$entries = [System.IO.Directory]::GetFileSystemEntries(`$p)
        `$lines += "OK     `$p  (`$(`$entries.Count) entries)"
    } catch {
        `$e = `$_.Exception
        if (`$e.InnerException) { `$e = `$e.InnerException }
        `$hr = '0x{0:X8}' -f (`$e.HResult -band 0xFFFFFFFF)
        `$lines += "FAIL   `$p :: [`$(`$e.GetType().Name)] HRESULT=`$hr :: `$(`$e.Message)"
    }
}
`$lines += "----- klist AFTER access (did it get a service ticket for the file server's cifs SPN?) -----"
try { `$lines += (klist 2>&1 | Out-String).Trim() } catch { `$lines += "klist error: `$(`$_.Exception.Message)" }
$extraProbe
`$lines += "----- read run.json -----"
try {
    `$j = Get-Content -LiteralPath '$runPath\run.json' -Raw -ErrorAction Stop
    `$lines += "READ OK bytes=`$(`$j.Length)"
} catch {
    `$lines += "READ FAIL :: [`$(`$_.Exception.GetType().Name)] `$(`$_.Exception.Message)"
}
`$lines -join [Environment]::NewLine | Set-Content -LiteralPath '$out' -Encoding UTF8
"@
        Set-Content -LiteralPath $probe -Value $body -Encoding UTF8

        $action    = New-ScheduledTaskAction -Execute '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe' `
                        -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $probe)
        $principal = New-ScheduledTaskPrincipal -UserId $account -LogonType Password -RunLevel Highest
        $task      = New-ScheduledTask -Action $action -Principal $principal
        Register-ScheduledTask -TaskName 'NetLogProbe' -InputObject $task -User $account -Password $plain -Force | Out-Null

        # What did it ACTUALLY register as? (Password vs S4U is the whole ballgame.)
        $actualLogon = (Get-ScheduledTask -TaskName 'NetLogProbe').Principal.LogonType

        # GPO that blocks stored-password network auth for tasks (interactive logon is unaffected).
        $disableDomainCreds = (Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Lsa' `
                                -Name DisableDomainCreds -ErrorAction SilentlyContinue).DisableDomainCreds

        Start-ScheduledTask -TaskName 'NetLogProbe'
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-Path $out) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
        $probeOut = if (Test-Path $out) { Get-Content -LiteralPath $out -Raw } else { 'NO RESULT (probe task did not write within 30s — check "log on as a batch job" right)' }

        $info = (Get-ScheduledTaskInfo -TaskName 'NetLogProbe' -ErrorAction SilentlyContinue)
        Unregister-ScheduledTask -TaskName 'NetLogProbe' -Confirm:$false -ErrorAction SilentlyContinue
        Remove-Item $probe -ErrorAction SilentlyContinue

        [pscustomobject]@{
            RegisteredLogonType = $actualLogon
            DisableDomainCreds  = $disableDomainCreds
            LastTaskResult      = if ($info) { '0x{0:X8}' -f $info.LastTaskResult } else { 'n/a' }
            ProbeOutput         = $probeOut.Trim()
        }
    } -ArgumentList $RunPath, $Account, $plain, $pathsLiteral, $extraProbe

    # Machine-wide context on the target (not logon-session-specific, so a plain WinRM call is
    # fine). This is where the working-vs-broken comparison lives: if the service account is
    # interactively logged on with a fresh `dir \\tdfs\...`, Get-SmbConnection captures that
    # WORKING connection's server/dialect/signing next to the task's failed attempt.
    $ctx = Invoke-Command -Session $session -ScriptBlock {
        param($nsServer, $directServer, $directShare)
        $lines = @()
        foreach ($n in @($nsServer, $directServer) | Where-Object { $_ }) {
            try {
                $r = Resolve-DnsName -Name $n -ErrorAction Stop | ForEach-Object {
                    if ($_.Type -eq 'CNAME') { "$($_.Name) CNAME -> $($_.NameHost)" }
                    elseif ($_.IPAddress)    { "$($_.Name) $($_.Type) $($_.IPAddress)" }
                    else                     { "$($_.Name) $($_.Type)" }
                }
                $lines += "DNS  $n :: " + ($r -join ' | ')
            } catch { $lines += "DNS  $n :: FAIL $($_.Exception.Message)" }
        }
        if ($directServer) {
            try {
                $t = Test-NetConnection -ComputerName $directServer -Port 445 -WarningAction SilentlyContinue -ErrorAction Stop
                $lines += "TCP  445->$directServer :: TcpTestSucceeded=$($t.TcpTestSucceeded) via $($t.RemoteAddress)"
            } catch { $lines += "TCP  445->$directServer :: $($_.Exception.Message)" }
        }
        try {
            $c = Get-SmbClientConfiguration -ErrorAction Stop
            $lines += "SMBc RequireSecuritySignature=$($c.RequireSecuritySignature) EnableSecuritySignature=$($c.EnableSecuritySignature) EnableInsecureGuestLogons=$($c.EnableInsecureGuestLogons)"
        } catch { $lines += "SMBc config :: $($_.Exception.Message)" }
        $smb1 = Get-Service mrxsmb10 -ErrorAction SilentlyContinue
        $lines += "SMB1 client driver (mrxsmb10) :: " + $(if ($smb1) { $smb1.Status } else { '(absent)' })
        try {
            $hp = Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\NetworkProvider\HardenedPaths' -ErrorAction Stop
            $any = $false
            foreach ($p in $hp.PSObject.Properties) {
                if ($p.Name -like '\\*') { $lines += "HardenedPath $($p.Name) = $($p.Value)"; $any = $true }
            }
            if (-not $any) { $lines += "HardenedPaths :: (key present, no path entries)" }
        } catch { $lines += "HardenedPaths :: (none)" }
        $lines += "--- DFS referral cache (which target does this box actually use?) ---"
        try { $lines += (dfsutil /pktinfo 2>&1 | Out-String).Trim() } catch { $lines += "dfsutil not available: $($_.Exception.Message)" }
        $lines += "--- Get-SmbConnection (live SMB sessions; an interactive one is the working control) ---"
        try {
            $conns = @(Get-SmbConnection -ErrorAction Stop)
            if (-not $conns) { $lines += "(no live SMB connections)" }
            foreach ($s in $conns) {
                $lines += ("SMB \\{0}\{1} user={2} dialect={3} signed={4} encrypted={5} opens={6}" -f `
                    $s.ServerName, $s.ShareName, $s.UserName, $s.Dialect, $s.Signed, $s.Encrypted, $s.NumOpens)
            }
        } catch { $lines += "Get-SmbConnection :: $($_.Exception.Message)" }
        $lines
    } -ArgumentList $nsServer, $directServer, $directShare

    # The SMB client driver logs the precise NTSTATUS behind the bare Win32 58 ("server cannot
    # perform the requested operation"). Read the SMBClient logs on the target (machine-wide;
    # the probe task's connection attempt is recorded here regardless of its logon session).
    $smbEvents = Invoke-Command -Session $session -ScriptBlock {
        $since = (Get-Date).AddMinutes(-5)
        $rows = @()
        foreach ($log in 'Microsoft-Windows-SMBClient/Connectivity',
                         'Microsoft-Windows-SMBClient/Security',
                         'Microsoft-Windows-SMBClient/Operational') {
            try {
                $ev = Get-WinEvent -FilterHashtable @{ LogName = $log; StartTime = $since } -MaxEvents 25 -ErrorAction Stop
                foreach ($e in $ev) {
                    $rows += [pscustomobject]@{
                        Log  = ($log -split '/')[-1]
                        Time = $e.TimeCreated.ToString('HH:mm:ss')
                        Id   = $e.Id
                        Msg  = (($e.Message -replace '\s+', ' ')).Trim()
                    }
                }
            } catch {
                $rows += [pscustomobject]@{ Log = ($log -split '/')[-1]; Time = ''; Id = 0; Msg = "(could not read: $($_.Exception.Message))" }
            }
        }
        $rows
    }

    # LogonType comes back as the CIM enum value (sometimes numeric). Map to a name.
    $logonMap = @{ '0' = 'None'; '1' = 'Password'; '2' = 'S4U'; '3' = 'Interactive'; '4' = 'Group'; '5' = 'ServiceAccount'; '6' = 'InteractiveOrPassword' }
    $logonRaw = "$($result.RegisteredLogonType)"
    $logon = if ($logonMap.ContainsKey($logonRaw)) { $logonMap[$logonRaw] } else { $logonRaw }
    $hasNetCreds = $logon -in @('Password', 'InteractiveOrPassword')
    Write-Host ""
    Write-Host "=== Task registration (the discriminator) ===" -ForegroundColor Cyan
    Write-Host ("  Registered LogonType : {0} ({1})" -f $logon, $logonRaw) `
        -ForegroundColor $(if ($hasNetCreds) { 'Green' } else { 'Red' })
    if (-not $hasNetCreds) {
        Write-Host "    -> NOT Password. An S4U/Interactive task has NO network credentials; every UNC fails." -ForegroundColor Red
    }
    Write-Host ("  DisableDomainCreds   : {0}" -f $(if ($null -eq $result.DisableDomainCreds) { '(unset / 0)' } else { $result.DisableDomainCreds })) `
        -ForegroundColor $(if ($result.DisableDomainCreds) { 'Red' } else { 'Green' })
    if ($result.DisableDomainCreds) {
        Write-Host "    -> GPO blocks stored-password network auth. Tasks can't reach UNC; interactive logon can." -ForegroundColor Red
    }
    Write-Host ("  Probe LastTaskResult : {0}" -f $result.LastTaskResult)
    Write-Host ""
    Write-Host "=== Inside the task (service account, network identity) ===" -ForegroundColor Cyan
    Write-Host $result.ProbeOutput
    Write-Host ""
    Write-Host "=== Machine-wide context on target (DNS, DFS cache, SMB config, live connections) ===" -ForegroundColor Cyan
    $ctx | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "=== SMBClient driver events on target (the NTSTATUS behind Win32 58) ===" -ForegroundColor Cyan
    if ($smbEvents) {
        $smbEvents | ForEach-Object {
            Write-Host ("  [{0,-12}] {1} Id={2} :: {3}" -f $_.Log, $_.Time, $_.Id, $_.Msg)
        }
    } else {
        Write-Host "  (no SMBClient events in the last 5 min — logs may be disabled or unreadable)"
    }
    Write-Host ""
    Write-Host "How to read this:" -ForegroundColor Yellow
    Write-Host "  Known so far: the task session reaches the NAMESPACE server fine (cifs/<ns> ticket, root"
    Write-Host "  enumerates) but dies with Win32 58 on the FILE SERVER — with NO Kerberos ticket for it,"
    Write-Host "  i.e. the failure happens at SMB connect/negotiate/session-setup, before or without Kerberos."
    Write-Host "  * 'klist get cifs/<fileserver>' inside the task:"
    Write-Host "      - ticket issued  -> Kerberos routing is fine; the fault is pure SMB. The SMBClient"
    Write-Host "        Connectivity/Security events carry the real NTSTATUS -> that names the fix"
    Write-Host "        (signing mismatch / encryption or dialect mismatch / middlebox mangling the response)."
    Write-Host "      - KDC error      -> Kerberos can't route to the file server's realm from this session"
    Write-Host "        (SPN missing on a CNAME alias, etype restriction, ...). The error code names it."
    Write-Host "  * DNS section: a CNAME chain for the file server matters — Kerberos SPNs bind to the"
    Write-Host "    canonical name; an alias without a registered SPN forces NTLM fallback."
    Write-Host "  * Get-SmbConnection: if the interactive control connection to the SAME server is visible,"
    Write-Host "    its dialect/signed/encrypted line is what a WORKING session negotiates — compare."
    Write-Host "  * dfsutil /pktinfo: confirms which folder TARGET this box's referral actually selects"
    Write-Host "    (if there are several, interactive and task may be hitting different servers)."
}
finally {
    Remove-PSSession -Session $session -ErrorAction SilentlyContinue
}
