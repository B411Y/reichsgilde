using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Wasser in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs; das Wasser selbst und die Brücken
/// zeichnet Stadtansicht.Wasser.cs, genau wie im Spiel). Der Kern führt Flüsse, Bäche, Seen, Teiche und Fährstellen (Kartenentwurf.Wasser.cs) und rechnet Spiegel,
/// Bett, Brücken und Furten; hier steht, was die Werkstatt daraus macht:
/// <list type="bullet">
/// <item><b>Werkzeug „Fluss“</b> (<see cref="Werkstattwerkzeug.Fluss"/>, Art Fluss oder Bach): Punkte klicken wie bei einem Weg, <b>in Fließrichtung</b> – die Pfeile in der Vorschau zeigen sie.
/// Doppelklick oder Enter legt ihn an. <b>Werkzeug „See“</b> (<see cref="Werkstattwerkzeug.See"/>, See oder Teich): Ecken klicken wie bei jeder Fläche.
/// <b>Werkzeug „Fähre“</b> (<see cref="Werkstattwerkzeug.Faehre"/>): zwei Klicks, die beiden Anleger – sie rasten an Wegen ein.</item>
/// <item><b>Markierungen</b> (<see cref="FlussMarkieren"/>, <see cref="FaehreMarkieren"/>): Jeder Fluss trägt Pfeile in Fließrichtung (kräftig, wenn er gewählt ist), gewählt gold mit seinem Namen,
/// mit Fehler rot; eine Fährstelle ist eine orange Linie zwischen ihren Anlegern. Ein See ist eine Fläche wie die anderen (blauer Rand).</item>
/// <item><b>Folgen</b>: Jede Änderung an Gewässern, Wegen und Gelände baut Wasser, Brücken und Fähren neu (<see cref="WasserNachziehen"/>); ein neuer Fluss gräbt sein Bett ins Gelände, also werden die
/// Kacheln unter ihm und alles darauf neu gebaut (wie bei Pinselstrichen); die Wege, die er quert, bekommen ihre Brücken.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Art, die das Werkzeug „Fluss“ zeichnet: „fluss“ oder „bach“.</summary>
    public string WerkstattFlussart { get; set; } = "fluss";

    /// <summary>Die Art, die das Werkzeug „See“ zeichnet: „see“ oder „teich“.</summary>
    public string WerkstattSeeart { get; set; } = "see";

    /// <summary>Die Breite neuer Flüsse in Metern (0: nach der Art, 12 m Fluss, 3 m Bach).</summary>
    public double WerkstattFlussbreite { get; set; }

    /// <summary>Der erste Klick der Fähre (der erste Anleger) – null: noch keiner.</summary>
    private Vector2? _wsFaehreStart;

    private Node3D? _wsGewaesserMarken;
    private readonly Dictionary<EntwurfTeil, Node3D> _wsGewaesser = new();

    /// <summary>Wo jedes Gewässer zuletzt lag (mit Bett und Ufer): Ändert es sich, werden die Kacheln am alten und am neuen Ort neu gebaut.</summary>
    private readonly Dictionary<EntwurfTeil, Rect2> _wsWasserBereiche = new();

    /// <summary>Am Ende des Bildes: Wasser, Brücken und Fähren neu (<see cref="_wsWasserNeu"/>); dazu alle Wege (<see cref="_wsWegeNeu"/>), wenn sich Gewässer geändert haben.</summary>
    private bool _wsWasserNeu, _wsWegeNeu;

    private static readonly Color Wasserblau = new(0.30f, 0.62f, 0.95f), Faehrorange = new(0.98f, 0.62f, 0.18f);

    private bool GewaesserLeer => GewaesserForm is not { IstLeer: false };

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau und Änderungen
    // ═════════════════════════════════════════════════════════════════════════

    private void WasserAufbauen()
    {
        _wsGewaesserMarken = new Node3D { Name = "GewaesserMarken" };
        AddChild(_wsGewaesserMarken);
        GewaesserBauen(); // das Wasser selbst (Stadtansicht.Wasser.cs)
        foreach (var g in _entwurf!.Gewaesser)
        {
            if (g is EntwurfFluss f) FlussMarkieren(f, false);
            _wsWasserBereiche[g] = GewaesserBereich(g);
        }
        foreach (var fa in _entwurf.Faehren) FaehreMarkieren(fa, false);
    }

    /// <summary>
    /// Das Rechteck, in dem ein Gewässer das Gelände ändert: der Fluss samt Ufer und der Glättung des Spiegels (er folgt dem Gelände ±8 m), ein See samt Ufer. Weit genug,
    /// dass nichts in der Nähe veraltet bleibt.
    /// </summary>
    private static Rect2 GewaesserBereich(EntwurfTeil g)
    {
        switch (g)
        {
            case EntwurfFluss f when f.Punkte.Count > 0:
            {
                var breit = Enumerable.Range(0, f.Punkte.Count).Max(f.BreiteAn);
                return Rahmen(f.Verlauf).Grow((float)(breit / 2 + Wasserform.Uferbreite(breit) + Wasserform.Glaettung + 4));
            }
            case EntwurfSee s when s.Ecken.Count > 0:
                return Rahmen(s.Ecken).Grow((float)(Wasserform.SeeUfer + Wasserform.Glaettung + 4));
        }
        return new Rect2();
    }

    /// <summary>Ein Gewässer ist neu, geändert oder gelöscht: das Gelände unter seinem alten und neuen Bett neu, dazu Wasser und Wege.</summary>
    private void GewaesserGeaendert(EntwurfTeil g, bool weg)
    {
        if (_wsWasserBereiche.Remove(g, out var alt)) GelaendeVormerken(RechteckVon(alt));
        if (!weg)
        {
            var neu = GewaesserBereich(g);
            _wsWasserBereiche[g] = neu;
            GelaendeVormerken(RechteckVon(neu));
        }
        _wsWasserNeu = _wsWegeNeu = true;
    }

    /// <summary>Am Ende des Bildes (aus <see cref="Nachziehen"/>): Wasser, Brücken, Fähren neu; die Wege neu, wo sich Gewässer geändert haben (Brücken kommen und gehen).</summary>
    private void WasserNachziehen()
    {
        _wsWasserNeu = false;
        if (_entwurf is null) return;
        GewaesserBauen();
        foreach (var g in _entwurf.Gewaesser)
            if (g is EntwurfFluss f) FlussMarkieren(f, false);
        foreach (var fa in _entwurf.Faehren) FaehreMarkieren(fa, false);
        if (_wsWegeNeu)
        {
            _wsWegeNeu = false;
            foreach (var w in _entwurf.Wege.ToList()) WegBauen(w, false);
            _wsNetzNeu = true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Markierungen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Pfeile entlang einer Linie (x, z): alle <paramref name="abstand"/> Meter einer, die Spitze zeigt in Laufrichtung; Dreiecke, Vorderseite oben, auf der Höhe <paramref name="hoehe"/>.</summary>
    private static Vector3[] PfeilDreiecke(IReadOnlyList<Vector2> linie, float abstand, float groesse, Func<Vector2, float> hoehe)
    {
        var ecken = new List<Vector3>();
        if (linie.Count < 2) return ecken.ToArray();
        var laenge = 0f;
        var naechster = abstand / 2;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            var stueck = a.DistanceTo(b);
            if (stueck < 1e-4f) continue;
            var d = (b - a) / stueck;
            while (naechster <= laenge + stueck)
            {
                var p = a + d * (naechster - laenge);
                var n = new Vector2(-d.Y, d.X);
                var (spitze, links, rechts) = (p + d * groesse, p - d * groesse * 0.6f + n * groesse * 0.75f, p - d * groesse * 0.6f - n * groesse * 0.75f);
                DreieckFlach(ecken, spitze, links, rechts, hoehe(p));
                naechster += abstand;
            }
            laenge += stueck;
        }
        return ecken.ToArray();
    }

    /// <summary>Der Spiegel eines Flusses an einem Ort seines Verlaufs – ohne Wasser (noch nicht gerechnet) die Höhe des Bodens plus 0,4 m.</summary>
    private float SpiegelBei(Vector2 p) => GewaesserForm?.Spiegel(p.X, p.Y) is { } s ? (float)s : BodenHoehe(p.X, p.Y) + 0.4f;

    /// <summary>Baut die Markierung eines Flusses neu: Pfeile in Fließrichtung, gewählt ein goldenes Band mit dem Namen, mit Fehler ein rotes.</summary>
    private void FlussMarkieren(EntwurfFluss f, bool weg)
    {
        if (_wsGewaesser.Remove(f, out var alt)) KnotenEntfernen(alt);
        if (weg || _wsGewaesserMarken is null || f.Punkte.Count < 2) return;
        var knoten = new Node3D { Name = "Fluss" };
        _wsGewaesserMarken.AddChild(knoten);
        _wsGewaesser[f] = knoten;
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, f);
        var fehler = Fehlerhaft(f);
        var verlauf = f.Verlauf.Select(V2).ToList();
        var farbe = fehler ? Fehlerrot : gewaehlt ? Auswahlgold : Wasserblau.Lightened(0.55f);
        if (gewaehlt || fehler)
        {
            var band = DurchsichtigesNetz(BandDreiecke(verlauf, (float)f.BreiteWirksam / 2 + 0.6f, 0.5f), farbe, gewaehlt ? 0.35f : 0.3f);
            ImmerSichtbar((StandardMaterial3D)band.MaterialOverride);
            knoten.AddChild(band);
        }
        var pfeile = DurchsichtigesNetz(PfeilDreiecke(verlauf, gewaehlt ? 14 : 28, gewaehlt ? 2.4f : 1.8f, p => SpiegelBei(p) + 0.3f), farbe, gewaehlt ? 0.95f : 0.6f);
        ImmerSichtbar((StandardMaterial3D)pfeile.MaterialOverride);
        knoten.AddChild(pfeile);
        if (gewaehlt)
        {
            var mitte = verlauf[verlauf.Count / 2];
            var name = Beschriftung($"{f.Bezeichnung} · {Kartenentwurf.GewaesserName(f.Art)}", new Vector3(mitte.X, SpiegelBei(mitte) + 4, mitte.Y), 36, knoten);
            name.Modulate = farbe.Lightened(0.4f);
        }
    }

    /// <summary>Baut die Markierung einer Fährstelle neu: eine orange Linie zwischen den Anlegern (gewählt gold, mit Fehler rot), an jedem Anleger ein Quadrat.</summary>
    private void FaehreMarkieren(EntwurfFaehre fa, bool weg)
    {
        if (_wsGewaesser.Remove(fa, out var alt)) KnotenEntfernen(alt);
        if (weg || _wsGewaesserMarken is null || !fa.IstFaehre) return;
        var knoten = new Node3D { Name = "Faehre" };
        _wsGewaesserMarken.AddChild(knoten);
        _wsGewaesser[fa] = knoten;
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, fa);
        var farbe = Fehlerhaft(fa) ? Fehlerrot : gewaehlt ? Auswahlgold : Faehrorange;
        var (a, b) = (V2(fa.Von), V2(fa.Bis));
        var band = DurchsichtigesNetz(BandDreiecke(new List<Vector2> { a, b }, gewaehlt ? 0.5f : 0.35f, 0.4f), farbe, gewaehlt ? 0.95f : 0.7f);
        ImmerSichtbar((StandardMaterial3D)band.MaterialOverride);
        knoten.AddChild(band);
        var ecken = new List<Vector3>();
        foreach (var p in new[] { a, b })
        {
            Dreieck(ecken, p + new Vector2(-1.2f, -1.2f), p + new Vector2(1.2f, -1.2f), p + new Vector2(1.2f, 1.2f), 0.45f);
            Dreieck(ecken, p + new Vector2(-1.2f, -1.2f), p + new Vector2(1.2f, 1.2f), p + new Vector2(-1.2f, 1.2f), 0.45f);
        }
        var anleger = DurchsichtigesNetz(ecken.ToArray(), farbe, 0.95f);
        ImmerSichtbar((StandardMaterial3D)anleger.MaterialOverride);
        knoten.AddChild(anleger);
        if (gewaehlt)
        {
            var mitte = (a + b) / 2;
            var name = Beschriftung(fa.Bezeichnung + " · Fährstelle", new Vector3(mitte.X, SpiegelBei(mitte) + 4, mitte.Y), 36, knoten);
            name.Modulate = farbe.Lightened(0.4f);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Finden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Fluss unter dem Punkt (der nächste, wenn mehrere): im Wasser oder höchstens <paramref name="toleranz"/> daneben.</summary>
    private EntwurfFluss? FlussBei(Vector2 p, float toleranz)
    {
        EntwurfFluss? beste = null;
        var abstand = float.MaxValue;
        foreach (var f in _entwurf!.Fluesse)
        {
            if (f.Punkte.Count < 2) continue;
            var halb = (float)Enumerable.Range(0, f.Punkte.Count).Max(f.BreiteAn) / 2;
            var d = (float)f.NaechsteStelle(new Kartenort(p.X, p.Y)).Abstand - halb;
            if (d <= toleranz && d < abstand) (beste, abstand) = (f, d);
        }
        return beste;
    }

    /// <summary>Liegt der Punkt höchstens <paramref name="toleranz"/> Meter neben der Mittellinie des Flusses (hier fügt ein Klick einen Stützpunkt ein)?</summary>
    private static bool AufFluss(EntwurfFluss f, Vector2 p, float toleranz) => f.Punkte.Count >= 2 && f.NaechsteStelle(new Kartenort(p.X, p.Y)).Abstand <= toleranz;

    /// <summary>Die Fährstelle unter dem Punkt: höchstens <paramref name="toleranz"/> neben der Linie zwischen den Anlegern.</summary>
    private EntwurfFaehre? FaehreBei(Vector2 p, float toleranz) => _entwurf!.Faehren
        .Where(fa => fa.IstFaehre && p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, V2(fa.Von), V2(fa.Bis))) <= toleranz)
        .OrderBy(fa => p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, V2(fa.Von), V2(fa.Bis)))).FirstOrDefault();

    /// <summary>Wählt einen Stützpunkt des gewählten Flusses (oder einen Anleger der gewählten Fähre) als Griff – die Tafel rechts stellt Breite und Tiefe dort ein.</summary>
    public void WerkstattGriffWaehlen(int index)
    {
        if (WerkstattGewaehlt is not (EntwurfFluss or EntwurfFaehre)) return;
        _wsGriff = index < 0 ? null : new Griff(-1, index);
        GriffeBauen();
    }

    /// <summary>Welcher Stützpunkt des gewählten Flusses ist als Griff gewählt (−1: keiner)?</summary>
    public int WerkstattGriffIndex => _wsGriff is { Vieleck: -1 } g ? g.Index : -1;

    /// <summary>Der gewählte Griff eines Flusses hat sich geändert (Klick in der Stadt) – die Tafel zeigt dann Breite und Tiefe dieses Punktes.</summary>
    public event Action? WerkstattGriffGeaendert;

    private int _wsGriffGemeldet = -1;

    private void GriffAenderungMelden()
    {
        var jetzt = WerkstattGewaehlt is EntwurfFluss ? WerkstattGriffIndex : -1;
        if (jetzt == _wsGriffGemeldet) return;
        _wsGriffGemeldet = jetzt;
        WerkstattGriffGeaendert?.Invoke();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeug „Fluss“: Vorschau und Anlegen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild beim Zeichnen eines Flusses: die Kurve durch die gesetzten Punkte bis zur Maus als blaues Band, Pfeile in Fließrichtung, die Punkte.</summary>
    private void FlussVorschau(Vector2? maus)
    {
        var punkte = _wsPunkte.ToList();
        if (maus is { } m) punkte.Add(m);
        var breite = (float)(WerkstattFlussbreite > 0 ? WerkstattFlussbreite : Wasserform.Vorgabebreite(WerkstattFlussart));
        if (punkte.Count >= 2)
        {
            var verlauf = Wegkurve.Verlauf(punkte.Select(p => new double[] { p.X, p.Y }).ToList(), glatt: true).Punkte.Select(p => new Vector2((float)p[0], (float)p[1])).ToList();
            VorschauDreiecke(BandDreiecke(verlauf, breite / 2, 0.2f), new Color(Wasserblau, 0.5f));
            VorschauDreiecke(PfeilDreiecke(verlauf, Mathf.Max(10f, breite * 1.5f), Mathf.Clamp(breite * 0.3f, 1.6f, 4f), p => BodenHoehe(p) + 0.35f), new Color(1f, 1f, 1f, 0.9f));
        }
        VorschauPunkte(_wsPunkte, new Color(1f, 1f, 1f, 0.95f));
    }

    private void FlussFertig()
    {
        var punkte = _wsPunkte.Select(p => new Kartenort(p.X, p.Y)).ToList();
        _wsPunkte.Clear();
        if (WerkstattFlussAnlegen(WerkstattFlussart, WerkstattFlussbreite, punkte) is { } neu) WerkstattWaehlen(neu);
    }

    /// <summary>Legt einen Fluss oder Bach an (ein Schritt; Punkte in Fließrichtung); null, wenn es nicht ging (die Meldung steht unten).</summary>
    public EntwurfFluss? WerkstattFlussAnlegen(string art, double breite, List<Kartenort> punkte)
    {
        if (_entwurf is not { } e || punkte.Count < 2) return null;
        return Befehl(() => e.FlussAnlegen(punkte, art, breite));
    }

    /// <summary>Legt einen See oder Teich an (ein Schritt); null, wenn es nicht ging.</summary>
    public EntwurfSee? WerkstattSeeAnlegen(string art, List<Kartenort> ecken, double? wasserstand = null)
    {
        if (_entwurf is not { } e || ecken.Count < 3) return null;
        return Befehl(() => e.SeeAnlegen(ecken, art, null, wasserstand));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeug „Fähre“: zwei Klicks
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Legt eine Fährstelle an (ein Schritt; die Anleger rasten an Wegen ein); null, wenn es nicht ging.</summary>
    public EntwurfFaehre? WerkstattFaehreSetzen(Kartenort von, Kartenort bis)
    {
        if (_entwurf is not { } e) return null;
        return Befehl(() => e.FaehreSetzen(von, bis));
    }

    private void FaehreKlick(Vector2? boden)
    {
        if (_entwurf is null || boden is not { } p) return;
        if (_wsFaehreStart is not { } von)
        {
            _wsFaehreStart = p;
            return;
        }
        _wsFaehreStart = null;
        if (WerkstattFaehreSetzen(new Kartenort(von.X, von.Y), new Kartenort(p.X, p.Y)) is { } neu) WerkstattWaehlen(neu);
    }

    /// <summary>Jedes Bild mit dem Werkzeug „Fähre“: der erste Anleger (nach dem ersten Klick) und eine orange Linie bis zur Maus; der Ring zeigt, an welchem Weg ein Anleger einrastet.</summary>
    private void FaehreVorschau(Vector2? maus)
    {
        Vector2? ziel = maus;
        if (maus is { } m && Fangen(m) is { } fang)
        {
            FangmarkeZeigen(V2(fang.Ort), Fangfarbe(fang.Ziel));
            ziel = V2(fang.Ort);
        }
        if (_wsFaehreStart is { } a)
        {
            VorschauPunkte(new[] { a }, new Color(Faehrorange, 0.95f));
            if (ziel is { } b && b.DistanceTo(a) > 0.5f)
            {
                VorschauDreiecke(BandDreiecke(new List<Vector2> { a, b }, 0.5f, 0.3f), new Color(Faehrorange, 0.8f));
                VorschauPunkte(new[] { b }, new Color(Faehrorange, 0.95f));
            }
        }
        else if (ziel is { } p) VorschauPunkte(new[] { p }, new Color(Faehrorange, 0.95f));
    }

    private void WasserWerkzeugAus() => _wsFaehreStart = null;
}
