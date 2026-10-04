function Invoke-NetLogArchiveCycle {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CachePath,
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$StatusPath,
        [Parameter(Mandatory)][ValidateSet('Full','Merged')][string]$Level,
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][int]$TotalPolls,
        [Parameter(Mandatory)][datetime]$NowUtc,
        [string]$RunArchivePath,
        [switch]$IncludeTimestamps
    )
    $cache     = Import-NetLogCache   -Path $CachePath
    $condensed = Invoke-NetLogCondense -Records $cache -Level $Level
    # Per-run archive (run<N>.csv): cumulative within this run only. The local cache is
    # per-run, so the condensed set never contains another run's records.
    if ($RunArchivePath) {
        $runExisting = Import-NetLogArchive -Path $RunArchivePath
        $runMerged   = Merge-NetLogRecordSet -Existing $runExisting -New $condensed -Level $Level
        Export-NetLogArchive -Records $runMerged -Path $RunArchivePath -IncludeTimestamps:$IncludeTimestamps
    }
    $existing  = Import-NetLogArchive -Path $ArchivePath
    $merged    = Merge-NetLogRecordSet -Existing $existing -New $condensed -Level $Level
    Export-NetLogArchive -Records $merged -Path $ArchivePath -IncludeTimestamps:$IncludeTimestamps
    $status = New-NetLogStatus -LastRunUtc $NowUtc -ProcessId $ProcessId `
                               -TotalConnections (@($merged).Count) -TotalPolls $TotalPolls
    Write-NetLogStatus -Status $status -Path $StatusPath
    ,$merged
}
