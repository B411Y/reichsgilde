using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Stadt aus der Karte – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Was wo steht, kommt nur aus der Karte der Partie (<c>Stadtwirtschaft.Karte</c>, ohne Partie die Greybox-Karte) und dem Gebäudekatalog
/// (Kartenformat 2, <c>docs/LEVELDESIGN.md</c>): Boden, Mauern mit Toren, Marktplatz, Rathaus, Dom, Kulissenhäuser, Felsberge, Wege, die
/// Arbeitsstellen im Umland und die Natur. Der Code kennt keine Lagen; er liest nur die Anker der Modelle (Tür, Karrenplatz, Gatter).
///
/// Lage eines Gebäudes: <c>punkt</c> ist die Mitte seiner Fläche, <c>drehung</c> der Winkel um die Hochachse in Grad wie Godot
/// <c>Rotation.Y</c> (0 = Vorderseite nach Süden, +z), <c>massstab</c> vergrößert Modell und Fläche. Der Ursprung des Modells liegt um den
/// <c>versatz</c> des Bausteins neben der Mitte (Dom, Felsberg), gedreht und mit dem Maßstab (<see cref="ModellLage"/>).
///
/// Das <see cref="Gassennetz"/> entsteht aus den Wegen der Karte (<see cref="WegeAnlegen"/>). Türen, Höfe, Karrenplätze, Gatter und Tore
/// hängen mit einem kurzen Stück am nächsten Weg (<see cref="AnWegAnschliessen"/>); eine Tür am Marktplatz geht über das Pflaster zur Mitte.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Karte, die die Stadtansicht zeichnet (null: keine geladen – dann gibt es nur die Wiese).</summary>
    private Stadtkarte? _karte;
    private bool _karteGebaut;
    private Gebaeudekatalog? Katalog => _karte?.Katalog ?? _entwurf?.Katalog;

    /// <summary>
    /// Mitte des (ersten) Marktplatzes (Ursprung, wenn die Karte keinen Markt hat) und die Lage seines Modells. <c>_markt</c> liegt immer auf y = 0 (das
    /// Gassennetz rechnet in der Ebene), <c>_marktY</c> ist die Höhe des Pflasters. Hat die Stadt mehrere Marktplätze (<see cref="_maerkte"/>, Köln), sind
    /// diese Felder der erste (Brunnen, Fahnen, Vögel und der Blick des Klangs bleiben bei ihm); alles, was zu einem bestimmten Platz gehört (Karren,
    /// Stände, Passanten, Wege über das Pflaster), liest den Platz aus <see cref="_maerkte"/>.
    /// </summary>
    private Vector3 _markt;
    private float _marktY;
    private Transform3D _marktLage = Transform3D.Identity;

    /// <summary>Halbe Seitenlänge des Pflasters (aus dem Katalog, 12 m beim Greybox-Markt); 0 ohne Marktplatz.</summary>
    private float _marktHalb;

    /// <summary>Die Tore aller Mauern: Name, Ort und die Richtung nach draußen (Feldseite).</summary>
    private readonly List<(string Name, Vector3 Ort, Vector3 Aussen)> _tore = new();

    /// <summary>Die Mauerlinien als Flächen (eine offene Linie wird mit einer Geraden geschlossen) – was darin liegt, ist „in der Stadt“.</summary>
    private readonly List<Vector2[]> _mauerFlaechen = new();

    /// <summary>Alle Gebäude, Wege und Mauern liegen in diesem Rechteck (x, z) – die Kamera bleibt in seiner Nähe.</summary>
    private Rect2 _kartenRahmen = new(-110, -100, 255, 200);

    /// <summary>Die gezeichneten Wege der Karte, Stück für Stück: hier schließen Türen und Höfe an, hier wächst nichts.</summary>
    private readonly List<(Vector3 Von, Vector3 Bis, float Breite)> _wegStrecken = new();

    /// <summary>Wo die Wege an Rathaus und Dom beginnen (vor der Tür des Modells); null, wenn die Karte keins hat.</summary>
    private Vector3? _rathausTuer, _domTuer;

    /// <summary>Die Arbeitsstellen der Einwohner im Umland (Weide, Feld, Wald) nach ihrem Namen in der Karte, wie der Kern sie nennt.</summary>
    private readonly Dictionary<string, Kartengebaeude> _umlandstellen = new();

    /// <summary>Wo das Spiel die Bewohner eines Kulissenhauses empfängt: Kulisse (Gebäude der Karte) → Haustür im Gassennetz.</summary>
    private readonly Dictionary<Kartengebaeude, Vector3> _kulissenTueren = new();

    /// <summary>Breite des Stücks, das einen Werkstatthof, einen Karrenplatz oder ein Gatter an den nächsten Weg hängt (wie die Gassen, Land- und Fußwege).</summary>
    private const float HofwegBreite = 3.5f, KarrenwegBreite = 4f, FusswegBreite = 2f;

    /// <summary>So weit innen im Vorplatz (vor der Vorderkante der Fläche) liegt der Hof einer Werkstatt: Hier beginnt ihre Gasse.</summary>
    private const float HofVorKante = 0.5f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechnen mit der Karte
    // ═════════════════════════════════════════════════════════════════════════

    private static Vector3 Punkt3(double[] p) => new((float)p[0], 0, (float)p[1]);
    private static Vector2 Punkt2(double[] p) => new((float)p[0], (float)p[1]);

    /// <summary>Die Drehung eines Gebäudes der Karte im Bogenmaß (wie <c>Rotation.Y</c>).</summary>
    private static float DrehungVon(Kartengebaeude g) => Mathf.DegToRad((float)g.Drehung);

    /// <summary>Die Richtung, in die die Vorderseite bei dieser Drehung (Bogenmaß) zeigt.</summary>
    private static Vector3 VorneBei(float drehung) => new(Mathf.Sin(drehung), 0, Mathf.Cos(drehung));

    /// <summary>Breite (an der Vorderseite) und Tiefe der Fläche eines Gebäudes in Metern, mit seinem Maßstab.</summary>
    private static Vector2 GroesseVon(Kartengebaeude g) =>
        g.Baustein?.Groesse is { Length: 2 } gr ? new Vector2((float)gr[0], (float)gr[1]) * (float)g.Massstab : Vector2.Zero;

    /// <summary>Ein Punkt der Fläche eines Gebäudes in Koordinaten der Stadt (<paramref name="lokal"/>: x nach rechts, z zur Vorderseite, von der Mitte).</summary>
    private static Vector3 AufFlaeche(Kartengebaeude g, Vector3 lokal) => Punkt3(g.Punkt) + new Basis(Vector3.Up, DrehungVon(g)) * lokal;

    /// <summary>Wo das Modell eines Gebäudes steht: Ursprung (Mitte der Fläche plus gedrehter <c>versatz</c>), Drehung und Maßstab.</summary>
    private Transform3D ModellLage(Kartengebaeude g)
    {
        var m = (float)g.Massstab;
        var drehung = new Basis(Vector3.Up, DrehungVon(g));
        var versatz = g.Baustein?.Versatz is { Length: 2 } v ? new Vector3((float)v[0], 0, (float)v[1]) * m : Vector3.Zero;
        return new Transform3D(drehung.Scaled(Vector3.One * m), Punkt3(g.Punkt) + drehung * versatz + new Vector3(0, FundamentVon(g).Hoehe, 0)); // auf dem Fußboden des Fundaments
    }

    /// <summary>Liegt der Punkt innerhalb einer Stadtmauer?</summary>
    public bool InDerStadt(Vector3 p) => _mauerFlaechen.Any(f => Geometry2D.IsPointInPolygon(new Vector2(p.X, p.Z), f));

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet die Stadt der Karte – einmal, vor allen Betrieben: aus <see cref="Verbinden"/> mit der Karte der Partie, ohne Partie (Hauptmenü)
    /// nach dem Aufbau der Szene mit der Greybox-Karte. In einem Zug; der Ladebildschirm nimmt <see cref="KarteAufbauenSchritte"/>.
    /// </summary>
    private void KarteAufbauen(Stadtkarte? karte)
    {
        foreach (var _ in KarteAufbauenSchritte(karte)) { }
    }

    /// <summary>
    /// Der Aufbau der Karte in Abschnitten (für den Ladebildschirm): Vor jedem Abschnitt meldet er, wie viel schon fertig ist und was jetzt kommt;
    /// gerechnet wird beim nächsten Weiterzählen. Die Anteile folgen der Messung mit <c>--ladezeit=1</c> (Köln, 29.10.2026).
    /// </summary>
    private IEnumerable<Ladeschritt> KarteAufbauenSchritte(Stadtkarte? karte)
    {
        if (_karteGebaut) yield break;
        _karteGebaut = true;
        _karte = karte;
        if (karte is null)
        {
            Flaeche(Vector3.Zero, new Vector2(520, 480), Wiese, -0.05f); // ohne Karte nur die Wiese (der Fehler ist schon gemeldet)
            yield break;
        }
        Ladezeit.Marke("vor dem Kartenaufbau");
        yield return new Ladeschritt(0f, "Das Land wird vermessen …");
        _gelaende = karte.Gelaendeform; // der Boden: vor allem, was darauf steht (Stadtansicht.Gelaende.cs)
        LagenLesen(karte);
        BodenLegen(karte);
        Ladezeit.Marke("Szene: Boden und Gelände");
        yield return new Ladeschritt(0.28f, "Die Stadtmauer wird hochgezogen …");
        foreach (var g in karte.Gebaeude.Where(g => g.Art is not (Bausteinart.Wohnhausplatz or Bausteinart.Einwohnerplatz))) GebaeudeSockel(g); // Häuser bekommen ihren Sockel selbst
        foreach (var mauer in karte.Mauern) MauerAusKarte(mauer);
        Ladezeit.Marke("Szene: Sockel und Mauern");
        yield return new Ladeschritt(0.30f, "Rathaus und Dom werden errichtet …");
        foreach (var platz in _maerkte) Marktstaende(platz); // Stadtansicht.Markt.cs: Stände, Verkäufer und Klickfläche an jedem Platz
        SonderbautenBauen(karte); // Rathaus, Dom und Bauten ohne eigene Ansicht
        Ladezeit.Marke("Szene: Markt und Sonderbauten");
        yield return new Ladeschritt(0.33f, "Häuser werden gebaut …");
        Kulissenhaeuser(karte);
        Ladezeit.Marke("Szene: Kulissenhäuser");
        yield return new Ladeschritt(0.34f, "Wege werden gepflastert …");
        foreach (var berg in karte.GebaeudeDerArt(Bausteinart.Berg)) BergBauen(berg);
        WegeAnlegen(karte);
        Ladezeit.Marke("Szene: Berge und Wege");
        yield return new Ladeschritt(0.54f, "Der Fluss wird eingelassen …");
        GewaesserBauen();    // Wasser, Brücken, Furten, Fähren (Stadtansicht.Wasser.cs)
        FaehrenAnsNetz();    // Karren setzen mit Fähren über (Stadtansicht.Wasser.Uebergaenge.cs)
        Ladezeit.Marke("Szene: Wasser");
        yield return new Ladeschritt(0.555f, "Höfe und Gruben werden angelegt …");
        UmlandstellenBauen(karte); // Stadtansicht.Umland.cs
        Anschliessen(karte);
        Ladezeit.Marke("Szene: Umland und Anschlüsse");
        yield return new Ladeschritt(0.58f, "Bäume werden gepflanzt …");
        NaturAusKarte(karte); // Stadtansicht.Natur.cs
        FelsenAusKarte(karte); // Stadtansicht.Felsen.cs
        KulisseAusKarte(karte); // Stadtansicht.Kulisse.cs
        Ladezeit.Marke("Szene: Natur");
        yield return new Ladeschritt(0.605f, "Felder werden bestellt …");
        FelderAusKarte(karte); // Stadtansicht.Rand.cs
        Ladezeit.Marke("Szene: Felder");
        yield return new Ladeschritt(0.70f, "Das Pflaster wird gelegt …");
        KameraAusKarte(karte);
        _schichtAlles = true;
        WegeschichtBild(); // Wege, Anschlüsse und Pflaster in den Boden (Stadtansicht.Wegeschicht.cs)
        Ladezeit.Marke("Szene: Wegeschicht");
        if (_netz.Teile() != 1)
            GD.PushWarning($"Gassennetz der Karte „{karte.Kennung}“: {_netz.Teile()} getrennte Teile – nicht jedes Ziel ist erreichbar.");
    }

    /// <summary>Marktplatz, Tore, Mauerflächen und Rahmen der Karte – was später beim Zeichnen und Laufen gebraucht wird.</summary>
    private void LagenLesen(Stadtkarte karte)
    {
        foreach (var markt in karte.Marktplaetze)
        {
            var platz = new MarktplatzAnsicht
            {
                Nummer = _maerkte.Count,
                Gebaeude = markt,
                Lage = ModellLage(markt),
                Mitte = Punkt3(markt.Punkt),
                Hoehe = FundamentVon(markt).Hoehe,
                Halb = Mathf.Min(GroesseVon(markt).X, GroesseVon(markt).Y) / 2,
            };
            _maerkte.Add(platz);
            if (_maerkte.Count > 1) continue;
            (_marktLage, _markt, _marktY, _marktHalb) = (platz.Lage, platz.Mitte, platz.Hoehe, platz.Halb); // der erste Platz bleibt „der Markt“ der übrigen Dateien
        }
        foreach (var mauer in karte.Mauern)
        {
            var linie = mauer.Linie.Select(Punkt3).ToList();
            if (linie.Count < 2) continue;
            _mauerFlaechen.Add(linie.Select(p => new Vector2(p.X, p.Z)).ToArray());
            var mitte = linie.Aggregate(Vector3.Zero, (s, p) => s + p) / linie.Count;
            foreach (var tor in mauer.Tore)
            {
                var ort = Punkt3(tor.Punkt);
                var i = NaechsteStrecke(linie, mauer.Geschlossen, ort);
                var (von, bis) = (linie[i], linie[(i + 1) % linie.Count]);
                _tore.Add((tor.Name, ort, Ausrichtung((bis - von).Normalized(), (von + bis) / 2 - mitte).Z));
            }
        }
        var punkte = karte.Gebaeude.Select(g => Punkt2(g.Punkt)).Concat(karte.Wege.SelectMany(w => w.Linie.Select(Punkt2)))
                          .Concat(karte.Mauern.SelectMany(m => m.Linie.Select(Punkt2))).ToList();
        if (punkte.Count > 0)
        {
            var rahmen = new Rect2(punkte[0], Vector2.Zero);
            foreach (var p in punkte) rahmen = rahmen.Expand(p);
            _kartenRahmen = rahmen;
        }
    }

    /// <summary>
    /// Boden und Dunst um die ganze Karte (Spielfeld und Randstreifen, dazu <see cref="RandZugabe"/> darüber hinaus – so sieht die Kamera am Rand
    /// auch ganz herausgezoomt weder Himmel noch eine Kante; Stadtansicht.Rand.cs) und das Pflaster in jeder Mauer.
    /// </summary>
    private void BodenLegen(Stadtkarte karte)
    {
        BodenBauen(_entwurf?.Kartengrenzen ?? karte.Kartengrenzen);
        if (!WegeschichtAn) // sonst liegt das Pflaster in der Wegeschicht (Stadtansicht.Wegeschicht.cs)
            foreach (var flaeche in _mauerFlaechen)
                Vieleck(flaeche, Pflaster, 0f);
    }

    /// <summary>Eine flache Fläche in Form eines Vielecks (Pflaster in der Mauer). Die UV-Koordinaten laufen wie bei einer Ebene von 0 bis 1 über das umschließende Rechteck.</summary>
    private void Vieleck(Vector2[] punkte, Color farbe, float hoehe, Node3D? eltern = null)
    {
        var dreiecke = Geometry2D.TriangulatePolygon(punkte);
        if (dreiecke.Length == 0) return;
        var rahmen = new Rect2(punkte[0], Vector2.Zero);
        foreach (var p in punkte) rahmen = rahmen.Expand(p);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        if (BodenBewegt)
        {
            // auf Gelände genau auf den Dreiecken des Bodens: kein Riss, nichts, das durchschaut (Stadtansicht.Gelaende.cs)
            var ecken = RasterDreiecke(punkte, hoehe + PflasterAmHang);
            arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = ecken.Select(e => BodenNormale(e.X, e.Z)).ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = ecken.Select(e => (new Vector2(e.X, e.Z) - rahmen.Position) / rahmen.Size).ToArray();
        }
        else
        {
            arrays[(int)Mesh.ArrayType.Vertex] = punkte.Select(p => new Vector3(p.X, hoehe, p.Y)).ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = punkte.Select(_ => Vector3.Up).ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = punkte.Select(p => (p - rahmen.Position) / rahmen.Size).ToArray();
            // Die Vorderseite soll nach oben schauen: Das ist bei Godot ein Dreieck, dessen Punkte in (x, z) gerechnet einen positiven Flächeninhalt haben.
            for (var i = 0; i + 2 < dreiecke.Length; i += 3)
                if (Flaecheninhalt(new[] { punkte[dreiecke[i]], punkte[dreiecke[i + 1]], punkte[dreiecke[i + 2]] }) < 0)
                    (dreiecke[i + 1], dreiecke[i + 2]) = (dreiecke[i + 2], dreiecke[i + 1]);
            arrays[(int)Mesh.ArrayType.Index] = dreiecke;
        }
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        (eltern ?? this).AddChild(new MeshInstance3D { Name = "Pflaster", Mesh = netz, MaterialOverride = Material(farbe) });
    }

    /// <summary>Flächeninhalt eines Vielecks mit Vorzeichen (Gaußsche Trapezformel): positiv, wenn die Punkte in (x, z) gegen den Uhrzeigersinn laufen.</summary>
    private static float Flaecheninhalt(IReadOnlyList<Vector2> punkte)
    {
        var summe = 0f;
        for (var i = 0; i < punkte.Count; i++)
        {
            var (a, b) = (punkte[i], punkte[(i + 1) % punkte.Count]);
            summe += a.X * b.Y - b.X * a.Y;
        }
        return summe / 2;
    }

    /// <summary>Eine Mauer der Karte aus ihrem Baukasten nach dem <see cref="Mauerplan"/> des Kerns (Stadtansicht.Mauer.cs).</summary>
    private void MauerAusKarte(Kartenmauer mauer)
    {
        if (mauer.Linie.Count < 2) return;
        MauerAufbauen(MauerDatei(mauer.Katalog), Mauerplan.Von(mauer), this, spiel: true);
    }

    /// <summary>Das Modell des Mauer-Baukastens (Kennung im Katalog).</summary>
    private string MauerDatei(string katalog) => Katalog?.Finde(katalog)?.Modell ?? "gebaeude/stadtmauer";

    /// <summary>
    /// Die Sonderbauten der Karte: Rathaus und Dom mit Innenraum (Stadtansicht.Sonderbauten.cs), je das erste der Karte. Andere Sonderbauten mit
    /// Modell stehen als Modell da; ohne Modell (Kirche, Hafen) ein schlichter Block mit Namen, bis es eins gibt.
    /// </summary>
    private void SonderbautenBauen(Stadtkarte karte)
    {
        foreach (var g in karte.GebaeudeDerArt(Bausteinart.Sonderbau))
        {
            if (g.Katalog == "markt") continue;
            if (g.Katalog == "rathaus" && !_sonderbauten.ContainsKey("Rathaus")) RathausBauen(g);
            else if (g.Katalog is "dom" or "koelner_dom" && !_sonderbauten.ContainsKey("Dom")) DomBauen(g);
            else if (g.Baustein?.Modell is { } datei)
            {
                if (ModellEinsetzen(datei, this, Vector3.Zero, new ModellZustand()) is { } modell)
                {
                    modell.Wurzel.Transform = ModellLage(g);
                    HafenbauKlickbar(karte, g, modell.Wurzel); // Kran und Lagerschuppen öffnen die Hafentafel (Stadtansicht.Schiffe.cs)
                }
            }
            else Platzhalter(g);
        }
    }

    /// <summary>Ein Gebäude ohne Modell: ein grauer Block auf seiner Fläche mit dem Namen darüber (unter der Maus, mit Alt).</summary>
    private void Platzhalter(Kartengebaeude g)
    {
        var groesse = GroesseVon(g);
        if (groesse == Vector2.Zero) return;
        var gruppe = new Node3D { Name = "Platzhalter_" + g.Kennung, Transform = new Transform3D(new Basis(Vector3.Up, DrehungVon(g)), Punkt3(g.Punkt) + new Vector3(0, FundamentVon(g).Hoehe, 0)) };
        AddChild(gruppe);
        const float hoehe = 6;
        Quader(new Vector3(0, hoehe / 2, 0), new Vector3(groesse.X - 1, hoehe, groesse.Y - 1), Material(Bruchstein), gruppe);
        Dach(new Vector3(0, hoehe, 0), new Vector3(groesse.X - 0.6f, 3, groesse.Y - 0.6f), Material(Schiefer), quer: true, gruppe);
        var kennung = "karte:" + g.Kennung;
        Klickflaeche(kennung, new Vector3(0, hoehe / 2, 0), new Vector3(groesse.X, hoehe, groesse.Y), gruppe);
        Beschriftung(g.Name, new Vector3(0, hoehe + 6, 0), 44, gruppe, kennung);
    }

    /// <summary>
    /// Die Kulissenhäuser der Karte: das Modell des Bausteins auf seiner Fläche, Variante und gedeckte Hausfarbe der Reihe nach wie bei den
    /// Einwohnerhäusern (<see cref="Hausfarben"/>). Einfache Einwohner wohnen zuerst hier (Stadtansicht.Wohnviertel.cs); ihre Haustür ist der
    /// Anker „Anker_Tuer“ des Modells.
    /// </summary>
    private void Kulissenhaeuser(Stadtkarte karte)
    {
        var i = 0;
        foreach (var g in karte.GebaeudeDerArt(Bausteinart.Kulisse))
        {
            if (g.Baustein?.Modell is not { } datei) continue;
            var kennung = "kulisse:" + g.Kennung;
            if (ModellEinsetzen(datei, this, Vector3.Zero, new ModellZustand(Variante: i % 3 + 1, Besitzer: Hausfarben[i % Hausfarben.Length]), kennung) is { } modell)
                modell.Wurzel.Transform = ModellLage(g);
            _kulissenTueren[g] = AufFlaeche(g, new Vector3(0, 0, GroesseVon(g).Y / 2)); // bis die Anker gelesen sind (nach dem Einhängen in die Szene): die Vorderkante
            i++;
        }
    }

    /// <summary>
    /// Ein Felsberg (hinter einer Grube): das Modell <c>berg_grube</c> an seiner Lage. Er ist viel größer als die Fläche der Grube (45 × 32 m, bis
    /// 16 m hoch); sein Maßstab verkleinert ihn, wo er sonst nicht neben die Nachbarn passt. Auf ihm wächst nichts (<see cref="Naturfreiraum"/>).
    /// </summary>
    private void BergBauen(Kartengebaeude g)
    {
        if (g.Baustein?.Modell is not { } datei) return;
        if (ModellEinsetzen(datei, this, Vector3.Zero, new ModellZustand()) is { } berg)
        {
            berg.Wurzel.Name = "Berg_" + g.Kennung;
            berg.Wurzel.Transform = ModellLage(g);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wege und Gassennetz
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet jeden Weg der Karte (Breite aus der Karte, sonst nach der Klasse) und legt ihn ins Gassennetz; Kreuzungen verbinden sich von selbst.
    /// Gerechnet wird mit dem Verlauf (<see cref="Kartenweg.Verlauf"/>): ohne <c>kurve</c> die Stützpunkte, mit <c>"glatt"</c> die weiche Kurve.
    /// </summary>
    private void WegeAnlegen(Stadtkarte karte)
    {
        for (var nummer = 0; nummer < karte.Wege.Count; nummer++)
        {
            var weg = karte.Wege[nummer];
            var punkte = weg.Verlauf.Where(p => p is { Length: 2 }).Select(Punkt3).ToArray();
            if (punkte.Length < 2) continue;
            var breite = (float)weg.BreiteWirksam;
            if (WegeschichtMoeglich) WegBelagZeichnen(nummer, punkte, breite, neuAuchGleich: true); // in den Boden eingebacken (Stadtansicht.Wegeschicht.cs, Wegbelag.cs)
            else WegZeichnen(punkte, breite, weg.IstGlatt, luecken: WegLuecken(nummer), boden: WegBoden(nummer)); // unter Brücken kein Band, in Furten eben (Stadtansicht.Wasser.Uebergaenge.cs)
            for (var i = 0; i + 1 < punkte.Length; i++) _wegStrecken.Add((punkte[i], punkte[i + 1], breite));
            _netz.Weg(weg.IstGlatt ? NetzPunkte(punkte) : punkte);
        }
    }

    /// <summary>
    /// Zeichnet einen Weg: gerade Strecken als je ein flacher Quader (wie seit jeher), eine glatte Kurve als ein Band (<see cref="WegBand"/>) –
    /// die vielen kurzen Stücke der Kurve als Quader lägen übereinander. Über den Marktplatz führt keiner (<see cref="OhneMarktplatz"/>).
    /// </summary>
    private void WegZeichnen(IReadOnlyList<Vector3> punkte, float breite, bool glatt, Node3D? eltern = null, List<(float Von, float Bis)>? luecken = null,
        Func<float, float, float>? boden = null)
    {
        if (luecken is { Count: > 0 }) // Brücken: Meter entlang des Weges ohne Band; die Läufe davor und dahinter wie ein Weg für sich
        {
            foreach (var teilweg in WegOhneLuecken(punkte, luecken)) WegZeichnen(teilweg, breite, glatt, eltern, boden: boden);
            return;
        }
        if (!glatt && !BodenBewegt) // auf Gelände wird jeder Weg ein Band, das dem Boden folgt (Quader lägen nur gerade im Raum)
        {
            for (var i = 0; i + 1 < punkte.Count; i++) WegZiehen(punkte[i], punkte[i + 1], breite, eltern);
            return;
        }
        var lauf = new List<Vector3>();
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            var (von, bis) = (punkte[i], punkte[i + 1]);
            if (OhneMarktplatz(von, bis, breite) is { } stuecke)
            {
                WegBand(lauf, breite, eltern ?? this, boden);
                lauf.Clear();
                foreach (var (a, b) in stuecke) WegStueck(a, b, breite, eltern ?? this);
                continue;
            }
            if (lauf.Count == 0) lauf.Add(von);
            lauf.Add(bis);
        }
        WegBand(lauf, breite, eltern ?? this, boden);
    }

    /// <summary>
    /// Die Punkte einer Kurve fürs Gassennetz: etwa alle 2 m einer (die Kurve ist alle Meter abgetastet; so viele Knoten bräuchte nur die
    /// Wegsuche, und Punkte unter <see cref="Gassennetz.Fang"/> legte das Netz ohnehin zusammen). Anfang und Ende bleiben.
    /// </summary>
    private static Vector3[] NetzPunkte(IReadOnlyList<Vector3> punkte)
    {
        var liste = new List<Vector3> { punkte[0] };
        for (var i = 1; i < punkte.Count - 1; i++)
            if (punkte[i].DistanceTo(liste[^1]) >= 2 && punkte[i].DistanceTo(punkte[^1]) >= 2) liste.Add(punkte[i]);
        liste.Add(punkte[^1]);
        return liste.ToArray();
    }

    /// <summary>
    /// Hängt die festen Ziele an das Netz: die Mitte des Marktplatzes, die Tore, die Türen von Rathaus und Dom, die Höfe der Werkstattplätze, die
    /// Karrenplätze der Umlandbetriebe und die Kulissenhäuser. Die Arbeitsstellen hängt <see cref="UmlandstellenBauen"/> an.
    /// </summary>
    private void Anschliessen(Stadtkarte karte)
    {
        // Ein Marktplatz ist eine Kreuzung (wie im Kern, Karrennetz.Marktlinien): Jede Karrenstraße, die sein Pflaster berührt, ist über den Platz mit
        // seiner Mitte verbunden – so sind Straßen, die am Rand enden, verbunden, und die Karren finden denselben Weg wie die Fahrzeit des Kerns.
        foreach (var linie in Karrennetz.Marktlinien(karte))
        {
            var (von, mitte) = (new Vector3((float)linie.Linie[0].X, 0, (float)linie.Linie[0].Z), new Vector3((float)linie.Linie[1].X, 0, (float)linie.Linie[1].Z));
            AnWegAnschliessen(von, ueberDenMarkt: false);
            _netz.Weg(von, mitte);
        }
        foreach (var platz in _maerkte)
            if (platz.Halb > 0 && MarktKnoten(platz) < 0)
                AnWegAnschliessen(platz.Mitte, ueberDenMarkt: false);
        foreach (var tor in _tore) AnWegAnschliessen(tor.Ort);
        if (TuerAnker("Rathaus") is { } rathaus) _rathausTuer = _netz.Knoten[AnWegAnschliessen(rathaus)];
        if (TuerAnker("Dom") is { } dom) _domTuer = _netz.Knoten[AnWegAnschliessen(dom)];
        foreach (var platz in karte.GebaeudeDerArt(Bausteinart.Werkstattplatz))
            AnWegAnschliessen(HofAuf(platz), HofwegBreite);
        foreach (var platz in karte.GebaeudeDerArt(Bausteinart.Umlandbetrieb))
            if (UmlandHof(platz) is { } hof) AnWegAnschliessen(hof, KarrenwegBreite);
        foreach (var kulisse in _kulissenTueren.Keys.ToList())
        {
            var tuer = TuerAnker("kulisse:" + kulisse.Kennung) ?? _kulissenTueren[kulisse];
            _kulissenTueren[kulisse] = _netz.Knoten[AnWegAnschliessen(tuer)];
        }
    }

    /// <summary>Der Knoten des Gassennetzes in der Mitte des Marktplatzes (ohne Angabe der erste) – −1, wenn kein Weg dorthin führt.</summary>
    private int MarktKnoten(MarktplatzAnsicht? platz = null)
    {
        var mitte = platz?.Mitte ?? _markt;
        for (var i = 0; i < _netz.Knoten.Count; i++)
            if (_netz.Knoten[i].DistanceTo(mitte) < Gassennetz.Fang) return i;
        return -1;
    }

    /// <summary>Der Hof einer Werkstattfläche: im Vorplatz, <see cref="HofVorKante"/> vor der Vorderkante – dort beginnt die Gasse, dort kommen Gäste an.</summary>
    private static Vector3 HofAuf(Kartengebaeude platz) => AufFlaeche(platz, new Vector3(0, 0, GroesseVon(platz).Y / 2 - HofVorKante));

    /// <summary>
    /// Hängt einen Punkt (Tür, Hof, Karrenplatz, Gatter, Tor) mit einem geraden Stück an den nächsten Weg der Karte und gibt seinen Knoten im Netz
    /// zurück. Liegt der Marktplatz näher als jeder Weg (die Markthalle des Rathauses), führt das Stück über das Pflaster zur Mitte. Mit
    /// <paramref name="zeichnen"/> wird das Stück als Weg dieser Breite gezeichnet (Höfe, Karrenplätze, Gatter), sonst nur ins Netz gelegt (Haustüren).
    /// </summary>
    private int AnWegAnschliessen(Vector3 p, float? zeichnen = null, bool ueberDenMarkt = true)
    {
        p.Y = 0;
        var (naechster, abstand) = NaechsterWegpunkt(p);
        if (ueberDenMarkt && NaechsterMarkt(p) is { } markt && markt.Abstand(p) < abstand)
        {
            _netz.Weg(p, markt.Mitte);
            return _netz.KnotenBei(p);
        }
        if (naechster is not { } q)
            return _netz.KnotenBei(p); // eine Karte ohne Wege: ein Knoten für sich
        _netz.Weg(p, q);
        if (zeichnen is { } breite && abstand > 0.3f) WegZiehen(p, q, breite);
        return _netz.KnotenBei(p);
    }

    /// <summary>Der nächste Punkt auf einem Weg der Karte und sein Abstand (null ohne Wege).</summary>
    private (Vector3? Punkt, float Abstand) NaechsterWegpunkt(Vector3 p)
    {
        Vector3? beste = null;
        var abstand = float.MaxValue;
        foreach (var (von, bis, _) in _wegStrecken)
        {
            var q = Geometry3D.GetClosestPointToSegment(p, von, bis);
            var d = q.DistanceTo(p);
            if (d < abstand) { abstand = d; beste = q; }
        }
        return (beste, abstand);
    }

    /// <summary>Abstand eines Punktes zum Pflaster des nächsten Marktplatzes (0 auf dem Pflaster; ohne Marktplatz ein sehr großer Wert).</summary>
    private float AbstandZumPflaster(Vector3 p) => NaechsterMarkt(p)?.Abstand(p) ?? float.MaxValue;

    /// <summary>Der Marktplatz, dessen Pflaster dem Punkt am nächsten liegt (null ohne Marktplatz); bei gleichem Abstand der erste.</summary>
    private MarktplatzAnsicht? NaechsterMarkt(Vector3 p)
    {
        MarktplatzAnsicht? beste = null;
        var abstand = float.MaxValue;
        foreach (var m in _maerkte)
        {
            if (m.Halb <= 0) continue;
            var d = m.Abstand(p);
            if (d < abstand) (beste, abstand) = (m, d);
        }
        return beste;
    }

    /// <summary>Ein Punkt in Koordinaten des (ersten) Marktplatzes (Mitte im Ursprung, Kanten parallel zu den Achsen, in Metern der Stadt).</summary>
    private Vector3 MarktLokal(Vector3 p)
    {
        var massstab = _marktLage.Basis.Scale.X;
        return _marktLage.AffineInverse() * p * massstab;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Der Blick beim Start: aus der Karte (<c>kamera</c>), sonst auf die Mitte aller Gebäude mit dem gewohnten Zoom. Die Drehung der Karte
    /// kommt zur gewohnten Blickrichtung (nach Nordosten) dazu.
    /// </summary>
    private void KameraAusKarte(Stadtkarte karte)
    {
        if (karte.Kamera is { } k)
        {
            _kameraDrehung.Position = Punkt3(k.Punkt);
            if (k.Zoom > 0) _kamera.Size = Mathf.Clamp((float)k.Zoom, KameraMinZoom, KameraGroessterZoom());
            _zielDrehung += (float)k.Drehung;
            _kameraDrehung.RotationDegrees = new Vector3(0, _zielDrehung, 0);
            KameraHoeheSofort();
            return;
        }
        var gebaeude = karte.Gebaeude.Select(g => Punkt2(g.Punkt)).ToList();
        if (gebaeude.Count == 0) return;
        var rahmen = new Rect2(gebaeude[0], Vector2.Zero);
        foreach (var p in gebaeude) rahmen = rahmen.Expand(p);
        _kameraDrehung.Position = new Vector3(rahmen.GetCenter().X, 0, rahmen.GetCenter().Y);
        KameraHoeheSofort();
    }
}
