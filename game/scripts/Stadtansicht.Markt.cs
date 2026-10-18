using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Marktplatz (Teil der <c>partial class Stadtansicht</c>): <c>marktplatz.glb</c> mit Pflaster, Brunnen, Marktkreuz und Bänken,
/// auf jedem „Anker_Stand&lt;n&gt;“ ein Stand aus <c>marktstaende.glb</c> (PR #143).
///
/// Auslagen: Ein Stand zeigt die Waren eines Handwerks, das in den letzten 17 Spielstunden etwas auf den Markt gebracht hat
/// (<see cref="Marktstand.SpielerAngebotImJahr"/> – Verkäufe der Spieler und KI-Häuser; die Grundversorgung der Stadt zählt nicht).
/// Ein Stand behält seine Ware, solange sie da ist; neue Ware bekommt den nächsten freien Stand. Hinter jedem belegten Stand steht
/// ein Verkäufer, zur Arbeitszeit.
///
/// Stellen: Einwohner auf dem Markt stehen an den Kundenplätzen der belegten Stände, plaudern in kleinen Runden, holen Wasser am Brunnen,
/// sitzen auf den Bänken oder schlendern den Rundgang entlang (Anker des Modells). Jede Stelle hat höchstens einen Einwohner.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>
    /// Ein Platz auf dem Markt: Ort, Blickrichtung (Drehung um die Hochachse, vorne +Z) und Bewegung. <paramref name="Stand"/> ist der Stand
    /// eines Kundenplatzes, <paramref name="Gruppe"/> die Plauderrunde, <paramref name="Rundgang"/> der Wegpunkt, an dem ein Schlendernder beginnt.
    /// </summary>
    private sealed record Marktstelle(Vector3 Ort, float Blick, string Bewegung, int Stand = -1, int Gruppe = -1, int Rundgang = -1);

    /// <summary>Ein Stand auf dem Platz: das Modell, seine Auslagen und der Verkäufer.</summary>
    private sealed class StandAnsicht
    {
        public required Node3D Wurzel { get; init; }
        public Dictionary<string, MeshInstance3D> Auslagen { get; } = new();
        /// <summary>Welche Auslage der Stand hat (null = leerer Stand) und welche gerade zu sehen ist (nachts keine).</summary>
        public string? Auslage { get; set; }
        public string? Gezeigt { get; set; } = "";
        public Node3D? VerkaeuferKnoten { get; set; }
        public Figur? Verkaeufer { get; set; }
        /// <summary>Die Stellen der Kunden vor dem Stand (Indizes in <see cref="_marktstellen"/>).</summary>
        public List<int> Kunden { get; } = new();
    }

    /// <summary>Die acht Auslagen des Baukastens, je ein Handwerk (die Rohstoffe aus dem Umland zusammen).</summary>
    private static readonly string[] AuslagenNamen = { "Eisen", "Holz", "Stein", "Tinktur", "Bier", "Kerzen", "Duft", "Rohstoffe" };

    /// <summary>Standform je Stellplatz: meist Tisch mit Plane, dazwischen offener Bock und Handkarren.</summary>
    private static readonly string[] StandFormen = { "Stand_Plane", "Stand_Bock", "Stand_Plane", "Stand_Karren", "Stand_Plane", "Stand_Plane", "Stand_Bock" };

    private static readonly Color[] Planenfarben =
    {
        new(0.72f, 0.62f, 0.38f), new(0.55f, 0.20f, 0.18f), new(0.25f, 0.38f, 0.30f), new(0.62f, 0.55f, 0.44f), new(0.30f, 0.34f, 0.52f),
    };

    /// <summary>So schnell schlendern die Leute den Rundgang entlang (Meter je echter Sekunde bei Tempo 1).</summary>
    private const float SchlenderTempo = 1.1f;

    private readonly List<StandAnsicht> _marktstaende = new();
    private readonly List<Marktstelle> _marktstellen = new();
    /// <summary>Wer an welcher Stelle steht oder dorthin unterwegs ist (Einwohnernummer, −1 = frei).</summary>
    private int[] _marktBelegt = Array.Empty<int>();
    /// <summary>Wegpunkte des Rundgangs und ihr Abstand vom ersten (Meter, entlang des Weges).</summary>
    private Vector3[] _rundgang = Array.Empty<Vector3>();
    private float[] _rundgangBis = Array.Empty<float>();
    private float _rundgangLaenge;
    private long _auslagenGeprueft = -1;
    private bool _marktOffen = true;
    private Dictionary<string, string>? _berufJeWare;

    private void Marktstaende()
    {
        if (ModellEinsetzen("marktplatz", this, Marktplatz, new ModellZustand()) is { } platz)
        {
            var anker = platz.Wurzel.GetChildren().OfType<Node3D>().Where(k => k.Name.ToString().StartsWith("Anker_"))
                             .ToDictionary(k => k.Name.ToString(), k => platz.Wurzel.Transform * k.Transform);
            for (var n = 1; anker.TryGetValue($"Anker_Stand{n}", out var lage); n++)
                StandAufbauen(lage, n - 1);
            foreach (var (name, lage) in anker.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var blick = Blickwinkel(lage);
                if (name.StartsWith("Anker_Brunnen")) _marktstellen.Add(new Marktstelle(lage.Origin, blick, "Stehen"));
                else if (name.StartsWith("Anker_Sitz")) _marktstellen.Add(new Marktstelle(lage.Origin, blick, "Sitzen"));
                else if (name.StartsWith("Anker_Plausch") && int.TryParse(name["Anker_Plausch".Length..].Split('_')[0], out var gruppe))
                    _marktstellen.Add(new Marktstelle(lage.Origin, blick, "Reden", Gruppe: gruppe));
            }
            // Rundgang: Anker_Weg1 … Anker_Weg12 in Laufrichtung; an jedem Wegpunkt beginnt ein Schlendernder
            _rundgang = Enumerable.Range(1, 99).Select(n => anker.TryGetValue($"Anker_Weg{n}", out var w) ? (Vector3?)w.Origin : null)
                                  .TakeWhile(w => w is not null).Select(w => w!.Value).ToArray();
            _rundgangBis = new float[_rundgang.Length];
            for (var i = 1; i < _rundgang.Length; i++) _rundgangBis[i] = _rundgangBis[i - 1] + _rundgang[i - 1].DistanceTo(_rundgang[i]);
            _rundgangLaenge = _rundgang.Length > 1 ? _rundgangBis[^1] + _rundgang[^1].DistanceTo(_rundgang[0]) : 0;
            for (var i = 0; i < _rundgang.Length && _rundgangLaenge > 0; i++)
                _marktstellen.Add(new Marktstelle(_rundgang[i], 0, "Gehen", Rundgang: i));
        }
        _marktBelegt = Enumerable.Repeat(-1, _marktstellen.Count).ToArray();
        Klickflaeche("Marktplatz", new Vector3(0, 1, 0), new Vector3(22, 2, 22));
        Beschriftung("Marktplatz", new Vector3(0, 6, 0), 56, kennung: "Marktplatz");
    }

    /// <summary>Blickrichtung eines Ankers als Drehung einer Figur: Anker blicken nach −Z, Figuren nach +Z.</summary>
    private static float Blickwinkel(Transform3D lage)
    {
        var blick = -lage.Basis.Z;
        return Mathf.Atan2(blick.X, blick.Z);
    }

    /// <summary>Ein Stand: Standform und Planenfarbe nach dem Stellplatz, Kundenplätze, Verkäufer. Bis die Simulation läuft
    /// (Hauptmenü), liegt auf jedem Stand eine andere Auslage.</summary>
    private void StandAufbauen(Transform3D lage, int nummer)
    {
        if (ModellEinsetzen("marktstaende", this, lage.Origin, new ModellZustand()) is not { } modell) return;
        modell.Wurzel.Basis = lage.Basis;
        var stand = new StandAnsicht { Wurzel = modell.Wurzel };
        var form = StandFormen[nummer % StandFormen.Length];
        foreach (var teil in modell.Wurzel.GetChildren().OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            if (name.StartsWith("Auslage_"))
            {
                stand.Auslagen[name["Auslage_".Length..]] = teil;
                teil.Visible = false;
                continue;
            }
            teil.Visible = name == form;
            if (name != form) continue;
            for (var i = 0; i < teil.Mesh.GetSurfaceCount(); i++)
                if (teil.Mesh.SurfaceGetMaterial(i) is BaseMaterial3D { ResourceName: "Plane" } plane)
                    teil.SetSurfaceOverrideMaterial(i, Umgefaerbt(plane, Planenfarben[nummer % Planenfarben.Length]));
        }
        foreach (var k in modell.Wurzel.GetChildren().OfType<Node3D>().Where(k => k.Name.ToString().StartsWith("Anker_Kunde")))
        {
            var kunde = lage * k.Transform;
            stand.Kunden.Add(_marktstellen.Count);
            _marktstellen.Add(new Marktstelle(kunde.Origin, Blickwinkel(kunde), "Stehen", Stand: nummer));
        }
        if (modell.Wurzel.GetNodeOrNull<Node3D>("Anker_Verkaeufer") is { } v)
        {
            var hinten = lage * v.Transform;
            stand.VerkaeuferKnoten = new Node3D { Name = $"Verkaeufer_{nummer + 1}", Position = hinten.Origin, Rotation = new Vector3(0, Blickwinkel(hinten), 0) };
            AddChild(stand.VerkaeuferKnoten);
            stand.Verkaeufer = FigurEinsetzen(stand.VerkaeuferKnoten, AussehenAusNummer(nummer * 5 + 2, schuerze: true), 1000 + nummer);
        }
        _marktstaende.Add(stand);
        stand.Auslage = AuslagenNamen[nummer % AuslagenNamen.Length];
        StandZeigen(stand, true);
    }

    /// <summary>Legt die Auslage des Stands auf und stellt den Verkäufer dahinter – oder räumt beides weg. Nur bei einer Änderung.</summary>
    private static void StandZeigen(StandAnsicht stand, bool offen)
    {
        var auslage = offen ? stand.Auslage : null;
        if (stand.Gezeigt == auslage) return;
        stand.Gezeigt = auslage;
        foreach (var (name, teil) in stand.Auslagen) teil.Visible = name == auslage;
        if (stand.VerkaeuferKnoten is { } knoten) knoten.Visible = auslage is not null;
    }

    /// <summary>Jedes Bild: alle zehn Spielminuten die Auslagen prüfen; Auslagen und Verkäufer nur zur Arbeitszeit.</summary>
    private void MarktAktualisieren(double minuteGenau, double sekunden)
    {
        if (_simulation?.Wirtschaft is not { } w) return;
        var zehntel = (long)Math.Floor(minuteGenau) / 10;
        if (zehntel != _auslagenGeprueft)
        {
            _auslagenGeprueft = zehntel;
            AuslagenVerteilen(w);
        }
        _marktOffen = w.IstArbeitszeit(_simulation.Zeit);
        foreach (var stand in _marktstaende)
        {
            StandZeigen(stand, _marktOffen);
            if (stand.Gezeigt is null || stand.VerkaeuferKnoten is not { } knoten || stand.Verkaeufer is not { } verkaeufer) continue;
            // Steht ein Kunde da, redet der Verkäufer mit ihm.
            BewegungSetzen(verkaeufer, stand.Kunden.Any(k => _marktBelegt[k] >= 0) ? "Reden" : "Stehen");
            FigurImBildBewegen(verkaeufer, knoten.Position, sekunden);
        }
    }

    /// <summary>
    /// Welche Handwerke gerade Ware auf dem Markt haben (Verkäufe der letzten 17 Spielstunden, nach Wert): Ein Stand behält seine
    /// Auslage, solange sie da ist; was neu kommt, bekommt den nächsten freien Stand. Gibt es mehr Handwerke als Stände, fehlt das kleinste.
    /// </summary>
    private void AuslagenVerteilen(Stadtwirtschaft w)
    {
        _berufJeWare ??= w.Daten.Betriebsarten.Values.OrderBy(a => a.Beruf, StringComparer.Ordinal)
                          .SelectMany(a => a.Produkte.Select(ware => (ware, a.Beruf)))
                          .GroupBy(x => x.ware).ToDictionary(g => g.Key, g => g.First().Beruf);
        var wert = new Dictionary<string, double>();
        foreach (var s in w.Markt.Staende)
        {
            if (s.SpielerAngebotImJahr < 1 || AuslageVon(s) is not { } auslage) continue;
            wert[auslage] = wert.GetValueOrDefault(auslage) + s.SpielerAngebotImJahr * s.Grundpreis;
        }
        var vorher = string.Join(", ", _marktstaende.Select(s => s.Auslage ?? "–"));
        foreach (var stand in _marktstaende.Where(s => s.Auslage is not null && !wert.ContainsKey(s.Auslage)))
            stand.Auslage = null;
        foreach (var auslage in wert.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key))
        {
            if (_marktstaende.Any(s => s.Auslage == auslage)) continue;
            if (_marktstaende.FirstOrDefault(s => s.Auslage is null) is not { } frei) break;
            frei.Auslage = auslage;
        }
        var nachher = string.Join(", ", _marktstaende.Select(s => s.Auslage ?? "–"));
        if (nachher != vorher) GD.Print($"Markt: Stände {nachher} ({string.Join(", ", wert.Select(x => $"{x.Key} {x.Value:0} Gulden"))})");
    }

    /// <summary>Welche Auslage zu einer Ware gehört: Rohstoffe zusammen, sonst nach dem Handwerk, das sie herstellt.</summary>
    private string? AuslageVon(Marktstand s) => s.Typ == "Rohstoff" ? "Rohstoffe" : _berufJeWare!.GetValueOrDefault(s.Ware) switch
    {
        "Schmied" => "Eisen",
        "Tischler" => "Holz",
        "Steinmetz" => "Stein",
        "Alchimist" => "Tinktur",
        "Wirt" => "Bier",
        "Prediger" => "Kerzen",
        "Parfumeur" => "Duft",
        _ => null,
    };

    /// <summary>Halbe Seitenlänge des Marktplatzes (Pflaster des Modells, 24 × 24 m).</summary>
    private const float MarktplatzHalb = 12f;

    /// <summary>
    /// Die Teile eines Weges außerhalb des Marktplatzes (Liang-Barsky-Schnitt mit dem Quadrat), damit kein Weg über das Pflaster gezogen
    /// wird. Ein Wegstück ragt um ein Viertel seiner Breite über seine Enden hinaus, darum ist das Quadrat entsprechend größer.
    /// Null, wenn der Weg den Platz nicht berührt.
    /// </summary>
    private static List<(Vector3 Von, Vector3 Bis)>? OhneMarktplatz(Vector3 von, Vector3 bis, float breite)
    {
        var halb = MarktplatzHalb + breite * 0.25f;
        var d = bis - von;
        float t0 = 0, t1 = 1;
        foreach (var (p, q) in new[] { (-d.X, von.X - Marktplatz.X + halb), (d.X, Marktplatz.X + halb - von.X),
                                       (-d.Z, von.Z - Marktplatz.Z + halb), (d.Z, Marktplatz.Z + halb - von.Z) })
        {
            if (Mathf.IsZeroApprox(p)) { if (q < 0) return null; continue; }
            var t = q / p;
            if (p < 0) t0 = Mathf.Max(t0, t);
            else t1 = Mathf.Min(t1, t);
        }
        if (t0 >= t1) return null;
        var stuecke = new List<(Vector3, Vector3)>();
        if (t0 * d.Length() > 0.01f) stuecke.Add((von, von + d * t0));
        if ((1 - t1) * d.Length() > 0.01f) stuecke.Add((von + d * t1, bis));
        return stuecke;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stellen für die Einwohner
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht eine freie Stelle für einen Einwohner, fest aus Nummer und Zeitpunkt (kein Würfel), und belegt sie. Meist gesellt er sich
    /// zu einer Plauderrunde, in der schon jemand steht – allein redet keiner gern. Kundenplätze gibt es nur an belegten Ständen.
    /// −1, wenn der Markt voll ist (dann steht er irgendwo zwischen den Ständen).
    /// </summary>
    private int MarktstelleBelegen(int nummer, long zeitpunkt)
    {
        var anzahl = _marktstellen.Count;
        if (anzahl == 0) return -1;
        var zahl = Streuung(nummer, zeitpunkt);
        var gewaehlt = -1;
        if ((zahl & 3) != 0)
            for (var k = 0; k < anzahl && gewaehlt < 0; k++)
                if (_marktstellen[k].Gruppe is var g and >= 0 && MarktstelleFrei(k)
                    && _marktstellen.Select((s, i) => (s, i)).Any(x => x.s.Gruppe == g && _marktBelegt[x.i] >= 0))
                    gewaehlt = k;
        for (var i = 0; i < anzahl && gewaehlt < 0; i++)
        {
            var k = (int)((zahl >> 8) % (uint)anzahl + i) % anzahl;
            if (MarktstelleFrei(k)) gewaehlt = k;
        }
        if (gewaehlt >= 0) _marktBelegt[gewaehlt] = nummer;
        return gewaehlt;
    }

    private bool MarktstelleFrei(int k) =>
        _marktBelegt[k] < 0 && (_marktstellen[k].Stand < 0 || _marktOffen && _marktstaende[_marktstellen[k].Stand].Auslage is not null);

    /// <summary>Gibt die Stelle wieder frei (nur, wenn dieser Einwohner sie hat).</summary>
    private void MarktstelleRaeumen(int k, int nummer)
    {
        if (k >= 0 && k < _marktBelegt.Length && _marktBelegt[k] == nummer) _marktBelegt[k] = -1;
    }

    private bool AufDemRundgang(int k) => k >= 0 && _marktstellen[k].Rundgang >= 0;

    /// <summary>
    /// Wo eine Stelle zur Spielminute <paramref name="minute"/> liegt und wohin man dort blickt. Wer schlendert, geht den Rundgang entlang:
    /// alle im Gleichschritt, jeder von seinem Wegpunkt aus versetzt – so holt keiner den anderen ein.
    /// </summary>
    private (Vector3 Ort, float Blick) MarktstelleLage(int k, double minute)
    {
        var s = _marktstellen[k];
        if (s.Rundgang < 0) return (s.Ort, s.Blick);
        var sekundenJeMinute = _simulation is null ? 1 : _simulation.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        var weit = (float)((_rundgangBis[s.Rundgang] + SchlenderTempo * sekundenJeMinute * minute) % _rundgangLaenge);
        var i = Array.FindLastIndex(_rundgangBis, b => b <= weit);
        var von = _rundgang[i];
        var bis = _rundgang[(i + 1) % _rundgang.Length];
        var stueck = (i + 1 < _rundgangBis.Length ? _rundgangBis[i + 1] : _rundgangLaenge) - _rundgangBis[i];
        var richtung = bis - von;
        return (von.Lerp(bis, stueck > 0 ? (weit - _rundgangBis[i]) / stueck : 0), Mathf.Atan2(richtung.X, richtung.Z));
    }
}
