using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Felspinsel P6: „Fläche füllen“ in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c>. Der Kern rechnet (Kartenentwurf.FelsFuellen,
/// FelsFuellenVorschau, FelsFuellenSchaetzen; Regeln in Bauen/Felsfuellung.cs); hier steht die Bedienung:
/// <list type="bullet">
/// <item>Klicks setzen die Ecken des Umrisses; ein Klick auf die erste Ecke oder Enter schließt ihn (Enter nach nur einer Ecke: ein Kreis mit dem Radius
/// des Pinsels). Rücktaste nimmt die letzte Ecke weg, Esc den ganzen Umriss.</item>
/// <item>Ist der Umriss zu, rechnet der Kern die Vorschau (genau die Stücke des Füllens) – je Stück ein Ring – und die Tafel zeigt die Zahl. Über
/// <see cref="Felsfuellung.Obergrenze"/> Stücken warnt sie und füllt erst beim zweiten Druck (Bestätigung, kein Verbot).</item>
/// <item>„Füllen“ ist ein Schritt (Strg+Z nimmt alles zurück); danach ist der Umriss leer.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die gewählte Füllung: <see cref="Felsfuellung.EinfachKennung"/> („im Umriss“ mit dem Pinsel der Tafel) oder eine Voreinstellung.</summary>
    public string FelsFuellKennung { get; set; } = Felsfuellung.EinfachKennung;

    /// <summary>„Im Umriss“: Anteil der Fläche, den das Rauschen frei lässt (0 … 0,9).</summary>
    public double FelsFuellLuecken { get; set; }

    /// <summary>„Im Umriss“: nur auf große Stücke setzen.</summary>
    public bool FelsFuellAufFels { get; set; }

    /// <summary>Startwert der nächsten Füllung (Vorschau und Füllen würfeln gleich; „Neu würfeln“ zählt hoch).</summary>
    public int FelsFuellStartwert { get; set; } = 1;

    /// <summary>Die Ecken des Umrisses, wie geklickt.</summary>
    public List<Kartenort> FelsUmriss { get; } = new();

    /// <summary>Ist der Umriss geschlossen (die Vorschau steht)?</summary>
    public bool FelsUmrissZu { get; private set; }

    /// <summary>Die Fläche des geschlossenen Umrisses (null: offen).</summary>
    public Felsflaeche? FelsFuellFlaeche { get; private set; }

    /// <summary>Die Vorschau (genau die Stücke des Füllens) und die Schätzung, beide zum geschlossenen Umriss.</summary>
    public IReadOnlyList<Felsfuellstueck> FelsFuellVorschau { get; private set; } = Array.Empty<Felsfuellstueck>();
    public int FelsFuellSchaetzung { get; private set; }

    /// <summary>Über der Obergrenze: der erste Druck auf „Füllen“ ist geschehen, der zweite füllt.</summary>
    public bool FelsFuellBestaetigt { get; set; }

    /// <summary>Umriss, Vorschau oder Zahl haben sich geändert (die Tafel baut sich neu).</summary>
    public event Action? FelsFuellungGeaendert;

    /// <summary>Ist das Werkzeug „Fläche füllen“ eingestellt (Enter, Rücktaste und Esc gehören dann dem Umriss)?</summary>
    private bool FelsUmrissAktiv => Kartenwerkzeug == Werkstattwerkzeug.Fels && FelsModus == FelsArt.Fuellen;

    /// <summary>Die Füllung mit den Werten der Tafel.</summary>
    public Felsfuellung FelsFuellungWirksam =>
        Felsfuellung.Finde(FelsFuellKennung) is { } v ? v.MitPinsel(FelsPinselWerte)
            : Felsfuellung.Einfach(FelsPinselWerte with { HangFormen = false }, FelsFuellLuecken, aufFels: FelsFuellAufFels);

    /// <summary>Ein Klick mit „Fläche füllen“: eine Ecke dazu, auf der ersten Ecke schließen; bei geschlossenem Umriss beginnt ein neuer.</summary>
    private void FelsUmrissKlick(Vector2 boden)
    {
        if (FelsUmrissZu) FelsUmrissLeeren(melden: false);
        var p = new Kartenort(boden.X, boden.Y);
        if (FelsUmriss.Count >= 3 && FelsUmriss[0].Abstand(p) <= Mathf.Max(1.5f, MeterJePixel * 10))
        {
            FelsUmrissSchliessen();
            return;
        }
        FelsUmriss.Add(p);
        FelsFuellungGeaendert?.Invoke();
    }

    /// <summary>Enter: schließt den Umriss (eine Ecke: Kreis mit dem Radius des Pinsels) und rechnet die Vorschau. False: zu wenige Ecken.</summary>
    public bool FelsUmrissSchliessen()
    {
        if (_entwurf is null || FelsUmriss.Count is 0 or 2) return false;
        FelsFuellFlaeche = FelsUmriss.Count == 1
            ? Felsflaeche.Kreis(FelsUmriss[0], Math.Clamp(FelsRadius, Felspinsel.MinRadius, Felspinsel.MaxRadius))
            : new Felsflaeche(FelsUmriss);
        FelsUmrissZu = true;
        FelsFuellVorschauRechnen();
        return true;
    }

    /// <summary>Rechnet Schätzung und Vorschau zum geschlossenen Umriss neu (nach jeder Änderung der Tafel und „Neu würfeln“).</summary>
    public void FelsFuellVorschauRechnen()
    {
        FelsFuellBestaetigt = false;
        _felsFuellRinge = null;
        if (_entwurf is null || FelsFuellFlaeche is not { } f)
        {
            (FelsFuellVorschau, FelsFuellSchaetzung) = (Array.Empty<Felsfuellstueck>(), 0);
        }
        else
        {
            var fuellung = FelsFuellungWirksam;
            FelsFuellSchaetzung = _entwurf.FelsFuellenSchaetzen(fuellung, f);
            FelsFuellVorschau = _entwurf.FelsFuellenVorschau(fuellung, f, FelsFuellStartwert);
        }
        FelsFuellungGeaendert?.Invoke();
    }

    /// <summary>Rücktaste: die letzte Ecke weg (ein geschlossener Umriss öffnet sich wieder).</summary>
    private bool FelsUmrissZurueck()
    {
        if (FelsUmriss.Count == 0) return false;
        if (FelsUmrissZu) (FelsUmrissZu, FelsFuellFlaeche, FelsFuellVorschau, _felsFuellRinge) = (false, null, Array.Empty<Felsfuellstueck>(), null);
        else FelsUmriss.RemoveAt(FelsUmriss.Count - 1);
        FelsFuellungGeaendert?.Invoke();
        return true;
    }

    /// <summary>Esc: der ganze Umriss weg. False: es gab keinen.</summary>
    private bool FelsUmrissLeeren(bool melden = true)
    {
        if (FelsUmriss.Count == 0) return false;
        FelsUmriss.Clear();
        (FelsUmrissZu, FelsFuellFlaeche, FelsFuellVorschau, FelsFuellSchaetzung, _felsFuellRinge, FelsFuellBestaetigt) = (false, null, Array.Empty<Felsfuellstueck>(), 0, null, false);
        if (melden) FelsFuellungGeaendert?.Invoke();
        return true;
    }

    /// <summary>
    /// „Füllen“ (Knopf der Tafel): Über der Obergrenze zuerst nur die Bestätigung (gibt −1 zurück), beim zweiten Druck füllt der Kern – ein Schritt.
    /// Gibt die Zahl der neuen Stücke zurück; danach ist der Umriss leer und der Startwert neu.
    /// </summary>
    public int FelsFuellenAusfuehren()
    {
        if (_entwurf is null || FelsFuellFlaeche is not { } f) return 0;
        if (FelsFuellVorschau.Count > Felsfuellung.Obergrenze && !FelsFuellBestaetigt)
        {
            FelsFuellBestaetigt = true;
            FelsFuellungGeaendert?.Invoke();
            return -1;
        }
        var fuellung = FelsFuellungWirksam;
        var neue = Befehl(() => _entwurf.FelsFuellen(fuellung, f, FelsFuellStartwert));
        var n = neue?.Count ?? 0;
        if (n > 0) FelsFuellStartwert++;
        WerkstattMeldung?.Invoke(n > 0 ? $"Fläche gefüllt ({fuellung.Name}): {n} Felsstücke – Strg+Z nimmt alles zurück." : "Die Füllung setzt hier nichts (Regeln, Filter oder Aussparen).", n == 0);
        FelsUmrissLeeren();
        return n;
    }

    // ── Vorschau in der 3D-Welt ──────────────────────────────────────────────

    /// <summary>Die Ringe der Vorschau (alle Stücke in einem Netz; neu gebaut, wenn sich die Vorschau oder der Zoom ändert).</summary>
    private Vector3[]? _felsFuellRinge;
    private float _felsFuellRingeZoom;

    /// <summary>Jedes Bild mit „Fläche füllen“: der Umriss (offen bis zur Maus), die Ecken und bei geschlossenem Umriss je Stück ein Ring.</summary>
    private void FelsUmrissZeigen(Vector2 maus, Color farbe)
    {
        var breite = Mathf.Max(0.12f, MeterJePixel * 1.2f);
        var linie = FelsUmrissZu && FelsFuellFlaeche is { } f
            ? f.Umriss.Append(f.Umriss[0]).Select(p => new Vector2((float)p.X, (float)p.Z)).ToList()
            : FelsUmriss.Select(p => new Vector2((float)p.X, (float)p.Z)).Append(maus).ToList();
        if (linie.Count >= 2) VorschauDreiecke(BandDreiecke(linie, breite, 0.2f), new Color(farbe, 0.95f));
        VorschauPunkte(FelsUmriss.Select(p => new Vector2((float)p.X, (float)p.Z)).Append(maus).ToArray(), new Color(farbe, 0.95f));
        if (!FelsUmrissZu || FelsFuellVorschau.Count == 0) return;
        var zoom = Mathf.Max(0.06f, MeterJePixel * 0.8f);
        if (_felsFuellRinge is null || !Mathf.IsEqualApprox(zoom, _felsFuellRingeZoom))
        {
            var alle = new List<Vector3>();
            foreach (var s in FelsFuellVorschau)
                alle.AddRange(BandDreiecke(Kreis(new Vector2((float)s.Lage.X, (float)s.Lage.Z), Mathf.Max(0.3f, (float)s.Lage.Radius)), zoom, 0.2f));
            (_felsFuellRinge, _felsFuellRingeZoom) = (alle.ToArray(), zoom);
        }
        // über der Obergrenze rot, sonst in der Farbe des Gesteins
        VorschauDreiecke(_felsFuellRinge, FelsFuellVorschau.Count > Felsfuellung.Obergrenze ? new Color(0.95f, 0.35f, 0.3f, 0.85f) : new Color(farbe, 0.8f));
    }

    // ── Startoption ──────────────────────────────────────────────────────────

    /// <summary>
    /// Für Startoptionen: füllt einen Umriss (eine Ecke: Kreis mit <paramref name="radius"/>) mit der eingestellten Füllung; mit <paramref name="nurVorschau"/>
    /// bleibt der Umriss geschlossen stehen (Vorschau und Tafel), sonst wird gefüllt – über der Obergrenze nur mit <paramref name="bestaetigen"/>.
    /// Gibt die Zahl der Stücke der Vorschau bzw. der neuen zurück.
    /// </summary>
    public int WerkstattFelsFuellen(IReadOnlyList<Kartenort> umriss, double? radius, bool nurVorschau, bool bestaetigen)
    {
        FelsUmrissLeeren(melden: false);
        FelsUmriss.AddRange(umriss);
        if (radius is { } r) FelsRadius = r;
        if (!FelsUmrissSchliessen()) return 0;
        if (nurVorschau) return FelsFuellVorschau.Count;
        var n = FelsFuellenAusfuehren();
        if (n == -1 && bestaetigen) n = FelsFuellenAusfuehren();
        return n;
    }
}
