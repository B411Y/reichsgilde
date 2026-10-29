<#
Zwischenprüfung für lange Aufgaben der Außenstelle.
Ausgang: 0 = weiter, 3 = sofort abbrechen (Sperrdatei STOPP ist da).
#>
. (Join-Path $PSScriptRoot 'gemeinsam.ps1')

if (Ist-Gesperrt) {
    Schreibe-Log 'pruefen: gesperrt, Abbruch'
    Write-Output 'Gesperrt (Sperrdatei STOPP). Sofort abbrechen.'
    exit 3
}
Write-Output 'Weiter.'
exit 0
