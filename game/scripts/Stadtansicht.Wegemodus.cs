using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Der Wege-Modus des Baumenüs: Nur hier lassen sich Wege anklicken (<see cref="WegAngeklickt"/> kommt sonst nie). Alle Wege leuchten in der Farbe ihres
/// Belags (Trampelpfad dunkelbraun, Fahrweg ocker, Kies grau, Pflaster hell, Baustelle orange); der Weg unter der Maus leuchtet heller
/// (<see cref="WegUnterDerMaus"/> meldet ihn der Hauptszene, die den Tooltip zeigt). Ändert sich ein Belag, wird das Leuchten neu gezeichnet.
/// </summary>
public partial class Stadtansicht
{
    private bool _wegeModus;
    private Node3D? _wegeLeuchten;
    private readonly Dictionary<int, List<(StandardMaterial3D Stoff, Color Farbe)>> _wegeStoffe = new();
    private bool _wegeVeraltet;
    private (int Weg, int Stueck)? _wegeHover;

    /// <summary>Der Weg (Nummer in <c>Karte.Wege</c>) und das 5-m-Stück unter der Maus – null, wenn keiner.</summary>
    public event Action<(int Weg, double Stelle)?>? WegUnterDerMaus;

    /// <summary>Die Farbe, in der ein Belag im Wege-Modus leuchtet (auch für die Legende der Bautafel).</summary>
    public static Color WegeFarbe(Belagstufe stufe, bool baustelle = false) => baustelle ? new Color(0.98f, 0.55f, 0.12f) : stufe switch
    {
        Belagstufe.Trampelpfad => new Color(0.55f, 0.33f, 0.16f),
        Belagstufe.Fahrweg => new Color(0.86f, 0.62f, 0.25f),
        Belagstufe.Kies => new Color(0.55f, 0.62f, 0.72f),
        _ => new Color(0.97f, 0.95f, 0.88f),
    };

    /// <summary>Wege-Modus an/aus: Die Wege leuchten und lassen sich anklicken (gesetzt von der Bautafel, <see cref="BauAnzeige"/>).</summary>
    public bool WegeModus
    {
        get => _wegeModus;
        set
        {
            if (_wegeModus == value) return;
            _wegeModus = value;
            WegeLeuchtenLoeschen();
            WegHoverMelden(null);
            if (value) WegeLeuchtenBauen();
            WegAuswahlMarkieren(-1, 0, 0);
        }
    }

    private void WegeLeuchtenLoeschen()
    {
        if (_wegeLeuchten is not null) KnotenEntfernen(_wegeLeuchten);
        _wegeLeuchten = null;
        _wegeStoffe.Clear();
    }

    private void WegeLeuchtenBauen()
    {
        if (_karte is not { } karte || _wirtschaft?.Wegebau is not { IstAktiv: true } bau) return;
        _wegeLeuchten = new Node3D { Name = "WegeLeuchten" };
        AddChild(_wegeLeuchten);
        for (var nummer = 0; nummer < karte.Wege.Count; nummer++)
        {
            var weg = karte.Wege[nummer];
            var linie = weg.Verlauf.Where(p => p is { Length: 2 }).Select(p => new Vector2((float)p[0], (float)p[1])).ToList();
            if (linie.Count < 2) continue;
            var teil = Verfeinert(linie, 1f);
            var stelle = new float[teil.Count];
            for (var i = 1; i < teil.Count; i++) stelle[i] = stelle[i - 1] + teil[i].DistanceTo(teil[i - 1]);
            var stoffe = new List<(StandardMaterial3D, Color)>();
            _wegeStoffe[nummer] = stoffe;

            // gleiche Stufen hintereinander zu einem Band zusammenfassen
            var stuecke = bau.Stuecke(nummer);
            for (var i = 0; i < stuecke.Count;)
            {
                var j = i + 1;
                while (j < stuecke.Count && stuecke[j].Stufe == stuecke[i].Stufe && stuecke[j].Baustelle == stuecke[i].Baustelle) j++;
                var (von, bis) = ((float)stuecke[i].Von, (float)stuecke[j - 1].Bis);
                var punkte = new List<Vector2>();
                for (var k = 0; k < teil.Count; k++)
                    if (stelle[k] >= von - 0.5f && stelle[k] <= bis + 0.5f) punkte.Add(teil[k]);
                if (punkte.Count >= 2)
                {
                    var farbe = WegeFarbe(stuecke[i].Stufe, stuecke[i].Baustelle);
                    var band = DurchsichtigesNetz(BandDreiecke(punkte, (float)weg.BreiteWirksam / 2 + 0.3f, 0.2f), farbe, 0.55f);
                    ImmerSichtbar((StandardMaterial3D)band.MaterialOverride);
                    _wegeLeuchten.AddChild(band);
                    stoffe.Add(((StandardMaterial3D)band.MaterialOverride, farbe));
                }
                i = j;
            }
        }
        WegeHoverFaerben();
    }

    /// <summary>Der Weg unter der Maus leuchtet heller und kräftiger, alle anderen gedämpft.</summary>
    private void WegeHoverFaerben()
    {
        foreach (var (nummer, stoffe) in _wegeStoffe)
        {
            var hell = _wegeHover?.Weg == nummer;
            foreach (var (stoff, farbe) in stoffe)
                stoff.AlbedoColor = hell ? new Color(farbe.Lightened(0.45f), 0.92f) : new Color(farbe, 0.55f);
        }
    }

    private void WegHoverMelden((int Weg, int Stueck)? neu)
    {
        if (neu == _wegeHover) return;
        var alterWeg = _wegeHover?.Weg;
        _wegeHover = neu;
        if (alterWeg != neu?.Weg) WegeHoverFaerben();
        WegUnterDerMaus?.Invoke(neu is { } n && _wirtschaft?.Wegebau is { } bau ? (n.Weg, (n.Stueck + 0.5) * bau.Daten!.Stueck) : null);
    }

    /// <summary>Jedes Bild (aus <see cref="_Process"/>): Leuchten nachziehen, wenn sich ein Belag geändert hat, und den Weg unter der Maus suchen.</summary>
    private void WegeModusBild()
    {
        if (!_wegeModus) return;
        if (_wegeVeraltet)
        {
            _wegeVeraltet = false;
            WegeLeuchtenLoeschen();
            WegeLeuchtenBauen();
        }
        (int, int)? treffer = null;
        if (_innen is null && GetViewport().GuiGetHoveredControl() is null && BodenPunktBei(GetViewport().GetMousePosition()) is { } boden
            && WegBeiPunkt(boden) is var (weg, stelle) && _wirtschaft?.Wegebau is { } bau)
            treffer = (weg, bau.StueckBei(weg, stelle));
        WegHoverMelden(treffer);
    }
}
