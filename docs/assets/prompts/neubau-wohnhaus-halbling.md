# Neubau der Gebäude: Wohnhaus Halbling, Stufe 1 (Grundformen 1 bis 4)

Keine Bild-KI und keine Image-to-3D-KI. Modell, UV und Texturen entstehen aus Skripten, die Claude (Opus 5.5, Claude Code, Teammitglied „darstellung“) in der Sitzung vom 17./18.11.2026 geschrieben hat.

**Vorgaben (Auftrag des Teamleiters, vom Nutzer am 17.11.2026 freigegeben):** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Polybudget, Dachstufung, Dual-UV, Trim-Sheet, Möbel-Cluster, `InteriorRoot`), die Konzeptbilder „Wohnhaus Halbling, Stufe 1“ (außen `wohnhaus_lvl1_halbling`, innen `wohnhaus_in_lvl1_halbling`) als reine Stilvorlage, eigene volkstypische Form nach dem Kunstwerk im Fußabdruck der Menschen-Grundformen, Türen und Decken auch für Menschen, Wappenregel `wappen_ort`, Fahnenmast im Dach verankert, Feuerstelle ganz aus Stein, Dreiecksgrenze 4.000 je Haus, Steinebene der Halblinge (K2 für Flächen, K5 nur als Akzent). Die Bilder sind kein Teil des Repositorys und wurden nicht verarbeitet, nur angesehen; die Farben sind Proben aus ihnen (Zahlen im Kopf von `trimsheet_halbling.py`).

Aus dem Kunstwerk übernommen: anderthalbgeschossiges Fachwerkhaus in Menschengröße, dickes goldgelbes Strohdach, das am Giebel als Wulst über die Kante läuft, darunter Pfettenköpfe; Zwerchhaus an der Traufseite mit eigenem Strohdach und verbretterter Front; Haustür mit Rundbogen; rundes Giebelfenster (Ochsenauge) über dem Kehlbalken; warmes Eichenfachwerk, ockergelber Lehmputz; innen warmer Putz, Dielenboden, Herd und Schlot aus Stein, Schlafboden über der Leiter. Nicht übernommen: der rote Backstein an Sockel, Schlot und Herd (die Steinebene der Halblinge sieht K2 vor; Designfrage im Bericht), Körbe, Karren und Fässer im Hof.

**Ablauf**

1. `tools/modelle/neubau/trimsheet_halbling.py` (Python mit numpy und Pillow): liest die fertigen Blätter der Menschen (`trim`, `innen_trim`), tönt die Bänder auf die Farbproben um und setzt K2 (`large_sandstone_blocks_01`, Poly Haven, CC0) ins Steinband; schreibt `trim_halbling` und `innen_trim_halbling`.
2. `tools/modelle/neubau/wohnhaus_halbling_s1.py --form=<1|2|3|4>` (Blender 4.5, `-b`): Schicht auf die Bauskripte der Menschen (`wohnhaus_mensch_s1_g1.py`, `wohnhaus_mensch_s1.py`, unverändert) mit `halbling_teile.py` (Strohdach mit Wulst, Pfettenköpfe, Zwerchhaus, Rundbogen, Ochsenauge); Halblingsmaße (`halbling_masse.py`); backt die Verdeckung in die Zelle des AO-Atlas und schreibt Hülle und Wohnstube als GLB.
3. `tools/modelle/neubau/trimsheet_ebenen.py`: Ebene `halbling_s1` im Häuser-Array (Band „bruchstein“ K2), `innen_trim_halbling` im Innen-Array, die Atlanten in den AO-Arrays.
4. Prüfung: `tools/modelle/neubau/wohnhaus_halbling_messen.py` (Prüfungen 1–36), `ebenen_pruefung.py`.
