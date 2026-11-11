import sys
from PIL import Image
o=sys.argv[1]; aus=sys.argv[2]; BG=(230,220,190)
b=Image.new("RGB",(3*400,400),BG)
for j,a in enumerate(("oben","seite","schraeg")):
    im=Image.open(f"{o}/Hilfe_Marke_{a}.png").convert("RGBA").resize((400,400),Image.LANCZOS); b.paste(im,(j*400,0),im)
b.save(aus)
