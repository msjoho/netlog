#!/usr/bin/env pwsh
# Runs the PowerShell (collector + deploy) test suites under Pester 5.
#
# These tests use Pester 5 syntax. Windows PowerShell 5.1 auto-loads its built-in Pester 3.4.0,
# which fails with "The BeforeAll command may only be used inside a Describe block." Run this from
# PowerShell 7 (pwsh), where Pester 5.7.1 is available. This script forces the right version.

#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module Pester -MinimumVersion 5.0.0 -Force
Write-Host "Using Pester $((Get-Module Pester).Version)" -ForegroundColor Cyan

$tests = @(
    Join-Path $PSScriptRoot 'collector\Tests\NetLogCollector.Tests.ps1'
    Join-Path $PSScriptRoot 'deploy\Tests\NetLogDeploy.Tests.ps1'
)

$result = Invoke-Pester -Path $tests -Output Detailed -PassThru
exit ($result.FailedCount -gt 0 ? 1 : 0)
