# Briefe für Meldungen (3D-Modelle, eigenes Skript)

Kein KI-Modell, kein Prompt: Die Modelle entstehen rein aus Rechnung und Formen in `tools/modelle/brief3d/brief_bau.py` (Blender 5.2.3, `-b`).

Auftrag (Nutzer, 17.11.2026, über den Teamleiter): Um 1400 gab es für Briefe keine Schriftrollen und keine Umschläge, sondern gefaltete Briefe aus Hadernpapier mit Wachssiegel. Die Meldungen werden Briefe (die Schriftrolle bleibt für später im Spiel).

- `brief_klein.glb`: kleiner Faltbrief (Symbol der Meldungsleiste), Dreierfalz mit Wachssiegel; Siegel und Symbolfläche sind eigene Netze und Materialien, damit die Oberfläche Farbe und Symbol je Meldungsart setzen kann; etwa 1.000 Dreiecke.
- `brief_gross.glb`: großer Brief mit Clips `auffalten` (Siegel bricht, Falze klappen auf, flaches Blatt liegt frontal) und `zufalten`; Inhaltsfläche für die Tafel; Blatthöhe streckbar (Streckknochen).
- Gleiche Machart wie der 3D-Handschuh und die Schriftrolle: schwarzer Umriss als invertierte Hülle, nur Albedo, Knochengewichte 1,0.

Textur: Hadernpapier (1024 x 1536 groß, 512 x 768 klein) wird im Skript mit numpy aus festen Saaten gemalt (Wertrauschen, Stockflecken, Hadernreste, Rippung, Faserrand, Falzlinien); keine Fremdbilder, keine KI.
