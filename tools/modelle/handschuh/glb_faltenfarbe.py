"""Tauscht in einer .glb die Eckenfarb-Ebenen COLOR_0 und COLOR_1 (nur die Namen im JSON-Teil, Größe bleibt).

Blender exportiert die weiße Standard-Ebene als COLOR_0 und die Faltenfarbe (Grauwerte 0,75 bis 1) als COLOR_1. Godot übernimmt nur COLOR_0
(Eckenfarbe als Albedo), die Falten gingen verloren. Aufruf (außerhalb Blenders): python -I glb_faltenfarbe.py <ein.glb> <aus.glb>
"""
import struct
import sys


def tauschen(daten: bytes) -> bytes:
    if daten[:4] != b"glTF":
        raise SystemExit("keine .glb")
    laenge, art = struct.unpack("<II", daten[12:20])
    if art != 0x4E4F534A:  # "JSON"
        raise SystemExit("erster Block ist kein JSON")
    json_teil = daten[20:20 + laenge]
    if b'"COLOR_1"' not in json_teil:
        raise SystemExit("kein COLOR_1: schon getauscht oder keine Faltenfarbe")
    neu = json_teil.replace(b'"COLOR_0"', b'"COLOR_X"').replace(b'"COLOR_1"', b'"COLOR_0"').replace(b'"COLOR_X"', b'"COLOR_1"')
    assert len(neu) == len(json_teil)
    return daten[:20] + neu + daten[20 + laenge:]


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    with open(sys.argv[1], "rb") as f:
        ergebnis = tauschen(f.read())
    with open(sys.argv[2], "wb") as f:
        f.write(ergebnis)
    print("getauscht:", sys.argv[2])
