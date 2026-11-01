using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Felsstücke in der Kartenwerkstatt (Felspinsel F4) – ein Teil der <c>partial class Stadtansicht</c>. Der Kern führt die Stücke und den Pinsel
/// (<see cref="Kartenentwurf.FelsStrichBeginnen"/>, <see cref="Kartenentwurf.FelsStempeln"/>); hier steht, was die Werkstatt in der 3D-Welt daraus macht:
/// <list type="bullet">
/// <item><b>Werkzeug</b> <see cref="Werkstattwerkzeug.Fels"/> in drei Arten (<see cref="FelsArt"/>): <b>Streuen</b> mit gedrückter Maus (jede Mausbewegung
/// gibt der Kern weiter, die Stücke erscheinen sofort, Loslassen macht den Strich zu einem Schritt, Esc nimmt ihn zurück), <b>Stempeln</b> mit einem
/// Klick (ein Stück oder eine Gruppe; die Vorschau unter der Maus zeigt genau, was der Klick setzt), <b>Radieren</b> wie Streuen.</item>
/// <item><b>Vorschau</b>: Kreis des Pinsels; beim Stempeln dazu die Stücke als helle Geister (höchstens acht Netze, je Bild nur neu gesetzt).</item>
/// <item><b>VORLÄUFIGE Anzeige</b> der Stücke: je Stück ein MeshInstance3D mit dem F1-Netz (Stufe 1) und dem Fels-Material aus F2 (<see cref="Gesteinsarten"/>).
/// F5 ersetzt sie durch MultiMeshes je Form mit Stufen nach dem Zoom und Schatten nur nah – bis dahin nur in der Werkstatt, nicht im Spiel.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Was das Felswerkzeug tut.</summary>
    public enum FelsArt
    {
        Streuen,
        Stempeln,
        Radieren,
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einstellungen (die Tafel rechts ändert sie)
    // ═════════════════════════════════════════════════════════════════════════

    public FelsArt FelsModus { get; set; } = FelsArt.Stempeln;

    /// <summary>Vorlage: eine Gruppe (wand, kuppe, rippe) – oder null, dann die Formen in <see cref="FelsFormen"/>.</summary>
    public string? FelsGruppe { get; set; } = "wand";

    /// <summary>Die Formen für Einzelstücke (zufällig gleich verteilt).</summary>
    public List<string> FelsFormen { get; } = new() { "block" };

    /// <summary>Gesteinsart der neuen Stücke (null: das Grundgestein der Karte).</summary>
    public string? FelsGestein { get; set; }

    public double FelsRadius { get; set; } = 10;
    public double FelsDichte { get; set; } = 0.2;
    public double FelsGroesse { get; set; } = 1;
    public double FelsStreuung { get; set; } = 0.25;
    public double FelsEinsinken { get; set; } = Felsformen.EinsinkenVorgabe;
    public double FelsNeigung { get; set; } = 0.5;
    public bool FelsZumHang { get; set; } = true;

    /// <summary>Wo der Felspinsel wirkt (Steigung, Boden); <see cref="Pinselfilter.Keiner"/>: überall.</summary>
    public Pinselfilter FelsFilter { get; set; } = Pinselfilter.Keiner;

    /// <summary>Startwert des nächsten Stempels: Vorschau und Klick würfeln gleich, nach jedem Stempel kommt ein neuer („neu würfeln“).</summary>
    public int FelsStempelStartwert { get; set; } = 1;

    /// <summary>Der Pinsel mit den gewählten Werten.</summary>
    public Felspinsel FelsPinsel => FelsModus == FelsArt.Radieren
        ? Felspinsel.Radiergummi(FelsRadius, FelsFilter)
        : new Felspinsel(FelsFormen.ToArray(), FelsGruppe, FelsGestein, FelsRadius, FelsDichte, FelsGroesse, FelsStreuung, FelsEinsinken, FelsNeigung,
            FelsZumHang, FelsFilter);

    /// <summary>Die Farbe einer Gesteinsart (hell) für Symbol und Kreis.</summary>
    public static Color FelsFarbe(string? gestein) => Gesteinsarten.Nach(gestein ?? Felsformen.GrundgesteinVorgabe).Hell;

    // ═════════════════════════════════════════════════════════════════════════
    //  Strich und Stempel
    // ═════════════════════════════════════════════════════════════════════════

    private bool _wsFelsStrich;

    /// <summary>Linke Maustaste mit dem Felswerkzeug: Stempeln setzt gleich, Streuen und Radieren beginnen einen Strich.</summary>
    private void FelsDruecken(Vector2 maus)
    {
        if (_entwurf is null || BodenpunktBei(maus) is not { } boden) return;
        if (FelsModus == FelsArt.Stempeln)
        {
            FelsStempelnBei(new Kartenort(boden.X, boden.Y));
            return;
        }
        Befehl(() => _entwurf.FelsStrichBeginnen(FelsPinsel));
        _wsFelsStrich = true;
        FelsStrichPunkt(boden);
    }

    private void FelsStempelnBei(Kartenort ort)
    {
        if (_entwurf is null) return;
        var neue = Befehl(() => _entwurf.FelsStempeln(FelsPinsel, ort, startwert: FelsStempelStartwert));
        if (neue is { Count: > 0 }) FelsStempelStartwert++;
        else WerkstattMeldung?.Invoke("Hier setzt der Stempel nichts – der Filter passt nicht oder der Punkt liegt außerhalb der Karte.", true);
    }

    private void FelsStrichWeiter(Vector2 maus)
    {
        if (!_wsFelsStrich || BodenpunktBei(maus) is not { } boden) return;
        FelsStrichPunkt(boden);
    }

    private void FelsStrichPunkt(Vector2 boden) => Befehl(() => _entwurf!.FelsStrichWeiter(new Kartenort(boden.X, boden.Y)));

    /// <summary>Loslassen: Der Strich wird ein Schritt der Geschichte.</summary>
    private void FelsStrichBeenden()
    {
        if (!_wsFelsStrich || _entwurf is null) return;
        _wsFelsStrich = false;
        _entwurf.FelsStrichBeenden();
    }

    /// <summary>Esc beim Ziehen: Der Strich wird beendet und gleich zurückgenommen.</summary>
    private void FelsStrichAbbrechen()
    {
        if (!_wsFelsStrich || _entwurf is null) return;
        _wsFelsStrich = false;
        if (_entwurf.FelsStrichBeenden()) _entwurf.Geschichte.Rueckgaengig();
    }

    private void FelsWerkzeugAus()
    {
        FelsStrichAbbrechen();
        FelsGeisterAus();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschau unter der Maus
    // ═════════════════════════════════════════════════════════════════════════

    private Node3D? _wsFelsGeister;
    private StandardMaterial3D? _wsFelsGeistStoff;

    /// <summary>Jedes Bild mit dem Felswerkzeug: der Kreis (Streuen, Radieren) bzw. die Geister des Stempels.</summary>
    private void FelsPinselZeigen(Vector2? maus)
    {
        if (maus is not { } m || _entwurf is null)
        {
            FelsGeisterAus();
            return;
        }
        var farbe = FelsModus == FelsArt.Radieren ? new Color(0.95f, 0.35f, 0.3f) : FelsFarbe(FelsGestein ?? _entwurf.Grundgestein.Wirksam).Lightened(0.2f);
        if (FelsModus == FelsArt.Stempeln)
        {
            var lagen = _entwurf.FelsStempelVorschau(FelsPinsel, new Kartenort(m.X, m.Y), startwert: FelsStempelStartwert);
            FelsGeisterSetzen(lagen);
            foreach (var l in lagen)
                VorschauDreiecke(BandDreiecke(Kreis(new Vector2((float)l.X, (float)l.Z), Mathf.Max(0.3f, (float)l.Radius)), Mathf.Max(0.06f, MeterJePixel * 0.8f), 0.2f),
                    new Color(farbe, 0.8f));
            if (lagen.Count == 0) VorschauPunkte(new[] { m }, new Color(0.95f, 0.35f, 0.3f, 0.95f));
            return;
        }
        FelsGeisterAus();
        var radius = (float)Math.Clamp(FelsRadius, Felspinsel.MinRadius, Felspinsel.MaxRadius);
        VorschauDreiecke(BandDreiecke(Kreis(m, radius), Mathf.Max(0.12f, MeterJePixel * 1.2f), 0.2f), new Color(farbe, 0.95f));
        VorschauPunkte(new[] { m }, new Color(farbe, 0.95f));
    }

    /// <summary>Die Geister des Stempels: je Stück ein Netz (wiederverwendet, höchstens so viele wie die größte Gruppe), durchscheinend hell.</summary>
    private void FelsGeisterSetzen(IReadOnlyList<Felslage> lagen)
    {
        if (_wsFelsGeister is null)
        {
            _wsFelsGeister = new Node3D { Name = "FelsGeister" };
            AddChild(_wsFelsGeister);
            _wsFelsGeistStoff = Transparent(new Color(0.92f, 0.9f, 0.82f), 0.55f);
        }
        _wsFelsGeister.Visible = true;
        var kinder = _wsFelsGeister.GetChildren().OfType<MeshInstance3D>().ToList();
        for (var i = 0; i < Math.Max(kinder.Count, lagen.Count); i++)
        {
            if (i >= lagen.Count)
            {
                kinder[i].Visible = false;
                continue;
            }
            var l = lagen[i];
            if (i >= kinder.Count)
            {
                var neu = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = _wsFelsGeistStoff };
                _wsFelsGeister.AddChild(neu);
                kinder.Add(neu);
            }
            var mi = kinder[i];
            mi.Mesh = FelsNetz(l.Form);
            mi.Transform = FelsLage(l.Form, l.X, l.Z, l.Drehung, l.Massstab, l.Neigung, l.Einsinken, l.KippenX, l.KippenZ);
            mi.Visible = mi.Mesh is not null;
        }
    }

    private void FelsGeisterAus()
    {
        if (_wsFelsGeister is not null) _wsFelsGeister.Visible = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  VORLÄUFIGE Anzeige der Stücke (bis F5): je Stück ein Netz
    // ═════════════════════════════════════════════════════════════════════════

    private const string FelsModell = "natur/natur_felsen";

    /// <summary>Die Stufe der Netze in der Werkstatt (1 = mittel); F5 wählt sie nach dem Zoom.</summary>
    private const int FelsStufe = 1;

    private readonly Dictionary<EntwurfFels, MeshInstance3D> _wsFelsen = new();
    private readonly Dictionary<string, ShaderMaterial> _wsFelsStoffe = new();
    private Node3D? _wsFelsenKnoten;

    /// <summary>Beim Aufbau der Werkstatt: alle Stücke.</summary>
    private void FelsenAufbauen()
    {
        if (_entwurf is null) return;
        _wsFelsenKnoten = new Node3D { Name = "Felsen" };
        AddChild(_wsFelsenKnoten);
        foreach (var f in _entwurf.Felsen) FelsBauen(f, false);
    }

    private Mesh? FelsNetz(string form) => Felsformen.Finde(form) is { } f ? NaturNetz(FelsModell, $"{f.Knoten}_{FelsStufe}") : null;

    /// <summary>Das Fels-Material einer Gesteinsart (eins je Art, für alle Stücke geteilt).</summary>
    private ShaderMaterial FelsStoff(string gestein)
    {
        if (!_wsFelsStoffe.TryGetValue(gestein, out var stoff)) _wsFelsStoffe[gestein] = stoff = Gesteinsarten.Material(Gesteinsarten.Nach(gestein), dreiseitig: true);
        return stoff;
    }

    /// <summary>
    /// Die Lage eines Stücks in der Welt: Drehung um die Senkrechte, geneigt zur Hangnormalen (Anteil <paramref name="neigung"/>), gekippt um die eigenen
    /// Achsen, um <paramref name="einsinken"/> seiner Höhe unter den Boden unter der Mitte gesenkt, gleichmäßig skaliert.
    /// </summary>
    private Transform3D FelsLage(string form, double x, double z, double drehung, double massstab, double neigung, double einsinken, double kippenX, double kippenZ)
    {
        var hoehe = (float)((Felsformen.Finde(form)?.Hoehe ?? 1) * massstab);
        var oben = Vector3.Up;
        if (_entwurf is not null && neigung > 0)
        {
            var (gx, gz) = Felsstreuung.Hang(_entwurf.Hoehen, x, z);
            var normale = new Vector3(-(float)gx, 1, -(float)gz).Normalized();
            oben = oben.Slerp(normale, (float)neigung).Normalized();
        }
        var basis = new Basis(Vector3.Up, Mathf.DegToRad((float)drehung))
                    * new Basis(Vector3.Right, Mathf.DegToRad((float)kippenX)) * new Basis(Vector3.Back, Mathf.DegToRad((float)kippenZ));
        if (!oben.IsEqualApprox(Vector3.Up))
        {
            var achse = Vector3.Up.Cross(oben).Normalized();
            basis = new Basis(achse, Vector3.Up.AngleTo(oben)) * basis;
        }
        var boden = BodenHoehe((float)x, (float)z);
        return new Transform3D(basis.Scaled(Vector3.One * (float)massstab), new Vector3((float)x, boden - (float)einsinken * hoehe, (float)z));
    }

    /// <summary>Ein Stück neu (oder weg, <paramref name="weg"/>).</summary>
    private void FelsBauen(EntwurfFels f, bool weg)
    {
        if (_wsFelsen.Remove(f, out var alt)) alt.QueueFree();
        if (weg || _wsFelsenKnoten is null || _entwurf is null || FelsNetz(f.Form) is not { } netz) return;
        var mi = new MeshInstance3D
        {
            Name = "Fels", Mesh = netz, MaterialOverride = FelsStoff(_entwurf.GesteinVon(f)),
            Transform = FelsLage(f.Form, f.X, f.Z, f.Drehung, f.Massstab, f.Neigung, f.Einsinken, f.KippenX, f.KippenZ),
        };
        _wsFelsenKnoten.AddChild(mi);
        _wsFelsen[f] = mi;
    }

    /// <summary>Das Grundgestein hat sich geändert: alle Stücke ohne eigenes Gestein bekommen das neue Material.</summary>
    private void FelsenGesteinNeu()
    {
        if (_entwurf is null) return;
        foreach (var (f, mi) in _wsFelsen) mi.MaterialOverride = FelsStoff(_entwurf.GesteinVon(f));
    }

    /// <summary>Das Gelände hat sich in diesem Rechteck geändert: die Stücke darin folgen dem Boden (aus <see cref="GelaendeNachziehen"/>).</summary>
    private void FelsenImBereich(Rect2 rect)
    {
        foreach (var (f, mi) in _wsFelsen)
            if (rect.Grow((float)f.Radius).HasPoint(V2(f.Punkt)))
                mi.Transform = FelsLage(f.Form, f.X, f.Z, f.Drehung, f.Massstab, f.Neigung, f.Einsinken, f.KippenX, f.KippenZ);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Startoption
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Für Startoptionen: ein ganzer Strich (Streuen oder Radieren, ein Schritt). False: nichts geändert.</summary>
    public bool WerkstattFelsStreuen(Felspinsel pinsel, IReadOnlyList<Kartenort> strich) =>
        _entwurf is not null && Befehl(() => _entwurf.FelsStreuen(pinsel, strich));

    /// <summary>Für Startoptionen: ein Stempel mit dem Startwert der Vorschau.</summary>
    public int WerkstattFelsStempeln(Felspinsel pinsel, Kartenort ort, double? drehung)
    {
        if (_entwurf is null) return 0;
        var n = Befehl(() => _entwurf.FelsStempeln(pinsel, ort, drehung, FelsStempelStartwert))?.Count ?? 0;
        if (n > 0) FelsStempelStartwert++;
        return n;
    }
}
