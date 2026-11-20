extends SceneTree
# Laedt die drei 3D-Gegenstaende in Godot (ohne Fenster) und prueft, was der Einbau braucht: Knoten, AnimationPlayer mit den Clips und ihren Laengen,
# Lage einzelner Knochen am Clipende gegen die Sollwerte, Fuellstufen der Geldtruhe. Zeigt zugleich, wie die Oberflaeche die Modelle benutzt
# (Umriss-Material, CustomAabb, Fuellstufen umschalten, Inhaltsflaechen mit eigenem Material).
#   godot --headless --path <projekt> --script res://godot_pruefen.gd
# Im Projekt liegen die .glb unter res://assets/modelle/oberflaeche/ (vorher einmal --import).

const ORDNER := "res://assets/modelle/oberflaeche/"
var fehler := 0


func _initialize() -> void:
	_kartentisch()
	_geldtruhe()
	_siegelstempel()
	print("GODOT FERTIG" if fehler == 0 else "GODOT %d Fehler" % fehler)
	quit(1 if fehler else 0)


func _p(bed: bool, text: String) -> void:
	print(("ok     " if bed else "FEHLER ") + text)
	if not bed:
		fehler += 1


func _laden(name: String) -> Node3D:
	var szene := load(ORDNER + name + ".glb") as PackedScene
	_p(szene != null, name + ".glb laedt")
	var n := szene.instantiate() as Node3D
	root.add_child(n)
	_material_setzen(n)
	return n


# So setzt die Oberflaeche das Umriss-Material (wie HandschuhZeiger.MaterialienEinrichten) und die CustomAabb der Netze mit Haut.
func _material_setzen(n: Node) -> void:
	for k in n.find_children("*", "MeshInstance3D", true, false):
		var m := k as MeshInstance3D
		if String(m.name).contains("Umriss"):
			m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			for i in m.mesh.get_surface_count():
				var s := m.mesh.surface_get_material(i) as StandardMaterial3D
				if s != null:
					s.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
					s.cull_mode = BaseMaterial3D.CULL_BACK


func _clips(n: Node, soll: Dictionary) -> AnimationPlayer:
	var ap := n.find_children("*", "AnimationPlayer", true, false)[0] as AnimationPlayer
	for c in soll:
		_p(ap.has_animation(c), "Clip " + c)
		if ap.has_animation(c):
			_p(absf(ap.get_animation(c).length - soll[c]) < 0.002, "Clip %s %.3f s (Soll %.3f)" % [c, ap.get_animation(c).length, soll[c]])
	return ap


func _knochen(alt: Node, clip: String, t: float, knochen: String, roh := false) -> Transform3D:
	# je Abfrage eine frische Instanz: ein Wechsel des Clips greift im selben Bild sonst nicht sicher
	var n := (load(alt.scene_file_path) as PackedScene).instantiate()
	root.add_child(n)
	var ap := n.find_children("*", "AnimationPlayer", true, false)[0] as AnimationPlayer
	var sk := n.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	ap.play(clip)
	ap.seek(t, true)
	var i := sk.find_bone(knochen)
	# Verformung gegen die Ruhelage (die Ruhelage des Knochens bringt eine eigene Drehung aus dem glTF mit)
	var g := sk.get_bone_global_pose(i) * sk.get_bone_global_rest(i).affine_inverse()
	var o := sk.get_bone_global_pose(i).origin
	if roh:
		g = sk.get_bone_global_pose(i)
	n.queue_free()
	return Transform3D(g.basis, o)


