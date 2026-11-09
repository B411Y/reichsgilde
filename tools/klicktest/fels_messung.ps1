<#
Leistungsmessung der Felsen (Felsen R6, vorbereitet in R7) unter Windows, im Fenster (Vulkan). Braucht den PC: erst starten, wenn der
Nutzer ihn freigibt. Fragt einmal mit reichsgilde-frage (tools\aussenstelle) für die ganze Reihe und prüft zwischen den Läufen den
Notschalter (reichsgilde-pruefen). Vorbild: passanten_messung.ps1 (Branch phase5/passanten-probe).

Aufruf (Windows PowerShell 5.1, im Worktree; vorher bauen und einmal importieren:
  dotnet build game\Reichsgilde.csproj -p:Optimize=true):
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\klicktest\fels_messung.ps1 [-Ausgabe <ordner>] [-Laeufe 2] [-Anzahl 48]
      [-Treiber vulkan] [-Python python]

Messkarte: tools\klicktest\fels_messkarte.py setzt -Anzahl Stücke des Felswand-Satzes (alle elf Formen reihum, jede zweite Reihe mit dem
Rücken zur Kamera) an eine freie Stelle im Umland von Köln und schreibt sie nach user://karten/felsmessung.json (im Datenordner der
Klicktests, nicht im Repository); gestartet wird mit --karte=felsmessung.json.

Läufe: je Grafikstufe × Ansicht × Lauf ein Start mit --felsvergleich=1 (im selben Prozess reihum „ohne“, „Felsen“, „Kulisse“, „beides“,
je 4 s, drei Runden, Median der GPU- und Bildzeit, Stadtansicht.Felsmessung.cs):
  Grafikstufe  Hoch, Ultra (--voreinstellung)
  Ansicht      z25, z50, z100 (Spielkamera, --blick=<mitte>,<zoom>) und nah (freie Kamera etwa 20 m vor dem Block, 6 m über dem Boden;
               die Bodenhöhe liest ein kurzer Vorlauf aus der Zeile [freiekamera] des Logs)
Standard 2 Läufe: 2 × 4 × 2 = 16 Starts zu je etwa 1,5 min, dazu der Vorlauf.

Ergebnis: je Lauf ein Ordner mit godot.log und bild.png (nach 15 s), dazu <ausgabe>\ergebnis.csv mit je Zustand GPU-Zeit, Unterschied zu
„ohne“ und Bildzeit (Zeilen [felsmessung]) und der Zahl der Stücke und Zellen.
Ausgang: 0 fertig, 1/2/3 wie reichsgilde-frage, 3 auch Notschalter, 4 Werkzeug fehlt, 5 Messkarte oder Vorlauf gescheitert.
Hinweis: Der Notschalter beendet nur Godot-Prozesse unter der Wurzel (tools\aussenstelle\README.md); den Worktree darum dort anlegen.
#>
param(
    [string]$Ausgabe = '',
    [int]$Laeufe = 2,
    [int]$Anzahl = 48,
    [ValidateSet('vulkan', 'd3d12')][string]$Treiber = 'vulkan',
    [string]$Python = 'python'
)

