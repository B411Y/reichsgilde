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
        /// <summary>Wie weit (Vektor in Metern) die Figur zuletzt neben der Mittellinie des Weges stand (null, wenn sie nicht lief; mit dem Ausweichen vor Karren), und von wo der laufende Weg ihren Versatz beginnt (null = losgegangen an einem Ort).</summary>
        public Vector3 VersatzJetzt { get; set; }
        /// <summary>Die Mittellinie des Pfades für <see cref="Passantenspur.Gehen"/> (neu gebaut, sobald der Pfad wechselt).</summary>
        public PfadMitte? Mitte { get; set; }
        public Vector3 VersatzStart { get; set; }

        /// <summary>Wie weit (Meter) die Figur gerade zusätzlich an den Rand ausweicht, weil ein Karren naht (Schritt 5d).</summary>
        public float Ausweichen { get; set; }
        /// <summary>
        /// Schicht 4 (Stadtansicht.Passantenausweichen.cs): seitlicher Versatz durch Ausweichen vor anderen Figuren (Meter, rechts positiv), die zuletzt
        /// gewählte Seite, der Tempoanteil (1 = frei) und wie viele Meter sie dadurch hinter ihrem Plan ist (holt sie danach auf).
        /// </summary>
        public float Seitlich { get; set; }
        public int Seite { get; set; }
        public float Bremse { get; set; } = 1;
        public float Rueckstand { get; set; }
        public float Haltezeit { get; set; }
        /// <summary>Wie viele Meter (Mittellinie) sie noch vom nächsten Ende ihres Weges ist (Anfang oder Ziel) – für die Messung.</summary>
        public float AmRand { get; set; }
        /// <summary>Sie geht gleich aus der Tür, wartet aber noch drinnen, bis die Tür frei ist (unsichtbar, rechnet beim Ausweichen weiter mit).</summary>
        public bool WartetDrinnen { get; set; }
        /// <summary>Wie schnell sich <see cref="Seitlich"/> zuletzt änderte (Meter je echter Sekunde): Die Figur dreht sich ein wenig in diese Richtung.</summary>
        public float SeitRate { get; set; }
        /// <summary>Für das Ausweichen, gesetzt in <see cref="Laufen"/>: Rechtsrichtung, Geschwindigkeit (Meter je echter Sekunde) und wie weit der Versatz
        /// nach links und rechts gehen darf, ohne der Wand zu nahe zu kommen.</summary>
        public Vector3 RechtsJetzt { get; set; }
        public Vector3 Geschwindigkeit { get; set; }
        public float RaumLinks { get; set; }
        public float RaumRechts { get; set; }
        /// <summary>Halbe Körperbreite (Meter, nach Volk): Darunter überschneiden sich zwei Figuren.</summary>
        public float Radius { get; init; } = 0.22f;
        /// <summary>Um wie viele Spielminuten sie bei diesem Weg später losgeht als der Kern (<see cref="Passantenspur.Losgehversatz"/>).</summary>
        public double Verzoegerung { get; set; }
        /// <summary>Wann sie an dem Ort angekommen ist, an dem der laufende Weg beginnt (für den Platz in der Gasse, an dem sie stand), und für welchen Weg das gilt.</summary>
        public long? VonZeit { get; set; }
        public Weg? VonZeitFuer { get; set; }
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

        // Markt v3 (Stadtansicht.Marktbummel.cs): von Ziel zu Ziel über den Platz
        /// <summary>Geht sie gerade auf dem Markt von einer Stelle zur nächsten (ein Weg der Darstellung, nicht des Kerns)?</summary>
        public bool ImBummel { get; set; }
        /// <summary>Wann (Spielminute) sie am Markt angekommen ist (−1 = noch nicht), das wievielte Ziel sie hat, bis wann die Pause dauert, seit wann sie an der Stelle steht und seit wann allein in der Gruppe (−1 = nicht allein).</summary>
        public long BummelAnkunft { get; set; } = -1;
        public int BummelZaehler { get; set; }
        public double BummelBis { get; set; }
        public double AnStelleSeit { get; set; }
        public double AlleinSeit { get; set; } = -1;
        /// <summary>Auf welchem Platz sie gerade um den Brunnen geht (−1 = nicht).</summary>
        public int KreistAuf { get; set; } = -1;
        /// <summary>Dreht sich nach der Ankunft noch weich zur Blickrichtung ihrer Stelle.</summary>
        public bool Drehen { get; set; }
        /// <summary>Wie weit die Figur auf dem Markt seitlich der Mittellinie stehen darf (v3, links ≤ 0, rechts ≥ 0; ±3 = keine Grenze), nur begrenzt geändert.</summary>
        public float GrenzeLinks { get; set; } = -3;
        public float GrenzeRechts { get; set; } = 3;
        /// <summary>Markt v3: Der Teil des Weges auf einem Platz ist noch nicht über die freie Fläche gesucht (geschieht kurz vor dem Losgehen).</summary>
        public bool PlatzOffen { get; set; }
        /// <summary>Beginnt der laufende Weg auf dem Pflaster eines Platzes (dann geht sie erst los, wenn der Weg über die Fläche gesucht ist)?</summary>
        public bool PfadBeginntAufPlatz { get; set; }
        /// <summary>Lage im letzten Bild (nur für die Messung).</summary>
        public Vector3? Messlage { get; set; }
    }

    /// <summary>
    /// Der Pfad einer Figur als Mittellinie für die Spur (auf dem Boden, mit der Breite jedes Stücks aus der Karte: <see cref="PfadBreiten"/>), dazu ihre
    /// <see cref="Boden"/>meter (<see cref="Passantenspur.Bodenmeter"/>): Die Figur geht nach dem Weg, den sie auf ihrer Spur wirklich zurücklegt.
    /// </summary>
    private sealed class PfadMitte : Passantenspur.Polylinie
    {
        public PfadMitte(Vector3[] pfad, float[] halbe) : base(pfad.Select(v => (v.X, v.Z)).ToArray(), halbe) => Pfad = pfad;
        public Vector3[] Pfad { get; }
        /// <summary>Ohne gerundete Mittellinie (<c>--passantenspur=v1</c>: wie bis Schicht 3).</summary>
        public bool OhneRundung { get; init; }
        public override (float X, float Z)? Gerundet(float strecke, float fenster) => OhneRundung ? null : base.Gerundet(strecke, fenster);
        public float[] Boden { get; set; } = Array.Empty<float>();
        /// <summary>Abstand der Stützpunkte von <see cref="Boden"/> (lange Wege gröber, <see cref="Passantenspur.BodenschrittLang"/>).</summary>
        public float BodenSchritt { get; set; } = Passantenspur.Bodenschritt;
        public float Bodenlaenge => Boden.Length > 0 && Fertig ? Boden[^1] : Laenge;
        /// <summary>Bis zu welchem Eintrag <see cref="Boden"/> schon gerechnet ist (lange Tabellen werden über mehrere Bilder gefüllt), und ob sie fertig ist.</summary>
        public int BodenBis { get; set; }
        public bool Fertig => BodenBis >= Boden.Length - 1;
        /// <summary>Der Versatz am Anfang, mit dem die Tabelle gerechnet wird.</summary>
        public (float X, float Z) Start { get; set; }
        public int Nummer { get; set; }
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
        _bildSekunden = sekunden;
        _marktUhr += sekunden;
        MarktAktualisieren(minuteGenau, sekunden); // Auslagen und Verkäufer (Stadtansicht.Markt.cs)
        if (_simulation?.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } einwohnerschaft || !_wohnhaeuserGebaut) return;
        _passantenEbene ??= NeueEbene();
        var ticksJeSekunde = _simulation.Takt.TicksProSekunde;

        MarktflaechenPflegen(sekunden); // Markt v3: Belegung der Wege, Stehende (Stadtansicht.Marktbummel.cs)
        Profil(0);
        BodenmeterFuellen();            // angefangene Tabellen der Bodenmeter weiterrechnen, solange Zeit ist
        Profil(1);
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
            Profil(0);
            // Ein neuer Weg des Kerns wird nur geplant, solange in diesem Bild noch Zeit für Wegsuchen ist – zur vollen Stunde bekommen in Köln über hundert
            // zugleich einen Weg, das verteilt sich so auf einige Bilder. Bis dahin bleibt alles, wie es war (wer geht, geht seinen alten Weg weiter, wer steht,
            // steht noch ein, zwei Bilder länger)
            if (weg is not null && !ReferenceEquals(p.FuerWeg, weg) && !MarktV2 && !WegsucheFrei())
            {
                if (p.Stand is null && p.Knoten.Visible && p.Pfad.Length > 0) Laufen(p, e.Nummer, minuteGenau, ticksJeSekunde);
                else if (p.Stand is not null && p.Figur is { Bewegung: "Reden" } redend && !NochGespraech(p)) BewegungSetzen(redend, "Stehen"); // der Partner ist gegangen
                if (p.Knoten.Visible && p.Figur is { } wartend) FigurImBildBewegen(wartend, p.Knoten.Position, sekunden);
                continue;
            }
            var planUhr = weg is not null && !ReferenceEquals(p.FuerWeg, weg) ? Stopwatch.StartNew() : null;
            var netzMs = 0.0;
            if (weg is not null && !ReferenceEquals(p.FuerWeg, weg))
            {
                // Geht die Figur gerade im Bild und ist mit ihrem alten Weg noch nicht fertig (sie geht langsamer, als der Kern rechnet), reißt
                // sie nicht ab: Sie geht von der Stelle, an der sie ist, über die Gassen zum Anfang des neuen Weges (siehe Weitergehen).
                var weiter = MitteDesWegesWennImBild(p, minuteGenau);
                var figurImBild = weiter is not null && ImBild(p.Knoten.Position);
                p.VersatzStart = weiter is null ? Vector3.Zero : p.VersatzJetzt; // Anschluss an den laufenden Weg: der Versatz (samt Ausweichen und Richtung) geht weich in den neuen über
                (p.Ausweichen, p.Seitlich, p.Seite, p.Rueckstand, p.SeitRate, p.Haltezeit, p.Bremse) = (0, 0, 0, 0, 0, 0, 1);
                (p.ImBummel, p.BummelAnkunft, p.KreistAuf) = (false, -1, -1);
                if (weiter is null) (p.GrenzeLinks, p.GrenzeRechts) = (-3f, 3f); // neu von einem Ort aus: keine alte Grenze // ein Weg des Kerns beendet das Bummeln (ein Gang über den Platz geht weiter, siehe Weitergehen)
                // Wo sie steht, wenn der Weg dort beginnt, wo der alte endete: der Platz ihrer Ankunft (nicht ein neuer aus der Abmarschzeit)
                (p.VonZeit, p.VonZeitFuer) = p.FuerWeg is { } vorher && vorher.Nach == weg.Von ? ((long?)vorher.Ankunft, weg) : ((long?)null, (Weg?)null);
                var altPfad = p.Pfad;
                p.FuerWeg = weg;
                var netzUhr = ProfilMessen ? Stopwatch.StartNew() : null;
                p.Pfad = Strecke(weg, e, p);
                if (netzUhr is not null) { netzMs = netzUhr.Elapsed.TotalMilliseconds; _netzMax = Math.Max(_netzMax, netzMs); }
                p.Laenge = Gassennetz.Laenge(p.Pfad);
                LaufPlanen(p, weg);
                // Wer schlendert, ist beim Losgehen und Ankommen schon ein Stück weiter auf dem Rundgang: den Weg mit diesen Zeiten neu legen
                if (p.Pfad.Length > 0 && (AufDemRundgang(p.AlteMarktstelle) || AufDemRundgang(p.Marktstelle)))
                {
                    p.Pfad = PfadBauen(weg, e, p, AufDemRundgang(p.AlteMarktstelle) ? p.LaufStart : weg.Abmarsch, AufDemRundgang(p.Marktstelle) ? p.LaufEnde : weg.Ankunft);
                    p.Laenge = Gassennetz.Laenge(p.Pfad);
                }
                if (weiter is { } strecke && p.Pfad.Length > 0 && (figurImBild || NeuerWegImBild(p, minuteGenau))) Weitergehen(p, weg, altPfad, strecke, minuteGenau);
                var bodenUhr = ProfilMessen ? Stopwatch.StartNew() : null;
                if (!SpurAlt && p.Pfad.Length > 0) MitteAnlegen(p); // Mittellinie gleich hier, Bodenmeter über die nächsten Bilder verteilt (BodenmeterFuellen)
                if (bodenUhr is not null) _bodenMax = Math.Max(_bodenMax, bodenUhr.Elapsed.TotalMilliseconds);
                if (planUhr is not null) WegsucheZaehlen(planUhr.Elapsed.TotalMilliseconds);
            }
            Profil(1); // neue Wege planen
            // Ist der Kern längst fertig und sieht keiner hin (weder die Figur noch ihr Ziel), kommt sie unbemerkt an: nie sichtbar springen
            if (weg is not null && p.Stand is null && p.Knoten.Visible && p.Pfad.Length > 0 && minuteGenau >= weg.Ankunft + p.Verzoegerung && minuteGenau < p.LaufEnde
                && !ImBild(p.Knoten.Position) && !ImBild(AufFuss(p.Pfad[^1])))
                p.LaufEnde = minuteGenau;
            // Markt v3: Weg über den Platz kurz vor dem Losgehen suchen, höchstens so viele je Bild, wie die Zeit erlaubt (Stadtansicht.Marktbummel.cs)
            Profil(0);
            if (p.PlatzOffen && weg is not null && minuteGenau >= p.LaufStart - PlatzVorlauf && !PlatzNachplanen(p, minuteGenau)
                && p.PfadBeginntAufPlatz && minuteGenau >= p.LaufStart && minuteGenau < p.LaufEnde)
            {
                // keine Zeit mehr in diesem Bild und der Weg beginnt auf dem Platz: noch ein Bild stehen bleiben (sonst ginge sie die rohe Linie durch den Brunnen)
                var schieben = minuteGenau - p.LaufStart + 1e-3;
                (p.LaufStart, p.LaufEnde) = (p.LaufStart + schieben, p.LaufEnde + schieben);
            }
            // Bodenmeter: Wer jetzt losginge, deren Tabelle aber noch nicht fertig ist, rechnet ein Stück weiter (wenn Zeit ist) und wartet sonst ein Bild
            if (weg is not null && p.Stand is not null && p.Mitte is { Fertig: false } offen && ReferenceEquals(offen.Pfad, p.Pfad)
                && minuteGenau >= p.LaufStart && minuteGenau < p.LaufEnde)
            {
                if (WegsucheFrei())
                {
                    var uhr = Stopwatch.StartNew();
                    BodenFuellen(offen, 2 * BodenStueck);
                    WegsucheZaehlen(uhr.Elapsed.TotalMilliseconds);
                }
                if (!offen.Fertig)
                {
                    var schieben = minuteGenau - p.LaufStart + 1e-3;
                    (p.LaufStart, p.LaufEnde) = (p.LaufStart + schieben, p.LaufEnde + schieben);
                }
            }
            Profil(1); // Wege planen (auch über die Fläche)
            var losgegangen = weg is null || minuteGenau >= p.LaufStart;
            if (losgegangen && p.AlteMarktstelle >= 0)
            {
                MarktstelleRaeumen(p.AlteMarktstelle, e.Nummer);
                p.AlteMarktstelle = -1;
            }
            // angekommen erst, wenn auch der Rückstand vom Ausweichen aufgeholt ist
            Aufenthalt? wo = weg is null || minuteGenau >= p.LaufEnde && p.Rueckstand <= 0.01f ? e.Aufenthalt
                           : !losgegangen ? weg.Von
                           : null;
            // Markt v3: Wer auf dem Markt von Stelle zu Stelle geht, läuft, bis er dort ist (Stadtansicht.Marktbummel.cs)
            if (p.ImBummel && wo is { Ort: Stadtort.Markt })
            {
                if (minuteGenau < p.LaufEnde || p.Rueckstand > 0.01f) wo = null;
                else p.ImBummel = false;
            }
            Profil(0);
            if (wo is { } ort)
            {
                AmOrtStellen(p, e, ort, weg, minuteGenau, losgegangen);
                Profil(2); // am Ort stellen (auch Bummeln)
            }
            else if (p.Stand is null && p.Knoten.Visible && !p.WartetDrinnen && (p.Nummer + _figurenBild) % AusserhalbJedes != 0 && !ImBild(p.Knoten.Position))
            {
                // Außerhalb des Bildes (mit 1,5 m Rand) läuft sie nur jedes dritte Bild weiter – ihre Lage folgt dem Plan, nichts geht verloren, und ehe sie
                // ins Bild kommt, ist sie wieder jedes Bild dran (1.11.2026: Laufen war in Köln der größte Posten)
            }
            else
            {
                Laufen(p, e.Nummer, minuteGenau, ticksJeSekunde);
                Profil(3); // laufen
            }
            PassantLichtStellen(p, e); // Laterne oder Fackel im Dunkeln (Stadtansicht.Nachtlichter.cs)
            Profil(4); // Licht
            if (p.Knoten.Visible && p.Figur is { } figur)
                FigurImBildBewegen(figur, p.Knoten.Position, sekunden);
            Profil(5); // Figur bewegen (Pose)
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

        Profil(0);
        AusweichenRechnen((float)sekunden, minuteGenau); // Schicht 4 für das nächste Bild (Stadtansicht.Passantenausweichen.cs)
        Profil(6); // Ausweichen
        if (NaeheMessen) { NaeheZaehlen(sekunden); MarktMessen(sekunden); }
        Profil(7); // Messung (nur mit --passantennaehe)
        PassantenHervorheben();
        Profil(8); // Hervorheben
        _passantenZeit += _passantenUhr.Elapsed.TotalMilliseconds;
        _passantenSchlimmstes = Math.Max(_passantenSchlimmstes, _passantenUhr.Elapsed.TotalMilliseconds);
        _passantenBilder++;
        _profilBilder++;
        if (ProfilMessen)
        {
            for (var i = 0; i < _profilBild.Length; i++) _profilMax[i] = Math.Max(_profilMax[i], _profilBild[i]);
            Array.Clear(_profilBild);
            _flaecheBildMax = Math.Max(_flaecheBildMax, _flaechenZeit - _flaecheBildVorher);
            _flaecheBildVorher = _flaechenZeit;
        }
        _passantenBewegtSumme += _figurenBewegt;
        _passantenSeitMessung += delta;
        if (_passantenSeitMessung >= 10)
        {
            // Wie schnell die Laufenden gerade gehen (Meter je echter Sekunde bei Tempo 1) – zum Prüfen der Gehtempi
            var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
            var laufend = _passanten.Values.Where(p => p.Stand is null && p.Knoten.Visible && p.LaufEnde > p.LaufStart)
                                           .Select(p => p.Laenge / ((p.LaufEnde - p.LaufStart) * sekundenJeMinute)).ToList();
            var tempi = laufend.Count == 0 ? "" : $"Gehtempo Ø {laufend.Average():0.0} m/s, höchstens {laufend.Max():0.0} m/s, ";
            if (NaeheMessen) { NaeheMelden(); MarktMelden(); } // Stadtansicht.Passantenausweichen.cs, Stadtansicht.Marktbummel.cs
            if (ProfilMessen) ProfilMelden();
            GD.Print($"Passanten: {_passanten.Count} Figuren, {_passanten.Values.Count(p => p.Knoten.Visible)} sichtbar, " +
                     $"{_passantenBewegtSumme / (double)_passantenBilder:0} bewegt je Bild, {tempi}" +
                     $"{_passantenZeit / _passantenBilder:0.00} ms je Bild (schlimmstes {_passantenSchlimmstes:0.0} ms), {Engine.GetFramesPerSecond()} Bilder/s " +
                     $"({_messUhr.Elapsed.TotalMilliseconds / _passantenBilder:0} ms Bildzeit), " +
                     $"{RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} Zeichenaufrufe" +
                     (MarktV2 ? "" : $", Wege über die Marktfläche {_flaechenWege} in {_flaechenZeit:0.0} ms, Bummelgänge {_bummelGaenge} von {_bummelVersuche}, " +
                                     $"am Markt stehend {_passanten.Values.Count(q => q.Stand is { Ort.Ort: Stadtort.Markt } && q.Knoten.Visible)}, bummelnd {_passanten.Values.Count(q => q.ImBummel)}"));
            (_flaechenWege, _flaechenZeit, _bummelGaenge, _bummelVersuche, _flaecheBildVorher) = (0, 0, 0, 0, 0);
            _passantenZeit = 0;
            _passantenSchlimmstes = 0;
            _passantenBilder = 0;
            _passantenBewegtSumme = 0;
            _passantenSeitMessung = 0;
            _messUhr.Restart();
        }
    }

    private long _passantenBewegtSumme;
    private double _passantenSchlimmstes;

    /// <summary>Laufende außerhalb des Bildes werden nur jedes so vielte Bild weitergestellt (über die Nummer verteilt).</summary>
    private const int AusserhalbJedes = 3;

    // ── Profil der Rechenzeit (Startoption --passantenprofil=1, nur Protokoll) ──
    private static readonly bool ProfilMessen = Wasserarten.Startoption("passantenprofil") is "1" or "an" or "ja";
    private static readonly string[] ProfilNamen = { "Rest", "Wege planen", "am Ort", "Laufen", "Licht", "Pose", "Ausweichen", "Messung", "Hervorheben" };
    private readonly double[] _profilMs = new double[9], _profilBild = new double[9], _profilMax = new double[9];
    private double _flaecheBildMax, _flaecheBildVorher, _netzMax, _bodenMax;
    private readonly Stopwatch _profilUhr = new();
    private int _profilBilder;

    /// <summary>Rechnet die Zeit seit dem letzten Aufruf dem Abschnitt <paramref name="abschnitt"/> zu (0 = Rest, nicht gezählt); ohne Option nichts.</summary>
    private void Profil(int abschnitt)
    {
        if (!ProfilMessen) return;
        if (abschnitt > 0) { _profilMs[abschnitt] += _profilUhr.Elapsed.TotalMilliseconds; _profilBild[abschnitt] += _profilUhr.Elapsed.TotalMilliseconds; }
        _profilUhr.Restart();
    }

    private void ProfilMelden()
    {
        var bilder = Math.Max(_profilBilder, 1);
        GD.Print("[passantenprofil] ms je Bild: " + string.Join(", ", Enumerable.Range(1, 8).Select(i => $"{ProfilNamen[i]} {_profilMs[i] / bilder:0.00} (höchstens {_profilMax[i]:0.0})")) +
                 $", Wege über die Fläche höchstens {_flaecheBildMax:0.0} ms in einem Bild (einzeln {_flaecheEinzelMax:0.0}), ein Weg im Gassennetz höchstens {_netzMax:0.0} ms, eine Mittellinie mit Bodenmetern höchstens {_bodenMax:0.0} ms" +
                 $", Figuren {_passanten.Count}, laufend {_passanten.Values.Count(q => q.Stand is null && q.Knoten.Visible)}, sichtbar {_passanten.Values.Count(q => q.Knoten.Visible)}");
        Array.Clear(_profilMs);
        Array.Clear(_profilMax);
        (_flaecheBildMax, _netzMax, _bodenMax, _flaecheEinzelMax) = (0, 0, 0, 0);
        _profilBilder = 0;
    }

    /// <summary>
    /// Startoption <c>--passantenspur=alt</c>: die frühere Linie in fünf Spuren statt der Spuren aus <see cref="Passantenspur"/>; <c>=v1</c>: die Spuren
    /// wie vor Schicht 4 (feste 2-m-Gasse, kein Ausweichen, alle gehen pünktlich los, Anschluss über den nächsten Knoten) – beides zum Vergleichen.
    /// </summary>
    private static readonly bool SpurAlt = Wasserarten.Startoption("passantenspur") is "alt";
    private static readonly bool SpurV1 = SpurAlt || Wasserarten.Startoption("passantenspur") is "v1";

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
            var warSichtbar = p.Knoten.Visible;
            // Markt v3: eine Stelle nur belegen, wer noch nicht zu sehen war (nach dem Laden); wer schon am Platz steht (Markt war voll), geht nach seiner
            // Pause zu einer frei gewordenen Stelle hin, statt dorthin zu springen
            if (stelle < 0 && losgegangen && (MarktV2 || !warSichtbar)) stelle = p.Marktstelle = MarktstelleBelegen(e.Nummer, weg?.Ankunft ?? 0, MarktFuer(e));
            var angekommen = p.Stand != stand;
            // Markt v3: nach der Pause zum nächsten Ziel (vor dem Losgehen des Kerns nicht – dann steht sie, bis sie den Markt verlässt)
            if (!MarktV2 && losgegangen && Bummeln(p, e, stelle, angekommen, minuteGenau)) return;
            // Jedes Bild nur, wer schlendert (v2), sich noch zu seiner Stelle dreht oder (v3, im Bild) redet, sich umschaut oder schöpft
            if (!angekommen && !AufDemRundgang(stelle) && !p.Drehen
                && (MarktV2 || stelle < 0 || _marktstellen[stelle].Art is not (Marktbummel.Art.Gruppe or Marktbummel.Art.Schauen or Marktbummel.Art.Brunnen or Marktbummel.Art.Rand) || !ImBild(p.Knoten.Position))) return;
            p.Stand = stand;
            _imDom.Remove(e.Nummer);
            p.Knoten.Visible = true;
            // v3: Wer im Bild ankommt, dreht sich weich zu seiner Stelle (nur wer vorher nicht zu sehen war, steht gleich richtig)
            if (angekommen) p.Drehen = !MarktV2 && warSichtbar;
            AmMarktStellen(p, e, ort, stelle, minuteGenau, angekommen);
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
    private void AmMarktStellen(Passant p, Einwohner e, Aufenthalt markt, int stelle, double minuteGenau, bool angekommen = true) // angekommen: erstes Bild an der Stelle
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
        else if (!MarktV2) ort = AufFuss(ort); // v3: stehend auf dem Boden wie beim Gehen (kein Höhensprung bei Ankunft und Abgang)
        p.Knoten.Position = ort;
        if (!MarktV2 && s.Art is Marktbummel.Art.Gruppe or Marktbummel.Art.Schauen or Marktbummel.Art.Brunnen or Marktbummel.Art.Rand)
        {
            MarktStehen(p, figur, s, blick, minuteGenau); // reden nur zu zweit, sonst umschauen; am Brunnen schöpfen (Stadtansicht.Marktbummel.cs)
            return;
        }
        p.Knoten.Rotation = new Vector3(0, p.Drehen ? WeichDrehen(p, blick) : blick, 0);
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
    /// Wo sie auf dem Weg ist, sagt der Plan (<see cref="LaufPlanen"/>) minus dem Rückstand vom Ausweichen (Schicht 4).
    /// </summary>
    private void Laufen(Passant p, int nummer, double minuteGenau, double ticksJeSekunde)
    {
        p.Stand = null;
        _imDom.Remove(nummer);
        if (p.Pfad.Length == 0) { p.Knoten.Visible = false; return; }
        var mitte = SpurAlt ? null : MitteVon(p);
        var strecke = StreckeJetzt(p, minuteGenau); // Meter Mittellinie
        var laenge = mitte?.Laenge ?? p.Laenge;
        p.AmRand = Mathf.Min(strecke, laenge - strecke);
        var anteil = laenge > 0 ? strecke / laenge : 1;
        var hier = Gassennetz.PunktAuf(p.Pfad, anteil, p.Laenge);
        var fenster = 0.5f / Mathf.Max(laenge, 1); // ein halber Meter vor und zurück: Ecken werden über etwa einen Meter gerundet
        var weiter = Gassennetz.PunktAuf(p.Pfad, Mathf.Min(1, anteil + fenster), p.Laenge);
        var richtung = weiter - hier;
        // Meter je echter Sekunde (mit Spieltempo) auf dem Boden, wie geplant, und wirklich (gebremst)
        var tempo = (float)((SpurV1 ? laenge : mitte!.Bodenlaenge) * ticksJeSekunde / Math.Max(p.LaufEnde - p.LaufStart, 1e-6));
        var wirklich = tempo * p.Bremse;
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
            var vorwaerts = richtung;
            if (SpurAlt) // zum Vergleichen: die frühere gerade Linie in fünf Spuren
            {
                rechts = new Vector3(-richtung.Z, 0, richtung.X);
                versatz = rechts * (0.55f + Mathf.Abs(p.Versatz) * 0.5f) + rechts * p.Ausweichen;
            }
            else
            {
                // Wohin die Figur wirklich geht (auch an Ecken und im Übergang), brauchen nur die, die man sieht
                var gang = Passantenspur.Gehen(mitte!, p.Nummer, laenge, strecke, (p.VersatzStart.X, p.VersatzStart.Z), ImBild(hier));
                hier = new Vector3(gang.Ort.X - gang.Versatz.X, hier.Y, gang.Ort.Z - gang.Versatz.Z); // die gerundete Mittellinie
                (rechts, versatz) = (new Vector3(gang.Rechts.X, 0, gang.Rechts.Z), new Vector3(gang.Versatz.X, 0, gang.Versatz.Z));
                var (fx, fz) = gang.Bewegung;
                var laengeBewegung = Mathf.Sqrt(fx * fx + fz * fz);
                // Die Richtung, in die sie auf dem Boden geht; ihr Tempo ist schon das auf dem Boden (Bodenmeter), der Takt braucht keinen Ausgleich.
                // Fast keine Bewegung auf diesem Stück Mittellinie (innen an einer Ecke): dann gilt die Richtung des Weges
                if (laengeBewegung >= (SpurV1 ? 0.3f : 0.02f)) vorwaerts = new Vector3(fx, 0, fz) / laengeBewegung;
                if (SpurV1 && laengeBewegung >= 0.3f) bodenfaktor = Mathf.Min(laengeBewegung, 2f); // bis Schicht 3: Tempo nach der Mittellinie, der Takt glich aus
                // Der Raum für Karrenausweichen und Schicht 4: die Mitte der Figur bleibt mindestens den Wandabstand vom Rand des Weges (Breite aus der
                // Karte); an Anfang und Ende des Weges (Tür, Platz) schrumpft er auf null, dort geht sie genau auf ihren Punkt
                var spurQuer = versatz.Dot(rechts);
                var spielraum = Mathf.Max(gang.Halb - Passantenspur.Wandabstand, 0);
                var enden = Mathf.Min(Passantenspur.Rampe(strecke, 1.5f), Passantenspur.Rampe(laenge - strecke, 1.5f));
                var (links, rechtsRaum) = (Mathf.Min(0, (-spielraum - spurQuer) * enden), Mathf.Max(0, (spielraum - spurQuer) * enden));
                // Auf dem Pflaster eines Marktplatzes (v3, Stadtansicht.Marktbummel.cs): Die ganze seitliche Lage – Spur, Schwanken, Ausweichen – bleibt so weit von
                // Ständen, Brunnen, Bänken und Kreuz, dass der Körper frei ist (die Grenzen gemessen von der Mittellinie aus, sie ändern sich höchstens 1,5 m je
                // Sekunde, damit niemand seitlich springt). Die Enden schrumpfen dort nur auf den letzten 0,6 m (um die Stelle herum ist Raum).
                if (!MarktV2)
                {
                    // auch 1 und 2 m voraus (auf der Mittellinie): Rückt ein Stand näher, rückt sie rechtzeitig ein, ohne zu springen
                    var flaeche = FlaechenRaum(p, hier, rechts);
                    foreach (var voraus in new[] { 1f, 2f })
                    {
                        if (strecke + voraus > laenge) break;
                        var (vx, vz) = mitte!.Bei(strecke + voraus);
                        if (FlaechenRaum(p, new Vector3(vx, hier.Y, vz), rechts) is not { } spaeter) continue;
                        flaeche = flaeche is { } jetzt ? (Mathf.Max(jetzt.Links, spaeter.Links), Mathf.Min(jetzt.Rechts, spaeter.Rechts), jetzt.Auf) : (spaeter.Links, spaeter.Rechts, false);
                    }
                    var (erlaubtL, erlaubtR) = flaeche is { } fl ? (fl.Links, fl.Rechts) : (-3f, 3f);
                    // gerade an einem Ort losgegangen (Spur noch null): gleich die wahre Grenze, sonst höchstens 1,5 m je Sekunde
                    var schritt = p.GrenzeLinks <= -3f && p.GrenzeRechts >= 3f && strecke < 0.5f ? 10f : 1.5f * (float)_bildSekunden;
                    (p.GrenzeLinks, p.GrenzeRechts) = (Mathf.Clamp(erlaubtL, p.GrenzeLinks - schritt, p.GrenzeLinks + schritt), Mathf.Clamp(erlaubtR, p.GrenzeRechts - schritt, p.GrenzeRechts + schritt));
                    if (p.GrenzeLinks < -2.9f && p.GrenzeRechts > 2.9f) { } // nicht auf dem Platz: die Wegbreite gilt
                    else
                    {
                        var begrenzt = Mathf.Clamp(spurQuer, Mathf.Min(p.GrenzeLinks, 0), Mathf.Max(p.GrenzeRechts, 0));
                        versatz += rechts * (begrenzt - spurQuer);
                        spurQuer = begrenzt;
                        var platzEnden = Mathf.Min(Passantenspur.Rampe(strecke, 0.6f), Passantenspur.Rampe(laenge - strecke, 0.6f));
                        if (flaeche is { Auf: true })
                            (links, rechtsRaum) = (Mathf.Min(0, (p.GrenzeLinks - spurQuer) * platzEnden), Mathf.Max(0, (p.GrenzeRechts - spurQuer) * platzEnden));
                        else (links, rechtsRaum) = (Mathf.Max(links, Mathf.Min(0, p.GrenzeLinks - spurQuer)), Mathf.Min(rechtsRaum, Mathf.Max(0, p.GrenzeRechts - spurQuer)));
                    }
                }
                var karren = Mathf.Clamp(p.Ausweichen, links, rechtsRaum);
                (p.RaumLinks, p.RaumRechts) = (links - karren, rechtsRaum - karren);
                versatz += rechts * Mathf.Clamp(karren + p.Seitlich, links, rechtsRaum);
                // beim Ausweichen dreht sie sich ein wenig zur Seite, wohin sie tritt
                if (wirklich > 0.05f && Mathf.Abs(p.SeitRate) > 0.01f)
                {
                    var schraeg = vorwaerts * wirklich + rechts * p.SeitRate;
                    bodenfaktor *= schraeg.Length() / wirklich;
                    vorwaerts = schraeg.Normalized();
                }
                if (vorwaerts.LengthSquared() > 1e-6f) drehung = Mathf.Atan2(vorwaerts.X, vorwaerts.Z);
            }
            // v3: beim Losgehen von einer Stelle auf dem Markt dreht sie sich in die Gehrichtung, statt herumzuspringen
            if (p.ImBummel && strecke < 1.2f && p.Knoten.Visible) drehung = Mathf.LerpAngle(p.Knoten.Rotation.Y, drehung, (float)Math.Min(1, _bildSekunden * 6));
            p.Knoten.Rotation = new Vector3(0, drehung, 0);
            p.VersatzJetzt = versatz;
            p.RechtsJetzt = rechts;
            p.Geschwindigkeit = vorwaerts * wirklich;
            hier += p.VersatzJetzt;
        }
        else (p.VersatzJetzt, p.Geschwindigkeit) = (Vector3.Zero, Vector3.Zero);
        p.Knoten.Position = AufFuss(hier); // auf Gelände auf dem Boden (Stadtansicht.Gelaende.cs)
        p.Knoten.Visible = true;
        // An der Tür (Schicht 4): Wer drinnen losgeht und dort warten muss, weil gerade jemand aus derselben Tür tritt, bleibt so lange drinnen; wer an
        // der eigenen Tür ankommt und dort aufgehalten wird, ist schon drin – so drängt sich niemand im Türrahmen
        p.WartetDrinnen = false;
        if (!SpurV1 && !p.ImBummel && p.FuerWeg is { } weg && p.Bremse < 0.3f)
        {
            if (strecke < 0.3f && p.VersatzStart == Vector3.Zero && !Draussen(weg.Von)) (p.Knoten.Visible, p.WartetDrinnen) = (false, true);
            else if (strecke > laenge - 0.5f && !Draussen(weg.Nach)) (p.Rueckstand, p.LaufEnde) = (0, Math.Min(p.LaufEnde, minuteGenau));
        }
        if (p.Figur is not { } figur) return;
        var bein = figur.Beinfaktor;
        var (bewegung, grund) = p.Gebeugt ? ("Gebeugt", GebeugtTempo)
                              : tempo > EilenAb * bein ? ("Eilen", EilenTempo)
                              : p.Traegt ? ("Tragen", TragenTempo)
                              : ("Gehen", GehenTempo);
        // Fast angehalten (hinter jemandem, der nicht Platz macht): stehen statt auf der Stelle zu treten
        if (p.Bremse < 0.2f && !SpurV1) BewegungSetzen(figur, "Stehen");
        else BewegungSetzen(figur, bewegung, wirklich * bodenfaktor / (grund * bein));
    }

    /// <summary>
    /// Wie weit (Meter Mittellinie) die Figur auf ihrem Weg ist: nach dem Plan auf dem Boden (gleichmäßig, auch um Ecken), weniger dem Rückstand vom
    /// Ausweichen, umgerechnet über ihre Bodenmeter (<see cref="Passantenspur.StreckeZuBoden"/>). Mit <c>--passantenspur=alt</c> die Meter des Pfades.
    /// </summary>
    private float StreckeJetzt(Passant p, double minuteGenau)
    {
        var anteil = (float)Math.Clamp((minuteGenau - p.LaufStart) / Math.Max(p.LaufEnde - p.LaufStart, 1e-9), 0, 1);
        if (SpurAlt) return Mathf.Clamp(anteil * p.Laenge - p.Rueckstand, 0, p.Laenge);
        var mitte = MitteVon(p);
        if (SpurV1) return Mathf.Clamp(anteil * mitte.Laenge, 0, mitte.Laenge);
        var boden = Mathf.Clamp(anteil * mitte.Bodenlaenge - p.Rueckstand, 0, mitte.Bodenlaenge);
        return Passantenspur.StreckeZuBoden(mitte.Boden, mitte.Laenge, boden, mitte.BodenSchritt);
    }

    /// <summary>Die Mittellinie des Pfades samt Bodenmetern, fertig gerechnet (neu, sobald der Pfad wechselt; eine angefangene Tabelle wird hier zu Ende gerechnet).</summary>
    private PfadMitte MitteVon(Passant p)
    {
        var mitte = MitteAnlegen(p);
        BodenFuellen(mitte, int.MaxValue);
        return mitte;
    }

    /// <summary>
    /// Die Mittellinie anlegen, die Tabelle der Bodenmeter aber noch nicht füllen (das geschieht über die Bilder verteilt, <see cref="BodenmeterFuellen"/>,
    /// oder spätestens beim ersten Schritt): Eine Tabelle für einen Weg von 400 m kostete sonst zur vollen Stunde ein ganzes Bild.
    /// </summary>
    private PfadMitte MitteAnlegen(Passant p)
    {
        if (p.Mitte is { } mitte && ReferenceEquals(mitte.Pfad, p.Pfad)) return mitte;
        mitte = PfadMitteBauen(p.Pfad);
        if (!SpurV1)
        {
            mitte.BodenSchritt = Passantenspur.BodenschrittFuer(mitte.Laenge); // lange Wege gröber
            mitte.Boden = new float[Passantenspur.BodenmeterAnzahl(mitte.Laenge, mitte.BodenSchritt) + 1];
            (mitte.Start, mitte.Nummer, mitte.BodenBis) = ((p.VersatzStart.X, p.VersatzStart.Z), p.Nummer, 0);
            _bodenOffen.Add(p);
        }
        return p.Mitte = mitte;
    }

    /// <summary>Rechnet höchstens <paramref name="schritte"/> weitere Einträge der Tabelle; true, wenn sie danach fertig ist.</summary>
    private static bool BodenFuellen(PfadMitte mitte, int schritte)
    {
        if (mitte.Fertig) return true;
        var bis = (int)Math.Min((long)mitte.BodenBis + schritte, mitte.Boden.Length - 1);
        Passantenspur.BodenmeterFuellen(mitte.Boden, mitte.BodenBis + 1, bis, mitte, mitte.Nummer, mitte.Laenge, mitte.Start, mitte.BodenSchritt);
        mitte.BodenBis = bis;
        return mitte.Fertig;
    }

    /// <summary>Figuren, deren Tabelle der Bodenmeter noch nicht fertig ist.</summary>
    private readonly List<Passant> _bodenOffen = new();

    /// <summary>So viele Einträge (ein Aufruf von Gehen je Eintrag) rechnet ein Stück der verteilten Tabelle.</summary>
    private const int BodenStueck = 150;

    /// <summary>
    /// Jedes Bild: Tabellen der Bodenmeter stückweise weiterrechnen, solange in diesem Bild Zeit für Wegsuchen ist (<see cref="WegsucheFrei"/>), wer zuerst
    /// losgeht, zuerst.
    /// </summary>
    private void BodenmeterFuellen()
    {
        if (_bodenOffen.Count == 0) return;
        _bodenOffen.RemoveAll(q => q.Mitte is not { } m || m.Fertig || !ReferenceEquals(m.Pfad, q.Pfad));
        if (_bodenOffen.Count > 1) _bodenOffen.Sort((a, b) => a.LaufStart.CompareTo(b.LaufStart));
        foreach (var q in _bodenOffen)
        {
            if (!WegsucheFrei()) break;
            var uhr = Stopwatch.StartNew();
            BodenFuellen(q.Mitte!, BodenStueck);
            WegsucheZaehlen(uhr.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Plant den Gang einer Figur mit ihrem eigenen, festen Tempo (<see cref="Gehtempo.Planen"/>): pünktlich, wenn die Zeit reicht, sonst früher
    /// los und höchstens 1,3-mal so schnell, und wenn auch das nicht reicht, kommt sie später an, als der Kern sie zählt.
    /// Die Geschwindigkeit gilt in echten Sekunden bei Tempo 1; bei höherem Spieltempo laufen alle entsprechend schneller.
    /// Losgehen gestreut (Nutzer, 1.11.2026): Jede Person geht um ihren festen Versatz (0 bis 45 Spielminuten, <see cref="Passantenspur.Losgehversatz"/>)
    /// später los als der Kern, damit nicht alle zur vollen Stunde zugleich auf die Gasse kommen – auch zur Messe (sonst drängten sich alle um 7 Uhr
    /// an der Domtür).
    /// </summary>
    private void LaufPlanen(Passant p, Weg weg)
    {
                p.Verzoegerung = SpurV1 ? 0 : Passantenspur.Losgehversatz(p.Nummer);
        (p.LaufStart, p.LaufEnde) = Gehtempo.Planen(p.Laenge, Gehtempo.MeterJeSpielminute(p.Gehtempo, _simulation!.Takt.RundenDauerSekunden), weg.Abmarsch + p.Verzoegerung, weg.Ankunft + p.Verzoegerung);
    }

    /// <summary>
    /// Wie weit (Meter) die Figur auf ihrem alten Weg ist, wenn sie gerade im Bild unterwegs ist und noch nicht ankommt – sonst null. Nur dann muss ein
    /// neuer Weg an sie anschließen (<see cref="Weitergehen"/>).
    /// </summary>
    private float? MitteDesWegesWennImBild(Passant p, double minuteGenau)
    {
        if (p.Stand is not null || !p.Knoten.Visible || p.Pfad.Length == 0 || minuteGenau < p.LaufStart) return null;
        if (minuteGenau >= p.LaufEnde && p.Rueckstand <= 0.01f) return null;
        return StreckeJetzt(p, minuteGenau);
    }

    /// <summary>
    /// Läge die Figur mit dem eben geplanten neuen Weg sichtbar im Bild – an seinem Anfang (dort wartet sie bis zum Losgehen) oder an der Stelle, an
    /// der sie jetzt auf ihm wäre (ist der Abmarsch schon vorbei)? Dann darf der Weg nicht hart beginnen, sie tauchte dort auf.
    /// </summary>
    private bool NeuerWegImBild(Passant p, double minuteGenau)
    {
        if (p.Pfad.Length == 0) return false;
        // auf dem Gelände, wo die Figur gezeichnet würde (die Punkte des Pfads liegen auf Höhe 0 – am Hochufer sonst am Bildrand falsch)
        if (ImBild(AufFuss(p.Pfad[0]))) return true;
        var anteil = p.LaufEnde > p.LaufStart ? (float)Math.Clamp((minuteGenau - p.LaufStart) / (p.LaufEnde - p.LaufStart), 0, 1) : 1f;
        return ImBild(AufFuss(Gassennetz.PunktAuf(p.Pfad, anteil)));
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
        var abmarsch = AbmarschZeit(weg, e);
        if (weg.Von.Ort == Stadtort.Gasse && weg.Nach.Ort == Stadtort.Gasse
            && GassenLage(e, abmarsch) is { } a && GassenLage(e, weg.Ankunft) is { } b
            && (a.Von.IsEqualApprox(b.Von) && a.Bis.IsEqualApprox(b.Bis) || !SpurV1 && _netz.KanteBei(a.Punkt) is { } ka && ka == _netz.KanteBei(b.Punkt)))
            return new List<Vector3> { a.Punkt, b.Punkt }; // dasselbe Stück: das Netz wird nicht gebraucht

        var anfaenge = Anlaeufe(weg.Von, e, abmarsch);
        var enden = Anlaeufe(weg.Nach, e, weg.Ankunft);
        if (!SpurV1) return _netz.PfadZwischen(anfaenge, enden)?.Pfad.ToList() ?? new List<Vector3>(); // alle Kombinationen in einem Durchgang
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
    /// Platz in der Gasse die beiden Enden des Wegstücks, auf dem man steht – des Stücks, wie das Netz jetzt ist (<see cref="Gassennetz.KanteBei"/>):
    /// Die gemerkten Gassen sind oft später an einer Einmündung geteilt worden, und über ihr fernes Ende ginge die Figur am Ziel erst vorbei und dann zurück.
    /// </summary>
    private List<(int Knoten, float Reststueck)> Anlaeufe(Aufenthalt a, Einwohner e, long zeitpunkt)
    {
        if (a.Ort != Stadtort.Gasse || GassenLage(e, zeitpunkt) is not { } lage) return new() { (Anlaufknoten(a, e), 0f) };
        if (!SpurV1 && _netz.KanteBei(lage.Punkt) is { } kante)
            return new() { (kante.A, lage.Punkt.DistanceTo(_netz.Knoten[kante.A])), (kante.B, lage.Punkt.DistanceTo(_netz.Knoten[kante.B])) };
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
        if (Draussen(weg.Von)) pfad.Insert(0, StreckenEnde(weg.Von, e, p.AlteMarktstelle, AbmarschZeit(weg, e), abmarsch));
        else if (TuerVon(weg.Von) is { } ausgang) pfad.Insert(0, ausgang); // kommt aus der Tür des Modells (mit der Höhe der Stufen)
        if (Draussen(weg.Nach)) pfad.Add(StreckenEnde(weg.Nach, e, p.Marktstelle, weg.Ankunft, ankunft));
        else if (TuerVon(weg.Nach) is { } eingang) pfad.Add(eingang);      // verschwindet erst an der Tür
        if (!SpurV1) pfad = OhneUmkehr(pfad); // nirgends bis zu einem Knoten und auf derselben Linie zurück (Stadtansicht.Passantenwege.cs)
        // Markt v3: Der Weg über den Platz wird erst kurz vor dem Losgehen gesucht (PlatzNachplanen) – zur vollen Stunde bekommen sonst alle zugleich einen Weg
        p.PlatzOffen = !MarktV2 && BeruehrtPlatz(pfad);
        p.PfadBeginntAufPlatz = p.PlatzOffen && AufDemPflaster(pfad[0]);
        return p.PlatzOffen ? pfad.ToArray() : UeberDenPlatz(pfad, p.Nummer).ToArray();
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

    /// <summary>
    /// Beim Ziel gilt die Ankunftszeit des letzten Weges, sonst (vor dem Abmarsch) die Ankunft des Weges davor, wenn die Darstellung sie kennt
    /// (<see cref="AbmarschZeit"/>: so steht sie bis zum Losgehen dort, wo sie angekommen ist), sonst dessen Abmarsch.
    /// </summary>
    private long Verweilzeit(Aufenthalt a, Einwohner e) => e.Weg is { } w ? (a == w.Nach ? w.Ankunft : AbmarschZeit(w, e)) : 0;

    /// <summary>
    /// Der Zeitpunkt, nach dem der Platz am Anfang eines Weges gewählt wird (Gasse, Umland): die Ankunft des Weges davor, wenn die Figur dort angekommen
    /// war – sonst stünde sie bis zum Losgehen plötzlich an einem anderen Platz –, ohne sie (nach dem Laden, <c>--passantenspur=v1</c>) der Abmarsch.
    /// </summary>
    private long AbmarschZeit(Weg weg, Einwohner e) =>
        !SpurV1 && _passanten.TryGetValue(e.Nummer, out var p) && ReferenceEquals(p.VonZeitFuer, weg) && p.VonZeit is { } zeit ? zeit : weg.Abmarsch;

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
            if (platz?.Flaeche is { } f ? f.Abstand(punkt.X, punkt.Z) >= 0.6f // Markt v3: genaue Umrisse der Stände
                : !_marktHindernisse.Any(h => new Vector2(punkt.X - h.Mitte.X, punkt.Z - h.Mitte.Z).Length() < h.Radius + Wegrand)) return punkt;
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
            Radius = KoerperRadius(e.Volk),
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
