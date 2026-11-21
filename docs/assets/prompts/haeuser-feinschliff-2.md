# Häuser-Feinschliff 2 (20.11.2026): erstmals ganz auf der NAS gebaut

Keine Bild-KI und keine Image-to-3D-KI. Geändert sind Bauskripte und Prüfungen unter `tools/modelle/neubau/`; Claude (Fable 5.2, Claude Code, Teammitglied „darstellung“) hat sie in der Sitzung vom 20.11.2026 geschrieben und die Häuser damit neu gebaut. Auftrag des Teamleiters (Nutzer, 20.11.2026): fünf Punkte aus den Klicktests der NAS (schwarze Balkenseiten, Blickpunkte, Verdeckung der Zwergenhäuser, Giebelzier der Elben).

**Rechner und Werkzeug:** NAS, Blender 5.2.2 LTS (`~/werkzeuge/blender/blender.sh`), Cycles mit CUDA (RTX 2000 Ada). Bisher baute der Grafik-PC die Häuser mit Blender 4.5.15 bzw. 5.2.3. Smart UV Project legt die UV2 auf der NAS anders als dort: Ein Haus, das hier neu gebaut wird, passt nicht mehr zu seiner Zelle im AO-Atlas des Repositorys. Deshalb ist jedes angefasste Haus **ganz** neu gebaut und neu gebacken (kein `--ohne-ao`).

**Was neu gebaut ist (34 von 48 Häusern, je Hülle und Wohnstube):**

| Häuser | Anlass |
|---|---|
| Halbling Stufe 1, Grundform 1 und 3 | Galerie auf vorderem Balken und Wandbalken, Grundlicht unter der Galerie, Grundform 3 neuer Blickpunkt und Vorräte an der Rückwand (Nutzer 20.11.2026, „Halbling A“) |
| Mensch und Zwerg Stufe 2 und 3, alle Grundformen | Grundlicht der Wohnstube über dem Blickpunkt |
| Zwerg Stufe 1, alle Grundformen | Hülle und Verdeckung aus einem Bau (Atlas neu gebacken) |
| Elb Stufe 1 bis 3, alle Grundformen | gröbere Giebelzier; Stufe 1 Grundform 3 neuer Blickpunkt |

Unverändert vom Grafik-PC: Mensch Stufe 1, Halbling Stufe 1 Grundform 2 und 4, Halbling Stufe 2 und 3, alle Hinterzimmer und Bibliotheken.

**Aufruf (NAS)**

```bash
B="flock /tmp/claude-1000/blender.lock nice -n 19 taskset -c 0-3 $HOME/werkzeuge/blender/blender.sh"   # ein Blender-Lauf zur Zeit: jeder Aufruf einzeln über die gemeinsame Sperre
REICHSGILDE_CYCLES=CUDA $B -b --factory-startup --python tools/modelle/neubau/wohnhaus_<volk>_s1.py -- --form=<1..4>
REICHSGILDE_CYCLES=CUDA $B -b --factory-startup --python tools/modelle/neubau/wohnhaus_<volk>_s23.py -- --stufe=<2|3> --form=<1..4>
$B -b --factory-startup --python tools/modelle/neubau/wohnhaus_<mensch|zwerg_neu|elb|halbling>_messen.py -- --haus=wohnhaus_<volk>_s<1..3>_g<1..4>
PYTHONPATH=<ordner mit Pillow> <python mit numpy> tools/modelle/neubau/trimsheet_ebenen.py --array=haeuser --nur-ao
PYTHONPATH=<ordner mit Pillow> <python mit numpy> tools/modelle/neubau/trimsheet_ebenen.py --array=innen --nur-ao
```

Die Messung der Halblinge (Prüfung 37) braucht Pillow im Blender-Python: `PYTHONPATH=<ordner mit Pillow>` davor und `--python-use-system-env` hinter `--factory-startup`. OptiX wurde nicht ausprobiert (CUDA genügte: 1 bis 4 min je Haus).

`REICHSGILDE_CYCLES=CUDA|OPTIX` schaltet das Backen auf die Grafikkarte (`wohnhaus_mensch_s1_g1.cycles_geraet`); ohne die Variable bleibt es bei der CPU wie bisher. `--nur-ao` packt nur `<array>_ao_ebenen.png` neu aus den Atlanten (ohne die Steintexturen, die nicht im Repository liegen); die Ebenen der nicht gebackenen Gruppen bleiben bildpunktgleich.

**Nachbesserung (21.11.2026, Rückmeldung des Nutzers; dieselbe Sitzung, Claude Fable 5.2):** fünf Punkte, wieder nur Skripte. (1) Giebelzier der Elben in der Akzentfarbe des Hauses (Band „farbholz“, `elb_teile.ortgang_schwung`). (2) Elb Stufe 1 Grundform 3: Rückwand eingerichtet (`wohnhaus_elb_s1.rueckwand_einrichten`). (3) Mensch und Zwerg Stufe 2/3: Grundlicht der Wohnstube in 1,70 m (`wohnhaus_mensch_s23.LICHT_UNTER_DECKE`) und zwei Anker für ein Deckenlicht (`Licht_Decke`, `Licht_Decke_Ziel`; der Strahler entsteht im Spiel). (4) Efeu der Elben als Blattkarten: neues Modul `tools/modelle/neubau/efeu_karten.py` (Stoff „Efeublatt“); die acht Blattbilder sind die der Halblinge Stufe 3 (`trimsheet_halbling.efeu_blaetter`, eigene prozedurale Zeichnung vom 19.11.2026), keine neue Textur – der Shader liest für Efeublatt diese Ebene (`efeu_ebene`). (5) Fußpfette unter den Sparrenfüßen in allen Wohnstuben der Stufe 1 (`PFETTE` in `wohnhaus_mensch_s1_g1.py`, `wohnhaus_mensch_s1.py`). Neu gebaut und gebacken: alle 16 Häuser der Stufe 1, Mensch und Zwerg Stufe 2/3 (16), Elb Stufe 2/3 (8); unverändert bleiben Halbling Stufe 2/3 und alle Hinterzimmer und Bibliotheken.

**Nachbearbeitung:** keine von Hand. Die Verdeckung mittelt, füllt und legt `uv2_und_ao` in die Zelle (wie bisher).

**Quellen:** keine neuen. Entscheidungen und Messwerte: `docs/bereiche/darstellung.md`, Abschnitt „Häuser-Feinschliff 2“.
