using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Felsstücke in der Kartenwerkstatt (Felspinsel F4) – ein Teil der <c>partial class Stadtansicht</c>. Der Kern führt die Stücke und den Pinsel
/// (<see cref="Kartenentwurf.FelsStrichBeginnen"/>, <see cref="Kartenentwurf.FelsStempeln"/>); hier steht, was die Werkstatt in der 3D-Welt daraus macht:
/// <list type="bullet">
/// <item><b>Werkzeug</b> <see cref="Werkstattwerkzeug.Fels"/> in vier Arten (<see cref="FelsArt"/>): <b>Streuen</b> mit gedrückter Maus (jede Mausbewegung
/// gibt der Kern weiter, die Stücke erscheinen sofort, Loslassen macht den Strich zu einem Schritt, Esc nimmt ihn zurück; mit Umschalt radiert der
/// Strich, P3), <b>Stempeln</b> mit einem Klick (ein Stück oder eine Gruppe; die Vorschau unter der Maus zeigt genau, was der Klick setzt; danach sind die
/// neuen Stücke gewählt und der Gizmo steht an ihnen – ein Klick auf einen Griff zieht, sonst stempelt er weiter, Fels P2), <b>Radieren</b> wie Streuen
/// (P3: mit einer Dichte dünnt er nur aus, mit „nur diese Formen“ nur die angehakten), <b>Neu verteilen</b> (P3) wie Streuen, würfelt aber die angehakten
/// Werte der Stücke unter dem Pinsel neu. Alle Werte des Pinsels stehen in der Tafel (Kartenwerkstatt.Felsen.cs); ein Strich ist ein Schritt.</item>
/// <item><b>Hang mitformen</b> (R5, Häkchen in der Tafel, Standard aus): Der Stempel hebt das Gelände hinter den Wänden an – ein Schritt mit den Stücken.</item>
/// <item><b>Vorschau</b>: Kreis des Pinsels; beim Stempeln dazu die Stücke als helle Geister (höchstens acht Netze, je Bild nur neu gesetzt).</item>
/// <item><b>Anzeige</b> der Stücke wie im Spiel (Stadtansicht.Felsen.cs, F5): MultiMeshes je Zelle und Form, Feinheit nach dem Zoom; dort auch Wählen,
/// Ziehen, Gizmo (Stadtansicht.Felsgizmo.cs), Rahmen, Kopieren und Schieben mit den Pfeiltasten (Stadtansicht.Felsauswahl.cs) und Löschen mit dem Werkzeug „Auswahl“.</item>
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

        /// <summary>„Neu verteilen“ (P3, wie „Reapply“ in Unreal): Der Strich würfelt die angehakten Werte der Stücke unter dem Pinsel neu.</summary>
        Verteilen,
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

    /// <summary>Boden mitmalen (F6): Fels unter dem Stück, Schuttkegel hangabwärts, Moossaum – im selben Schritt wie die Stücke.</summary>
    public bool FelsBoden { get; set; } = true;

    /// <summary>
    /// „Hang mitformen“ (R5, Vorgabe aus): Der Stempel hebt das Gelände hinter den großen Wänden an, damit sie im Hang stecken (Felshang.cs). Gilt nur für
    /// den Stempel und nur für die Wände des Wand-Satzes.
    /// </summary>
    public bool FelsHangFormen { get; set; }

    /// <summary>Wo der Felspinsel wirkt (Steigung, Boden); <see cref="Pinselfilter.Keiner"/>: überall.</summary>
    public Pinselfilter FelsFilter { get; set; } = Pinselfilter.Keiner;

    /// <summary>Startwert des nächsten Stempels: Vorschau und Klick würfeln gleich, nach jedem Stempel kommt ein neuer („neu würfeln“).</summary>
    public int FelsStempelStartwert { get; set; } = 1;

    // ── P3: alle Pinselwerte (Felspinsel.cs), Vorgaben wie vorher ─────────────

    /// <summary>Meter, die beim Streuen zusätzlich zwischen zwei Stücken frei bleiben.</summary>
    public double FelsAbstand { get; set; }

    /// <summary>± Anteil der Höhe um das Einsinken (Vorgabe ± 5 %).</summary>
    public double FelsEinsinkenStreuung { get; set; } = Felspinsel.EinsinkenStreuungVorgabe;

    /// <summary>Zufallsbereiche („Erweitert“ in der Tafel); null: wie ohne Bereich (Drehung zufällig bzw. hangabwärts ± 20°, Kippen und Rollen ± 8°).</summary>
    public Felsbereich? FelsDrehbereich { get; set; }
    public Felsbereich? FelsKippbereich { get; set; }
    public Felsbereich? FelsRollbereich { get; set; }
    public Felsbereich? FelsAchseX { get; set; }
    public Felsbereich? FelsAchseY { get; set; }
    public Felsbereich? FelsAchseZ { get; set; }
    public Felsbereich? FelsVersatzbereich { get; set; }

    /// <summary>Am Hang höchstens so viele Grad aus der Senkrechten neigen (null: ohne Grenze).</summary>
    public double? FelsNeigungBis { get; set; }

    /// <summary>Höhenfilter (Meter, Geländehöhe an der Mitte); null: überall.</summary>
    public Felsbereich? FelsHoehenbereich { get; set; }

    /// <summary>Radierer, Ausdünnen und Neu verteilen nehmen nur Stücke der angehakten Formen.</summary>
    public bool FelsNurFormen { get; set; }

    /// <summary>P3b: Streuen setzt nichts in Gebäude (mit Vorplatz) und auf Wege; Vorgabe an. Stempel und Radierer fragen nicht.</summary>
    public bool FelsAussparen { get; set; } = true;

    /// <summary>P3b: dazu auch Bauzonen aussparen (nur mit <see cref="FelsAussparen"/>; Vorgabe aus).</summary>
    public bool FelsAussparenBauzonen { get; set; }

    /// <summary>Radierer: bis zu dieser Dichte ausdünnen (Anteil der Kreisfläche); 0: alles weg wie bisher.</summary>
    public double FelsAusduennen { get; set; }

    /// <summary>Was „Neu verteilen“ (Strich und Knopf an der Auswahl) neu würfelt.</summary>
    public Felswurf FelsVerteilenWurf { get; set; } = Felswurf.Groesse | Felswurf.Drehung;

    /// <summary>
    /// Der Pinsel mit allen eingestellten Werten, unabhängig von der Art (Streuen, Stempeln, Neu verteilen nutzen ihn; der Knopf „Neu verteilen“ an
    /// der Auswahl auch, wenn gerade der Radierer eingestellt ist).
    /// </summary>
    public Felspinsel FelsPinselWerte => new(FelsFormen.ToArray(), FelsGruppe, FelsGestein, FelsRadius, FelsDichte, FelsGroesse, FelsStreuung, FelsEinsinken,
        FelsNeigung, FelsZumHang, FelsFilter, Boden: FelsBoden, AchseX: FelsAchseX, AchseY: FelsAchseY, AchseZ: FelsAchseZ, Drehbereich: FelsDrehbereich,
        Kippbereich: FelsKippbereich, Rollbereich: FelsRollbereich, Versatzbereich: FelsVersatzbereich, NeigungBis: FelsNeigungBis, HangFormen: FelsHangFormen,
        Abstand: FelsAbstand, EinsinkenStreuung: FelsEinsinkenStreuung, Hoehenbereich: FelsHoehenbereich, NurFormen: FelsNurFormen, Aussparen: FelsAussparen, AussparenBauzonen: FelsAussparenBauzonen);

    /// <summary>Der Radierer mit Radius, Filter, Höhe und Formen (Ausdünnen, wenn eine Dichte eingestellt ist).</summary>
    public Felspinsel FelsRadierer => Felspinsel.Radiergummi(FelsRadius, FelsFilter) with
    {
        Formen = FelsFormen.ToArray(), NurFormen = FelsNurFormen, Hoehenbereich = FelsHoehenbereich, Ausduennen = FelsAusduennen > 0, Dichte = FelsAusduennen,
    };

    /// <summary>Der Pinsel mit den gewählten Werten.</summary>
    public Felspinsel FelsPinsel => FelsModus switch
    {
        FelsArt.Radieren => FelsRadierer,
        FelsArt.Verteilen => FelsPinselWerte with { Gruppe = null, Verteilen = FelsVerteilenWurf == Felswurf.Keins ? null : FelsVerteilenWurf },
        _ => FelsPinselWerte,
    };

    /// <summary>Die Farbe einer Gesteinsart (hell) für Symbol und Kreis.</summary>
    public static Color FelsFarbe(string? gestein) => Gesteinsarten.Nach(gestein ?? Felsformen.GrundgesteinVorgabe).Hell;

    // ═════════════════════════════════════════════════════════════════════════
    //  Strich und Stempel
    // ═════════════════════════════════════════════════════════════════════════

    private bool _wsFelsStrich;

    /// <summary>Radiert der laufende Strich, weil beim Drücken Umschalt gehalten war (P3)?</summary>
    private bool _wsFelsRadiert;

    /// <summary>Linke Maustaste mit dem Felswerkzeug: Stempeln setzt gleich, Streuen und Radieren beginnen einen Strich.</summary>
    private void FelsDruecken(Vector2 maus)
    {
        if (_entwurf is null) return;
        if (FelsModus == FelsArt.Stempeln && FelsGizmoDruecken(maus)) return; // P2: der Gizmo an den gerade gestempelten Stücken geht vor (Stadtansicht.Felsgizmo.cs)
        if (BodenpunktBei(maus) is not { } boden) return;
        if (FelsModus == FelsArt.Stempeln)
        {
            FelsStempelnBei(new Kartenort(boden.X, boden.Y));
            return;
        }
        _wsFelsRadiert = FelsUmschaltRadiert;
        FelsStrichStarten(_wsFelsRadiert ? FelsRadierer with { Ausduennen = false } : FelsPinsel);
        FelsStrichPunkt(boden);
    }

    private int _felsZufallZaehler;

    /// <summary>
    /// Beginnt den Strich im Kern. Ausdünnen und Neu verteilen bekommen je Strich einen neuen Startwert (sonst wählte ein zweiter Strich über dieselbe
    /// Stelle genau dieselben Stücke bzw. Werte); Streuen und Radieren wie bisher den aus dem ersten Punkt.
    /// </summary>
    private void FelsStrichStarten(Felspinsel pinsel)
    {
        if (_entwurf is null) return;
        int? startwert = pinsel.Ausduennen || pinsel.Verteilen is not null ? ++_felsZufallZaehler * 7919 : null;
        Befehl(() => _entwurf.FelsStrichBeginnen(pinsel, startwert));
        _wsFelsStrich = true;
    }

    /// <summary>
    /// „Neu verteilen“ an der Auswahl (Knopf in der Tafel, wie „Reapply“ in Unreal): würfelt die angehakten Werte (<see cref="FelsVerteilenWurf"/>)
    /// der gewählten Stücke mit den Werten des Pinsels neu – ein Schritt. Jeder Druck würfelt neu. Gibt zurück, wie viele Stücke gewählt waren.
    /// </summary>
    public int FelsAuswahlVerteilen(Felswurf? wurf = null)
    {
        var w = wurf ?? FelsVerteilenWurf;
        if (_entwurf is null || FelsAuswahl.Count == 0 || w == Felswurf.Keins) return 0;
        var liste = FelsAuswahl.ToList();
        Befehl(() => _entwurf.FelsNeuWuerfeln(liste, FelsPinselWerte with { Radierer = false }, w, ++_felsZufallZaehler * 7919));
        return liste.Count;
    }

    /// <summary>Umschalt beim Streuen radiert (P3, wie in Unreal): mit den Formen, dem Filter und der Höhe der Tafel, ohne Ausdünnen.</summary>
    private bool FelsUmschaltRadiert => FelsModus == FelsArt.Streuen && Input.IsKeyPressed(Key.Shift);

    private void FelsStempelnBei(Kartenort ort)
    {
        if (_entwurf is null) return;
        var neue = Befehl(() => _entwurf.FelsStempeln(FelsPinsel, ort, startwert: FelsStempelStartwert));
        if (neue is { Count: > 0 })
        {
            FelsStempelStartwert++;
            FelsMehrfachWaehlen(neue, dazu: false); // P2: gleich gewählt, der Gizmo steht an ihnen (Stadtansicht.Felsauswahl.cs)
        }
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
        // rot radiert (auch Umschalt beim Streuen), orange dünnt aus, gold verteilt neu, sonst die Farbe des Gesteins
        var radiert = FelsModus == FelsArt.Streuen && (_wsFelsStrich ? _wsFelsRadiert : FelsUmschaltRadiert);
        var farbe = FelsModus == FelsArt.Radieren && FelsAusduennen > 0 ? new Color(0.95f, 0.6f, 0.25f)
            : FelsModus == FelsArt.Radieren || radiert ? new Color(0.95f, 0.35f, 0.3f)
            : FelsModus == FelsArt.Verteilen ? new Color(0.95f, 0.82f, 0.35f)
            : FelsFarbe(FelsGestein ?? _entwurf.Grundgestein.Wirksam).Lightened(0.2f);
        if (FelsModus == FelsArt.Stempeln)
        {
            if (_gizmoZug is not null || _gizmoHover is not null)
            {
                FelsGeisterAus(); // über einem Griff des Gizmos (oder beim Ziehen daran) stempelt ein Klick nicht – also keine Vorschau
                return;
            }
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
            mi.Transform = FelsLage(new Felsbild(l.Form, "", l.X, l.Z, l.Drehung, l.Massstab, l.Neigung, l.Einsinken, l.KippenX, l.KippenZ,
                l.AchseX, l.AchseY, l.AchseZ, l.Versatz));
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

    /// <summary>Für Startoptionen: ein Stempel mit dem Startwert der Vorschau; die neuen Stücke sind danach gewählt.</summary>
    public int WerkstattFelsStempeln(Felspinsel pinsel, Kartenort ort, double? drehung)
    {
        if (_entwurf is null) return 0;
        var neue = Befehl(() => _entwurf.FelsStempeln(pinsel, ort, drehung, FelsStempelStartwert));
        if (neue is not { Count: > 0 }) return 0;
        FelsStempelStartwert++;
        FelsMehrfachWaehlen(neue, dazu: false); // wie der Klick (P2): die neuen Stücke sind gewählt
        return neue.Count;
    }
}
