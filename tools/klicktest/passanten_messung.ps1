<#
Leistungsmessung der Probe-Passanten (Weg A, Stadtansicht.Passantenprobe.cs) unter Windows, im Fenster (Vulkan). Braucht den PC:
erst starten, wenn der Nutzer ihn freigibt. Fragt einmal mit reichsgilde-frage (tools\aussenstelle) für die ganze Reihe und prüft
zwischen den Läufen den Notschalter (reichsgilde-pruefen).

Aufruf (Windows PowerShell 5.1, im Worktree mit dem Branch phase5/passanten-probe; vorher bauen:
  dotnet build game\Reichsgilde.csproj -p:Optimize=true):
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\klicktest\passanten_messung.ps1 [-Ausgabe <ordner>] [-Laeufe 3] [-Treiber vulkan]

Läufe: je Fall × Zoom × Lauf ein Start von Köln (Markt, 10 Uhr, klar) mit --messung=15:
  alt       ohne Probe (die Figuren aus Blender wie in main)
  probe     --passantenprobe=1 (alle Passanten Probefigur; Zoom 25 Skelett, 50 VAT nah, 100 VAT fern)
  probe400  --passantenprobe=400 (dazu 400 Messfiguren um den Blickpunkt)
  skelett400 --passantenprobe=400 --probestufe=skelett (nur bei Zoom 50 und 100: dieselben 400 mit Skelett statt VAT)
Zoom 25, 50, 100 (--blick=37,-110,<zoom>). Standard 3 Läufe: 4 Fälle, zusammen 33 Starts zu je etwa 1,5 min.

Ergebnis: je Lauf ein Ordner mit godot.log und bild.png (nach 20 s), dazu <ausgabe>\ergebnis.csv mit Bilder/s, Bildzeit, GPU-Zeit, Mittel
der Zeichenaufrufe (Sekundenzeilen der Messung), Grafikspeicher (Godot-Monitor, falls im Log) und der letzten Zeile [passantenprobe].
Ausgang: 0 fertig, 1/2/3 wie reichsgilde-frage, 3 auch Notschalter, 4 Werkzeug fehlt.
Hinweis: Der Notschalter beendet nur Godot-Prozesse unter der Wurzel (tools\aussenstelle\README.md); den Worktree darum dort anlegen.
#>
param(
    [string]$Ausgabe = '',
    [int]$Laeufe = 3,
    [ValidateSet('vulkan', 'd3d12')][string]$Treiber = 'vulkan'
)

$worktree = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
if ($Ausgabe -eq '') { $Ausgabe = Join-Path $env:LOCALAPPDATA ('Reichsgilde\passanten_messung_' + (Get-Date -Format 'yyyyMMdd_HHmm')) }
$daten = Join-Path $env:LOCALAPPDATA 'Reichsgilde\klicktest-daten'
New-Item -ItemType Directory -Force -Path $Ausgabe, $daten | Out-Null

