D=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d/b
cd /home/claude/projekte/rg-3d/tools/versuch_kopf3d
~/werkzeuge/mpfb/blender_mpfb.sh -b --python exportieren_proto.py -- ${EXTRA} --dna=${2:-proto_dna.json} --trellis=$D/${1:-m2}_trellis.glb ${3} --aus=$D/out/${1:-m2}_kopf.glb 2>&1 | grep -E "PLANB|EXPORT|NETZ gesamt|Error|Traceback|line [0-9]|\.py" | cut -c1-220
