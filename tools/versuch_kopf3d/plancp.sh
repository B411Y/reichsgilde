# Aufruf: plancp.sh <name> <tl> <tr> <p_el> <p_er> <p_nase> <p_mund> [Optionen]
N=$1; TL=$2; TR=$3; PEL=$4; PER=$5; PN=$6; PM=$7; shift 7
B=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d/b
Z=""; [ -f $B/${N}_front_zu.png ] && Z="--zu=$B/${N}_front_zu.png"
cd /home/claude/projekte/rg-3d/tools/versuch_kopf3d
~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_cp.py -- --glb=$B/${N}_trellis.glb --tl=$TL --tr=$TR --pbild=$B/${N}_real.png --p_el=$PEL --p_er=$PER --p_nase=$PN --p_mund=$PM --aus=$B/cp/${N}_cp.glb $Z "$@" 2>&1 | grep -E "PLANC|Error|Traceback|line [0-9]|\.py\"" | cut -c1-250
P=/home/claude/projekte/rg-3d/game/versuch/kopf3d/proto/cp
mkdir -p $P; cp $B/cp/${N}_cp.glb $P/; [ -f $B/cp/haut_zu.jpg ] && cp $B/cp/haut_zu.jpg $P/${N}_cp_haut_zu.jpg
