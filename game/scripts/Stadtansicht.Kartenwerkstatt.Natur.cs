using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Natur in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs).
///
/// <b>Natur der Flächen, live.</b> Wo welche Pflanze steht, rechnet der Kern wie im Spiel (<see cref="Kartenentwurf.NaturVon"/> mit einem
/// <see cref="Naturfreiraum"/>); hier wird nur gezeichnet, je Fläche und Art ein MultiMesh. Neu gestreut wird nur, was sich geändert hat: die
/// Fläche selbst, und jede Fläche in der Nähe eines geänderten Gebäudes, Weges oder einer Bauzone (am alten und am neuen Ort) – der Freiraum
/// einmal je Bild. So weicht die Natur einem Gebäude aus, das man darauf zieht, und wächst nach, wo es wegging. Die Werkstatt zeigt immer alle
/// Pflanzen (die Natur-Dichte der Grafikeinstellungen gilt nur im Spiel).
///
/// <b>Einzelne Pflanzen</b> sind je ein Knoten (wählen, ziehen, Q/E drehen, Entf löschen); gesetzt mit dem Werkzeug „Pflanze“ (eine Art aus dem
/// Katalog). <b>Pinsel</b>: Ziehen mit gedrückter Maus sammelt einen Strich, Loslassen streut darauf Pflanzen (<see cref="Kartenentwurf.PflanzenStreuen"/>,
/// ein Schritt). <b>Radierer</b>: Was der Kreis beim Ziehen berührt, verschwindet beim Loslassen (<see cref="Kartenentwurf.PflanzenLoeschen"/>).
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsNaturKnoten, _wsPflanzenKnoten;
    private readonly Dictionary<EntwurfNaturflaeche, Node3D> _wsNatur = new();
    private readonly Dictionary<EntwurfNaturflaeche, int> _wsNaturAnzahl = new();
    private readonly Dictionary<EntwurfPflanze, Node3D> _wsPflanzen = new();

    /// <summary>Flächen, deren Natur am Ende des Bildes neu gestreut wird, und Bereiche, in denen sich ein Hindernis geändert hat.</summary>
    private readonly HashSet<EntwurfNaturflaeche> _wsNaturNeu = new();
    private readonly List<Rect2> _wsNaturBereiche = new();

    /// <summary>So weit reicht eine Gruppe höchstens über ihren Streupunkt hinaus (Radius der Greybox-Gruppen bis 4 m).</summary>
    private const float NaturUeberstand = 6;

    /// <summary>Wie viele Pflanzen die Natur einer Fläche gerade hat (für die Tafel).</summary>
    public int WerkstattNaturAnzahl(EntwurfNaturflaeche f) => _wsNaturAnzahl.GetValueOrDefault(f);

    // ═════════════════════════════════════════════════════════════════════════
    //  Natur der Flächen
    // ═════════════════════════════════════════════════════════════════════════

    private void NaturBereichVormerken(Rect2 bereich) => _wsNaturBereiche.Add(bereich);

    /// <summary>Am Ende des Bildes: die vorgemerkten Flächen und alle in der Nähe geänderter Hindernisse neu streuen, mit einem Freiraum für alle.</summary>
    private void NaturNachziehen()
    {
        if (_entwurf is null || (_wsNaturNeu.Count == 0 && _wsNaturBereiche.Count == 0)) return;
        foreach (var f in _entwurf.Naturflaechen)
        {
            if (f.Ecken.Count == 0 || _wsNaturNeu.Contains(f)) continue;
            var rahmen = Rahmen(f.Ecken).Grow(NaturUeberstand);
            if (_wsNaturBereiche.Any(b => b.Intersects(rahmen))) _wsNaturNeu.Add(f);
        }
        _wsNaturBereiche.Clear();
        if (_wsNaturNeu.Count == 0) return;
        var frei = _entwurf.Freiraum(Modellmass);
        foreach (var f in _wsNaturNeu.Where(_entwurf.Enthaelt).ToList()) NaturStreuen(f, frei);
        var gewaehlt = WerkstattGewaehlt is EntwurfNaturflaeche g && _wsNaturNeu.Contains(g);
        _wsNaturNeu.Clear();
        if (gewaehlt) WerkstattNaturGestreut?.Invoke();
    }

    /// <summary>Die Natur der gewählten Fläche ist neu gestreut (die Tafel zeigt die Zahl der Pflanzen).</summary>
    public event Action? WerkstattNaturGestreut;

    /// <summary>Die Natur einer Fläche neu: die Lagen des Kerns, je Art (Datei, Knoten mit Variante) ein MultiMesh.</summary>
    private void NaturStreuen(EntwurfNaturflaeche f, Naturfreiraum frei)
    {
        NaturEntfernen(f);
        if (_entwurf is null) return;
        if (_wsNaturKnoten is null)
        {
            _wsNaturKnoten = new Node3D { Name = "Natur" };
            AddChild(_wsNaturKnoten);
        }
        var lagen = _entwurf.NaturVon(f, frei);
        var knoten = new Node3D { Name = "Natur_" + f.Bezeichnung };
        _wsNaturKnoten.AddChild(knoten);
        _wsNatur[f] = knoten;
        _wsNaturAnzahl[f] = lagen.Count;
        foreach (var art in lagen.Where(l => l.Art is { Modell: not null, Knoten: not null }).GroupBy(l => (Datei: l.Art.Modell!, Knoten: l.Art.Knoten + l.Variante)))
        {
            if (NaturNetz(art.Key.Datei, art.Key.Knoten) is not { } netz) continue;
            var liste = art.ToList();
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = liste.Count };
            for (var i = 0; i < liste.Count; i++) vielfach.SetInstanceTransform(i, PflanzenLage(liste[i]));
            knoten.AddChild(new MultiMeshInstance3D
            {
                Name = art.Key.Knoten, Multimesh = vielfach,
                CastShadow = art.Key.Datei == "natur/natur_boden" ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
    }

    private void NaturEntfernen(EntwurfNaturflaeche f)
    {
        if (_wsNatur.Remove(f, out var alt)) alt.QueueFree();
        _wsNaturAnzahl.Remove(f);
    }

    /// <summary>Lage einer Pflanze des Kerns: Ort, Drehung (Grad) um die Hochachse, Größe.</summary>
    private static Transform3D PflanzenLage(Pflanzenlage l) =>
        new(new Basis(Vector3.Up, Mathf.DegToRad((float)l.Drehung)).Scaled(Vector3.One * (float)l.Massstab), new Vector3((float)l.X, 0, (float)l.Z));

    // ═════════════════════════════════════════════════════════════════════════
    //  Einzelne Pflanzen
    // ═════════════════════════════════════════════════════════════════════════

    private void PflanzenAufbauen()
    {
        _wsPflanzenKnoten = new Node3D { Name = "Pflanzen" };
        AddChild(_wsPflanzenKnoten);
        foreach (var p in _entwurf!.Pflanzen) PflanzeBauen(p, false);
    }

    /// <summary>Baut den Knoten einer Pflanze neu: an ihrem Punkt, darunter das Netz (gedreht, mit Maßstab) und ein Ring als Markierung.</summary>
    private void PflanzeBauen(EntwurfPflanze p, bool weg)
    {
        if (_wsPflanzen.Remove(p, out var alt)) alt.QueueFree();
        if (weg || _wsPflanzenKnoten is null || _entwurf?.LageVon(p) is not { Art: { Modell: { } datei, Knoten: { } knoten } } lage) return;
        var wurzel = new Node3D { Name = "Pflanze", Position = V3(p.Punkt) };
        _wsPflanzenKnoten.AddChild(wurzel);
        _wsPflanzen[p] = wurzel;
        if (NaturNetz(datei, knoten + lage.Variante) is { } netz)
            wurzel.AddChild(new MeshInstance3D { Name = "Netz", Mesh = netz, Transform = PflanzenLage(lage with { X = 0, Z = 0 }) });
        PflanzeMarkieren(p);
    }

    /// <summary>Der Ring unter einer Pflanze: gold, wenn gewählt, rot bei einem Fehler, sonst keiner.</summary>
    private void PflanzeMarkieren(EntwurfPflanze p)
    {
        if (!_wsPflanzen.TryGetValue(p, out var wurzel)) return;
        wurzel.GetNodeOrNull("Markierung")?.Free();
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, p);
        var fehler = Fehlerhaft(p);
        if (!gewaehlt && !fehler) return;
        var groesse = wurzel.GetNodeOrNull<MeshInstance3D>("Netz") is { } netz ? netz.GetAabb().Size * (float)p.Massstab : Vector3.One;
        var radius = Mathf.Max(0.8f, Mathf.Max(groesse.X, groesse.Z) / 2);
        wurzel.AddChild(new MeshInstance3D
        {
            Name = "Markierung", Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + 0.3f, Rings = 32 }, Position = new Vector3(0, 0.2f, 0),
            MaterialOverride = ImmerSichtbar(Transparent(fehler ? Fehlerrot : Auswahlgold, 0.95f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Die Pflanze am nächsten am Punkt, höchstens 1,5 m (bzw. <paramref name="toleranz"/>) entfernt – null: keine.</summary>
    private EntwurfPflanze? PflanzeBei(Vector2 p, float toleranz)
    {
        EntwurfPflanze? beste = null;
        var abstand = Mathf.Max(1.5f, toleranz);
        foreach (var pflanze in _entwurf!.Pflanzen)
        {
            var d = p.DistanceTo(V2(pflanze.Punkt));
            if (d <= abstand) (beste, abstand) = (pflanze, d);
        }
        return beste;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeug „Pflanze“: die gewählte Art an der Maus
    // ═════════════════════════════════════════════════════════════════════════

    private MeshInstance3D? _wsGeist;

    private void PflanzenGeistZeigen(Vector2? maus)
    {
        if (maus is not { } m || _entwurf?.Katalog.Finde(WerkstattBaustein) is not { Modell: { } datei, Knoten: { } knoten })
        {
            PflanzenGeistAus();
            return;
        }
        var netz = NaturNetz(datei, knoten + 1);
        _wsGeist ??= new MeshInstance3D { Name = "Pflanzengeist", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        if (_wsGeist.GetParent() is null) AddChild(_wsGeist);
        _wsGeist.Mesh = netz;
        _wsGeist.Visible = true;
        _wsGeist.Position = new Vector3(m.X, 0, m.Y);
        _wsGeist.Rotation = new Vector3(0, Mathf.DegToRad((float)WerkstattDrehung), 0);
        VorschauPunkte(new[] { m }, Colors.White);
    }

    private void PflanzenGeistAus()
    {
        if (_wsGeist is not null) _wsGeist.Visible = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Pinsel und Radierer
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Halbmesser des Pinsels bzw. Radierers (Meter).</summary>
    public double PinselRadius { get; set; } = 6;

    /// <summary>Pflanzen je 1.000 m² des Strichs.</summary>
    public double PinselDichte { get; set; } = 30;

    /// <summary>Welche Arten der Pinsel streut (Kennung → Gewicht).</summary>
    public Dictionary<string, double> PinselArten { get; } = new() { ["eiche"] = 1, ["buche"] = 1, ["hasel"] = 1, ["holunder"] = 1 };

    /// <summary>Der Strich, der gerade gezogen wird (null: keiner), und was der Radierer dabei schon berührt hat.</summary>
    private List<Vector2>? _wsStrich;
    private readonly HashSet<EntwurfPflanze> _wsRadiert = new();

    private MeshInstance3D? _wsPinselKreis;

    private void StrichBeginnen(Vector2 maus)
    {
        if (BodenpunktBei(maus) is not { } boden) return;
        _wsStrich = new List<Vector2> { boden };
        _wsRadiert.Clear();
        if (Kartenwerkzeug == Werkstattwerkzeug.Radierer) Radieren(boden);
    }

    /// <summary>Beim Ziehen: Punkte sammeln (etwa alle drittel Halbmesser), der Radierer blendet berührte Pflanzen schon aus.</summary>
    private void StrichWeiter(Vector2 maus)
    {
        if (_wsStrich is null || BodenpunktBei(maus) is not { } boden) return;
        if (Kartenwerkzeug == Werkstattwerkzeug.Radierer) Radieren(boden);
        if (boden.DistanceTo(_wsStrich[^1]) >= Mathf.Max(0.5f, (float)PinselRadius / 3)) _wsStrich.Add(boden);
    }

    private void Radieren(Vector2 boden)
    {
        foreach (var p in _entwurf!.Pflanzen)
            if (V2(p.Punkt).DistanceTo(boden) <= PinselRadius && _wsRadiert.Add(p) && _wsPflanzen.TryGetValue(p, out var knoten)) knoten.Visible = false;
    }

    /// <summary>Loslassen: der Pinsel streut (ein Schritt), der Radierer löscht, was er berührt hat (ein Schritt).</summary>
    private void StrichBeenden()
    {
        if (_wsStrich is not { } strich || _entwurf is null) return;
        _wsStrich = null;
        if (Kartenwerkzeug == Werkstattwerkzeug.Pinsel) WerkstattStreuen(strich.Select(p => new Kartenort(p.X, p.Y)).ToList());
        else if (Kartenwerkzeug == Werkstattwerkzeug.Radierer && _wsRadiert.Count > 0)
        {
            var weg = _wsRadiert.ToList();
            _wsRadiert.Clear();
            Befehl(() => _entwurf.PflanzenLoeschen(weg));
            WerkstattMeldung?.Invoke($"{weg.Count} Pflanzen gelöscht.", false);
        }
    }

    /// <summary>Esc beim Ziehen: kein Befehl, ausgeblendete Pflanzen kommen wieder.</summary>
    private void StrichAbbrechen()
    {
        _wsStrich = null;
        foreach (var p in _wsRadiert)
            if (_wsPflanzen.TryGetValue(p, out var knoten)) knoten.Visible = true;
        _wsRadiert.Clear();
    }

    /// <summary>Streut mit den Einstellungen des Pinsels entlang des Strichs (ein Schritt) und meldet, wie viele es wurden.</summary>
    public int WerkstattStreuen(IReadOnlyList<Kartenort> strich)
    {
        if (_entwurf is null) return 0;
        if (PinselArten.Count == 0)
        {
            WerkstattMeldung?.Invoke("Der Pinsel hat keine Arten – rechts mindestens eine ankreuzen.", true);
            return 0;
        }
        var neue = Befehl(() => _entwurf.PflanzenStreuen(strich, PinselRadius, PinselDichte, PinselArten, modellmass: Modellmass));
        var anzahl = neue?.Count ?? 0;
        WerkstattMeldung?.Invoke(anzahl == 0 ? "Hier ist kein Platz für Pflanzen (Wege, Gebäude, Bauzonen und die Stadt bleiben frei)." : $"{anzahl} Pflanzen gestreut.", anzahl == 0);
        return anzahl;
    }

    /// <summary>Für Startoptionen: löscht alle einzelnen Pflanzen im Kreis des Radierers um die Punkte (ein Schritt).</summary>
    public int WerkstattRadieren(IReadOnlyList<Kartenort> strich)
    {
        if (_entwurf is null) return 0;
        var weg = _entwurf.Pflanzen.Where(p => strich.Any(s => s.Abstand(p.Punkt) <= PinselRadius)).ToList();
        if (weg.Count > 0) Befehl(() => _entwurf.PflanzenLoeschen(weg));
        return weg.Count;
    }

    /// <summary>Jedes Bild mit Pinsel oder Radierer: der Kreis an der Maus, der Strich bis jetzt (Pinsel grün, Radierer rot).</summary>
    private void PinselZeigen(Vector2? maus)
    {
        var radierer = Kartenwerkzeug == Werkstattwerkzeug.Radierer;
        var farbe = radierer ? new Color(0.95f, 0.35f, 0.3f) : new Color(0.45f, 0.95f, 0.45f);
        if (_wsPinselKreis is null)
        {
            _wsPinselKreis = new MeshInstance3D
            {
                Name = "Pinselkreis", Mesh = new TorusMesh { InnerRadius = 0.97f, OuterRadius = 1f, Rings = 48, RingSegments = 6 },
                MaterialOverride = ImmerSichtbar(Transparent(Colors.White, 0.9f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_wsPinselKreis);
        }
        _wsPinselKreis.Visible = maus is not null;
        ((StandardMaterial3D)_wsPinselKreis.MaterialOverride).AlbedoColor = new Color(farbe, 0.9f);
        if (maus is { } m)
        {
            _wsPinselKreis.Position = new Vector3(m.X, 0.3f, m.Y);
            _wsPinselKreis.Scale = new Vector3((float)PinselRadius, 1, (float)PinselRadius);
        }
        if (_wsStrich is { Count: > 0 } strich && !radierer)
        {
            var linie = maus is { } mm && mm.DistanceTo(strich[^1]) > 0.5f ? strich.Append(mm).ToList() : strich;
            if (linie.Count >= 2) VorschauDreiecke(BandDreiecke(linie, (float)PinselRadius, 0.15f), new Color(farbe, 0.22f));
        }
    }

    /// <summary>Kreis des Pinsels aus (anderes Werkzeug).</summary>
    private void PinselAus()
    {
        if (_wsPinselKreis is not null) _wsPinselKreis.Visible = false;
    }
}
