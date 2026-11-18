# Neubau der Gebäude: Wohnhaus Elb, Stufe 1 (Grundformen 1 bis 4)

Keine Bild-KI und keine Image-to-3D-KI. Modell, UV und Texturen entstehen aus Skripten, die Claude (Opus 5.5, Claude Code, Teammitglied „darstellung“) in der Sitzung vom 17.11.2026 geschrieben hat.

**Vorgaben (Auftrag des Teamleiters, vom Nutzer am 17.11.2026 freigegeben):** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Polybudget, Dachstufung, Dual-UV, Trim-Sheet, Möbel-Cluster, `InteriorRoot`), die Konzeptbilder „Wohnhaus Elb, Stufe 1“ (außen `wohnhaus_lvl1_elb`, innen `wohnhaus_in_lvl1_elb`) als reine Stilvorlage, „wie Mensch bzw. Zwerg“ (die vier Grundformen der Menschenhäuser), Türen und Decken für Elben (1,85 m), Menschen und Zwerge, Wappenregel `wappen_ort`, Fahnenmast im Dach verankert, Dreiecksgrenze 4.000 je Haus, Steinebene der Elben (K2 für Flächen). Die Bilder sind kein Teil des Repositorys und wurden nicht verarbeitet, nur angesehen; die Farben sind Proben aus ihnen (Zahlen im Kopf von `trimsheet_elb.py`).

Aus dem Kunstwerk übernommen: Fachwerk aus hellem, grau verwittertem Holz mit Kalkputz in blassem Gelbgrün, gebogene Streben, grau-olivgrünes Schindeldach, an Traufe und First aufgeschwungene Ortgangbretter, Schlot aus hellem Haustein; innen cremeweißer Putz und Dielen. In der ersten Probe nicht gebaut, nach der Rückmeldung des Nutzers (17.11.2026) ergänzt: hochgezogene Dachecken, Spitzbogen außen an Haustür und Giebelöffnung, Efeu und Moos als Textur, Holzunterstand an der Seite (Grundform 1 und 4).

**Ablauf**

1. `tools/modelle/neubau/trimsheet_elb.py` (in Blender, numpy): packt Poly-Haven-Texturen (CC0; `game/assets/texturen/neubau/HERKUNFT.md`) zu `trim_elb` und `innen_trim_elb` mit den Bandgrenzen der Menschenhäuser; die Schindeln sind die gezeichneten der Zwerge, umgetönt.
2. `tools/modelle/neubau/wohnhaus_elb_s1.py --form=<1|2|3|4>` (Blender 4.5, `-b`): Schicht auf die Bauskripte der Menschen (`wohnhaus_mensch_s1_g1.py`, `wohnhaus_mensch_s1.py`, unverändert) mit `elb_teile.py` (gebogene Streben, Schwünge am Ortgang, Wappenregel) und dem Schindeldach von `zwerg_teile.py` ohne Stangen und Findlinge; Elbenmaße (`elb_masse.py`); backt die Verdeckung in die Zelle des AO-Atlas und schreibt Hülle und Wohnstube als GLB.
3. `tools/modelle/neubau/trimsheet_ebenen.py`: Ebene `elb_s1` im Häuser-Array (Band „bruchstein“ K2), `innen_trim_elb` im Innen-Array, die Atlanten in den AO-Arrays.
4. Prüfung: `tools/modelle/neubau/wohnhaus_elb_messen.py` (Prüfungen 1–24), `ebenen_pruefung.py`.

**Eigene Form (Rückmeldung des Nutzers 17.11.2026, „Elben eigene Form“):** Stufe 1 neu aufgesetzt, niedrig und breit wie im Kunstwerk: ein Geschoss und Dachgeschoss unter einem steilen, tief heruntergezogenen, stark geschwungenen Dach, mehr Efeu und Moos; jede Grundform im Fußabdruck der Menschen-Grundform. Weiter ohne Bild- oder 3D-KI.
