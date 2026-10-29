#!/bin/bash
# Messung der Bildrate in der Feuer-Testszene (Phase 1, docs/feuer/phase1.md): startet das Spiel mit --feuertest=1 auf einem virtuellen
# Bildschirm (Xvfb) mit Vulkan auf der Grafikkarte, wartet, bis die Szene läuft, und mittelt die Zeilen „[feuer] fps=…“ des Protokolls.
#
# Aufruf:  WORKTREE=<projektordner> [DISPLAY_NR=77] [TREIBER=nvidia|vulkan|opengl3] [SEKUNDEN=20] [VORLAUF=4] \
#          tools/feuer/messen.sh <name> <ausgabeordner> -- <godot-optionen>
# Beispiel: DISPLAY_NR=77 tools/feuer/messen.sh stadt-A /tmp/m -- --feuertest=1 --feuersicht=stadt --feuerart=A --bildrate=0 --vsync=Aus
#
# Die Ausgabe: eine Kopfzeile (Name und Last, loadavg 1 Minute, vor und nach der Messung), dann je Zustand eine Zeile: mittlere Bildrate (kleinste,
# größte), die Zeit des Zeichners auf der Grafikkarte (ms je Bild ohne das Anzeigen im Fenster: Mittel, Median und unteres Zehntel – andere Prozesse
# auf derselben Grafikkarte machen die Zeit nur länger, das untere Zehntel kommt der ungestörten Zeit am nächsten), im Hauptprozessor und die
# Zeichenaufrufe. Ohne --feuerzyklus gibt es einen Zustand „-“. Mit --feuerzyklus=<s> wechselt die Szene selbst durch ohne Feuer, A, B (im Fackelraster
# mit 0, 12 und allen Lichtern): SEKUNDEN = Zahl der Zustände · s · Durchläufe. Die Rechnerlast beeinflusst die Zahlen stark; im Zyklus treffen sie alle Zustände gleich.
# Die ersten VORLAUF Sekunden der Szene zählen nicht (Shader und Partikel wärmen sich auf).
NAME=$1; OUT=$2; shift 3
X=~/werkzeuge/xvfb
WORKTREE=${WORKTREE:-$(cd "$(dirname "$0")/../.." && pwd)}
DISPLAY_NR=${DISPLAY_NR:-77}
GODOT=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu
export LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export __GLX_VENDOR_LIBRARY_NAME=mesa
export XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
TREIBER=${TREIBER:-nvidia}
if [ "$TREIBER" = nvidia ]; then
  export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json
  unset __GLX_VENDOR_LIBRARY_NAME
  TREIBER=vulkan
fi
mkdir -p /tmp/xkb; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
mkdir -p "$OUT"
export XDG_DATA_HOME=$OUT/daten
$X/Xvfb-gepatcht :$DISPLAY_NR -screen 0 1600x900x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > "$OUT/xvfb.log" 2>&1 &
XPID=$!
sleep 2
LAST_VOR=$(cut -d' ' -f1 /proc/loadavg)
DISPLAY=:$DISPLAY_NR $GODOT --rendering-driver $TREIBER --path "$WORKTREE/game" --resolution 1600x900 -- "$@" > "$OUT/godot.log" 2>&1 &
GPID=$!
LOG="$OUT/godot.log"
# Warten, bis die Szene Bilder liefert (höchstens 300 s), dann VORLAUF + SEKUNDEN Zeilen
FERTIG=$(( ${VORLAUF:-4} + ${SEKUNDEN:-20} ))
for i in $(seq 1 300); do
  [ "$(grep -c "feuer\] fps=" "$LOG" 2>/dev/null)" -ge "$FERTIG" ] && break
  sleep 1
done
LAST_NACH=$(cut -d' ' -f1 /proc/loadavg)
kill $GPID 2>/dev/null; sleep 1; kill $XPID 2>/dev/null
grep "feuer\] fps=" "$LOG" | tail -n +$(( ${VORLAUF:-4} + 1 )) | head -n ${SEKUNDEN:-20} | python3 -c "
import sys, re
# Zeilen nach Zustand (--feuerzyklus) zusammenfassen; die ersten ${UEBERSPRINGEN:-2} Zeilen nach jedem Wechsel zählen nicht
zustaende = {}; reihenfolge = []; vorher = None; nr = 0
for z in sys.stdin:
    g = re.search(r'fps=([0-9.]+) ms=([0-9.]+) gpu=([0-9.]+) cpu=([0-9.]+) zustand=(\S+).*aufrufe=([0-9]+)', z)
    if not g: continue
    name = g.group(5)
    nr = nr + 1 if name == vorher else 0
    vorher = name
    if nr < ${UEBERSPRINGEN:-2} and name != '-': continue
    if name not in zustaende: zustaende[name] = []; reihenfolge.append(name)
    zustaende[name].append((float(g.group(1)), float(g.group(2)), float(g.group(3)), float(g.group(4)), int(g.group(6))))
print('## $NAME   Last $LAST_VOR -> $LAST_NACH')
if not zustaende: print('keine Messwerte')
for name in reihenfolge:
    w = zustaende[name]; n = len(w)
    mittel = lambda i: sum(x[i] for x in w) / n
    gpu = sorted(x[2] for x in w)
    print('%-22s fps %5.1f (min %5.1f, max %5.1f)  gpu: Mittel %6.2f, Median %6.2f, unteres Zehntel %6.2f ms  cpu %5.2f ms  aufrufe %4d  (%d Messwerte)' % (name, mittel(0), min(x[0] for x in w), max(x[0] for x in w), mittel(2), gpu[n // 2], gpu[n // 10], mittel(3), mittel(4), n))
"
