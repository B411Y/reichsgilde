# Neubau der Gebäude: Wohnhaus Zwerg, Stufe 1 (Grundformen 1 bis 4)

Keine Bild-KI und keine Image-to-3D-KI. Modell, UV und Texturen entstehen aus Skripten, die Claude (Fable 5.2, Claude Code) in der Sitzung vom 14.11.2026 geschrieben hat.

**Vorgaben des Nutzers:** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (wie beim Wohnhaus der Menschen: Polybudget, Alpha-Cards, Dachstufung, Dual-UV, Trim-Sheet, Möbel-Cluster, `InteriorRoot`) und die Konzeptbilder „Wohnhaus Zwerg, Stufe 1“ (außen `wohnhaus_lvl1_zwerg`, innen `wohnhaus_in_lvl1_zwerg`) als reine Stilvorlage: Erdgeschoss aus behauenen Steinquadern, Obergeschoss in Fachwerk mit Steinfüllung, Schindeldach mit Findlingen auf Rundhölzern, großer Steinschlot, Amboss, Kette, Kohlensäcke und Kohlenkisten; innen Steinwände und Steinpflaster, Herd mit Kessel, Schinken und Kräuter über dem Feuer, Schaufel, Spitzhacke und Hammer an der Wand. Die Bilder sind kein Teil des Repositorys und wurden nicht verarbeitet, nur angesehen. Die vier Grundformen (schmal und hoch, breit, mit Anbau, mit Laube) sind die des Menschenhauses (Entscheidung des Nutzers, 14.11.2026).

**Ablauf**

1. `tools/modelle/neubau/trimsheet_zwerg.py` (in Blender, numpy): packt Poly-Haven-Texturen (CC0; Quellen und Urheber: `game/assets/texturen/neubau/HERKUNFT.md`) zu zwei Trim-Sheets mit den Bandgrenzen der Menschenhäuser; die Schindeln zeichnet das Skript selbst.
2. `tools/modelle/neubau/wohnhaus_zwerg_s1.py --form=<1|2|3|4>` (Blender 5.2, `-b`): legt eine Schicht auf die Bauskripte der Menschen (`wohnhaus_mensch_s1_g1.py`, `wohnhaus_mensch_s1.py`, unverändert): gleiche Maße, Stoff und Dinge der Zwerge (`zwerg_teile.py`: Schindeldach mit Findlingen, Steinschlot, Amboss, Kohle, Werkzeugleiste, Vorrat; `trimsheet_zwerg.py`), Zwergenhöhen bei Tisch, Sitz und Blick; backt die Verdeckung in die Zelle des AO-Atlas der Gruppe; schreibt Hülle und Innenraum als GLB und misst am Ende Soll gegen Ist (`zwerg_masse.py`).
3. Im Spiel setzt `Stadtansicht.Neubau.cs` Trim-Sheet (nach dem Volk des Besitzers), Shader und Farben wie bei den Menschen.

**Stufen 2 und 3, Neuaufbau (16.11.2026, Claude Opus 5.5 in Claude Code):** Konzeptbilder `wohnhaus_lvl2_zwerg` und `wohnhaus_lvl3_zwerg` als reine Stilvorlage (Bürgerhaus: Steinerdgeschoss, Fachwerk mit Steinfüllung, Schieferdach; Steinhaus: dunkles Mauerwerk mit Werkstein aus hellem Haustein, Treppengiebel). `tools/modelle/neubau/wohnhaus_zwerg_s23.py --stufe=<2|3> --form=<1–4>` legt eine Schicht auf `wohnhaus_mensch_s23.py` (unverändert); die Räume `tools/modelle/raeume/raeume_zwerg_s23.py`. Vorgaben des Nutzers im Auftrag: Holzpfosten 25 × 10 cm zwischen Fensterpaaren, keine Ankereisen, Wappenregel, Stein der Zwerge über die Steinebenen, Dreiecksgrenze 4.000. Keine Bild- oder 3D-KI.

**Stand der Menschen und Halblinge (22.11.2026, Claude Fable 5.2 in Claude Code, Teammitglied „darstellung“):** Auftrag des Teamleiters (Nutzer 22.11.2026): die Zwergen-Wohnhäuser Stufe 1 bis 3 auf die Qualität der Menschen- und Halblinghäuser bringen – zwergentypisch, ernsthaft, mittelalterlich, Stein bzw. Quader und schweres Holz, nichts dazuerfinden. Nachgetragen im selben Auftrag: geschlossene Läden im festen Zufall wie in den Zeilenmodulen. Keine Bild-KI, keine Image-to-3D-KI; geändert sind nur Bauskripte (`wohnhaus_zwerg_s1.py`, `wohnhaus_zwerg_s23.py`, `zwerg_teile.py`, `tools/modelle/raeume/raeume_zwerg_s23.py`, `raum_bibliothek_dach.py`), die Skripte der Menschen bleiben bis auf das Kontrollbild unverändert. Gebaut: echtes Fachwerk (Holzlage) in Stufe 1 und 2, dunkler Quader in Lagen der Bandhöhe statt gestreckt, Gaube hinter dem Schornstein (Stufe 2 und 3; im Steinhaus aus den Lagen des Werksteins, das Fenster der Dachbibliothek zieht mit), geschlossene Läden auf fensterlosen Feldern mit 30 % nach fester Saat (`zeile_module.laden_zu`). Alle 12 Hüllen und die 4 Dachbibliotheken der Stufe 3 sind neu gebaut und neu gebacken; die 12 Wohnstuben und ihr AO-Atlas kamen bytegleich aus dem Bau (unverändert).

Rechner und Werkzeug: NAS, Blender 5.2.2 LTS (`~/werkzeuge/blender/blender.sh`), Backen mit Cycles/CUDA (RTX 2000 Ada). Aufruf:

```bash
B="flock /tmp/claude-1000/blender.lock nice -n 19 taskset -c 0-3 $HOME/werkzeuge/blender/blender.sh"
REICHSGILDE_CYCLES=CUDA $B -b --factory-startup --python tools/modelle/neubau/wohnhaus_zwerg_s1.py -- --form=<1..4>
REICHSGILDE_CYCLES=CUDA $B -b --factory-startup --python tools/modelle/neubau/wohnhaus_zwerg_s23.py -- --stufe=<2|3> --form=<1..4>
REICHSGILDE_CYCLES=CUDA $B -b --factory-startup --python tools/modelle/raeume/raeume_zwerg_s23.py -- --haus=wohnhaus_zwerg_s3_g<1..4> --raum=bibliothek
$B -b --factory-startup --python tools/modelle/neubau/wohnhaus_zwerg_neu_messen.py -- --haus=wohnhaus_zwerg_s<1..3>_g<1..4>
PYTHONPATH=<ordner mit Pillow> <python mit numpy> tools/modelle/neubau/trimsheet_ebenen.py --array=haeuser --nur-ao
PYTHONPATH=<ordner mit Pillow> <python mit numpy> tools/modelle/neubau/trimsheet_ebenen.py --array=innen --nur-ao
```

Kontrollbilder: `--bilder=<ordner>`, mit `REICHSGILDE_BILDER=aussen_vorn,aussen_rechts` nur die genannten. Nachbearbeitung von Hand: keine. Entscheidungen und Messwerte: `docs/bereiche/darstellung.md`, Abschnitt „Zwergen-Wohnhäuser auf dem Stand der Menschen und Halblinge“.
