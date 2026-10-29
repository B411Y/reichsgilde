<#
Klicktest unter Windows: startet das Spiel im Fenster; das Spiel führt die Aktionen selbst aus (KlicktestTreiber.cs,
als InputEvents, die echte Maus bleibt frei) und macht die Bildschirmfotos aus der Viewport-Textur.

Aufruf (Windows PowerShell 5.1):
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\klicktest\lauf.ps1 [-Treiber vulkan|d3d12] [-Daten <ordner>] `
      <ausgabeordner> <wartesekunden> "<aktionen>" -- <godot-optionen>

Parameter:
  -Treiber   vulkan (Standard) oder d3d12 (DirectX 12).
  -Daten     Ablageort für Spieldaten (user://: Einstellungen, Spielstände, Shader-Zwischenspeicher), damit der Klicktest
             die echten Daten des Spielers nicht anfasst; Standard %LOCALAPPDATA%\Reichsgilde\klicktest-daten.
  Godot      aus $env:GODOT, sonst der winget-Pfad; genommen wird die _console.exe (sonst kommt kein Log an).
  Worktree   der Ordner zwei Ebenen über diesem Skript (enthält game\).

Aktionen (durch ; getrennt, wie lauf.sh):
  m x y            Maus bewegen
  w sek            warten (Tooltips brauchen ca. 10 s Hover; Hover folgt allerdings der echten Maus, siehe unten)
  k taste          Taste drücken (xdotool-Name, z. B. Escape, space, Return, Tab, F5, a; auch ctrl+s)
  b knopf          Mausknopf klicken (1 links, 3 rechts, 4/5 Rad)
  d x1 y1 x2 y2    mit gedrückter linker Taste ziehen
  s name           Bildschirmfoto <ausgabeordner>\<name>.png
Beispiel:
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\klicktest\lauf.ps1 C:\Temp\k 30 "m 800 450;b 1;w 2;s stadt" -- --karte=koeln --startstunde=12

Ablauf: Erst fragt reichsgilde-frage (tools\aussenstelle) den Nutzer; ist der Rückgabewert nicht 0, startet nichts.
Während des Laufs prüft reichsgilde-pruefen alle 10 s den Notschalter (Code 3: Godot wird beendet). Nach Wartezeit plus
Aktionen plus 120 s ohne Ende bricht das Skript ab. Log: <ausgabeordner>\godot.log.
Grenzen: Das Fenster ist sichtbar. Hover, Tooltips und die Lage der Auswahltafel lesen die echte Maus
(Viewport.GetMousePosition), Klicks und Tasten wirken dagegen wie im Spiel.
Ausgang: 0 fertig, 1/2/3 wie reichsgilde-frage (abgelehnt, KI läuft, gesperrt), 3 auch Notschalter im Lauf,
4 Werkzeug fehlt, 5 Zeitüberschreitung. Die Bilder danach selbst ansehen (Read-Werkzeug) und beschreiben.
#>
# Kein param-Block: Windows PowerShell 5.1 weist bei -File ein „--“ oder „--karte=…“ sonst als unbekannten Parameter ab.
# Darum werden die Argumente hier von Hand gelesen: -Treiber/-Daten, drei Pflichtangaben, nach „--“ die Godot-Optionen.
$Treiber = 'vulkan'
$Daten = ''
$pflicht = @()
$Optionen = @()
for ($i = 0; $i -lt $args.Count; $i++) {
    $a = [string]$args[$i]
    if ($a -eq '--') {
        if ($i + 1 -lt $args.Count) { $Optionen = @($args[($i + 1)..($args.Count - 1)]) }
        break
    }
    elseif ($a -ieq '-Treiber') { $Treiber = [string]$args[++$i] }
    elseif ($a -ieq '-Daten') { $Daten = [string]$args[++$i] }
    else { $pflicht += $a }
}
if ($pflicht.Count -lt 3 -or $Treiber -notin 'vulkan', 'd3d12') {
    Write-Output 'Aufruf: lauf.ps1 [-Treiber vulkan|d3d12] [-Daten <ordner>] <ausgabeordner> <wartesekunden> "<aktionen>" -- <godot-optionen>'
    exit 4
}
$Ausgabe = $pflicht[0]
$Warten = [double]::Parse($pflicht[1], [Globalization.CultureInfo]::InvariantCulture)
$Aktionen = $pflicht[2]

$worktree = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
if ($Daten -eq '') { $Daten = Join-Path $env:LOCALAPPDATA 'Reichsgilde\klicktest-daten' }
New-Item -ItemType Directory -Force -Path $Ausgabe, $Daten | Out-Null
$Ausgabe = (Resolve-Path -LiteralPath $Ausgabe).Path

# Godot: $env:GODOT, sonst winget; immer die Konsolenfassung.
$godot = $env:GODOT
if (-not $godot) {
    $godot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
}
if ($godot -notmatch '_console\.exe$') {
    $konsole = $godot -replace '\.exe$', '_console.exe'
    if (Test-Path -LiteralPath $konsole) { $godot = $konsole }
}
if (-not (Test-Path -LiteralPath $godot)) { Write-Output "Godot nicht gefunden: $godot (GODOT setzen)"; exit 4 }

# Ein Fenster von vor einrichten.ps1 kennt den neuen Benutzer-PATH noch nicht.
if (-not (Get-Command 'reichsgilde-frage' -ErrorAction SilentlyContinue)) {
    $env:Path = $env:Path + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
foreach ($werkzeug in 'reichsgilde-frage', 'reichsgilde-pruefen') {
    if (-not (Get-Command $werkzeug -ErrorAction SilentlyContinue)) {
        Write-Output "$werkzeug fehlt im PATH (tools\aussenstelle\einrichten.ps1 ausführen)."
        exit 4
    }
}

# Dauer: Wartezeit plus Wartebefehle plus etwas je Aktion.
$schritte = @($Aktionen -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
$aktionSek = 0.0
foreach ($s in $schritte) {
    $teile = $s -split '\s+'
    if ($teile[0] -eq 'w') { $aktionSek += [double]::Parse($teile[1], [Globalization.CultureInfo]::InvariantCulture) } else { $aktionSek += 1 }
}
$minuten = [int][math]::Ceiling(($Warten + $aktionSek) / 60) + 2

& reichsgilde-frage -Aufgabe "Klicktest ($Treiber)" -Minuten $minuten
$frage = $LASTEXITCODE
if ($frage -ne 0) { Write-Output "Nicht gestartet (reichsgilde-frage: $frage)."; exit $frage }

# Argumente für Windows zitieren (Leerzeichen, Anführungszeichen, Backslash davor).
function Zitieren([string]$text) {
    if ($text -ne '' -and $text -notmatch '[\s"]') { return $text }
    return '"' + (($text -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}
$warteText = $Warten.ToString([Globalization.CultureInfo]::InvariantCulture)
$liste = @('--rendering-driver', $Treiber, '--path', (Join-Path $worktree 'game'), '--resolution', '1600x900', '--') + $Optionen +
    @("--klickskript=$Aktionen", "--klickausgabe=$Ausgabe", "--klickwarten=$warteText")
$argumente = ($liste | ForEach-Object { Zitieren $_ }) -join ' '

$log = Join-Path $Ausgabe 'godot.log'
$fehlerLog = Join-Path $Ausgabe 'godot-fehler.log'
Write-Output "Start: $godot $argumente"
# user:// liegt unter Windows in %APPDATA%; nur für Godot umgelenkt.
$altAppData = $env:APPDATA
$env:APPDATA = $Daten
try {
    $prozess = Start-Process -FilePath $godot -ArgumentList $argumente -NoNewWindow -PassThru `
        -RedirectStandardOutput $log -RedirectStandardError $fehlerLog
} finally {
    $env:APPDATA = $altAppData
}
$null = $prozess.Handle # sonst liefert ExitCode in PowerShell 5.1 nichts

