<# Hebt die Sperre des Notschalters wieder auf. #>
. (Join-Path $PSScriptRoot 'gemeinsam.ps1')

if (Ist-Gesperrt) {
    Remove-Item -LiteralPath $script:Sperrdatei -Force
    Schreibe-Log 'FREI: Sperre aufgehoben'
    Write-Output 'Reichsgilde-Außenstelle freigegeben. Neue Aufträge sind wieder möglich.'
} else {
    Write-Output 'Es war nicht gesperrt. Nichts geändert.'
}
