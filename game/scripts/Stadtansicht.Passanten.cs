using System.Diagnostics;
using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Passanten (sechster Teil der <c>partial class Stadtansicht</c>): die Einwohner der Bürger-KI, sichtbar in Stadt und Umland.
///
/// Die Darstellung rechnet nichts selbst aus, sie liest nur:
///   • <see cref="Einwohner.Wo"/> – wo jemand gerade ist (null = unterwegs),
///   • <see cref="Einwohner.Weg"/> – von wo nach wo, mit Abmarsch und Ankunft.
/// Daraus folgt in jedem Bild:
///   • unterwegs → die Figur läuft im <see cref="Gassennetz"/> vom alten zum neuen Ort; wie weit, sagt der Anteil der
///     Wegzeit (Minute + angebrochener Bruchteil, also flüssig). Lange Wege werden schneller gegangen, kurze langsamer.
///   • am Ort → sichtbar nur auf Markt, Gasse, am Tor und im Umland. Wer zu Hause, in Schänke, Kirche oder Rathaus ist,
///     ist drinnen und nicht zu sehen. So passt das Bild immer zur Zählung des Kerns.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Eine Figur auf der Straße und was sie sich gemerkt hat, damit nicht jedes Bild neu gerechnet wird.</summary>
    private sealed class Passant
    {
        public required Node3D Knoten { get; init; }
        public required Node3D Koerper { get; init; }
        /// <summary>Der Weg, für den <see cref="Pfad"/> berechnet wurde (Vergleich über die Referenz: neuer Weg = neue Strecke).</summary>
        public Weg? FuerWeg { get; set; }
        public Vector3[] Pfad { get; set; } = Array.Empty<Vector3>();
        public float Laenge { get; set; }
        /// <summary>Seitlicher Versatz (Meter), damit nicht alle auf derselben Linie laufen.</summary>
        public float Versatz { get; init; }
        /// <summary>Eigenes Gehtempo in Metern je echter Sekunde bei Tempo 1 (schlendern, normal, eilig).</summary>
        public float Gehtempo { get; init; }
        /// <summary>Wann die Figur für den aktuellen Weg losgeht und ankommt (Spielminuten, mit Bruchteil).</summary>
        public double LaufStart { get; set; }
        public double LaufEnde { get; set; }
        /// <summary>Wo die Figur zuletzt stand (null = sie lief). Steht sie noch dort, wird nichts neu gesetzt.</summary>
        public (Aufenthalt Ort, Weg? Weg)? Stand { get; set; }
    }

    private readonly Dictionary<int, Passant> _passanten = new();
    private readonly Dictionary<Aufenthalt, Vector3> _verweilpunkte = new();
    private Node3D? _passantenEbene;

    // Gemeinsame Formen und Stoffe für alle Figuren – so muss die Grafikkarte nur wenige verschiedene Dinge kennen.
    private static readonly CapsuleMesh Rumpf = new() { Radius = 0.28f, Height = 1.3f };
    private static readonly SphereMesh Kopf = new() { Radius = 0.2f, Height = 0.4f };
    private static readonly Color Hautfarbe = new(0.85f, 0.68f, 0.55f);

    // Messung der Rechenzeit (nur Protokoll, alle 10 Sekunden)
    private readonly Stopwatch _passantenUhr = new();
    private double _passantenZeit, _passantenSeitMessung;
    private int _passantenBilder;

    /// <summary>Jedes Bild: alle Einwohner an ihren Platz stellen.</summary>
    private void PassantenAktualisieren(double minuteGenau, double delta)
    {
        if (_simulation?.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } einwohnerschaft || !_wohnhaeuserGebaut) return;
        _passantenUhr.Restart();
        _passantenEbene ??= NeueEbene();
        var minute = (long)Math.Floor(minuteGenau);

        var einwohner = einwohnerschaft.Einwohner;
        foreach (var e in einwohner)
        {
            if (!_passanten.TryGetValue(e.Nummer, out var p))
                _passanten[e.Nummer] = p = PassantBauen(e);

            // Wo die Figur ist, sagt ihr eigener Laufplan (siehe LaufPlanen): vor dem Losgehen am alten Ort,
            // nach der Ankunft am neuen, dazwischen unterwegs.
            var weg = e.Weg;
            if (weg is not null && !ReferenceEquals(p.FuerWeg, weg))
            {
                p.FuerWeg = weg;
                p.Pfad = Strecke(weg, e);
                p.Laenge = Gassennetz.Laenge(p.Pfad);
                LaufPlanen(p, weg);
            }
            Aufenthalt? wo = weg is null || minuteGenau >= p.LaufEnde ? e.Aufenthalt
                           : minuteGenau < p.LaufStart ? weg.Von
                           : null;
            if (wo is { } ort)
            {
                // Am Ort: nur draußen sichtbar. Steht die Figur schon da, bleibt alles, wie es ist –
                // jede gesetzte Position kostet Godot Arbeit, auch wenn sie sich nicht ändert.
                var stand = (ort, weg);
                if (p.Stand == stand) continue;
                p.Stand = stand;
                var punkt = Draussen(ort) ? Verweilpunkt(ort, e) : (Vector3?)null;
                p.Knoten.Visible = punkt is not null;
                if (punkt is { } q)
                {
                    p.Knoten.Position = q;
                    p.Koerper.Position = Vector3.Zero;
                }
                continue;
            }
            p.Stand = null;

            // Unterwegs
            if (p.Pfad.Length == 0) { p.Knoten.Visible = false; continue; }
            var anteil = (float)Math.Clamp((minuteGenau - p.LaufStart) / (p.LaufEnde - p.LaufStart), 0, 1);
            var hier = Gassennetz.PunktAuf(p.Pfad, anteil);
            var weiter = Gassennetz.PunktAuf(p.Pfad, Mathf.Min(1, anteil + 0.5f / Mathf.Max(p.Laenge, 1)));
            var richtung = weiter - hier;
            if (richtung.LengthSquared() > 1e-6f)
            {
                richtung = richtung.Normalized();
                p.Knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);
                hier += new Vector3(-richtung.Z, 0, richtung.X) * p.Versatz; // rechts gehen, je nach Nummer etwas versetzt
            }
            p.Knoten.Position = hier;
            p.Knoten.Visible = true;
            // Schritte: leichtes Auf und Ab, abhängig vom zurückgelegten Weg (nicht von der Zeit) – so passt es zu jedem Tempo.
            p.Koerper.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(anteil * p.Laenge * 2.2f)) * 0.08f, 0);
        }

        // Verstorbene und Weggezogene verschwinden (nur prüfen, wenn die Zahl nicht stimmt)
        if (einwohner.Count != _passanten.Count)
            foreach (var nummer in _passanten.Keys.Except(einwohner.Select(e => e.Nummer)).ToList())
            {
                _passanten[nummer].Knoten.QueueFree();
                _passanten.Remove(nummer);
            }

        _passantenZeit += _passantenUhr.Elapsed.TotalMilliseconds;
        _passantenBilder++;
        _passantenSeitMessung += delta;
        if (_passantenSeitMessung >= 10)
        {
            // Wie schnell die Laufenden gerade gehen (Meter je echter Sekunde bei Tempo 1) – zum Prüfen der Gehtempi
            var sekunden = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
            var laufend = _passanten.Values.Where(p => p.Stand is null && p.Knoten.Visible && p.LaufEnde > p.LaufStart)
                                           .Select(p => p.Laenge / ((p.LaufEnde - p.LaufStart) * sekunden)).ToList();
            var tempi = laufend.Count == 0 ? "" : $"Gehtempo Ø {laufend.Average():0.0} m/s, höchstens {laufend.Max():0.0} m/s, ";
            GD.Print($"Passanten: {_passanten.Count} Figuren, {_passanten.Values.Count(p => p.Knoten.Visible)} sichtbar, {tempi}" +
                     $"{_passantenZeit / _passantenBilder:0.00} ms je Bild, {Engine.GetFramesPerSecond()} Bilder/s, " +
                     $"{RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} Zeichenaufrufe");
            _passantenZeit = 0;
            _passantenBilder = 0;
            _passantenSeitMessung = 0;
        }
    }

    /// <summary>Höchstens so viel schneller als ihr eigenes Tempo geht eine Figur, wenn die Zeit knapp ist.</summary>
    private const double Hoechstens = 2;

    /// <summary>
    /// Plant den Gang einer Figur mit ihrem eigenen Tempo:
    ///   • Reicht die Zeit zwischen Abmarsch und Ankunft (Kern), geht sie so spät los, dass sie genau zur Ankunft da ist.
    ///   • Reicht sie nicht, geht sie beim Abmarsch los und beeilt sich – bis doppelt so schnell.
    ///   • Reicht auch das nicht, kommt sie etwas später an, als der Kern sie zählt. Dafür bleibt sie glaubwürdig.
    /// Die Geschwindigkeit gilt in echten Sekunden bei Tempo 1; bei höherem Spieltempo laufen alle entsprechend schneller.
    /// </summary>
    private void LaufPlanen(Passant p, Weg weg)
    {
        var sekundenJeMinute = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        var meterJeMinute = p.Gehtempo * sekundenJeMinute;
        var gemuetlich = p.Laenge / meterJeMinute;        // Minuten im eigenen Tempo
        var verfuegbar = (double)(weg.Ankunft - weg.Abmarsch);
        if (gemuetlich <= verfuegbar)
        {
            p.LaufStart = weg.Ankunft - gemuetlich;
            p.LaufEnde = weg.Ankunft;
        }
        else
        {
            p.LaufStart = weg.Abmarsch;
            p.LaufEnde = weg.Abmarsch + Math.Max(verfuegbar, gemuetlich / Hoechstens);
        }
    }

    private Node3D NeueEbene()
    {
        var ebene = new Node3D { Name = "Passanten" };
        AddChild(ebene);
        return ebene;
    }

    /// <summary>Wo ein Einwohner draußen zu sehen ist. Drinnen (Heim, Schänke, Kirche, Rathaus) nicht.</summary>
    private static bool Draussen(Aufenthalt a) => a.Ort is Stadtort.Markt or Stadtort.Gasse or Stadtort.Tor or Stadtort.Umland;

    // ═════════════════════════════════════════════════════════════════════════
    //  Orte und Strecken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Knoten im Gassennetz, an dem ein Aufenthalt beginnt oder endet (Haustür, Hof der Schänke, Domtür …).</summary>
    private int Anlaufknoten(Aufenthalt a)
    {
        Vector3 punkt = a.Ort switch
        {
            Stadtort.Heim when _haustueren.TryGetValue(a.Haus, out var tuer) => _netz.Knoten[tuer],
            Stadtort.Schaenke or Stadtort.Kirche when a.Betrieb is { } b && Stadtplan.TryGetValue(b, out var p) => p + new Vector3(0, 0, 5.5f),
            Stadtort.Kirche => DomTuer,
            Stadtort.Rathaus => RathausTuer,
            Stadtort.Tor => Osttor,
            Stadtort.Umland when a.Stelle is { } s && Umlandstellen.TryGetValue(s, out var u) => u,
            _ => Marktplatz,
        };
        return _netz.KnotenBei(punkt);
    }

    /// <summary>
    /// Die Strecke eines Weges: vom Platz, an dem er stand (Verweilpunkt oder Tür), über das Gassennetz zum neuen Platz.
    /// </summary>
    private Vector3[] Strecke(Weg weg, Einwohner e)
    {
        var pfad = _netz.Pfad(Anlaufknoten(weg.Von), Anlaufknoten(weg.Nach)).ToList();
        if (pfad.Count == 0) return Array.Empty<Vector3>();
        if (Draussen(weg.Von)) pfad.Insert(0, Verweilpunkt(weg.Von, e, weg.Abmarsch));
        if (Draussen(weg.Nach)) pfad.Add(Verweilpunkt(weg.Nach, e, weg.Ankunft));
        return pfad.ToArray();
    }

    private Vector3 Verweilpunkt(Aufenthalt a, Einwohner e) =>
        // Beim Ziel gilt die Ankunftszeit des letzten Weges, sonst (vor dem Abmarsch) dessen Abmarsch.
        Verweilpunkt(a, e, e.Weg is { } w ? (a == w.Nach ? w.Ankunft : w.Abmarsch) : 0);

    /// <summary>
    /// Wo genau jemand am Markt, in der Gasse, am Tor oder im Umland steht – fest aus Nummer und Zeitpunkt
    /// (kein Würfel, also bei jedem Bild und jedem Laden gleich).
    /// </summary>
    private Vector3 Verweilpunkt(Aufenthalt a, Einwohner e, long zeitpunkt)
    {
        var zahl = Streuung(e.Nummer, zeitpunkt);
        float Teil(int verschiebung) => ((zahl >> verschiebung) & 0xFFFF) / 65535f; // 0 … 1
        return a.Ort switch
        {
            // Markt: zwischen den Ständen (Marktplatz 24 × 24 m, Brunnen in der Mitte frei lassen)
            Stadtort.Markt => RingUmMitte(Marktplatz, 3f, 10.5f, Teil(0), Teil(16)),
            // Tor: vor dem Osttor neben der Straße
            Stadtort.Tor => Osttor + new Vector3(3 + Teil(0) * 4, 0, (Teil(16) < 0.5f ? -1 : 1) * (3 + Teil(8) * 2)),
            // Umland: auf der Weide, auf dem Feld, zwischen den Bäumen
            Stadtort.Umland when a.Stelle is { } s && Umlandstellen.TryGetValue(s, out var u) =>
                u + new Vector3((Teil(0) - 0.5f) * 11, 0, (Teil(16) - 0.5f) * 7),
            // Gasse: irgendwo auf einer Gasse in der Stadt
            _ => PunktInDerGasse(Teil(0), Teil(16)),
        };
    }

    private static Vector3 RingUmMitte(Vector3 mitte, float innen, float aussen, float a, float b)
    {
        var winkel = a * Mathf.Tau;
        var radius = innen + b * (aussen - innen);
        return mitte + new Vector3(Mathf.Cos(winkel) * radius, 0, Mathf.Sin(winkel) * radius);
    }

    private List<(Vector3 Von, Vector3 Bis)>? _gassenInDerStadt;

    private Vector3 PunktInDerGasse(float wahl, float stelle)
    {
        _gassenInDerStadt ??= _netz.Kanten.Where(k => InDerStadt(k.Von) && InDerStadt(k.Bis) && k.Von.DistanceTo(k.Bis) > 3
                                                 && k.Von.Length() > 13 && k.Bis.Length() > 13).ToList(); // nicht auf dem Markt
        if (_gassenInDerStadt.Count == 0) return Marktplatz;
        var (von, bis) = _gassenInDerStadt[(int)(wahl * (_gassenInDerStadt.Count - 1))];
        return von.Lerp(bis, 0.15f + stelle * 0.7f);
    }

    /// <summary>Feste „Zufallszahl“ aus Nummer und Zeitpunkt (Hash-Mischung, in .NET bei jedem Start gleich).</summary>
    private static uint Streuung(int nummer, long zeitpunkt)
    {
        var x = (uint)nummer * 2654435761u ^ (uint)zeitpunkt * 40503u;
        x ^= x >> 15;
        x *= 2246822519u;
        x ^= x >> 13;
        return x;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Figur
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Größe und Breite nach Volk: Zwerge klein und breit, Halblinge klein, Elben groß und schlank.</summary>
    private static Vector3 Gestalt(Volk volk) => volk switch
    {
        Volk.Zwerg => new Vector3(1.2f, 0.78f, 1.2f),
        Volk.Halbling => new Vector3(0.78f, 0.7f, 0.78f),
        Volk.Elb => new Vector3(0.85f, 1.1f, 0.85f),
        _ => Vector3.One,
    };

    private Passant PassantBauen(Einwohner e)
    {
        var knoten = new Node3D { Name = $"Einwohner_{e.Nummer}", Visible = false };
        _passantenEbene!.AddChild(knoten);
        var koerper = new Node3D { Scale = Gestalt(e.Volk) };
        knoten.AddChild(koerper);
        var kleidung = Gewandfarben[e.Nummer * 7 % Gewandfarben.Length];
        koerper.AddChild(new MeshInstance3D { Mesh = Rumpf, MaterialOverride = Stoff(kleidung), Position = new Vector3(0, 0.65f, 0) });
        koerper.AddChild(new MeshInstance3D { Mesh = Kopf, MaterialOverride = Stoff(Hautfarbe), Position = new Vector3(0, 1.5f, 0) });
        var versatz = ((e.Nummer * 37 % 9) - 4) * 0.22f; // −0,9 … +0,9 m
        return new Passant { Knoten = knoten, Koerper = koerper, Versatz = versatz, Gehtempo = Gehtempo(e) };
    }

    /// <summary>
    /// Gehtempo in Metern je echter Sekunde (bei Tempo 1): ein Viertel schlendert (1,5), gut die Hälfte geht normal (2,5),
    /// ein Fünftel ist eilig (3,5) – je ±10 %, fest nach der Nummer. Boten eilen immer, Bettler, Witwen und Witwer schlendern.
    /// </summary>
    private static float Gehtempo(Einwohner e)
    {
        var zahl = Streuung(e.Nummer, 977);
        var wahl = (zahl & 0xFFFF) / 65535f;
        var feinheit = 0.9f + ((zahl >> 16) & 0xFFFF) / 65535f * 0.2f;
        var grund = e.Rolle switch
        {
            "Bote" => 3.5f,
            "Bettler" or "Witwer" => 1.5f,
            _ => wahl < 0.25f ? 1.5f : wahl < 0.8f ? 2.5f : 3.5f,
        };
        return grund * feinheit;
    }
}
