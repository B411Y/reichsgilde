using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Wege in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs).
///
/// <b>Zeichnen.</b> Jeder Weg des Entwurfs hat einen Knoten mit dem Weg, wie das Spiel ihn zeigt (<see cref="WegZeichnen"/>: gerade Strecken als
/// Quader, eine glatte Kurve als Band entlang <see cref="EntwurfWeg.Verlauf"/>), darüber eine Markierung (gold gewählt, rot mit Fehler). Nach
/// jeder Änderung an Wegen oder Gebäuden entsteht das Gassennetz der Werkstatt neu (<see cref="NetzBauen"/>): die Wege und, wie im Spiel, die
/// kurzen Stücke von Höfen, Karrenplätzen, Toren und vom Markt zum nächsten Weg. Der Schalter „Gassennetz“ zeigt es.
///
/// <b>Werkzeug „Weg“.</b> Punkte klicken; die Vorschau zeigt die Kurve (<see cref="Wegkurve.Verlauf"/>) bis zur Maus. Wo der erste und der letzte
/// Punkt einrasten, sagt der Kern (<see cref="Kartenentwurf.Einrasten"/>): am Hof eines Werkstattplatzes, in der Mitte eines Tores, an einem
/// Halteplatz (Karrenplatz eines Umlandbetriebs, Gatter einer Arbeitsstelle, <see cref="Halteplaetze"/>), sonst an einem anderen Weg; Punkte
/// dazwischen rasten nur in Tore ein (<see cref="Kartenentwurf.TorEinrasten"/>). Der Ring an der Maus zeigt das Ziel in seiner Farbe
/// (<see cref="Fangfarbe"/>). Doppelklick oder Enter legt den Weg an (<see cref="Kartenentwurf.WegAnlegen"/> – eine Gasse, die an einem Hof
/// endet, dreht der Kern um), Esc bricht ab, die Rücktaste nimmt den letzten Punkt weg.
///
/// <b>Bearbeiten.</b> Ein gewählter Weg zeigt seine Stützpunkte als Griffe (<see cref="GriffeBauen"/>): Ziehen verschiebt (die Enden rasten ein),
/// ein Klick auf die Linie fügt einen ein (gleich weiterziehbar), Entf löscht den gewählten Griff. Flächen nutzen dieselben Griffe für ihre Ecken.
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsWegeKnoten, _wsAnschluesse, _wsNetzAnsicht;
    private readonly Dictionary<EntwurfWeg, Node3D> _wsWege = new();

    /// <summary>Das Gassennetz der Werkstatt (das Spiel hat sein eigenes, <see cref="_netz"/>).</summary>
    private Gassennetz _wsNetz = new();
    private bool _wsNetzNeu;
    private bool _wsNetzSichtbar;

    /// <summary>Die Wegart, die das Werkzeug „Weg“ zeichnet.</summary>
    public Wegklasse WerkstattWegklasse { get; set; } = Wegklasse.Gasse;

    /// <summary>Neue Wege als weiche Kurve (true) oder aus geraden Strecken.</summary>
    public bool WerkstattGlatt { get; set; } = true;

    /// <summary>Zeigt das Gassennetz der Werkstatt (pink, Knoten gelb) – wie <c>--wegenetz=1</c> im Spiel.</summary>
    public bool WerkstattNetzSichtbar
    {
        get => _wsNetzSichtbar;
        set
        {
            _wsNetzSichtbar = value;
            if (_wsNetzNeu) NetzBauen(); // baut auch die Ansicht
            else NetzAnsichtBauen();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wege zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    private void WegeAufbauen()
    {
        _wsWegeKnoten = new Node3D { Name = "Wege" };
        AddChild(_wsWegeKnoten);
        _wsAnschluesse = new Node3D { Name = "Anschluesse" };
        AddChild(_wsAnschluesse);
        foreach (var w in _entwurf!.Wege) WegBauen(w, false);
        _wsNetzNeu = true;
    }

    /// <summary>Baut den Knoten eines Weges neu (<paramref name="weg"/>: er ist gelöscht – nur entfernen).</summary>
    private void WegBauen(EntwurfWeg w, bool weg)
    {
        if (_wsWege.Remove(w, out var alt)) KnotenEntfernen(alt);
        SchichtTeilGeaendert(w, weg || w.Verlauf.Count < 2 ? null : w.Verlauf.Select(V2), (float)w.BreiteWirksam / 2); // Stadtansicht.Wegeschicht.cs
        if (weg || _wsWegeKnoten is null || w.Verlauf.Count < 2) return;
        var knoten = new Node3D { Name = "Weg" };
        _wsWegeKnoten.AddChild(knoten);
        _wsWege[w] = knoten;
        var nummer = 0;
        foreach (var anderer in _entwurf.Wege)
            if (ReferenceEquals(anderer, w)) break;
            else nummer++;
        if (!WegeschichtMoeglich) WegZeichnen(w.Verlauf.Select(p => V3(p)).ToArray(), (float)w.BreiteWirksam, w.IstGlatt, knoten, WegLuecken(nummer), WegBoden(nummer)); // unter Brücken kein Band, in Furten eben (Stadtansicht.Wasser.Uebergaenge.cs)
        WegMarkieren(w);
        if (BereichVon(w) is { } bereich) _wsBereiche[w] = bereich;
    }

    /// <summary>Die Markierung über einem Weg: ein durchscheinendes Band, gold mit dem Namen, wenn gewählt, rot bei einem Fehler.</summary>
    private void WegMarkieren(EntwurfWeg w)
    {
        if (!_wsWege.TryGetValue(w, out var knoten)) return;
        if (knoten.GetNodeOrNull<Node3D>("Markierung") is { } alt)
        {
            knoten.RemoveChild(alt);
            KnotenEntfernen(alt);
        }
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, w);
        var fehler = Fehlerhaft(w);
        if (!gewaehlt && !fehler) return;
        var markierung = new Node3D { Name = "Markierung" };
        knoten.AddChild(markierung);
        var farbe = fehler ? Fehlerrot : Auswahlgold;
        // über allem (ohne Tiefentest): Die Häuser am Ring verdecken sonst die Gassen davor
        var ecken = BandDreiecke(w.Verlauf.Select(V2).ToList(), (float)w.BreiteWirksam / 2 + 0.6f, 0.14f);
        var band = DurchsichtigesNetz(ecken, farbe, gewaehlt ? 0.45f : 0.38f);
        ImmerSichtbar((StandardMaterial3D)band.MaterialOverride);
        markierung.AddChild(band);
        if (gewaehlt)
        {
            var mitte = V3(MitteVon(w));
            var art = Kartenweg.Anzeigename(w.Klasse);
            var name = Beschriftung(w.Bezeichnung == art ? art : $"{w.Bezeichnung} · {art}", mitte + new Vector3(0, 4, 0), 36, markierung);
            name.Modulate = farbe.Lightened(0.4f);
        }
    }

    /// <summary>Ein flaches, durchscheinendes Netz aus Dreiecken (Markierungen, Flächen), unbeleuchtet, ohne Schatten.</summary>
    private static MeshInstance3D DurchsichtigesNetz(Vector3[] ecken, Color farbe, float alpha)
    {
        var netz = new ArrayMesh();
        if (ecken.Length > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = ecken;
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        return new MeshInstance3D { Mesh = netz, MaterialOverride = Transparent(farbe, alpha), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    /// <summary>Liegt der Punkt auf dem Weg (halbe Breite plus <paramref name="toleranz"/>)?</summary>
    private static bool AufWeg(EntwurfWeg w, Vector2 p, float toleranz) =>
        w.Verlauf.Count >= 2 && w.NaechsteStelle(new Kartenort(p.X, p.Y)).Abstand <= w.BreiteWirksam / 2 + toleranz;

    /// <summary>Der Weg unter dem Punkt (der nächste, wenn mehrere) – null: keiner.</summary>
    private EntwurfWeg? WegBei(Vector2 p, float toleranz)
    {
        EntwurfWeg? beste = null;
        var abstand = double.MaxValue;
        foreach (var w in _entwurf!.Wege)
        {
            if (w.Verlauf.Count < 2) continue;
            var d = w.NaechsteStelle(new Kartenort(p.X, p.Y)).Abstand - w.BreiteWirksam / 2;
            if (d <= toleranz && d < abstand) (beste, abstand) = (w, d);
        }
        return beste;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gassennetz
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut das Gassennetz der Werkstatt aus den Wegen des Entwurfs und hängt, wie das Spiel (<see cref="Anschliessen"/>), den Markt, die Tore,
    /// die Höfe der Werkstattplätze, die Karrenplätze der Umlandbetriebe und die Gatter der Arbeitsstellen mit einem kurzen Stück an den nächsten
    /// Weg (gezeichnet wie im Spiel). Türen der Häuser kommen im Spiel dazu, hier nicht.
    /// </summary>
    private void NetzBauen()
    {
        _wsNetzNeu = false;
        if (_entwurf is null || _wsAnschluesse is null) return;
        _wsNetz = new Gassennetz();
        var strecken = new List<(Vector3 Von, Vector3 Bis)>();
        foreach (var w in _entwurf.Wege)
        {
            var punkte = w.Verlauf.Select(p => V3(p)).ToArray();
            if (punkte.Length < 2) continue;
            for (var i = 0; i + 1 < punkte.Length; i++) strecken.Add((punkte[i], punkte[i + 1]));
            _wsNetz.Weg(w.IstGlatt ? NetzPunkte(punkte) : punkte);
        }
        foreach (var kind in _wsAnschluesse.GetChildren()) kind.QueueFree();
        SchichtAnschluesseVergessen(); // Stadtansicht.Wegeschicht.cs

        void Anhaengen(Vector3 p, float? breite, bool ueberDenMarkt = true)
        {
            Vector3? naechster = null;
            var abstand = float.MaxValue;
            foreach (var (von, bis) in strecken)
            {
                var q = Geometry3D.GetClosestPointToSegment(p, von, bis);
                if (q.DistanceTo(p) < abstand) (naechster, abstand) = (q, q.DistanceTo(p));
            }
            if (ueberDenMarkt && NaechsterMarkt(p) is { } markt && markt.Abstand(p) < abstand) _wsNetz.Weg(p, markt.Mitte);
            else if (naechster is { } q)
            {
                _wsNetz.Weg(p, q);
                if (breite is { } b && abstand > 0.3f) WegZiehen(p, q, b, _wsAnschluesse);
            }
        }
        foreach (var platz in _maerkte)
            if (platz.Halb > 0 && !_wsNetz.Knoten.Any(k => k.DistanceTo(platz.Mitte) < Gassennetz.Fang)) Anhaengen(platz.Mitte, null, ueberDenMarkt: false);
        foreach (var tor in _entwurf.Mauern.SelectMany(m => m.Tore)) Anhaengen(V3(tor.Punkt), null);
        foreach (var g in _entwurf.Gebaeude)
        {
            if (g.Art == Bausteinart.Werkstattplatz && g.Hof is { } hof) Anhaengen(V3(hof), HofwegBreite);
            else if (g.Art == Bausteinart.Umlandbetrieb && g.Baustein?.Modell is { } datei)
                Anhaengen(ModellLage(g) * UmlandAnkerImModell(datei).Karren with { Y = 0 }, KarrenwegBreite);
            else if (g.Art == Bausteinart.Umlandstelle && g.Baustein?.Modell is { } stelle)
                Anhaengen(ModellLage(g) * UmlandAnkerImModell(stelle).Tuer with { Y = 0 }, FusswegBreite); // das Gatter der Arbeitsstelle
        }
        if (Uebergangsplan is { } plan)
            foreach (var f in plan.Faehren)
                if (f.UeberWasser > 1) _wsNetz.Faehre(new Vector3((float)f.Von.X, 0, (float)f.Von.Z), new Vector3((float)f.Bis.X, 0, (float)f.Bis.Z)); // Karren setzen über
        NetzAnsichtBauen();
    }

    private void NetzAnsichtBauen()
    {
        if (_wsNetzAnsicht is not null) KnotenEntfernen(_wsNetzAnsicht);
        _wsNetzAnsicht = null;
        if (!_wsNetzSichtbar || _entwurf is null) return;
        _wsNetzAnsicht = new Node3D { Name = "Gassennetz" };
        AddChild(_wsNetzAnsicht);
        NetzZeichnen(_wsNetz, _wsNetzAnsicht, immerSichtbar: true); // Stadtansicht.Wohnviertel.cs
    }

    /// <summary>Wie viele getrennte Teile das Gassennetz der Werkstatt hat (1 = alles verbunden).</summary>
    public int WerkstattNetzTeile => _wsNetz.Teile();

    // ═════════════════════════════════════════════════════════════════════════
    //  Einrasten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wo ein Wegende einrastet – der Kern sagt es (<see cref="Kartenentwurf.Einrasten"/>: Hof, Tor, Halteplatz, sonst ein Weg, jeweils das nächste
    /// Ziel); null: nirgends. <paramref name="ausser"/>: der Weg, der bearbeitet wird.
    /// </summary>
    private Wegfang? Fangen(Vector2 p, EntwurfWeg? ausser = null) => _entwurf?.Einrasten(p.X, p.Y, ausser);

    /// <summary>Wo ein Punkt zwischen Anfang und Ende einrastet: nur in der Mitte eines Tores (null: bleibt, wo er ist).</summary>
    private Wegfang? Zwischenpunkt(Vector2 p) => _entwurf?.TorEinrasten(p.X, p.Y);

    /// <summary>Die Farbe des Rings je Ziel: Weg weiß, Hof orange, Tor blau, Halteplatz grün.</summary>
    private static Color Fangfarbe(Fangziel ziel) => ziel switch
    {
        Fangziel.Hof => new Color(1f, 0.62f, 0.2f),
        Fangziel.Tor => Torblau,
        Fangziel.Halteplatz => new Color(0.45f, 0.95f, 0.4f),
        _ => Colors.White,
    };

    /// <summary>
    /// Die Halteplätze eines Gebäudes, die nur die Modelle kennen (<see cref="Kartenentwurf.Fangpunkte"/>): der Karrenplatz eines Umlandbetriebs
    /// („Anker_Karren“) und das Gatter einer Arbeitsstelle („Anker_Tuer“) – dieselben Punkte, die das Spiel ans Gassennetz hängt.
    /// </summary>
    private IEnumerable<Kartenort> Halteplaetze(EntwurfGebaeude g)
    {
        if (g.Baustein?.Modell is not { } datei) yield break;
        if (g.Art == Bausteinart.Umlandbetrieb) yield return Ort(ModellLage(g) * UmlandAnkerImModell(datei).Karren);
        else if (g.Art == Bausteinart.Umlandstelle) yield return Ort(ModellLage(g) * UmlandAnkerImModell(datei).Tuer);
    }

    private static Kartenort Ort(Vector3 p) => new(p.X, p.Z);

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeug „Weg“
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Vorschau beim Zeichnen: die Kurve durch die gesetzten Punkte bis zur Maus, die Punkte, der Fangring.</summary>
    private void WegVorschau(Vector2? maus)
    {
        var punkte = _wsPunkte.ToList();
        if (maus is { } m)
        {
            var fang = Fangen(m);
            if (fang is { } f) FangmarkeZeigen(V2(f.Ort), Fangfarbe(f.Ziel));
            punkte.Add(fang is { } g ? V2(g.Ort) : m);
        }
        var breite = (float)Kartenweg.Standardbreite(WerkstattWegklasse);
        if (punkte.Count >= 2)
        {
            var verlauf = Wegkurve.Verlauf(punkte.Select(p => new double[] { p.X, p.Y }).ToList(), WerkstattGlatt).Punkte;
            VorschauDreiecke(BandDreiecke(verlauf.Select(p => new Vector2((float)p[0], (float)p[1])).ToList(), breite / 2, 0.16f), new Color(1f, 0.9f, 0.55f, 0.55f));
        }
        VorschauPunkte(_wsPunkte, new Color(1f, 1f, 1f, 0.95f));
    }

    /// <summary>Legt den gezeichneten Weg an (ein Schritt, siehe <see cref="WerkstattWegAnlegen"/>) und wählt ihn.</summary>
    private void WegFertig()
    {
        var punkte = _wsPunkte.Select(p => new Kartenort(p.X, p.Y)).ToList();
        _wsPunkte.Clear();
        if (WerkstattWegAnlegen(WerkstattWegklasse, punkte, WerkstattGlatt) is { } neu) WerkstattWaehlen(neu);
    }

    /// <summary>
    /// Legt einen Weg an (ein Schritt): Anfang und Ende rasten im Kern ein (Hof, Tor, Halteplatz, Weg), Punkte dazwischen in Tore; eine Gasse,
    /// die an einem Hof endet und nicht an einem beginnt, dreht der Kern um (die Gasse beginnt dann am Hof). Null, wenn es nicht ging
    /// (die Meldung steht unten).
    /// </summary>
    public EntwurfWeg? WerkstattWegAnlegen(Wegklasse klasse, List<Kartenort> punkte, bool glatt)
    {
        if (_entwurf is not { } e || punkte.Count < 2) return null;
        return Befehl(() => e.WegAnlegen(klasse, punkte, glatt));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Griffe: Stützpunkte des gewählten Weges, Ecken der gewählten Fläche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Griff: Vieleck (−1 = Weg bzw. Umriss der Fläche, sonst die Aussparung) und Nummer des Punktes darin.</summary>
    private readonly record struct Griff(int Vieleck, int Index);

    /// <summary>Der gewählte Griff (Entf löscht ihn) – null: keiner.</summary>
    private Griff? _wsGriff;

    private Node3D? _wsGriffe;
    private readonly List<(Griff Griff, Vector3 Ort, MeshInstance3D Knoten)> _wsGriffListe = new();
    private StandardMaterial3D? _griffGold, _griffWeiss, _griffAussparung, _griffTor;
    private CylinderMesh? _griffNetz;

    /// <summary>Die Griffe des gewählten Weges, der gewählten Fläche bzw. Mauer neu (nach Auswahl und nach jeder Änderung daran).</summary>
    private void GriffeBauen()
    {
        _wsGriffeNeu = false;
        if (_wsGriffe is null)
        {
            _wsGriffe = new Node3D { Name = "Griffe" };
            AddChild(_wsGriffe);
            _griffGold = ImmerSichtbar(Transparent(Auswahlgold, 1));
            _griffWeiss = ImmerSichtbar(Transparent(Colors.White, 1));
            _griffAussparung = ImmerSichtbar(Transparent(new Color(0.95f, 0.55f, 0.2f), 1));
            _griffTor = ImmerSichtbar(Transparent(Torblau, 1));
            _griffNetz = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.15f, RadialSegments = 16 };
        }
        foreach (var (_, _, knoten) in _wsGriffListe) knoten.QueueFree();
        _wsGriffListe.Clear();
        var vielecke = WerkstattGewaehlt switch
        {
            EntwurfWeg w => new[] { (-1, w.Punkte) },
            EntwurfFluss fl => new[] { (-1, fl.Punkte) },
            EntwurfFaehre fa => new[] { (-1, (IReadOnlyList<Kartenort>)new[] { fa.Von, fa.Bis }) },
            EntwurfFlaeche f => f.Aussparungen.Select((a, i) => (i, a)).Prepend((-1, f.Ecken)).ToArray(),
            EntwurfMauer m => new[] { (-1, m.Ecken), (TorGriffe, (IReadOnlyList<Kartenort>)m.Tore.Select(t => t.Punkt).ToList()) },
            _ => Array.Empty<(int, IReadOnlyList<Kartenort>)>(),
        };
        foreach (var (vieleck, punkte) in vielecke)
            for (var i = 0; i < punkte.Count; i++)
            {
                var griff = new Griff(vieleck, i);
                var ort = V3(punkte[i], 0.3f);
                var knoten = new MeshInstance3D
                {
                    Mesh = _griffNetz, Position = ort, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = griff == _wsGriff ? _griffWeiss : vieleck == TorGriffe ? _griffTor : vieleck >= 0 ? _griffAussparung : _griffGold,
                };
                _wsGriffe.AddChild(knoten);
                _wsGriffListe.Add((griff, ort, knoten));
            }
        GriffeSkalieren();
    }

    /// <summary>Jedes Bild: Die Griffe sind auf dem Bildschirm immer gleich groß (etwa 7 Bildpunkte Halbmesser), der gewählte etwas größer.</summary>
    private void GriffeSkalieren()
    {
        var s = MeterJePixel * 7;
        foreach (var (griff, _, knoten) in _wsGriffListe)
        {
            var g = griff == _wsGriff ? s * 1.35f : s;
            knoten.Scale = new Vector3(g, 1, g);
        }
    }

    /// <summary>Der Griff an dieser Bildschirmstelle (höchstens 10 Bildpunkte daneben, der nächste) – null: keiner.</summary>
    private Griff? GriffBei(Vector2 maus)
    {
        Griff? beste = null;
        var abstand = 10f;
        foreach (var (griff, ort, _) in _wsGriffListe)
        {
            if (_kamera.IsPositionBehind(ort)) continue;
            var d = _kamera.UnprojectPosition(ort).DistanceTo(maus);
            if (d <= abstand) (beste, abstand) = (griff, d);
        }
        return beste;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ziehen von Griffen, Flächen und Pflanzen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Was gezogen wird: ein Griff, ein neuer Punkt auf der Linie bzw. dem Rand, oder der ganze Teil (Fläche, Pflanze).</summary>
    private enum Ziehart { Griff, Einfuegen, Ganz }

    private sealed class Teilziehvorgang
    {
        public required EntwurfTeil Teil { get; init; }
        public required Ziehart Art { get; init; }

        /// <summary>Wo die Maus drückte (Bildpunkte) und wo auf dem Boden.</summary>
        public required Vector2 Start { get; init; }
        public required Vector2 Anfang { get; init; }

        public int Vieleck { get; init; }

        /// <summary>Der gezogene Punkt bzw. die Stelle, an der eingefügt wird.</summary>
        public int Index { get; init; }

        public bool Aktiv { get; set; }

        /// <summary>Die vorläufige Lage (eingerastet).</summary>
        public Vector2 Ort { get; set; }
    }

    private Teilziehvorgang? _wsTeilZiehen;

    private void TeilZiehenBeginnen(EntwurfTeil teil, Ziehart art, Vector2 maus, Vector2 boden, int vieleck, int index) =>
        _wsTeilZiehen = new Teilziehvorgang { Teil = teil, Art = art, Start = maus, Anfang = boden, Vieleck = vieleck, Index = index, Ort = boden };

    /// <summary>Jedes Bild beim Ziehen: die vorläufige Lage rechnen (einrasten) und als Vorschau zeigen – der Entwurf ändert sich erst beim Loslassen.</summary>
    private void TeilZiehenNachfuehren(Teilziehvorgang t, Vector2? maus)
    {
        if (!t.Aktiv || maus is not { } m) return;
        switch (t.Teil)
        {
            case EntwurfWeg w:
            {
                var punkte = w.Punkte.Select(V2).ToList();
                var ende = t.Art == Ziehart.Griff && (t.Index == 0 || t.Index == punkte.Count - 1);
                var fang = ende ? Fangen(m, w) : Zwischenpunkt(m);
                if (fang is { } f) FangmarkeZeigen(V2(f.Ort), Fangfarbe(f.Ziel));
                t.Ort = fang is { } g ? V2(g.Ort) : m;
                if (t.Art == Ziehart.Griff) punkte[t.Index] = t.Ort;
                else punkte.Insert(t.Index, t.Ort);
                var verlauf = Wegkurve.Verlauf(punkte.Select(p => new double[] { p.X, p.Y }).ToList(), w.IstGlatt).Punkte;
                VorschauDreiecke(BandDreiecke(verlauf.Select(p => new Vector2((float)p[0], (float)p[1])).ToList(), (float)w.BreiteWirksam / 2, 0.18f),
                    new Color(1f, 0.9f, 0.55f, 0.55f));
                VorschauPunkte(new[] { t.Ort }, Colors.White);
                break;
            }
            case EntwurfMauer mauer:
                MauerZiehenNachfuehren(t, mauer, m); // Stadtansicht.Kartenwerkstatt.Mauern.cs
                break;
            case EntwurfFluss fluss:
            {
                var punkte = fluss.Punkte.Select(V2).ToList();
                t.Ort = m;
                if (t.Art == Ziehart.Griff) punkte[t.Index] = t.Ort;
                else punkte.Insert(t.Index, t.Ort);
                var verlauf = Wegkurve.Verlauf(punkte.Select(p => new double[] { p.X, p.Y }).ToList(), glatt: true).Punkte.Select(p => new Vector2((float)p[0], (float)p[1])).ToList();
                VorschauDreiecke(BandDreiecke(verlauf, (float)fluss.BreiteWirksam / 2, 0.2f), new Color(Wasserblau, 0.5f));
                VorschauDreiecke(PfeilDreiecke(verlauf, 14, 2f, p => BodenHoehe(p) + 0.35f), new Color(1f, 1f, 1f, 0.9f));
                VorschauPunkte(new[] { t.Ort }, Colors.White);
                break;
            }
            case EntwurfFaehre faehre:
            {
                var fang = Fangen(m);
                if (fang is { } f) FangmarkeZeigen(V2(f.Ort), Fangfarbe(f.Ziel));
                t.Ort = fang is { } g ? V2(g.Ort) : m;
                var anderer = V2(faehre.Anleger(1 - t.Index));
                VorschauDreiecke(BandDreiecke(new List<Vector2> { anderer, t.Ort }, 0.5f, 0.3f), new Color(Faehrorange, 0.8f));
                VorschauPunkte(new[] { t.Ort }, Colors.White);
                break;
            }
            case EntwurfFlaeche f when t.Art != Ziehart.Ganz:
            {
                t.Ort = Eckpunkt(f, m);
                var punkte = f.Vieleck(t.Vieleck).Select(V2).ToList();
                if (t.Art == Ziehart.Griff) punkte[t.Index] = t.Ort;
                else punkte.Insert(t.Index, t.Ort);
                VorschauVieleck(punkte, FlaechenFarbe(f), geschlossen: true);
                VorschauPunkte(new[] { t.Ort }, Colors.White);
                break;
            }
            case EntwurfFlaeche f:
            {
                var d = m - t.Anfang;
                if (f is EntwurfBauzone) d = new Vector2((float)Kartenraster.Runden(d.X, _entwurf!.Raster), (float)Kartenraster.Runden(d.Y, _entwurf.Raster));
                t.Ort = t.Anfang + d;
                if (_wsFlaechen.TryGetValue(f, out var knoten)) knoten.Position = new Vector3(d.X, 0, d.Y);
                if (_wsGriffe is not null) _wsGriffe.Position = new Vector3(d.X, 0, d.Y);
                break;
            }
            case EntwurfPflanze p:
                t.Ort = m;
                if (_wsPflanzen.TryGetValue(p, out var pflanze)) pflanze.Position = V3(p.Punkt) + new Vector3(m.X - t.Anfang.X, 0, m.Y - t.Anfang.Y);
                break;
        }
    }

    /// <summary>
    /// Loslassen: aus dem Ziehen wird ein Befehl (ein Rückgängig-Schritt). Ein Klick auf die Linie bzw. den Rand ohne Ziehen fügt dort einen
    /// Punkt ein; der neue bzw. gezogene Punkt bleibt als Griff gewählt.
    /// </summary>
    private void TeilZiehenBeenden()
    {
        if (_wsTeilZiehen is not { } t || _entwurf is null) return;
        TeilZiehenAbbrechen();
        var e = _entwurf;
        var (x, z) = ((double)t.Ort.X, (double)t.Ort.Y);
        Griff? griff = null;
        switch (t.Teil)
        {
            case EntwurfWeg w when t.Art == Ziehart.Griff:
                if (!t.Aktiv) return;
                Befehl(() => e.StuetzpunktVerschieben(w, t.Index, x, z)); // Enden rasten ein wie beim Anlegen, Punkte dazwischen in Tore
                griff = new Griff(-1, t.Index);
                break;
            case EntwurfWeg w:
                if (t.Aktiv) Befehl(() => e.StuetzpunktEinfuegen(w, t.Index, x, z));
                var index = t.Aktiv ? t.Index : Befehl<int?>(() => e.StuetzpunktEinfuegenBei(w, t.Anfang.X, t.Anfang.Y)) ?? -1;
                griff = index >= 0 ? new Griff(-1, index) : null;
                break;
            case EntwurfFlaeche f when t.Art == Ziehart.Griff:
                if (!t.Aktiv) return;
                Befehl(() => e.EckeVerschieben(f, t.Index, x, z, t.Vieleck));
                griff = new Griff(t.Vieleck, t.Index);
                break;
            case EntwurfFlaeche f when t.Art == Ziehart.Einfuegen:
                if (t.Aktiv) Befehl(() => e.EckeEinfuegen(f, t.Index, x, z, t.Vieleck));
                var ecke = t.Aktiv ? t.Index : Befehl<int?>(() => e.EckeEinfuegenBei(f, t.Anfang.X, t.Anfang.Y, t.Vieleck)) ?? -1;
                griff = ecke >= 0 ? new Griff(t.Vieleck, ecke) : null;
                break;
            case EntwurfFlaeche f:
                if (t.Aktiv && t.Ort != t.Anfang) Befehl(() => e.FlaecheVerschieben(f, t.Ort.X - t.Anfang.X, t.Ort.Y - t.Anfang.Y));
                break;
            case EntwurfPflanze p:
                if (t.Aktiv && t.Ort != t.Anfang) Befehl(() => e.PflanzeVerschieben(p, p.X + t.Ort.X - t.Anfang.X, p.Z + t.Ort.Y - t.Anfang.Y));
                break;
            case EntwurfMauer mauer:
                griff = MauerZiehenBeenden(t, mauer);
                break;
            case EntwurfFluss fluss when t.Art == Ziehart.Griff:
                if (!t.Aktiv) return;
                Befehl(() => e.FlussPunktVerschieben(fluss, t.Index, x, z));
                griff = new Griff(-1, t.Index);
                break;
            case EntwurfFluss fluss:
                if (t.Aktiv) Befehl(() => e.FlussPunktEinfuegen(fluss, t.Index, x, z));
                var punkt = t.Aktiv ? t.Index : Befehl<int?>(() => e.FlussPunktEinfuegenBei(fluss, t.Anfang.X, t.Anfang.Y)) ?? -1;
                griff = punkt >= 0 ? new Griff(-1, punkt) : null;
                break;
            case EntwurfFaehre faehre when t.Art == Ziehart.Griff:
                if (!t.Aktiv) return;
                Befehl(() => e.FaehreAnlegerVerschieben(faehre, t.Index, x, z)); // rastet an einem Weg ein
                griff = new Griff(-1, t.Index);
                break;
        }
        if (griff is not null && ReferenceEquals(WerkstattGewaehlt, t.Teil))
        {
            _wsGriff = griff;
            GriffeBauen();
        }
    }

    /// <summary>Esc beim Ziehen (und vor jedem Befehl beim Loslassen): alles Vorläufige zurück an seinen Platz.</summary>
    private void TeilZiehenAbbrechen()
    {
        if (_wsTeilZiehen is not { } t) return;
        _wsTeilZiehen = null;
        if (t.Teil is EntwurfFlaeche f && _wsFlaechen.TryGetValue(f, out var knoten)) knoten.Position = Vector3.Zero;
        if (_wsGriffe is not null) _wsGriffe.Position = Vector3.Zero;
        if (t.Teil is EntwurfPflanze p && _wsPflanzen.TryGetValue(p, out var pflanze)) pflanze.Position = V3(p.Punkt);
        if (t.Teil is EntwurfMauer m && _wsMauern.TryGetValue(m, out var mauer)) (mauer.Visible, mauer.Position) = (true, Vector3.Zero);
        TorwegeZeigen(Array.Empty<EntwurfWeg>()); // Wege, die der Vorschau eines wandernden Tores folgten (Stadtansicht.Kartenwerkstatt.Mauern.cs)
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschau (jedes Bild neu) und Fangring
    // ═════════════════════════════════════════════════════════════════════════

    private MeshInstance3D? _wsVorschau, _wsFangmarke;
    private ImmediateMesh? _wsVorschauNetz;
    private StandardMaterial3D? _wsVorschauStoff;

    private void VorschauLeeren()
    {
        _wsVorschauNetz?.ClearSurfaces();
        FangmarkeZeigen(null);
    }

    /// <summary>Durchscheinende Dreiecke in einer Farbe, über allem (ohne Tiefentest) – für Wege, Flächen, Pinselstriche beim Zeichnen.</summary>
    private void VorschauDreiecke(Vector3[] ecken, Color farbe)
    {
        if (ecken.Length == 0) return;
        if (_wsVorschauNetz is null)
        {
            _wsVorschauNetz = new ImmediateMesh();
            _wsVorschauStoff = ImmerSichtbar(new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            });
            _wsVorschau = new MeshInstance3D { Name = "Vorschau", Mesh = _wsVorschauNetz, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_wsVorschau);
        }
        _wsVorschauNetz.SurfaceBegin(Mesh.PrimitiveType.Triangles, _wsVorschauStoff);
        foreach (var e in ecken)
        {
            _wsVorschauNetz.SurfaceSetColor(farbe);
            _wsVorschauNetz.SurfaceAddVertex(e);
        }
        _wsVorschauNetz.SurfaceEnd();
    }

    /// <summary>Kleine Quadrate an Punkten (gesetzte Punkte beim Zeichnen), auf dem Bildschirm etwa 10 Bildpunkte groß.</summary>
    private void VorschauPunkte(IEnumerable<Vector2> punkte, Color farbe)
    {
        var h = MeterJePixel * 5;
        var ecken = new List<Vector3>();
        foreach (var p in punkte)
        {
            Dreieck(ecken, p + new Vector2(-h, -h), p + new Vector2(h, -h), p + new Vector2(h, h), 0.25f);
            Dreieck(ecken, p + new Vector2(-h, -h), p + new Vector2(h, h), p + new Vector2(-h, h), 0.25f);
        }
        VorschauDreiecke(ecken.ToArray(), farbe);
    }

    /// <summary>Versteckt den Ring.</summary>
    private void FangmarkeZeigen(Vector2? ort)
    {
        if (ort is { } p) FangmarkeZeigen(p, Colors.White);
        else if (_wsFangmarke is not null) _wsFangmarke.Visible = false;
    }

    /// <summary>Der Ring an der Stelle, an der ein Punkt einrastet, in der Farbe des Ziels (<see cref="Fangfarbe"/>).</summary>
    private void FangmarkeZeigen(Vector2 ort, Color farbe)
    {
        if (_wsFangmarke is null)
        {
            _wsFangmarke = new MeshInstance3D
            {
                Name = "Fangmarke", Mesh = new TorusMesh { InnerRadius = 0.8f, OuterRadius = 1f, Rings = 32 },
                MaterialOverride = ImmerSichtbar(Transparent(Colors.White, 0.95f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_wsFangmarke);
        }
        ((StandardMaterial3D)_wsFangmarke.MaterialOverride).AlbedoColor = new Color(farbe, 0.95f);
        _wsFangmarke.Visible = true;
        _wsFangmarke.Position = new Vector3(ort.X, BodenHoehe(ort.X, ort.Y) + 0.3f, ort.Y);
        _wsFangmarke.Scale = Vector3.One * Mathf.Max(0.8f, MeterJePixel * 12);
    }
}
