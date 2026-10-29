<#
Macht einrichten.ps1 rückgängig: Ordner aus dem Benutzer-PATH nehmen, Desktop-Verknüpfung löschen.
Logdatei und Sperrdatei in %LOCALAPPDATA%\Reichsgilde bleiben unberührt.
#>
. (Join-Path $PSScriptRoot 'gemeinsam.ps1')

$ordner = (Resolve-Path -LiteralPath $PSScriptRoot).Path.TrimEnd('\')

$pfad = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($null -eq $pfad) { $pfad = '' }
$eintraege = @($pfad -split ';' | Where-Object { $_ -ne '' })
$rest = @($eintraege | Where-Object { $_.TrimEnd('\') -ine $ordner })
if ($rest.Count -ne $eintraege.Count) {
    [Environment]::SetEnvironmentVariable('Path', ($rest -join ';'), 'User')
    Write-Output "PATH: Ordner aus dem Benutzer-PATH entfernt ($ordner)."
} else {
    Write-Output 'PATH: war nicht eingetragen. Nichts geändert.'
}

$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Reichsgilde – PC zurückholen.lnk'
if (Test-Path -LiteralPath $lnk) {
    Remove-Item -LiteralPath $lnk -Force
    Write-Output "Verknüpfung: gelöscht ($lnk)."
} else {
    Write-Output 'Verknüpfung: war nicht vorhanden. Nichts geändert.'
}

Schreibe-Log "entfernt ($ordner)"
Write-Output 'Fertig.'
