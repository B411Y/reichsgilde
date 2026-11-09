# Herkunft: Probe-Passanten mit Skelett (Weg A)

`passant_probe.glb` (Männer) und `passant_probe_frau.glb` (Frauen) entstehen mit `tools/modelle/passanten_probe.py` in Blender (ohne
Bildschirm). Nur für die Probe: Das Spiel zeigt die Figuren nur mit der Startoption `--passantenprobe` (`game/scripts/Stadtansicht.Passantenprobe.cs`).

| Teil | Quelle | Lizenz | Bezug |
|---|---|---|---|
| Körper (MakeHuman-Grundnetz, Formziele Mann/Frau, Rockhelfer) | MPFB 2.0.18, MakeHuman Community | CC0 1.0 (Assets); MPFB-Code GPL-3.0, nur als Werkzeug benutzt, die Ausgabe gehört dem Nutzer (`LICENSE.md` von MPFB, Abschnitt D) | https://github.com/makehumancommunity/mpfb2 (Tag v2.0.18) |
| Skelett und Gewichte | Rig „game_engine“ aus MPFB 2.0.18 | CC0 1.0 | wie oben |
| Stehen, Gehen, Reden, Sitzen, Kauern (daraus „Betteln“ mit eigener Armhaltung) | Quaternius, Universal Animation Library [Standard]: `Idle_Loop`, `Walk_Loop`, `Idle_Talking_Loop`, `Sitting_Idle_Loop`, `Crouch_Idle_Loop` | CC0 1.0 (`License.txt` im Paket) | https://opengameart.org/content/universal-animation-library |
| Tragen, Hacken, Bücken | Quaternius, Universal Animation Library 2 [Standard]: `Walk_Carry_Loop`, `TreeChopping_Loop`, `Farm_Harvest` | CC0 1.0 (`License.txt` im Paket) | https://opengameart.org/content/universal-animation-library-2 |
| Kleidung (Cotte, Gürtel, Gugel mit Zipfel, langer Rock, Kleid, Haube, Gebende mit Schleier, Bettlerkleidung), Haar, Halb- und Holzschuhe, Kiste, Axt, Zonen | eigenes Skript | Alle Rechte vorbehalten (`LICENSE-INHALTE.md`) | `tools/modelle/passanten_probe.py` |
| Stoffstruktur (Wolle/Leinen, Leder) | Schichten „tuch“ und „leder“ der Material-Bibliothek des Spiels (nur ihre Nummer steht im Netz, UV.y) | wie die Bibliothek | `game/daten/materialbibliothek.json` |

Prüfsummen der geladenen Archive (SHA-256, abgerufen 08.11.2026): MPFB `mpfb2-2.0.18.zip` ADA7C4FF11E79CF6ED9B441BCCABC52C9359F2848B988F66E89C14C30B21FED7,
`universal_animation_librarystandard.zip` 18FF1A7215F4852B320203E8AAF02A1578B5C8EEF9027FBAEDFCEDC7B85A3AC2,
`universal_animation_library_2standard.zip` EC0E40D6D78FE9AAAD59E322F40865A8675C22F0745E291622E54520391A9217. Die Archive liegen nicht im Repository.

Bearbeitung: Kleidung auf dem Körper gebaut (aufgedickte Körperflächen mit Schnittkanten entlang eines Feldes, Rockhelfer gekürzt bzw.
verlängert, Kopfbedeckungen aus den Kopfflächen, Zipfel), Bundschuhe als eigener Körper über dem Fuß, Netz je Variante auf 2.900 (nah, Kopf
und Kanten geschont) und 560 Dreiecke (fern) verkleinert, Skelett auf 23 Knochen mit den Namen von Godots `SkeletonProfileHumanoid` (Finger
in die Hand gelegt, die Finger vorher locker gekrümmt), die sieben Clips auf dieses Skelett übertragen (Richtung der Arm- und Beinknochen wie
in der Quelle, Hüfthöhe angeglichen; Oberkörper und Kopf aufgerichtet, Arme hängend und Ellbogen lockerer, Handgelenk gerade, Schrittweite von
Gehen und Tragen verstärkt), dazu eine Axt aus zwei Quadern für „Hacken“. Nicht benutzt: MakeHuman-Kleidung, Kimodo, Mixamo, Human Generator, Kaufpakete.

Herkunft je Datei aller Assets: `docs/assets/protokoll.csv`.
