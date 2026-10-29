# Prompts: Einrichtungs-Icons (PR #224, 28.10.2026)

Dateien: `game/assets/oberflaeche/icons/einrichtung_<kennung>.png` (38), Motive in `tools/icons/quellen/motiv/einrichtung_<kennung>.png`.
Protokoll: `docs/assets/protokoll.csv`.

## Werkzeug

- **Motive:** lokale Bild-KI auf dem Grafik-PC des Nutzers: ComfyUI mit **Qwen-Image** (Text zu Bild) und dem Bearbeiten-Workflow (**Qwen-Image-Edit**, Bild plus Vorlage). Aufgerufen über ein Aufrufskript außerhalb des Repositorys, Einstellung `QUALITAET=1`. Modelle (Stand der Abfrage 29.10.2026, nicht belegt, dass die Motive genau damit entstanden): ComfyUI v0.42.0 (Commit 0b05cd8a) mit ComfyUI-GGUF 6ea2651; Text zu Bild qwen-image-2512-Q4_K_M.gguf (Qwen-Image 2512, Apache-2.0) mit Text-Encoder qwen_2.5_vl_7b_fp8_scaled, VAE qwen_image_vae und LoRA Qwen-Image-2512-Lightning-4steps-V1.0-bf16; Bearbeiten qwen-image-edit-2511-Q4_K_M.gguf mit LoRA Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.
- **Rahmen und Hintergründe:** die Gemini-Teile der Waren-Knöpfe aus PR #210 (`tools/icons/quellen/rahmen/`, `hintergrund/`).
- **Zusammensetzen:** `tools/icons/knoepfe_bauen.gd`, Befehl `einrichtung` (Rahmen nach Reihe, Hintergrund nach Spalte des Talentbaums).

## Abläufe

