using Godot;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Einbruch und Banden bei Nacht (Teil der <c>partial class Stadtansicht</c>; Wachen und Diebe, Paket P4, Nutzer 05.11.2026).
/// Die Darstellung zeigt nur, was der Kern wirklich gewürfelt hat: Zu jedem Vorfall im <see cref="Vorfallbuch"/> (Einbruch, Hauseinbruch, Brand, Gift,
/// Entführung, Auskundschaften; nicht Taschendiebe und Überfall im Umland) erscheinen nachts die Täter (<see cref="Nachtgang"/>):
/// <list type="bullet">
/// <item><b>Weg ohne Licht</b>: Zum Ziel (Tür des Betriebs bzw. des Wohnhauses, Anker <c>Anker_Tuer</c>) kommt der Täter auf dem dunkelsten von bis zu fünf
/// Wegen durch das Gassennetz (<see cref="NachtLicht"/>: Feuerkörbe, Wandlaternen, Fackeln und die Streife, soweit sie einfach zu meiden sind); eigene Diebe kommen
/// aus ihrem Versteck.</item>
/// <item><b>Der Ablauf</b>: gebückt heranschleichen, kniend im Schatten warten, zur Tür huschen, mit dem Dietrich arbeiten (Knien beim Legen von Feuer und Gift,
/// Lauschen beim Auskundschaften), bei einem gelungenen Einbruch kurz im Haus verschwinden und mit einem <b>Sack</b> über der Schulter herauskommen, dann
/// zurück ins Dunkel – eilig, wenn der Kern einen Zeugen meldet. Misslingt die Tat, zieht der Täter an der Tür ab.</item>
/// <item><b>Banden</b> aus dem Hinterzimmer haben 2 bis 3 namenlose Mitglieder, die versetzt hintereinandergehen; eines bleibt als Posten vor der Tür.</item>
/// <item><b>Anklicken</b>: eigene Diebe mit Namen und Auftrag; die eigene Bande als „Bande aus dem Hinterzimmer“; fremde Täter (auch fremde Banden)
/// nur als „Verdächtige Gestalt“ ohne Haus (der Kern kennt <see cref="Vorfall.Taeterhaus"/>, die Darstellung verrät es nicht).</item>
/// <item><b>Kein echtes Licht</b>, keine Wirkung auf das Spiel; alles hängt nur an der Spielzeit seit dem Vorfall (Pause, Sprung, Laden stimmen). Ist die Stadt
/// groß (<see cref="VieleFiguren"/>), werden nur Taten in Kameranähe gezeigt.</item>
/// </list>
/// Was der Kern nicht hergibt und die Darstellung deshalb nicht zeigt: Festnahmen bei Nacht (der Kern kennt nur den gefassten Taschendieb am Tag),
/// Wachen, die eine Tat verhindern, und das Ziel einer Tat vor ihrem Versuch (er wird zur vollen Stunde gewürfelt, die Szene folgt danach).
/// Testhilfe: <c>--diebtest=nacht|einbruch|bande|fremd|echt|nachtfolge|klicknacht|klickbande|klickfremd</c> (Hauptszene.Diebtest.cs).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Vorfall in Arbeit: die Täter, ihre Wege und was sie tragen.</summary>
    private sealed class Nachtszene
    {
        public required Vorfall Vorfall { get; init; }
        public required double Start { get; init; }
        public required Nachtgang.Plan Plan { get; init; }
        public required Vector3[] Hin { get; init; }
        public required Vector3[] Naeher { get; init; }
        public required Vector3[] Flucht { get; init; }
        public required Vector3 Tuer { get; init; }
        /// <summary>Blickrichtung zur Tür (waagerecht, Länge 1) und die Seite, auf der Bandenmitglieder nebeneinanderstehen.</summary>
        public required Vector3 Blick { get; init; }
        public required List<Diebfigur> Leute { get; init; }
        public required bool Eigen { get; init; }
        public required bool Sack { get; init; }
        public int Folge { get; set; }
        /// <summary>Die zweite Bahn, wenn eine Streife dem Täter nahe kommt: beim Anschleichen und auf der Flucht (beim Aufbau geplant, Stadtansicht.Ausweichen.cs).</summary>
        public Ausweichbahn? AusweichHin { get; set; }
        public Ausweichbahn? AusweichFlucht { get; set; }
    }

    private readonly List<Vorfall> _nachtVorfaelle = new();
    private readonly List<Nachtszene> _nachtszenen = new();
    private int _nachtZaehler, _nachtHaltStufe;
    private StandardMaterial3D? _sackStoff;
    private static readonly bool NachtLog = Wasserarten.Startoption("diebtest") is { Length: > 0 } and not "0";
    private static void NachtMelden(string text) { if (NachtLog) GD.Print("[nachtdieb] " + text); }

    /// <summary>Test (<c>--diebtest=nacht|einbruch|bande|fremd</c>): kurz nach 20 Uhr trägt die Stadt selbst einen Vorfall ins Bild ein (nur Bild, der Kern merkt nichts).</summary>
    public string? NachtProbe { get; set; }
    private bool _nachtProbeGemacht, _nachtWarGezeigt;
    /// <summary>Test (<c>--diebtest=nachtfolge</c>): die Zeit hält dreimal 6 s bei einem eigenen Täter an (Anschleichen, Tür, Flucht).</summary>
    public bool NachtFolge { get; set; }
    /// <summary>Test (<c>--diebtest=klicknacht|klickbande|klickfremd</c>): die erste sichtbare Gestalt dieser Art wird angeklickt.</summary>
    public string? NachtKlick { get; set; }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    private void NachtdiebeAktualisieren(Stadtwirtschaft w, Spielzeit zeit, double bruchteil, double delta)
    {
        if (_simulation is null) return;
        var gezeigt = Fenster.PassantenMenge > 0 && _raum is null && _netz.Knoten.Count > 0;
        if (!gezeigt)
        {
            _nachtWarGezeigt = false;
            _nachtVorfaelle.Clear();
            foreach (var s in _nachtszenen) NachtEntfernen(s);
            _nachtszenen.Clear();
            return;
        }
        var jetzt = zeit.VergangeneMinuten + bruchteil;
        (_ausweichJetzt, _ausweichUhr) = (jetzt, zeit.UhrzeitInMinuten + bruchteil);
        var sekunden = _simulation.Takt.IstPausiert ? 0 : delta;
        var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Spielzeit.MinutenProTag;

        if (!_nachtWarGezeigt)
        {
            // Wieder im Bild (Innenraum verlassen, Passanten wieder an, Laden): laufende Szenen der letzten Stunde neu aufbauen
            _nachtWarGezeigt = true;
            _nachtVorfaelle.AddRange(w.Gauner.Vorfaelle.Zwischen((long)jetzt - 60, (long)jetzt + 1).Where(v => Nachtgang.Zeigbar(v.Art) && !_nachtVorfaelle.Contains(v)));
        }
        if (NachtProbe is { } probe && !_nachtProbeGemacht && zeit.UhrzeitInMinuten >= 20 * 60 + 2 && zeit.UhrzeitInMinuten < 22 * 60)
        {
            _nachtProbeGemacht = true;
            NachtProbeEintragen(w, zeit, probe);
        }

        while (_nachtVorfaelle.Count > 0)
        {
            var v = _nachtVorfaelle[0];
            _nachtVorfaelle.RemoveAt(0);
            if (Nachtgang.Verfuegbar(v.Uhrzeit) <= 0) { NachtMelden($"{v.Art} um 23 Uhr: die Nacht ist vorbei, nichts zu zeigen"); continue; }
            if (jetzt - v.Minute > 60) { NachtMelden($"{v.Art} um {v.Uhrzeit / 60}:{v.Uhrzeit % 60:00} zu alt zum Zeigen"); continue; }
            if (NachtszeneBauen(w, v, jetzt) is { } neu)
            {
                _nachtszenen.Add(neu);
                NachtMelden($"{v.Art} {(neu.Eigen ? "eigener" : "fremder")} Täter, {neu.Leute.Count} Figur(en), Ziel {v.Ziel}, {(v.Erfolg ? "Erfolg" : "misslungen")}, " +
                            $"Tür {neu.Tuer.X:0}|{neu.Tuer.Z:0} (Bild {StadtKamera.UnprojectPosition(neu.Tuer).X:0}|{StadtKamera.UnprojectPosition(neu.Tuer).Y:0}), Plan {neu.Plan.Gesamt:0.0} min, Hin {Gassennetz.Laenge(neu.Hin):0.0} m, Flucht {Gassennetz.Laenge(neu.Flucht):0.0} m, Sack {neu.Sack}");
            }
            else NachtMelden($"{v.Art} bei {v.Ziel}: nicht zu zeigen ({(NachtTuer(v) is { } t ? $"Tür {t.X:0}|{t.Z:0}, Bild {StadtKamera.UnprojectPosition(t).X:0}|{StadtKamera.UnprojectPosition(t).Y:0}: zu weit vom Bild oder kein Weg" : "keine Tür im Modell")})");
        }
        for (var i = _nachtszenen.Count - 1; i >= 0; i--)
            if (!NachtSetzen(_nachtszenen[i], jetzt, sekundenJeMinute, sekunden))
            {
                NachtEntfernen(_nachtszenen[i]);
                _nachtszenen.RemoveAt(i);
            }

        if (NachtKlick is { } art && _diebfiguren.Values.FirstOrDefault(d => d.Sichtbar && d.Kennung.StartsWith("dieb:nacht:", StringComparison.Ordinal)
                && (art == "klickfremd" ? !d.Eigen : art == "klickbande" ? d.Eigen && d.Kennung.EndsWith(":b", StringComparison.Ordinal) : d.Eigen && !d.Kennung.EndsWith(":b", StringComparison.Ordinal))) is { } probeFigur)
        {
            NachtKlick = null;
            Auswahl = probeFigur.Kennung;
            GebaeudeAngeklickt?.Invoke(probeFigur.Kennung);
            if (Wasserarten.Startoption("diebbild") is { Length: > 0 } ordner)
                GetTree().CreateTimer(2.0).Timeout += () => GD.Print($"[nachtdieb] Bild {ordner}/{art}.png: {GetViewport().GetTexture().GetImage().SavePng($"{ordner}/{art}.png")}");
        }
    }

    private void NachtEntfernen(Nachtszene s)
    {
        foreach (var d in s.Leute) DiebEntfernen(d);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbauen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Orte der festen Lichter und der Streife, die ein Täter meidet (Feuerkörbe, Wandlaternen, Fackeln, Wachleute mit Laterne).</summary>
    private List<Vector3> NachtLichter()
    {
        var orte = new List<Vector3>();
        foreach (var q in _lichtquellen)
            if (q.Art is Lichtart.Feuerkorb or Lichtart.Wandlaterne or Lichtart.Fackel && IsInstanceValid(q.Knoten) && q.Knoten.IsInsideTree())
                orte.Add(ToLocal(q.Knoten.GlobalPosition));
        foreach (var g in _wachgaenge.Values)
            if (g.Sichtbar) orte.Add(g.Knoten.Position);
        return orte;
    }

    /// <summary>Wie hell ein Weg beleuchtet ist: je Meter die Summe der Lichter in 9 m Nähe (je näher, desto mehr). Kleiner heißt dunkler.</summary>
    private static float NachtLicht(Vector3[] weg, List<Vector3> lichter)
    {
        var l = Gassennetz.Laenge(weg);
        var summe = 0f;
        for (var m = 0f; m <= l; m += 1.5f)
        {
            var p = Gassennetz.PunktAuf(weg, l <= 0 ? 0 : m / l, l);
            foreach (var q in lichter)
            {
                var d = new Vector2(p.X - q.X, p.Z - q.Z).Length();
                if (d < 9f) summe += 9f - d;
            }
        }
        return summe;
    }

    /// <summary>Der Weg über das Gassennetz von <paramref name="von"/> zur Tür (Anfang und Ende eingeschlossen); null, wenn es keinen gibt.</summary>
    private Vector3[]? NachtRoute(Vector3 von, Vector3 tuer)
    {
        var a = _netz.KnotenBei(von);
        var b = _netz.KnotenBei(tuer);
        if (a < 0 || b < 0) return null;
        var liste = new List<Vector3> { von };
        foreach (var p in _netz.Pfad(a, b))
            if (liste[^1].DistanceTo(p) > 0.05f) liste.Add(p);
        if (liste[^1].DistanceTo(tuer) > 0.05f) liste.Add(tuer);
        return liste.Count >= 2 ? liste.ToArray() : null;
    }

    /// <summary>Der Anfang eines Weges bis <paramref name="meter"/> Meter (Gegenstück zu <see cref="WegAb"/>).</summary>
    private static Vector3[] WegBis(Vector3[] pfad, float meter)
    {
        var aus = new List<Vector3> { pfad[0] };
        var strecke = 0f;
        for (var i = 0; i + 1 < pfad.Length; i++)
        {
            var l = pfad[i].DistanceTo(pfad[i + 1]);
            if (strecke + l >= meter)
            {
                aus.Add(pfad[i].Lerp(pfad[i + 1], l <= 0 ? 0 : (meter - strecke) / l));
                return aus.ToArray();
            }
            aus.Add(pfad[i + 1]);
            strecke += l;
        }
        return aus.Count >= 2 ? aus.ToArray() : new[] { pfad[0], pfad[0] };
    }

    /// <summary>Die Tür des Ziels: Betrieb oder Wohnhaus des Opfers (Anker <c>Anker_Tuer</c>); null, wenn das Modell keinen hat.</summary>
    private Vector3? NachtTuer(Vorfall v)
    {
        if (v.Ort == Vorfallort.Betrieb && v.Betrieb is { } betrieb) return TuerAnker(betrieb);
        if (v.Ort == Vorfallort.Wohnhaus && v.Opfer is { } opfer) return TuerAnker(WohnhausKennung(opfer));
        return null;
    }

    private Nachtszene? NachtszeneBauen(Stadtwirtschaft w, Vorfall v, double jetzt)
    {
        if (_simulation is null || NachtTuer(v) is not { } tuer) return null;
        if (_passanten.Count > VieleFiguren && !ImBild(tuer, 40f)) return null; // große Stadt: nur was nahe der Kamera geschieht (wie die Passanten)
        var eigen = v.Taeterhaus == _spielerKonto;
        var bande = v.IstBande;
        var anzahl = Nachtgang.Anzahl(bande, v.Minute, v.Ziel);
        var lichter = NachtLichter();

        // Wege: eigene Diebe kommen aus ihrem Versteck, die übrigen aus der Dunkelheit der Gassen. Der dunkelste Weg ist der Hinweg, der zweitdunkelste die Flucht.
        var wege = new List<Vector3[]>();
        if (eigen && !bande && v.Taeterbetrieb is { } versteck && TuerAnker(versteck) is { } start && NachtRoute(start, tuer) is { } r0) wege.Add(r0);
        var hatVersteck = wege.Count > 0;
        var knoten = _netz.Knoten;
        var passend = Enumerable.Range(0, knoten.Count)
            .Where(i => { var d = new Vector2(knoten[i].X - tuer.X, knoten[i].Z - tuer.Z).Length(); return d is >= 22 and <= 48; })
            .OrderBy(i => Taschendiebgang.Streu(i, v.Minute, 12)).Take(5);
        foreach (var i in passend)
            if (NachtRoute(knoten[i], tuer) is { } r && Gassennetz.Laenge(r) > 8) wege.Add(r);
        if (wege.Count == 0) return null;
        var gewertet = wege.Select(r => (Route: r, Licht: NachtLicht(Wegende(r, Nachtgang.AnschleichMeter + Nachtgang.NaeherMeter), lichter)))
            .OrderBy(x => x.Licht).ToList();
        if (hatVersteck) gewertet = gewertet.OrderBy(x => ReferenceEquals(x.Route, wege[0]) ? 0 : 1).ThenBy(x => x.Licht).ToList(); // eigene Diebe kommen aus ihrem Versteck
        var hinRoute = Wegende(gewertet[0].Route, Nachtgang.AnschleichMeter + Nachtgang.NaeherMeter);
        var fluchtRoute = Wegende(gewertet[gewertet.Count > 1 ? 1 : 0].Route, Nachtgang.FluchtMeter).Reverse().ToArray();
        var laenge = Gassennetz.Laenge(hinRoute);
        var naeherMeter = Mathf.Min(Nachtgang.NaeherMeter, laenge * 0.4f);
        var hin = WegBis(hinRoute, laenge - naeherMeter);
        var naeher = WegAb(hinRoute, laenge - naeherMeter);

        var blick = tuer - hinRoute[^2];
        blick.Y = 0;
        blick = blick.LengthSquared() < 1e-4f ? Vector3.Forward : blick.Normalized();
        var sack = Nachtgang.TraegtSack(v.Art, v.Erfolg, v.Wert);
        var betritt = Nachtgang.Betritt(v.Art, v.Erfolg);
        var rundenDauer = _simulation.Takt.RundenDauerSekunden;
        var fluchtTempo = v.Zeuge ? 2.4 : sack ? 1.1 : Gehtempo.Gehen;
        var plan = new Nachtgang.Plan(
            Nachtgang.Dauer(Gassennetz.Laenge(hin), Gehtempo.MeterJeSpielminute(Gehtempo.Gebeugt, rundenDauer)),
            Nachtgang.Dauer(Gassennetz.Laenge(naeher), Gehtempo.MeterJeSpielminute(Gehtempo.Gebeugt, rundenDauer)),
            Nachtgang.Dauer(Gassennetz.Laenge(fluchtRoute), Gehtempo.MeterJeSpielminute(fluchtTempo, rundenDauer)),
            betritt).Stauchen(Nachtgang.Verfuegbar(v.Uhrzeit) - (anzahl - 1) * Nachtgang.BandeAbstand); // vor dem nächsten Versuch bzw. 22:59 fertig, bei jeder Rundenlänge

        var nr = ++_nachtZaehler;
        var leute = new List<Diebfigur>();
        for (var i = 0; i < anzahl; i++)
        {
            var nummer = 40000 + nr * 10 + i;
            var kennung = $"dieb:nacht:{nr}:{i}" + (bande ? ":b" : "");
            var aussehen = MarktDiebAussehen(nummer, "Geselle");
            var dieb = DiebfigurBauen(kennung, aussehen, nummer, eigen, null, v.Taeterbetrieb ?? "", null);
            dieb.Knoten.GetNodeOrNull("Marke")?.QueueFree(); // keine Bildpunkte: nachts soll man die Gestalten selbst erkennen
            // kein Mondschein-Rand mehr (war: Fresnel-Überlagerung, ließ die Täter nachts bläulich leuchten): Fremde Diebe sind nachts dunkle Schatten, nur das Hervorheben bei Maus und Auswahl bleibt
            dieb.Beschreibung = d => NachtText(v, d, eigen, bande, anzahl, sack, betritt);
            if (sack && i < 2 && (anzahl < 3 || i < anzahl - 1)) dieb.Sack = NachtSackBauen(dieb);
            leute.Add(dieb);
        }
        var szene = new Nachtszene { Vorfall = v, Start = v.Minute, Plan = plan, Hin = hin, Naeher = naeher, Flucht = fluchtRoute, Tuer = tuer, Blick = blick, Leute = leute, Eigen = eigen, Sack = sack };
        // Kommt eine Streife dem Täter nahe, weicht er aus: auf dem Weg zum Ziel (nicht kurz vor dem Warten) und auf der Flucht (nicht bei einem Zeugen: dann läuft er)
        var hinLaenge = Gassennetz.Laenge(hin);
        var fluchtLaenge = Gassennetz.Laenge(fluchtRoute);
        var vonSeit = Math.Max(0, jetzt - v.Minute);
        szene.AusweichHin = AusweichenPlanen(hin, t => Nachtgang.Bei(t, plan, out var a) == Nachtgang.Abschnitt.Anschleichen ? a * hinLaenge : null,
            vonSeit, plan.Anschleichen - 0.5, v.Minute, null, $"Täter {nr} (Anschleichen)");
        if (!v.Zeuge)
        {
            var fluchtBeginn = plan.Gesamt - plan.Flucht;
            szene.AusweichFlucht = AusweichenPlanen(fluchtRoute, t => Nachtgang.Bei(t, plan, out var a) == Nachtgang.Abschnitt.Flucht ? a * fluchtLaenge : null,
                Math.Max(fluchtBeginn, vonSeit), plan.Gesamt - 0.5, v.Minute, null, $"Täter {nr} (Flucht)");
        }
        return szene;
    }

    /// <summary>Das letzte Stück eines Weges (höchstens <paramref name="meter"/> Meter bis zur Tür).</summary>
    private static Vector3[] Wegende(Vector3[] route, float meter) => WegAb(route, Mathf.Max(0, Gassennetz.Laenge(route) - meter));

    private MeshInstance3D NachtSackBauen(Diebfigur d)
    {
        _sackStoff ??= new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.5f, 0.32f), Roughness = 1f };
        var sack = new MeshInstance3D
        {
            Name = "Sack", Mesh = new CapsuleMesh { Radius = 0.17f, Height = 0.62f }, MaterialOverride = _sackStoff,
            Position = new Vector3(-0.12f, 1.28f, -0.24f), RotationDegrees = new Vector3(28, 0, 18), Scale = new Vector3(1, 1, 0.85f), Visible = false,
        };
        d.Knoten.AddChild(sack);
        return sack;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Setzen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Setzt alle Täter für diese Zeit. Gibt false zurück, wenn der Vorfall vorbei ist.</summary>
    private bool NachtSetzen(Nachtszene s, double jetzt, double sekundenJeMinute, double sekunden)
    {
        var n = s.Leute.Count;
        var seit = jetzt - s.Start;
        var abstand = Nachtgang.BandeAbstand * s.Plan.Skala;
        var ende = s.Plan.Gesamt + (n - 1) * abstand;
        if (seit >= ende) return false;
        var v = s.Vorfall;
        var seite = new Vector3(-s.Blick.Z, 0, s.Blick.X);
        var zeuge = v.Zeuge;
        var traegtSack = s.Sack;
        for (var i = 0; i < n; i++)
        {
            var d = s.Leute[i];
            var tm = seit - i * abstand;
            var abschnitt = Nachtgang.Bei(tm, s.Plan, out var anteil);
            var versatz = seite * ((i - (n - 1) / 2f) * 0.9f);
            var posten = n == 3 && i == n - 1; // der Letzte einer Dreierbande bleibt draußen und hält Ausschau
            Vector3 ort;
            Vector3 blick;
            string bewegung;
            float tempo = 0;
            var neigung = 0.0;
            var sackZeigen = false;
            var ausgewichen = false;
            switch (abschnitt)
            {
                case Nachtgang.Abschnitt.Vorher or Nachtgang.Abschnitt.Nachher:
                    DiebVerstecken(d);
                    continue;
                case Nachtgang.Abschnitt.Anschleichen:
                    ort = Gassennetz.PunktAuf(s.Hin, (float)anteil);
                    blick = Gassennetz.PunktAuf(s.Hin, Mathf.Min(1, (float)anteil + 0.5f / Mathf.Max(1, Gassennetz.Laenge(s.Hin)))) - ort;
                    bewegung = "Gebeugt";
                    tempo = Gehtempo.Gebeugt;
                    if (s.AusweichHin is { } ah && AusweichenStellen(ah, tm, sekundenJeMinute, true, out var ao, out var ab, out var abew, out var at))
                        (ort, blick, bewegung, tempo, ausgewichen) = (ao, ab, abew == "Gehen" ? "Gebeugt" : abew, at, true);
                    break;
                case Nachtgang.Abschnitt.Warten:
                    ort = s.Hin[^1] + versatz;
                    blick = s.Blick;
                    bewegung = "Knien";
                    break;
                case Nachtgang.Abschnitt.Naehern:
                    ort = Gassennetz.PunktAuf(s.Naeher, (float)anteil).Lerp(s.Tuer + versatz, (float)anteil * (float)anteil);
                    blick = s.Blick;
                    bewegung = "Gebeugt";
                    tempo = Gehtempo.Gebeugt;
                    break;
                case Nachtgang.Abschnitt.Arbeiten:
                    ort = s.Tuer + versatz;
                    bewegung = i == 0 ? Nachtgang.Arbeit(v.Art) : "Stehen";
                    blick = i == 0 ? s.Blick : -s.Blick; // der Erste arbeitet an der Tür, die anderen halten Ausschau
                    neigung = i == 0 ? 0.6 : 0;
                    break;
                case Nachtgang.Abschnitt.Drinnen:
                    if (!posten) { DiebVerstecken(d); continue; }
                    ort = s.Tuer + versatz;
                    blick = -s.Blick;
                    bewegung = "Stehen";
                    break;
                default: // Flucht
                    ort = Gassennetz.PunktAuf(s.Flucht, (float)anteil);
                    blick = Gassennetz.PunktAuf(s.Flucht, Mathf.Min(1, (float)anteil + 0.5f / Mathf.Max(1, Gassennetz.Laenge(s.Flucht)))) - ort;
                    sackZeigen = d.Sack is not null;
                    bewegung = zeuge ? "Eilen" : sackZeigen ? "Tragen" : "Gehen";
                    tempo = (float)(Gassennetz.Laenge(s.Flucht) / (s.Plan.Flucht * sekundenJeMinute));
                    if (s.AusweichFlucht is { } af && AusweichenStellen(af, tm, sekundenJeMinute, true, out var fo, out var fb, out var fbew, out var ft))
                        (ort, blick, bewegung, tempo, ausgewichen) = (fo, fb, fbew == "Gehen" && sackZeigen ? "Tragen" : fbew, ft, true);
                    break;
            }
            ort = new Vector3(ort.X, new Vector2(ort.X - s.Tuer.X, ort.Z - s.Tuer.Z).Length() < 1.6f ? s.Tuer.Y : AufBoden(ort).Y, ort.Z);
            d.Zustand = ausgewichen ? "weicht der Streife aus und duckt sich in den Schatten" : Nachtgang.Zustand(abschnitt, v.Art, s.Plan.Betritt, traegtSack, zeuge);
            DiebStellen(d, ort, blick, bewegung, tempo, sekunden, neigung, false);
            if (d.Sack is { } sack && sack.Visible != sackZeigen) sack.Visible = sackZeigen;
            if (i == 0) NachtFolgeProbe(s, abschnitt, anteil);
        }
        return true;
    }

    /// <summary>Test (<c>nachtfolge</c>): dreimal anhalten – kurz vor dem Ziel, an der Tür, auf der Flucht.</summary>
    private void NachtFolgeProbe(Nachtszene s, Nachtgang.Abschnitt abschnitt, double anteil)
    {
        if (!NachtFolge || !s.Eigen || _haltBis != 0 || _nachtHaltStufe >= 3) return;
        var da = _nachtHaltStufe switch
        {
            0 => abschnitt == Nachtgang.Abschnitt.Anschleichen && anteil >= 0.75,
            1 => abschnitt == Nachtgang.Abschnitt.Arbeiten && anteil >= 0.5,
            _ => abschnitt == Nachtgang.Abschnitt.Flucht && anteil >= 0.35,
        };
        if (!da) return;
        _haltBis = Time.GetTicksMsec() + 6000;
        _simulation!.Takt.IstPausiert = true;
        GD.Print($"[nachtdieb] Halt {_nachtHaltStufe} {Time.GetUnixTimeFromSystem():0.0}");
        if (Wasserarten.Startoption("diebbild") is { Length: > 0 } ordner) // Bild des Fensters ablegen (statt Bildschirmfoto von außen)
        {
            var stufe = _nachtHaltStufe;
            GetTree().CreateTimer(1.5).Timeout += () =>
            {
                var datei = $"{ordner}/halt{stufe}.png";
                var bild = GetViewport().GetTexture().GetImage();
                var fehler = bild.SavePng(datei);
                GD.Print($"[nachtdieb] Bild {datei}: {fehler}");
                // Ausschnitt um den ersten Täter, dreifach vergrößert (Pixel scharf), damit man die Gestalt nachts beurteilen kann
                if (_diebfiguren.Values.FirstOrDefault(d => d.Sichtbar && d.Kennung.StartsWith("dieb:nacht:", StringComparison.Ordinal)) is { } figur)
                {
                    var mitte = StadtKamera.UnprojectPosition(figur.Knoten.GlobalPosition + new Vector3(0, 0.9f, 0));
                    var x = Mathf.Clamp((int)mitte.X - 200, 0, bild.GetWidth() - 400);
                    var y = Mathf.Clamp((int)mitte.Y - 112, 0, bild.GetHeight() - 225);
                    var teil = bild.GetRegion(new Rect2I(x, y, 400, 225));
                    teil.Resize(1200, 675, Image.Interpolation.Nearest);
                    teil.SavePng($"{ordner}/halt{stufe}_nah.png");
                }
            };
        }
        _nachtHaltStufe++;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anklicken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Text der Auswahltafel einer nächtlichen Gestalt: eigener Dieb mit Name und Auftrag, eigene Bande als „Bande aus dem Hinterzimmer“, alles Fremde als „Verdächtige
    /// Gestalt“ ohne Haus. Das Ziel steht nur bei den eigenen Leuten.
    /// </summary>
    private string NachtText(Vorfall v, Diebfigur d, bool eigen, bool bande, int anzahl, bool sack, bool betritt)
    {
        if (!eigen)
            return "Verdächtige Gestalt\n\nIn dunklem Gewand, die Gugel tief im Gesicht, huscht sie nachts durch die Gassen. Wem sie dient, weiß niemand."
                   + (d.Zustand.Length > 0 ? $"\nSie {d.Zustand}." : "");
        var auftrag = v.Art switch
        {
            Vorfallart.Einbruch => $"Einbruch in {v.Ziel}",
            Vorfallart.Hauseinbruch => $"Einbruch ins {v.Ziel}",
            Vorfallart.Brandanschlag => $"Brandanschlag auf {v.Ziel}",
            Vorfallart.Hausbrand => $"Feuer am {v.Ziel}",
            Vorfallart.Giftanschlag => $"Krötenschleim im {v.Ziel}",
            Vorfallart.Entfuehrung => $"Entführung aus dem {v.Ziel}",
            _ => $"Auskundschaften: {v.Ziel}",
        };
        var zustand = d.Zustand.Length > 0 ? d.Zustand : "ist unterwegs";
        if (bande)
            return string.Join("\n", "Bande aus dem Hinterzimmer", "", $"Eure Leute – {anzahl} Mann ohne Namen, für Gold angeheuert.", $"Auftrag: {auftrag} – sie {zustand}.");
        var name = v.TaeterName is { Length: > 0 } n ? n : "Euer Dieb";
        return string.Join("\n", name, "", $"Dieb aus {(v.Taeterbetrieb ?? "dem Versteck")} – Euer Mann.", $"Auftrag: {auftrag} – er {zustand}.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Testhilfe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trägt für die Bilder einen Vorfall ein: an der Tür, die der Kamera am nächsten ist (Betrieb eines anderen Hauses für eigene Täter, sonst ein eigener).
    /// <paramref name="art"/>: <c>nacht</c> (eigener Dieb, eigene Bande, fremder Dieb nacheinander), <c>einbruch</c>, <c>bande</c>, <c>fremd</c>.
    /// </summary>
    private void NachtProbeEintragen(Stadtwirtschaft w, Spielzeit zeit, string art)
    {
        var rivale = w.Konten.FirstOrDefault(k => k != _spielerKonto && k.IstAktiv);
        if (_spielerKonto is null || rivale is null) return;
        var fokus = BlickpunktAmBoden();
        Betrieb? Naechster(Func<Betrieb, bool> gilt) => w.Betriebe.Where(b => b.InDerStadt && gilt(b) && TuerAnker(b.Name) is not null)
            .OrderBy(b => TuerAnker(b.Name)!.Value.DistanceTo(fokus)).FirstOrDefault();
        var eigeneZiele = new HashSet<Betrieb>();
        void Eintragen(bool eigen, bool bande, Func<Betrieb, bool> gilt, string name)
        {
            var b = Naechster(b0 => gilt(b0) && (!eigen || !eigeneZiele.Contains(b0))); // die fremde Gestalt darf an dieselbe Tür wie die eigene (wenige Türen in Bildnähe)
            if (b is null) return;
            eigeneZiele.Add(b);
            _nachtVorfaelle.Add(new Vorfall
            {
                Art = Vorfallart.Einbruch, Ort = Vorfallort.Betrieb, Ziel = b.Name, Betrieb = b.Name, Erfolg = true, Wert = 40, Versuch = 0, Opfer = b.Konto,
                Taeterhaus = eigen ? _spielerKonto : rivale, Taeterbetrieb = bande ? null : "Unterschlupf", TaeterName = bande ? null : name,
                Bande = bande ? "Bande" : null, Minute = zeit.VergangeneMinuten - (eigen ? 0 : 9), Jahr = zeit.Jahr, Uhrzeit = zeit.UhrzeitInMinuten,
            });
        }
        bool Fremd(Betrieb b) => b.Konto != _spielerKonto;
        if (art is "nacht" or "einbruch") Eintragen(true, false, Fremd, "Hans Hellermann");
        if (art is "nacht" or "bande") Eintragen(true, true, Fremd, "");
        if (art is "nacht" or "fremd") Eintragen(false, false, b => true, "Fremder");
        NachtMelden($"Probe „{art}“: {_nachtVorfaelle.Count} Vorfall/Vorfälle eingetragen");
    }
}
