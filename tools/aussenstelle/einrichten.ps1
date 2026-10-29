<#
Richtet die Außenstelle ein (ohne Administratorrechte, nur für den aktuellen Benutzer):
- nimmt diesen Ordner in den Benutzer-PATH auf,
- legt auf dem Desktop die Verknüpfung „Reichsgilde – PC zurückholen“ an,
- schreibt die Wurzel für den Notschalter nach %LOCALAPPDATA%\Reichsgilde\wurzel.txt
  (Standard: Elternordner des Repos, z. B. C:\Reichsgilde_Aussenstelle; mit -Wurzel überschreibbar).
Mehrfaches Aufrufen ändert nichts doppelt. Rückgängig: entfernen.ps1
#>
param(
    [string]$Wurzel = ''
)

. (Join-Path $PSScriptRoot 'gemeinsam.ps1')

$ordner = (Resolve-Path -LiteralPath $PSScriptRoot).Path.TrimEnd('\')

# 0. Wurzel des Notschalters.
if ($Wurzel -eq '') { $Wurzel = Hole-StandardWurzel }
$Wurzel = (Resolve-Path -LiteralPath $Wurzel).Path.TrimEnd('\', '/')
Stelle-OrdnerSicher
Set-Content -LiteralPath $script:Wurzeldatei -Value $Wurzel -Encoding UTF8
Write-Output "Wurzel: $Wurzel (in $script:Wurzeldatei)."

# 1. Benutzer-PATH (nicht der System-PATH).
$pfad = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($null -eq $pfad) { $pfad = '' }
$eintraege = @($pfad -split ';' | Where-Object { $_ -ne '' })
$schonDa = $eintraege | Where-Object { $_.TrimEnd('\') -ieq $ordner }
if ($schonDa) {
    Write-Output "PATH: schon eingetragen ($ordner). Nichts geändert."
} else {
    $neuerPfad = (@($eintraege) + $ordner) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $neuerPfad, 'User')
    Write-Output "PATH: Ordner in den Benutzer-PATH aufgenommen ($ordner)."
    Write-Output '      Schon offene Eingabeaufforderungen und PowerShell-Fenster neu öffnen.'
}

# 2. Verknüpfung auf dem Desktop; /k hält das Fenster offen, damit die Meldung lesbar bleibt.
$desktop = [Environment]::GetFolderPath('Desktop')
$lnk = Join-Path $desktop 'Reichsgilde – PC zurückholen.lnk'
$cmd = Join-Path $ordner 'reichsgilde-stopp.cmd'
$ziel = Join-Path $env:SystemRoot 'System32\cmd.exe'
$argumente = '/k ""' + $cmd + '""'
$shell = New-Object -ComObject WScript.Shell
$bestehend = $null
if (Test-Path -LiteralPath $lnk) { $bestehend = $shell.CreateShortcut($lnk) }
if ($bestehend -and $bestehend.TargetPath -ieq $ziel -and $bestehend.Arguments -eq $argumente) {
    Write-Output "Verknüpfung: schon vorhanden ($lnk). Nichts geändert."
} else {
    $link = $shell.CreateShortcut($lnk)
    $link.TargetPath = $ziel
    $link.Arguments = $argumente
    $link.WorkingDirectory = $ordner
    $link.Description = 'Notschalter: sperrt die Reichsgilde-Außenstelle und beendet ihre Prozesse'
    $link.IconLocation = (Join-Path $env:SystemRoot 'System32\shell32.dll') + ',27'
    $link.Save()
    Write-Output "Verknüpfung: angelegt ($lnk)."
}

Schreibe-Log "eingerichtet ($ordner)"
Write-Output 'Fertig. Notschalter: reichsgilde-stopp, Freigabe: reichsgilde-frei.'
