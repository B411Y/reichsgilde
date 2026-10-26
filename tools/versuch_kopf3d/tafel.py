# Bilder zu einer Tafel zusammensetzen (ohne PIL, mit Blenders Bild-API und numpy).
# Aufruf: blender -b --python tafel.py -- --aus=<png> --spalten=3 --hoehe=512 bild1.png bild2.png ...
import bpy, sys, numpy as np


def laden(pfad, hoehe):
    img = bpy.data.images.load(pfad)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    # nächster-Nachbar-Skalierung auf Zielhöhe (genügt für Vergleichstafeln)
    breite = int(round(w * hoehe / h))
    ys = (np.arange(hoehe) * h / hoehe).astype(int)
    xs = (np.arange(breite) * w / breite).astype(int)
    return px[ys][:, xs]


def speichern(px, pfad):
    h, w = px.shape[:2]
    img = bpy.data.images.new("tafel", w, h, alpha=True)
    img.pixels[:] = px.astype(np.float32).ravel()
    img.filepath_raw = pfad
    img.file_format = "PNG"
    img.save()


def tafel(bilder, spalten, hoehe, rand=8, grund=(1, 1, 1, 1)):
    kacheln = [laden(p, hoehe) for p in bilder]
    breite = max(k.shape[1] for k in kacheln)
    zeilen = (len(kacheln) + spalten - 1) // spalten
    H = zeilen * hoehe + (zeilen + 1) * rand
    W = spalten * breite + (spalten + 1) * rand
    out = np.empty((H, W, 4), np.float32)
    out[:] = grund
    for i, k in enumerate(kacheln):
        z, s = divmod(i, spalten)
        # Blender-Bilder beginnen unten: Zeile 0 soll oben stehen
        y0 = H - (z + 1) * (hoehe + rand)
        x0 = rand + s * (breite + rand)
        out[y0:y0 + hoehe, x0:x0 + k.shape[1]] = k
    return out


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:]
    opt = {x[2:].split("=")[0]: x.split("=", 1)[1] for x in a if x.startswith("--")}
    bilder = [x for x in a if not x.startswith("--")]
    speichern(tafel(bilder, int(opt.get("spalten", 3)), int(opt.get("hoehe", 512))), opt["aus"])