function Beenden {
    & taskkill /PID $prozess.Id /T /F *> $null # samt dem eigentlichen Godot, das die _console.exe startet
}

$ende = (Get-Date).AddSeconds($Warten + $aktionSek + 120)
$takt = 0
$code = 0
while (-not $prozess.HasExited) {
    Start-Sleep -Seconds 2
    $takt++
    if ($takt % 5 -eq 0) {
        & reichsgilde-pruefen *> $null
        if ($LASTEXITCODE -eq 3) { Write-Output 'Notschalter: Godot wird beendet.'; Beenden; $code = 3; break }
    }
    if ((Get-Date) -gt $ende) { Write-Output 'Zeitüberschreitung: Godot wird beendet.'; Beenden; $code = 5; break }
}
$prozess.WaitForExit(10000) | Out-Null

if (Test-Path -LiteralPath $fehlerLog) {
    Add-Content -LiteralPath $log -Value (Get-Content -LiteralPath $fehlerLog) -Encoding UTF8
    Remove-Item -LiteralPath $fehlerLog
}
Get-Content -LiteralPath $log -Encoding UTF8 | Where-Object { $_ -match 'Klicktest:' }
Get-ChildItem -LiteralPath $Ausgabe -Filter '*.png' | ForEach-Object { "Bild: $($_.FullName)" }
exit $code
