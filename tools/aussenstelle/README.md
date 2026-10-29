# Außenstelle auf dem Grafik-PC

Werkzeuge für die Claude-Code-Sitzung „Aussenstelle“ auf dem Windows-PC (Godot-Klicktests, Messungen). Die Außenstelle darf nur arbeiten, wenn die KI-Dienste ruhen und du es erlaubst; du hast jederzeit einen Notschalter. Alles läuft ohne Administratorrechte und ohne Netzwerk (geprüft werden nur lokale Ports).

## Einrichten (einmal)

1. Eingabeaufforderung oder PowerShell im Ordner `tools\aussenstelle` des Außenstellen-Klons öffnen (z. B. `C:\Reichsgilde_Aussenstelle\reichsgilde`).
2. `powershell -NoProfile -ExecutionPolicy Bypass -File einrichten.ps1`
3. Das Skript nimmt den Ordner in deinen **Benutzer-PATH** auf, legt auf dem Desktop die Verknüpfung **„Reichsgilde – PC zurückholen“** an und schreibt die **Wurzel** des Notschalters nach `%LOCALAPPDATA%\Reichsgilde\wurzel.txt`. Offene Fenster einmal neu öffnen. Mehrfaches Ausführen ändert nichts doppelt.
4. Die Wurzel ist der Elternordner des Repos, also `C:\Reichsgilde_Aussenstelle` mit allen Klonen und Worktrees darunter, egal aus welchem Klon du einrichtest. Eine andere Wurzel: `einrichten.ps1 -Wurzel D:\Anderer_Ordner`. Fehlt `wurzel.txt`, gilt ebenfalls der Elternordner des Repos.

## Notschalter

- Doppelklick auf die Desktop-Verknüpfung, oder in einem Fenster `reichsgilde-stopp`.
- Das legt die Sperrdatei `%LOCALAPPDATA%\Reichsgilde\STOPP` an und beendet sofort die Prozesse der Außenstelle (siehe unten).
- Meldung: „Reichsgilde-Außenstelle gestoppt. Neue Aufträge sind gesperrt, bis du ‚reichsgilde-frei‘ eingibst.“

### Was der Notschalter beendet

- Prozesse, deren Name mit `godot` oder `dotnet` beginnt **und** deren Befehlszeile oder Programmpfad in der Wurzel liegt (`C:\Reichsgilde_Aussenstelle\…`).
- Dazu deren Kindprozesse namens `godot*`, `dotnet*`, `msbuild*`, `vbcscompiler*` oder `testhost*` (Build- und Testhelfer).
- Nicht beendet werden andere Programme, auch nicht in der Wurzel (z. B. PowerShell, Git, Claude Code selbst), und Godot außerhalb der Wurzel (dein Editor in einem anderen Ordner bleibt unberührt). Die Sperrdatei hält die Außenstelle trotzdem an: Sie fragt nicht mehr und bricht lange Aufgaben beim nächsten `pruefen` ab.

## Wieder freigeben

`reichsgilde-frei` entfernt die Sperrdatei. Danach darf die Außenstelle wieder fragen.

## Entfernen

`powershell -NoProfile -ExecutionPolicy Bypass -File entfernen.ps1` nimmt den Ordner aus dem PATH und löscht die Verknüpfung. Log- und Sperrdatei bleiben (bei Bedarf `%LOCALAPPDATA%\Reichsgilde` von Hand löschen).

## Logdatei

`%LOCALAPPDATA%\Reichsgilde\aussenstelle.log`: je Entscheidung eine Zeile mit Zeitstempel (erlaubt, abgelehnt, Zeitablauf, KI läuft, gesperrt, Stopp, Freigabe).

## Regeln, die dahinterstehen

1. Läuft ComfyUI (8188), ComfyUI-3D (8189), TTS (8190) oder AssetFurnace (8777), startet die Außenstelle nichts.
2. Bist du am PC (Eingabe in den letzten 10 Minuten), erscheint ein Fenster mit Ton und 3 Minuten Frist. Ohne Antwort gilt „Ablehnen“. Die Eingabetaste lehnt ab.
3. Bist du länger als 10 Minuten untätig, ist es erlaubt, ohne Frage.
4. Der Notschalter gilt vor allem anderen.

## Für die Außenstelle

Vor **jedem** schweren Auftrag (Godot, Klicktest, Messung, Build):

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\aussenstelle\frage.ps1 -Aufgabe "Klicktest Marktplatz" -Minuten 15
```

(oder kurz `reichsgilde-frage -Aufgabe "..." -Minuten 15`). Ausgang beachten:

| Code | Bedeutung | Was tun |
|---|---|---|
| 0 | erlaubt | Auftrag starten |
| 1 | abgelehnt oder keine Antwort | nichts starten, später neu fragen |
| 2 | KI läuft | nichts starten, später neu fragen |
| 3 | gesperrt | nichts starten, nicht erneut fragen; dem Teamleiter melden |

Bei langen Aufgaben zwischendurch (z. B. alle paar Minuten, zwischen Teilschritten):

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\aussenstelle\pruefen.ps1
```

Code 0 heißt weiter, Code 3 heißt sofort abbrechen (Godot- und dotnet-Prozesse beenden, nichts Neues starten), weil die Sperrdatei da ist.
