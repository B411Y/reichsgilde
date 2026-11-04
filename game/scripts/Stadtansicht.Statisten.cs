using System.Diagnostics;
using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Dynastie;

namespace Reichsgilde;

/// <summary>
/// Straßenvolk (siebter Teil der <c>partial class Stadtansicht</c>): Statisten, die in großen Städten das Gedränge füllen. Reine Darstellung.
///
/// Der Kern kennt nur die Bürger der Häuser (25 je Haus), in Köln etwa 190 Einwohner – verteilt auf eine Stadt von einem Kilometer Länge, von
/// denen mittags nur ein Teil draußen ist. Wer am Heumarkt hinsieht, sah deshalb nur eine Handvoll Leute. Die Statisten haben keine Nummer im
/// Kern, keinen Beruf und keinen Aufenthalt, sind nicht anklickbar und zählen nirgends; der Kern liest und ändert sich nicht.
///
/// Wie viele es sind, richtet sich nach dem, was die Kamera sieht – nicht nach der ganzen Stadt, so bleibt die Rechenzeit gleich, wie groß die Stadt ist:
///   • Länge der Gassen im Bild (alle halbe Sekunde gezählt) × Dichte je Meter × Größe der Stadt (Wurzel aus Einwohnern ÷ 100, 0,8 bis 1,6),
///   • × Tageslauf (morgens Markt und Handwerk voll, mittags viel, abends weniger, nachts fast keine),
///   • × Wetter (Regen und Schnee halten die Leute im Haus), × Einstellung „Menge der Passanten“, × Entfernung der Kamera (herausgezoomt weniger),
///   • höchstens <see cref="StatistenHoechstens"/> zugleich.
/// Sie kommen am Rand des Bildes aus der Gasse, gehen im Gassennetz (Rechtsverkehr) von einem Rand zum anderen, manche bleiben unterwegs kurz stehen,
/// und verschwinden wieder am Rand – nie sichtbar mitten im Bild. Weit außerhalb der Kamera gibt es keine. Figuren im Bild bewegen sich wie die
/// Einwohner (Stadtansicht.Figuren.cs); die Lage rechnet nur aus der Spielzeit, ohne Zufall des Kerns.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Startoption <c>--statisten=aus|&lt;Faktor&gt;</c>: aus = nur die Einwohner (wie früher), sonst Faktor auf die Dichte (Standard 1).</summary>
    private static readonly float StatistenFaktor = Statistenfaktor();

    private static float Statistenfaktor()
    {
        var wert = Wasserarten.Startoption("statisten");
        if (wert is null) return 1f;
        if (wert is "aus" or "0" or "nein") return 0f;
        return float.TryParse(wert, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? Mathf.Clamp(f, 0f, 5f) : 1f;
    }

    /// <summary>Wie viele Statisten es zugleich höchstens gibt (Obergrenze der Figuren und damit der Zeichenaufrufe).</summary>
    private const int StatistenHoechstens = 100;

    /// <summary>Wie viele Statisten im Bild zugleich höchstens (die übrigen sind im Streifen am Rand unterwegs).</summary>
    private const int StatistenImBildHoechstens = 70;

    /// <summary>Gewünschte Statisten je Meter Gasse im Bild zur Stoßzeit (Mittel der Tageszeit 1,0).</summary>
    private const float StatistenJeMeter = 0.065f;

    /// <summary>Wie weit (Meter) neben dem Bild noch Statisten wandern, ehe sie verschwinden, und der Streifen, in dem sie auftauchen.</summary>
    private const float StatistenRandAussen = 45f, StatistenRandNah = 22f;

    private sealed class Statist
    {
        public required Node3D Knoten { get; init; }
        public required Figur? Figur { get; init; }
        public required int Nummer { get; init; }
        /// <summary>Gehtempo in Metern je echter Sekunde bei Tempo 1.</summary>
        public required float Tempo { get; init; }
        public required bool Gebeugt { get; init; }
        public bool Aktiv { get; set; }
        public Vector3[] Pfad { get; set; } = Array.Empty<Vector3>();
        /// <summary>Meter vom Anfang des Pfades bis zu jedem Punkt.</summary>
        public float[] Summe { get; set; } = Array.Empty<float>();
        public float Strecke { get; set; }
        public int Stueck { get; set; }
        public int EndKnoten { get; set; }
        /// <summary>Seitlich der Mittellinie (Meter, rechts positiv): Rechtsverkehr mit ein wenig Streuung.</summary>
        public float Spur { get; set; } = 0.7f;
        /// <summary>Wie weit von der Gassenmitte zum Rand sie geht (Anteil der halben Breite) und die halben Breiten jedes Stücks des Pfades.</summary>
        public float SpurAnteil { get; set; } = 0.5f;
        public float[] Breiten { get; set; } = Array.Empty<float>();
        public bool ImBild { get; set; }
        /// <summary>Bleibt bei <see cref="PauseBei"/> Meter bis zu dieser Spielminute stehen (0 = keine Pause geplant).</summary>
        public float PauseBei { get; set; } = -1;
        public double PauseBis { get; set; }
        public bool Pausiert { get; set; }
        public float Drehung { get; set; }
    }

    private Node3D? _statistenEbene;
    private readonly List<Statist> _statisten = new();
    private int _statistenAktiv, _statistenImBild;
    private double _statistenLetzte = -1, _statistenSicht;
    private float _statistenZiel;
    /// <summary>Gassenlänge im Bild, Zahl der Knoten am Rand (Auftauchen) und die Knoten dazu.</summary>
    private float _sichtLaenge;
    private readonly List<int> _statistenRand = new();
    private int _statistenZaehler, _statistenSchritt;
    private bool _statistenErstfuellung = true;
    private Einwohner[] _statistenVorlagen = Array.Empty<Einwohner>();

    // ── Tageslauf und Wetter ──

    /// <summary>
    /// Wie voll die Gassen zu welcher Uhrzeit (Spielstunde 4 bis 23) sind, 1 = Stoßzeit: morgens Markt und Handwerk (Arbeitsbeginn 8 Uhr), mittags viel,
    /// nachmittags ruhiger, abends wenige, in der Nacht fast keine (die Streife ist eigene Darstellung, Stadtansicht.Nachtlichter.cs).
    /// </summary>
    private static readonly float[] StatistenTageslauf =
    {
        0.06f, 0.14f, 0.30f, 0.62f,   // 4, 5, 6, 7
        1.00f, 1.00f, 0.95f, 1.00f,   // 8, 9, 10, 11 (Markt, Handwerk, Messe)
        0.95f, 0.80f, 0.72f, 0.70f,   // 12, 13, 14, 15
        0.66f, 0.60f, 0.45f, 0.28f,   // 16, 17, 18, 19
        0.16f, 0.08f, 0.04f, 0.02f,   // 20, 21, 22, 23
    };

    /// <summary>Tageslauf mit Übergang zwischen den Stunden (Uhrzeit in Spielminuten seit Mitternacht).</summary>
    private static float StatistenTageslaufBei(float uhrzeitMinuten)
    {
        var stunde = Mathf.Clamp(uhrzeitMinuten / 60f - 4f, 0f, StatistenTageslauf.Length - 1.001f);
        var i = (int)stunde;
        return Mathf.Lerp(StatistenTageslauf[i], StatistenTageslauf[i + 1], stunde - i);
    }

    /// <summary>Wetter: bei Regen gehen nur etwa halb so viele vor die Tür, bei Schnee zwei Drittel, im dichten Nebel etwas weniger.</summary>
    private static float StatistenWetterfaktor(Wetterwerte w) =>
        Mathf.Max(0.3f, 1f - 0.55f * w.Regen - 0.35f * w.Schnee - 0.1f * w.Nebel);

    /// <summary>Wie viele Statisten bei diesem Stand gewünscht sind (ohne Obergrenze der Figuren): Gasse im Bild × Dichte × Tageslauf × Wetter × Stadtgröße.</summary>
    private static float StatistenZielFuer(float sichtLaenge, float uhrzeitMinuten, Wetterwerte wetter, float stadtGroesse, float menge, float entfernung) =>
        sichtLaenge * StatistenJeMeter * StatistenTageslaufBei(uhrzeitMinuten) * StatistenWetterfaktor(wetter) * stadtGroesse * menge * entfernung;

    // ── Bild für Bild ──

    private void StatistenAktualisieren(double minuteGenau, double sekunden, Einwohnerschaft einwohnerschaft)
    {
        if (StatistenFaktor <= 0f || _simulation is null) return;
        var uhr = Stopwatch.StartNew();
        StatistenAktualisierenInnen(minuteGenau, sekunden, einwohnerschaft);
        _statistenZeit += uhr.Elapsed.TotalMilliseconds;
        _statistenBilder++;
    }

    private double _statistenZeit;
    private int _statistenBilder;

    private void StatistenAktualisierenInnen(double minuteGenau, double sekunden, Einwohnerschaft einwohnerschaft)
    {
        if (_stadtVerdeckt)
        {
            if (_statistenEbene is not null) _statistenEbene.Visible = false;
            return;
        }
        _statistenEbene ??= new Node3D { Name = "Statisten" };
        if (_statistenEbene.GetParent() is null) AddChild(_statistenEbene);
        _statistenEbene.Visible = true;
        if (_statistenVorlagen.Length == 0 && einwohnerschaft.Einwohner.Count > 0)
        {
            _statistenVorlagen = einwohnerschaft.Einwohner.Where(e => e.Rolle is not ("Hirte" or "Feldarbeiter" or "Holzsammler" or "Ratsherr")).ToArray();
            if (_statistenVorlagen.Length == 0) return;
        }
        if (_statistenVorlagen.Length == 0 || _netz.Knoten.Count < 2) return;

        // Sicht und Zielzahl: zweimal in der Sekunde, nicht jedes Bild
        _statistenSicht -= sekunden;
        if (_statistenSicht <= 0 || _statistenLetzte < 0)
        {
            _statistenSicht = 0.5;
            var zeit = _simulation.Zeit;
            SichtZaehlen();
            var uhr = _lichtUhrVorgabe is { } lichtUhr ? lichtUhr * 60f : zeit.UhrzeitInMinuten + (float)_simulation.Takt.AngebrocheneMinute;
            var gross = Mathf.Clamp(Mathf.Sqrt(einwohnerschaft.Einwohner.Count / 100f), 0.8f, 1.6f);
            var entfernung = _figurenTeiler switch { 1 => 1f, 2 => 0.75f, _ => 0.5f };
            _statistenZiel = Mathf.Min(StatistenImBildHoechstens, StatistenZielFuer(_sichtLaenge, uhr, _wetter, gross, Fenster.PassantenMenge / 100f * StatistenFaktor, entfernung));
        }

        var dm = _statistenLetzte < 0 ? 0 : Math.Max(0, minuteGenau - _statistenLetzte);
        _statistenLetzte = minuteGenau;
        var ticksJeSekunde = _simulation.Takt.TicksProSekunde;
        var rundenDauer = _simulation.Takt.RundenDauerSekunden;
        var wegBudget = 2; // höchstens so viele Wegsuchen (Dijkstra) je Bild

        // Alle bewegen, am Rand auch wieder wegnehmen
        _statistenAktiv = 0;
        _statistenImBild = 0;
        foreach (var s in _statisten)
        {
            if (!s.Aktiv) continue;
            StatistGehen(s, minuteGenau, dm, ticksJeSekunde, rundenDauer, sekunden, ref wegBudget);
            if (!s.Aktiv) continue;
            _statistenAktiv++;
            if (s.ImBild) _statistenImBild++;
        }

        // Fehlende nachholen: je Bild höchstens drei neue (die erste Füllung beim Start ohne Grenze)
        var neu = 0;
        while (_statistenImBild < _statistenZiel && _statistenAktiv < StatistenGesamt() && (neu < 3 || _statistenErstfuellung) && wegBudget > 0 && _statistenRand.Count > 1)
        {
            if (!StatistStarten(minuteGenau, ref wegBudget)) { wegBudget--; neu++; continue; }
            _statistenAktiv++;
            neu++;
            if (_statistenErstfuellung) _statistenImBild++; // mitten auf dem Weg gesetzt: grob gezählt, bis das nächste Bild genau zählt
        }
        if (_statistenErstfuellung && (_statistenImBild >= _statistenZiel || _statistenAktiv >= StatistenGesamt() || neu == 0) && _statistenZiel > 0) _statistenErstfuellung = false;
    }

    /// <summary>Wie viele Statisten bei diesem Ziel zugleich unterwegs sein dürfen (die im Bild und die im Streifen am Rand).</summary>
    private int StatistenGesamt() => Math.Min(StatistenHoechstens, (int)(_statistenZiel * 1.35f) + 3);

    /// <summary>Zählt die Gassen im Bild und merkt sich die Knoten im Streifen darum (hier tauchen die Statisten auf).</summary>
    private void SichtZaehlen()
    {
        var laenge = 0f;
        foreach (var (von, bis) in _netz.Kanten)
        {
            var mitte = (von + bis) * 0.5f;
            var halb = von.DistanceTo(bis) * 0.5f;
            if (StatistRandAbstand(mitte) <= halb) laenge += halb * 2;
        }
        _sichtLaenge = laenge;
        _statistenRand.Clear();
        var knoten = _netz.Knoten;
        // gleichmäßig auswählen, damit die Wege (und ihre Pfad-Zwischenspeicher) sich wiederholen: höchstens 48 Knoten
        var wahl = new List<int>();
        for (var i = 0; i < knoten.Count; i++)
        {
            var abstand = StatistRandAbstand(knoten[i]);
            if (abstand > 1.5f && abstand < StatistenRandNah) wahl.Add(i);
        }
        var schritt = Math.Max(1, wahl.Count / 48);
        for (var i = _statistenSchritt % schritt; i < wahl.Count; i += schritt) _statistenRand.Add(wahl[i]);
        _statistenSchritt++;
    }

    /// <summary>Wie weit (Meter) der Punkt außerhalb des Bildes liegt (größter Abstand zu einer Ebene; ≤ 0 = im Bild).</summary>
    private float StatistRandAbstand(Vector3 punkt)
    {
        punkt = _stadtLage * punkt;
        var weit = float.MinValue;
        foreach (var ebene in _bildEbenen) weit = Mathf.Max(weit, ebene.DistanceTo(punkt));
        return weit;
    }

    private bool StatistStarten(double minute, ref int wegBudget)
    {
        // Platz im Pool: ein freier Statist oder ein neuer
        var s = _statisten.FirstOrDefault(x => !x.Aktiv);
        if (s is null)
        {
            if (_statisten.Count >= StatistenHoechstens) return false;
            s = StatistBauen(_statisten.Count);
            if (s is null) return false;
            _statisten.Add(s);
        }
        var von = _statistenRand[(int)(HashZahl(++_statistenZaehler, 11) % (uint)_statistenRand.Count)];
        if (!StatistWegSuchen(s, von, ref wegBudget, vorBild: false)) return false;
        // Erste Füllung: gleich mitten auf dem Weg (nur beim Start oder wenn die Kamera weit springt), sonst am Rand
        if (_statistenErstfuellung) s.Strecke = s.Summe[^1] * (HashZahl(s.Nummer + _statistenZaehler, 17) % 1000) / 1000f;
        s.Aktiv = true;
        s.Pausiert = false;
        s.Stueck = 0;
        s.Knoten.Visible = true;
        StatistLageSetzen(s, 0);
        return true;
    }

    /// <summary>Sucht einen Weg vom Knoten <paramref name="von"/> quer durchs Bild zu einem anderen Randknoten.</summary>
    private bool StatistWegSuchen(Statist s, int von, ref int wegBudget, bool vorBild)
    {
        for (var versuch = 0; versuch < 3 && wegBudget > 0; versuch++)
        {
            var nach = _statistenRand[(int)(HashZahl(++_statistenZaehler, 23) % (uint)_statistenRand.Count)];
            if (nach == von || _netz.Knoten[von].DistanceTo(_netz.Knoten[nach]) < 45f) continue;
            wegBudget--;
            var pfad = _netz.Pfad(von, nach);
            if (pfad.Length < 2) continue;
            var summe = new float[pfad.Length];
            for (var i = 1; i < pfad.Length; i++) summe[i] = summe[i - 1] + pfad[i - 1].DistanceTo(pfad[i]);
            if (summe[^1] < 30f) continue;
            // Der Weg muss durchs Bild führen, sonst sieht ihn keiner
            var imBild = 0f;
            var erstes = -1f;
            for (var m = 0f; m < summe[^1]; m += 6f)
                if (StatistRandAbstand(PunktBei(pfad, summe, m)) <= 0)
                {
                    imBild += 6f;
                    if (erstes < 0) erstes = m;
                }
            if (imBild < 12f || imBild * 2f < summe[^1]) continue; // mindestens die Hälfte des Weges im Bild, sonst laufen zu viele im Streifen am Rand
            s.Pfad = pfad;
            s.Summe = summe;
            s.Strecke = 0;
            s.Stueck = 0;
            s.EndKnoten = nach;
            s.SpurAnteil = 0.2f + (HashZahl(s.Nummer, 29) % 100) / 100f * 0.65f;
            s.Breiten = PfadBreiten(pfad); // halbe Breite je Stück: auf breiten Wegen geht man weiter rechts
            // manche bleiben im Bild kurz stehen (Plausch, Schaufenster, Warten): ein Drittel, 6 bis 40 Spielminuten
            var z = HashZahl(++_statistenZaehler, 31);
            s.PauseBei = z % 3 == 0 && erstes >= 0 ? erstes + 4f + (z >> 4) % (uint)Math.Max(1, (int)imBild - 8) : -1f;
            s.PauseBis = 0;
            return true;
        }
        return false;
    }

    private static Vector3 PunktBei(Vector3[] pfad, float[] summe, float strecke)
    {
        var i = Array.BinarySearch(summe, strecke);
        if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, pfad.Length - 2);
        var laenge = summe[i + 1] - summe[i];
        return laenge < 1e-4f ? pfad[i] : pfad[i].Lerp(pfad[i + 1], (strecke - summe[i]) / laenge);
    }

    private void StatistGehen(Statist s, double minute, double dm, double ticksJeSekunde, double rundenDauer, double sekunden, ref int wegBudget)
    {
        var meterJeMinute = Gehtempo.MeterJeSpielminute(s.Tempo, rundenDauer);
        if (s.Pausiert)
        {
            if (minute >= s.PauseBis) s.Pausiert = false;
        }
        else
        {
            var vorher = s.Strecke;
            s.Strecke += (float)(dm * meterJeMinute);
            if (s.PauseBei >= 0 && vorher < s.PauseBei && s.Strecke >= s.PauseBei)
            {
                s.Strecke = s.PauseBei;
                s.PauseBei = -1;
                s.Pausiert = true;
                s.PauseBis = minute + 6 + HashZahl(s.Nummer + _statistenZaehler, 37) % 35;
            }
        }
        var laenge = s.Summe[^1];
        if (s.Strecke >= laenge)
        {
            // am Ende (am Rand): noch gewünscht und genug Platz im Bild, dann weiter, sonst weg
            var ende = StatistenPunkt(s, laenge);
            if (_statistenImBild < _statistenZiel && wegBudget > 0 && _statistenRand.Count > 1 && StatistRandAbstand(ende) > 0 && StatistWegSuchen(s, s.EndKnoten, ref wegBudget, true)) { }
            else { StatistWegnehmen(s); return; }
        }
        // Weit ab vom Bild (Kamera ist gesprungen) oder zu viele und außer Sicht: verschwinden
        var pos = StatistenPunkt(s, s.Strecke);
        var abstand = StatistRandAbstand(pos);
        if (abstand > StatistenRandAussen || (abstand > 1.5f && _statistenAktiv > StatistenGesamt())) { StatistWegnehmen(s); return; }
        StatistLageSetzen(s, minute, pos, abstand, sekunden, ticksJeSekunde, meterJeMinute);
    }

    private Vector3 StatistenPunkt(Statist s, float strecke) => PunktBei(s.Pfad, s.Summe, strecke);

    private void StatistLageSetzen(Statist s, double minute) => StatistLageSetzen(s, minute, StatistenPunkt(s, s.Strecke), -1, 0, 1, 0);

    private void StatistLageSetzen(Statist s, double minute, Vector3 mitte, float abstand, double sekunden, double ticksJeSekunde, double meterJeMinute)
    {
        // Richtung des Stückes: ein wenig voraus, damit sie sich an Ecken weich dreht
        var voraus = PunktBei(s.Pfad, s.Summe, Math.Min(s.Summe[^1], s.Strecke + 0.8f));
        var richtung = voraus - mitte;
        richtung.Y = 0;
        if (richtung.LengthSquared() > 1e-6f)
        {
            richtung = richtung.Normalized();
            var ziel = Mathf.Atan2(richtung.X, richtung.Z);
            s.Drehung = sekunden <= 0 ? ziel : Mathf.LerpAngle(s.Drehung, ziel, (float)Math.Min(1, sekunden * 9));
        }
        var rechts = new Vector3(Mathf.Cos(s.Drehung), 0, -Mathf.Sin(s.Drehung));
        var stueck = Math.Clamp(Array.BinarySearch(s.Summe, s.Strecke) is var b && b < 0 ? ~b - 1 : b, 0, s.Breiten.Length - 1);
        var seite = Mathf.Max(0.35f, (s.Breiten.Length == 0 ? 1f : s.Breiten[stueck]) * s.SpurAnteil);
        s.Spur = sekunden <= 0 ? seite : Mathf.MoveToward(s.Spur, seite, (float)sekunden * 1.2f); // weicher Übergang zwischen den Stücken
        var ort = mitte + rechts * s.Spur;
        if (abstand < 0) abstand = StatistRandAbstand(ort);
        var imBild = s.ImBild = abstand <= 1.5f;
        s.Knoten.Visible = abstand <= 10f; // nur im Bild und knapp daneben (der Schatten fällt ins Bild), im Streifen am Rand unsichtbar: spart Zeichenaufrufe
        if (!s.Knoten.Visible) return;
        if (imBild || s.Knoten.Position == Vector3.Zero) s.Knoten.Position = AufFuss(ort);
        else s.Knoten.Position = new Vector3(ort.X, s.Knoten.Position.Y, ort.Z);
        s.Knoten.Rotation = new Vector3(0, s.Drehung, 0);
        if (s.Figur is not { } figur) return;
        if (s.Pausiert) BewegungSetzen(figur, "Stehen");
        else
        {
            var wirklich = (float)(meterJeMinute * ticksJeSekunde); // Meter je echter Sekunde (mit Spieltempo)
            var bein = figur.Beinfaktor;
            var (bewegung, grund) = s.Gebeugt ? ("Gebeugt", GebeugtTempo) : ("Gehen", GehenTempo);
            BewegungSetzen(figur, bewegung, Math.Max(wirklich, 0.01f) / (grund * bein));
        }
        StatistBewegen(figur, ort, sekunden);
    }

    /// <summary>Wie oft (Sekunden) die Pose eines Statisten höchstens neu gesetzt wird: 20 Mal in der Sekunde genügt für Figuren, die im Bild nur zehn Pixel hoch sind.</summary>
    private const double StatistenPoseAbstand = 0.05;

    private void StatistBewegen(Figur f, Vector3 lage, double sekunden)
    {
        if (!f.Neu && f.Aufgespart + sekunden < StatistenPoseAbstand) { f.Aufgespart += sekunden; return; }
        FigurImBildBewegen(f, lage, sekunden);
    }

    private void StatistWegnehmen(Statist s)
    {
        s.Aktiv = false;
        s.Knoten.Visible = false;
        s.Pausiert = false;
    }

    private Statist? StatistBauen(int nummer)
    {
        var vorlage = _statistenVorlagen[(int)(HashZahl(nummer, 41) % (uint)_statistenVorlagen.Length)];
        var tracht = TrachtVon(vorlage);
        // Umlandtrachten (Last Stab) und Bote fallen weg: auf der Gasse trägt man Kiepe, Bündel oder nichts
        if (tracht.Last == Last.Stab) tracht = tracht with { Last = Last.Keine };
        var alt = HashZahl(nummer, 43) % 9 == 0;
        var gebeugt = tracht.Gebeugt || alt;
        var knoten = new Node3D { Name = $"Statist_{nummer}", Visible = false };
        _statistenEbene!.AddChild(knoten);
        var farbe = Gewandfarben[(int)(HashZahl(nummer, 47) % (uint)Gewandfarben.Length)];
        var nr = 100000 + nummer;
        var haare = Haarfarben(vorlage.Volk);
        var aussehen = new FigurAussehen(vorlage.Volk, vorlage.Geschlecht == Geschlecht.Frau,
            tracht.Gewand == Schwarz || tracht.Kopf is Kopfbedeckung.Kapuze or Kopfbedeckung.Schleier ? tracht.Gewand : farbe,
            Beinkleidfarben[nr * 3 % Beinkleidfarben.Length], HautVon(vorlage.Volk), alt ? GrauesHaar : haare[nr % haare.Length],
            tracht.Kopffarbe ?? Leinenfarbe, tracht.Kopf, tracht.Last, tracht.Schuerze);
        var zahl = HashZahl(nummer, 53);
        var tempo = Gehtempo.Mit(Gehtempo.Grund(vorlage.Rolle, gebeugt, tracht.Last is Last.Kiepe or Last.Buendel), ((zahl >> 16) & 0xFFFF) / 65535f);
        return new Statist { Knoten = knoten, Figur = FigurEinsetzen(knoten, aussehen, nr), Nummer = nr, Tempo = tempo, Gebeugt = gebeugt };
    }

    private static uint HashZahl(int nummer, int salz) => Streuung(nummer, salz);
}
