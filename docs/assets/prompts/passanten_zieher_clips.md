# Quelle: Zieher-Clips der Passanten (Karren-Auftrag Teil c, 10.11.2026)

Dateien: `game/assets/modelle/figuren/probe/passant_probe.glb`, `passant_probe_frau.glb`, `passant_probe_ork.glb`, `passant_probe_oger.glb` (je drei neue Animationen hinter „Betteln“: `Schieben`, `Nebenhergehen`, `SitzenBock`). Protokoll: `docs/assets/protokoll.csv` (Zeile `passant_probe*.glb`); Herkunft je Teil: `game/assets/modelle/figuren/probe/HERKUNFT.md`.

Kein KI-Werkzeug und kein Prompt: gebaut mit `tools/modelle/passanten_clips.py` (Blender `-b`, ohne MPFB und ohne neue Quaternius-Datei). Das Skript liest die fertigen `.glb`, leitet die neuen Clips aus den schon vorhandenen Clips `Gehen`, `Stehen` und `Sitzen` ab und kopiert nur die neuen Animationen binär in die Dateien (Netze, Stoffe und die neun bisherigen Clips bleiben unverändert; Rundreise von `Gehen` und `Stehen` bis auf 0,05 Grad gleich).

| Clip | Ableitung |
|---|---|
| `Schieben` | `Gehen` (Beine, Becken, Boden), Oberkörper 18 Grad nach vorn, beide Hände fest am Griff des Handkarrens (Zweigelenk-Rechnung, Handflächen nach unten); 16 Zeilen in der VAT-Textur |
| `Nebenhergehen` | `Gehen`, rechter Arm aus dem ersten Bild von `Stehen` (dort steht der Stecken `Zub_stab` senkrecht in der Faust) und 14 Grad nach vorn gehoben; 16 Zeilen |
| `SitzenBock` | `Sitzen` (Atmen), beide Hände vor dem Bauch an den Zügeln mit leichtem Schwanken; 10 Zeilen (6 Bilder/s) |

Die Quaternius-Bibliothek, aus der `Gehen`, `Stehen` und `Sitzen` stammen, ist die **Universal Animation Library [Standard]** (`Walk_Loop`, `Idle_Loop`, `Sitting_Idle_Loop`; Archiv-SHA-256 18FF1A7215F4852B320203E8AAF02A1578B5C8EEF9027FBAEDFCEDC7B85A3AC2). `Push_Loop` derselben Bibliothek wurde für `Schieben` probiert und **verworfen** (Stemmen gegen eine Wand: Oberkörper fast 50 Grad vorn, Hände am Kopf, Schritt 0,45 m/s); in die Dateien ging davon nichts.

Lizenz der Quaternius-Bibliothek, wörtlich aus `License.txt` im Paket (gelesen 10.11.2026): „CC0 1.0 Universal (CC0 1.0) Public Domain Dedication https://creativecommons.org/publicdomain/zero/1.0/“; „Models by @Quaternius“. Seite https://quaternius.com/packs/universalanimationlibrary.html, abgerufen 10.11.2026: „Free to use in personal, educational and commercial projects. (CC0 License)“.

Nichts aus „Die Gilde“, kein Human Generator, kein Kimodo.
