using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;

namespace Reichsgilde;

/// <summary>
/// Wohnviertel und Gassennetz (fünfter Teil der <c>partial class Stadtansicht</c>).
///
/// Die Bürger-KI im Kern vergibt Wohnhäuser nur als Nummern (<see cref="Stadthaus"/>: Nummer und Art) und kennt
/// keine Koordinaten. Hier bekommt jede Nummer einen Platz der Karte (Einwohnerplätze, dazu die Wohnhausplätze, auf denen kein Spielhaus steht):
///   • Ratsherren- und Bürgerhäuser stehen immer in der Stadt, auf den Plätzen, die dem Markt am nächsten sind.
///   • Einfache Wohnhäuser füllen zuerst die Kulissenhäuser der Karte, dann die übrigen Plätze in der Stadt.
///   • Was dann noch übrig ist, kommt auf die Plätze vor der Mauer (Vorstadt), in der Reihenfolge der Karte – mit mehr Spielhäusern wächst sie.
///
/// Das <see cref="Gassennetz"/> entsteht aus den Wegen der Karte (Stadtansicht.Karte.cs); hier hängen die Haustüren daran. Darauf laufen die Passanten.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Platz für ein Wohnhaus: Mitte, Drehung (Bogenmaß), Breite und Tiefe; bei einem Kulissenhaus der Karte das Haus, das schon steht.</summary>
    private sealed record Hausplatz(Vector3 Mitte, float Drehung, Vector2 Groesse, Kartengebaeude? Kulisse = null)
    {
        /// <summary>Die Richtung, in die die Vorderseite zeigt (zur Gasse).</summary>
        public Vector3 Vorne => VorneBei(Drehung);
    }

    private readonly Gassennetz _netz = new();
    public Gassennetz Netz => _netz;

    /// <summary>Hausnummer (aus dem Kern) → Knoten der Haustür im Gassennetz.</summary>
    private readonly Dictionary<int, int> _haustueren = new();
    /// <summary>Hausnummer → Platz, auf dem das Haus steht.</summary>
    private readonly Dictionary<int, Hausplatz> _hausplaetze = new();
    private bool _wohnhaeuserGebaut;

    private readonly Dictionary<Color, StandardMaterial3D> _stoffe = new();
    private StandardMaterial3D Stoff(Color farbe) => _stoffe.TryGetValue(farbe, out var m) ? m : _stoffe[farbe] = Material(farbe);

    // ═════════════════════════════════════════════════════════════════════════
    //  Wohnhäuser
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut die Wohnhäuser, sobald der Kern sie vergeben hat (beim ersten Takt mit Häusern). Ohne Simulation oder
    /// ohne Bürger-KI (Hauptmenü) werden die Plätze der Stadt mit einer festen Mustermischung gefüllt, damit sie nicht leer wirken.
    /// </summary>
    private void WohnhaeuserPruefen()
    {
        if (_wohnhaeuserGebaut || !_karteGebaut) return;
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

    /// <summary>Für das Hauptmenü: so viele Häuser, wie die Stadt Plätze hat – 13 Ratsherren, 3 Bürger, der Rest einfache Häuser.</summary>
    private List<Stadthaus> Mustermischung()
    {
        var liste = new List<Stadthaus>();
        var plaetze = Einwohnerplaetze().Stadt.Count;
        for (var i = 1; i <= plaetze; i++)
            liste.Add(new Stadthaus(i, i <= 13 ? Hausart.Ratsherrenhaus : i <= 16 ? Hausart.Buergerhaus : Hausart.Wohnhaus));
        return liste;
    }

    private void WohnhaeuserBauen(IReadOnlyList<Stadthaus> haeuser)
    {
        _wohnhaeuserGebaut = true;
        var (stadt, draussen) = Einwohnerplaetze();
        var gross = haeuser.Where(h => h.Art != Hausart.Wohnhaus).OrderBy(h => h.Art == Hausart.Ratsherrenhaus ? 0 : 1).ThenBy(h => h.Nummer).ToList();
        var einfach = haeuser.Where(h => h.Art == Hausart.Wohnhaus).OrderBy(h => h.Nummer).ToList();

        // Große Häuser: die Plätze in der Stadt nächst dem Markt; reicht die Stadt nicht, nehmen sie die Vorstadt wie alle anderen.
        var frei = new Queue<Hausplatz>(stadt);
        var zuordnung = new List<(Stadthaus Haus, Hausplatz? Platz)>();
        foreach (var h in gross) zuordnung.Add((h, frei.Count > 0 ? frei.Dequeue() : null));
        // Einfache Häuser: zuerst die Kulissenhäuser (sie stehen schon), dann die übrigen Plätze der Stadt.
        var kulisse = new Queue<Hausplatz>(_kulissenTueren.Keys.Select(k => new Hausplatz(Punkt3(k.Punkt), DrehungVon(k), GroesseVon(k), k)));
        foreach (var h in einfach)
            zuordnung.Add((h, kulisse.Count > 0 ? kulisse.Dequeue() : frei.Count > 0 ? frei.Dequeue() : null));

        // Überlauf: die Plätze vor der Mauer. Reichen auch sie nicht, teilen sich die übrigen Häuser den letzten Platz.
        var ohnePlatz = zuordnung.Count(z => z.Platz is null);
        if (ohnePlatz > draussen.Count)
            GD.PushWarning($"Vorstadt: {ohnePlatz} Häuser, aber nur {draussen.Count} Plätze – die übrigen teilen sich den letzten.");
        var vorstadt = new Queue<Hausplatz>(draussen.Take(ohnePlatz));

        Hausplatz? letzter = null;
        foreach (var (haus, platz) in zuordnung)
        {
            var p = platz ?? (vorstadt.Count > 0 ? vorstadt.Dequeue() : letzter);
            if (p is null) continue;
            letzter = p;
            _hausplaetze[haus.Nummer] = p;
            if (p.Kulisse is { } k)
            {
                _platzKennung[p] = "kulisse:" + k.Kennung; // die Tür des Kulissenmodells
                _haustueren[haus.Nummer] = _netz.KnotenBei(_kulissenTueren[k]);
                continue;
            }
            if (!_gebautePlaetze.Contains(p))
            {
                WohnhausModell(haus.Art, p, haus.Nummer);
                _gebautePlaetze.Add(p);
                GrasPlatz(p.Mitte, p.Groesse); // unter dem Haus kein Grasteppich (Stadtansicht.Gras.cs)
            }
            _haustueren[haus.Nummer] = AnWegAnschliessen(p.Mitte + p.Vorne * (p.Groesse.Y / 2 + VorplatzBreite));
        }
        _schichtAlles = true; // freie Plätze in der Stadt sind Wiese (Stadtansicht.Wegeschicht.cs): erst jetzt steht fest, welche frei bleiben
        if (_netz.Teile() != 1)
            GD.PushWarning($"Gassennetz nach den Wohnhäusern: {_netz.Teile()} getrennte Teile.");
        if (WegenetzZeigen) NetzZeichnen();
    }

    private readonly HashSet<Hausplatz> _gebautePlaetze = new();

    /// <summary>
    /// Die Plätze für Einwohnerhäuser aus der Karte: alle Einwohnerplätze und jeder Wohnhausplatz, auf dem kein Spielhaus steht – geteilt in so
    /// viele Plätze, wie nebeneinander passen (ein Doppelplatz am Ring nimmt zwei Häuser auf). In der Stadt nach dem Abstand zum Markt geordnet,
    /// die Plätze vor der Mauer (Vorstadt) in der Reihenfolge der Karte.
    /// </summary>
    private (List<Hausplatz> Stadt, List<Hausplatz> Draussen) Einwohnerplaetze()
    {
        if (_karte is null) return (new(), new());
        var belegt = BelegteWohnhausplaetze();
        var plaetze = _karte.GebaeudeDerArt(Bausteinart.Einwohnerplatz).Select(g => new Hausplatz(Punkt3(g.Punkt), DrehungVon(g), GroesseVon(g))).ToList();
        foreach (var g in _karte.Wohnhausplaetze.Where(g => !belegt.Contains(g.Kennung)))
        {
            var groesse = GroesseVon(g);
            var anzahl = Math.Max(1, (int)(groesse.X / EinwohnerplatzBreite));
            var breite = groesse.X / anzahl;
            for (var i = 0; i < anzahl; i++)
                plaetze.Add(new Hausplatz(AufFlaeche(g, new Vector3(-groesse.X / 2 + breite * (i + 0.5f), 0, 0)), DrehungVon(g), new Vector2(breite, groesse.Y)));
        }
        var stadt = plaetze.Where(p => InDerStadt(p.Mitte)).OrderBy(p => p.Mitte.DistanceTo(_markt)).ThenBy(p => p.Mitte.X).ThenBy(p => p.Mitte.Z).ToList();
        return (stadt, plaetze.Where(p => !InDerStadt(p.Mitte)).ToList());
    }

    /// <summary>So breit (Meter, an der Gasse) braucht ein Einwohnerhaus mindestens – danach wird ein freier Wohnhausplatz geteilt.</summary>
    private const float EinwohnerplatzBreite = 8f;

    /// <summary>
    /// Die Wohnhausplätze der Karte, auf denen ein Spielhaus steht: mit Partie die, für die der Kern ein Grundstück führt (bewohnt, im Bau, Leerstand
    /// oder Bauplatz), im Hauptmenü die ersten vier (<see cref="SpielhaeuserMuster"/>).
    /// </summary>
    private HashSet<string> BelegteWohnhausplaetze() =>
        _simulation?.Wirtschaft is { } w
            ? w.Bauen.Grundstuecke.Select(g => g.Kennung).OfType<string>().ToHashSet()
            : _karte?.Wohnhausplaetze.Take(MusterSpielhaeuser).Select(g => g.Kennung).ToHashSet() ?? new HashSet<string>();

    /// <summary>Die Modelle der Einwohnerhäuser je Hausart aus dem Gebäudekatalog (Konventionen in <c>docs/bereiche/darstellung.md</c>).</summary>
    private string EinwohnerDatei(Hausart art) => Katalog?.Einwohnerhaus(art.ToString())?.Modell ?? art switch
    {
        Hausart.Ratsherrenhaus => "gebaeude/einwohner_ratsherrenhaus",
        Hausart.Buergerhaus => "gebaeude/einwohner_buergerhaus",
        _ => "gebaeude/einwohner_wohnhaus",
    };

    /// <summary>Wo die Hausfront im Modell liegt (z): Ratsherren- und Bürgerhaus bei +3 (Fläche 8 × 10 m), einfaches Haus bei +2 (6 × 8 m) – jeweils 2 m Vorplatz davor.</summary>
    private static float EinwohnerFront(Hausart art) => art == Hausart.Wohnhaus ? 2f : 3f;

    /// <summary>Streifen zwischen Hauswand und Gassenrand (Meter) – dort steht der Anker der Tür, die Haustür im Gassennetz liegt am Gassenrand.</summary>
    private const float VorplatzBreite = 0.8f;

    /// <summary>Gedeckte Hausfarben der Einwohnerhäuser (Ochsenblut, Waid-Blau, Ocker, Moosgrün, Braun) – nach der Hausnummer, damit sie sich nicht mit den kräftigen Farben der Spielhäuser beißen.</summary>
    private static readonly Color[] Hausfarben =
    {
        new(0.42f, 0.17f, 0.15f), new(0.24f, 0.31f, 0.43f), new(0.62f, 0.46f, 0.20f), new(0.30f, 0.38f, 0.22f), new(0.38f, 0.27f, 0.18f),
    };

    /// <summary>Welche Tür-Kennung zu welchem gebauten Platz gehört – teilen sich Häuser einen Platz (Vorstadt voll), gehen alle zur Tür des gebauten.</summary>
    private readonly Dictionary<Hausplatz, string> _platzKennung = new();

    /// <summary>Kennung der Haustür eines Einwohnerhauses für <see cref="TuerAnker"/> (beim Kulissenhaus die Tür seines Modells) – null, wenn es keine gibt.</summary>
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
        var gruppe = new Node3D { Name = $"Wohnhaus_{nummer}", Position = AufFundament(platz.Mitte, platz.Drehung, platz.Groesse), Rotation = new Vector3(0, platz.Drehung, 0) };
        AddChild(gruppe);
        GruppenSockel(gruppe, platz.Drehung, platz.Groesse); // auf einem Hang auf einem Bruchsteinsockel (Stadtansicht.Gelaende.cs)
        var variante = art switch
        {
            Hausart.Wohnhaus => nummer % 3 == 0 ? 2 : nummer % 4 == 0 ? 3 : 1, // meist Fachwerk, jedes dritte verputzt, jedes vierte mit Schiefer
            _ => nummer % 3 + 1,
        };
        var kennung = "einwohner:" + nummer;
        ModellEinsetzen(EinwohnerDatei(art), gruppe, new Vector3(0, 0, platz.Groesse.Y / 2 - EinwohnerFront(art)),
            new ModellZustand(Variante: variante, Besitzer: Hausfarben[nummer % Hausfarben.Length]), kennung);
        _platzKennung[platz] = kennung;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfsansicht: --wegenetz=1 zeigt das Gassennetz
    // ═════════════════════════════════════════════════════════════════════════

    private static bool WegenetzZeigen => OS.GetCmdlineUserArgs().Contains("--wegenetz=1");

    private void NetzZeichnen() => NetzZeichnen(_netz, this);

    /// <summary>
    /// Zeichnet ein Gassennetz: Kanten pink, Knoten gelb – auch die Kartenwerkstatt (Schalter „Gassennetz“), dort <paramref name="immerSichtbar"/>
    /// über allem, sonst verdecken die Häuser am Ring die Gassen.
    /// </summary>
    private void NetzZeichnen(Gassennetz netz, Node3D eltern, bool immerSichtbar = false)
    {
        var linie = immerSichtbar ? ImmerSichtbar(Transparent(new Color(1f, 0.15f, 0.6f), 0.9f)) : Stoff(new Color(1f, 0.15f, 0.6f));
        linie.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        foreach (var (von, bis) in netz.Kanten)
        {
            var laenge = von.DistanceTo(bis);
            if (laenge < 0.01f) continue;
            eltern.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.1f, laenge) },
                MaterialOverride = linie,
                Position = (von + bis) / 2 + new Vector3(0, 0.3f, 0),
                Rotation = new Vector3(0, Mathf.Atan2(bis.X - von.X, bis.Z - von.Z), 0),
            });
        }
        var punkt = immerSichtbar ? ImmerSichtbar(Transparent(new Color(1f, 0.9f, 0.1f), 0.95f)) : Stoff(new Color(1f, 0.9f, 0.1f));
        punkt.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        foreach (var k in netz.Knoten)
            eltern.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f }, MaterialOverride = punkt, Position = k + new Vector3(0, 0.4f, 0) });
    }
}
