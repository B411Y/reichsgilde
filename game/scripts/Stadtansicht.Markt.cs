using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Marktplatz (Teil der <c>partial class Stadtansicht</c>): das Modell des Bausteins „markt“ (<c>marktplatz.glb</c>) mit Pflaster, Brunnen,
/// Marktkreuz und Bänken an seiner Lage in der Karte (auch gedreht), auf jedem „Anker_Stand&lt;n&gt;“ ein Stand aus <c>marktstaende.glb</c> (PR #143).
/// Wo das Pflaster liegt, rechnet die Ansicht in Koordinaten des Platzes (<see cref="MarktLokal"/>).
///
/// Auslagen: Ein Stand zeigt die Waren eines Handwerks. Zuerst bekommen die einen Stand, die in den letzten 17 Spielstunden etwas auf den
/// Markt gebracht haben (<see cref="Marktstand.SpielerAngebotImJahr"/> – Verkäufe der Spieler und KI-Häuser), die übrigen Stände füllt die
/// Grundversorgung der Stadt (PR #148). Ein Stand behält seine Ware, solange sie da ist. Hinter jedem belegten Stand steht
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
    /// <summary>Was auf dem Pflaster im Weg steht (Brunnen, Marktkreuz, Bänke, Stände): Mitte und Radius in Metern, ohne Abstand für die Figur.</summary>
    private readonly List<(Vector3 Mitte, float Radius)> _marktHindernisse = new();

    private void Marktstaende(Kartengebaeude markt)
    {
        if (markt.Baustein?.Modell is { } datei && ModellEinsetzen(datei, this, Vector3.Zero, new ModellZustand()) is { } platz)
        {
            platz.Wurzel.Transform = _marktLage;
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
            MarktHindernisseSammeln(platz.Wurzel, anker);
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
        // Klickfläche (das Pflaster ohne Rand) und Name: Die Kennung „Marktplatz“ öffnet die Preistafel (Hauptszene)
        var mitte = new Node3D { Name = "Markt", Transform = new Transform3D(_marktLage.Basis.Orthonormalized(), _markt + new Vector3(0, _marktY, 0)) };
        AddChild(mitte);
        Klickflaeche("Marktplatz", new Vector3(0, 1, 0), new Vector3(2 * _marktHalb - 2, 2, 2 * _marktHalb - 2), mitte);
        Beschriftung(markt.Name, new Vector3(0, 6, 0), 56, mitte, "Marktplatz");
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
        if (ModellEinsetzen("gebaeude/marktstaende", this, lage.Origin, new ModellZustand()) is not { } modell) return;
        modell.Wurzel.Basis = lage.Basis;
        var stand = new StandAnsicht { Wurzel = modell.Wurzel };
        var form = StandFormen[nummer % StandFormen.Length];
        // Hindernis: Tisch 2,8 × 1,4 m samt Pfosten und Plane als drei Kreise längs des Tisches; der Handkarren hat hinten eine Deichsel
        foreach (var dx in new[] { -1f, 0f, 1f })
            _marktHindernisse.Add((lage * new Vector3(dx, 0, 0), 0.95f));
        if (form == "Stand_Karren") _marktHindernisse.Add((lage * new Vector3(-1.9f, 0, 0), 0.5f));
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
            // Beim ersten Mal die Platzhalter des Hauptmenüs wegräumen: Die Stände werden nach Wert belegt, nicht nach Nummer
            if (_auslagenGeprueft < 0) foreach (var stand in _marktstaende) stand.Auslage = null;
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
    /// Welche Waren auf den Ständen liegen. Zuerst bekommen Handwerke einen Stand, die in den letzten 17 Spielstunden etwas auf den Markt gebracht
    /// haben (Verkäufe der Häuser, nach Wert); dann füllt die Grundversorgung der Stadt (Gesamtangebot ohne die Verkäufe der Häuser, nach Wert)
    /// die übrigen Stände. Ein Stand behält seine Auslage, solange sie da ist; eine verkaufte Ware verdrängt bei Bedarf die kleinste
    /// Grundversorgung. Bei sieben Ständen und acht Warengruppen fehlt am ersten Tag meist die kleinste.
    /// </summary>
    private void AuslagenVerteilen(Stadtwirtschaft w)
    {
        _berufJeWare ??= w.Daten.Betriebsarten.Values.OrderBy(a => a.Beruf, StringComparer.Ordinal)
                          .SelectMany(a => a.Produkte.Select(ware => (ware, a.Beruf)))
                          .GroupBy(x => x.ware).ToDictionary(g => g.Key, g => g.First().Beruf);
        var verkauft = new Dictionary<string, double>();   // Verkäufe der Häuser
        var versorgung = new Dictionary<string, double>(); // Grundversorgung der Stadt
        foreach (var s in w.Markt.Staende)
        {
            if (AuslageVon(s) is not { } auslage) continue;
            if (s.SpielerAngebotImJahr >= 1) verkauft[auslage] = verkauft.GetValueOrDefault(auslage) + s.SpielerAngebotImJahr * s.Grundpreis;
            var grund = s.Gesamtangebot - s.SpielerAngebotImJahr;
            if (grund > 0) versorgung[auslage] = versorgung.GetValueOrDefault(auslage) + grund * s.Grundpreis;
        }
        var vorher = string.Join(", ", _marktstaende.Select(s => s.Auslage ?? "–"));
        foreach (var stand in _marktstaende.Where(s => s.Auslage is not null && !verkauft.ContainsKey(s.Auslage) && !versorgung.ContainsKey(s.Auslage)))
            stand.Auslage = null;
        // Verkaufte Ware zuerst: freier Stand, sonst der mit der kleinsten Grundversorgung (nicht der eines anderen Verkaufs)
        foreach (var auslage in verkauft.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key))
        {
            if (_marktstaende.Any(s => s.Auslage == auslage)) continue;
            var platz = _marktstaende.FirstOrDefault(s => s.Auslage is null)
                        ?? _marktstaende.Where(s => !verkauft.ContainsKey(s.Auslage!))
                                        .OrderBy(s => versorgung.GetValueOrDefault(s.Auslage!)).FirstOrDefault();
            if (platz is null) break;
            platz.Auslage = auslage;
        }
        // Die übrigen Stände füllt die Grundversorgung, die größte zuerst
        foreach (var auslage in versorgung.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key))
        {
            if (_marktstaende.Any(s => s.Auslage == auslage)) continue;
            if (_marktstaende.FirstOrDefault(s => s.Auslage is null) is not { } frei) break;
            frei.Auslage = auslage;
        }
        var nachher = string.Join(", ", _marktstaende.Select(s => s.Auslage ?? "–"));
        if (nachher != vorher)
            GD.Print($"Markt: Stände {nachher} (verkauft: {string.Join(", ", verkauft.Select(x => $"{x.Key} {x.Value:0}"))}; Grundversorgung: {string.Join(", ", versorgung.Select(x => $"{x.Key} {x.Value:0}"))} Gulden)");
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

    /// <summary>
    /// Die Teile eines Weges außerhalb des Marktplatzes (Liang-Barsky-Schnitt mit dem Quadrat), damit kein Weg über das Pflaster gezogen
    /// wird. Ein Wegstück ragt um ein Viertel seiner Breite über seine Enden hinaus, darum ist das Quadrat entsprechend größer.
    /// Null, wenn der Weg den Platz nicht berührt.
    /// </summary>
    private List<(Vector3 Von, Vector3 Bis)>? OhneMarktplatz(Vector3 von, Vector3 bis, float breite)
    {
        if (_marktHalb <= 0 || QuadratSchnitt(MarktLokal(von), MarktLokal(bis), _marktHalb + breite * 0.25f) is not { } schnitt) return null;
        var (t0, t1) = schnitt;
        var d = bis - von;
        var stuecke = new List<(Vector3, Vector3)>();
        if (t0 * d.Length() > 0.01f) stuecke.Add((von, von + d * t0));
        if ((1 - t1) * d.Length() > 0.01f) stuecke.Add((von + d * t1, bis));
        return stuecke;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wege über das Pflaster
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>So viel Abstand (Meter) halten Figuren zu Brunnen, Kreuz, Bänken und Ständen.</summary>
    private const float Wegrand = 0.3f;

    /// <summary>
    /// Sammelt, was auf dem Pflaster im Weg steht, aus dem Modell: den Brunnen (höchste Punkte des Pflasters nahe der Mitte, mit Eimer),
    /// die Bänke (je zwei Sitzanker; drei Kreise längs der Bank) und das Marktkreuz (Lage aus <c>tools/modelle/markt.py</c>, ein Anker fehlt).
    /// Die Stände kommen in <see cref="StandAufbauen"/> dazu.
    /// </summary>
    private void MarktHindernisseSammeln(Node3D wurzel, Dictionary<string, Transform3D> anker)
    {
        var brunnen = 0f;
        foreach (var teil in wurzel.GetChildren().OfType<MeshInstance3D>())
        {
            var lage = wurzel.Transform * teil.Transform;
            foreach (var ecke in teil.Mesh.GetFaces())
            {
                var p = lage * ecke;
                var abstand = new Vector2(p.X - _markt.X, p.Z - _markt.Z).Length();
                if (p.Y > 0.1f && abstand < 4f) brunnen = Mathf.Max(brunnen, abstand);
            }
        }
        _marktHindernisse.Add((_markt, Mathf.Max(brunnen, 2f)));
        _marktHindernisse.Add((_marktLage * new Vector3(-10.4f, 0, 10.4f), 1.0f)); // das Marktkreuz (Lage im Modell)
        for (var n = 1; anker.TryGetValue($"Anker_Sitz{2 * n - 1}", out var a) && anker.TryGetValue($"Anker_Sitz{2 * n}", out var b); n++)
        {
            var mitte = (a.Origin + b.Origin) / 2;
            var achse = (b.Origin - a.Origin).Normalized();
            foreach (var d in new[] { -0.9f, 0f, 0.9f })
                _marktHindernisse.Add((new Vector3(mitte.X, 0, mitte.Z) + achse * d, 0.6f));
        }
    }

    /// <summary>Liegt der Punkt auf dem Pflaster des Marktplatzes?</summary>
    private bool AufDemPflaster(Vector3 p)
    {
        if (_marktHalb <= 0) return false;
        var lokal = MarktLokal(p);
        return Mathf.Abs(lokal.X) <= _marktHalb && Mathf.Abs(lokal.Z) <= _marktHalb;
    }

    /// <summary>Der Teil der Strecke von <paramref name="a"/> nach <paramref name="b"/> (Anteile 0 bis 1), der auf dem Pflaster liegt. Null, wenn keiner.</summary>
    private (float Von, float Bis)? ImPflaster(Vector3 a, Vector3 b) =>
        _marktHalb > 0 ? QuadratSchnitt(MarktLokal(a), MarktLokal(b), _marktHalb) : null;

    /// <summary>
    /// Wo die Strecke a–b (Koordinaten des Platzes) das Quadrat mit der halben Seite <paramref name="halb"/> um den Ursprung betritt und verlässt
    /// (Anteile 0 bis 1, Liang-Barsky). Null, wenn sie es nicht berührt. Die Anteile gelten auch für dieselbe Strecke in Koordinaten der Stadt.
    /// </summary>
    private static (float Von, float Bis)? QuadratSchnitt(Vector3 a, Vector3 b, float halb)
    {
        var d = b - a;
        float t0 = 0, t1 = 1;
        foreach (var (p, q) in new[] { (-d.X, a.X + halb), (d.X, halb - a.X), (-d.Z, a.Z + halb), (d.Z, halb - a.Z) })
        {
            if (Mathf.IsZeroApprox(p)) { if (q < 0) return null; continue; }
            var t = q / p;
            if (p < 0) t0 = Mathf.Max(t0, t);
            else t1 = Mathf.Min(t1, t);
        }
        return t0 < t1 ? (t0, t1) : null;
    }

    /// <summary>
    /// Führt einen Weg über das Pflaster des Marktplatzes so, wie man wirklich geht: Wer hineingeht, betritt den Platz dort, wo der Weg das Pflaster
    /// trifft, und geht von da direkt zu seinem Ziel; wer hinausgeht, geht direkt zum Rand; von Stelle zu Stelle direkt. Brunnen, Kreuz, Bänke und
    /// Stände umgeht er (<see cref="PlatzZwischenpunkte"/>). Wege, die nur durchs Netz über die Mitte führen (Knoten am Brunnen), laufen so um den Brunnen
    /// herum. Der erste und letzte Punkt des Weges bleiben, wie sie sind.
    /// </summary>
    private List<Vector3> UeberDenPlatz(IReadOnlyList<Vector3> pfad)
    {
        var aus = new List<Vector3>();
        if (pfad.Count == 0) return aus;
        var drin = AufDemPflaster(pfad[0]);
        var eintritt = pfad[0]; // wo der Weg das Pflaster betritt (oder dort beginnt)
        aus.Add(pfad[0]);
        for (var i = 0; i + 1 < pfad.Count; i++)
        {
            var a = pfad[i];
            var b = pfad[i + 1];
            var abschnitt = ImPflaster(a, b);
            var aDrin = AufDemPflaster(a);
            var bDrin = AufDemPflaster(b);
            if (!aDrin && !bDrin)
            {
                // streift nur eine Ecke des Platzes
                if (abschnitt is { } stueck && (stueck.Bis - stueck.Von) * a.DistanceTo(b) > 0.05f)
                {
                    var x1 = a.Lerp(b, stueck.Von);
                    var x2 = a.Lerp(b, stueck.Bis);
                    aus.Add(x1);
                    aus.AddRange(PlatzZwischenpunkte(x1, x2));
                    aus.Add(x2);
                }
                aus.Add(b);
            }
            else if (!aDrin)
            {
                eintritt = a.Lerp(b, abschnitt?.Von ?? 1);
                aus.Add(eintritt);
                drin = true;
            }
            else if (!bDrin)
            {
                var austritt = a.Lerp(b, abschnitt?.Bis ?? 0);
                aus.AddRange(PlatzZwischenpunkte(eintritt, austritt));
                aus.Add(austritt);
                aus.Add(b);
                drin = false;
            }
        }
        if (drin)
        {
            aus.AddRange(PlatzZwischenpunkte(eintritt, pfad[^1]));
            if (aus.Count == 0 || aus[^1] != pfad[^1]) aus.Add(pfad[^1]);
        }
        return aus;
    }

    /// <summary>
    /// Die Punkte zwischen <paramref name="von"/> und <paramref name="bis"/> (ohne diese beiden), mit denen man Brunnen, Kreuz, Bänke und Stände
    /// umgeht: Die Gerade, wenn nichts im Weg steht, sonst am ersten Hindernis um den Kreis herum (Tangente, ein Stück Kreisbogen, Tangente) auf der kürzeren Seite.
    /// Steht ein Ende dicht am Hindernis (Brunnenplatz, Kundenplatz), schrumpft der Kreis bis dorthin.
    /// </summary>
    private List<Vector3> PlatzZwischenpunkte(Vector3 von, Vector3 bis, int tiefe = 0)
    {
        var punkte = new List<Vector3>();
        if (tiefe > 4) return punkte;
        var d = new Vector3(bis.X - von.X, 0, bis.Z - von.Z);
        var laenge2 = d.LengthSquared();
        if (laenge2 < 1e-4f) return punkte;
        (Vector3 Mitte, float Radius)? erstes = null;
        var ersteT = float.MaxValue;
        foreach (var (mitte, radius) in _marktHindernisse)
        {
            var m = new Vector3(mitte.X, 0, mitte.Z);
            var abVon = new Vector3(von.X, 0, von.Z).DistanceTo(m);
            var abBis = new Vector3(bis.X, 0, bis.Z).DistanceTo(m);
            var r = Mathf.Min(radius + Wegrand, Mathf.Min(abVon, abBis) - 0.05f);
            if (r < 0.3f) continue;
            var t = Mathf.Clamp((m - new Vector3(von.X, 0, von.Z)).Dot(d) / laenge2, 0, 1);
            var q = new Vector3(von.X, 0, von.Z) + d * t;
            if (q.DistanceTo(m) >= r - 0.01f || t >= ersteT) continue;
            ersteT = t;
            erstes = (m, r);
        }
        if (erstes is not { } h) return punkte;

        // Um den Kreis herum: Tangentenpunkte von beiden Seiten, die kürzere Richtung nehmen
        Vector2 Auf(Vector3 v) => new(v.X - h.Mitte.X, v.Z - h.Mitte.Z);
        var wVon = Auf(von);
        var wBis = Auf(bis);
        var winkelVon = Mathf.Atan2(wVon.Y, wVon.X);
        var winkelBis = Mathf.Atan2(wBis.Y, wBis.X);
        var abwVon = Mathf.Acos(Mathf.Clamp(h.Radius / wVon.Length(), -1, 1));
        var abwBis = Mathf.Acos(Mathf.Clamp(h.Radius / wBis.Length(), -1, 1));
        var beste = (Laenge: float.MaxValue, Seite: 1, TVon: 0f, TBis: 0f, Spanne: 0f);
        foreach (var seite in new[] { 1, -1 })
        {
            var tVon = winkelVon + seite * abwVon;
            var tBis = winkelBis - seite * abwBis;
            var spanne = Mathf.PosMod(seite * (tBis - tVon), Mathf.Tau);
            var gesamt = Mathf.Sqrt(Mathf.Max(wVon.LengthSquared() - h.Radius * h.Radius, 0)) + h.Radius * spanne
                       + Mathf.Sqrt(Mathf.Max(wBis.LengthSquared() - h.Radius * h.Radius, 0));
            if (gesamt < beste.Laenge) beste = (gesamt, seite, tVon, tBis, spanne);
        }
        Vector3 PunktAmKreis(float winkel, float radius) => new(h.Mitte.X + Mathf.Cos(winkel) * radius, 0, h.Mitte.Z + Mathf.Sin(winkel) * radius);
        var ersterPunkt = PunktAmKreis(beste.TVon, h.Radius);
        var letzterPunkt = PunktAmKreis(beste.TBis, h.Radius);
        punkte.AddRange(PlatzZwischenpunkte(von, ersterPunkt, tiefe + 1));
        punkte.Add(ersterPunkt);
        var schritte = Mathf.CeilToInt(beste.Spanne / 0.6f);
        for (var k = 1; k < schritte; k++) // Sehnen des Kreisbogens berühren den Kreis nur, wenn die Zwischenpunkte weiter außen liegen
            punkte.Add(PunktAmKreis(beste.TVon + beste.Seite * beste.Spanne * k / schritte, h.Radius / Mathf.Cos(beste.Spanne / schritte / 2)));
        punkte.Add(letzterPunkt);
        punkte.AddRange(PlatzZwischenpunkte(letzterPunkt, bis, tiefe + 1));
        if (tiefe > 0) return punkte;
        // Punkte dicht hintereinander (von Kreisen, die sich überlappen) ergäben nur ein Zittern: zusammenfassen
        var glatt = new List<Vector3>();
        var davor = von;
        foreach (var punkt in punkte)
        {
            if (punkt.DistanceTo(davor) < 0.5f || punkt.DistanceTo(bis) < 0.5f) continue;
            glatt.Add(punkt);
            davor = punkt;
        }
        return glatt;
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
