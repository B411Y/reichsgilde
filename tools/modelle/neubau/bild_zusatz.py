"""Zusatzbilder fuer die Pruefung der Patrizierhaeuser (Blender-Rechenvorschau, wie die Kontrollbilder von wohnhaus_mensch_s23.py):

    og_nah     Obergeschoss und Gurtgesims der Gassenseite von vorn-links aus der Naehe (Wandband, Laeden, Gurtgesims, Kielbogen)
    og_rechts  Obergeschoss und Giebelfuss von der rechten Seite aus der Naehe (Laeden auf der Seitenwand)

Gerufen aus den Bauskripten nach `S23.main()`, wenn `--bilder=<ordner>` gesetzt ist; nur die Bilder, deren Name zu `REICHSGILDE_BILDER`
passt (wie g1.kontrollbild). Die Huelle bleibt, Innenraeume, Leerstand und Geruest werden verborgen."""
import os

HUELLE = ("Haus", "Dach", "Scheiben", "Wappen", "Fahne", "Kontrollboden", "Kontrollkamera", "Sonne")


def zeichne(g1, M, ordner, stufe, form, dx, bpy):
    verbergen = [o.name for o in bpy.data.objects if o.type == "MESH" and o.name.split(".")[0] not in HUELLE]
    vor = f"s{stufe}_g{form}_blender_"
    z0 = M.Z_EG_DECKE
    g1.kontrollbild(os.path.join(ordner, vor + "og_nah.jpg"), (-4.2 + dx, M.OVORN - 6.0, z0 + 1.6), (-0.8 + dx, M.OVORN, z0 + 1.2), brennweite=35, verbergen=verbergen)
    g1.kontrollbild(os.path.join(ordner, vor + "og_rechts.jpg"), (M.OX + 6.0 + dx, M.VORN + 2.5, z0 + 1.6), (M.OX + dx, M.VORN + 5.0, z0 + 1.0), brennweite=35, verbergen=verbergen)
