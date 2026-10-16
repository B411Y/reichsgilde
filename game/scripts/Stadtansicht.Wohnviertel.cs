using Godot;
using Reichsgilde.Core.Buerger;

namespace Reichsgilde;

/// <summary>
/// Wohnviertel und Gassennetz (fünfter Teil der <c>partial class Stadtansicht</c>).
///
/// Die Bürger-KI im Kern vergibt Wohnhäuser nur als Nummern (<see cref="Stadthaus"/>: Nummer und Art) und kennt
/// keine Koordinaten. Hier bekommt jede Nummer einen Platz:
///   • Ratsherren- und Bürgerhäuser stehen immer in der Stadt, auf den Plätzen am Ring, die dem Markt am nächsten sind.
///   • Einfache Wohnhäuser füllen die übrigen Ringplätze und die vier alten Häuser der Kulisse.
///   • Was dann noch übrig ist, kommt als Vorstadt vor das Osttor (Überlauf) – mit mehr Spielhäusern wächst sie.
///
/// Das <see cref="Gassennetz"/> verbindet Haustüren, Werkstätten, Markt, Rathaus, Dom, Tore und die drei Stellen im
/// Umland (Weide, Feld, Wald). Darauf laufen später die Passanten.
/// </summary>
public partial class Stadtansicht
{
    // ── Feste Punkte ────────────────────────────────────────────────────────

    /// <summary>Die Ringgasse läuft innen an der Mauer entlang, dort, wo früher die alte Mauer stand (±38 m).</summary>
    private const float Ringgasse = 38;

    /// <summary>Stichwege vom Ring nach innen, wo zwischen den Bauplätzen Platz ist (vom Ring zur nächsten Gasse).</summary>
    private static readonly Vector3[][] RingAnschluesse =
    {
        new Vector3[] { new(12, 0, Ringgasse), new(11, 0, 24) },       // Süden: zur Gasse der Maurerei an der Stadtmauer
        new Vector3[] { new(-21, 0, Ringgasse), new(-20.5f, 0, 22) },  // Süden: zur Gasse der Schänke am Fischmarkt
    };

    public static readonly Vector3 RathausTuer = new(0, 0, -14.5f);
    public static readonly Vector3 DomTuer = new(-20, 0, -2.5f);

    /// <summary>Die Stellen im Umland, an denen Hirten, Feldarbeiter und Holzsammler arbeiten (Namen wie im Kern).</summary>
    public static readonly Dictionary<string, Vector3> Umlandstellen = new()
    {
        ["Weide"] = new(70, 0, -44),
        ["Feld"] = new(30, 0, 60),
        ["Wald"] = new(34, 0, -82),
    };

    /// <summary>Breite, Tiefe und Abstand eines Ringplatzes (Meter).</summary>
    private const float PlatzBreite = 8.5f, PlatzTiefe = 8f;

    /// <summary>Ein Platz für ein Wohnhaus: Mitte, Blickrichtung der Vorderseite (Einheitsvektor) und die Tür.</summary>
    private sealed record Hausplatz(Vector3 Mitte, Vector3 Vorne, bool Ring)
    {
        public Vector3 Tuer(float tiefe) => Mitte + Vorne * (tiefe / 2);
    }

    private readonly Gassennetz _netz = new();
    public Gassennetz Netz => _netz;

    /// <summary>Alle gezeichneten Wege (für die Lage der Vorstadthäuser: nicht auf eine Straße bauen).</summary>
    private readonly List<(Vector3 Von, Vector3 Bis, float Breite)> _strassen = new();

    /// <summary>Hausnummer (aus dem Kern) → Knoten der Haustür im Gassennetz.</summary>
    private readonly Dictionary<int, int> _haustueren = new();
    /// <summary>Hausnummer → Platz, auf dem das Haus steht.</summary>
    private readonly Dictionary<int, Hausplatz> _hausplaetze = new();
    private bool _wohnhaeuserGebaut;

    private readonly Dictionary<Color, StandardMaterial3D> _stoffe = new();
    private StandardMaterial3D Stoff(Color farbe) => _stoffe.TryGetValue(farbe, out var m) ? m : _stoffe[farbe] = Material(farbe);

