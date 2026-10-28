# Versuch: Porträtköpfe aus Human Generator 3D

Live-3D-Porträts (Brustbild, sprechend, blinzelnd) aus **Human Generator 3D** (HG, Blender-Add-on), alles per Skript.
HG steht unter der Personal-Lizenz des Nutzers: Add-on, Inhaltspakete und daraus erzeugte Köpfe (`.glb`, Texturen)
liegen nur unter `~/werkzeuge/humgen/` bzw. `game/versuch/hg/koepfe/` und kommen **nie** ins Repository (`.gitignore`).

## Einrichtung (ohne root)

- Blender 4.5 LTS portabel unter `~/werkzeuge/humgen/blender-4.5/`, Aufruf `~/werkzeuge/humgen/blender_hg.sh`
  (eigener Benutzerordner `~/werkzeuge/humgen/user`, EGL der T400 wie bei MPFB).
- Add-on in `user/scripts/addons/HumGen3D`, Inhalte (ohne 8K-Texturen) in `~/werkzeuge/humgen/inhalt`;
  Pfad und Aktivierung setzt `einrichten.py` (`blender_hg.sh -b --factory-startup -P einrichten.py`).

## Ablauf

| Schritt | Skript | Dauer |
|---|---|---|
| A: Mensch erzeugen (Vorlage, 8K-Haut, Hautregler, Bartschatten, Iris/Lederhaut, Frisur, Bart, Haarfarbe, Gesichtsrig, Haarkarten) | `hg_erzeugen.py -- presets/<x>.json <x>_a.blend` | ~1–2 min |
| Rassenform „Elb“: Anpassung an die TRELLIS-Elbin (Landmarken, ICP, nicht-starr), davon nur Wangenknochen, Augenpartie und schmaleres Kinn; dazu HG-Regler (Wangenknochen, Augenneigung, Kinn) und ein parametrisches Elbenohr (obere Ohrmuschel gestreckt und verjüngt, Knorpel bleibt) | `hg_elbform.py -- <ef3r_d.glb> <inhalt>/livekeys/special/rasse/elb.npz` | ~15 s |
| B: Brustbild für das Spiel (Formen, Zuschnitt, Kittel, Backen, Verschlanken, GLB) | `kopf.sh <x>_a.blend <x>.glb [schnell]` (liest `kopf.json`) | 1–3 min |
| Ansehen in Godot | `nah.sh`, `film.sh`, `schau.sh … --modus=tafel/regler/mess` | |

`kopf.json`: Regler (HG-Livekeys als Blendshapes, u. a. `alter`, `elb`), Zuschnitt, Kittelfarbe, Texturgrößen,
Haardichte. `glb_schlank.py` dünnt danach die Normalen der Formen aus (Datei etwa halbiert).

## Godot

`game/versuch/hg/`: `hg_kopf.gd` (Viseme a/e/i/o/u/m/f/l/s aus ARKit-Formen, Blinzeln, Atmen, Blick, Regler,
Hauttypen je Volk `setze_hauttyp()`), `haut.gdshader` (Wrap-Streulicht, Poren, Tönung je Volk; UV2.y = Kittelmaske),
`haar.gdshader` + `haar_saum.gdshader` + `haar_licht.gdshaderinc` (Kajiya-Kay-Glanz längs der Strähne, Alpha-to-Coverage,
weicher Saum als zweiter Durchgang, dunkler Ansatz über UV2.x), `augen.gdshader`,
`hg_fenster.gd` (Porträtfenster mit eigenem SubViewport), `haar.gdshader` (Haarkarten), `hg_schau.tscn` (Vorführung).
Köpfe mit eigenen Formwerten: `--kopf=res://versuch/hg/koepfe/frau.glb@elb:1;alter:0.5;haut:elb`
(Modi `nah`, `film`, `tafel`, `reihe`, `regler`, `mess`).

## Stand Runde 3 (Aufrufe)

```bash
kopf.sh arbeit/mann_a.blend arbeit/mann/mann.glb voll '{"koerper_dezimieren":0.33,"kittel_dezimieren":0.3,"dezimieren":{"augen":0.25,"zaehne":0.07}}'
kopf.sh arbeit/frau_a.blend arbeit/frau/frau.glb voll '{"koerper_dezimieren":0.45,"kittel_dezimieren":0.3,"regler_dazu":{"alter":"livekeys/special/age/aged_female.npz"},"haut_varianten":{"mittel":"textures/female/Default 8K/Female 06.png","dunkel":"textures/female/Default 8K/Female 10.png"}}'
koepfe_kopieren.sh mann frau      # GLB + Hautvarianten nach game/versuch/hg/koepfe/
eevee_form.py                     # schnelle Blender-Ansicht einer Form (z. B. Elbenohr) ohne Godot
```

TressFX (zurückgestellt, Addon braucht glibc 2.43): `hg_tfx.py` wandelt HG-Partikelhaar in `.tfx`/`.tfxbone`,
`game/versuch/hg/hg_tressfx.gd` hängt die Strähnen an (`--tressfx=<ordner>`); Addon und Strähnen nur lokal.
