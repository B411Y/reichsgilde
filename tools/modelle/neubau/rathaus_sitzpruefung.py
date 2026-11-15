"""Sitzpruefung der neuen Ratshalle mit tools/modelle/sitzpruefung.py aus dem Figuren-Baukasten (Branch phase5/figuren-baukasten).

    python tools/modelle/neubau/rathaus_sitzpruefung.py --sitzpruefung=<pfad zu sitzpruefung.py> [--arten=Mann,Oger] [--gegenprobe]

sitzpruefung.py ist noch nicht in main (Abhaengigkeit, siehe docs/bereiche/darstellung.md); dieses Skript laedt es von dort, setzt
statt der alten Ratssitzung (innen/rathaus_innen.glb) die neue Ratshalle ein (gebaeude/neubau/rathaus_ratshalle_innen.glb) und
laesst nur diese Szene laufen: alle Voelker an jedem Platz (Buergermeister, sechs Ratsherren, Schreiber, vier Plaetze der
Zuhoererbank), Eindringen von Koerper und Saum in die Moebel (Ziel 0 cm), Fuesse ueber dem Boden, Blick zur Tafel (unter 45 Grad).
Die Figuren nimmt sitzpruefung.py aus seinem eigenen Repository (dort haben sie den Sitz-Clip).
"""
import importlib.util
import math
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import rathaus_masse as M  # noqa: E402

ORDNER = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "modelle", "gebaeude", "neubau")


def laden(pfad):
    spec = importlib.util.spec_from_file_location("sitzpruefung", pfad)
    sp = importlib.util.module_from_spec(spec)
    sys.argv = [pfad] + [a for a in sys.argv[1:] if not a.startswith("--sitzpruefung=")]
    spec.loader.exec_module(sp)
    return sp


def main():
    pfad = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--sitzpruefung=")), None)
    if not pfad or not os.path.exists(pfad):
        print("Bitte --sitzpruefung=<pfad zu tools/modelle/sitzpruefung.py im Baukasten-Branch> angeben.")
        return 2
    sp = laden(pfad)

    def ziel(n, namen, welt, m):
        """Wohin der Platz blicken soll (glTF: x, y oben, z = -Blender-y): der Buergermeister zum Redner, die Ratsherren auf die Tafel
        vor sich, der Schreiber zur Tafel, die Zuhoerer in den Saal."""
        x, z = m[0, 3], m[2, 3]
        if n == "Anker_Buergermeister":
            return tuple(welt[namen.index("Anker_Redner")][:3, 3])
        if n.startswith("Anker_Sitz_"):
            by = -z
            schenkel = M.U_SCHENKEL_Y[0] if by < M.U_MITTE_Y else M.U_SCHENKEL_Y[1]
            return (x, m[1, 3], -schenkel)
        if n == "Anker_Schreiber":
            return (M.U_X[0], m[1, 3], -M.U_SCHENKEL_Y[0])
        return (x, m[1, 3], -(M.RAEUME["ratshalle"][2] + 2.0))       # Bank: in den Saal

    def ratssitzung():
        # absoluter Pfad: os.path.join in glb_raum nimmt ihn so
        return sp.glb_raum(os.path.join(ORDNER, "rathaus_ratshalle_innen.glb"), ("Anker_Sitz_", "Anker_Buergermeister", "Anker_Schreiber", "Anker_Bank_"), ziel,
                           ["Tafel_Rat", "Kamin_Rat", "Gegenstand_Ratsbuch", "Antrag"],
                           lambda e: float(str(e.get("sitzhoehe", 0.44)).replace(",", ".")))

    sp.ratssitzung = ratssitzung
    if not any(a.startswith("--szenen=") for a in sys.argv):
        sys.argv.append("--szenen=Ratssitzung")
    return sp.main()


if __name__ == "__main__":
    sys.exit(main() or 0)