    // ═════════════════════════════════════════════════════════════════════════
    //  Gassennetz
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Baut Ringgasse, Stichwege und Umlandstellen und das ganze Gassennetz (einmal beim Start).</summary>
    private void WohnviertelAnlegen()
    {
        // Ringgasse und Stichwege zeichnen
        var ecken = new[] { new Vector3(-Ringgasse, 0, -Ringgasse), new Vector3(Ringgasse, 0, -Ringgasse), new Vector3(Ringgasse, 0, Ringgasse), new Vector3(-Ringgasse, 0, Ringgasse) };
        for (var i = 0; i < 4; i++) WegZiehen(ecken[i], ecken[(i + 1) % 4], 3);
        foreach (var stich in RingAnschluesse) WegZiehen(stich[0], stich[1], 3);
        WegZiehen(DomTuer, new Vector3(DomTuer.X, 0, WestgasseKnick.Z), 2.5f);

        UmlandstellenBauen();
        NetzKnuepfen(ecken);
    }

    private void NetzKnuepfen(Vector3[] ecken)
    {
        var n = _netz;
        // Hauptwege: Ost- und Westgasse mit den Straßen vor den Toren
        n.Weg(Marktplatz, Osttor, Landstrasse);
        n.Weg(Marktplatz, WestgasseKnick, new Vector3(-38, 0, 6), Westtor, Weststrasse);
        n.Weg(ecken[0], ecken[1], ecken[2], ecken[3], ecken[0]);
        foreach (var stich in RingAnschluesse) n.Weg(stich);
        n.Weg(RathausTuer, Marktplatz);
        n.Weg(DomTuer, new Vector3(DomTuer.X, 0, WestgasseKnick.Z));

        foreach (var (name, p) in Stadtplan)
        {
            if (InDerStadt(p))
            {
                // wie die Karren: vom Hof vor dem Tor über die Gasse(n) zum Markt
                var punkte = Gasse(name, p).Prepend(p + new Vector3(0, 0, 5.5f)).Append(Marktplatz).ToArray();
                n.Weg(punkte);
            }
            else
            {
                var abzweig = Abzweige.GetValueOrDefault(name, Osttor + new Vector3(20, 0, 0));
                if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
                    n.Weg(Osttor, abzweig);
                n.Weg(abzweig, p + new Vector3(-2, 0, 0));
            }
        }

        foreach (var (_, stelle) in Umlandstellen)
        {
            var anschluss = n.Knoten[n.Anschliessen(stelle)];
            n.Weg(anschluss, stelle);
            WegZiehen(anschluss, stelle, 2);
        }

        if (n.Teile() != 1)
            GD.PushWarning($"Gassennetz: {n.Teile()} getrennte Teile – nicht jedes Ziel ist erreichbar.");
    }

    public static bool InDerStadt(Vector3 p) => Mathf.Abs(p.X) < Mauerrand && Mathf.Abs(p.Z) < Mauerrand;

