#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Builds a portable, offline-installable NetLog package into dist\.

  Run this on the dev machine (needs the .NET SDK and NuGet access). The output is a
  SELF-CONTAINED win-x64 build: target machines need no .NET runtime/SDK and no internet.
  Copy dist\NetLog (or dist\NetLog.zip) to the machine and run .\install.ps1 inside it —
  the installer detects the prebuilt exe and just copies, no build.
#>
#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$dist    = Join-Path $PSScriptRoot 'dist'
$payload = Join-Path $dist 'NetLog'
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }

Write-Host "Publishing self-contained netlog (win-x64) to $payload ..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'src\NetLog.Cli\NetLog.Cli.csproj') `
    -c Release -r win-x64 --self-contained -p:PublishSingleFile=true `
    -o $payload --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# Same assets install.ps1 ships when building from source (tests are not deployed).
foreach ($asset in 'collector', 'deploy') {
    $dest = Join-Path $payload $asset
    Copy-Item -Path (Join-Path $PSScriptRoot $asset) -Destination $dest -Recurse
    $tests = Join-Path $dest 'Tests'
    if (Test-Path $tests) { Remove-Item $tests -Recurse -Force }
}
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') -Destination $payload

$zip = Join-Path $dist 'NetLog.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $payload -DestinationPath $zip

Write-Host 'Done.' -ForegroundColor Green
Write-Host "  Folder : $payload"
Write-Host "  Zip    : $zip"
Write-Host 'Copy either to the target machine and run .\install.ps1 from inside the NetLog folder.'
