"""Beschriftungsbild fuer klinke_bearbeiten.blend / klinke_fertig.blend: je Haltung (Bild 1 Bereit, 2 Greifen, 3 Gedrueckt) Zeiger- und Seitenansicht, Handschuh deckend,
Knochen als farbige Linien mit Namen. KEIN GUI-Screenshot, sondern im Hintergrund gerendert.
  blender -b klinke_bearbeiten.blend --python klinke_selbst_bild.py -- <aus.png> [px]
Danach: python -I klinke_selbst_bild.py --blatt <rohordner> <aus.png> (setzt die Rohbilder und Beschriftungen zusammen; Pillow)."""
import sys, os, json
HIER = os.path.dirname(os.path.abspath(__file__))
if "--blatt" in sys.argv:
    from PIL import Image, ImageDraw
    roh = sys.argv[sys.argv.index("--blatt") + 1]; aus = sys.argv[sys.argv.index("--blatt") + 2]
    dat = json.load(open(os.path.join(roh, "_knochen.json")))
    px = 450; B = Image.new("RGB", (px * 3, px * 2 + 20), (230, 220, 190)); d = ImageDraw.Draw(B)
    FARBE = {"Wurzel": (200, 0, 0), "wrist.R": (200, 80, 0), "finger1": (0, 120, 0), "finger2": (0, 0, 200), "finger3": (0, 120, 160), "finger4": (140, 0, 140), "finger5": (120, 80, 0)}
    for j, f in enumerate((1, 2, 3)):
        for i, an in enumerate(("zeiger", "seite")):
            im = Image.open(os.path.join(roh, f"b{f}_{an}.png")).convert("RGBA").resize((px, px)); bb = Image.new("RGBA", (px, px), (230, 220, 190, 255)); bb.alpha_composite(im)
            B.paste(bb.convert("RGB"), (j * px, i * px + 20))
            for n, (h, t) in dat[f"{f}_{an}"].items():
                fa = FARBE.get(n.split("-")[0].split(".")[0], (90, 90, 90))
                x0, y0, x1, y1 = j * px + h[0] * px, i * px + 20 + (1 - h[1]) * px, j * px + t[0] * px, i * px + 20 + (1 - t[1]) * px
                d.line([x0, y0, x1, y1], fill=fa, width=2)
                if an == "zeiger" and (n in ("Wurzel", "wrist.R") or n.endswith(("-1.R",))): d.text((x1 + 2, y1 - 5), n.replace(".R", ""), fill=fa)
        d.text((j * px + 6, 3), f"Bild {f}: " + ("Bereit" if f == 1 else "Greifen" if f == 2 else "Gedrueckt"), fill=(40, 30, 20))
    B.save(aus); print("Blatt", aus); sys.exit(0)
import bpy
from bpy_extras.object_utils import world_to_camera_view
sys.path.insert(0, HIER)
import v6_rig_lib as L
aus = sys.argv[sys.argv.index("--") + 1]; px = int(sys.argv[sys.argv.index("--") + 2]) if len(sys.argv) > sys.argv.index("--") + 2 else 450
roh = os.path.splitext(aus)[0] + "_roh"; os.makedirs(roh, exist_ok=True)
sc = bpy.context.scene; rig = bpy.data.objects["Handschuh_Geruest"]
sc.render.engine = "BLENDER_EEVEE"; sc.render.resolution_x = sc.render.resolution_y = px
r = L.buehne(px)
cams = {"zeiger": bpy.data.objects["Ansicht_Zeiger"], "seite": bpy.data.objects["Ansicht_Seite"]}
for o in bpy.data.objects:
    if o.name.startswith("Hilfe_Marke"): o.hide_render = True
dat = {}
for f in (1, 2, 3):
    sc.frame_set(f); bpy.context.view_layer.update()
    for an, c in cams.items():
        sc.camera = c; sc.render.filepath = os.path.join(roh, f"b{f}_{an}.png"); bpy.ops.render.render(write_still=True)
        dat[f"{f}_{an}"] = {pb.name: [list(world_to_camera_view(sc, c, rig.matrix_world @ pb.head)[:2]), list(world_to_camera_view(sc, c, rig.matrix_world @ pb.tail)[:2])]
                            for pb in rig.pose.bones if not pb.name.startswith("Knoechel")}
json.dump(dat, open(os.path.join(roh, "_knochen.json"), "w"))
print("ROHBILDER fertig", roh)
