#!/usr/bin/env python3
"""Wartet, bis die NAS frei genug ist: Last (1-Minuten-Mittel) unter einer Grenze (Standard 12) und höchstens
`gpu` andere Prozesse auf der Grafikkarte (laut nvidia-smi; Standard: nicht prüfen).

Vor jedem schweren Lauf aufrufen:  warte_last.py [grenze] [hoechstens_minuten] [gpu]
Beispiel: warte_last.py 12 20 1  -> Last < 12 und höchstens ein anderer Vulkan-Lauf (die T400 verträgt zwei gleichzeitig)
Rückgabe 1, wenn die Zeit abläuft (der Aufrufer entscheidet, ob er trotzdem läuft).
"""
import os
import subprocess
import sys
import time

grenze = float(sys.argv[1]) if len(sys.argv) > 1 else 12.0
hoechstens = float(sys.argv[2]) if len(sys.argv) > 2 else 30.0
gpu = int(sys.argv[3]) if len(sys.argv) > 3 else -1


def gpu_prozesse():
    try:
        aus = subprocess.run(["nvidia-smi", "--query-compute-apps=pid", "--format=csv,noheader"],
                             capture_output=True, text=True, timeout=20).stdout
        pids = {z.strip() for z in aus.splitlines() if z.strip()}
        # Grafik-Prozesse (C+G) stehen nicht immer in der Compute-Liste: Tabelle mitlesen
        tabelle = subprocess.run(["nvidia-smi"], capture_output=True, text=True, timeout=20).stdout
        for z in tabelle.splitlines():
            teile = z.split()
            if len(teile) > 6 and teile[0] == "|" and teile[-1] == "|" and teile[-2].endswith("MiB") and teile[4].isdigit():
                pids.add(teile[4])
        return len(pids)
    except (OSError, subprocess.SubprocessError):
        return 0


anfang = time.time()
while True:
    last = os.getloadavg()[0]
    andere = gpu_prozesse() if gpu >= 0 else 0
    if last < grenze and (gpu < 0 or andere <= gpu):
        print(f"Last {last:.1f}, GPU-Prozesse {andere}: weiter")
        sys.exit(0)
    if time.time() - anfang > hoechstens * 60:
        print(f"Last {last:.1f}, GPU-Prozesse {andere}: nach {hoechstens:.0f} min noch zu voll")
        sys.exit(1)
    print(f"Last {last:.1f}, GPU-Prozesse {andere}: warte ...", flush=True)
    time.sleep(30)
