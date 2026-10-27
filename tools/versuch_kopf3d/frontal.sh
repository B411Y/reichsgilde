#!/bin/bash
# Frontale, schattenlose Zwischenstufen je Kopf (Bild-KI 8188), danach TRELLIS (8189). Aufruf: frontal.sh <name> [schritte: fs,al,zu,tr]
# Regeln: nie beide ComfyUIs gleichzeitig (/free vorher), PAUSE beachten.
# WER="She is a young woman with a smooth beardless feminine face. " beugt Geschlechtswechsel vor
N=$1; SCHRITTE=${2:-fs,al,zu,tr}
S=${ORDNER:-/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d2/f}; mkdir -p $S
B=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d/b
frei() { curl -s -X POST -H 'Content-Type: application/json' -d '{"unload_models": true, "free_memory": true}' http://192.168.2.23:$1/free >/dev/null; }
[ -f ~/werkzeuge/comfy/PAUSE ] && { echo PAUSE; exit 75; }
if [[ $SCHRITTE == *fs* ]]; then
  frei 8189
  VORLAGE=$B/${N}_real.png python3 ~/werkzeuge/comfy/bild.py $S/${N}_fs.png "${WER}Turn this painted portrait into a real high-resolution studio photograph of the same person, seen exactly from the front: head straight and upright, face symmetric, both eyes looking straight into the camera, ears equally visible on both sides, neutral calm expression with closed mouth. Photorealistic: natural skin texture with fine pores, real individual hair strands, sharp focus, soft even studio lighting, plain uniform light grey background. Keep the same face, hair, clothes. Not a painting, not an illustration. no text, no lettering" || exit $?
fi
if [[ $SCHRITTE == *al* ]]; then
  frei 8189
  VORLAGE=$S/${N}_fs.png python3 ~/werkzeuge/comfy/bild.py $S/${N}_al.png "${WER}Relight this photograph with completely flat, even, shadowless frontal light, like an albedo texture: no shadows under the nose, under the chin, in the eye sockets or on the neck, no highlights. Keep it a photograph with the exact same face, skin texture, skin color, eyes, hair, clothes, pose and plain uniform light grey background. Change nothing else. no text, no lettering" || exit $?
fi
if [[ $SCHRITTE == *zu* ]]; then
  frei 8189
  VORLAGE=$S/${N}_al.png python3 ~/werkzeuge/comfy/bild.py $S/${N}_al_zu.png "${WER}Close both eyes of the person, eyelids gently closed as in a relaxed blink with the lash line visible, keep the face, skin, lighting, expression, mouth, hair, clothes, the pose and the background exactly the same. Change nothing else. no text, no lettering" || exit $?
fi
if [[ $SCHRITTE == *tr* ]]; then
  frei 8188
  python3 /home/claude/projekte/rg-3d/tools/versuch_kopf3d/trellis.py $S/${N}_fs.png $S/${N}_fs_trellis.glb || exit $?
  frei 8189
fi
echo FERTIG $N
