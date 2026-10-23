using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Brücken, Furten, Fähren und Häfen – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5c). Der Kern sagt aus dem
/// <see cref="Wasserplan"/>, wo ein Weg ein Gewässer quert (<see cref="Wasserquerung"/>) und wo eine Fähre fährt (<see cref="Faehrverbindung"/>); die Modelle in
/// <c>assets/modelle/wasser/</c> sind Bausteine, die hier gereiht werden (Maße und Anschlüsse: <c>docs/bereiche/darstellung.md</c>):
/// <list type="bullet">
/// <item><b>Holzbrücke</b> (bis 20 m): Endstück – Joche zu 4 m (je mit zwei Geländern) – Endstück; die Joche sind in Längsrichtung gestreckt, damit sie die Weite genau füllen.
/// <b>Steinbrücke</b> (darüber): Rampe – Bogen (8 m, gestreckt, mit zwei Brüstungen) – Pfeiler (2 m, nie gedreht: der Eisbrecher zeigt stromauf) – … – Rampe.
/// Das Deck liegt auf <see cref="Wasserquerung.Deckhoehe"/>; die Endstücke und Rampen werden in der Höhe so gestreckt, dass sie unten auf dem Gelände am Weg landen.
/// Stromauf ist beim Modell −Z: Die Brücke wird so gedreht, dass das stimmt.</item>
/// <item>Wo eine Brücke steht, zeichnet der Weg kein Band (<see cref="WegLuecken"/>).</item>
/// <item><b>Furt</b>: Trittsteine bzw. Schotter oder gesetzte Steine, auf dem Wasserspiegel, 4 m lang gereiht.</item>
/// <item><b>Fähre</b>: zwei Anlegestege, der Prahm und das Fährseil von den Pfosten zur Fähre. Die Fähre pendelt; kommt ein Karren zur Fährstelle, kommt sie zu
/// ihm und fährt mit ihm hinüber (<see cref="FaehrenBewegen"/>).</item>
/// <item><b>Fahrflächen</b> (<see cref="FussHoehe(float, float)"/>): Auf Brücken, Stegen und der Fähre stehen Figuren und Karren auf dem Deck, nicht im Wasser.</item>
/// <item><b>Hafen</b>: Das Tretrad des Hafenmodells dreht sich langsam, die Kähne sitzen auf dem Wasserspiegel (<see cref="HafenBelebt"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Modelle der Bausteine
    // ═════════════════════════════════════════════════════════════════════════

    private Node3D? _wasserVorlagen;
    private readonly Dictionary<string, Node3D?> _wasserVorlagenCache = new();

    /// <summary>Die Vorlage eines Wasser-Modells (<c>wasser/&lt;datei&gt;</c>): einmal eingesetzt und unsichtbar – aus ihr kommen die Kopien der Bausteine.</summary>
    private Node3D? WasserVorlage(string datei)
    {
        if (_wasserVorlagenCache.TryGetValue(datei, out var vorlage) && (vorlage is null || IsInstanceValid(vorlage))) return vorlage;
        if (_wasserVorlagen is null || !IsInstanceValid(_wasserVorlagen))
        {
            _wasserVorlagen = new Node3D { Name = "WasserVorlagen", Visible = false };
            AddChild(_wasserVorlagen);
        }
        return _wasserVorlagenCache[datei] = ModellEinsetzen("wasser/" + datei, _wasserVorlagen, Vector3.Zero, new ModellZustand())?.Wurzel;
    }

    /// <summary>Eine Kopie eines Bausteins (Knoten <paramref name="knoten"/> der Datei) an die Lage im Elternknoten; null, wenn es ihn nicht gibt.</summary>
    private Node3D? WasserTeil(string datei, string knoten, Node3D eltern, Transform3D lage)
    {
        if (WasserVorlage(datei)?.FindChild(knoten, true, false) is not Node3D teil) return null;
        var kopie = (Node3D)teil.Duplicate();
        eltern.AddChild(kopie);
        kopie.Transform = lage;
        return kopie;
    }

    /// <summary>Ort (x, z), Höhe, Drehung um die Hochachse (Bogenmaß) und Streckung entlang der eigenen Achsen.</summary>
    private static Transform3D BausteinLage(Vector2 ort, float y, float drehung, Vector3? streckung = null) =>
        new(new Basis(Vector3.Up, drehung) * Basis.FromScale(streckung ?? Vector3.One), new Vector3(ort.X, y, ort.Y));

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau aus dem Wasserplan
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Holz: so weit (Meter) reicht das Endstück hinter dem Anfang der Brücke ins Land, Stein ebenso (die Rampe).</summary>
    private const float HolzLand = 3f, SteinLand = 9f;

    private void UebergaengeVergessen()
    {
        _fahrflaechen.Clear();
        _faehren.Clear();
    }

    /// <summary>Hängt die Fähren der Karte als Kanten ans Gassennetz: Nur Karren fahren darüber (<see cref="Gassennetz.Pfad"/> mit <c>faehren</c>), nie Passanten.</summary>
    private void FaehrenAnsNetz()
    {
        if (Uebergangsplan is not { } plan) return;
        foreach (var f in plan.Faehren)
            if (f.UeberWasser > 1) _netz.Faehre(new Vector3((float)f.Von.X, 0, (float)f.Von.Z), new Vector3((float)f.Bis.X, 0, (float)f.Bis.Z));
    }

    private void UebergaengeBauen(Node3D eltern, Wasserplan plan)
    {
        var form = GewaesserForm!;
        foreach (var q in plan.Querungen)
        {
            if (q.IstBruecke) BrueckeBauen(eltern, q, form);
            else FurtBauen(eltern, q, form);
        }
        foreach (var f in plan.Faehren) FaehreBauen(eltern, f, form);
    }

    /// <summary>
    /// Die Stellen (Meter entlang des Verlaufs), an denen Weg <paramref name="weg"/> Brücke ist: Dort zeichnet <see cref="WegZeichnen"/> kein Band, auch nicht unter den
    /// Endstücken und Rampen, die ein Stück ins Land reichen.
    /// </summary>
    private List<(float Von, float Bis)> WegLuecken(int weg)
    {
        var luecken = new List<(float, float)>();
        if (Uebergangsplan is not { } plan) return luecken;
        foreach (var q in plan.Querungen)
        {
            if (q.Weg != weg || !q.IstBruecke) continue;
            var land = q.Art == Uebergangsart.Holzbruecke ? HolzLand : SteinLand;
            luecken.Add(((float)q.VonStelle - land, (float)q.BisStelle + land));
        }
        return luecken;
    }

    /// <summary>Der Weg ohne die Stücke in den Lücken: lauter zusammenhängende Läufe (an den Enden der Lücken genau abgeschnitten).</summary>
    private static List<List<Vector3>> WegOhneLuecken(IReadOnlyList<Vector3> punkte, List<(float Von, float Bis)> luecken)
    {
        var laeufe = new List<List<Vector3>>();
        List<Vector3>? lauf = null;
        var s = 0f;
        bool Drin(float x) => luecken.Any(l => x > l.Von && x < l.Bis);
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            var (a, b) = (punkte[i], punkte[i + 1]);
            var laenge = a.DistanceTo(b);
            var grenzen = new List<float> { s };
            grenzen.AddRange(luecken.SelectMany(l => new[] { l.Von, l.Bis }).Where(x => x > s && x < s + laenge).OrderBy(x => x));
            grenzen.Add(s + laenge);
            for (var k = 0; k + 1 < grenzen.Count; k++)
            {
                if (Drin((grenzen[k] + grenzen[k + 1]) / 2)) { lauf = null; continue; }
                if (grenzen[k + 1] - grenzen[k] < 1e-4f) continue;
                var t0 = laenge > 0 ? (grenzen[k] - s) / laenge : 0;
                var t1 = laenge > 0 ? (grenzen[k + 1] - s) / laenge : 1;
                if (lauf is null)
                {
                    lauf = new List<Vector3> { a.Lerp(b, t0) };
                    laeufe.Add(lauf);
                }
                lauf.Add(a.Lerp(b, t1));
            }
            s += laenge;
        }
        return laeufe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fahrflächen: Brücken, Stege und die Fähre tragen Figuren und Karren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Streifen, auf dem Figuren und Karren höher stehen als der Boden: ab <see cref="Start"/> in <see cref="Richtung"/> <see cref="Laenge"/> Meter, auf jeder Seite
    /// <see cref="Halb"/> Meter. <see cref="Hoehe"/> gibt die Höhe bei der Stelle u (Meter ab Start); null: dort gilt der Boden.
    /// </summary>
    private sealed class Fahrflaeche
    {
        public required Vector2 Start { get; init; }
        public required Vector2 Richtung { get; init; }
        public required float Laenge { get; init; }
        public required float Halb { get; init; }
        public required Func<float, float?> Hoehe { get; init; }
        public required Rect2 Rahmen { get; init; }

        public float? HoeheBei(Vector2 p)
        {
            if (!Rahmen.HasPoint(p)) return null;
            var d = p - Start;
            var u = d.Dot(Richtung);
            if (u < 0 || u > Laenge || Mathf.Abs(d.Dot(new Vector2(-Richtung.Y, Richtung.X))) > Halb) return null;
            return Hoehe(u);
        }
    }

    private readonly List<Fahrflaeche> _fahrflaechen = new();

    private void FahrflaecheMerken(Vector2 start, Vector2 richtung, float laenge, float halb, Func<float, float?> hoehe)
    {
        var quer = new Vector2(-richtung.Y, richtung.X) * halb;
        var ende = start + richtung * laenge;
        var rahmen = new Rect2(start + quer, Vector2.Zero).Expand(start - quer).Expand(ende + quer).Expand(ende - quer);
        _fahrflaechen.Add(new Fahrflaeche { Start = start, Richtung = richtung, Laenge = laenge, Halb = halb, Hoehe = hoehe, Rahmen = rahmen });
    }

    /// <summary>Die Höhe eines Decks (Brücke, Steg, Fähre) an der Stelle – null, wenn dort keins ist.</summary>
    private float? DeckHoehe(float x, float z)
    {
        if (_fahrflaechen.Count == 0) return null;
        var p = new Vector2(x, z);
        foreach (var f in _fahrflaechen)
            if (f.HoeheBei(p) is { } h) return h;
        return null;
    }

    /// <summary>Wo Figuren und Karren stehen und fahren: das Deck, wenn eins da ist, sonst der Boden (für das Nicken der Karren).</summary>
    private float FahrHoehe(float x, float z) => DeckHoehe(x, z) ?? BodenHoehe(x, z);

    // ═════════════════════════════════════════════════════════════════════════
    //  Brücken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Stromab zeigt beim Modell +Z: Dreht die Achse (a → b, Einheitsrichtung d) so, dass das stimmt – sonst tauschen Anfang und Ende. Gibt die Drehung des Modells zurück.</summary>
    private static float StromabDrehung(Wasserform form, ref Vector2 a, ref Vector2 b, ref Vector2 d, ref float hA, ref float hB)
    {
        var phi = Mathf.Atan2(-d.Y, d.X); // lokal +X zeigt dann nach d
        var mitte = (a + b) / 2;
        var (sx, sz, tempo) = form.Stroemung(mitte.X, mitte.Y);
        if (tempo > 0.01 && Mathf.Sin(phi) * sx + Mathf.Cos(phi) * sz < 0)
        {
            (a, b) = (b, a);
            (hA, hB) = (hB, hA);
            d = -d;
            phi = Mathf.Atan2(-d.Y, d.X);
        }
        return phi;
    }

    private void BrueckeBauen(Node3D eltern, Wasserquerung q, Wasserform form)
    {
        var holz = q.Art == Uebergangsart.Holzbruecke;
        var (a, b) = (V2(q.Anfang), V2(q.Ende));
        var laenge = a.DistanceTo(b);
        if (laenge < 2.5f) return;
        var d = (b - a) / laenge;
        float hA = (float)q.AnfangHoehe, hB = (float)q.EndeHoehe;
        var phi = StromabDrehung(form, ref a, ref b, ref d, ref hA, ref hB);
        var deck = (float)q.Deckhoehe;
        var weite = laenge - 2f; // zwischen den Uferkanten (je 1 m Widerlager davor)
        var uferA = a + d;
        var uferB = b - d;
        var gruppe = new Node3D { Name = (holz ? "Holzbruecke_" : "Steinbruecke_") + q.GewaesserName };
        eltern.AddChild(gruppe);

        if (holz)
        {
            const string datei = "wasser_holzbruecke";
            const float hoehe = 0.6f; // Deck des Modells über der Uferkante
            var felder = Math.Max(1, Mathf.RoundToInt(weite / 4f));
            var s = weite / (4f * felder);
            WasserTeil(datei, "Holz_Ende", gruppe, BausteinLage(uferA, EndeUnten(deck, hA, hoehe, out var syA), phi, new Vector3(1, syA, 1)));
            WasserTeil(datei, "Holz_Ende", gruppe, BausteinLage(uferB, EndeUnten(deck, hB, hoehe, out var syB), phi + Mathf.Pi, new Vector3(1, syB, 1)));
            for (var i = 0; i < felder; i++)
            {
                var mitte = uferA + d * (weite * (i + 0.5f) / felder);
                var streckung = new Vector3(s, 1, 1);
                WasserTeil(datei, "Holz_Joch", gruppe, BausteinLage(mitte, deck - hoehe, phi, streckung));
                WasserTeil(datei, "Holz_Gelaender", gruppe, BausteinLage(mitte, deck - hoehe, phi, streckung));
                WasserTeil(datei, "Holz_Gelaender", gruppe, BausteinLage(mitte, deck - hoehe, phi + Mathf.Pi, streckung));
            }
            FahrflaecheBruecke(a, d, laenge, q, deck, hA, hB, flachVon: 0, rampe: HolzLand);
        }
        else
        {
            const string datei = "wasser_steinbruecke";
            const float hoehe = 1.4f;
            var felder = Math.Max(1, Mathf.RoundToInt((weite + 2f) / 10f));
            var s = Math.Max(0.4f, (weite - 2f * (felder - 1)) / (8f * felder));
            WasserTeil(datei, "Stein_Rampe", gruppe, BausteinLage(uferA, EndeUnten(deck, hA, hoehe, out var syA), phi, new Vector3(1, syA, 1)));
            WasserTeil(datei, "Stein_Rampe", gruppe, BausteinLage(uferB, EndeUnten(deck, hB, hoehe, out var syB), phi + Mathf.Pi, new Vector3(1, syB, 1)));
            for (var i = 0; i < felder; i++)
            {
                var mitte = uferA + d * (8f * s * (i + 0.5f) + 2f * i);
                var streckung = new Vector3(s, 1, 1);
                WasserTeil(datei, "Stein_Bogen", gruppe, BausteinLage(mitte, deck - hoehe, phi, streckung));
                WasserTeil(datei, "Stein_Bruestung", gruppe, BausteinLage(mitte, deck - hoehe, phi, streckung));
                WasserTeil(datei, "Stein_Bruestung", gruppe, BausteinLage(mitte, deck - hoehe, phi + Mathf.Pi, streckung));
                if (i + 1 < felder)
                    WasserTeil(datei, "Stein_Pfeiler", gruppe, BausteinLage(uferA + d * (8f * s * (i + 1) + 2f * i + 1f), deck - hoehe, phi)); // nie gedreht: Eisbrecher stromauf
            }
            FahrflaecheBruecke(a, d, laenge, q, deck, hA, hB, flachVon: -1, rampe: SteinLand - 1);
        }
    }

    /// <summary>
    /// Wo ein Endstück oder eine Rampe steht: Es hat oben die Höhe <paramref name="hoehe"/> des Modells über seinem Ursprung und unten (am Land) 0. Damit es unten auf dem
    /// Gelände <paramref name="land"/> landet und oben das Deck trifft, wird es mit <paramref name="streckung"/> in der Höhe gestreckt; gibt die Höhe des Ursprungs zurück.
    /// </summary>
    private static float EndeUnten(float deck, float land, float hoehe, out float streckung)
    {
        streckung = Mathf.Clamp((deck - land) / hoehe, 0.15f, 8f);
        return deck - hoehe * streckung;
    }

    /// <summary>Das Deck einer Brücke als Fahrfläche: eben von <paramref name="flachVon"/> bis zum Ende davor, dann je eine Rampe der Länge <paramref name="rampe"/> hinab zum Weg.</summary>
    private void FahrflaecheBruecke(Vector2 start, Vector2 d, float laenge, Wasserquerung q, float deck, float hA, float hB, float flachVon, float rampe)
    {
        var land = rampe - flachVon; // so weit reicht die Rampe vor den Anfang
        var halb = Mathf.Max(2.2f, (float)q.Breite / 2 + 0.6f);
        float? Hoehe(float u)
        {
            u -= land; // u zählt jetzt ab dem Anfang der Brücke (die Fläche beginnt eine Rampe davor)
            if (u < flachVon) return Mathf.Lerp(hA, deck, Mathf.Clamp((u - (flachVon - rampe)) / rampe, 0, 1));
            if (u > laenge - flachVon) return Mathf.Lerp(hB, deck, Mathf.Clamp((laenge - flachVon + rampe - u) / rampe, 0, 1));
            return deck;
        }
        FahrflaecheMerken(start - d * land, d, laenge + 2 * land, halb, Hoehe);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Furten
    // ═════════════════════════════════════════════════════════════════════════

    private void FurtBauen(Node3D eltern, Wasserquerung q, Wasserform form)
    {
        var (a, b) = (V2(q.Anfang), V2(q.Ende));
        var laenge = a.DistanceTo(b);
        if (laenge < 0.5f) return;
        var d = (b - a) / laenge;
        float h0 = 0, h1 = 0;
        var phi = StromabDrehung(form, ref a, ref b, ref d, ref h0, ref h1);
        var gruppe = new Node3D { Name = "Furt_" + q.GewaesserName };
        eltern.AddChild(gruppe);
        var stuecke = Math.Max(1, Mathf.RoundToInt(laenge / 4f));
        var s = laenge / (4f * stuecke);
        for (var i = 0; i < stuecke; i++)
        {
            var mitte = a + d * (laenge * (i + 0.5f) / stuecke);
            var knoten = q.Karren ? (i % 2 == 0 ? "Furt3" : "Furt2") : "Furt1"; // Karren: gesetzte Steine und Schotterbänke, zu Fuß: Trittsteine
            WasserTeil("wasser_steine", knoten, gruppe, BausteinLage(mitte, (float)(form.Spiegel(mitte.X, mitte.Y) ?? q.Spiegel), phi, new Vector3(s, 1, 1)));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fähren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Eine Fährstelle in der Welt: der Prahm und das, was er zum Pendeln braucht (Lage der Anleger auf der Strecke von <see cref="Von"/> nach Bis, in Metern).</summary>
    private sealed class FaehreAnsicht
    {
        public required Node3D Wurzel { get; init; }
        public required Vector2 Von { get; init; }
        public required Vector2 Richtung { get; init; }
        public required float Laenge { get; init; }
        public required float Spiegel { get; init; }

        /// <summary>Wo der Prahm festmacht (Mitte, Meter ab Von) am ersten und am zweiten Ufer.</summary>
        public required float AnlegeA { get; init; }
        public required float AnlegeB { get; init; }

        public required Node3D? SeilAnkerA { get; init; }
        public required Node3D? SeilAnkerB { get; init; }
        public required Vector3 PfostenA { get; init; }
        public required Vector3 PfostenB { get; init; }
        public required MeshInstance3D SeilA { get; init; }
        public required MeshInstance3D SeilB { get; init; }

        /// <summary>Wo der Prahm gerade ist (Meter ab Von) und wohin er ohne Karren fährt.</summary>
        public float Stelle { get; set; }
        public float Ziel { get; set; }
        public float Ruhe { get; set; }
    }

    private readonly List<FaehreAnsicht> _faehren = new();

    private static readonly Color Seilfarbe = new(0.55f, 0.48f, 0.35f);

    private void FaehreBauen(Node3D eltern, Faehrverbindung f, Wasserform form)
    {
        var (von, bis) = (V2(f.Von), V2(f.Bis));
        var laenge = von.DistanceTo(bis);
        if (laenge < 6f) return;
        var d = (bis - von) / laenge;

        // wo das Wasser beginnt und endet (u in Metern ab Von)
        float wA = -1, wB = -1;
        for (var u = 0f; u <= laenge; u += 0.25f)
            if (form.IstWasser(von.X + d.X * u, von.Y + d.Y * u))
            {
                if (wA < 0) wA = u;
                wB = u;
            }
        if (wA < 0 || wB - wA < 8f) return; // kein Wasser dazwischen (die Prüfung meldet es)

        var spiegel = (float)f.Spiegel;
        var steghoehe = spiegel + 0.75f; // Oberkante am Land; am Ende des Stegs (7 m weiter, 0,5 m tiefer) liegt sie 0,3 m über dem Spiegel: auf Höhe des Prahms
        float Ursprung(float kante, float vorzeichen) // auf dem Ufer, wo der Boden etwa die Höhe des Stegs erreicht (1,5 bis 8 m landeinwärts)
        {
            var u = kante;
            for (var abstand = 1.5f; abstand <= 8f; abstand += 0.5f)
            {
                u = kante - vorzeichen * abstand;
                if (BodenHoehe(von.X + d.X * u, von.Y + d.Y * u) >= steghoehe - 0.1f) break;
            }
            return Mathf.Clamp(u, 0, laenge);
        }
        var uA = Ursprung(wA, 1);
        var uB = Ursprung(wB, -1);
        var phi = Mathf.Atan2(-d.Y, d.X);

        var gruppe = new Node3D { Name = "Faehre_" + f.Name };
        eltern.AddChild(gruppe);
        var stegA = WasserTeil("wasser_faehre", "Anlegesteg", gruppe, BausteinLage(von + d * uA, steghoehe, phi));
        var stegB = WasserTeil("wasser_faehre", "Anlegesteg", gruppe, BausteinLage(von + d * uB, steghoehe, phi + Mathf.Pi));

        // der Prahm: +Z stromab (die Seilführungen liegen dann stromauf)
        var (sx, sz, tempo) = form.Stroemung((von.X + bis.X) / 2, (von.Y + bis.Y) / 2);
        var phiF = tempo > 0.01 && Mathf.Sin(phi) * sx + Mathf.Cos(phi) * sz < 0 ? phi + Mathf.Pi : phi;
        var anlegeA = uA + 11.7f; // Steg 6 m + Mitte des Prahms 5,7 m dahinter
        var anlegeB = uB - 11.7f;
        var prahm = WasserTeil("wasser_faehre", "Faehre", gruppe, BausteinLage(von + d * anlegeA, spiegel, phiF));
        if (stegA is null || stegB is null || prahm is null) return;

        // Pfosten: der stromauf liegende jedes Stegs (in Koordinaten der Stadt; der Steg steht in einer Gruppe ohne eigene Lage)
        Vector3 Pfosten(Node3D steg)
        {
            Vector3 Lage(string name) => steg.Transform * ((steg.FindChild(name, true, false) as Node3D)?.Position ?? Vector3.Zero);
            var (p1, p2) = (Lage("Anker_Pfosten_A"), Lage("Anker_Pfosten_B"));
            return p1.X * sx + p1.Z * sz <= p2.X * sx + p2.Z * sz ? p1 : p2;
        }
        var seilStoff = Stoff(Seilfarbe);
        MeshInstance3D Seil(string name)
        {
            var seil = new MeshInstance3D
            {
                Name = name, Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 1f, RadialSegments = 5, Rings = 1 },
                MaterialOverride = seilStoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            gruppe.AddChild(seil);
            return seil;
        }
        var ansicht = new FaehreAnsicht
        {
            Wurzel = prahm, Von = von, Richtung = d, Laenge = laenge, Spiegel = spiegel, AnlegeA = anlegeA, AnlegeB = anlegeB,
            SeilAnkerA = prahm.FindChild("Anker_Seil_A", true, false) as Node3D, SeilAnkerB = prahm.FindChild("Anker_Seil_B", true, false) as Node3D,
            PfostenA = Pfosten(stegA), PfostenB = Pfosten(stegB), SeilA = Seil("SeilA"), SeilB = Seil("SeilB"),
            Stelle = anlegeA, Ziel = anlegeA, Ruhe = 0,
        };
        _faehren.Add(ansicht);
        FaehreSeilSetzen(ansicht);

        // Stege und Prahm tragen: Steg 7 m lang (Oberkante fällt um 0,5 m), dazwischen das Deck des Prahms
        float? Deck(float u)
        {
            float Steg(float x) => steghoehe + 0.05f - 0.5f * (x + 1) / 7f;
            if (u >= uA - 1 && u <= uA + 6) return Steg(u - uA);
            if (u >= uB - 6 && u <= uB + 1) return Steg(uB - u);
            if (u > uA + 6 && u < uB - 6) return spiegel + 0.3f;
            return null;
        }
        FahrflaecheMerken(von + d * (uA - 1), d, uB - uA + 2, 2.3f, u => Deck(u + uA - 1));
    }

    /// <summary>Jedes Bild: Die Fähre pendelt von selbst. Kommt ein Karren zur Fährstelle, kommt sie zu seinem Ufer und fährt mit ihm hinüber.</summary>
    private void FaehrenBewegen(double delta)
    {
        foreach (var f in _faehren)
        {
            if (!IsInstanceValid(f.Wurzel)) continue;
            float? ziel = null;
            var mitfahrt = false;
            var quer = new Vector2(-f.Richtung.Y, f.Richtung.X);
            foreach (var k in _karren.Values)
            {
                var rel = new Vector2(k.Wurzel.Position.X, k.Wurzel.Position.Z) - f.Von;
                var u = rel.Dot(f.Richtung);
                if (u < -2 || u > f.Laenge + 2 || Mathf.Abs(rel.Dot(quer)) > 3f) continue;
                if (u > f.AnlegeA && u < f.AnlegeB) { ziel = u; mitfahrt = true; break; }
                ziel = u <= f.AnlegeA ? f.AnlegeA : f.AnlegeB;
            }
            if (ziel is null)
            {
                // ohne Karren: alle 40 Sekunden zum anderen Ufer, langsam
                f.Ruhe += (float)delta;
                if (f.Ruhe > 40f)
                {
                    f.Ruhe = 0;
                    f.Ziel = Mathf.IsEqualApprox(f.Ziel, f.AnlegeA) ? f.AnlegeB : f.AnlegeA;
                }
                f.Stelle = Mathf.MoveToward(f.Stelle, f.Ziel, 1.6f * (float)delta);
            }
            else if (mitfahrt) (f.Stelle, f.Ziel) = (ziel.Value, ziel.Value);
            else
            {
                f.Ziel = ziel.Value;
                f.Ruhe = 0;
                f.Stelle = Mathf.MoveToward(f.Stelle, ziel.Value, 8f * (float)delta);
            }
            var ort = f.Von + f.Richtung * f.Stelle;
            f.Wurzel.Position = new Vector3(ort.X, f.Spiegel, ort.Y);
            FaehreSeilSetzen(f);
        }
    }

    /// <summary>Die beiden Seile von den Pfosten der Stege zu den Seilführungen des Prahms (die zum nächsten Pfosten gehört).</summary>
    private static void FaehreSeilSetzen(FaehreAnsicht f)
    {
        if (f.SeilAnkerA is null || f.SeilAnkerB is null) return;
        var (p, q) = (f.Wurzel.Transform * f.SeilAnkerA.Position, f.Wurzel.Transform * f.SeilAnkerB.Position);
        var (nahA, nahB) = f.PfostenA.DistanceSquaredTo(p) <= f.PfostenA.DistanceSquaredTo(q) ? (p, q) : (q, p);
        SeilZiehen(f.SeilA, f.PfostenA, nahA);
        SeilZiehen(f.SeilB, f.PfostenB, nahB);
    }

    private static void SeilZiehen(MeshInstance3D seil, Vector3 von, Vector3 bis)
    {
        var strecke = bis - von;
        var laenge = strecke.Length();
        if (laenge < 0.05f) return;
        seil.Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up, strecke / laenge)) * Basis.FromScale(new Vector3(1, laenge, 1)), (von + bis) / 2);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hafen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Hafenmodell in der Welt: das Tretrad (dreht sich), die Kähne (sitzen auf dem Wasserspiegel).</summary>
    private sealed class HafenAnsicht
    {
        public required Node3D Wurzel { get; init; }
        public Node3D? Tretrad { get; init; }
        public Node3D[] Kaehne { get; init; } = Array.Empty<Node3D>();
        public float[] KahnHoehen { get; init; } = Array.Empty<float>();
        public bool Gesetzt { get; set; }
    }

    private readonly List<HafenAnsicht> _haefen = new();

    /// <summary>Wird von <see cref="ModellEinsetzen"/> gerufen, wenn das Modell ein Tretrad hat (der Hafen).</summary>
    private void HafenBelebt(Node3D wurzel)
    {
        var kaehne = new[] { "Kahn1", "Kahn2" }.Select(n => wurzel.FindChild(n, true, false) as Node3D).Where(k => k is not null).Select(k => k!).ToArray();
        _haefen.Add(new HafenAnsicht
        {
            Wurzel = wurzel, Tretrad = wurzel.FindChild("Tretrad", true, false) as Node3D, Kaehne = kaehne, KahnHoehen = kaehne.Select(k => k.Position.Y).ToArray(),
        });
    }

    private double _wasserZeit;

    /// <summary>Jedes Bild: das Tretrad der Häfen dreht sich langsam, die Kähne wiegen sich; beim ersten Mal setzen sie sich auf den Spiegel.</summary>
    private void HaefenBewegen(double delta)
    {
        _wasserZeit += delta;
        _haefen.RemoveAll(h => !IsInstanceValid(h.Wurzel));
        foreach (var h in _haefen)
        {
            if (!h.Wurzel.IsInsideTree()) continue;
            if (h.Tretrad is not null) h.Tretrad.Rotation = new Vector3((float)(_wasserZeit * 0.35), h.Tretrad.Rotation.Y, h.Tretrad.Rotation.Z);
            for (var i = 0; i < h.Kaehne.Length; i++)
            {
                var kahn = h.Kaehne[i];
                if (!h.Gesetzt && GewaesserForm?.Spiegel(kahn.GlobalPosition.X, kahn.GlobalPosition.Z) is { } spiegel)
                    h.KahnHoehen[i] = (float)spiegel - h.Wurzel.GlobalPosition.Y;
                kahn.Position = new Vector3(kahn.Position.X, h.KahnHoehen[i] + 0.04f * Mathf.Sin((float)_wasserZeit * 0.9f + i * 2.1f), kahn.Position.Z);
                kahn.Rotation = new Vector3(0.012f * Mathf.Sin((float)_wasserZeit * 0.7f + i), kahn.Rotation.Y, 0.015f * Mathf.Sin((float)_wasserZeit * 0.8f + i * 1.3f));
            }
            h.Gesetzt = true;
        }
    }

    /// <summary>Alles Bewegte am Wasser, jedes Bild (aus <c>_Process</c>).</summary>
    private void WasserBild(double delta)
    {
        WasserFarbeNachfuehren();
        FaehrenBewegen(delta);
        HaefenBewegen(delta);
    }
}