$worktree = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
if ($Ausgabe -eq '') { $Ausgabe = Join-Path $env:LOCALAPPDATA ('Reichsgilde\fels_messung_' + (Get-Date -Format 'yyyyMMdd_HHmm')) }
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
if (-not (Get-Command $Python -ErrorAction SilentlyContinue)) { Write-Output "$Python fehlt (-Python <pfad>)"; exit 4 }
if (-not (Get-Command 'reichsgilde-frage' -ErrorAction SilentlyContinue)) {
    $env:Path = $env:Path + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
foreach ($werkzeug in 'reichsgilde-frage', 'reichsgilde-pruefen') {
    if (-not (Get-Command $werkzeug -ErrorAction SilentlyContinue)) { Write-Output "$werkzeug fehlt im PATH (tools\aussenstelle\einrichten.ps1)."; exit 4 }
}

# 1. Messkarte in den Datenordner der Klicktests (user:// = %APPDATA%\Godot\app_userdata\Reichsgilde, APPDATA wird beim Start umgebogen)
$kartenOrdner = Join-Path $daten 'Godot\app_userdata\Reichsgilde\karten'
New-Item -ItemType Directory -Force -Path $kartenOrdner | Out-Null
$env:PYTHONIOENCODING = 'utf-8'
$ausgabeKarte = & $Python (Join-Path $worktree 'tools\klicktest\fels_messkarte.py') (Join-Path $kartenOrdner 'felsmessung.json') "--anzahl=$Anzahl"
$ausgabeKarte | ForEach-Object { Write-Output "  $_" }
$mitteZeile = $ausgabeKarte | Where-Object { $_ -match '^MITTE ' } | Select-Object -Last 1
if (-not $mitteZeile) { Write-Output 'Messkarte gescheitert.'; exit 5 }
$mx, $mz = ($mitteZeile -split ' ')[1, 2]

$stufen = @('Hoch', 'Ultra')
$ansichten = @('z25', 'z50', 'z100', 'nah')
$anzahlStarts = $stufen.Count * $ansichten.Count * $Laeufe + 1
& reichsgilde-frage -Aufgabe "Fels-Messung ($anzahlStarts Starts, Fenster)" -Minuten ([int]($anzahlStarts * 1.7) + 5)
$frage = $LASTEXITCODE
if ($frage -ne 0) { Write-Output "Nicht gestartet (reichsgilde-frage: $frage)."; exit $frage }

function Zitieren([string]$text) {
    if ($text -ne '' -and $text -notmatch '[\s"]') { return $text }
    return '"' + (($text -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

# Startet Godot im Fenster und wartet (höchstens $sekunden), mit Notschalter; gibt die Zeilen des Logs zurück
function Starten([string]$ordner, [string[]]$optionen, [int]$sekunden) {
    New-Item -ItemType Directory -Force -Path $ordner | Out-Null
    $liste = @('--rendering-driver', $Treiber, '--path', (Join-Path $worktree 'game'), '--resolution', '1600x900', '--') + $optionen
    $argumente = ($liste | ForEach-Object { Zitieren $_ }) -join ' '
    $log = Join-Path $ordner 'godot.log'
    $fehlerLog = Join-Path $ordner 'godot-fehler.log'
    $altAppData = $env:APPDATA
    $env:APPDATA = $daten
    try {
        $prozess = Start-Process -FilePath $godot -ArgumentList $argumente -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError $fehlerLog
    } finally { $env:APPDATA = $altAppData }
    $null = $prozess.Handle
    $ende = (Get-Date).AddSeconds($sekunden)
    $takt = 0
    while (-not $prozess.HasExited) {
        Start-Sleep -Seconds 2
        $takt++
        if ($takt % 5 -eq 0) {
            & reichsgilde-pruefen *> $null
            if ($LASTEXITCODE -eq 3) { & taskkill /PID $prozess.Id /T /F *> $null; Write-Output 'Notschalter: Messung abgebrochen.'; exit 3 }
        }
        if ((Get-Date) -gt $ende) { & taskkill /PID $prozess.Id /T /F *> $null; break }
    }
    $prozess.WaitForExit(10000) | Out-Null
    if (Test-Path -LiteralPath $fehlerLog) {
        Add-Content -LiteralPath $log -Value (Get-Content -LiteralPath $fehlerLog) -Encoding UTF8
        Remove-Item -LiteralPath $fehlerLog
    }
    return Get-Content -LiteralPath $log -Encoding UTF8
}

$gemeinsam = @('--karte=felsmessung.json', '--startstunde=10', '--wetter=klar', '--vsync=Aus')

# 2. Vorlauf: Bodenhöhe am Blickpunkt für die freie Kamera (Zeile „[freiekamera] Ort …, Blickpunkt x,z (Boden h)“)
& reichsgilde-pruefen *> $null
if ($LASTEXITCODE -eq 3) { Write-Output 'Notschalter: Messung abgebrochen.'; exit 3 }
Write-Output 'Vorlauf: Bodenhöhe am Block'
$vorlauf = Starten (Join-Path $Ausgabe 'vorlauf') ($gemeinsam + @("--blick=$mx,$mz,25", '--freiekamera=1', '--messung=4')) 150
$bodenZeile = $vorlauf | Where-Object { $_ -match '^\[freiekamera\] Ort .*\(Boden (-?[\d,\.]+)\)' } | Select-Object -Last 1
if (-not $bodenZeile) { Write-Output 'Vorlauf ohne Zeile [freiekamera]: Bodenhöhe unbekannt.'; exit 5 }
$null = $bodenZeile -match '\(Boden (-?[\d,\.]+)\)'
$kultur = [Globalization.CultureInfo]::InvariantCulture
$boden = [double]::Parse(($Matches[1] -replace ',', '.'), $kultur)
$x0 = [double]::Parse($mx, $kultur)
$z0 = [double]::Parse($mz, $kultur)
# freie Kamera: 20 m vor der Reihe mit dem größten z (Reihen 20 m auseinander, um die Mitte), 6 m über dem Boden, Blick auf diese Reihe
$reiheZ = $z0 + ([math]::Ceiling($Anzahl / 12) - 1) * 10
$nah = '--freiekamera-start={0},{1},{2}:{3},{4},{5}' -f ($x0 + 4).ToString('0.0', $kultur), ($boden + 6).ToString('0.0', $kultur),
    ($reiheZ + 20).ToString('0.0', $kultur), $x0.ToString('0.0', $kultur), ($boden + 3).ToString('0.0', $kultur), $reiheZ.ToString('0.0', $kultur)
Write-Output "  Boden $boden m, nah: $nah"

# 3. Messläufe
$zeilen = @('stufe;ansicht;lauf;zustand;gpu_ms;gpu_mehr_ms;bildzeit_ms;stuecke_zellen')
foreach ($stufe in $stufen) {
    foreach ($ansicht in $ansichten) {
        for ($lauf = 1; $lauf -le $Laeufe; $lauf++) {
            & reichsgilde-pruefen *> $null
            if ($LASTEXITCODE -eq 3) { Write-Output 'Notschalter: Messung abgebrochen.'; exit 3 }
            $ordner = Join-Path $Ausgabe ("{0}_{1}_{2}" -f $stufe, $ansicht, $lauf)
            $blick = if ($ansicht -eq 'nah') { @("--blick=$mx,$mz,25", '--freiekamera=1', $nah) } else { @("--blick=$mx,$mz,$($ansicht.Substring(1))") }
            $optionen = $gemeinsam + @("--voreinstellung=$stufe", '--felsvergleich=1') + $blick +
                @('--klickskript=w 15;s bild;w 600', "--klickausgabe=$ordner", '--klickwarten=5')
            Write-Output ("Lauf {0} {1} #{2}" -f $stufe, $ansicht, $lauf)
            $text = Starten $ordner $optionen 300
            # Auswerten: „[felsmessung] <zustand>: GPU <x> ms (<±d> ms), Bildzeit <y> ms = …“ und „[felsmessung] fertig: <n> Felsstücke in <z> Zellen …“
            $fertig = ($text | Where-Object { $_ -match '^\[felsmessung\] fertig: (.*)' } | Select-Object -Last 1) -replace '^\[felsmessung\] fertig: ', '' -replace ';', ','
            $treffer = 0
            foreach ($z in $text) {
                if ($z -match '^\[felsmessung\] (\S+): GPU ([\d,\.]+) ms \(([+-][\d,\.]+) ms\), Bildzeit ([\d,\.]+) ms') {
                    $zeilen += ('{0};{1};{2};{3};{4};{5};{6};{7}' -f $stufe, $ansicht, $lauf, $Matches[1], $Matches[2], $Matches[3], $Matches[4], $fertig)
                    Write-Output ("  {0}: GPU {1} ms ({2}), Bildzeit {3} ms" -f $Matches[1], $Matches[2], $Matches[3], $Matches[4])
                    $treffer++
                }
            }
            if ($treffer -eq 0) { Write-Output '  keine Zeilen [felsmessung] (Log ansehen)'; $zeilen += ('{0};{1};{2};;;;;' -f $stufe, $ansicht, $lauf) }
        }
    }
}
$csv = Join-Path $Ausgabe 'ergebnis.csv'
Set-Content -LiteralPath $csv -Value $zeilen -Encoding UTF8
Write-Output "Fertig: $csv"
exit 0
