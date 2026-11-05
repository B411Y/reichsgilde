using Godot;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Taschendiebe am Markt und Festnahmen (Teil der <c>partial class Stadtansicht</c>; Wachen und Diebe, Paket P3, Nutzer 05.11.2026).
/// Die Darstellung zeigt nur, was der Kern wirklich rechnet – kein Schein-Dieb:
/// <list type="bullet">
/// <item><b>Griffe</b>: Zu jedem Griff aus <see cref="Diebeswesen.GriffeDieserStunde"/> (feste Formel, ohne Zufall) erscheint der Dieb in dunklem Gewand mit
/// Gugel, geht auf einen Passanten am Markt zu (am liebsten im Gedränge), rempelt ihn an, greift in die Gürteltasche und geht mit dem Beutel davon
/// (<see cref="Taschendiebgang"/>). <b>Eigene</b> Diebe kommen vom Rand des Marktes und sind anklickbar (Name, Auftrag); <b>fremde</b> lösen sich erst kurz
/// vor dem Griff aus dem Gedränge und heißen „Verdächtige Gestalt“ – ohne Angabe des Hauses.</item>
/// <item><b>Festnahme</b>: Meldet der Kern über <see cref="Vorfallbuch.Neu"/> einen gefassten Taschendieb, läuft ein Gardist (vom Wachhaus der fassenden
/// Wache, sonst vom nächsten Tor) zum Dieb am Markt, packt ihn und führt ihn zum Wachhaus ab. Der Gefasste ist anklickbar (Name wie in der Meldung).</item>
/// <item><b>Zeit</b>: Alles ist eine Funktion der Spielzeit (Pause, Sprung und Laden stimmen immer); keine Wirkung aufs Spiel.</item>
/// </list>
/// Startoption <c>--diebtest=1|festnahme</c> (mit <c>--entwickler=1</c>): Hauptszene.Diebtest.cs.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Eine Gestalt am Markt: Figur, Knoten und was beim Anklicken steht.</summary>
    private sealed class Diebfigur
    {
        public required Node3D Knoten { get; init; }
        public required Figur? Figur { get; init; }
        /// <summary>„dieb:&lt;n&gt;“ bzw. „dieb:fest:&lt;n&gt;“ – verrät kein Haus.</summary>
        public required string Kennung { get; init; }
        public bool Eigen { get; init; }
        public Arbeiter? Arbeiter { get; init; }
        public string Betrieb { get; init; } = "";
        /// <summary>Was die Gestalt gerade tut (nur eigene Diebe zeigen es), bzw. der Text der Festnahme.</summary>
        public string Zustand { get; set; } = "";
        public string? Fest { get; init; }
        public MeshInstance3D? Beutel { get; set; }
        public bool Sichtbar { get; set; }
        public Vector3 Letzte { get; set; }
    }

    /// <summary>Ein Griff in Arbeit.</summary>
    private sealed class Griffszene
    {
        public required Diebfigur Dieb { get; init; }
        public required double Griff { get; init; }
        public required bool Eigen { get; init; }
        public required int Opfer { get; init; }
        public required MarktplatzAnsicht Platz { get; init; }
        public required Vector3[] Hin { get; init; }
        public required Vector3 Hinten { get; init; }
        public Vector3 OpferOrt { get; set; }
        public Vector3[]? Weg { get; set; }
        public float WegLaenge { get; set; }
    }

    /// <summary>Eine Festnahme in Arbeit.</summary>
    private sealed class Festnahmeszene
    {
        public required Diebfigur Dieb { get; init; }
        public required Node3D WacheKnoten { get; init; }
        public required Figur? Wache { get; init; }
        public required double Start { get; init; }
        public required Vector3[] Hin { get; init; }
        public required Vector3[] Rueck { get; init; }
        public required Vector3 Ort { get; init; }
        public required double LaufDauer { get; init; }
        public required double FuehrDauer { get; init; }
    }

    private readonly Dictionary<(string Haus, int Nummer), Diebfigur> _diebe = new();
    private readonly Dictionary<string, Diebfigur> _diebfiguren = new();
    private readonly Dictionary<(string Haus, int Nummer), Griffszene> _griffszenen = new();
    private readonly HashSet<(string, int, long)> _griffErledigt = new();
    private readonly List<Festnahmeszene> _festnahmen = new();
    private readonly List<Vorfall> _diebVorfaelle = new();
    private readonly List<(Betrieb Haus, Taschengriff Griff, double Minute)> _diebGriffe = new();
    private Vorfallbuch? _diebBuch;
    private Node3D? _diebEbene;
    private long _diebAbfrage = -1;
    private int _diebZaehler, _festZaehler;
    private StandardMaterial3D? _beutelStoff;
    /// <summary>Mit <c>--diebtest</c> schreibt die Stadt Zeilen <c>[taschendieb]</c> ins Protokoll (Griffe, Festnahmen).</summary>
    private static readonly bool DiebLog = Wasserarten.Startoption("diebtest") is { Length: > 0 } and not "0";
    private static void DiebMelden(string text) { if (DiebLog) GD.Print("[taschendieb] " + text); }

    /// <summary>Test (Startoption <c>--diebtest=festnahme</c>): eine Festnahme zeigen, sobald Passanten am Markt sind.</summary>
    public bool FestnahmeProbe { get; set; }
    private double _probeWarten;
    /// <summary>Test (<c>--diebtest=klick|klickfremd|klickfest</c>): die erste sichtbare Gestalt dieser Art wird angeklickt (Auswahltafel), für Bilder.</summary>
    public string? KlickProbe { get; set; }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    private void TaschendiebeAktualisieren(Stadtwirtschaft w, Spielzeit zeit, double bruchteil, double delta)
    {
        if (_simulation is null) return;
        if (!ReferenceEquals(_diebBuch, w.Gauner.Vorfaelle))
        {
            if (_diebBuch is not null) _diebBuch.Neu -= DiebVorfallNeu;
            _diebBuch = w.Gauner.Vorfaelle;
            _diebBuch.Neu += DiebVorfallNeu;
        }
        var gezeigt = Fenster.PassantenMenge > 0 && _raum is null && _maerkte.Count > 0;
        var jetzt = zeit.VergangeneMinuten + bruchteil;
        var pausiert = _simulation.Takt.IstPausiert;
        var sekunden = pausiert ? 0 : delta;

        if (!gezeigt)
        {
            _diebVorfaelle.Clear();
            foreach (var d in _diebfiguren.Values) DiebVerstecken(d);
            foreach (var f in _festnahmen) f.WacheKnoten.Visible = false;
            _griffszenen.Clear();
            _festnahmen.Clear();
            return;
        }

        if (FestnahmeProbe && _passanten.Values.Any(p => p.Knoten.Visible) && (_probeWarten += delta) > 2)
        {
            FestnahmeProbe = false;
            var wache = w.Konten.FirstOrDefault(k => k != _spielerKonto && k.IstAktiv);
            _diebVorfaelle.Add(new Vorfall
            {
                Art = Vorfallart.Taschendiebstahl, Ort = Vorfallort.Markt, Ziel = "Markt", Erfolg = false, Wache = wache,
                TaeterName = "Hans Hellermann", Taeterbetrieb = "Unterschlupf", OpferName = "Bürger am Markt", Minute = zeit.VergangeneMinuten,
            });
        }

        // Einmal je Spielminute: wer greift wann zu, und neue Szenen beginnen
        if (zeit.VergangeneMinuten != _diebAbfrage)
        {
            _diebAbfrage = zeit.VergangeneMinuten;
            _diebGriffe.Clear();
            if (w.Diebe.IstAktiv)
                foreach (var b in w.Betriebe.Where(w.Diebe.IstDiebesbetrieb))
                    foreach (var g in w.Diebe.GriffeDieserStunde(b))
                        _diebGriffe.Add((b, g, zeit.VergangeneMinuten + (g.Uhrzeit - zeit.UhrzeitInMinuten)));
            foreach (var (haus, griff, minute) in _diebGriffe)
            {
                var schluessel = (haus.Name, griff.Dieb.Nummer);
                var kennung = (haus.Name, griff.Dieb.Nummer, (long)minute);
                var eigen = haus.Konto == _spielerKonto;
                if (_griffszenen.ContainsKey(schluessel) || _griffErledigt.Contains(kennung)) continue;
                if (jetzt < minute - Taschendiebgang.Vorlauf(eigen) || jetzt >= minute + Taschendiebgang.GriffDauer) continue;
                var neu = GriffszeneBauen(haus, griff, minute, eigen);
                if (neu is not null || jetzt >= minute - 1) _griffErledigt.Add(kennung); // ohne Opfer weiter versuchen, bis der Griff dran wäre
                if (neu is { } szene0) { _griffszenen[schluessel] = szene0; DiebMelden($"Griff {(eigen ? "eigener" : "fremder")} Dieb {griff.Dieb.Nummer} um {new Spielzeit((long)minute).UhrzeitText}, Opfer {szene0.Opfer}, Weg {Gassennetz.Laenge(szene0.Hin):0.0} m"); }
                else if (jetzt >= minute - 1) DiebMelden($"Griff um {new Spielzeit((long)minute).UhrzeitText}: kein Opfer am Markt");
            }
            if (_griffErledigt.Count > 400) _griffErledigt.Clear();
        }

        var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Spielzeit.MinutenProTag;
        foreach (var (schluessel, szene) in _griffszenen.ToList())
            if (!GriffSetzen(szene, jetzt, sekundenJeMinute, sekunden)) { DiebVerstecken(szene.Dieb); _griffszenen.Remove(schluessel); }

        while (_diebVorfaelle.Count > 0)
        {
            var v = _diebVorfaelle[0];
            _diebVorfaelle.RemoveAt(0);
            if (jetzt - v.Minute < 10 && FestnahmeBauen(v, jetzt) is { } fest) { _festnahmen.Add(fest); DiebMelden($"Festnahme von {v.TaeterName}, Lauf {fest.LaufDauer:0.0} min, Abführen {fest.FuehrDauer:0.0} min"); }
            else DiebMelden("Festnahme nicht zu zeigen");
        }
        for (var i = _festnahmen.Count - 1; i >= 0; i--)
            if (!FestnahmeSetzen(_festnahmen[i], jetzt, sekundenJeMinute, sekunden))
            {
                DiebVerstecken(_festnahmen[i].Dieb);
                _festnahmen[i].WacheKnoten.QueueFree();
                _festnahmen.RemoveAt(i);
            }

        if (KlickProbe is { } art && _diebfiguren.Values.FirstOrDefault(d => d.Sichtbar && (art == "klickfest" ? d.Fest is not null : d.Fest is null && d.Eigen == (art == "klick"))) is { } probe)
        {
            KlickProbe = null;
            Auswahl = probe.Kennung;
            GebaeudeAngeklickt?.Invoke(probe.Kennung);
        }
        foreach (var d in _diebfiguren.Values)
            if (d.Figur is { } figur) FigurHervorheben(figur, d.Sichtbar && (d.Kennung == _unterDerMaus || d.Kennung == Auswahl));
    }

    private void DiebVorfallNeu(Vorfall v)
    {
        if (v.Art == Vorfallart.Taschendiebstahl) _diebVorfaelle.Add(v);
    }

    private void DiebVerstecken(Diebfigur d)
    {
        if (!d.Sichtbar) return;
        d.Knoten.Visible = d.Sichtbar = false;
        if (d.Beutel is { } beutel) beutel.Visible = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bausteine
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Hülle einer Gestalt am Markt: dunkles Gewand mit Gugel, ein kleiner Beutel am Gürtel (erst nach dem Griff zu sehen).</summary>
    private Diebfigur DiebfigurBauen(string kennung, FigurAussehen aussehen, int nummer, bool eigen, Arbeiter? arbeiter, string betrieb, string? fest)
    {
        _diebEbene ??= new Node3D { Name = "Taschendiebe" };
        if (_diebEbene.GetParent() is null) AddChild(_diebEbene);
        var knoten = new Node3D { Name = "Dieb_" + kennung.Replace(':', '_'), Visible = false };
        _diebEbene.AddChild(knoten);
        var d = new Diebfigur
        {
            Knoten = knoten, Figur = FigurEinsetzen(knoten, aussehen, nummer), Kennung = kennung, Eigen = eigen, Arbeiter = arbeiter, Betrieb = betrieb, Fest = fest,
        };
        _beutelStoff ??= new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.24f, 0.13f), Roughness = 0.9f };
        d.Beutel = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.07f, Height = 0.12f }, MaterialOverride = _beutelStoff, Position = new Vector3(0.24f, 0.88f, 0.1f), Visible = false };
        knoten.AddChild(d.Beutel);
        if (d.Figur is { } figur && !_passantenSchatten) FigurSchatten(figur, false);
        if (DiebLog) DiebMarke(knoten, fest is not null ? new Color(1, 0.1f, 0.1f) : eigen ? new Color(1, 0.8f, 0.1f) : new Color(0.2f, 0.6f, 1f));
        _diebfiguren[kennung] = d;
        return d;
    }

    /// <summary>Nur mit <c>--diebtest</c>, für Bilder: ein farbiger Punkt über dem Kopf (gelb eigener, blau fremder Dieb, rot Gefasster, grün Gardist), damit man sie im Gedränge findet.</summary>
    private static void DiebMarke(Node3D knoten, Color farbe) => knoten.AddChild(new MeshInstance3D
    {
        Mesh = new SphereMesh { Radius = 0.14f, Height = 0.28f }, Position = new Vector3(0, 2.1f, 0),
        MaterialOverride = new StandardMaterial3D { AlbedoColor = farbe, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
    });

    /// <summary>Aussehen eines Taschendiebs am Markt: wie im Versteck (<see cref="DiebAussehen"/>), aber immer mit Gugel.</summary>
    private static FigurAussehen MarktDiebAussehen(int nummer, string arbeitskraft) =>
        DiebAussehen(nummer, arbeitskraft, false) with { Kopf = Kopfbedeckung.Kapuze };

    private float MarktBodenHoehe(Vector3 p)
    {
        foreach (var m in _maerkte)
            if (m.Auf(p)) return m.Hoehe;
        return FussHoehe(p.X, p.Z);
    }

    private Vector3 AufBoden(Vector3 p) => new(p.X, MarktBodenHoehe(p), p.Z);

    /// <summary>Die Passanten am Markt, die ein Dieb sich aussuchen kann (sichtbar, auf dem Pflaster), mit der Zahl der Leute in 4 m Umkreis.</summary>
    private List<(int Nummer, Vector3 Ort, MarktplatzAnsicht Platz, int Nah)> Marktleute()
    {
        var liste = new List<(int Nummer, Vector3 Ort, MarktplatzAnsicht Platz, int Nah)>();
        foreach (var (nr, p) in _passanten)
        {
            if (!p.Knoten.Visible) continue;
            var ort = p.Knoten.GlobalPosition;
            if (_maerkte.FirstOrDefault(m => m.Flaeche is not null && m.Auf(ort)) is { } platz) liste.Add((nr, ort, platz, 0));
        }
        for (var i = 0; i < liste.Count; i++)
        {
            var nah = 0;
            for (var j = 0; j < liste.Count; j++)
                if (i != j && new Vector2(liste[i].Ort.X - liste[j].Ort.X, liste[i].Ort.Z - liste[j].Ort.Z).Length() < 4f) nah++;
            liste[i] = liste[i] with { Nah = nah };
        }
        return liste;
    }

    private static Vector3[] WegAuf(MarktplatzAnsicht platz, Vector3 von, Vector3 nach, float hoehe)
    {
        if (platz.Flaeche is { } f && (f.PfadEinfach((von.X, von.Z), (nach.X, nach.Z), 0.6f) ?? f.PfadEinfach((von.X, von.Z), (nach.X, nach.Z), 0.3f)) is { Count: >= 2 } weg)
            return weg.Select(q => new Vector3(q.X, hoehe, q.Z)).ToArray();
        return new[] { new Vector3(von.X, hoehe, von.Z), new Vector3(nach.X, hoehe, nach.Z) };
    }

    /// <summary>Ein Punkt am Rand des Pflasters (frei von Hindernissen), dessen Abstand zu <paramref name="ziel"/> zwischen min und max liegt; sonst der weiteste.</summary>
    private static Vector3? RandPunkt(MarktplatzAnsicht platz, Vector3 ziel, uint streu, float min, float max)
    {
        if (platz.Flaeche is not { } f) return null;
        Vector3? weitester = null;
        var weit = -1f;
        for (var i = 0; i < 24; i++)
        {
            var seite = (int)((streu >> 3) + i) % 4;
            var entlang = (((streu >> 7) + (uint)i * 37) % 17 / 16f * 2 - 1) * 0.8f * platz.Halb;
            var rand = (seite % 2 == 0 ? 1 : -1) * (platz.Halb - 0.7f);
            var (x, z) = seite < 2 ? f.Welt(rand, entlang) : f.Welt(entlang, rand);
            if (f.Frei(x, z) < 0.8f) continue;
            var p = new Vector3(x, platz.Hoehe, z);
            var abstand = new Vector2(x - ziel.X, z - ziel.Z).Length();
            if (abstand >= min && abstand <= max) return p;
            if (abstand > weit) { weit = abstand; weitester = p; }
        }
        return weitester;
    }

    /// <summary>Ein freier Punkt in genau diesem Abstand (Meter) um <paramref name="mitte"/> auf dem Pflaster; sonst null.</summary>
    private static Vector3? PunktUm(MarktplatzAnsicht platz, Vector3 mitte, float abstand, uint streu)
    {
        if (platz.Flaeche is not { } f) return null;
        for (var i = 0; i < 8; i++)
        {
            var w = ((streu % 8) + i) * Mathf.Pi / 4;
            var p = mitte + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * abstand;
            if (f.Auf(p.X, p.Z, 0.5f) && f.Frei(p.X, p.Z) >= 0.8f) return new Vector3(p.X, platz.Hoehe, p.Z);
        }
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Der Griff
    // ═════════════════════════════════════════════════════════════════════════

    private Griffszene? GriffszeneBauen(Betrieb haus, Taschengriff griff, double minute, bool eigen)
    {
        var leute = Marktleute();
        if (leute.Count == 0) return null; // niemand zum Bestehlen: nichts zeigen
        var streu = Taschendiebgang.Streu(griff.Dieb.Nummer + haus.Name.Length * 131, (long)minute, 1);
        var gedraenge = leute.OrderByDescending(l => l.Nah).ThenBy(l => l.Nummer).Take(5).ToList();
        var opfer = gedraenge[(int)(streu % (uint)gedraenge.Count)];
        var start = eigen
            ? RandPunkt(opfer.Platz, opfer.Ort, streu, Taschendiebgang.EntfernungEigenMin, Taschendiebgang.EntfernungEigenMax)
            : PunktUm(opfer.Platz, opfer.Ort, Taschendiebgang.EntfernungFremd, streu);
        if (start is not { } s) return null;
        var schluessel = (haus.Name, griff.Dieb.Nummer);
        if (!_diebe.TryGetValue(schluessel, out var dieb))
        {
            var nummer = 20000 + haus.Name.Sum(c => c) % 1000 * 100 + griff.Dieb.Nummer;
            dieb = DiebfigurBauen($"dieb:{++_diebZaehler}", MarktDiebAussehen(nummer, griff.Dieb.Arbeitskraft), nummer, eigen, griff.Dieb, haus.Name, null);
            _diebe[schluessel] = dieb;
        }
        var richtung = new Vector3(s.X - opfer.Ort.X, 0, s.Z - opfer.Ort.Z);
        richtung = richtung.LengthSquared() < 1e-4f ? Vector3.Back : richtung.Normalized();
        return new Griffszene
        {
            Dieb = dieb, Griff = minute, Eigen = eigen, Opfer = opfer.Nummer, Platz = opfer.Platz, OpferOrt = opfer.Ort,
            Hin = WegAuf(opfer.Platz, s, opfer.Ort, opfer.Platz.Hoehe), Hinten = richtung,
        };
    }

    /// <summary>Setzt den Dieb für diese Zeit. Gibt false zurück, wenn der Griff vorbei ist.</summary>
    private bool GriffSetzen(Griffszene s, double jetzt, double sekundenJeMinute, double sekunden)
    {
        var d = s.Dieb;
        var abschnitt = Taschendiebgang.AbschnittBei(jetzt, s.Griff, s.Eigen, out var anteil);
        if (abschnitt == Taschendiebgang.Abschnitt.Nachher) return false;
        if (abschnitt == Taschendiebgang.Abschnitt.Vorher) { DiebVerstecken(d); return true; }
        if (_passanten.TryGetValue(s.Opfer, out var opfer) && opfer.Knoten.Visible) s.OpferOrt = opfer.Knoten.GlobalPosition;
        var hinter = AufBoden(s.OpferOrt + s.Hinten * Taschendiebgang.Abstand);
        Vector3 ort;
        string bewegung;
        float tempo; // Meter je echter Sekunde (bei Tempo 1)
        var neigung = 0.0;
        switch (abschnitt)
        {
            case Taschendiebgang.Abschnitt.Anschleichen:
            {
                var laenge = Gassennetz.Laenge(s.Hin);
                var basis = Gassennetz.PunktAuf(s.Hin, (float)anteil);
                ort = anteil > 0.7 ? basis.Lerp(hinter, (float)((anteil - 0.7) / 0.3)) : basis;
                tempo = (float)(laenge / (Taschendiebgang.Vorlauf(s.Eigen) * sekundenJeMinute));
                bewegung = "Gehen";
                d.Zustand = "schleicht sich an einen Passanten heran";
                break;
            }
            case Taschendiebgang.Abschnitt.Griff:
            {
                neigung = Taschendiebgang.Neigung(anteil);
                ort = hinter - s.Hinten * (float)(Taschendiebgang.Anrempeln * neigung);
                bewegung = "Stehen";
                tempo = 0;
                d.Zustand = "rempelt jemanden an und greift in die Tasche";
                break;
            }
            default:
            {
                if (s.Weg is null)
                {
                    var ziel = s.Eigen
                        ? RandPunkt(s.Platz, hinter, Taschendiebgang.Streu(d.Arbeiter?.Nummer ?? 0, (long)s.Griff, 2), 8, 14)
                        : PunktUm(s.Platz, hinter, 6f, Taschendiebgang.Streu(d.Arbeiter?.Nummer ?? 0, (long)s.Griff, 3));
                    s.Weg = WegAuf(s.Platz, hinter, ziel ?? hinter + s.Hinten * 6f, s.Platz.Hoehe);
                    s.WegLaenge = Gassennetz.Laenge(s.Weg);
                }
                ort = Gassennetz.PunktAuf(s.Weg, (float)anteil);
                tempo = (float)(s.WegLaenge / (Taschendiebgang.Abgang(s.Eigen) * sekundenJeMinute));
                bewegung = "Gehen";
                d.Zustand = "geht mit der Beute davon";
                break;
            }
        }
        ort = AufBoden(ort);
        var blick = abschnitt == Taschendiebgang.Abschnitt.Griff ? new Vector3(s.OpferOrt.X - ort.X, 0, s.OpferOrt.Z - ort.Z) : ort - d.Letzte;
        DiebStellen(d, ort, blick, bewegung, tempo, sekunden, neigung, abschnitt != Taschendiebgang.Abschnitt.Anschleichen);
        return true;
    }

    /// <summary>Lage, Blick, Bewegung und Neigung einer Gestalt setzen und die Figur im Bild weiterbewegen.</summary>
    private void DiebStellen(Diebfigur d, Vector3 ort, Vector3 blick, string bewegung, float tempo, double sekunden, double neigung, bool beutel)
    {
        if (!d.Sichtbar) { d.Knoten.Visible = d.Sichtbar = true; d.Letzte = ort; }
        blick.Y = 0;
        if (blick.LengthSquared() > 1e-5f) d.Knoten.Rotation = new Vector3(0, Mathf.Atan2(blick.X, blick.Z), 0);
        d.Knoten.Position = ort;
        d.Letzte = ort;
        if (d.Beutel is { } b && b.Visible != beutel) b.Visible = beutel;
        if (d.Figur is not { } figur) return;
        BewegungSetzen(figur, bewegung, tempo <= 0 ? 1 : tempo * (float)_simulation!.Takt.Tempo / (GehenTempo * figur.Beinfaktor));
        figur.Modell.Rotation = new Vector3((float)(0.22 * neigung), 0, 0);
        FigurImBildBewegen(figur, ort, sekunden);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Die Festnahme
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Stück eines Weges ab <paramref name="meter"/> Metern vom Anfang bis zum Ende.</summary>
    private static Vector3[] WegAb(Vector3[] pfad, float meter)
    {
        var aus = new List<Vector3>();
        var strecke = 0f;
        for (var i = 0; i + 1 < pfad.Length; i++)
        {
            var l = pfad[i].DistanceTo(pfad[i + 1]);
            if (strecke + l > meter)
            {
                if (aus.Count == 0) aus.Add(pfad[i].Lerp(pfad[i + 1], l <= 0 ? 0 : (meter - strecke) / l));
                aus.Add(pfad[i + 1]);
            }
            strecke += l;
        }
        return aus.Count >= 2 ? aus.ToArray() : new[] { pfad[^1], pfad[^1] };
    }

    private Festnahmeszene? FestnahmeBauen(Vorfall v, double jetzt)
    {
        if (_simulation?.Wirtschaft is not { } w) return null;
        // Wo: beim bestohlenen Einwohner, wenn er am Markt zu sehen ist, sonst auf dem Platz, der dem Wachhaus am nächsten ist
        Vector3? tuer = null;
        if (v.Wache is { } wache)
            foreach (var b in w.Betriebe.Where(x => x.Konto == wache && w.Wache.IstWachbetrieb(x)))
                if (TuerAnker(b.Name) is { } t) { tuer = t; break; }
        tuer ??= _tore.Count > 0 ? _tore[(int)(Taschendiebgang.Streu(v.TaeterName?.Length ?? 0, v.Jahr, 4) % (uint)_tore.Count)].Ort : null;
        if (tuer is not { } eingang) return null;
        var leute = Marktleute();
        MarktplatzAnsicht? platz;
        Vector3 ort;
        if (v.OpferEinwohner is int on && leute.FirstOrDefault(l => l.Nummer == on) is { Platz: not null } opfer)
            (platz, ort) = (opfer.Platz, opfer.Ort + new Vector3(0.7f, 0, 0));
        else
        {
            platz = _maerkte.Where(m => m.Flaeche is not null).OrderBy(m => new Vector2(m.Mitte.X - eingang.X, m.Mitte.Z - eingang.Z).Length()).FirstOrDefault();
            if (platz is null) return null;
            ort = PunktUm(platz, platz.Mitte, platz.Halb * 0.4f, Taschendiebgang.Streu(v.TaeterName?.Length ?? 0, v.Jahr, 5)) ?? platz.Mitte;
        }
        ort = new Vector3(ort.X, platz.Hoehe, ort.Z);
        // Weg vom Eingang über die Gassen zum Dieb, über das Pflaster mit Umweg um die Stände
        var hin = new List<Vector3> { eingang };
        if (_netz.Knoten.Count > 0)
            foreach (var punkt in _netz.Pfad(_netz.KnotenBei(eingang), _netz.KnotenBei(ort)))
                if (hin[^1].DistanceTo(punkt) > 0.05f) hin.Add(punkt);
        hin.Add(ort);
        var ganz = UeberDenPlatz(hin).ToArray();
        if (ganz.Length < 2) return null;
        var laenge = Gassennetz.Laenge(ganz);
        var lauf = WegAb(ganz, Mathf.Max(0, laenge - Taschendiebgang.WacheVorlauf));
        var rueck = ganz.Reverse().ToArray();
        var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Spielzeit.MinutenProTag;
        var nummer = 30000 + ++_festZaehler;
        var name = string.IsNullOrWhiteSpace(v.TaeterName) ? "Ein Taschendieb" : v.TaeterName;
        var text = $"{name}\n\nAm Markt auf frischer Tat ertappt" + (v.OpferName is { } o ? $" – die Hand in der Tasche von {o}." : ".")
                   + (v.Wache is { } wh ? $" Die Wache von {wh.Inhaber} führt ihn ab." : " Ein Bürger hat ihn erkannt, die Büttel führen ihn ab.");
        var dieb = DiebfigurBauen($"dieb:fest:{_festZaehler}", MarktDiebAussehen(nummer, "Geselle"), nummer, false, null, "", text);
        var knoten = new Node3D { Name = $"Festnahme_Wache_{_festZaehler}", Visible = false };
        _diebEbene!.AddChild(knoten);
        if (DiebLog) DiebMarke(knoten, new Color(0.2f, 0.9f, 0.2f));
        var figur = FigurEinsetzen(knoten, WacheAussehen(nummer + 500, "Geselle", false), nummer + 500);
        return new Festnahmeszene
        {
            Dieb = dieb, WacheKnoten = knoten, Wache = figur, Start = jetzt, Hin = lauf, Rueck = rueck, Ort = ort,
            LaufDauer = Gassennetz.Laenge(lauf) / Gehtempo.MeterJeSpielminute(3.0, _simulation.Takt.RundenDauerSekunden),
            FuehrDauer = laenge / Gehtempo.MeterJeSpielminute(1.2, _simulation.Takt.RundenDauerSekunden),
        };
    }

    private bool FestnahmeSetzen(Festnahmeszene s, double jetzt, double sekundenJeMinute, double sekunden)
    {
        var abschnitt = Taschendiebgang.FestnahmeBei(jetzt - s.Start, s.LaufDauer, s.FuehrDauer, out var anteil);
        if (abschnitt == Taschendiebgang.Fest.Fertig) return false;
        var tempoMal = (float)_simulation!.Takt.Tempo;
        void Wache(Vector3 ort, Vector3 blick, string bewegung, float meterJeSekunde)
        {
            ort = AufBoden(ort);
            if (!s.WacheKnoten.Visible) s.WacheKnoten.Visible = true;
            blick.Y = 0;
            if (blick.LengthSquared() > 1e-5f) s.WacheKnoten.Rotation = new Vector3(0, Mathf.Atan2(blick.X, blick.Z), 0);
            s.WacheKnoten.Position = ort;
            if (s.Wache is not { } f) return;
            BewegungSetzen(f, bewegung, meterJeSekunde <= 0 ? 1 : meterJeSekunde * tempoMal / ((bewegung == "Eilen" ? EilenTempo : GehenTempo) * f.Beinfaktor));
            FigurImBildBewegen(f, ort, sekunden);
        }
        switch (abschnitt)
        {
            case Taschendiebgang.Fest.Lauf:
            {
                var wache = Gassennetz.PunktAuf(s.Hin, (float)anteil);
                var davor = Gassennetz.PunktAuf(s.Hin, Mathf.Min(1, (float)anteil + 0.5f / Mathf.Max(1, Gassennetz.Laenge(s.Hin))));
                Wache(wache, davor - wache, "Eilen", 3.0f);
                s.Dieb.Zustand = "";
                DiebStellen(s.Dieb, s.Ort, wache - s.Ort, "Stehen", 0, sekunden, -0.05, false);
                break;
            }
            case Taschendiebgang.Fest.Zugriff:
            {
                var an = s.Ort + (s.Hin[^2] - s.Ort).Normalized() * 0.8f;
                Wache(an, s.Ort - an, "Stehen", 0);
                DiebStellen(s.Dieb, s.Ort, an - s.Ort, "Stehen", 0, sekunden, 0.1 * Math.Sin(anteil * 14), false);
                break;
            }
            default:
            {
                var laenge = Mathf.Max(1, Gassennetz.Laenge(s.Rueck));
                var meter = (float)anteil * laenge;
                var dieb = Gassennetz.PunktAuf(s.Rueck, Mathf.Min(1, (meter + 1.0f) / laenge));
                var wache = Gassennetz.PunktAuf(s.Rueck, (float)anteil);
                var vorn = Gassennetz.PunktAuf(s.Rueck, Mathf.Min(1, (meter + 2f) / laenge));
                Wache(wache, vorn - wache, "Gehen", 1.2f);
                DiebStellen(s.Dieb, dieb, vorn - dieb, "Gebeugt", 1.0f * 1.2f / 1.0f, sekunden, 0, false);
                break;
            }
        }
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anklicken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die sichtbare Gestalt unter dem Mauszeiger (wie <see cref="PassantBei"/>); null, wenn keine nah genug ist.</summary>
    private string? DiebBei(Vector2 bildschirm)
    {
        if (_raum is not null || _innen is not null || _diebfiguren.Count == 0) return null;
        var fang = Mathf.Max(8, 1.1f * GetViewport().GetVisibleRect().Size.Y / _kamera.Size);
        string? beste = null;
        foreach (var d in _diebfiguren.Values)
        {
            if (!d.Sichtbar) continue;
            var mitte = d.Knoten.GlobalPosition + new Vector3(0, 0.9f, 0);
            if (StadtKamera.IsPositionBehind(mitte)) continue;
            var abstand = StadtKamera.UnprojectPosition(mitte).DistanceTo(bildschirm);
            if (abstand < fang) { fang = abstand; beste = d.Kennung; }
        }
        return beste;
    }

    /// <summary>Text für die Auswahltafel: eigener Dieb mit Auftrag, fremder als „Verdächtige Gestalt“, Gefasster mit Namen. Null, wenn die Kennung keine Gestalt ist.</summary>
    public string? DiebText(string kennung)
    {
        if (!kennung.StartsWith("dieb:", StringComparison.Ordinal)) return null;
        if (!_diebfiguren.TryGetValue(kennung, out var d)) return "Verdächtige Gestalt\n\nIm Gedränge verschwunden.";
        if (d.Fest is { } fest) return fest;
        if (!d.Eigen || d.Arbeiter is not { } a)
            return "Verdächtige Gestalt\n\nIn dunklem Gewand, die Gugel tief im Gesicht, drängt sie sich durch die Leute am Markt. Wem sie dient, weiß niemand."
                   + (d.Sichtbar ? "" : "\nGerade ist sie im Gedränge verschwunden.");
        return string.Join("\n", $"{(a.Name is { Length: > 0 } n ? n : a.Arbeitskraft + " Nr. " + a.Nummer)}", "",
            $"{a.Arbeitskraft} in {d.Betrieb} – Euer Taschendieb.",
            $"Auftrag: Taschendiebstahl am Markt – {(d.Sichtbar ? d.Zustand : "mischt sich unter die Leute am Markt")}.",
            $"Laune {Reichsgilde.Core.Zahlenformat.Ganz(a.Laune)}");
    }
}
