# Dot-source every function file, then export all functions.
Get-ChildItem -Path (Join-Path $PSScriptRoot 'Functions') -Filter '*.ps1' -ErrorAction SilentlyContinue |
    Sort-Object Name |
    ForEach-Object { . $_.FullName }
Export-ModuleMember -Function *
