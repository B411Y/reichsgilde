# Gemeinsame Helfer der Außenstelle (wird von den anderen Skripten eingebunden).
# Keine Netzwerkzugriffe außer der Prüfung lokaler Ports.

$script:Ordner = Join-Path $env:LOCALAPPDATA 'Reichsgilde'
$script:Sperrdatei = Join-Path $script:Ordner 'STOPP'
$script:Logdatei = Join-Path $script:Ordner 'aussenstelle.log'

# Lokale Ports der KI-Dienste: ComfyUI, ComfyUI-3D, TTS, AssetFurnace.
$script:KiPorts = @(
    @{ Port = 8188; Name = 'ComfyUI' },
    @{ Port = 8189; Name = 'ComfyUI-3D' },
    @{ Port = 8190; Name = 'TTS' },
    @{ Port = 8777; Name = 'AssetFurnace' }
)

function Stelle-OrdnerSicher {
    if (-not (Test-Path -LiteralPath $script:Ordner)) {
        New-Item -ItemType Directory -Path $script:Ordner -Force | Out-Null
    }
}

function Schreibe-Log([string]$Text) {
    try {
        Stelle-OrdnerSicher
        $zeile = '{0} | {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Text
        Add-Content -LiteralPath $script:Logdatei -Value $zeile -Encoding UTF8
    } catch {
        Write-Warning "Logdatei nicht beschreibbar: $($_.Exception.Message)"
    }
}

function Teste-Port([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $verbindung = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        if ($verbindung.AsyncWaitHandle.WaitOne(500) -and $client.Connected) { return $true }
        return $false
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

# Liefert die Namen der laufenden KI-Dienste (leer, wenn keiner läuft).
function Hole-LaufendeKi {
    $laufend = @()
    foreach ($ki in $script:KiPorts) {
        if (Teste-Port $ki.Port) { $laufend += ('{0} (Port {1})' -f $ki.Name, $ki.Port) }
    }
    return $laufend
}

function Ist-Gesperrt {
    return (Test-Path -LiteralPath $script:Sperrdatei)
}
