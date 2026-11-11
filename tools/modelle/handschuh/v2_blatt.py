import sys
from PIL import Image
ordner=sys.argv[1]; poses=sys.argv[2].split(","); aus=sys.argv[3]; ansichten=sys.argv[4].split(",") if len(sys.argv)>4 else ["oben","seite","schraeg"]
S=420; b=Image.new("RGB",(S*len(ansichten),S*len(poses)),(230,220,190))
for i,p in enumerate(poses):
    for j,a in enumerate(ansichten):
        im=Image.open(f"{ordner}/{p}_{a}.png").convert("RGBA").resize((S,S),Image.LANCZOS)
        b.paste(im,(j*S,i*S),im)
b.save(aus)
