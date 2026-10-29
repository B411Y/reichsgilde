# Testanleitung: DirectX 12 unter Windows

*Für den Nutzer: DirectX 12 lässt sich nur auf einem Windows-Rechner prüfen. Dauer etwa 15 Minuten. Stand: 29.10.2026, Umbau der Einstellungen.*

Seit dem Umbau der Einstellungen kann man unter **Grafik → Erweitert → Grafikschnittstelle** zwischen **Vulkan** (Standard) und **DirectX 12** wählen. Die Wahl gilt nach einem Neustart. Wie es technisch geht, steht in [ARCHITEKTUR.md](ARCHITEKTUR.md) (Abschnitt „Grafikschnittstelle“) und in `game/scripts/Grafikschnittstelle.cs`.

## Vorbereitung

1. Spiel aus dem Godot-Editor oder mit `Godot_console.exe --path game` starten (die Konsole zeigt, welchen Treiber Godot nimmt). Im Editor das Einbetten im Reiter „Game“ ausschalten, sonst gilt der Treiber des Editors.
2. Die Dateien der Einstellungen liegen in `%APPDATA%\Godot\app_userdata\Reichsgilde\`. Dort entstehen:
   - `grafikschnittstelle.cfg` – die Wahl (nur nach einer Wahl vorhanden),
   - `grafikschnittstelle.versuch` – der Vermerk „DirectX-12-Start läuft“ (nur während eines solchen Starts, wenige Sekunden bis 30 s).
3. Zum Zurücksetzen jederzeit: beide Dateien löschen, dann gilt wieder Vulkan. Oder das Spiel mit `--rendering-driver vulkan` starten (die Startoption geht vor der Datei).

## Test 1: Standard ist Vulkan

- Datei `grafikschnittstelle.cfg` löschen (falls da), Spiel starten.
- **Erwartet:** Die Konsole zeigt beim Start eine Zeile mit „Vulkan“ und dem Namen der Grafikkarte. Einstellungen → Grafik → Erweitert → ganz unten: Zeile **Grafikschnittstelle** mit „Vulkan (empfohlen)“ und darunter „Läuft mit Vulkan.“

## Test 2: auf DirectX 12 umstellen

- In der Zeile **Vulkan** → **DirectX 12 (wenig getestet)** wählen.
- **Erwartet:** Darunter steht „Gilt erst nach einem Neustart des Spiels. Jetzt läuft Vulkan, danach DirectX 12.“ Die Datei `grafikschnittstelle.cfg` enthält:

```
[rendering]

rendering_device/driver.windows="d3d12"
```

- Spiel ganz beenden und neu starten.
- **Erwartet:** Die Konsole zeigt jetzt eine Zeile mit „Direct3D 12“ (statt „Vulkan“), die Zeile sagt „Läuft mit DirectX 12.“ Das Bild sieht aus wie unter Vulkan (Schatten, Gras, Wasser, Himmel, Tafeln). Eine Minute spielen, Voreinstellung wechseln, Kamera drehen und zoomen, Vollbild (F11) an und aus.
- **Während der ersten 30 Sekunden** liegt `grafikschnittstelle.versuch` im Ordner, danach nicht mehr.

## Test 3: sauberer Rückweg

- Zeile auf **Vulkan** zurückstellen, Spiel beenden und neu starten. **Erwartet:** „Läuft mit Vulkan.“, kein Hinweis.

## Test 4: Rückfall nach einem Absturz (Vermerk)

Das stellt nach, dass das Spiel mit DirectX 12 abgestürzt ist, ohne dass man es abstürzen lassen muss.

- Auf DirectX 12 umstellen und neu starten (wie Test 2). **Sofort**, innerhalb von 30 Sekunden, das Spiel im Task-Manager beenden (Task beenden). **Erwartet:** `grafikschnittstelle.versuch` bleibt liegen.
- Spiel wieder starten (läuft noch mit DirectX 12, weil die Datei es so will). Einstellungen → Grafik → Erweitert öffnen.
- **Erwartet:** Dort steht der Hinweis „Der letzte Start mit DirectX 12 kam nicht bis zum Ende …“; `grafikschnittstelle.cfg` enthält jetzt `"vulkan"`. Der nächste Start läuft mit Vulkan.

## Test 5: DirectX 12 startet nicht (Godots Rückfall)

Schwer herbeizuführen, wenn die Grafikkarte DirectX 12 kann. Nur prüfen, wenn ein Rechner mit sehr alter Grafikkarte oder Treiber da ist (ohne Feature Level 12_0).

- Dort auf DirectX 12 stellen und neu starten.
- **Erwartet:** Das Spiel startet trotzdem (Godot nimmt von selbst Vulkan, Warnung „switching to Vulkan“ in der Konsole). Der Hinweis in den Einstellungen lautet „DirectX 12 ließ sich auf diesem Rechner nicht starten; das Spiel läuft mit Vulkan und merkt sich das.“ und die Datei steht wieder auf `"vulkan"`.
- Läuft auch Vulkan nicht, kann das Spiel nicht starten; das ist wie ohne diese Funktion.

## Test 6: Klicktest mit DirectX 12 (Bonus)

`tools\klicktest\lauf.ps1 -Treiber d3d12 …` (siehe [ENTWICKLUNG.md](ENTWICKLUNG.md)) nimmt DirectX 12 über die Startoption und rührt `grafikschnittstelle.cfg` nicht an (der Lauf hat sein eigenes `user://`).

## Zu melden

- Bildfehler (Schwarz, Flackern, fehlende Schatten, falsche Farben) mit Bild und der Startzeile mit dem Treiber aus der Konsole.
- Bildrate unter DirectX 12 gegen Vulkan (Entwickler-Fenster F12 → Messwerte).
- Welche Tests nicht liefen und warum.

## Hinweis für den Export (offen, Nutzer entscheidet)

Beim Export nach Windows legt Godot die DirectX-12-Bibliotheken (`D3D12Core.dll`, `d3d12SDKLayers.dll`, „Agility SDK“) nur bei, wenn die Exportvorgabe **„Export D3D12“ auf „Yes“** steht oder die Projekteinstellung selbst DirectX 12 nennt (Quelle: `platform/windows/export/export_plugin.cpp`, Godot 4.5; für 4.7.2 nicht einzeln geprüft). Bei Standard Vulkan und „Auto“ fehlen sie im Export. Empfehlung: in `game/export_presets.cfg` bei der Vorgabe „Windows“ unter `[preset.0.options]` die Zeile `application/export_d3d12=1` ergänzen. Die Datei gehört den Code-Besitzern; deshalb steht die Änderung hier nur als Empfehlung.
