"""Handflaeche, graue/lila/olive/zu dunkle Stellen umfaerben: Rang-Abgleich der Helligkeit auf die Farben des Handrückens (Detail bleibt)."""
import json,sys,numpy as np
from PIL import Image,ImageDraw,ImageFilter
ein,fl,aus=sys.argv[1:4]; hell=float(sys.argv[4]) if len(sys.argv)>4 else 1.25
im=Image.open(ein).convert("RGB"); N=im.size[0]; a=np.asarray(im).astype(np.float32)/255
F=json.load(open(fl))
def maske(cl,dil=7):
    m=Image.new("L",(N,N),0); d=ImageDraw.Draw(m)
    for f in F:
        if cl(f): d.polygon([(u*N,(1-v)*N) for u,v in f["uv"]],fill=255)
    return np.asarray(m.filter(ImageFilter.MaxFilter(dil)))>0
alle=maske(lambda f:True,3)
ruecken=maske(lambda f:f["n"][1]<-0.35 and f["c"][0]<0.17)
innen=maske(lambda f:f["n"][1]>0.2 and f["c"][0]<0.2)
lum=lambda x:0.299*x[...,0]+0.587*x[...,1]+0.114*x[...,2]
Lg=np.asarray(Image.fromarray((np.clip(lum(a),0,1)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(9))).astype(np.float32)/255
L=lum(a); mx=a.max(2); mn=a.min(2); sat=(mx-mn)/(mx+1e-4)
leder=(a[...,0]>=a[...,1]*1.20)&(a[...,1]>=a[...,2]*1.0)&(sat>0.25)
falsch=alle&((~leder)|(L<0.07))
ref=ruecken&leder&(L>0.07)
rc=a[ref]; rl=lum(rc); rc=rc[np.argsort(rl)]
print("ref px",int(ref.sum()),"mittel",rc.mean(0),"falsch px",int(falsch.sum()),"innen px",int(innen.sum()))
b=a.copy()
def umf(m,faktor):
    idx=np.nonzero(m); tl=Lg[idx]; rang=np.argsort(np.argsort(tl))/max(1,len(tl)-1)
    rang=0.22+0.56*rang
    neu=rc[(rang*(len(rc)-1)).astype(int)].copy()
    det=np.clip((L[idx]/np.maximum(Lg[idx],0.02))**0.6,0.7,1.3)[:,None]; neu=neu*det
    neu=np.clip(neu*np.array([faktor*1.04,faktor,faktor*0.96]),0,1); b[idx]=neu
umf(innen&~falsch,hell); umf(falsch,hell)
import cv2
m=(innen|falsch)
mf=m.astype(np.float32); sig=20
num=cv2.GaussianBlur(b*mf[...,None],(0,0),sig); den=cv2.GaussianBlur(mf,(0,0),sig)[...,None]
glatt=num/np.maximum(den,1e-3)
fein=b-cv2.GaussianBlur(b,(0,0),3)
nb=np.where(m[...,None],glatt+0.35*fein,b)
b=nb
Image.fromarray((np.clip(b,0,1)*255+0.5).astype(np.uint8)).save(aus); print("fertig",aus)
