# MJPEG-AVI in Einzelbilder zerlegen und Kontaktbogen bauen: avi_bogen.py <avi> <ordner> <jedes_n> <spalten> <bogen.png> [x y w h]
import sys, os, numpy as np, OpenImageIO as oiio
avi, ordner, n, cols, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
crop = list(map(int, sys.argv[6:10])) if len(sys.argv) > 9 else None
os.makedirs(ordner, exist_ok=True)
d = open(avi, "rb").read()
i = 0; k = 0; namen = []
while True:
    a = d.find(b"\xff\xd8\xff", i)
    if a < 0: break
    b = d.find(b"\xff\xd9", a)
    p = os.path.join(ordner, "f%04d.jpg" % k); open(p, "wb").write(d[a:b + 2]); namen.append(p)
    i = b + 2; k += 1
print("Bilder", k)
sel = namen[::n]
bl = []
for p in sel:
    x = oiio.ImageBuf(p).get_pixels(oiio.FLOAT)[:, :, :3]
    if crop: x = x[crop[1]:crop[1] + crop[3], crop[0]:crop[0] + crop[2]]
    bl.append(x)
h, w = bl[0].shape[:2]
rows = (len(bl) + cols - 1) // cols
m = np.full((rows * (h + 4), cols * (w + 4), 3), 0.08, np.float32)
for j, x in enumerate(bl):
    r, c = divmod(j, cols); m[r * (h + 4):r * (h + 4) + h, c * (w + 4):c * (w + 4) + w] = x
s = oiio.ImageSpec(m.shape[1], m.shape[0], 3, oiio.UINT8); o = oiio.ImageOutput.create(out); o.open(out, s); o.write_image(m); o.close()
