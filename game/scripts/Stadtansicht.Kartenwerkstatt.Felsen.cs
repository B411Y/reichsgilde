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
/// <item><b>Anzeige</b> der Stücke wie im Spiel (Stadtansicht.Felsen.cs, F5): MultiMeshes je Zelle und Form, Feinheit nach dem Zoom; dort auch Wählen,
/// Ziehen, Drehen (Q/E) und Löschen einzelner Stücke mit dem Werkzeug „Auswahl“.</item>
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
    /// <summary>Dichte beim Streuen: je eine für Einzelstücke (0,2) und für Gruppen (0,1 – Gruppen sind groß und stehen sonst zu dicht).</summary>
    public double FelsDichte
    {
        get => FelsGruppe is null ? _felsDichteEinzeln : _felsDichteGruppe;
        set
        {
            if (FelsGruppe is null) _felsDichteEinzeln = value;
            else _felsDichteGruppe = value;
        }
    }

    private double _felsDichteEinzeln = 0.2, _felsDichteGruppe = 0.1;
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
        if (_entwurf is not { FelsStrichLaeuft: true }) // Strg+Z bei gedrückter Maus hat den Strich beendet
        {
            _wsFelsStrich = false;
            return;
        }
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
            mi.Mesh = FelsNetz(l.Form, 1);
            mi.Transform = FelsLage(new Felsbild(l.Form, "", l.X, l.Z, l.Drehung, l.Massstab, l.Neigung, l.Einsinken, l.KippenX, l.KippenZ));
            mi.Visible = mi.Mesh is not null;
        }
    }

    private void FelsGeisterAus()
    {
        if (_wsFelsGeister is not null) _wsFelsGeister.Visible = false;
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
