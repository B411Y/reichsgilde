using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

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
        new Vector3[] { new(12, 0, Ringgasse), new(12, 0, 19.5f) },      // Süden: zur Gasse der Gießerei Eisenbart (ihr Knick)
        new Vector3[] { new(-21, 0, Ringgasse), new(-21, 0, 22.5f) },    // Süden: zum Hof am Weinmarkt
    };

    /// <summary>Wo das Gassennetz an Rathaus und Dom beginnt: vor dem Rundbogen der Markthalle, in dem der Anker des Modells liegt (x −1,8), und am Fuß der Stufen vor dem Domportal.</summary>
    public static readonly Vector3 RathausTuer = new(-1.8f, 0, -14.5f);
    public static readonly Vector3 DomTuer = new(-20, 0, 4.2f);

    /// <summary>Die Stellen im Umland, an denen Hirten, Feldarbeiter und Holzsammler arbeiten (Namen wie im Kern).</summary>
    public static readonly Dictionary<string, Vector3> Umlandstellen = new()
    {
        ["Weide"] = new(-70, 0, 26),  // vor dem Westtor, südlich der Weststraße
        ["Feld"] = new(-60, 0, 62),
        ["Wald"] = new(-68, 0, -14),  // nördlich der Weststraße
    };

    /// <summary>Wo der Fußweg zu einer Umlandstelle von der Weststraße abgeht – je Stelle ein eigener, damit keiner durch den anderen führt.</summary>
    private static readonly Dictionary<string, Vector3> UmlandZugaenge = new()
    {
        ["Weide"] = new(-70, 0, 6),
        ["Feld"] = new(-58, 0, 6),
        ["Wald"] = new(-66, 0, 6),
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
                var abzweig = Abzweige.GetValueOrDefault(name, Wegkreuz + new Vector3(10, 0, 0));
                if (!AnDerStrasse(name, abzweig))
                    n.Weg(Kreuz(name), abzweig);
                n.Weg(abzweig, UmlandHof(name, p, UmlandArtVon(name) ?? Betriebsart.Mine)); // bis zum „Anker_Karren“ des Modells
            }
        }

        foreach (var (name, stelle) in Umlandstellen)
        {
            var zugang = UmlandZugaenge[name];
            n.Weg(zugang, stelle);
            // sichtbar endet der Fußweg am Gatter („Anker_Tuer“ des Modells, 5,4 m vor der Mitte), im Netz an der Mitte der Stelle
            WegZiehen(zugang, stelle + new Basis(Vector3.Up, UmlandstelleDrehung(name)) * UmlandAnkerImModell("umland_stellen").Tuer, 2);
        }

        if (n.Teile() != 1)
            GD.PushWarning($"Gassennetz: {n.Teile()} getrennte Teile – nicht jedes Ziel ist erreichbar.");
    }

    public static bool InDerStadt(Vector3 p) => Mathf.Abs(p.X) < Mauerrand && Mathf.Abs(p.Z) < Mauerrand;

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
        {
            WohnhaeuserBauen(Mustermischung());
            if (_simulation is null) SpielhaeuserMuster();
        }
    }

    /// <summary>Für das Hauptmenü: so viele Häuser, wie der Ring fasst – 13 Ratsherren, 3 Bürger, der Rest einfache Häuser.</summary>
    private static List<Stadthaus> Mustermischung()
    {
        var liste = new List<Stadthaus>();
        var plaetze = RingPlaetze().Count - 2 * 4; // vier Doppelplätze für die Muster-Spielhäuser
        for (var i = 1; i <= plaetze; i++)
            liste.Add(new Stadthaus(i, i <= 13 ? Hausart.Ratsherrenhaus : i <= 16 ? Hausart.Buergerhaus : Hausart.Wohnhaus));
        return liste;
    }

    private void WohnhaeuserBauen(IReadOnlyList<Stadthaus> haeuser)
    {
        _wohnhaeuserGebaut = true;
        // Doppelplätze der Spielhäuser vorher abziehen (Stadtansicht.Spielhaeuser.cs)
        SpielhausPlaetze(AnzahlSpielhaeuser());
        var ring = RingPlaetze().Where(p => !BelegteRingplaetze.Contains(p))
                                .OrderBy(p => p.Mitte.Length()).ThenBy(p => p.Mitte.X).ThenBy(p => p.Mitte.Z).ToList();
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

        // Überlauf: die Vorstadt vor dem Osttor. Reicht auch sie nicht, teilen sich die übrigen Häuser den letzten Platz.
        var ohnePlatz = zuordnung.Count(z => z.Platz is null);
        var vorstadtPlaetze = VorstadtPlaetze();
        if (ohnePlatz > vorstadtPlaetze.Count)
            GD.PushWarning($"Vorstadt: {ohnePlatz} Häuser, aber nur {vorstadtPlaetze.Count} Plätze – die übrigen teilen sich den letzten.");
        var vorstadt = new Queue<Hausplatz>(vorstadtPlaetze.Take(ohnePlatz));
        VorstadtgasseAnlegen(vorstadt);

        Hausplatz? letzter = null;
        foreach (var (haus, platz) in zuordnung)
        {
            var p = platz ?? (vorstadt.Count > 0 ? vorstadt.Dequeue() : letzter);
            if (p is null) continue;
            letzter = p;
            _hausplaetze[haus.Nummer] = p;
            var tuer = KulissePlaetze().Contains(p) ? p.Mitte + p.Vorne * 3.2f : p.Mitte + p.Vorne * (Vorderkante(p) + VorplatzBreite);
            if (!KulissePlaetze().Contains(p) && !_gebautePlaetze.Contains(p))
            {
                WohnhausModell(haus.Art, p, haus.Nummer);
                _gebautePlaetze.Add(p);
            }
            var knoten = _netz.Anschliessen(tuer);
            _netz.Weg(_netz.Knoten[knoten], tuer);
            _haustueren[haus.Nummer] = _netz.KnotenBei(tuer);
        }
        if (_netz.Teile() != 1)
            GD.PushWarning($"Gassennetz nach den Wohnhäusern: {_netz.Teile()} getrennte Teile.");
        if (WegenetzZeigen) NetzZeichnen();
    }

    private readonly HashSet<Hausplatz> _gebautePlaetze = new();

    /// <summary>Die Modelle der Einwohnerhäuser je Hausart (<c>game/assets/modelle/</c>, Konventionen in <c>docs/bereiche/darstellung.md</c>).</summary>
    private static string EinwohnerDatei(Hausart art) => art switch
    {
        Hausart.Ratsherrenhaus => "einwohner_ratsherrenhaus",
        Hausart.Buergerhaus => "einwohner_buergerhaus",
        _ => "einwohner_wohnhaus",
    };

    /// <summary>Wo die Hausfront im Modell liegt (z): Ratsherren- und Bürgerhaus bei +3 (Fläche 8 × 10 m), einfaches Haus bei +2 (6 × 8 m) – jeweils 2 m Vorplatz davor.</summary>
    private static float EinwohnerFront(Hausart art) => art == Hausart.Wohnhaus ? 2f : 3f;

    /// <summary>Abstand der Hauswand von der Mitte des Platzes (Meter): am Ring die halbe Platztiefe, in der Vorstadt bleiben 0,8 m Vorplatz bis zur Gasse.</summary>
    private static float Vorderkante(Hausplatz platz) => platz.Ring ? PlatzTiefe / 2 : 3.1f;

    /// <summary>Streifen zwischen Hauswand und Gassenrand (Meter) – dort steht der Anker der Tür, die Haustür im Gassennetz liegt am Gassenrand.</summary>
    private const float VorplatzBreite = 0.8f;

    /// <summary>Gedeckte Hausfarben der Einwohnerhäuser (Ochsenblut, Waid-Blau, Ocker, Moosgrün, Braun) – nach der Hausnummer, damit sie sich nicht mit den kräftigen Farben der Spielhäuser beißen.</summary>
    private static readonly Color[] Hausfarben =
    {
        new(0.42f, 0.17f, 0.15f), new(0.24f, 0.31f, 0.43f), new(0.62f, 0.46f, 0.20f), new(0.30f, 0.38f, 0.22f), new(0.38f, 0.27f, 0.18f),
    };

    /// <summary>Welche Tür-Kennung zu welchem gebauten Platz gehört – teilen sich Häuser einen Platz (Vorstadt voll), gehen alle zur Tür des gebauten.</summary>
    private readonly Dictionary<Hausplatz, string> _platzKennung = new();

    /// <summary>Kennung der Haustür eines Einwohnerhauses für <see cref="TuerAnker"/> – null, wenn das Haus nicht als Modell steht (Kulisse).</summary>
    private string? EinwohnerKennung(int nummer) =>
        _hausplaetze.TryGetValue(nummer, out var platz) && _platzKennung.TryGetValue(platz, out var kennung) ? kennung : null;

    /// <summary>
    /// Ein Wohnhaus nach seiner Art als Modell aus Blender, die Vorderseite zur Gasse: <c>einwohner_ratsherrenhaus</c>, <c>einwohner_buergerhaus</c>
    /// oder <c>einwohner_wohnhaus</c>, eingesetzt über <see cref="ModellEinsetzen"/>. Die Variante (1–3) folgt der Hausnummer, die Hausfarbe
    /// ebenso (gedeckte Palette). Die Hauswand steht an der Vorderkante des Platzes – am Ring (8 m tief, ohne Vorplatz) damit 1 m weiter zur
    /// Gasse als in der Mitte einer 10-m-Fläche.
    /// </summary>
    private void WohnhausModell(Hausart art, Hausplatz platz, int nummer)
    {
        var gruppe = new Node3D { Name = $"Wohnhaus_{nummer}", Position = platz.Mitte, Rotation = new Vector3(0, Mathf.Atan2(platz.Vorne.X, platz.Vorne.Z), 0) };
        AddChild(gruppe);
        var variante = art switch
        {
            Hausart.Wohnhaus => nummer % 3 == 0 ? 2 : nummer % 4 == 0 ? 3 : 1, // meist Fachwerk, jedes dritte verputzt, jedes vierte mit Schiefer
            _ => nummer % 3 + 1,
        };
        var kennung = "einwohner:" + nummer;
        ModellEinsetzen(EinwohnerDatei(art), gruppe, new Vector3(0, 0, Vorderkante(platz) - EinwohnerFront(art)),
            new ModellZustand(Variante: variante, Besitzer: Hausfarben[nummer % Hausfarben.Length]), kennung);
        _platzKennung[platz] = kennung;
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
        new Hausplatz(new Vector3(-13, 0, 29), Vector3.Back, true), // in der Reihe mit den beiden davor, südöstlich vom Weinmarkt
    };
    private static List<Hausplatz> KulissePlaetze() => _kulissePlaetze;

    /// <summary>Die Vorstadtgasse kreuzt die Landstraße zwischen Osttor und Wegkreuz und reicht so weit nach Norden und Süden.</summary>
    public const float VorstadtRand = 46;
    private const float VorstadtGasseX = 64;
    private const float VorstadtBreite = 7.5f;

    /// <summary>
    /// Plätze in der Vorstadt: beiderseits der Vorstadtgasse (x = 64), nördlich und südlich der Landstraße,
    /// die Vorderseite zur Gasse. Die Plätze nächst der Landstraße kommen zuerst. Für 8 Spielhäuser reicht es.
    /// </summary>
    private static List<Hausplatz> VorstadtPlaetze()
    {
        var liste = new List<Hausplatz>();
        var abstand = 1.5f + 0.8f + 3.1f; // Gassenrand + Vorplatz + halbe Tiefe eines einfachen Hauses
        const float anfang = 5;           // Abstand zur Landstraße
        var anzahl = (int)((VorstadtRand - anfang) / VorstadtBreite);
        for (var i = 0; i < anzahl; i++)
        {
            var z = anfang + VorstadtBreite * (i + 0.5f);
            foreach (var seite in new[] { -1f, 1f })        // west, ost
                foreach (var richtung in new[] { -1f, 1f })  // nord, süd
                    liste.Add(new Hausplatz(new Vector3(VorstadtGasseX + seite * abstand, 0, z * richtung), new Vector3(-seite, 0, 0), false));
        }
        return liste;
    }

    /// <summary>Zeichnet die Vorstadtgasse so weit, wie Häuser an ihr stehen, und knüpft sie ins Netz.</summary>
    private void VorstadtgasseAnlegen(IEnumerable<Hausplatz> plaetze)
    {
        var liste = plaetze.ToList();
        if (liste.Count == 0) return;
        var nord = Mathf.Min(0, liste.Min(p => p.Mitte.Z) - VorstadtBreite / 2);
        var sued = Mathf.Max(0, liste.Max(p => p.Mitte.Z) + VorstadtBreite / 2);
        var a = new Vector3(VorstadtGasseX, 0, nord);
        var b = new Vector3(VorstadtGasseX, 0, sued);
        WegZiehen(a, b, 3);
        _netz.Weg(a, b); // kreuzt die Landstraße – der Knoten dort entsteht von selbst
    }

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