    /// <summary>Weide (Zaun), Feld (Furchen) und Wald (eine Baumgruppe) – die Arbeitsstellen der Einwohner im Umland.</summary>
    private void UmlandstellenBauen()
    {
        var holz = Stoff(Holz);
        var weide = Umlandstellen["Weide"];
        foreach (var (mitte, groesse) in new[]
        {
            (new Vector3(0, 0.5f, -5), new Vector3(14, 0.8f, 0.15f)), (new Vector3(0, 0.5f, 5), new Vector3(14, 0.8f, 0.15f)),
            (new Vector3(-7, 0.5f, 0), new Vector3(0.15f, 0.8f, 10)), (new Vector3(7, 0.5f, 0), new Vector3(0.15f, 0.8f, 10)),
        })
            Quader(weide + mitte, groesse, holz);
        Flaeche(weide, new Vector2(14, 10), new Color(0.38f, 0.47f, 0.26f), 0.01f);

        var feld = Umlandstellen["Feld"];
        Flaeche(feld, new Vector2(14, 9), new Color(0.42f, 0.33f, 0.22f), 0.01f);
        for (var i = 0; i < 6; i++)
            Quader(feld + new Vector3(-6 + i * 2.4f, 0.12f, 0), new Vector3(0.8f, 0.24f, 8.4f), Stoff(i % 2 == 0 ? Korn : Kraut));

        var wald = Umlandstellen["Wald"];
        var tanne = Stoff(Tanne);
        foreach (var (dx, dz, h) in new[] { (-5f, -3f, 8f), (-1f, -6f, 10f), (4f, -4f, 7f), (6f, 2f, 9f), (-4f, 4f, 9f), (1f, 5f, 7f), (-7f, 1f, 6f) })
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 2f, Height = h, RadialSegments = 7 },
                MaterialOverride = tanne,
                Position = wald + new Vector3(dx, h / 2, dz),
            });
        // Reisigbündel am Wegrand
        Quader(wald + new Vector3(2, 0.3f, 0), new Vector3(2.2f, 0.6f, 0.8f), holz);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wohnhäuser
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut die Wohnhäuser, sobald der Kern sie vergeben hat (beim ersten Takt mit Häusern). Ohne Simulation oder
    /// ohne Bürger-KI (Hauptmenü) wird der Ring mit einer festen Mustermischung gefüllt, damit er nicht leer wirkt.
    /// </summary>
    private void WohnhaeuserPruefen()
    {
        if (_wohnhaeuserGebaut) return;
        var einwohner = _simulation?.Wirtschaft?.Einwohnerschaft;
        if (einwohner is { IstAktiv: true })
        {
            if (einwohner.Wohnhaeuser.Count == 0) return; // noch nicht gegründet – nächstes Bild wieder fragen
            WohnhaeuserBauen(einwohner.Wohnhaeuser);
        }
        else
            WohnhaeuserBauen(Mustermischung());
    }

    /// <summary>Für das Hauptmenü: so viele Häuser, wie der Ring fasst – 13 Ratsherren, 3 Bürger, der Rest einfache Häuser.</summary>
    private static List<Stadthaus> Mustermischung()
    {
        var liste = new List<Stadthaus>();
        var plaetze = RingPlaetze().Count;
        for (var i = 1; i <= plaetze; i++)
            liste.Add(new Stadthaus(i, i <= 13 ? Hausart.Ratsherrenhaus : i <= 16 ? Hausart.Buergerhaus : Hausart.Wohnhaus));
        return liste;
    }

    private void WohnhaeuserBauen(IReadOnlyList<Stadthaus> haeuser)
    {
        _wohnhaeuserGebaut = true;
        var ring = RingPlaetze().OrderBy(p => p.Mitte.Length()).ThenBy(p => p.Mitte.X).ThenBy(p => p.Mitte.Z).ToList();
        var gross = haeuser.Where(h => h.Art != Hausart.Wohnhaus).OrderBy(h => h.Art == Hausart.Ratsherrenhaus ? 0 : 1).ThenBy(h => h.Nummer).ToList();
        var einfach = haeuser.Where(h => h.Art == Hausart.Wohnhaus).OrderBy(h => h.Nummer).ToList();

        // Große Häuser: die Ringplätze nächst dem Markt; reicht der Ring nicht, nehmen sie die Vorstadt wie alle anderen.
        var frei = new Queue<Hausplatz>(ring);
        var zuordnung = new List<(Stadthaus Haus, Hausplatz? Platz)>();
        foreach (var h in gross) zuordnung.Add((h, frei.Count > 0 ? frei.Dequeue() : null));
        // Einfache Häuser: zuerst die alten Häuser der Kulisse (sie stehen schon), dann der übrige Ring.
        var kulisse = new Queue<Hausplatz>(KulissePlaetze());
        foreach (var h in einfach)
            zuordnung.Add((h, kulisse.Count > 0 ? kulisse.Dequeue() : frei.Count > 0 ? frei.Dequeue() : null));

        var ohnePlatz = zuordnung.Count(z => z.Platz is null);
        var vorstadt = new Queue<Hausplatz>(VorstadtPlaetze(ohnePlatz));
        if (vorstadt.Count < ohnePlatz)
            GD.PushWarning($"Vorstadt: nur {vorstadt.Count} von {ohnePlatz} Plätzen gefunden – die übrigen Häuser teilen sich den letzten Platz.");

        Hausplatz? letzter = null;
        foreach (var (haus, platz) in zuordnung)
        {
            var p = platz ?? (vorstadt.Count > 0 ? vorstadt.Dequeue() : letzter);
            if (p is null) continue;
            letzter = p;
            _hausplaetze[haus.Nummer] = p;
            var tiefe = Masse(haus.Art).Z;
            var tuer = platz is not null && KulissePlaetze().Contains(platz) ? p.Mitte + p.Vorne * 3.2f : p.Tuer(tiefe);
            if (!KulissePlaetze().Contains(p) && !_gebautePlaetze.Contains(p))
            {
                WohnhausModell(haus.Art, p, haus.Nummer);
                _gebautePlaetze.Add(p);
            }
            if (!p.Ring && !KulissePlaetze().Contains(p))
                WegZiehen(tuer, VorstadtAnschluss(tuer), 2); // Pfad von der Vorstadthaustür zur Straße
            var knoten = _netz.Anschliessen(tuer);
            _netz.Weg(_netz.Knoten[knoten], tuer);
            _haustueren[haus.Nummer] = _netz.KnotenBei(tuer);
        }
        if (_netz.Teile() != 1)
            GD.PushWarning($"Gassennetz nach den Wohnhäusern: {_netz.Teile()} getrennte Teile.");
        if (WegenetzZeigen) NetzZeichnen();
    }

    private readonly HashSet<Hausplatz> _gebautePlaetze = new();

    private Vector3 VorstadtAnschluss(Vector3 tuer)
    {
        var beste = tuer;
        var abstand = float.MaxValue;
        foreach (var (von, bis, _) in _strassen)
        {
            var ab = bis - von;
            var t = ab.LengthSquared() > 0 ? Mathf.Clamp((tuer - von).Dot(ab) / ab.LengthSquared(), 0, 1) : 0;
            var q = von + ab * t;
            if (q.DistanceTo(tuer) < abstand) { abstand = q.DistanceTo(tuer); beste = q; }
        }
        return beste;
    }

    /// <summary>Maße (Breite, Höhe, Tiefe) je Hausart.</summary>
    private static Vector3 Masse(Hausart art) => art switch
    {
        Hausart.Ratsherrenhaus => new Vector3(7.6f, 8.5f, 7.6f),
        Hausart.Buergerhaus => new Vector3(7.2f, 7f, 7f),
        _ => new Vector3(6.2f, 5f, 6f),
    };

    /// <summary>
    /// Ein Wohnhaus nach seiner Art, die Vorderseite zur Gasse:
    /// Ratsherrenhaus aus Stein mit Giebel zur Gasse und Erker, Bürgerhaus verputzt mit Giebel,
    /// einfaches Haus aus Fachwerk oder Putz mit der Traufe zur Gasse.
    /// </summary>
    private void WohnhausModell(Hausart art, Hausplatz platz, int nummer)
    {
        var gruppe = new Node3D { Name = $"Wohnhaus_{nummer}", Position = platz.Mitte, Rotation = new Vector3(0, Mathf.Atan2(platz.Vorne.X, platz.Vorne.Z), 0) };
        AddChild(gruppe);
        var m = Masse(art);
        var (wand, dach, giebel) = art switch
        {
            Hausart.Ratsherrenhaus => (Mauerstein, Schiefer, true),
            Hausart.Buergerhaus => (Putz, nummer % 2 == 0 ? Ziegel : Schiefer, true),
            _ => (nummer % 3 == 0 ? Putz : Fachwerk, nummer % 4 == 0 ? Schiefer : Ziegel, false),
        };
        Quader(new Vector3(0, m.Y / 2, 0), m, Stoff(wand), gruppe);
        // Giebel zur Gasse: First läuft von vorn nach hinten (PrismMesh ohne Drehung); sonst Traufe zur Gasse.
        Dach(new Vector3(0, m.Y, 0), new Vector3(m.X + 0.6f, Mathf.Min(m.X, m.Z) * 0.6f, m.Z + 0.6f), Stoff(dach), quer: !giebel, eltern: gruppe);
        // Tür und Fenster auf der Vorderseite (lokal +Z)
        var dunkel = Stoff(new Color(0.18f, 0.13f, 0.10f));
        Quader(new Vector3(0, 1.1f, m.Z / 2 + 0.03f), new Vector3(1.2f, 2.2f, 0.1f), dunkel, gruppe);
        var glas = Stoff(new Color(0.20f, 0.22f, 0.26f));
        var stockwerke = art == Hausart.Ratsherrenhaus ? 3 : art == Hausart.Buergerhaus ? 2 : 1;
        for (var s = 0; s < stockwerke; s++)
            foreach (var x in new[] { -m.X / 3, m.X / 3 })
                Quader(new Vector3(x, 1.6f + s * 2.4f + (s > 0 ? 0.5f : 0), m.Z / 2 + 0.03f), new Vector3(0.9f, 1.1f, 0.1f), glas, gruppe);
        if (art == Hausart.Ratsherrenhaus)
            Quader(new Vector3(0, 5.2f, m.Z / 2 + 0.5f), new Vector3(2.4f, 2.2f, 1f), Stoff(Putz), gruppe); // Erker über der Tür
    }

    /// <summary>
    /// Plätze am Ring: je Seite eine Reihe zwischen Ringgasse und Mauer, die Vorderseite zur Gasse.
    /// Nord- und Südseite reichen bis an die Ecktürme, Ost- und Westseite lassen die Tore frei.
    /// </summary>
    private static List<Hausplatz> RingPlaetze()
    {
        var liste = new List<Hausplatz>();
        var abstand = Ringgasse + 1.5f + 0.8f + PlatzTiefe / 2; // Gassenrand + kleiner Vorplatz + halbe Haustiefe
        void Reihe(float von, float bis, Func<float, Vector3> mitte, Vector3 vorne)
        {
            var anzahl = (int)((bis - von) / PlatzBreite);
            var rand = (bis - von - anzahl * PlatzBreite) / 2;
            for (var i = 0; i < anzahl; i++)
                liste.Add(new Hausplatz(mitte(von + rand + PlatzBreite * (i + 0.5f)), vorne, true));
        }
        const float ecke = Mauerrand - 4.5f; // vor dem Eckturm
        Reihe(-ecke, ecke, x => new Vector3(x, 0, -abstand), Vector3.Back);   // Norden, Front nach Süden (+Z)
        Reihe(-ecke, ecke, x => new Vector3(x, 0, abstand), Vector3.Forward); // Süden, Front nach Norden (−Z)
        var innen = Ringgasse - 2;
        Reihe(-innen, Osttor.Z - 5, z => new Vector3(abstand, 0, z), Vector3.Left);
        Reihe(Osttor.Z + 5, innen, z => new Vector3(abstand, 0, z), Vector3.Left);
        Reihe(-innen, Westtor.Z - 5, z => new Vector3(-abstand, 0, z), Vector3.Right);
        Reihe(Westtor.Z + 5, innen, z => new Vector3(-abstand, 0, z), Vector3.Right);
        return liste;
    }

    /// <summary>Die vier Häuser der alten Kulisse (siehe <see cref="Wohnhaeuser"/>) – Tür zur nächsten Gasse.</summary>
    private static readonly List<Hausplatz> _kulissePlaetze = new()
    {
        new Hausplatz(new Vector3(10, 0, -29), Vector3.Back, true),
        new Hausplatz(new Vector3(-4, 0, 29), Vector3.Back, true),
        new Hausplatz(new Vector3(5, 0, 29), Vector3.Back, true),
        new Hausplatz(new Vector3(-29, 0, 18), Vector3.Right, true),
    };
    private static List<Hausplatz> KulissePlaetze() => _kulissePlaetze;

    /// <summary>
    /// Plätze vor dem Osttor: entlang der Landstraße und der Wege, die am Tor abzweigen, je Straßenseite.
    /// Ein Platz gilt nur, wenn er keine andere Straße, keinen Berg, keinen Betrieb und kein anderes Haus berührt.
    /// Die Plätze nächst dem Tor kommen zuerst.
    /// </summary>
    private List<Hausplatz> VorstadtPlaetze(int anzahl)
    {
        var liste = new List<Hausplatz>();
        if (anzahl <= 0) return liste;
        var kandidaten = new List<(Hausplatz Platz, float Abstand, int Strasse)>();
        for (var s = 0; s < _strassen.Count; s++)
        {
            var (von, bis, breite) = _strassen[s];
            if (InDerStadt(von) && InDerStadt(bis)) continue;
            var richtung = bis - von;
            var laenge = richtung.Length();
            if (laenge < PlatzBreite) continue;
            richtung /= laenge;
            var quer = new Vector3(-richtung.Z, 0, richtung.X);
            for (var t = 4f; t + 4 < laenge; t += PlatzBreite)
                foreach (var seite in new[] { 1f, -1f })
                {
                    var mitte = von + richtung * t + quer * seite * (breite / 2 + 0.8f + 3);
                    var p = new Hausplatz(mitte, -quer * seite, false);
                    kandidaten.Add((p, mitte.DistanceTo(Osttor), s));
                }
        }
        foreach (var (platz, _, strasse) in kandidaten.OrderBy(k => k.Abstand))
        {
            if (liste.Count >= anzahl) break;
            if (VorstadtPlatzFrei(platz.Mitte, strasse, liste)) liste.Add(platz);
        }
        return liste;
    }

    private bool VorstadtPlatzFrei(Vector3 mitte, int eigeneStrasse, List<Hausplatz> schonVergeben)
    {
        const float radius = 4.6f; // umschreibender Kreis eines Hauses
        if (Mathf.Abs(mitte.X) < Mauerrand + 3 + radius && Mathf.Abs(mitte.Z) < Mauerrand + 3 + radius) return false; // an der Mauer
        for (var s = 0; s < _strassen.Count; s++)
        {
            if (s == eigeneStrasse) continue;
            var (von, bis, breite) = _strassen[s];
            if (AbstandZurStrecke(mitte, von, bis) < radius + breite / 2 + 0.5f) return false;
        }
        foreach (var (name, p) in Stadtplan)
        {
            if (InDerStadt(p)) continue;
            if (Abzweige.ContainsKey(name) && Gruben.Contains(name))
            {
                // Berge der Gruben: zwei abgeflachte Kugeln (siehe BergBauen)
                if (mitte.DistanceTo(p + new Vector3(10, 0, -8)) < 18 + radius || mitte.DistanceTo(p + new Vector3(20, 0, 4)) < 13 + radius) return false;
            }
            if (mitte.DistanceTo(p) < 12 + radius) return false;
        }
        foreach (var (_, stelle) in Umlandstellen)
            if (mitte.DistanceTo(stelle) < 9 + radius) return false;
        foreach (var anderer in schonVergeben)
            if (mitte.DistanceTo(anderer.Mitte) < 2 * radius) return false;
        return true;
    }

    private static readonly HashSet<string> Gruben = new() { "Grube Eisenbart", "Grube am Silberberg", "Grube im Tannenwald" };

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfsansicht: --wegenetz=1 zeigt das Gassennetz
    // ═════════════════════════════════════════════════════════════════════════

    private static bool WegenetzZeigen => OS.GetCmdlineUserArgs().Contains("--wegenetz=1");

    private void NetzZeichnen()
    {
        var linie = Stoff(new Color(1f, 0.15f, 0.6f));
        linie.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        foreach (var (von, bis) in _netz.Kanten)
        {
            var laenge = von.DistanceTo(bis);
            if (laenge < 0.01f) continue;
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.1f, laenge) },
                MaterialOverride = linie,
                Position = (von + bis) / 2 + new Vector3(0, 0.3f, 0),
                Rotation = new Vector3(0, Mathf.Atan2(bis.X - von.X, bis.Z - von.Z), 0),
            });
        }
        var punkt = Stoff(new Color(1f, 0.9f, 0.1f));
        punkt.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        foreach (var k in _netz.Knoten)
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f }, MaterialOverride = punkt, Position = k + new Vector3(0, 0.4f, 0) });
    }
}
