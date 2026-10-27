# Kontaktbogen: Ausschnitt aus PNGs zu einem Bild (mit Blenders Python: OpenImageIO + numpy).
# Aufruf: <blender-python> bogen.py <ziel.png> <spalten> <x0,y0,x1,y1> <bild>...
import sys
import numpy as np
import OpenImageIO as oiio
ziel, sp = sys.argv[1], int(sys.argv[2])
x0, y0, x1, y1 = (int(v) for v in sys.argv[3].split(","))
bs = [oiio.ImageBuf(p).get_pixels(oiio.FLOAT)[y0:y1, x0:x1, :3] for p in sys.argv[4:]]
h, w = bs[0].shape[:2]
z = (len(bs) + sp - 1) // sp
out = np.full((z * h, sp * w, 3), 0.08, np.float32)
for i, b in enumerate(bs):
    out[(i // sp) * h:(i // sp) * h + b.shape[0], (i % sp) * w:(i % sp) * w + b.shape[1]] = b
o = oiio.ImageOutput.create(ziel)
o.open(ziel, oiio.ImageSpec(out.shape[1], out.shape[0], 3, oiio.UINT8))
o.write_image((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8))
o.close()
