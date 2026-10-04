function ConvertTo-NetLogRecordKey {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Record
    )
    '{0}|{1}|{2}|{3}|{4}|{5}' -f `
        $Record.Direction, $Record.Source, $Record.SourcePort, `
        $Record.Target, $Record.TargetPort, $Record.Process
}
