"""Handflaeche/graue Stellen umfaerben: Rang-Abgleich der Helligkeit auf die Farben des Handrückens (Detail bleibt)."""
import json,sys,numpy as np
from PIL import Image,ImageDraw,ImageFilter
ein,fl,aus=sys.argv[1:4]
im=Image.open(ein).convert("RGB"); N=im.size[0]; a=np.asarray(im).astype(np.float32)/255
F=json.load(open(fl))
def maske(cl):
    m=Image.new("L",(N,N),0); d=ImageDraw.Draw(m)
    for f in F:
        if cl(f): d.polygon([(u*N,(1-v)*N) for u,v in f["uv"]],fill=255)
    return np.asarray(m.filter(ImageFilter.MaxFilter(7)))>0
ruecken=maske(lambda f:f["n"][1]<-0.35 and f["c"][0]<0.17)
hand_innen=maske(lambda f:f["n"][1]>0.2 and f["c"][0]<0.2)
lum=lambda x:0.299*x[...,0]+0.587*x[...,1]+0.114*x[...,2]
L=lum(a); mx=a.max(2); mn=a.min(2); sat=(mx-mn)/(mx+1e-4)
grau=(sat<0.22)&(L>0.25)
ziel=(hand_innen|grau)
ref=ruecken&~grau&(L>0.03)
print("ruecken px",ref.sum(),"ziel px",ziel.sum(),"mittel ref",a[ref].mean(0),"ziel",a[ziel].mean(0))
rc=a[ref]; rl=lum(rc); o=np.argsort(rl); rc=rc[o]
idx=np.nonzero(ziel)
tl=L[idx]; rang=np.argsort(np.argsort(tl))/max(1,len(tl)-1)
neu=rc[(rang*(len(rc)-1)).astype(int)]
b=a.copy(); b[idx]=neu
# Rueckenhelligkeit moderat anheben fuer Lesbarkeit (Gamma)
b=np.clip(b,0,1)
Image.fromarray((b*255+0.5).astype(np.uint8)).save(aus)
print("fertig",aus)
