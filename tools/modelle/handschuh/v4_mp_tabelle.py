import csv,os,numpy as np
D=os.environ.get("MP_AUS", "mediapipe")
def w(a,b,c):
    u=a-b; v=c-b; return float(np.degrees(np.arccos(np.clip(u@v/(np.linalg.norm(u)*np.linalg.norm(v)+1e-9),-1,1))))
for n in (2,1):
    R=list(csv.reader(open(rf"{D}\landmarken_{n}.csv")))[1:]
    T=np.array([float(r[1]) for r in R]); W=np.array([[float(x) for x in r[4:67]] for r in R]).reshape(len(R),21,3)
    def ang(i,fn):
        m,p,d,t={"klein":(17,18,19,20),"ring":(13,14,15,16),"mittel":(9,10,11,12),"zeige":(5,6,7,8)}[fn]; X=W[i]
        a=np.degrees(np.arccos(np.clip((X[m]-X[0])@(X[p]-X[m])/(np.linalg.norm(X[m]-X[0])*np.linalg.norm(X[p]-X[m])),-1,1)))
        return a,180-w(X[m],X[p],X[d]),180-w(X[p],X[d],X[t])
    print("Video",n)
    for tt in (0.8,1.5,1.8,2.1,2.6,3.5):
        idx=[i for i in range(len(T)) if abs(T[i]-tt)<0.07]
        s=f"  t={tt}: "
        for fn in ("klein","ring","mittel","zeige"):
            a=np.mean([ang(i,fn) for i in idx],axis=0); s+=f"{fn} {a[0]:.0f}/{a[1]:.0f}/{a[2]:.0f}  "
        print(s)