func _kartentisch() -> void:
	print("--- kartentisch")
	var n := _laden("kartentisch")
	_clips(n, {"zu": 1.0 / 30.0, "offen": 1.0 / 30.0, "entrollen": 37.0 / 30.0, "zusammenrollen": 1.0})
	_p(n.find_child("Kartenflaeche", true, false) != null, "Knoten Kartenflaeche")
	var sk := n.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var letzte := "Karte_%02d" % (sk.get_bone_count() - 2)
	var zu := _knochen(n, "zu", 0.0, letzte)
	var offen := _knochen(n, "offen", 0.0, letzte)
	_p(zu.origin.y > 0.79 and zu.origin.x < -0.2, "Karte zu: letzte Linie in der Rolle links (%s)" % str(zu.origin))
	_p(absf(offen.origin.y - 0.7808) < 0.002 and offen.origin.x > 0.3, "Karte offen: letzte Linie flach rechts (%s)" % str(offen.origin))
	var b_letzte := _knochen(n, "offen", 0.0, letzte, true).basis
	var b_erste := _knochen(n, "offen", 0.0, "Karte_00", true).basis
	_p((b_letzte * b_erste.inverse()).get_euler().length() < 0.01, "Karte offen: letzte Linie liegt so flach wie die erste")


func _geldtruhe() -> void:
	print("--- geldtruhe")
	var n := _laden("geldtruhe")
	_clips(n, {"zu": 1.0 / 30.0, "offen": 1.0 / 30.0, "aufspringen": 31.0 / 30.0, "zuklappen": 0.7})
	var d := _knochen(n, "offen", 0.0, "Deckel")
	# Winkel zwischen der Deckelnormale und oben = Oeffnungswinkel
	var oben := d.basis * Vector3.UP
	var w := rad_to_deg(Vector3.UP.angle_to(oben))
	_p(absf(w - 104.0) < 0.5, "Deckel offen um %.1f Grad aufgeklappt (Soll 104)" % w)
	var z := _knochen(n, "zuklappen", 0.7, "Deckel")
	_p(rad_to_deg(Vector3.UP.angle_to(z.basis * Vector3.UP)) < 0.5, "Deckel nach zuklappen zu")
	# Fuellstufen: genau eine sichtbar (oder keine = leer)
	for stufe in ["leer", "viertel", "halb", "voll"]:
		for s in ["viertel", "halb", "voll"]:
			(n.find_child("Fuellstufe_" + s, true, false) as Node3D).visible = (s == stufe)
		var an := 0
		for s in ["viertel", "halb", "voll"]:
			if (n.find_child("Fuellstufe_" + s, true, false) as Node3D).visible:
				an += 1
		_p(an == (0 if stufe == "leer" else 1), "Fuellstufe %s umschaltbar" % stufe)
	_p(n.find_child("Muenzen_voll", true, false) != null and n.find_child("Muenzhaufen_voll", true, false) != null, "Muenznetze unter den Fuellstufen")


func _siegelstempel() -> void:
	print("--- siegelstempel")
	var n := _laden("siegelstempel")
	_clips(n, {"bereit": 1.0 / 30.0, "gesiegelt": 1.0 / 30.0, "siegeln": 38.0 / 30.0})
	var p := _knochen(n, "siegeln", 21.0 / 30.0, "Stempel")
	_p(absf(p.origin.y - 0.0071) < 0.0005 and absf(p.origin.x + 0.025) < 0.001, "Stempel presst auf dem Wachs (%s)" % str(p.origin))
	var w := _knochen(n, "gesiegelt", 0.0, "Wachs_Rand")
	_p(absf(w.basis.get_scale().x - 1.55) < 0.01 and absf(w.basis.get_scale().y - 0.55) < 0.01, "Wachs gesiegelt breiter und flacher (Skala %s)" % str(w.basis.get_scale()))
	var a := _knochen(n, "bereit", 0.0, "Abdruck")
	var b := _knochen(n, "gesiegelt", 0.0, "Abdruck")
	_p(b.origin.y - a.origin.y > 0.002, "Abdruck taucht beim Siegeln aus dem Wachs auf")
	_p(n.find_child("Abdruck_Wappen", true, false) != null, "Knoten Abdruck_Wappen")
