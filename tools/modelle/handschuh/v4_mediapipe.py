"""MediaPipe-Handlandmarken fuer die beiden Videos (lokal), Gelenkwinkel, Kurven, Tabelle. Python einer eigenen MediaPipe-venv (ausserhalb des Repos). Umgebungsvariablen VIDEO, MP_AUS, MP_MODELL"""
import cv2, numpy as np, json, csv, os, sys
import mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
V = os.environ.get("VIDEO", os.path.join("..", "video")); OUT = os.environ.get("MP_AUS", "mediapipe"); os.makedirs(OUT, exist_ok=True)
MODELL = os.environ.get("MP_MODELL", "hand_landmarker.task")  # Google hand_landmarker.task, ausserhalb des Repos
opt = vision.HandLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=MODELL), running_mode=vision.RunningMode.VIDEO, num_hands=1,
                                   min_hand_detection_confidence=0.2, min_hand_presence_confidence=0.2, min_tracking_confidence=0.2)
def winkel(a, b, c):
    u = a - b; v = c - b; cs = np.dot(u, v) / (np.linalg.norm(u) * np.linalg.norm(v) + 1e-9); return float(np.degrees(np.arccos(np.clip(cs, -1, 1))))
def beuge(a, b, c):   # 0 = gestreckt
    return 180.0 - winkel(a, b, c)
def analyse(W):
    r = {}
    wr = W[0]
    for name, (m, p, d, t) in {"zeige": (5, 6, 7, 8), "mittel": (9, 10, 11, 12), "ring": (13, 14, 15, 16), "klein": (17, 18, 19, 20)}.items():
        mcp = float(np.degrees(np.arccos(np.clip(np.dot(W[m] - wr, W[p] - W[m]) / (np.linalg.norm(W[m] - wr) * np.linalg.norm(W[p] - W[m]) + 1e-9), -1, 1))))
        r[name] = (mcp, beuge(W[m], W[p], W[d]), beuge(W[p], W[d], W[t]))
    r["daumen"] = (beuge(W[1], W[2], W[3]), beuge(W[2], W[3], W[4]), float(np.degrees(np.arccos(np.clip(np.dot(W[2] - W[1], W[3] - W[2]) / (np.linalg.norm(W[2] - W[1]) * np.linalg.norm(W[3] - W[2]) + 1e-9), -1, 1)))))
    return r
alle = {}
for n in (1, 2):
    cap = cv2.VideoCapture(rf"{V}\video_{n}.mp4"); fps = cap.get(cv2.CAP_PROP_FPS); lm = vision.HandLandmarker.create_from_options(opt)
    rows = []; i = 0; ueb = os.path.join(OUT, f"ueberlagerung_{n}"); os.makedirs(ueb, exist_ok=True)
    while True:
        ok, f = cap.read()
        if not ok: break
        s = 1280 / f.shape[1]; g = cv2.resize(f, (1280, int(f.shape[0] * s)), interpolation=cv2.INTER_AREA)
        img = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(g, cv2.COLOR_BGR2RGB))
        res = lm.detect_for_video(img, int(i * 1000 / fps))
        if res.hand_world_landmarks:
            W = np.array([[l.x, l.y, l.z] for l in res.hand_world_landmarks[0]]); N = np.array([[l.x, l.y] for l in res.hand_landmarks[0]])
            conf = float(res.handedness[0][0].score); hand = res.handedness[0][0].category_name
            rows.append((i, i / fps, conf, hand, W, N, analyse(W)))
            if i % 6 == 0:
                for q, (x, y) in enumerate(N): cv2.circle(g, (int(x * g.shape[1]), int(y * g.shape[0])), 5, (0, 0, 255) if q in (17, 18, 19, 20) else (0, 255, 0), -1)
                for a, b in ((0, 1), (1, 2), (2, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8), (9, 10), (10, 11), (11, 12), (13, 14), (14, 15), (15, 16), (0, 17), (17, 18), (18, 19), (19, 20), (5, 9), (9, 13), (13, 17)):
                    cv2.line(g, (int(N[a][0] * g.shape[1]), int(N[a][1] * g.shape[0])), (int(N[b][0] * g.shape[1]), int(N[b][1] * g.shape[0])), (255, 255, 0), 2)
                cv2.imwrite(os.path.join(ueb, f"f{i:03d}.jpg"), cv2.resize(g, (900, int(900 * g.shape[0] / g.shape[1]))))
        else:
            rows.append((i, i / fps, 0.0, "-", None, None, None))
        i += 1
    alle[n] = rows
    print("Video", n, "Bilder", i, "erkannt", sum(1 for r in rows if r[4] is not None), "Hand", set(r[3] for r in rows))
    with open(os.path.join(OUT, f"landmarken_{n}.csv"), "w", newline="") as fh:
        w = csv.writer(fh); w.writerow(["frame", "t", "konfidenz", "hand"] + [f"{a}{k}" for k in range(21) for a in "xyz"])
        for r in rows: w.writerow([r[0], f"{r[1]:.3f}", f"{r[2]:.2f}", r[3]] + ([f"{v:.4f}" for p in r[4] for v in p] if r[4] is not None else []))