$godot = $env:GODOT
if (-not $godot) {
    $godot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
}
if ($godot -notmatch '_console\.exe$') {
    $konsole = $godot -replace '\.exe$', '_console.exe'
    if (Test-Path -LiteralPath $konsole) { $godot = $konsole }
}
if (-not (Test-Path -LiteralPath $godot)) { Write-Output "Godot nicht gefunden: $godot (GODOT setzen)"; exit 4 }
if (-not (Get-Command 'reichsgilde-frage' -ErrorAction SilentlyContinue)) {
    $env:Path = $env:Path + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
foreach ($werkzeug in 'reichsgilde-frage', 'reichsgilde-pruefen') {
    if (-not (Get-Command $werkzeug -ErrorAction SilentlyContinue)) { Write-Output "$werkzeug fehlt im PATH (tools\aussenstelle\einrichten.ps1)."; exit 4 }
}

# Fälle: Name, zusätzliche Optionen, Zoomstufen
$faelle = @(
    @{ Name = 'alt'; Optionen = @(); Zoom = @(25, 50, 100) },
    @{ Name = 'probe'; Optionen = @('--passantenprobe=1'); Zoom = @(25, 50, 100) },
    @{ Name = 'probe400'; Optionen = @('--passantenprobe=400'); Zoom = @(25, 50, 100) },
    @{ Name = 'skelett400'; Optionen = @('--passantenprobe=400', '--probestufe=skelett'); Zoom = @(50, 100) }
)
$anzahl = 0
foreach ($f in $faelle) { $anzahl += $f.Zoom.Count * $Laeufe }
& reichsgilde-frage -Aufgabe "Passanten-Messung ($anzahl Starts, Fenster)" -Minuten ([int]($anzahl * 1.6) + 5)
$frage = $LASTEXITCODE
if ($frage -ne 0) { Write-Output "Nicht gestartet (reichsgilde-frage: $frage)."; exit $frage }

function Zitieren([string]$text) {
    if ($text -ne '' -and $text -notmatch '[\s"]') { return $text }
    return '"' + (($text -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

$zeilen = @('fall;zoom;lauf;bilder_s;bildzeit_ms;gpu_ms;zeichenaufrufe_mittel;probe_zeile')
$kultur = [Globalization.CultureInfo]::InvariantCulture
foreach ($f in $faelle) {
    foreach ($zoom in $f.Zoom) {
        for ($lauf = 1; $lauf -le $Laeufe; $lauf++) {
            & reichsgilde-pruefen *> $null
            if ($LASTEXITCODE -eq 3) { Write-Output 'Notschalter: Messung abgebrochen.'; exit 3 }
            $ordner = Join-Path $Ausgabe ("{0}_z{1}_{2}" -f $f.Name, $zoom, $lauf)
            New-Item -ItemType Directory -Force -Path $ordner | Out-Null
            $optionen = @('--karte=koeln', '--startstunde=10', '--wetter=klar', "--blick=37,-110,$zoom", '--messung=15') + $f.Optionen +
                @('--klickskript=w 20;s bild;w 300', "--klickausgabe=$ordner", '--klickwarten=5')
            $liste = @('--rendering-driver', $Treiber, '--path', (Join-Path $worktree 'game'), '--resolution', '1600x900', '--') + $optionen
            $argumente = ($liste | ForEach-Object { Zitieren $_ }) -join ' '
            $log = Join-Path $ordner 'godot.log'
            $fehlerLog = Join-Path $ordner 'godot-fehler.log'
            Write-Output ("Lauf {0} Zoom {1} #{2}" -f $f.Name, $zoom, $lauf)
            $altAppData = $env:APPDATA
            $env:APPDATA = $daten
            try {
                $prozess = Start-Process -FilePath $godot -ArgumentList $argumente -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError $fehlerLog
            } finally { $env:APPDATA = $altAppData }
            $null = $prozess.Handle
            $ende = (Get-Date).AddSeconds(240)
            $takt = 0
            while (-not $prozess.HasExited) {
                Start-Sleep -Seconds 2
                $takt++
                if ($takt % 5 -eq 0) {
                    & reichsgilde-pruefen *> $null
                    if ($LASTEXITCODE -eq 3) { & taskkill /PID $prozess.Id /T /F *> $null; Write-Output 'Notschalter: Messung abgebrochen.'; exit 3 }
                }
                if ((Get-Date) -gt $ende) { & taskkill /PID $prozess.Id /T /F *> $null; Write-Output '  Zeitüberschreitung, Lauf beendet.'; break }
            }
            $prozess.WaitForExit(10000) | Out-Null
            if (Test-Path -LiteralPath $fehlerLog) {
                Add-Content -LiteralPath $log -Value (Get-Content -LiteralPath $fehlerLog) -Encoding UTF8
                Remove-Item -LiteralPath $fehlerLog
            }
            # Auswerten: „Messung Ergebnis: … Bilder/s im Mittel …, Bildzeit … ms, GPU … ms“ und die Sekundenzeilen „… Zeichenaufrufe“
            $text = Get-Content -LiteralPath $log -Encoding UTF8
            $ergebnis = $text | Where-Object { $_ -match 'Messung Ergebnis:' } | Select-Object -Last 1
            $bilder = if ($ergebnis -match '([\d,\.]+) Bilder/s im Mittel') { $Matches[1] } else { '' }
            $bildzeit = if ($ergebnis -match 'Bildzeit ([\d,\.]+) ms') { $Matches[1] } else { '' }
            $gpu = if ($ergebnis -match 'GPU ([\d,\.]+) ms') { $Matches[1] } else { '' }
            $aufrufe = @($text | Where-Object { $_ -match '^Messung +\d+ s:.* (\d+) Zeichenaufrufe' } | ForEach-Object {
                    if ($_ -match ' (\d+) Zeichenaufrufe') { [double]$Matches[1] } })
            $mittel = if ($aufrufe.Count -gt 0) { ($aufrufe | Measure-Object -Average).Average.ToString('0', $kultur) } else { '' }
            $probe = ($text | Where-Object { $_ -match '^\[passantenprobe\] Stufe' } | Select-Object -Last 1) -replace ';', ','
            $zeilen += ('{0};{1};{2};{3};{4};{5};{6};{7}' -f $f.Name, $zoom, $lauf, $bilder, $bildzeit, $gpu, $mittel, $probe)
            Write-Output ("  {0} Bilder/s, Bildzeit {1} ms, GPU {2} ms, Zeichenaufrufe {3}" -f $bilder, $bildzeit, $gpu, $mittel)
        }
    }
}
$csv = Join-Path $Ausgabe 'ergebnis.csv'
Set-Content -LiteralPath $csv -Value $zeilen -Encoding UTF8
Write-Output "Fertig: $csv"
exit 0
