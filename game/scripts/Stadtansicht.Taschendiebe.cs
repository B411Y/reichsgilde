using Godot;
using Reichsgilde.Core.Buerger;
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
        /// <summary>Der Sack über der Schulter (nur nachts nach einem Einbruch mit Beute, Stadtansicht.Nachtdiebe.cs).</summary>
        public MeshInstance3D? Sack { get; set; }
        /// <summary>Text der Auswahltafel, wenn er sich mit der Zeit ändert (nächtliche Gestalten); sonst null.</summary>
        public Func<Diebfigur, string>? Beschreibung { get; set; }
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
        /// <summary>Wie lange vor dem Griff die Szene beginnt (kürzer, wenn der Griff erst spät bekannt wurde, <see cref="Taschendiebgang.StartVorlauf"/>).</summary>
        public double Vorlauf { get; init; }
        /// <summary>Das Opfer, wenn es steht: seine Lage vor dem Zucken (Rempeln), damit es danach wieder so steht.</summary>
        public Vector3? OpferRotation { get; set; }
        public bool Gezuckt { get; set; }
        /// <summary>Die zweite Bahn, wenn eine Streife dem Dieb nahe kommt: auf dem Weg zum Opfer (beim Aufbau geplant) und im Abgang (geplant, sobald der Abgang beginnt).</summary>
        public Ausweichbahn? AusweichHin { get; set; }
        public Ausweichbahn? AusweichAb { get; set; }
        public bool AbGeplant { get; set; }
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
        /// <summary>Fasst ein Torwächter (statt eines eigens erscheinenden Gardisten): er selbst, sein Weg zurück zum Posten und dessen Dauer (Spielminuten).</summary>
        public Torposten? Torwaechter { get; init; }
        public Vector3[] Heim { get; init; } = Array.Empty<Vector3>();
        public double RueckDauer { get; init; }
        /// <summary>Wo die Festnahme zuletzt war (für den Text der Auswahltafel des Torwächters).</summary>
        public Taschendiebgang.Fest Phase { get; set; }
    }

    private readonly Dictionary<string, Diebfigur> _diebfiguren = new();
    private readonly Dictionary<(string Haus, int Nummer, long Minute), Griffszene> _griffszenen = new();
    private readonly HashSet<(string, int, long)> _griffErledigt = new();
    private readonly List<Festnahmeszene> _festnahmen = new();
    private readonly List<Vorfall> _diebVorfaelle = new();
    private readonly List<(Betrieb Haus, Taschengriff Griff, double Minute)> _diebGriffe = new();
    private Vorfallbuch? _diebBuch;
    private Node3D? _diebEbene;
    private long _diebAbfrage = -1;
    private int _diebZaehler, _festZaehler;
    /// <summary>Uhrzeit (Minuten im Tag, mit Bruchteil) und Tag des laufenden Bildes: für den Dienstplan der Torwächter bei einer Festnahme.</summary>
    private double _festUhr;
    private int _festTag;
    private StandardMaterial3D? _beutelStoff;
    /// <summary>Mit <c>--diebtest</c> schreibt die Stadt Zeilen <c>[taschendieb]</c> ins Protokoll (Griffe, Festnahmen).</summary>
    private static readonly bool DiebLog = Wasserarten.Startoption("diebtest") is { Length: > 0 } and not "0";
    /// <summary>Mit <c>--diebtest</c> nur der Markt, auf den die Kamera blickt (damit die Bilder die Szenen zeigen).</summary>
    private bool DiebTestPlatz(MarktplatzAnsicht platz) => new Vector2(platz.Mitte.X, platz.Mitte.Z).DistanceTo(new Vector2(BlickpunktAmBoden().X, BlickpunktAmBoden().Z)) < platz.Halb + 12;
    private ulong _diebBildLog;
    /// <summary>Test (<c>--diebtest=bildfolge</c>): Die Zeit hält 5 s lang bei einem eigenen Dieb an – kurz vor dem Griff, mitten im Griff, kurz danach – für drei Bilder.</summary>
    public bool Bildfolge { get; set; }
    private int _haltStufe;
    /// <summary>Test (<c>--diebtest=festnahmetor</c>): wie <see cref="FestnahmeProbe"/>, aber die Zeit hält dreimal 6 s an – der Wächter läuft los, packt zu, führt ab – für Bilder.</summary>
    public bool FestFolge { get; set; }
    private int _festHaltStufe;
    private ulong _haltBis;
    private Griffszene? _haltSzene;
    /// <summary>Test: Mit <c>--diebbild=&lt;ordner&gt;</c> legt die Stadt 1,5 s nach einem Halt das Bild des Fensters ab (<c>&lt;name&gt;.png</c>), statt dass ein Bildschirmfoto von außen den Zeitpunkt treffen muss.</summary>
    private void DiebBildAblegen(string name, Func<IEnumerable<Node3D?>>? um = null)
    {
        if (Wasserarten.Startoption("diebbild") is not { Length: > 0 } ordner) return;
        GetTree().CreateTimer(1.5).Timeout += () =>
        {
            var bild = GetViewport().GetTexture().GetImage();
            var fehler = bild.SavePng($"{ordner}/{name}.png");
            GD.Print($"[taschendieb] Bild {ordner}/{name}.png: {fehler}");
            // Ausschnitt um die Mitte der Gestalten (2,5fach vergrößert, Pixel scharf)
            var punkte = um?.Invoke().Where(k => k is not null && IsInstanceValid(k)).Select(k => StadtKamera.UnprojectPosition(k!.GlobalPosition + new Vector3(0, 0.9f, 0))).ToList();
            if (punkte is { Count: > 0 })
            {
                var mitte = punkte.Aggregate(Vector2.Zero, (a, p) => a + p) / punkte.Count;
                var x = Mathf.Clamp((int)mitte.X - 240, 0, bild.GetWidth() - 480);
                var y = Mathf.Clamp((int)mitte.Y - 135, 0, bild.GetHeight() - 270);
                var teil = bild.GetRegion(new Rect2I(x, y, 480, 270));
                teil.Resize(1200, 675, Image.Interpolation.Nearest);
                teil.SavePng($"{ordner}/{name}_nah.png");
            }
        };
    }
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
        if (_haltBis > 0 && Time.GetTicksMsec() > _haltBis) { _haltBis = 0; _simulation.Takt.IstPausiert = false; }
        var gezeigt = Fenster.PassantenMenge > 0 && _raum is null && _maerkte.Count > 0;
        var jetzt = zeit.VergangeneMinuten + bruchteil;
        var pausiert = _simulation.Takt.IstPausiert;
        var sekunden = pausiert ? 0 : delta;
        (_festUhr, _festTag) = (zeit.UhrzeitInMinuten + bruchteil, zeit.Jahr);
        (_ausweichJetzt, _ausweichUhr) = (jetzt, _festUhr);

        if (!gezeigt)
        {
            _diebVorfaelle.Clear();
            foreach (var f in _festnahmen) FestnahmeAbbauen(f, true);
            foreach (var g in _griffszenen.Values) DiebEntfernen(g.Dieb);
            _griffszenen.Clear();
            _festnahmen.Clear();
            return;
        }

        if (FestnahmeProbe && _passanten.Values.Any(p => p.Knoten.Visible) && (_probeWarten += delta) > 2)
        {
            FestnahmeProbe = false;
            var wache = w.Konten.FirstOrDefault(k => k != _spielerKonto && k.IstAktiv);
            if (FestFolge && _spielerKonto is { } eigenes && w.Betriebe.Any(b => b.Konto == eigenes && w.Wache.IstWachbetrieb(b))) wache = eigenes; // Test festnahmetor mit --beruf=Gardist: die eigene Wache fasst (Wachhaus nahe am Tor: Rückkehr des Torwächters)
            _diebVorfaelle.Add(new Vorfall
            {
                Art = Vorfallart.Taschendiebstahl, Ort = Vorfallort.Markt, Ziel = "Markt", Erfolg = false, Wache = wache,
                TaeterName = "Konrad Hellermann", Taeterbetrieb = "Unterschlupf", OpferName = "Bürger am Markt", Minute = zeit.VergangeneMinuten,
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
                var kennung = (haus.Name, griff.Dieb.Nummer, (long)minute);
                var eigen = haus.Konto == _spielerKonto;
                if (_griffErledigt.Contains(kennung) || !Taschendiebgang.Zeigen(jetzt, minute, eigen)) continue;
                var neu = GriffszeneBauen(haus, griff, minute, eigen, jetzt);
                if (neu is not null || jetzt >= minute - 1) _griffErledigt.Add(kennung); // ohne Opfer weiter versuchen, bis der Griff dran wäre
                if (neu is { } szene0) { _griffszenen[kennung] = szene0; DiebMelden($"Griff {(eigen ? "eigener" : "fremder")} Dieb {griff.Dieb.Nummer} um {new Spielzeit((long)minute).UhrzeitText}, Vorlauf {szene0.Vorlauf:0.0} min, Opfer {szene0.Opfer}, Weg {Gassennetz.Laenge(szene0.Hin):0.0} m"); }
                else if (jetzt >= minute - 1) DiebMelden($"Griff um {new Spielzeit((long)minute).UhrzeitText}: kein Opfer am Markt");
            }
            if (_griffErledigt.Count > 400) _griffErledigt.Clear();
        }

        var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Spielzeit.MinutenProTag;
        foreach (var (schluessel, szene) in _griffszenen.ToList())
            if (!GriffSetzen(szene, jetzt, sekundenJeMinute, sekunden)) { DiebEntfernen(szene.Dieb); _griffszenen.Remove(schluessel); }

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
                FestnahmeAbbauen(_festnahmen[i]);
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
        else if (Nachtgang.Zeigbar(v.Art)) _nachtVorfaelle.Add(v); // Einbruch und Banden bei Nacht (Stadtansicht.Nachtdiebe.cs)
    }

    /// <summary>Gibt eine Gestalt frei (Knoten und Figur) und nimmt sie aus der Liste – wer anklickbar sein soll, ist in <see cref="_diebfiguren"/>.</summary>
    private void DiebEntfernen(Diebfigur d)
    {
        _diebfiguren.Remove(d.Kennung);
        if (IsInstanceValid(d.Knoten)) d.Knoten.QueueFree();
    }

    private void DiebVerstecken(Diebfigur d)
    {
        if (!d.Sichtbar) return;
        d.Knoten.Visible = d.Sichtbar = false;
        if (d.Beutel is { } beutel) beutel.Visible = false;
        if (d.Sack is { } sack) sack.Visible = false;
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
        if (DiebLog && Wasserarten.Startoption("diebmarken") != "0") DiebMarke(knoten, fest is not null ? new Color(1, 0.1f, 0.1f) : eigen ? new Color(1, 0.8f, 0.1f) : new Color(0.2f, 0.6f, 1f));
        _diebfiguren[kennung] = d;
        return d;
    }

    /// <summary>Nur mit <c>--diebtest</c>, für Bilder: ein farbiger Punkt über dem Kopf (gelb eigener, blau fremder Dieb, rot Gefasster, grün Gardist), damit man sie im Gedränge findet.</summary>
    private static void DiebMarke(Node3D knoten, Color farbe, float radius = 0.14f) => knoten.AddChild(new MeshInstance3D
    {
        Name = "Marke",
        Mesh = new SphereMesh { Radius = radius, Height = 2 * radius }, Position = new Vector3(0, 2.1f, 0),
        MaterialOverride = new StandardMaterial3D { AlbedoColor = farbe, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
    });

    /// <summary>Aussehen eines Taschendiebs am Markt: wie im Versteck (<see cref="DiebAussehen"/>), aber immer mit Gugel.</summary>
    private static FigurAussehen MarktDiebAussehen(int nummer, Arbeiterbild bild) =>
        DiebAussehen(nummer, bild, false) with { Kopf = Kopfbedeckung.Kapuze };

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
            if (_maerkte.FirstOrDefault(m => m.Flaeche is not null && m.Auf(ort)) is { } platz && (!DiebLog || DiebTestPlatz(platz))) liste.Add((nr, ort, platz, 0));
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

    private Griffszene? GriffszeneBauen(Betrieb haus, Taschengriff griff, double minute, bool eigen, double jetzt)
    {
        var leute = Marktleute();
        if (leute.Count == 0) return null; // niemand zum Bestehlen: nichts zeigen
        var streu = Taschendiebgang.Streu(griff.Dieb.Nummer + haus.Name.Length * 131, (long)minute, 1);
        var gedraenge = leute.OrderByDescending(l => l.Nah).ThenBy(l => l.Nummer).Take(5).ToList();
        var opfer = gedraenge[(int)(streu % (uint)gedraenge.Count)];
        var vorlauf = Taschendiebgang.StartVorlauf(jetzt, minute, eigen, out var nah);
        var voll = vorlauf >= Taschendiebgang.Vorlauf(eigen) - 0.5; // sonst taucht der Dieb dicht beim Opfer aus dem Gedränge auf (kein Sprung ins Bild)
        var start = eigen && voll
            ? RandPunkt(opfer.Platz, opfer.Ort, streu, Taschendiebgang.EntfernungEigenMin, Taschendiebgang.EntfernungEigenMax)
            : PunktUm(opfer.Platz, opfer.Ort, voll ? Taschendiebgang.EntfernungFremd : nah, streu);
        if (start is not { } s) return null;
        var nummer = 20000 + haus.Name.Sum(c => c) % 1000 * 100 + griff.Dieb.Nummer;
        var dieb = DiebfigurBauen($"dieb:{++_diebZaehler}", MarktDiebAussehen(nummer, Arbeiterbild.Von(griff.Dieb, _wirtschaft?.Personal)), nummer, eigen, griff.Dieb, haus.Name, null);
        var richtung = new Vector3(s.X - opfer.Ort.X, 0, s.Z - opfer.Ort.Z);
        richtung = richtung.LengthSquared() < 1e-4f ? Vector3.Back : richtung.Normalized();
        var szene = new Griffszene
        {
            Dieb = dieb, Griff = minute, Eigen = eigen, Vorlauf = vorlauf, Opfer = opfer.Nummer, Platz = opfer.Platz, OpferOrt = opfer.Ort,
            Hin = WegAuf(opfer.Platz, s, opfer.Ort, opfer.Platz.Hoehe), Hinten = richtung,
        };
        // Kommt eine Streife dem Dieb auf dem Weg zum Opfer nahe, weicht er aus – nicht in der Schlussphase (ab 60 % des Weges geht er auf die Stelle hinter dem Opfer zu)
        var hinLaenge = Gassennetz.Laenge(szene.Hin);
        var anfang = Math.Max(minute - vorlauf, jetzt);
        var minDauer = eigen ? Ausweichgang.MinDauer : Ausweichgang.MinDauerFremd;
        szene.AusweichHin = AusweichenPlanen(szene.Hin, t => Taschendiebgang.AnschleichenAusweichAnteil(t, minute, eigen, vorlauf) * hinLaenge,
            anfang, minute - vorlauf + Taschendiebgang.AusweichBisAnteil * vorlauf, 0, opfer.Platz, $"Dieb {griff.Dieb.Nummer} (Anschleichen)", minDauer);
        return szene;
    }

    /// <summary>Setzt den Dieb für diese Zeit. Gibt false zurück, wenn der Griff vorbei ist.</summary>
    private bool GriffSetzen(Griffszene s, double jetzt, double sekundenJeMinute, double sekunden)
    {
        var d = s.Dieb;
        var abschnitt = Taschendiebgang.AbschnittBei(jetzt, s.Griff, s.Eigen, s.Vorlauf, out var anteil);
        if (abschnitt == Taschendiebgang.Abschnitt.Nachher) return false;
        if (abschnitt == Taschendiebgang.Abschnitt.Vorher) { DiebVerstecken(d); return true; }
        if (_passanten.TryGetValue(s.Opfer, out var opfer) && opfer.Knoten.Visible) s.OpferOrt = opfer.Knoten.GlobalPosition;
        var hinter = AufBoden(s.OpferOrt + s.Hinten * Taschendiebgang.Abstand);
        Vector3 ort;
        string bewegung;
        float tempo; // Meter je echter Sekunde (bei Tempo 1)
        var neigung = 0.0;
        Vector3? blickFest = null; // beim Ausweichen die eigene Blickrichtung
        switch (abschnitt)
        {
            case Taschendiebgang.Abschnitt.Anschleichen:
            {
                var laenge = Gassennetz.Laenge(s.Hin);
                var basis = Gassennetz.PunktAuf(s.Hin, (float)anteil);
                ort = anteil > 0.7 ? basis.Lerp(hinter, (float)((anteil - 0.7) / 0.3)) : basis;
                tempo = (float)(laenge / (s.Vorlauf * sekundenJeMinute));
                bewegung = "Gehen";
                d.Zustand = "schleicht sich an einen Passanten heran";
                if (s.AusweichHin is { } ausweichen && AusweichenStellen(ausweichen, jetzt, sekundenJeMinute, false, 0f, out var ao, out var ab, out var abew, out var at))
                    (ort, blickFest, bewegung, tempo, d.Zustand) = (ao, ab, abew, at, "weicht der Streife aus");
                break;
            }
            case Taschendiebgang.Abschnitt.Griff:
            {
                neigung = Taschendiebgang.Neigung(anteil) * 1.4;
                ort = hinter - s.Hinten * (float)(Taschendiebgang.Anrempeln * Taschendiebgang.Neigung(anteil));
                bewegung = "Knacken"; // die Hand arbeitet vor dem Körper (Dietrich in der Hand), liest sich als Griff in die Gürteltasche
                tempo = 0;
                OpferZucken(s, anteil);
                d.Zustand = "rempelt jemanden an und greift in die Tasche";
                break;
            }
            default:
            {
                OpferZucken(s, 2);
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
                if (!s.AbGeplant)
                {
                    // Der Abgang ist erst jetzt bekannt (der Weg hängt vom Ort des Opfers beim Griff ab): einmal planen, ab jetzt bis kurz vor dem Ende
                    s.AbGeplant = true;
                    var abLaenge = s.WegLaenge;
                    var abBeginn = s.Griff + Taschendiebgang.GriffDauer;
                    var abDauer = Taschendiebgang.Abgang(s.Eigen);
                    s.AusweichAb = AusweichenPlanen(s.Weg, t => Taschendiebgang.AbgangAnteil(t, s.Griff, s.Eigen) * abLaenge,
                        Math.Max(abBeginn, jetzt), abBeginn + abDauer - 0.6, 0, s.Platz, $"Dieb (Abgang um {new Spielzeit((long)s.Griff).UhrzeitText})",
                        s.Eigen ? Ausweichgang.MinDauer : Ausweichgang.MinDauerFremd);
                }
                if (s.AusweichAb is { } ausweichen && AusweichenStellen(ausweichen, jetzt, sekundenJeMinute, false, 0f, out var ao, out var ab, out var abew, out var at))
                    (ort, blickFest, bewegung, tempo, d.Zustand) = (ao, ab, abew, at, "weicht der Streife aus");
                break;
            }
        }
        ort = AufBoden(ort);
        var blick = blickFest ?? (abschnitt == Taschendiebgang.Abschnitt.Griff ? new Vector3(s.OpferOrt.X - ort.X, 0, s.OpferOrt.Z - ort.Z) : ort - d.Letzte);
        DiebStellen(d, ort, blick, bewegung, tempo, sekunden, neigung, abschnitt != Taschendiebgang.Abschnitt.Anschleichen);
        if (Bildfolge && s.Eigen && _haltBis == 0 && _haltStufe < 3 && (_haltSzene is null || _haltSzene == s))
        {
            var da = _haltStufe switch
            {
                0 => abschnitt == Taschendiebgang.Abschnitt.Anschleichen && anteil >= 0.8,
                1 => abschnitt == Taschendiebgang.Abschnitt.Griff && anteil >= 0.5,
                _ => abschnitt == Taschendiebgang.Abschnitt.Abgang && anteil >= 0.15,
            };
            if (da)
            {
                _haltSzene = s;
                _haltBis = Time.GetTicksMsec() + 6000;
                _simulation!.Takt.IstPausiert = true;
                GD.Print($"[taschendieb] Halt {_haltStufe} {Time.GetUnixTimeFromSystem():0.0}");
                _haltStufe++;
            }
        }
        if (DiebLog && s.Eigen && Time.GetTicksMsec() - _diebBildLog > 250)
        {
            _diebBildLog = Time.GetTicksMsec();
            var px = StadtKamera.UnprojectPosition(ort + new Vector3(0, 0.9f, 0));
            GD.Print($"[taschendieb] Bild {Time.GetUnixTimeFromSystem():0.00} {abschnitt} {anteil:0.00} bei {px.X:0}|{px.Y:0}");
        }
        return true;
    }

    /// <summary>
    /// Das Opfer zuckt beim Rempeln und dreht sich ein Stück zum Dieb um – nur wenn es steht (ein Gehender folgt seinem eigenen Weg). Danach, bei
    /// <paramref name="anteil"/> über 1, steht es wieder wie vorher.
    /// </summary>
    private void OpferZucken(Griffszene s, double anteil)
    {
        if (!_passanten.TryGetValue(s.Opfer, out var p) || p.Stand is null) return;
        if (anteil >= 1)
        {
            if (s.Gezuckt && s.OpferRotation is { } alt) p.Knoten.Rotation = alt;
            s.Gezuckt = false;
            return;
        }
        s.OpferRotation ??= p.Knoten.Rotation;
        if (anteil < 0.45) return;
        s.Gezuckt = true;
        var zum = new Vector3(s.Dieb.Knoten.Position.X - p.Knoten.Position.X, 0, s.Dieb.Knoten.Position.Z - p.Knoten.Position.Z);
        var ziel = zum.LengthSquared() > 1e-4f ? Mathf.Atan2(zum.X, zum.Z) : s.OpferRotation!.Value.Y;
        p.Knoten.Rotation = new Vector3(0, Mathf.LerpAngle(s.OpferRotation!.Value.Y, ziel, 0.6f), 0);
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

    /// <summary>Der Weg von <paramref name="von"/> über die Gassen zu <paramref name="nach"/> und über das Pflaster mit Umweg um die Stände. <paramref name="netzVon"/>: der Punkt, an dem ein Anfang abseits der Gassen (Posten am Tor) auf das Netz trifft.</summary>
    private Vector3[] FestWeg(Vector3 von, Vector3 nach, Vector3? netzVon = null)
    {
        var weg = new List<Vector3> { von };
        var anfang = von;
        if (netzVon is { } nv) { anfang = nv; if (von.DistanceTo(nv) > 0.05f) weg.Add(nv); }
        if (_netz.Knoten.Count > 0)
            foreach (var punkt in _netz.Pfad(_netz.KnotenBei(anfang), _netz.KnotenBei(nach)))
                if (weg[^1].DistanceTo(punkt) > 0.05f) weg.Add(punkt);
        weg.Add(nach);
        return UeberDenPlatz(weg).ToArray();
    }

    /// <summary>
    /// Fasst ein sichtbarer Torwächter den Dieb? Das nächste Tor liegt höchstens <see cref="Taschendiebgang.TorwaechterNaehe"/> m vom Ort, mindestens zwei Wächter stehen dort
    /// am Posten (einer bleibt als Ersatzmann) und der Gewählte (der dem Ort nächste) hat bis zum Ende der Festnahme Dienst (nicht über 23 Uhr, kein Schichtwechsel).
    /// Null: dann kommt wie bisher ein Gardist. <paramref name="dauer"/> rechnet die Szene nach der Wahl aus (Spielminuten).
    /// </summary>
    private (Torposten Posten, Torlage Lage)? TorwaechterWaehlen(Vector3 ort, Func<Torposten, Torlage, double> dauer)
    {
        if (_torposten.Count == 0 || _tore.Count == 0 || _simulation?.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } ew) return null;
        var tor = _tore.OrderBy(t => new Vector2(t.Ort.X - ort.X, t.Ort.Z - ort.Z).Length()).First();
        if (Wasserarten.Startoption("festtor") is { Length: > 0 } festTor && _tore.FirstOrDefault(t => t.Name == festTor) is { Name: not null } gewaehlt) tor = gewaehlt; // nur Test (--festtor=<Name>): dieses Tor
        var abstand = new Vector2(tor.Ort.X - ort.X, tor.Ort.Z - ort.Z).Length();
        if (abstand > Taschendiebgang.TorwaechterNaehe) { DiebMelden($"Festnahme bei {ort.X:0}|{ort.Z:0}: nächstes Tor {tor.Name} {abstand:0} m weit, ein Gardist kommt"); return null; }
        var amPosten = _torposten.Values.Where(p => p.Sichtbar && p.Abgeordnet is null && p.Dienst == Wachdienst.Posten && p.Wache.Tor == tor.Name && p.Figur is not null)
            .OrderBy(p => p.Knoten.Position.DistanceTo(ort)).ToList();
        if (!Taschendiebgang.TorwaechterFasst(abstand, amPosten.Count) || TorlageVon(tor.Name) is not { } lage || lage.Gasse.Length == 0)
        {
            DiebMelden($"Festnahme bei {ort.X:0}|{ort.Z:0}: Tor {tor.Name} {abstand:0} m weit, aber {amPosten.Count} Wächter am Posten sichtbar, ein Gardist kommt");
            return null;
        }
        var posten = amPosten[0];
        var bis = _festUhr + dauer(posten, lage) + 1;
        if (bis >= Reichsgilde.Core.Zeit.Spielzeit.TagesendeMinute || ew.Torwache.Dienst(posten.Wache, bis, _festTag).Dienst != Wachdienst.Posten) // Tagesende, Schichtwechsel
        {
            DiebMelden($"Festnahme bei {ort.X:0}|{ort.Z:0}: der Wächter hätte vor dem Ende Dienstschluss, ein Gardist kommt");
            return null;
        }
        return (posten, lage);
    }

    /// <summary>Gibt die Festnahme frei: den Dieb und – bei einem Gardisten – seinen Knoten; ein Torwächter bleibt (er gehört der Torwache und geht zurück auf den Posten).</summary>
    private void FestnahmeAbbauen(Festnahmeszene f, bool abgebrochen = false)
    {
        DiebEntfernen(f.Dieb);
        if (f.Torwaechter is { } tw)
        {
            tw.Abgeordnet = null;
            DiebMelden(abgebrochen ? $"Festnahme abgebrochen, {tw.Wache.Person.Name} wird neu gestellt" : $"{tw.Wache.Person.Name} ist zurück auf seinem Posten am {tw.Wache.Tor}");
            if (abgebrochen) TorpostenVerstecken(tw); // Stadt nicht mehr gezeigt: die Torwache stellt ihn beim nächsten Mal an seinen Platz
        }
        else if (IsInstanceValid(f.WacheKnoten)) f.WacheKnoten.QueueFree();
    }

    private Festnahmeszene? FestnahmeBauen(Vorfall v, double jetzt)
    {
        if (_simulation?.Wirtschaft is not { } w) return null;
        // Wo: beim bestohlenen Einwohner, wenn er am Markt zu sehen ist, sonst auf dem Platz, der dem Wachhaus am nächsten ist
        Vector3? tuer = null;
        Vector3? wachhaus = null;
        if (v.Wache is { } wache)
            foreach (var b in w.Betriebe.Where(x => x.Konto == wache && w.Wache.IstWachbetrieb(x)))
                if (TuerAnker(b.Name) is { } t) { tuer = wachhaus = t; break; }
        tuer ??= _tore.Count > 0 ? _tore[(int)(Taschendiebgang.Streu(v.TaeterName?.Length ?? 0, v.Jahr, 4) % (uint)_tore.Count)].Ort : null;
        if (tuer is not { } eingang) return null;
        var leute = Marktleute();
        MarktplatzAnsicht? platz;
        Vector3 ort;
        if (v.OpferEinwohner is int on && leute.FirstOrDefault(l => l.Nummer == on) is { Platz: not null } opfer)
            (platz, ort) = (opfer.Platz, opfer.Ort + new Vector3(0.7f, 0, 0));
        else
        {
            platz = _maerkte.Where(m => m.Flaeche is not null && (!DiebLog || Wasserarten.Startoption("festtor") is not null || DiebTestPlatz(m))).OrderBy(m => new Vector2(m.Mitte.X - eingang.X, m.Mitte.Z - eingang.Z).Length()).FirstOrDefault(); // beim Test (--diebtest) der Markt, auf den die Kamera blickt
            if (platz is null) return null;
            ort = PunktUm(platz, platz.Mitte, platz.Halb * 0.4f, Taschendiebgang.Streu(v.TaeterName?.Length ?? 0, v.Jahr, 5)) ?? platz.Mitte;
        }
        ort = new Vector3(ort.X, platz.Hoehe, ort.Z);
        var tempoDrei = Gehtempo.MeterJeSpielminute(3.0, _simulation.Takt.RundenDauerSekunden);
        var tempoFuehren = Gehtempo.MeterJeSpielminute(1.2, _simulation.Takt.RundenDauerSekunden);
        var tempoGehen = Gehtempo.MeterJeSpielminute(Gehtempo.Gehen, _simulation.Takt.RundenDauerSekunden);

        // Ein Torwächter, wenn ein Tor nahe ist (statt eines Gardisten): Er läuft vom Posten zum Dieb, führt ihn zum Wachhaus (liegt es nahe am Tor) bzw. in den Torturm
        // und kehrt danach auf seinen Posten zurück. Wege und Dauern stehen mit dem Wächter fest (Funktion der Zeit).
        Torposten? torwaechter = null;
        Vector3[] lauf, rueck, heim = Array.Empty<Vector3>();
        var rueckDauer = 0.0;
        float laenge;
        var geholt = TorwaechterWaehlen(ort, (p, lage) =>
        {
            var weg = TorFestwege(p, lage, ort, wachhaus);
            return Gassennetz.Laenge(weg.Hin) / tempoDrei + Taschendiebgang.ZugriffDauer + Gassennetz.Laenge(weg.Rueck) / tempoFuehren + (weg.Heim.Length > 1 ? Gassennetz.Laenge(weg.Heim) / tempoGehen : 0);
        });
        if (geholt is { } tw)
        {
            torwaechter = tw.Posten;
            var weg = TorFestwege(tw.Posten, tw.Lage, ort, wachhaus);
            (lauf, rueck, heim) = (weg.Hin, weg.Rueck, weg.Heim);
            laenge = Gassennetz.Laenge(rueck);
            rueckDauer = heim.Length > 1 ? Gassennetz.Laenge(heim) / tempoGehen : 0;
        }
        else
        {
            // Weg vom Eingang über die Gassen zum Dieb, über das Pflaster mit Umweg um die Stände
            var ganz = FestWeg(eingang, ort);
            if (ganz.Length < 2) return null;
            laenge = Gassennetz.Laenge(ganz);
            lauf = WegAb(ganz, Mathf.Max(0, laenge - Taschendiebgang.WacheVorlauf));
            rueck = ganz.Reverse().ToArray();
        }
        var nummer = 30000 + ++_festZaehler;
        var name = string.IsNullOrWhiteSpace(v.TaeterName) ? "Ein Taschendieb" : v.TaeterName;
        var text = $"{name}\n\nAm Markt auf frischer Tat ertappt" + (v.OpferName is { } o ? $" – die Hand in der Tasche von {o}." : ".")
                   + (v.Wache is { } wh ? $" Die Wache von {wh.Inhaber} führt ihn ab." : " Ein Bürger hat ihn erkannt, die Büttel führen ihn ab.");
        var dieb = DiebfigurBauen($"dieb:fest:{_festZaehler}", MarktDiebAussehen(nummer, Arbeiterbild.Ersatz()), nummer, false, null, "", text);
        Node3D knoten;
        Figur? figur;
        if (torwaechter is not null) { knoten = torwaechter.Knoten; figur = torwaechter.Figur; }
        else
        {
            knoten = new Node3D { Name = $"Festnahme_Wache_{_festZaehler}", Visible = false };
            _diebEbene!.AddChild(knoten);
            if (DiebLog && Wasserarten.Startoption("diebmarken") != "0") DiebMarke(knoten, new Color(0.2f, 0.9f, 0.2f));
            figur = FigurEinsetzen(knoten, WacheAussehen(nummer + 500, Arbeiterbild.Ersatz(), false), nummer + 500);
        }
        var szene = new Festnahmeszene
        {
            Dieb = dieb, WacheKnoten = knoten, Wache = figur, Start = jetzt, Hin = lauf, Rueck = rueck, Ort = ort,
            LaufDauer = Gassennetz.Laenge(lauf) / tempoDrei,
            FuehrDauer = laenge / tempoFuehren,
            Torwaechter = torwaechter, Heim = heim, RueckDauer = rueckDauer,
        };
        if (torwaechter is not null)
        {
            torwaechter.Abgeordnet = szene;
            DiebMelden($"Torwächter {torwaechter.Wache.Person.Name} vom {torwaechter.Wache.Tor} fasst zu: Lauf {Gassennetz.Laenge(lauf):0} m, Abführen {laenge:0} m, zurück {Gassennetz.Laenge(heim):0} m");
        }
        return szene;
    }

    /// <summary>
    /// Die Wege eines Torwächters bei einer Festnahme: hin vom Posten zum Dieb, mit dem Dieb ins Ziel (das Wachhaus der fassenden Wache, wenn es höchstens
    /// <see cref="Taschendiebgang.TorwaechterNaehe"/> m vom Tor liegt, sonst der Torturm am Posten) und zurück auf den Posten.
    /// </summary>
    private (Vector3[] Hin, Vector3[] Rueck, Vector3[] Heim) TorFestwege(Torposten posten, Torlage lage, Vector3 ort, Vector3? wachhaus)
    {
        var beginn = lage.Gasse[0];
        var stand = TorStandplatz(lage, posten.Wache.Platz);
        var start = posten.Knoten.Position;
        var hin = FestWeg(start, ort, beginn);
        if (wachhaus is { } haus && new Vector2(haus.X - lage.Ort.X, haus.Z - lage.Ort.Z).Length() <= Taschendiebgang.TorwaechterNaehe)
        {
            var rueck = FestWeg(ort, haus);
            var heim = FestWeg(stand, haus, beginn).Reverse().ToArray();
            return (hin, rueck, heim);
        }
        return (hin, hin.Reverse().ToArray(), Array.Empty<Vector3>()); // in den Torturm: zurück zum Posten, dort übergibt er den Dieb
    }

    private bool FestnahmeSetzen(Festnahmeszene s, double jetzt, double sekundenJeMinute, double sekunden)
    {
        var abschnitt = Taschendiebgang.FestnahmeBei(jetzt - s.Start, s.LaufDauer, s.FuehrDauer, out var anteil, s.RueckDauer);
        s.Phase = abschnitt;
        if (abschnitt == Taschendiebgang.Fest.Fertig) return false;
        if (FestFolge && _haltBis == 0 && _festHaltStufe < (s.Heim.Length > 1 ? 4 : 3) && s.Torwaechter is not null)
        {
            var da = _festHaltStufe switch
            {
                0 => abschnitt == Taschendiebgang.Fest.Lauf && anteil >= 0.4,
                1 => abschnitt == Taschendiebgang.Fest.Zugriff && anteil >= 0.5,
                2 => abschnitt == Taschendiebgang.Fest.Abfuehren && anteil >= 0.25,
                _ => abschnitt == Taschendiebgang.Fest.Rueckkehr && anteil >= 0.5,
            };
            if (da)
            {
                _haltBis = Time.GetTicksMsec() + 6000;
                _simulation!.Takt.IstPausiert = true;
                var wp = StadtKamera.UnprojectPosition(s.WacheKnoten.GlobalPosition + new Vector3(0, 0.9f, 0));
                var dp = StadtKamera.UnprojectPosition(s.Dieb.Knoten.GlobalPosition + new Vector3(0, 0.9f, 0));
                GD.Print($"[taschendieb] Halt {_festHaltStufe} {Time.GetUnixTimeFromSystem():0.0} Festnahme {abschnitt} {anteil:0.00}, Wächter im Bild bei {wp.X:0}|{wp.Y:0}, Dieb bei {dp.X:0}|{dp.Y:0}");
                DiebBildAblegen($"fest{_festHaltStufe}", () => abschnitt == Taschendiebgang.Fest.Lauf ? new[] { s.WacheKnoten } : new[] { s.WacheKnoten, s.Dieb.Knoten });
                _festHaltStufe++;
            }
        }
        var tempoMal = (float)_simulation!.Takt.Tempo;
        void Wache(Vector3 ort, Vector3 blick, string bewegung, float meterJeSekunde)
        {
            ort = AufBoden(ort);
            if (!s.WacheKnoten.Visible) s.WacheKnoten.Visible = true;
            if (s.Torwaechter is { } tw) tw.Sichtbar = true;
            blick.Y = 0;
            if (blick.LengthSquared() > 1e-5f) s.WacheKnoten.Rotation = new Vector3(0, Mathf.Atan2(blick.X, blick.Z), 0);
            if (s.Torwaechter is { } tw2) tw2.Blick = s.WacheKnoten.Rotation.Y; // zurück am Posten dreht er von dort weiter (kein Sprung)
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
            case Taschendiebgang.Fest.Abfuehren:
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
            default: // Rückkehr des Torwächters auf seinen Posten: der Dieb ist übergeben (im Wachhaus bzw. Torturm)
            {
                DiebVerstecken(s.Dieb);
                var laenge = Mathf.Max(1, Gassennetz.Laenge(s.Heim));
                var wache = Gassennetz.PunktAuf(s.Heim, (float)anteil);
                var vorn = Gassennetz.PunktAuf(s.Heim, Mathf.Min(1, (float)anteil + 0.5f / laenge));
                Wache(wache, vorn - wache, "Gehen", (float)Gehtempo.Gehen);
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
        if (DiebzeichenBei(bildschirm) is { } zeichen) return zeichen; // das Zeichen über dem eigenen Dieb (Stadtansicht.Diebzeichen.cs)
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
        if (d.Beschreibung is { } beschreibung) return beschreibung(d);
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
