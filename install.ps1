#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Installs the netlog CLI for the current user.

  Two modes, decided by what sits next to this script:
    * Prebuilt package (netlog.exe present — produced by package.ps1): copies the
      self-contained build to %LOCALAPPDATA%\Programs\NetLog. No .NET, no internet.
    * Source tree: publishes the CLI with the .NET SDK (needs NuGet access), then
      copies the PowerShell assets (collector + deploy) next to the exe — the CLI
      resolves them relative to its own location.

  Either way the install folder is added to the user PATH. Re-run to update.
#>
#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\NetLog'

if (Test-Path (Join-Path $PSScriptRoot 'netlog.exe')) {
    # ---- Prebuilt package: plain copy, nothing to build ----
    if ((Get-Item $PSScriptRoot).FullName -eq $installDir) {
        Write-Host 'Running from the install folder itself — nothing to copy.' -ForegroundColor Yellow
    } else {
        Write-Host "Installing prebuilt netlog to $installDir ..." -ForegroundColor Cyan
        if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }  # no stale leftovers
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        Get-ChildItem $PSScriptRoot |
            Where-Object Name -ne 'install.ps1' |
            Copy-Item -Destination $installDir -Recurse
    }
} else {
    # ---- Source tree: build with the SDK ----
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'No .NET SDK found. On machines without SDK/NuGet access, install from a prebuilt package instead: run .\package.ps1 on a dev machine and copy dist\NetLog over.'
    }
    Write-Host "Publishing netlog to $installDir ..." -ForegroundColor Cyan
    dotnet publish (Join-Path $PSScriptRoot 'src\NetLog.Cli\NetLog.Cli.csproj') -c Release -o $installDir --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet publish failed. If this machine has no NuGet access (NU1100/NU1301), build a package on a dev machine instead: .\package.ps1 there, then copy dist\NetLog here and run its install.ps1.'
    }

    # The CLI locates the collector source and deploy entry script by walking up from the
    # exe directory — ship them alongside it (tests excluded; they are not deployed to hosts).
    foreach ($asset in 'collector', 'deploy') {
        $dest = Join-Path $installDir $asset
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Copy-Item -Path (Join-Path $PSScriptRoot $asset) -Destination $dest -Recurse
        $tests = Join-Path $dest 'Tests'
        if (Test-Path $tests) { Remove-Item $tests -Recurse -Force }
    }
}

# Add to the user PATH (idempotent).
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (($userPath -split ';') -notcontains $installDir) {
    [Environment]::SetEnvironmentVariable('Path', "$userPath;$installDir", 'User')
    Write-Host "Added $installDir to your user PATH." -ForegroundColor Yellow
    Write-Host 'Open a NEW terminal for the change to take effect.' -ForegroundColor Yellow
} else {
    Write-Host 'Install folder is already on your user PATH.'
}

Write-Host "Done. Run 'netlog' (new terminal if PATH was just added)." -ForegroundColor Green
