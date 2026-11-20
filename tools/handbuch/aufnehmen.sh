#!/bin/bash
# Nimmt die Rohbilder für das Handbuch-Kapitel „Steuerung“ im Spiel auf (PNG, 1600 x 900; die Oberfläche ist dabei 83 % groß, siehe „Größe nach Fensterhöhe“) nach /tmp/handbuch-roh (oder dem ersten Argument).
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

# 1) bildschirm und tooltip (seit dem UI-Umbau „Zunftlade“, 20.11.2026): Die Kurzübersicht des eigenen Betriebs öffnet die Startoption --verwalten=mehr,
#    die Meldungen (Faltbriefe und Einblendung) kommen aus --meldungen=demo; die Maus ruht auf der Zeile „Gewinn dieses Jahr bisher“. Der Tooltip erscheint,
#    sein Kreis füllt sich (--tooltipfest=6): Aufnahme "tooltip" mit halb vollem Kreis, fünf Sekunden später "bildschirm" mit festgeheftetem Tooltip.
#    Das Skript wartet nach dem Start 100 s auf das Ende des Ladebilds; lädt der Rechner länger, die erste Wartezeit ("w") erhöhen.
"$WURZEL/tools/klicktest/lauf.sh" "$TMP/b" 100 \
  "w 6;m 1312 321;w 1;s t1;w 2;s tooltip;w 5;s bildschirm" -- $OPT --startstunde=13 --verwalten=mehr --meldungen=demo --tooltipfest=6
cp "$TMP/b/tooltip.png" "$TMP/b/bildschirm.png" "$TMP/b/t1.png" "$ROH/"

# 2) bauen und wege: B, Wohnhaus „Fachwerkhaus“ wählen (Liste, Eintrag 1), Umriss auf eine freie Stelle der Bauzone Kern (grün); danach Esc, Esc
#    (zurück, Baumenü zu), G (Wege-Modus) und die Maus auf die Ringgasse (Tooltip des Weges).
"$WURZEL/tools/klicktest/lauf.sh" "$TMP/c" 100 \
  "w 4;m 800 450;k b;w 2;m 1392 387;b 1;w 1;m 965 505;w 2;s bauen;k Escape;w 1;k Escape;w 1;k g;w 2;m 760 300;w 4;s wege" -- $OPT --startstunde=11
cp "$TMP/c/bauen.png" "$TMP/c/wege.png" "$ROH/"
rm -rf "$TMP"
echo "Rohbilder in $ROH"
