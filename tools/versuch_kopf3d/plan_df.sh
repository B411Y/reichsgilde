# Aufruf: plan_df.sh <name> [Optionen]  – frontale Fassung: TRELLIS aus <name>_fs, Projektion/Landmarken aus <name>_al, Augen zu aus <name>_al_zu
N=$1; shift
F=${ORDNER:-/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d2/f}; K=${KENN:-f}
O=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d2/df
mkdir -p $O
cd /home/claude/projekte/rg-3d/tools/versuch_kopf3d
~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_d.py -- --glb=$F/${N}_fs_trellis.glb --pbild=$F/${N}_al.png --pzu=$F/${N}_al_zu.png --lm=landmarken/${N}${K}.json --aus=$O/${N}${K}_d.glb "$@" 2>&1 | grep -E "PLAND|Error|Traceback|line [0-9]|\.py\"" | cut -c1-300
P=/home/claude/projekte/rg-3d/game/versuch/kopf3d/proto/d
for f in ${N}${K}_d.glb ${N}${K}_d_atlas.jpg ${N}${K}_d_portraet.jpg ${N}${K}_d_portraet_zu.jpg ${N}${K}_d_augen.png ${N}${K}_d.json; do cp $O/$f $P/ 2>/dev/null; done
