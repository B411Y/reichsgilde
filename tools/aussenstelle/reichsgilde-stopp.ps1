<#
Notschalter: sperrt neue Aufträge der Außenstelle und beendet ihre Godot- und dotnet-Prozesse.
Betroffen sind nur Prozesse, die aus dem Ordner dieser Außenstelle gestartet wurden
(Pfad in der Befehlszeile oder im Programmpfad); ein Godot-Editor in einem anderen Ordner bleibt.
#>
. (Join-Path $PSScriptRoot 'gemeinsam.ps1')

Stelle-OrdnerSicher
Set-Content -LiteralPath $script:Sperrdatei -Value ('Gesperrt am ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')) -Encoding UTF8

# Wurzel des Klons: tools\aussenstelle liegt zwei Ebenen darunter.
$wurzel = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path.TrimEnd('\', '/')
$wurzelSchraeg = $wurzel.Replace('\', '/')

function Gehoert-Zur-Aussenstelle($prozess) {
    foreach ($feld in @($prozess.CommandLine, $prozess.ExecutablePath)) {
        if ([string]::IsNullOrEmpty($feld)) { continue }
        if ($feld.IndexOf($wurzel, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
        if ($feld.IndexOf($wurzelSchraeg, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
    }
    return $false
}

$alle = @(Get-CimInstance Win32_Process)
$treffer = @{}
foreach ($p in $alle) {
    if ($p.ProcessId -eq $PID) { continue }
    if ($p.Name -notmatch '^(godot|dotnet)') { continue }
    if (Gehoert-Zur-Aussenstelle $p) { $treffer[[int]$p.ProcessId] = $p.Name }
}

# Auch deren Kindprozesse (Godot und dotnet starten Helfer wie MSBuild oder Testhost).
$neu = $true
while ($neu) {
    $neu = $false
    foreach ($p in $alle) {
        if ($p.ProcessId -eq $PID) { continue }
        if ($p.Name -notmatch '^(godot|dotnet|msbuild|vbcscompiler|testhost)') { continue }
        if (-not $treffer.ContainsKey([int]$p.ProcessId) -and $treffer.ContainsKey([int]$p.ParentProcessId)) {
            $treffer[[int]$p.ProcessId] = $p.Name
            $neu = $true
        }
    }
}

$beendet = 0
foreach ($id in $treffer.Keys) {
    try {
        Stop-Process -Id $id -Force -ErrorAction Stop
        $beendet++
    } catch {
        Write-Warning "Prozess $id ($($treffer[$id])) nicht beendet: $($_.Exception.Message)"
    }
}

Schreibe-Log "STOPP: Sperre gesetzt, $beendet Prozess(e) beendet"
Write-Output "$beendet Prozess(e) der Außenstelle beendet."
Write-Output "Reichsgilde-Außenstelle gestoppt. Neue Aufträge sind gesperrt, bis du `‚reichsgilde-frei`‘ eingibst."
