#!/bin/bash
# Nimmt die Rohbilder für das Handbuch-Kapitel „Steuerung“ im Spiel auf (PNG, 1600 x 900) nach /tmp/handbuch-roh (oder dem ersten Argument).
# Aufruf (vom Projektordner aus):  TREIBER=nvidia tools/handbuch/aufnehmen.sh [rohordner]
# Danach: tools/handbuch/beschriften.sh [rohordner]  (setzt die Nummern aus bilder.json und schreibt docs/bilder/handbuch/*.webp).
# Das Spiel braucht auf einem ausgelasteten Rechner 90–120 s zum Laden. Alle Läufe: Schmied, Frühling, klares Wetter, Miniatur-Unschärfe aus,
# Klicktest-Skript tools/klicktest/lauf.sh (Xvfb, Ordner ~/werkzeuge). Die Bilder immer ansehen: Meldungen, Uhrzeit und Tooltip-Kreis hängen vom Zeitpunkt ab,
# bei einer Abweichung die Wartezeiten ("w") anpassen und die Koordinaten in bilder.json mit dem neuen Rohbild abgleichen.
set -e
WURZEL=$(cd "$(dirname "$0")/../.." && pwd)
ROH=${1:-/tmp/handbuch-roh}
TMP=$(mktemp -d)
export WORKTREE=$WURZEL DISPLAY_NR=${DISPLAY_NR:-95}
OPT="--beruf=Schmied --tiltshift=Aus --wetter=klar --tempo=1"
mkdir -p "$ROH"

# 1) bildschirm und tooltip: Tempo 4× von Frühjahr 9 Uhr an bis nach 14 Uhr (ab etwa 14 Uhr melden die KI-Häuser, die Meldungen stehen 20 s), dann Tempo 1×,
#    Kurzübersicht des eigenen Betriebs über die Betriebsleiste und Maus auf der Zeile „Dieses Jahr bisher“. Der Tooltip erscheint, sein Kreis füllt sich
#    (--tooltipfest=6, mehr als 5 s gilt nicht): Aufnahme "tooltip" mit halb vollem Kreis, eine Sekunde später "bildschirm" mit festgeheftetem Tooltip und Meldungen.
#    Die Zeitpunkte hängen von der Ladezeit des Rechners ab: Bilder ansehen, bei Abweichung die Wartezeiten ändern (die Zwischenbilder t1 und t4 helfen).
"$WURZEL/tools/klicktest/lauf.sh" "$TMP/b" 100 \
  "m 800 450;w 2;k 3;w 8;w 6;w 6;w 6;w 6;w 6;w 5;k 1;m 130 856;b 1;w 3;m 1270 288;w 1;s t1;w 1;s tooltip;w 1;s bildschirm;w 2;s t4" -- $OPT --startstunde=9 --tooltipfest=6
cp "$TMP/b/tooltip.png" "$TMP/b/bildschirm.png" "$TMP/b/t1.png" "$TMP/b/t4.png" "$ROH/"

# 2) bauen und wege: B, Wohnhaus „Fachwerkhaus“ wählen (Liste, Eintrag 1), Umriss auf freie Stelle der Bauzone Kern; danach Esc, Esc (zurück, Baumenü zu), G (Wege-Modus)
"$WURZEL/tools/klicktest/lauf.sh" "$TMP/c" 100 \
  "m 800 450;w 2;k b;w 2;m 1370 427;b 1;w 1;m 930 650;w 2;s bauen;k Escape;w 1;k Escape;w 1;k g;w 2;m 800 450;w 4;s wege" -- $OPT --startstunde=11
cp "$TMP/c/bauen.png" "$TMP/c/wege.png" "$ROH/"
rm -rf "$TMP"
echo "Rohbilder in $ROH"
