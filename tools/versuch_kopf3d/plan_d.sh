# Aufruf: plan_d.sh <name> [Optionen]  (Landmarken aus landmarken/<name>.json)
N=$1; shift
B=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d/b
O=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d2/d
mkdir -p $O
Z=""; [ -f $B/${N}_front_zu.png ] && Z="--zu=$B/${N}_front_zu.png"
cd /home/claude/projekte/rg-3d/tools/versuch_kopf3d
~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_d.py -- --glb=$B/${N}_trellis.glb --pbild=$B/${N}_real.png --lm=landmarken/${N}.json --aus=$O/${N}_d.glb $Z "$@" 2>&1 | grep -E "PLAND|Error|Traceback|line [0-9]|\.py\"" | cut -c1-300
P=/home/claude/projekte/rg-3d/game/versuch/kopf3d/proto/d
mkdir -p $P; for f in ${N}_d.glb ${N}_d_atlas.jpg ${N}_d_portraet.jpg ${N}_d_portraet_zu.jpg ${N}_d_augen.png ${N}_d.json; do cp $O/$f $P/ 2>/dev/null; done