Je Kennung eine Zeile „Kennung | Beschreibung“; die Beschreibung wird in einen festen Rahmen-Prompt eingesetzt. Gab es mehrere Läufe, gilt der letzte (Zuordnung nach den Dateizeiten im Notizordner des Teamleiters und den Commits von PR #224).

**Ablauf A – Bearbeiten mit Stilvorlage.** Vorlage: das Waren-Motiv `tools/icons/quellen/motiv/beschlag.png` (Gemini, PR #210).

> Replace the iron hinge with: `<Beschreibung>` Keep exactly the same drawing style: black ink outlines, cross-hatching in the shadows, soft watercolor colors, one object centered, plain white background. No text, no frame, no hinge.

**Ablauf B – Bearbeiten mit eigenem Motiv als Stilvorlage.** Vorlage: das Rohbild des Motivs „Regale“ aus Ablauf A.

> Remove the shelf completely and draw instead: `<Beschreibung>` It is the 15th century: only medieval materials (wood, iron, leather, copper, clay, glass), nothing modern. Keep exactly the same drawing style: ink outlines, light hatching, soft watercolor, one object centered on a plain white background, no border, no text.

**Ablauf C – zwei Stufen.** Stufe 1 Text zu Bild (Inhalt), Stufe 2 Bearbeiten mit dem Bild aus Stufe 1 als Vorlage und dem Rohbild „Regale“ als Stilreferenz.

> Stufe 1: `<Beschreibung>` Single object centered on a plain white background, late medieval 15th century, illustration, no text, no border.
>
> Stufe 2: Redraw the object of image 1 exactly as it is (same shape and parts) in the drawing style of image 2: ink outlines, light hatching, soft watercolor colors, plain white background, no border, no text.

## Beschreibungen je Kennung (letzter Lauf)

| Kennung | Ablauf | Beschreibung |
|---|---|---|
| regale | A | A wooden storage shelf with stacked boxes and sacks. |
| lehrlingsstube | A | A small stool and a straw bed beside a wooden bench with tools. |
| hausapotheke | A | A clay jar with herbs and a small bottle of healing salve. |
| werkzeug1 | A | A pair of crossed good-quality hammer and tongs on a wooden wall rack. |
| werkzeug2 | A | Crossed master-grade hammer, chisel and tongs with polished steel heads. |
| flaschenzug | A | A wooden block-and-tackle pulley with rope lifting a stone block. |
| destillierhelm | A | A copper alembic still with a curved neck and a small flame beneath. |
| glasflakons | A | Several small Venetian glass perfume bottles on a shelf. |
| braupfanne | A | A large copper brewing kettle with foam and a wooden paddle. |
| gaensekiele | A | Goose quills in an ink pot with a pinch of blue lapis lazuli powder. |
| loeschfass | A | A wooden water barrel with a leather bucket beside it. |
| eisentuer | A | A heavy wooden door reinforced with iron bands and a lock. |
| schuerzen | A | A folded leather apron hanging on a hook. |
| handschuhe | A | A pair of leather work gloves. |
| steinsaege | A | A long two-man stone saw cutting a block of sandstone. |
| gewoelbekeller | A | A vaulted stone cellar with barrels stacked in rows. |
| verkleidung | A | An open chest spilling hoods, masks and a false beard. |
| goldwaage | A | A small brass balance scale with coins on one pan. |
| kassenschrank | A | An iron-bound cash chest with a big padlock and gold coins. |
| vorratskammer | A | A larder shelf with sacks of grain, hams and a cheese. |
| kueche | A | A hearth with a hanging cooking pot and bundles of herbs. |
| stube | A | A cozy parlor with a carved stool, a table and a candle. |
| bibliothek | A | A small shelf with several bound books and a lectern. |
| hauswaechter | A | A watchman with a halberd and lantern standing at a house door. |
| hauswappen | A | A carved stone plaque with a brightly painted heraldic shield, mounted above the stone lintel of a house doorway. |
| hund | A | A stocky short-haired medieval German guard dog of the Bullenbeisser type, broad head and strong jaw, fawn coat with a dark muzzle, wearing a leather collar, chained beside a small wooden kennel. |
| hunde | A | Two stocky short-haired medieval Bullenbeisser guard dogs with broad heads and spiked leather collars, alert and watchful. |
| schleifstein | A | A large round medieval grindstone in a wooden frame with a hand crank, a trough of water below it and a sword blade being sharpened on it. |
| drechselbank | B | A medieval spring-pole lathe: a long wooden bench with two wooden posts holding a half-turned table leg, a cord running up to a bent springy pole above and a foot treadle below. |
| enfleurage | B | A wooden frame holding a glass plate thickly coated with white fat, with fresh rose and jasmine petals pressed into it. |
| ladentheke | B | A plain wooden medieval shop counter with a brass beam balance with two hanging pans, a few silver coins and a small cloth sack on top. |
| leimzwingen | B | A small earthenware glue pot warming on a little iron brazier, with two wooden screw clamps beside it. |
| pfandkammer | B | A heavy medieval oak cupboard with iron bands and a big lock, its open doors showing pawned goods: a silver cup, a folded cloak, a candlestick and a sword. |
| kettenhemd | B | A medieval chain mail shirt made of small interlocking iron rings, with short sleeves, hanging on a wooden stand. |
| riegel | C | A thick hand-forged iron door bolt with a round knob handle, sliding through iron staples across rough wooden planks. |
| athanor | C | A medieval alchemist's athanor: a squat brick furnace with a tall round brick fuel tower attached on one side, a small fire glowing in an opening at the bottom, and on top a round glass flask with a long neck sitting in a bowl of sand. |
| skriptorium | C | A slanted medieval scribe's writing desk with an open illuminated manuscript, a goose quill, an ink horn and a burning candle. |
| blasebalg | C | Side view of a large medieval blacksmith's forge bellows lying horizontally: two teardrop-shaped wooden boards with folded brown leather between them, open like an accordion, a long wooden handle at the wide end and an iron nozzle at the narrow end. |

## Korrekturen auf Wunsch des Nutzers

Schleifstein statt Wasserhammer, Bullenbeißer statt Schäferhund (Hund, Hunde), Athanor neu gezeichnet, kein Ring an der Ladentheke. Abnahme der Motive durch den Nutzer (PR #224); ob er Bilder selbst bearbeitet hat, ist nicht belegt.
