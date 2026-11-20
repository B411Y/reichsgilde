# Kartenbau-MCP-Server

Ein kleiner [MCP](https://modelcontextprotocol.io)-Server, mit dem eine KI (z. B. Claude Code) Karten über Werkzeuge auflisten, lesen, bauen, prüfen und als Bild ansehen kann. Er ruft dieselben Befehle auf wie `tools/Kartenbau` (Kartenskript) und greift **nicht** ins laufende Spiel oder den Godot-Editor ein.

- Code: `tools/kartenbau-mcp/` (C#/.NET 8, MIT). Bibliotheken: `ModelContextProtocol` 1.4.2 (offizielles C#-SDK, Apache-2.0), `Microsoft.Extensions.Hosting` (MIT) und `Svg.Skia` 5.2.3 mit SkiaSharp (beide MIT; SVG zu PNG, native Linux-Bibliothek als NuGet-Paket).
- Nur **stdio**, kein Netz-Port. Der Server liest die Anfragen von stdin und antwortet auf stdout; Meldungen gehen nach stderr.

## Starten

```bash
dotnet build -c Release tools/kartenbau-mcp/KartenbauMcp.csproj
dotnet tools/kartenbau-mcp/bin/Release/net8.0/KartenbauMcp.dll        # wartet auf einen MCP-Client (stdio)
```

Den Projektordner (mit `game/daten/katalog.json`) findet er vom Arbeitsordner aus; sonst `KARTENBAU_MCP_PROJEKT=<Pfad zu reichsgilde>` setzen. Der Arbeitsordner für Entwürfe und Bilder ist `<Temp>/kartenbau-mcp` (einstellbar mit `KARTENBAU_MCP_ARBEIT`).

## Einrichtung für Claude Code (Vorschlag, nicht im Repository)

Eine `.mcp.json` im Ordner `reichsgilde` (bewusst nicht eingecheckt; zuerst bauen, siehe oben):

```json
{
  "mcpServers": {
    "kartenbau": {
      "command": "dotnet",
      "args": ["tools/kartenbau-mcp/bin/Release/net8.0/KartenbauMcp.dll"]
    }
  }
}
```

Oder auf der Kommandozeile: `claude mcp add kartenbau -- dotnet tools/kartenbau-mcp/bin/Release/net8.0/KartenbauMcp.dll`.

## Werkzeuge

| Werkzeug | Was es tut | Befehl dahinter |
|---|---|---|
| `karten_auflisten` | Karten in `game/karten`, Entwürfe im Arbeitsordner, Stadtskripte und Umbauten | `liste` |
| `karte_lesen(karte)` | Größe, Zonen, Wege, Gebäude und die Übersicht der Prüfung | `Befehle.Bericht` (wie `pruefen`) |
| `karte_bauen(skript, schreiben=false, ueberschreiben=false)` | Stadtskript ausführen; ohne `schreiben` nur ein Entwurf im Arbeitsordner (auch mit Fehlern), mit `schreiben` nach `game/karten/<kennung>.json` (nur ohne Fehler, über eine vorhandene Karte nur mit `ueberschreiben`) | `bauen` |
| `karte_pruefen(karte)` | Befunde als Liste (Fehler und Hinweise, mit Regel), dazu Katalog gegen die Modelle | `pruefen` |
| `karte_bild(karte, belegung=false)` | Vorschaubild; immer ein PNG (höchstens 1600 Pixel breit, Beschriftungen fehlen ohne Systemschriften); nur wenn das Zeichnen scheitert, das SVG als Text (bis 100 KB) und der Dateipfad | `vorschau` |

`karte` ist die Kennung (`greybox`, `koeln` …) oder der Name eines Entwurfs im Arbeitsordner.

## Sicherheit

- Kennungen sind nur `a-z`, `0-9`, `_` (höchstens 40 Zeichen); Pfade, `..` und Schrägstriche lehnt der Server ab.
- Geschrieben wird nur nach `game/karten/` (nur bei `karte_bauen` mit `schreiben`) und in den Arbeitsordner (Entwürfe, Bilder). Die Vorschaubilder gehen in den Arbeitsordner, **nicht** nach `docs/karten`; wer die eingecheckten Bilder erneuern will, nimmt `tools/Kartenbau` selbst.
- Fehler kommen als Text zurück, nicht als Absturz.

## Beispiel-Ablauf

1. `karten_auflisten` – es gibt `greybox`, `koeln`, `messstadt`, `probestadt` und die Stadtskripte.
2. `karte_lesen karte=greybox` – 400 × 400 m, 15 Bauzonen, 33 Wege, 62 Gebäude.
3. `karte_bauen skript=probestadt` – Entwurf in den Arbeitsordner; die Übersicht zeigt, ob die Karte Fehler hat.
4. `karte_pruefen karte=probestadt` – Befunde; Skript ändern, erneut bauen.
5. `karte_bild karte=probestadt` – Bild ansehen; erst wenn alles stimmt, `karte_bauen … schreiben=true`.

## Tests

```bash
dotnet run -c Release --project tools/kartenbau-mcp/Tests                                   # die Werkzeuge direkt (Greybox lesen, prüfen, Bild, Ausbruch)
python3 tools/kartenbau-mcp/Tests/probe_client.py tools/kartenbau-mcp/bin/Release/net8.0/KartenbauMcp.dll .   # echter MCP-Ablauf über stdio
```

Die Tests laufen nicht in der CI und nicht in `Reichsgilde.sln` (eigenes Projekt, holt ein NuGet-Paket); sie gehören zu den Werkzeugen, nicht zum Spiel.
