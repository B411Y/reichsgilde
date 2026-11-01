using System.Diagnostics;
using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;

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
/// Jede Figur ist ein Modell mit Skelett (Stadtansicht.Figuren.cs); die Bewegung folgt dem Zustand: unterwegs Gehen, Eilen,
/// Gebeugt oder Tragen im Takt des Gehtempos, auf dem Markt nach der Stelle (Stadtansicht.Markt.cs), im Umland Hacken, Bücken, Hüten.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Eine Figur auf der Straße und was sie sich gemerkt hat, damit nicht jedes Bild neu gerechnet wird.</summary>
    private sealed class Passant
    {
        /// <summary>Lage und Blickrichtung der Figur (vorne ist +Z); das Modell hängt darunter.</summary>
        public required Node3D Knoten { get; init; }
        public required Figur? Figur { get; init; }
        /// <summary>Geht gebeugt (Bettler, Alte) oder trägt eine Last auf dem Rücken (Kiepe, Bündel).</summary>
        public bool Gebeugt { get; init; }
        public bool Traegt { get; init; }
        /// <summary>Der Weg, für den <see cref="Pfad"/> berechnet wurde (Vergleich über die Referenz: neuer Weg = neue Strecke).</summary>
        public Weg? FuerWeg { get; set; }
        public Vector3[] Pfad { get; set; } = Array.Empty<Vector3>();
        public float Laenge { get; set; }
        /// <summary>Nummer des Einwohners (für die Spur, <see cref="Passantenspur"/>).</summary>
        public int Nummer { get; init; }
        /// <summary>Alter Versatz (Meter, nur für <c>--passantenspur=alt</c> zum Vergleichen).</summary>
        public float Versatz { get; init; }
        /// <summary>Wie weit (Vektor in Metern) die Figur zuletzt neben der Mittellinie des Weges stand (null, wenn sie nicht lief), und von wo der laufende Weg ihren Versatz beginnt (null = losgegangen an einem Ort).</summary>
        public Vector3 VersatzJetzt { get; set; }
        public Vector3 VersatzStart { get; set; }

        /// <summary>Wie weit (Meter) die Figur gerade zusätzlich an den Rand ausweicht, weil ein Karren naht (Schritt 5d).</summary>
        public float Ausweichen { get; set; }
        /// <summary>Eigenes Gehtempo in Metern je echter Sekunde bei Tempo 1 (<see cref="Reichsgilde.Gehtempo"/>).</summary>
        public float Gehtempo { get; init; }
        /// <summary>Wann die Figur für den aktuellen Weg losgeht und ankommt (Spielminuten, mit Bruchteil).</summary>
        public double LaufStart { get; set; }
        public double LaufEnde { get; set; }
        /// <summary>Wo die Figur zuletzt stand (null = sie lief). Steht sie noch dort, wird nichts neu gesetzt.</summary>
        public (Aufenthalt Ort, Weg? Weg)? Stand { get; set; }
        /// <summary>Die Stelle auf dem Markt, zu der sie geht oder an der sie steht, und die, die sie gerade verlässt (−1 = keine).
        /// Die alte wird frei, sobald die Figur losgeht.</summary>
        public int Marktstelle { get; set; } = -1;
        public int AlteMarktstelle { get; set; } = -1;
        /// <summary>Laterne oder Fackel in der Hand, solange es dunkel ist (Stadtansicht.Nachtlichter.cs); null, bis die Figur das erste Mal im Dunkeln draußen ist.</summary>
        public Traglicht? Licht { get; set; }
    }

    private readonly Dictionary<int, Passant> _passanten = new();

    /// <summary>Nummern der Einwohner, die gerade im Dom angekommen sind – dort sitzen sie auf den Bänken (Stadtansicht.Sonderbauten.cs).</summary>
    private readonly HashSet<int> _imDom = new();
    private readonly Dictionary<Aufenthalt, Vector3> _verweilpunkte = new();
    private Node3D? _passantenEbene;

    // Messung der Rechenzeit (nur Protokoll, alle 10 Sekunden)
    private readonly Stopwatch _passantenUhr = new(), _messUhr = Stopwatch.StartNew();
    private double _passantenZeit, _passantenSeitMessung;
    private int _passantenBilder;

    /// <summary>Ab diesem Alter haben Einwohner graues Haar und gehen gebeugt.</summary>
    private const int GrauAb = 55, GebeugtAb = 65;

    /// <summary>Jedes Bild: alle Einwohner an ihren Platz stellen und die Figuren im Bild bewegen.</summary>
    private void PassantenAktualisieren(double minuteGenau, double delta)
    {
        _passantenUhr.Restart();
        FigurenBildVorbereiten(); // Sichtfeld und Bildrate der Figuren (Stadtansicht.Figuren.cs)
        _figurenBewegt = 0;
        var sekunden = _simulation is { Takt.IstPausiert: false } ? delta : 0; // in der Pause stehen auch die Bewegungen still
        MarktAktualisieren(minuteGenau, sekunden); // Auslagen und Verkäufer (Stadtansicht.Markt.cs)
        if (_simulation?.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } einwohnerschaft || !_wohnhaeuserGebaut) return;
        _passantenEbene ??= NeueEbene();
        var ticksJeSekunde = _simulation.Takt.TicksProSekunde;

        var einwohner = einwohnerschaft.Einwohner;
        foreach (var e in einwohner)
        {
            if (!_passanten.TryGetValue(e.Nummer, out var p))
                _passanten[e.Nummer] = p = PassantBauen(e);
            if (!PassantGezeigt(e.Nummer)) // Einstellung „Menge der Passanten“ (Stadtansicht.Grafik.cs)
            {
                PassantVerbergen(p, e.Nummer);
                continue;
            }

            // Wo die Figur ist, sagt ihr eigener Laufplan (siehe LaufPlanen): vor dem Losgehen am alten Ort,
            // nach der Ankunft am neuen, dazwischen unterwegs.
            var weg = e.Weg;
            if (weg is not null && !ReferenceEquals(p.FuerWeg, weg))
            {
                // Geht die Figur gerade im Bild und ist mit ihrem alten Weg noch nicht fertig (sie geht langsamer, als der Kern rechnet), reißt
                // sie nicht ab: Sie geht von der Stelle, an der sie ist, über die Gassen zum Anfang des neuen Weges (siehe Weitergehen).
                var weiter = MitteDesWegesWennImBild(p, minuteGenau);
                p.VersatzStart = weiter is null ? Vector3.Zero : p.VersatzJetzt; // Anschluss an den laufenden Weg: der Versatz geht weich in den neuen über (samt Richtung)
                p.FuerWeg = weg;
                p.Pfad = Strecke(weg, e, p);
                p.Laenge = Gassennetz.Laenge(p.Pfad);
                LaufPlanen(p, weg);
                // Wer schlendert, ist beim Losgehen und Ankommen schon ein Stück weiter auf dem Rundgang: den Weg mit diesen Zeiten neu legen
                if (p.Pfad.Length > 0 && (AufDemRundgang(p.AlteMarktstelle) || AufDemRundgang(p.Marktstelle)))
                {
                    p.Pfad = PfadBauen(weg, e, p, AufDemRundgang(p.AlteMarktstelle) ? p.LaufStart : weg.Abmarsch, AufDemRundgang(p.Marktstelle) ? p.LaufEnde : weg.Ankunft);
                    p.Laenge = Gassennetz.Laenge(p.Pfad);
                }
                if (weiter is { } stelle && p.Pfad.Length > 0) Weitergehen(p, weg, stelle, minuteGenau);
            }
            // Ist der Kern längst fertig und sieht keiner hin (weder die Figur noch ihr Ziel), kommt sie unbemerkt an: nie sichtbar springen
            if (weg is not null && p.Stand is null && p.Knoten.Visible && p.Pfad.Length > 0 && minuteGenau >= weg.Ankunft && minuteGenau < p.LaufEnde
                && !ImBild(p.Knoten.Position) && !ImBild(p.Pfad[^1]))
                p.LaufEnde = minuteGenau;
            var losgegangen = weg is null || minuteGenau >= p.LaufStart;
            if (losgegangen && p.AlteMarktstelle >= 0)
            {
                MarktstelleRaeumen(p.AlteMarktstelle, e.Nummer);
                p.AlteMarktstelle = -1;
            }
            Aufenthalt? wo = weg is null || minuteGenau >= p.LaufEnde ? e.Aufenthalt
                           : !losgegangen ? weg.Von
                           : null;
            if (wo is { } ort)
                AmOrtStellen(p, e, ort, weg, minuteGenau, losgegangen);
            else
                Laufen(p, e.Nummer, minuteGenau, ticksJeSekunde);
            PassantLichtStellen(p, e); // Laterne oder Fackel im Dunkeln (Stadtansicht.Nachtlichter.cs)
            if (p.Knoten.Visible && p.Figur is { } figur)
                FigurImBildBewegen(figur, p.Knoten.Position, sekunden);
        }

        // Verstorbene und Weggezogene verschwinden (nur prüfen, wenn die Zahl nicht stimmt)
        if (einwohner.Count != _passanten.Count)
            foreach (var nummer in _passanten.Keys.Except(einwohner.Select(e => e.Nummer)).ToList())
            {
                MarktstelleRaeumen(_passanten[nummer].Marktstelle, nummer);
                MarktstelleRaeumen(_passanten[nummer].AlteMarktstelle, nummer);
                _passanten[nummer].Knoten.QueueFree();
                _passanten.Remove(nummer);
                _imDom.Remove(nummer);
            }

        if (NaeheMessen) NaeheZaehlen(sekunden);
        PassantenHervorheben();
        _passantenZeit += _passantenUhr.Elapsed.TotalMilliseconds;
        _passantenBilder++;
        _passantenBewegtSumme += _figurenBewegt;
        _passantenSeitMessung += delta;
        if (_passantenSeitMessung >= 10)
        {
            // Wie schnell die Laufenden gerade gehen (Meter je echter Sekunde bei Tempo 1) – zum Prüfen der Gehtempi
            var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
            var laufend = _passanten.Values.Where(p => p.Stand is null && p.Knoten.Visible && p.LaufEnde > p.LaufStart)
                                           .Select(p => p.Laenge / ((p.LaufEnde - p.LaufStart) * sekundenJeMinute)).ToList();
            var tempi = laufend.Count == 0 ? "" : $"Gehtempo Ø {laufend.Average():0.0} m/s, höchstens {laufend.Max():0.0} m/s, ";
            if (NaeheMessen)
                GD.Print($"[passantennaehe] Spur {(SpurAlt ? "alt" : "neu")}, Tempo {_simulation.Takt.Tempo}: seit {_naheSekunden:0} s unter {NaeheAbstand} m: {_naheNeuGesamt} Begegnungen " +
                         $"({_naheNeuGesamt * 60.0 / Math.Max(_naheSekunden, 1):0.0} je Minute; im Bild {_naheNeuImBild}, {_naheNeuImBild * 60.0 / Math.Max(_naheSekunden, 1):0.0} je Minute; " +
                         $"davon gegenläufig {_naheGegen}, gleichläufig {_naheGleich}), im Bild laufen im Mittel {_naheLaeufer / Math.Max(_naheBilder, 1):0.0} Figuren");
            GD.Print($"Passanten: {_passanten.Count} Figuren, {_passanten.Values.Count(p => p.Knoten.Visible)} sichtbar, " +
                     $"{_passantenBewegtSumme / (double)_passantenBilder:0} bewegt je Bild, {tempi}" +
                     $"{_passantenZeit / _passantenBilder:0.00} ms je Bild, {Engine.GetFramesPerSecond()} Bilder/s " +
                     $"({_messUhr.Elapsed.TotalMilliseconds / _passantenBilder:0} ms Bildzeit), " +
                     $"{RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} Zeichenaufrufe");
            _passantenZeit = 0;
            _passantenBilder = 0;
            _passantenBewegtSumme = 0;
            _passantenSeitMessung = 0;
            _messUhr.Restart();
        }
    }

    private long _passantenBewegtSumme;

    // ── Messung für Schicht 4 (Abstoßung): wie oft kommen sich laufende Figuren näher als 0,5 m? (Startoption --passantennaehe=1, nur Protokoll) ──
    private static readonly bool NaeheMessen = Wasserarten.Startoption("passantennaehe") is "1" or "an" or "ja";
    private const float NaeheAbstand = 0.5f;
    private readonly HashSet<long> _naheBeruehrt = new();
    private double _naheSekunden, _naheBilder, _naheLaeufer;
    private long _naheNeuGesamt, _naheNeuImBild, _naheGegen, _naheGleich;

    /// <summary>
    /// Zählt jede neue Berührung zweier laufender Figuren (Abstand auf dem Boden unter 0,5 m; eine Berührung zählt einmal, bis sie sich wieder
    /// trennen). Getrennt nach „im Bild“ und gegenläufig (Blickrichtungen mehr als 90° auseinander) oder gleichläufig. In der Pause zählt nichts.
    /// </summary>
    private void NaeheZaehlen(double sekunden)
    {
        if (sekunden <= 0) return;
        _naheSekunden += sekunden;
        _naheBilder++;
        var laeufer = _passanten.Where(k => k.Value.Stand is null && k.Value.Knoten.Visible).OrderBy(k => k.Key).ToList();
        _naheLaeufer += laeufer.Count(k => ImBild(k.Value.Knoten.Position));
        var jetzt = new HashSet<long>();
        for (var i = 0; i < laeufer.Count; i++)
            for (var j = i + 1; j < laeufer.Count; j++)
            {
                var (a, b) = (laeufer[i].Value.Knoten, laeufer[j].Value.Knoten);
                var d = a.Position - b.Position;
                if (d.X * d.X + d.Z * d.Z >= NaeheAbstand * NaeheAbstand) continue;
                var schluessel = (long)laeufer[i].Key * 1_000_000 + laeufer[j].Key;
                jetzt.Add(schluessel);
                if (_naheBeruehrt.Contains(schluessel)) continue;
                _naheNeuGesamt++;
                if (ImBild(a.Position) && ImBild(b.Position)) _naheNeuImBild++;
                if (Mathf.Cos(a.Rotation.Y - b.Rotation.Y) < 0) _naheGegen++; else _naheGleich++;
            }
        _naheBeruehrt.Clear();
        _naheBeruehrt.UnionWith(jetzt);
    }

    /// <summary>Startoption <c>--passantenspur=alt</c>: die frühere Linie in fünf Spuren statt der Spuren aus <see cref="Passantenspur"/> (zum Vergleichen).</summary>
    private static readonly bool SpurAlt = Wasserarten.Startoption("passantenspur") is "alt";

    /// <summary>
    /// Die Figur steht an ihrem Ort: nur draußen sichtbar. Steht sie schon da, bleibt alles, wie es ist – jede gesetzte Position
    /// kostet Godot Arbeit, auch wenn sie sich nicht ändert. Ausnahme: Wer auf dem Markt schlendert, geht jedes Bild ein Stück weiter.
    /// </summary>
    private void AmOrtStellen(Passant p, Einwohner e, Aufenthalt ort, Weg? weg, double minuteGenau, bool losgegangen)
    {
        var stand = (ort, weg);
        if (ort.Ort == Stadtort.Markt)
        {
            // vor dem Losgehen an der alten Stelle, sonst an der eigenen (nach dem Laden ohne Weg erst jetzt belegt)
            var stelle = losgegangen ? p.Marktstelle : p.AlteMarktstelle;
            if (stelle < 0 && losgegangen) stelle = p.Marktstelle = MarktstelleBelegen(e.Nummer, weg?.Ankunft ?? 0, MarktFuer(e));
            if (p.Stand == stand && !AufDemRundgang(stelle)) return;
            p.Stand = stand;
            _imDom.Remove(e.Nummer);
            p.Knoten.Visible = true;
            AmMarktStellen(p, e, ort, stelle, minuteGenau);
            return;
        }
        if (p.Stand == stand) return;
        p.Stand = stand;
        if (ort.Ort == Stadtort.Kirche && ort.Betrieb is null) _imDom.Add(e.Nummer);
        else _imDom.Remove(e.Nummer);
        var punkt = Draussen(ort) ? Verweilpunkt(ort, e) : (Vector3?)null;
        p.Knoten.Visible = punkt is not null;
        if (punkt is not { } q || p.Figur is not { } figur) return;
        p.Knoten.Position = q;
        // Im Umland bei der Arbeit: auf dem Feld hacken, im Wald Holz aufheben, auf der Weide hüten – in irgendeine Richtung (nur an einer Stelle,
        // die die Karte hat: Eine Karte ohne Weide oder Feld, etwa eine neue aus der Kartenwerkstatt, schickt die Leute trotzdem dorthin)
        var arbeit = ort.Ort == Stadtort.Umland && ort.Stelle is { } s && _umlandstellen.ContainsKey(s)
            ? StellenArt(s) switch { "Feld" => "Hacken", "Wald" => "Buecken", "Weide" => "Hueten", _ => null } : null;
        if (arbeit is not null)
            p.Knoten.Rotation = new Vector3(0, UmlandPlatz(ort.Stelle!, Streuung(e.Nummer, Verweilzeit(ort, e))).Blick, 0);
        BewegungSetzen(figur, arbeit ?? "Stehen");
    }

    /// <summary>An die Stelle auf dem Markt (oder, wenn der Markt voll ist, irgendwo zwischen die Stände) – mit Blick und Bewegung der Stelle.</summary>
    private void AmMarktStellen(Passant p, Einwohner e, Aufenthalt markt, int stelle, double minuteGenau)
    {
        if (p.Figur is not { } figur) return;
        if (stelle < 0)
        {
            p.Knoten.Position = Verweilpunkt(markt, e);
            BewegungSetzen(figur, "Stehen");
            return;
        }
        var s = _marktstellen[stelle];
        var (ort, blick) = MarktstelleLage(stelle, minuteGenau);
        // Auf der Bank: die Hüfte auf Sitzhöhe – kleine Völker sitzen höher und lassen die Beine baumeln
        if (s.Bewegung == "Sitzen") ort.Y = Mathf.Max(s.Platz?.Hoehe ?? _marktY, s.Ort.Y - (figur.Kniehoehe - 0.02f));
        p.Knoten.Position = ort;
        p.Knoten.Rotation = new Vector3(0, blick, 0);
        var takt = s.Rundgang >= 0 ? SchlenderTempo * (float)_simulation!.Takt.Tempo / (GehenTempo * figur.Beinfaktor) : 1;
        BewegungSetzen(figur, s.Bewegung, takt);
    }

    /// <summary>
    /// So viele Meter schafft ein Mensch mit einer Schleife je Sekunde (Blender-Tempo): Gehen 1,4, Eilen 2,6, gebeugt 0,65, mit Last 0,95.
    /// Kleinere Völker nach Beinlänge weniger (<see cref="Figur.Beinfaktor"/>).
    /// </summary>
    private const float GehenTempo = 1.4f, EilenTempo = 2.6f, GebeugtTempo = 0.65f, TragenTempo = 0.95f;

    /// <summary>Ab so viel Meter je Sekunde (mal Beinfaktor) eilt eine Figur statt zu gehen.</summary>
    private const float EilenAb = 2.0f;

    /// <summary>Kommt ein Karren näher als so viel Meter, weicht eine Figur an den Rand aus.</summary>
    private const float PassantenAusweichAbstand = 4.5f;

    /// <summary>
    /// Unterwegs: die Figur läuft im Gassennetz. Die Bewegung folgt dem Tempo, und ihr Takt passt dazu, damit die Füße nicht rutschen:
    /// Bettler und Alte gebeugt, wer schnell ist, eilt, wer eine Kiepe oder ein Bündel trägt, geht mit Last, die übrigen gehen.
    /// </summary>
    private void Laufen(Passant p, int nummer, double minuteGenau, double ticksJeSekunde)
    {
        p.Stand = null;
        _imDom.Remove(nummer);
        if (p.Pfad.Length == 0) { p.Knoten.Visible = false; return; }
        var anteil = (float)Math.Clamp((minuteGenau - p.LaufStart) / (p.LaufEnde - p.LaufStart), 0, 1);
        var laenge = Mathf.Max(p.Laenge, 1);
        var hier = Gassennetz.PunktAuf(p.Pfad, anteil, p.Laenge);
        var fenster = 0.5f / laenge; // ein halber Meter vor und zurück: Ecken werden über etwa einen Meter gerundet
        var weiter = Gassennetz.PunktAuf(p.Pfad, Mathf.Min(1, anteil + fenster), p.Laenge);
        var richtung = weiter - hier;
        var bodenfaktor = 1f; // Weg auf dem Boden je Meter Mittellinie: seitlich versetzt geht die Figur etwas weiter, ihre Schritte laufen entsprechend schneller
        if (richtung.LengthSquared() > 1e-6f)
        {
            richtung = richtung.Normalized();
            var drehung = Mathf.Atan2(richtung.X, richtung.Z);
            // Rechtsverkehr: auf der rechten Gassenhälfte gehen, auf der eigenen Spur der Person mit sanftem Schwanken, am Anfang und Ende weich
            // (Passantenspur.cs); naht ein Karren, weichen sie noch weiter aus (Schritt 5d)
            var naht = false;
            foreach (var karrenAnsicht in _karren.Values)
            {
                var d = karrenAnsicht.Wurzel.Position - hier;
                if (d.X * d.X + d.Z * d.Z < PassantenAusweichAbstand * PassantenAusweichAbstand) { naht = true; break; }
            }
            p.Ausweichen = Mathf.Lerp(p.Ausweichen, naht ? 1.0f : 0f, naht ? 0.12f : 0.04f);
            Vector3 rechts, versatz;
            if (SpurAlt) // zum Vergleichen: die frühere gerade Linie in fünf Spuren
            {
                rechts = new Vector3(-richtung.Z, 0, richtung.X);
                versatz = rechts * (0.55f + Mathf.Abs(p.Versatz) * 0.5f);
            }
            else
            {
                var zurueck = Gassennetz.PunktAuf(p.Pfad, Mathf.Max(0, anteil - fenster), p.Laenge);
                var (rx, rz) = Passantenspur.Rechts((zurueck.X, zurueck.Z), (weiter.X, weiter.Z), (richtung.X, richtung.Z));
                var (strecke, start) = (anteil * p.Laenge, (p.VersatzStart.X, p.VersatzStart.Z));
                var (vx, vz) = Passantenspur.Versatz(p.Nummer, strecke, p.Laenge - strecke, start, (rx, rz));
                var (fx, fz) = Passantenspur.Bewegung(p.Nummer, strecke, p.Laenge - strecke, start, (rx, rz), (richtung.X, richtung.Z));
                (rechts, versatz) = (new Vector3(rx, 0, rz), new Vector3(vx, 0, vz));
                bodenfaktor = Mathf.Sqrt(fx * fx + fz * fz);
                if (bodenfaktor > 1e-3f) drehung = Mathf.Atan2(fx, fz); // die Figur blickt dorthin, wohin sie wirklich geht: kein seitliches Rutschen im Übergang
                else bodenfaktor = 1f;
            }
            p.Knoten.Rotation = new Vector3(0, drehung, 0);
            p.VersatzJetzt = versatz;
            hier += versatz + rechts * p.Ausweichen;
        }
        else p.VersatzJetzt = Vector3.Zero;
        p.Knoten.Position = AufFuss(hier); // auf Gelände auf dem Boden (Stadtansicht.Gelaende.cs)
        p.Knoten.Visible = true;
        if (p.Figur is not { } figur) return;
        // Meter je echter Sekunde (mit Spieltempo)
        var tempo = (float)(p.Laenge * ticksJeSekunde / Math.Max(p.LaufEnde - p.LaufStart, 1e-6));
        var bein = figur.Beinfaktor;
        var (bewegung, grund) = p.Gebeugt ? ("Gebeugt", GebeugtTempo)
                              : tempo > EilenAb * bein ? ("Eilen", EilenTempo)
                              : p.Traegt ? ("Tragen", TragenTempo)
                              : ("Gehen", GehenTempo);
        BewegungSetzen(figur, bewegung, tempo * bodenfaktor / (grund * bein));
    }

    /// <summary>
    /// Plant den Gang einer Figur mit ihrem eigenen, festen Tempo (<see cref="Gehtempo.Planen"/>): pünktlich, wenn die Zeit reicht, sonst früher
    /// los und höchstens 1,3-mal so schnell, und wenn auch das nicht reicht, kommt sie später an, als der Kern sie zählt.
    /// Die Geschwindigkeit gilt in echten Sekunden bei Tempo 1; bei höherem Spieltempo laufen alle entsprechend schneller.
    /// </summary>
    private void LaufPlanen(Passant p, Weg weg)
    {
        var sekundenJeMinute = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        (p.LaufStart, p.LaufEnde) = Gehtempo.Planen(p.Laenge, p.Gehtempo * sekundenJeMinute, weg.Abmarsch, weg.Ankunft);
    }

    /// <summary>
    /// Wo die Figur auf ihrem alten Weg ist (die Mittellinie des Weges, ohne seitlichen Versatz), wenn sie gerade im Bild unterwegs ist und noch nicht
    /// ankommt – sonst null. Nur dann muss ein neuer Weg an sie anschließen.
    /// </summary>
    private Vector3? MitteDesWegesWennImBild(Passant p, double minuteGenau)
    {
        if (p.Stand is not null || !p.Knoten.Visible || p.Pfad.Length == 0 || minuteGenau >= p.LaufEnde || minuteGenau < p.LaufStart) return null;
        if (!ImBild(p.Knoten.Position)) return null;
        var anteil = (float)Math.Clamp((minuteGenau - p.LaufStart) / (p.LaufEnde - p.LaufStart), 0, 1);
        return Gassennetz.PunktAuf(p.Pfad, anteil);
    }

    /// <summary>
    /// Der neue Weg beginnt für diese Figur dort, wo sie gerade geht: von <paramref name="stelle"/> über das Gassennetz (ohne neue Knoten) zum
    /// bisherigen Anfang, dann der Weg. Sie geht sofort weiter, ohne Wartezeit, und holt auf (<see cref="Gehtempo.Weitergehen"/>).
    /// </summary>
    private void Weitergehen(Passant p, Weg weg, Vector3 stelle, double minuteGenau)
    {
        var anfang = p.Pfad[0];
        var pfad = new List<Vector3> { stelle };
        if (_netz.Knoten.Count > 0)
        {
            var (a, b) = (NaechsterKnoten(stelle), NaechsterKnoten(anfang));
            if (a != b)
                foreach (var punkt in _netz.Pfad(a, b))
                    if (pfad[^1].DistanceTo(punkt) > 0.05f) pfad.Add(punkt);
        }
        foreach (var punkt in p.Pfad)
            if (pfad[^1].DistanceTo(punkt) > 0.05f) pfad.Add(punkt);
        p.Pfad = pfad.ToArray();
        p.Laenge = Gassennetz.Laenge(p.Pfad);
        var sekundenJeMinute = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        (p.LaufStart, p.LaufEnde) = Gehtempo.Weitergehen(p.Laenge, p.Gehtempo * sekundenJeMinute, minuteGenau, weg.Ankunft);
    }

    /// <summary>Der Knoten des Gassennetzes, der einem Punkt am nächsten liegt (ohne einen neuen anzulegen).</summary>
    private int NaechsterKnoten(Vector3 punkt)
    {
        var (beste, abstand) = (0, float.MaxValue);
        for (var i = 0; i < _netz.Knoten.Count; i++)
        {
            var d = _netz.Knoten[i].DistanceSquaredTo(new Vector3(punkt.X, _netz.Knoten[i].Y, punkt.Z));
            if (d < abstand) (beste, abstand) = (i, d);
        }
        return beste;
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

    /// <summary>
    /// Auf welchen Marktplatz der Einwohner geht: bei mehreren Plätzen (Köln) auf den, der der Tür seines Hauses am nächsten liegt; ohne Haustür
    /// fest nach der Nummer. Null ohne Marktplatz.
    /// </summary>
    private MarktplatzAnsicht? MarktFuer(Einwohner e)
    {
        if (_maerkte.Count <= 1) return _maerkte.FirstOrDefault();
        if (!_haustueren.TryGetValue(e.Haus, out var tuer)) return _maerkte[(int)(Streuung(e.Nummer, 7) % (uint)_maerkte.Count)];
        var heim = _netz.Knoten[tuer];
        return _maerkte.OrderBy(m => m.Mitte.DistanceTo(heim)).First();
    }

    /// <summary>Der Knoten im Gassennetz, an dem ein Aufenthalt beginnt oder endet (Haustür, Hof der Schänke, Domtür …).</summary>
    private int Anlaufknoten(Aufenthalt a, Einwohner e)
    {
        Vector3 punkt = a.Ort switch
        {
            Stadtort.Heim when _haustueren.TryGetValue(a.Haus, out var tuer) => _netz.Knoten[tuer],
            Stadtort.Schaenke or Stadtort.Kirche when a.Betrieb is { } b && HofVon(b) is { } hof => hof,
            Stadtort.Kirche => _domTuer ?? _markt,
            Stadtort.Rathaus => _rathausTuer ?? _markt,
            Stadtort.Tor when _tore.Count > 0 => _tore[0].Ort,
            Stadtort.Umland when a.Stelle is { } s && _umlandstellen.TryGetValue(s, out var u) => Punkt3(u.Punkt),
            Stadtort.Markt => MarktFuer(e)?.Mitte ?? _markt,
            _ => _markt,
        };
        return _netz.KnotenBei(punkt);
    }

    /// <summary>
    /// Ein Weg im Gassennetz von Anlaufknoten zu Anlaufknoten. Ist ein Ende ein Gassen-Aufenthalt, steht die Figur dort mitten auf einem Wegstück
    /// (<see cref="GassenLage"/>): Dann kommen die beiden Enden des Stücks in Frage, und es gilt die Kombination mit dem kürzesten Gesamtweg
    /// (Netzweg plus Reststück entlang der Gasse). So läuft der ganze Weg auf Gassen, und die letzten Meter gehen nur an der Gasse entlang.
    /// Beide Enden auf demselben Stück: gerade am Stück entlang. Wird nur beim Planen gebaut; die Netzwege sind gemerkt.
    /// </summary>
    private List<Vector3> GassenPfad(Weg weg, Einwohner e)
    {
        if (weg.Von.Ort == Stadtort.Gasse && weg.Nach.Ort == Stadtort.Gasse
            && GassenLage(e, weg.Abmarsch) is { } a && GassenLage(e, weg.Ankunft) is { } b
            && a.Von.IsEqualApprox(b.Von) && a.Bis.IsEqualApprox(b.Bis))
            return new List<Vector3> { a.Punkt, b.Punkt }; // dasselbe Stück: das Netz wird nicht gebraucht

        var anfaenge = Anlaeufe(weg.Von, e, weg.Abmarsch);
        var enden = Anlaeufe(weg.Nach, e, weg.Ankunft);
        List<Vector3>? beste = null;
        var bestes = float.MaxValue;
        foreach (var (von, vorlauf) in anfaenge)
            foreach (var (nach, nachlauf) in enden)
            {
                var weg2 = _netz.Pfad(von, nach);
                if (weg2.Length == 0) continue;
                var gesamt = Gassennetz.Laenge(weg2) + vorlauf + nachlauf;
                if (gesamt >= bestes) continue;
                bestes = gesamt;
                beste = weg2.ToList();
            }
        return beste ?? new List<Vector3>();
    }

    /// <summary>
    /// Die Knoten, über die man einen Aufenthalt erreicht, mit dem Rest bis zum Standpunkt in Metern: sonst einer (Reststück 0), bei einem
    /// Platz in der Gasse die beiden Enden des Wegstücks, auf dem man steht.
    /// </summary>
    private List<(int Knoten, float Reststueck)> Anlaeufe(Aufenthalt a, Einwohner e, long zeitpunkt)
    {
        if (a.Ort != Stadtort.Gasse || GassenLage(e, zeitpunkt) is not { } lage) return new() { (Anlaufknoten(a, e), 0f) };
        return new()
        {
            (_netz.KnotenBei(lage.Von), lage.Punkt.DistanceTo(lage.Von)),
            (_netz.KnotenBei(lage.Bis), lage.Punkt.DistanceTo(lage.Bis)),
        };
    }

    /// <summary>
    /// Die Strecke eines Weges: vom Platz, an dem er stand (Verweilpunkt, Marktstelle oder Tür), über das Gassennetz zum neuen Platz.
    /// Auf dem Markt belegt sie dabei die neue Stelle; die alte behält sie, bis sie losgeht.
    /// </summary>
    private Vector3[] Strecke(Weg weg, Einwohner e, Passant p)
    {
        MarktstelleRaeumen(p.AlteMarktstelle, e.Nummer);
        p.AlteMarktstelle = -1;
        if (weg.Von.Ort == Stadtort.Markt) p.AlteMarktstelle = p.Marktstelle >= 0 ? p.Marktstelle : MarktstelleBelegen(e.Nummer, weg.Abmarsch, MarktFuer(e));
        else MarktstelleRaeumen(p.Marktstelle, e.Nummer);
        p.Marktstelle = weg.Nach.Ort == Stadtort.Markt ? MarktstelleBelegen(e.Nummer, weg.Ankunft, MarktFuer(e)) : -1;

        return PfadBauen(weg, e, p, weg.Abmarsch, weg.Ankunft);
    }

    /// <summary>
    /// Der Weg Punkt für Punkt: Anfang, Gassennetz, Ende. Auf dem Markt betritt und verlässt man den Platz am Rand und geht dort direkt zur Stelle
    /// (<see cref="UeberDenPlatz"/>), statt erst zur Mitte zu laufen. <paramref name="abmarsch"/> und <paramref name="ankunft"/> sind die Spielminuten
    /// (mit Bruchteil), zu denen die alte und die neue Stelle gelten – wichtig für den Rundgang, der sich bewegt.
    /// </summary>
    private Vector3[] PfadBauen(Weg weg, Einwohner e, Passant p, double abmarsch, double ankunft)
    {
        var pfad = GassenPfad(weg, e);
        if (pfad.Count == 0) return Array.Empty<Vector3>();
        if (Draussen(weg.Von)) pfad.Insert(0, StreckenEnde(weg.Von, e, p.AlteMarktstelle, weg.Abmarsch, abmarsch));
        else if (TuerVon(weg.Von) is { } ausgang) pfad.Insert(0, ausgang); // kommt aus der Tür des Modells (mit der Höhe der Stufen)
        if (Draussen(weg.Nach)) pfad.Add(StreckenEnde(weg.Nach, e, p.Marktstelle, weg.Ankunft, ankunft));
        else if (TuerVon(weg.Nach) is { } eingang) pfad.Add(eingang);      // verschwindet erst an der Tür
        return UeberDenPlatz(pfad).ToArray();
    }

    /// <summary>
    /// Die Tür des Hauses, in dem ein Aufenthalt stattfindet (Anker des Modells, Stadtansicht.Modelle.cs): Einwohnerhaus beim Heim, Werkstatt
    /// bei Schänke und Kirche eines Hauses, dazu Dom (Stufen vor dem Portal) und Rathaus (Markthalle). Null ohne Modell (Kulisse): Dann endet der Weg
    /// wie bisher an der Haustür im Gassennetz. Wird nur beim Planen eines Weges gefragt, nicht jedes Bild.
    /// </summary>
    private Vector3? TuerVon(Aufenthalt a) => a.Ort switch
    {
        Stadtort.Heim when EinwohnerKennung(a.Haus) is { } kennung => TuerAnker(kennung),
        Stadtort.Schaenke or Stadtort.Kirche when a.Betrieb is { } betrieb => TuerAnker(betrieb),
        Stadtort.Kirche => TuerAnker("Dom"),
        Stadtort.Rathaus => TuerAnker("Rathaus"),
        _ => null,
    };

    /// <summary>Anfang oder Ende einer Strecke draußen: auf dem Markt die Stelle (am Boden, auch bei einer Bank), sonst der Verweilpunkt.</summary>
    private Vector3 StreckenEnde(Aufenthalt a, Einwohner e, int marktstelle, long zeitpunkt, double minute)
    {
        if (a.Ort != Stadtort.Markt || marktstelle < 0) return Verweilpunkt(a, e, zeitpunkt);
        var ort = MarktstelleLage(marktstelle, minute).Ort;
        return new Vector3(ort.X, Mathf.Min(ort.Y, 0.04f), ort.Z);
    }

    private Vector3 Verweilpunkt(Aufenthalt a, Einwohner e) => Verweilpunkt(a, e, Verweilzeit(a, e));

    /// <summary>Beim Ziel gilt die Ankunftszeit des letzten Weges, sonst (vor dem Abmarsch) dessen Abmarsch.</summary>
    private static long Verweilzeit(Aufenthalt a, Einwohner e) => e.Weg is { } w ? (a == w.Nach ? w.Ankunft : w.Abmarsch) : 0;

    /// <summary>
    /// Wo genau jemand am Markt, in der Gasse, am Tor oder im Umland steht – fest aus Nummer und Zeitpunkt
    /// (kein Würfel, also bei jedem Bild und jedem Laden gleich).
    /// </summary>
    private Vector3 Verweilpunkt(Aufenthalt a, Einwohner e, long zeitpunkt)
    {
        var zahl = Streuung(e.Nummer, zeitpunkt);
        float Teil(int verschiebung) => ((zahl >> verschiebung) & 0xFFFF) / 65535f; // 0 … 1
        return AufFuss(a.Ort switch
        {
            // Markt (nur, wenn alle Stellen besetzt sind): zwischen den Ständen (Marktplatz 24 × 24 m), nicht in Brunnen, Kreuz, Bank oder Stand
            Stadtort.Markt => FreierPunktAufDemMarkt(zahl, MarktFuer(e)),
            // Tor: vor dem ersten Tor der Karte (Greybox: Osttor) neben der Straße
            Stadtort.Tor when _tore.Count > 0 => _tore[0].Ort + _tore[0].Aussen * (3 + Teil(0) * 4)
                                                 + _tore[0].Aussen.Cross(Vector3.Up) * ((Teil(16) < 0.5f ? -1 : 1) * (3 + Teil(8) * 2)),
            // Umland: auf der Weide, zwischen den Furchen, bei Reisig und Stümpfen im Wald (UmlandPlatz)
            Stadtort.Umland when a.Stelle is { } s && _umlandstellen.ContainsKey(s) => UmlandPlatz(s, zahl).Ort,
            // Gasse: irgendwo auf einer Gasse in der Stadt
            _ => GassenLage(e, zeitpunkt)?.Punkt ?? _markt,
        });
    }

    /// <summary>Arbeitsplätze im Wald (Koordinaten im Modell, vorne +Z): bei den Stümpfen, am Reisig und am Holzstapel, mindestens 2 m von jeder Tanne.</summary>
    private static readonly Vector2[] WaldPlaetze =
    {
        new(-2.4f, -1.2f), new(2.2f, -3.0f), new(-3.2f, 6.0f), new(5.0f, 2.2f), new(2.3f, 2.2f), new(-1.6f, 2.4f), new(0.0f, -1.5f), new(-3.6f, -2.0f),
    };

    /// <summary>Was auf der Weide im Weg steht (Koordinaten im Modell): Tränke, zwei Büsche, Weidestein – mit Radius.</summary>
    private static readonly (Vector2 Mitte, float Radius)[] WeideHindernisse =
    {
        (new(4.6f, -2.6f), 1.6f), (new(-5.2f, -3.4f), 1.4f), (new(5.6f, 3.4f), 1.2f), (new(-2.0f, -1.4f), 1.0f),
    };

    /// <summary>
    /// Wo jemand an einer Arbeitsstelle im Umland steht und wohin er blickt – passend zu <c>umland_stellen.glb</c> (<c>tools/modelle/umland_stellen.py</c>):
    /// Feld zwischen zwei Furchen, längs der Reihe; Wald an einem Stumpf oder beim Reisig; Weide irgendwo in der Koppel, nicht in Tränke, Busch oder Stein.
    /// Fest aus der Zahl (Nummer und Zeitpunkt). Die Lage im Modell wird mit der Drehung der Stelle in die Stadt gedreht.
    /// </summary>
    private (Vector3 Ort, float Blick) UmlandPlatz(string stelle, uint zahl)
    {
        float Teil(int verschiebung) => ((zahl >> verschiebung) & 0xFF) / 255f; // 0 … 1
        Vector2 lokal;
        float blick;
        switch (StellenArt(stelle))
        {
            case "Feld":
                // Gassen zwischen den sechs Furchen (Furchen bei x = −6 + 2,4 · i, längs z), die Vogelscheuche steht bei (5,6 | −1)
                lokal = new Vector2(-4.8f + 2.4f * (int)(Teil(0) * 4.99f), -3.5f + 7f * Teil(8));
                if (lokal.X > 4 && Mathf.Abs(lokal.Y + 1) < 1.2f) lokal.Y += 2.4f;
                blick = Teil(16) < 0.5f ? 0 : Mathf.Pi;
                break;
            case "Wald":
                var platz = WaldPlaetze[(int)(Teil(0) * (WaldPlaetze.Length - 0.01f))];
                lokal = platz + new Vector2(Teil(8) - 0.5f, Teil(16) - 0.5f) * 0.6f;
                blick = Teil(24) * Mathf.Tau;
                break;
            default:
                // Weide: Koppel 14 × 10 m mit Zaun; bis zu vier Versuche, bis nichts im Weg steht
                lokal = Vector2.Zero;
                for (var versuch = 0; versuch < 4; versuch++)
                {
                    var z = Streuung((int)zahl, versuch);
                    lokal = new Vector2(((z & 0xFFFF) / 65535f - 0.5f) * 11.5f, (((z >> 16) & 0xFFFF) / 65535f - 0.5f) * 7.5f);
                    if (!WeideHindernisse.Any(h => h.Mitte.DistanceTo(lokal) < h.Radius)) break;
                }
                blick = Teil(24) * Mathf.Tau;
                break;
        }
        var g = _umlandstellen[stelle];
        return (ModellLage(g) * new Vector3(lokal.X, 0, lokal.Y), DrehungVon(g) + blick);
    }

    /// <summary>Welcher Teil von <c>umland_stellen.glb</c> eine Stelle ist („Weide“, „Feld“, „Wald“ – der Knoten ihres Bausteins): Danach richten sich Plätze und Bewegung.</summary>
    private string StellenArt(string stelle) => _umlandstellen.TryGetValue(stelle, out var g) ? g.Baustein?.Knoten ?? stelle : stelle;

    /// <summary>Ein Punkt zwischen den Ständen, an dem nichts steht: bis zu acht feste Versuche aus der Zahl, sonst der erste.</summary>
    private Vector3 FreierPunktAufDemMarkt(uint zahl, MarktplatzAnsicht? platz)
    {
        var (mitte, halb) = platz is null ? (_markt, _marktHalb) : (platz.Mitte, platz.Halb);
        Vector3 erster = default;
        for (var versuch = 0; versuch < 8; versuch++)
        {
            var z = versuch == 0 ? zahl : Streuung((int)zahl, 900 + versuch);
            var punkt = RingUmMitte(mitte, 3f, halb - 1.5f, (z & 0xFFFF) / 65535f, ((z >> 16) & 0xFFFF) / 65535f);
            if (versuch == 0) erster = punkt;
            if (!_marktHindernisse.Any(h => new Vector2(punkt.X - h.Mitte.X, punkt.Z - h.Mitte.Z).Length() < h.Radius + Wegrand)) return punkt;
        }
        return erster;
    }

    private static Vector3 RingUmMitte(Vector3 mitte, float innen, float aussen, float a, float b)
    {
        var winkel = a * Mathf.Tau;
        var radius = innen + b * (aussen - innen);
        return mitte + new Vector3(Mathf.Cos(winkel) * radius, 0, Mathf.Sin(winkel) * radius);
    }

    private List<(Vector3 Von, Vector3 Bis)>? _gassenInDerStadt;

    /// <summary>
    /// Wo jemand in der Gasse steht: ein Punkt auf einem Wegstück der Stadt (nicht auf dem Markt), fest aus Nummer und Zeitpunkt,
    /// dazu die Enden dieses Stücks. Null, wenn es keine Gasse gibt.
    /// </summary>
    private (Vector3 Punkt, Vector3 Von, Vector3 Bis)? GassenLage(Einwohner e, long zeitpunkt)
    {
        var zahl = Streuung(e.Nummer, zeitpunkt);
        var wahl = (zahl & 0xFFFF) / 65535f;
        var stelle = ((zahl >> 16) & 0xFFFF) / 65535f;
        _gassenInDerStadt ??= _netz.Kanten.Where(k => InDerStadt(k.Von) && InDerStadt(k.Bis) && k.Von.DistanceTo(k.Bis) > 3
                                                 && AbstandZumPflaster(k.Von) > 1 && AbstandZumPflaster(k.Bis) > 1) // nicht auf dem Markt
                                     // nach Lage geordnet: Die Kanten liegen in einem HashSet, gleiche Nummer soll nach dem Laden gleiche Gasse sein
                                     .OrderBy(k => k.Von.X).ThenBy(k => k.Von.Z).ThenBy(k => k.Bis.X).ThenBy(k => k.Bis.Z).ToList();
        if (_gassenInDerStadt.Count == 0) return null;
        var (von, bis) = _gassenInDerStadt[(int)(wahl * (_gassenInDerStadt.Count - 1))];
        return (von.Lerp(bis, 0.15f + stelle * 0.7f), von, bis);
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

    // ── Tracht ──────────────────────────────────────────────────────────────
    // Kopfbedeckungen und Lasten sind Trachtteile der Figurmodelle (figur_<volk>.glb); das Spiel blendet sie je Rolle ein.

    private enum Kopfbedeckung { Keine, Strohhut, Barett, Kapuze, Haube, Schleier }
    private enum Last { Keine, Kiepe, Buendel, Tasche, Stab }

    /// <summary>Tracht einer Rolle: Gewandfarbe, Kopfbedeckung, was sie trägt, ob sie eine Schürze hat und gebeugt geht.</summary>
    private sealed record PassantenTracht(Color Gewand, Kopfbedeckung Kopf, Last Last, bool Schuerze = false, bool Gebeugt = false, Color? Kopffarbe = null);

    private static readonly Color Leinenfarbe = new(0.78f, 0.74f, 0.64f);
    private static readonly Color Schwarz = new(0.12f, 0.11f, 0.12f);

    /// <summary>
    /// Die Tracht nach Rolle (Schlüssel wie im Kern, z. B. „Knecht“ auch für die Magd). Woran man jemanden erkennt:
    /// Ratsherr roter Mantel und Barett, Mönch braune Kutte mit Kapuze (Nonne schwarz mit weißem Schleier), Bettler graue
    /// Lumpen und gebeugt, Hirte Stab und Strohhut, Feldarbeiter Strohhut, Holzsammler Reisigbündel, Krämer Kiepe,
    /// Bote blaues Wams mit Tasche, Knecht und Magd Schürze, Verwitwete schwarz, Bürgerkinder kräftige Farben mit Barett.
    /// </summary>
    private static PassantenTracht TrachtVon(Einwohner e)
    {
        var frau = e.Geschlecht == Geschlecht.Frau;
        var farbe = Gewandfarben[e.Nummer * 7 % Gewandfarben.Length];
        return e.Rolle switch
        {
            "Ratsherr" => new PassantenTracht(new Color(0.55f, 0.12f, 0.12f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Barett, Last.Keine, Kopffarbe: frau ? Leinenfarbe : Schwarz),
            "Bürgersohn" => new PassantenTracht(frau ? new Color(0.45f, 0.22f, 0.50f) : new Color(0.18f, 0.30f, 0.58f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Barett, Last.Keine, Kopffarbe: frau ? Leinenfarbe : Schwarz),
            "Mönch" => frau ? new PassantenTracht(Schwarz, Kopfbedeckung.Schleier, Last.Keine, Kopffarbe: new Color(0.92f, 0.92f, 0.88f))
                            : new PassantenTracht(new Color(0.36f, 0.24f, 0.14f), Kopfbedeckung.Kapuze, Last.Keine),
            "Bettler" => new PassantenTracht(new Color(0.36f, 0.35f, 0.33f), Kopfbedeckung.Keine, Last.Keine, Gebeugt: true),
            "Witwer" => new PassantenTracht(Schwarz, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Kopffarbe: Schwarz),
            "Hirte" => new PassantenTracht(new Color(0.40f, 0.36f, 0.24f), Kopfbedeckung.Strohhut, Last.Stab),
            "Feldarbeiter" => new PassantenTracht(new Color(0.58f, 0.50f, 0.36f), Kopfbedeckung.Strohhut, Last.Keine),
            "Holzsammler" => new PassantenTracht(new Color(0.42f, 0.30f, 0.20f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Buendel, Kopffarbe: Leinenfarbe),
            "Krämer" => new PassantenTracht(new Color(0.24f, 0.42f, 0.26f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Kiepe, Kopffarbe: Leinenfarbe),
            "Bote" => new PassantenTracht(new Color(0.20f, 0.32f, 0.62f), Kopfbedeckung.Keine, Last.Tasche),
            "Knecht" => new PassantenTracht(farbe, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Schuerze: true, Kopffarbe: Leinenfarbe),
            _ => new PassantenTracht(farbe, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Kopffarbe: Leinenfarbe), // Tagelöhner und neue Rollen
        };
    }

    /// <summary>Aussehen eines Einwohners: Gestalt nach Volk und Geschlecht, Tracht nach Rolle, Haar nach Volk (im Alter grau).</summary>
    private FigurAussehen AussehenVon(Einwohner e, PassantenTracht tracht)
    {
        var haare = Haarfarben(e.Volk);
        var alt = _simulation is { } sim && e.Alter(sim.Zeit.Jahr) >= GrauAb;
        return new FigurAussehen(e.Volk, e.Geschlecht == Geschlecht.Frau, tracht.Gewand, Beinkleidfarben[e.Nummer * 3 % Beinkleidfarben.Length],
            HautVon(e.Volk), alt ? GrauesHaar : haare[e.Nummer % haare.Length], tracht.Kopffarbe ?? tracht.Gewand, tracht.Kopf, tracht.Last, tracht.Schuerze);
    }

    /// <summary>
    /// Ein Einwohner sitzt drinnen (Dom): Gestalt, Gewand und Haut wie draußen, damit man ihn wiedererkennt.
    /// Den Strohhut nimmt man in der Kirche ab; Kiepe, Bündel und Stab bleiben draußen.
    /// </summary>
    private Arbeiterfigur SitzendeFigur(InnenAnsicht a, Einwohner e, Arbeitsplatz platz)
    {
        var tracht = TrachtVon(e);
        var aussehen = AussehenVon(e, tracht) with { Last = Last.Keine, Kopf = tracht.Kopf == Kopfbedeckung.Strohhut ? Kopfbedeckung.Keine : tracht.Kopf };
        var figur = SitzendBauen(a, platz, aussehen, e.Nummer);
        a.Besucher.Add(figur);
        return figur;
    }

    /// <summary>Gläubiger zur Messe im Dom (kein Einwohner): Volk und Gewand aus der Nummer, zunächst versteckt.</summary>
    private Arbeiterfigur GlaeubigerBauen(InnenAnsicht a, Arbeitsplatz platz, int nummer)
    {
        var figur = SitzendBauen(a, platz, AussehenAusNummer(nummer), 5000 + nummer);
        figur.Koerper.Visible = false;
        a.Besucher.Add(figur);
        return figur;
    }

    /// <summary>Oberkante der Dombänke (Stadtansicht.Sonderbauten.cs): Darauf ruht die Hüfte der Sitzenden.</summary>
    private const float DomSitzhoehe = 0.49f;

    /// <summary>Die sitzenden Figuren im Dom und ihr Körperknoten – bewegt werden sie nur, solange man hineinsieht (<see cref="SitzendeBewegen"/>).</summary>
    private readonly Dictionary<Node3D, Figur> _sitzende = new();

    /// <summary>
    /// Eine sitzende Figur auf einer Bank im Dom. Sie gibt sich als <see cref="Arbeiterfigur"/> aus, damit der Innenraum sie wie die übrigen
    /// Besucher zeigt und wegräumt; der Arm ist ein leerer Knoten, denn das Modell bewegt sich selbst („Sitzen“).
    /// </summary>
    private Arbeiterfigur SitzendBauen(InnenAnsicht a, Arbeitsplatz platz, FigurAussehen aussehen, int nummer)
    {
        // Die Innenraumfiguren blicken bei Drehung 0 nach −Z, die Modelle nach +Z: eine halbe Drehung mehr.
        var koerper = new Node3D { Position = a.Mitte + platz.Ort, Rotation = new Vector3(0, platz.Drehung + Mathf.Pi, 0) };
        a.Innen!.AddChild(koerper);
        var arm = new Node3D();
        koerper.AddChild(arm);
        if (FigurEinsetzen(koerper, aussehen, nummer) is { } figur)
        {
            BewegungSetzen(figur, "Sitzen");
            koerper.Position += new Vector3(0, DomSitzhoehe - (figur.Kniehoehe - 0.02f), 0); // kleine Völker sitzen höher
            _sitzende[koerper] = figur;
        }
        return new Arbeiterfigur(koerper, arm, nummer * 0.7f, Bewegung.Beten, Vector3.Zero);
    }

    /// <summary>Bewegt die sichtbaren Sitzenden im Dom, solange man hineinsieht (in der Pause nicht; „Sitzen“ ist langsam, also jedes dritte Bild).</summary>
    private void SitzendeBewegen()
    {
        var sekunden = _simulation?.Takt.IstPausiert == true ? 0 : GetProcessDeltaTime();
        foreach (var (koerper, figur) in _sitzende)
            if (koerper.Visible) FigurImBildBewegen(figur, null, sekunden);
    }

    /// <summary>Eine Figur steht auf: aus dem Dom entfernen.</summary>
    private void SitzendeEntfernen(Arbeiterfigur figur)
    {
        _sitzende.Remove(figur.Koerper);
        KnotenEntfernen(figur.Koerper);
    }

    private Passant PassantBauen(Einwohner e)
    {
        var knoten = new Node3D { Name = $"Einwohner_{e.Nummer}", Visible = false };
        _passantenEbene!.AddChild(knoten);
        var tracht = TrachtVon(e);
        var alt = _simulation is { } sim && e.Alter(sim.Zeit.Jahr) >= GebeugtAb;
        var gebeugt = tracht.Gebeugt || alt;
        var ki = KiSchmiedFuer(e, tracht, gebeugt);
        if (ki) _kiPassanten.Add(e.Nummer);
        return new Passant
        {
            Knoten = knoten, Figur = FigurEinsetzen(knoten, AussehenVon(e, tracht) with { KiSchmied = ki }, e.Nummer),
            Gebeugt = gebeugt, Traegt = tracht.Last is Last.Kiepe or Last.Buendel,
            Nummer = e.Nummer, Versatz = ((e.Nummer * 37 % 9) - 4) * 0.22f, Gehtempo = GehtempoVon(e, gebeugt, tracht.Last is Last.Kiepe or Last.Buendel),
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anklicken und Hervorheben
    // ═════════════════════════════════════════════════════════════════════════

    public static string EinwohnerKennung(Einwohner e) => "einwohner:" + e.Nummer;

    /// <summary>
    /// Welche sichtbare Figur ist an dieser Bildschirmstelle? Figuren haben keine Klickfläche (die müsste jedes Bild
    /// mitwandern); stattdessen wird jede sichtbare Figur auf den Bildschirm projiziert und die nächste genommen,
    /// wenn sie nah genug am Mauszeiger ist. Das kostet bei 225 Figuren nur einen Klick lang Rechenzeit.
    /// </summary>
    private string? PassantBei(Vector2 bildschirm)
    {
        if (_raum is not null || _innen is not null || _passanten.Count == 0) return null;
        // Wie viele Pixel ist eine Figur hoch? Die Kamerahöhe in Metern ist _kamera.Size.
        var pixelJeMeter = GetViewport().GetVisibleRect().Size.Y / _kamera.Size;
        var fang = Mathf.Max(8, 1.1f * pixelJeMeter);
        string? beste = null;
        var besterAbstand = fang;
        foreach (var (nummer, p) in _passanten)
        {
            if (!p.Knoten.Visible) continue;
            var mitte = p.Knoten.GlobalPosition + new Vector3(0, 0.9f, 0);
            if (StadtKamera.IsPositionBehind(mitte)) continue;
            var abstand = StadtKamera.UnprojectPosition(mitte).DistanceTo(bildschirm);
            if (abstand < besterAbstand) { besterAbstand = abstand; beste = "einwohner:" + nummer; }
        }
        return beste;
    }

    /// <summary>Hebt die Figur unter der Maus und die ausgewählte hell hervor (nur bei Änderung).</summary>
    private void PassantenHervorheben()
    {
        // Nummern einmal herauslesen statt je Figur einen Text zu bauen (das wären jedes Bild hunderte neue Texte)
        var maus = EinwohnerNummer(_unterDerMaus);
        var gewaehlt = EinwohnerNummer(Auswahl);
        foreach (var (nummer, p) in _passanten)
            if (p.Figur is { } figur) FigurHervorheben(figur, nummer == maus || nummer == gewaehlt);
    }

    private static int? EinwohnerNummer(string? kennung) =>
        kennung is not null && kennung.StartsWith("einwohner:") && int.TryParse(kennung.AsSpan("einwohner:".Length), out var n) ? n : null;

    /// <summary>Text für die Auswahltafel: wer, was, wo, wie gestimmt. Null, wenn die Kennung kein Einwohner ist.</summary>
    public string? EinwohnerText(string kennung)
    {
        if (!kennung.StartsWith("einwohner:") || !int.TryParse(kennung["einwohner:".Length..], out var nummer)) return null;
        if (_simulation?.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } ew) return null;
        if (ew.Nummer(nummer) is not { } e) return "Dieser Einwohner lebt nicht mehr in der Stadt.";
        var zeit = _simulation.Zeit;
        var volk = e.Volk switch { Volk.Zwerg => "Zwerg", Volk.Elb => "Elb", Volk.Halbling => "Halbling", Volk.Ork => "Ork", Volk.Oger => "Oger", _ => "Mensch" };
        if (e.Geschlecht == Geschlecht.Frau) volk = e.Volk switch { Volk.Zwerg => "Zwergin", Volk.Elb => "Elbin", Volk.Halbling => "Halbling", _ => "Mensch" };
        var herkunft = e.Herkunft switch
        {
            Herkunft.Ratsherr => " · im Stadtrat",
            Herkunft.Kandidat => " · in der Brautschau",
            _ => "",
        };
        var minute = zeit.VergangeneMinuten + _simulation.Takt.AngebrocheneMinute;
        var tun = e.Handlung switch
        {
            Handlung.Ruhen => "ruht sich aus",
            Handlung.Arbeiten => "arbeitet",
            Handlung.Einkaufen => "kauft ein",
            Handlung.Einkehren => "kehrt ein",
            Handlung.Beten => "betet",
            Handlung.Flanieren => "geht spazieren",
            _ => e.Handlung.ToString(),
        };
        string wo;
        if (_passanten.TryGetValue(e.Nummer, out var p) && p.Stand is null && e.Weg is { } weg && minute < p.LaufEnde)
            wo = $"unterwegs {OrtText(weg.Nach, mitZu: true)} (an etwa {new Reichsgilde.Core.Zeit.Spielzeit((long)Math.Ceiling(p.LaufEnde)).UhrzeitText} Uhr)";
        else
            wo = OrtText(e.Aufenthalt, mitZu: false);
        var haus = ew.Wohnhaeuser.FirstOrDefault(h => h.Nummer == e.Wohnhaus) is { } stadthaus
            ? $"Wohnt in einem {(stadthaus.Art switch { Hausart.Ratsherrenhaus => "Ratsherrenhaus", Hausart.Buergerhaus => "Bürgerhaus", _ => "einfachen Haus" })}"
              + (_hausplaetze.TryGetValue(stadthaus.Nummer, out var platz) && !InDerStadt(platz.Mitte) ? " in der Vorstadt" : " in der Stadt")
            : null;
        var stimmung = e.Stimmung switch { >= 75 => "zufrieden", >= 55 => "gut gelaunt", >= 35 => "mürrisch", _ => "unzufrieden" };
        return string.Join("\n", new string?[]
        {
            e.Name,
            "",
            $"{ew.RollenName(e)} · {volk}, {e.Alter(zeit.Jahr)} Jahre{herkunft}",
            $"{char.ToUpper(tun[0])}{tun[1..]} – {wo}.",
            haus,
            $"Stimmung: {stimmung} ({e.Stimmung:0} von 100)",
        }.OfType<string>()); // ohne Haus fällt die Zeile weg
    }

    private string OrtText(Aufenthalt a, bool mitZu) => a.Ort switch
    {
        Stadtort.Heim => mitZu ? "nach Hause" : "zu Hause",
        Stadtort.Markt => mitZu ? "zum Markt" : "am Markt",
        Stadtort.Gasse => mitZu ? "in die Gassen" : "in den Gassen",
        Stadtort.Schaenke => (mitZu ? "zur " : "in der ") + (a.Betrieb ?? "Schänke"),
        Stadtort.Kirche => a.Betrieb is { } k ? (mitZu ? "zur " : "in der ") + k : (mitZu ? "zum Dom" : "im Dom"),
        Stadtort.Rathaus => mitZu ? "zum Rathaus" : "im Rathaus",
        Stadtort.Tor => (mitZu ? "zum " : "am ") + (_tore.Count > 0 ? _tore[0].Name : "Tor"),
        Stadtort.Umland => (mitZu ? "hinaus " : "draußen ") + (a.Stelle switch { "Weide" => mitZu ? "auf die Weide" : "auf der Weide", "Feld" => mitZu ? "aufs Feld" : "auf dem Feld", "Wald" => mitZu ? "in den Wald" : "im Wald", _ => "im Umland" }),
        _ => a.Ort.ToString(),
    };

    /// <summary>
    /// Gehtempo in Metern je echter Sekunde (bei Tempo 1), fest nach der Nummer (<see cref="Gehtempo"/>): 1,3 m/s, ±10 % je Person; Alte (gebeugt),
    /// Bettler und Witwer 1,0, Boten 1,8; wer eine Last trägt, ist etwas langsamer.
    /// </summary>
    private static float GehtempoVon(Einwohner e, bool gebeugt, bool traegt)
    {
        var zahl = Streuung(e.Nummer, 977);
        var feinheit = ((zahl >> 16) & 0xFFFF) / 65535f;
        return Gehtempo.Mit(Gehtempo.Grund(e.Rolle, gebeugt, traegt), feinheit);
    }
}
