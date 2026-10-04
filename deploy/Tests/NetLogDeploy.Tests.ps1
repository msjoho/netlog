#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }
# Pester 5 syntax (top-level BeforeAll). Run under PowerShell 7 (pwsh), not Windows PowerShell 5.1,
# whose built-in Pester 3.4.0 rejects a top-level BeforeAll.

BeforeAll {
    Import-Module "$PSScriptRoot\..\NetLogDeploy\NetLogDeploy.psd1" -Force
}

Describe 'Get-NetLogTaskName' {
    It 'prefixes and sanitizes the run name' {
        Get-NetLogTaskName -RunName 'weekly-audit' | Should -Be 'NetLogCollector_weekly-audit'
    }
    It 'replaces invalid characters with underscore' {
        Get-NetLogTaskName -RunName 'a\b/c:d' | Should -Be 'NetLogCollector_a_b_c_d'
    }
}

Describe 'Get-NetLogCollectorAction' {
    It 'builds a Windows PowerShell action that runs the collector entry against the run path' {
        $a = Get-NetLogCollectorAction -InstallDir 'C:\ProgramData\NetworkActivityLogger\bin' -RunPath '\\share\runs\audit'
        $a.Execute  | Should -Be '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe'
        $a.Argument | Should -BeLike '*-File "C:\ProgramData\NetworkActivityLogger\bin\Start-NetLogCollector.ps1"*'
        $a.Argument | Should -BeLike '*-RunPath "\\share\runs\audit"*'
        $a.Argument | Should -BeLike '*-NoProfile*'
    }
}

Describe 'Test-NetLogDeployInput' {
    It 'passes for valid input' {
        { Test-NetLogDeployInput -Target 'S1' -RunPath '\\share\runs\audit' -Account 'DOMAIN\svc' } | Should -Not -Throw
    }
    It 'throws on empty target' {
        { Test-NetLogDeployInput -Target '' -RunPath '\\share\runs\audit' -Account 'DOMAIN\svc' } | Should -Throw
    }
    It 'throws on empty account' {
        { Test-NetLogDeployInput -Target 'S1' -RunPath '\\share\runs\audit' -Account '' } | Should -Throw
    }
    It 'throws on empty run path' {
        { Test-NetLogDeployInput -Target 'S1' -RunPath '' -Account 'DOMAIN\svc' } | Should -Throw
    }
}

Describe 'New-NetLogTaskDefinition' {
    It 'builds a task with the collector action, startup trigger, and service-account principal' {
        $task = New-NetLogTaskDefinition -InstallDir 'C:\bin' -RunPath '\\share\runs\audit' -Account 'DOMAIN\svc'
        $task.Actions[0].Execute     | Should -Be '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe'
        $task.Actions[0].Arguments   | Should -BeLike '*Start-NetLogCollector.ps1*'
        $task.Principal.UserId       | Should -Be 'DOMAIN\svc'
        $task.Principal.LogonType    | Should -Be 'Password'
        # at-startup trigger present
        ($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskBootTrigger' }) | Should -Not -BeNullOrEmpty
        $task.Settings.RestartCount  | Should -BeGreaterThan 0
    }
    It 'adds a one-time trigger at the run start when StartUtc is given (future starts fire without a reboot)' {
        $task = New-NetLogTaskDefinition -InstallDir 'C:\bin' -RunPath '\\s\a' -Account 'D\svc' -StartUtc '2026-06-13T08:00:00+00:00'
        $time = @($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskTimeTrigger' })
        $time.Count | Should -Be 1
        # StartBoundary is the target's LOCAL representation of the UTC instant.
        ([datetime]$time[0].StartBoundary).ToUniversalTime() | Should -Be ([datetime]::new(2026, 6, 13, 8, 0, 0, [System.DateTimeKind]::Utc))
        # boot trigger still present
        ($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskBootTrigger' }) | Should -Not -BeNullOrEmpty
    }
}

Describe 'Invoke-NetLogHostDeploy (with fake operations)' {
    It 'opens a session, copies the collector, registers the task, and returns Success' {
        $calls = [System.Collections.Generic.List[string]]::new()
        $ops = @{
            NewSession   = { param($Target, $Credential) $calls.Add("session:$Target"); return "SESSION-$Target" }
            EnsureDir    = { param($Session, $Dir) $calls.Add("ensuredir:$Dir") }
            CopyCollector= { param($Session, $Source, $Dest) $calls.Add("copy:$Source->$Dest") }
            RegisterTask = { param($Session, $TaskName, $Definition, $Account, $Password) $calls.Add("register:$TaskName") }
            StartTask    = { param($Session, $TaskName) $calls.Add("start:$TaskName") }
            RemoveSession= { param($Session) $calls.Add("removesession") }
        }
        $result = Invoke-NetLogHostDeploy -Target 'S1' -RunPath '\\share\runs\audit' -RunName 'audit' `
            -Account 'DOMAIN\svc' -Password 'pw' -CollectorSource 'C:\repo\collector' `
            -InstallDir 'C:\ProgramData\NetworkActivityLogger\bin' -StartNow -Credential $null -Operations $ops

        $result.Target  | Should -Be 'S1'
        $result.Status  | Should -Be 'Success'
        $calls          | Should -Contain 'session:S1'
        $calls          | Should -Contain 'register:NetLogCollector_audit'
        $calls          | Should -Contain 'start:NetLogCollector_audit'
        $calls          | Should -Contain 'removesession'
    }

    It 'returns Failed with the error when an operation throws, and still removes the session' {
        $removed = $false
        $ops = @{
            NewSession    = { param($Target, $Credential) 'S' }
            EnsureDir     = { param($Session, $Dir) }
            CopyCollector = { param($Session, $Source, $Dest) throw 'copy boom' }
            RegisterTask  = { param($Session, $TaskName, $Definition, $Account, $Password) }
            StartTask     = { param($Session, $TaskName) }
            RemoveSession = { param($Session) $script:removed = $true }
        }
        $result = Invoke-NetLogHostDeploy -Target 'S1' -RunPath '\\s\a' -RunName 'a' -Account 'D\s' -Password 'p' `
            -CollectorSource 'C:\repo\collector' -InstallDir 'C:\bin' -Credential $null -Operations $ops
        $result.Status | Should -Be 'Failed'
        $result.Error  | Should -BeLike '*copy boom*'
        $script:removed | Should -BeTrue
    }
}

Describe 'Invoke-NetLogHostTeardown (with fake operations)' {
    It 'stops and unregisters the task and returns Success' {
        $calls = [System.Collections.Generic.List[string]]::new()
        $ops = @{
            NewSession    = { param($Target, $Credential) 'S' }
            StopTask      = { param($Session, $TaskName) $calls.Add("stop:$TaskName") }
            UnregisterTask= { param($Session, $TaskName) $calls.Add("unregister:$TaskName") }
            RemoveSession = { param($Session) $calls.Add('removesession') }
        }
        $r = Invoke-NetLogHostTeardown -Target 'S1' -RunName 'audit' -Credential $null -Operations $ops
        $r.Status | Should -Be 'Success'
        $calls | Should -Contain 'unregister:NetLogCollector_audit'
    }
}