# Kurven
fing = ["klein", "ring", "mittel", "zeige", "daumen"]; farbe = {"klein": "b", "ring": "g", "mittel": "y", "zeige": "r", "daumen": "k"}
tab = {}
for n in (1, 2):
    rows = [r for r in alle[n] if r[4] is not None]; t = np.array([r[1] for r in rows])
    fig, ax = plt.subplots(3, 1, figsize=(10, 9), sharex=True)
    for gi, gname in enumerate(("Grundgelenk (MCP)", "Mittelgelenk (PIP)", "Endgelenk (DIP)")):
        for fn in fing:
            y = np.array([r[6][fn][gi] for r in rows]); k = 5
            ys = np.convolve(np.pad(y, (k // 2, k // 2), mode="edge"), np.ones(k) / k, mode="valid")
            ax[gi].plot(t, ys, color=farbe[fn], label=fn, linewidth=2)
        ax[gi].set_ylabel(gname + " [Grad]"); ax[gi].grid(True); ax[gi].legend(ncol=5, fontsize=8)
    ax[2].set_xlabel("Zeit [s]"); fig.suptitle(f"Video {n}: Beugewinkel (MediaPipe, Weltkoordinaten, 5-Bild-Mittel)"); fig.tight_layout(); fig.savefig(os.path.join(OUT, f"kurven_{n}.png"), dpi=110); plt.close(fig)
    # Tabelle: Beginn (PIP > 20 Grad nach Glaettung), 90 % des Endwertes, Endwerte (letzte 0,5 s Mittel), Anfangswerte
    tab[n] = {}
    for fn in fing:
        a = np.array([[r[6][fn][g] for g in range(3)] for r in rows])
        k = 5; asm = np.stack([np.convolve(np.pad(a[:, g], (k // 2, k // 2), mode="edge"), np.ones(k) / k, mode="valid") for g in range(3)], 1)
        end = asm[t > t.max() - 0.6].mean(0); start = asm[t < 0.8].mean(0); pip = asm[:, 1] if fn != "daumen" else asm[:, 0]
        e = end[1] if fn != "daumen" else end[0]; s0 = start[1] if fn != "daumen" else start[0]
        onset = t[np.argmax(pip > s0 + 0.15 * (e - s0))] if e > s0 + 15 else float("nan"); t90 = t[np.argmax(pip > s0 + 0.9 * (e - s0))] if e > s0 + 15 else float("nan")
        tab[n][fn] = dict(start=[round(float(x)) for x in start], ende=[round(float(x)) for x in end], beginn=round(float(onset), 2), t90=round(float(t90), 2))
json.dump(tab, open(os.path.join(OUT, "tabelle.json"), "w"), indent=1)
print(json.dumps(tab, indent=1))
