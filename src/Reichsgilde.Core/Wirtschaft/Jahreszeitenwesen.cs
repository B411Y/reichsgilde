using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Der Winter eines Jahres: wie kalt er ist und ob (und wann) der Fluss zufriert. Die Minuten zählen ab Tagesbeginn wie
/// <see cref="Spielzeit.MinuteImTag"/>; <see cref="TauwetterMinute"/> = <see cref="Spielzeit.MinutenProTag"/> heißt: zu bis zum Jahresende.
/// </summary>
/// <param name="Kaelte">Gewürfelt, 0 (mild) bis knapp 1 (bitterkalt).</param>
/// <param name="Streng">Ab 1 − <see cref="StrengerWinterDaten.Chance"/>: Der Fluss friert zu, die Nachfrage wächst noch einmal.</param>
public sealed record Winterlage(int Jahr, double Kaelte, bool Streng, int ZufrierenMinute, int TauwetterMinute)
{
    /// <summary>Ist der Fluss zu dieser Minute des Jahres zu?</summary>
    public bool ZuUm(int minuteImTag) => Streng && minuteImTag >= ZufrierenMinute && minuteImTag < TauwetterMinute;

    /// <summary>Bleibt der Fluss bis zum Jahresende zu (der ganze Rest des Winters)?</summary>
    public bool BisJahresende => Streng && TauwetterMinute >= Spielzeit.MinutenProTag;
}

/// <summary>
/// Die Folgen der Jahreszeit für die Wirtschaft (Abschnitt „jahreszeiten“, Datenversion 67; Nutzer 31.10.2026, Weg B). Jedes Spieljahr hat eine
/// Jahreszeit (<see cref="Spielzeit.Jahreszeit"/>), und sie gilt das ganze Jahr:
///   • <b>Winter:</b> mehr Nachfrage nach Brennholz, Kerzen und Schnaps; Arbeit im Freien (Holzschlag, Steinbruch, Höfe, Gärten) langsamer. In einem
///     <b>strengen Winter</b> (gewürfelt, <see cref="Winterlage"/>) wächst die Nachfrage noch einmal, und der Fluss friert für einige Stunden zu:
///     Der Hafen nimmt keine Aufträge an, alle Schiffe ruhen (ihre Uhr steht still, auch auf dem Fluss), die Fähre kostet die Minuten aus
///     <see cref="StrengerWinterDaten.FaehreMinuten"/>.
///   • <b>Herbst:</b> Ernte – die Bauern des Umlands bringen mehr Gerste, Roggen und Kräuter (Grundversorgung höher, Preis tiefer), und die eigenen
///     Höfe und Kräutergärten bringen mehr.
///   • <b>Frühling:</b> Schlamm – Trampelpfade und Fahrwege dauern länger (<see cref="Schlamm"/> im Zeitfaktor des Kerns, das Bild fährt mit demselben
///     Faktor), Kies und Pflaster nicht.
///   • <b>Festmärkte</b> zu festen Stunden einer Jahreszeit: mehr Nachfrage aller Waren, mehr Wirtshausgäste, mehr Besucher (für die Darstellung).
///
/// Nichts davon steht im Spielstand: Die Jahreszeit kommt aus der Uhr, der Winter aus dem Startwert der Partie und dem Jahr (eigene
/// <see cref="Zufallsquelle"/> je Winter, sie würfelt genau zweimal). Eine geladene Partie und jeder Mitspieler rechnen denselben Winter.
/// Die einzige Wirkung, die bleibt, sind die verschobenen Zeiten der Schiffe – und die stehen ohnehin im Spielstand.
/// Ohne den Abschnitt ist alles 1 bzw. aus.
/// </summary>
public sealed class Jahreszeitenwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly int _startwert;
    private Winterlage? _winter;

    /// <summary>Leistungsfaktor je Jahreszeit und Betriebsart (aus den Daten, einmal gerechnet).</summary>
    private readonly Dictionary<Betriebsart, double>[] _arbeit = new Dictionary<Betriebsart, double>[4];

    /// <summary>Was am Markt zuletzt gesetzt wurde: Jahreszeit, strenger Winter, Festmarkt (−1 = keiner). Null: noch nie.</summary>
    private (Jahreszeit, bool, int)? _marktStand;

    private WasserDaten? _eisWasser, _eisWasserVon;

    public JahreszeitenDaten? Daten { get; }

    /// <summary>Wirkt die Jahreszeit auf die Wirtschaft? (Abschnitt „jahreszeiten“)</summary>
    public bool IstAktiv => Daten is not null;

    internal Jahreszeitenwesen(Stadtwirtschaft w, JahreszeitenDaten? daten, int zufallsstartwert)
    {
        _w = w;
        Daten = daten;
        _startwert = zufallsstartwert;
        foreach (var j in Enum.GetValues<Jahreszeit>())
        {
            var d = new Dictionary<Betriebsart, double>();
            if (daten is not null)
                foreach (var art in w.Daten.Betriebsarten.Keys)
                    d[art] = daten.Von(j).ArbeitVon(art);
            _arbeit[(int)j] = d;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen (für Oberfläche, Darstellung, KI)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Jahreszeit jetzt (nach der Uhr der Wirtschaft).</summary>
    public Jahreszeit Jahreszeit => _w.Uhr.Jahreszeit;

    /// <summary>Der Winter dieses Jahres – null, wenn jetzt kein Winter ist oder die Jahreszeiten nicht wirken.</summary>
    public Winterlage? DiesesJahr => WinterVon(_w.Uhr.Jahr);

    /// <summary>
    /// Der Winter eines Jahres (null, wenn das Jahr kein Winter ist oder der Abschnitt fehlt). Fest aus Startwert und Jahr gewürfelt: zweimal
    /// aus einer eigenen <see cref="Zufallsquelle"/> (Kälte, Stunde des Zufrierens) – nichts davon muss gespeichert werden.
    /// </summary>
    public Winterlage? WinterVon(int jahr)
    {
        if (Daten is null || JahreszeitVon(jahr) != Jahreszeit.Winter) return null;
        if (_winter is { } bekannt && bekannt.Jahr == jahr) return bekannt;
        var z = new Zufallsquelle(Mischen(_startwert, jahr));
        var kaelte = z.NextDouble();
        var stundeWurf = z.Next(1000);
        var lage = new Winterlage(jahr, kaelte, false, Spielzeit.MinutenProTag, Spielzeit.MinutenProTag);
        if (Daten.StrengerWinter is { Chance: > 0 } s && kaelte >= 1 - s.Chance)
        {
            var stunde = s.ZufrierenVon + stundeWurf % (s.ZufrierenBis - s.ZufrierenVon + 1);
            var anteil = Math.Clamp((kaelte - (1 - s.Chance)) / s.Chance, 0, 1); // 0 im mildesten strengen Winter, knapp 1 im kältesten
            var dauer = s.DauerMin + (int)Math.Round(anteil * (s.DauerMax - s.DauerMin), MidpointRounding.AwayFromZero);
            var zu = stunde * 60 - Spielzeit.TagesbeginnMinute;
            lage = lage with { Streng = true, ZufrierenMinute = zu, TauwetterMinute = Math.Min(Spielzeit.MinutenProTag, zu + dauer * 60) };
        }
        return _winter = lage;
    }

    /// <summary>Ist jetzt ein strenger Winter?</summary>
    public bool IstStrengerWinter => DiesesJahr is { Streng: true };

    /// <summary>Ist der Fluss jetzt zugefroren? Dann sind Hafen und Fähre geschlossen, die Schiffe ruhen. Für die Darstellung: Eis auf dem Fluss.</summary>
    public bool FlussZugefroren => ZugefrorenUm(_w.Uhr);

    /// <summary>Ist der Fluss zu dieser Zeit zugefroren?</summary>
    public bool ZugefrorenUm(Spielzeit zeit) => HatFluss && WinterVon(zeit.Jahr) is { } l && l.ZuUm(zeit.MinuteImTag);

    /// <summary>Strenger Winter, aber der Fluss ist noch offen und wird heute noch zufrieren („Eis treibt“) – Schiffe sollten nicht mehr auslaufen.</summary>
    public bool EisDroht => HatFluss && DiesesJahr is { Streng: true } l && _w.Uhr.MinuteImTag < l.ZufrierenMinute;

    /// <summary>Hat die Karte einen Fluss, der zufrieren kann (Gewässer der Art „fluss“)?</summary>
    public bool HatFluss => Flussname is not null;

    /// <summary>Name des ersten Flusses der Karte (am Hafen, sonst der erste) – null ohne Fluss oder ohne Karte.</summary>
    public string? Flussname =>
        _w.Schiffahrt.Haefen.Count > 0 ? _w.Schiffahrt.Haefen[0].Fluss
        : _w.Karte?.Wasser.FirstOrDefault(g => g.Art == "fluss") is { } g ? (g.Name != "" ? g.Name : "Fluss") : null;

    /// <summary>Der Festmarkt, der gerade läuft (null: keiner).</summary>
    public FestmarktDaten? FestmarktJetzt => FestmarktUm(Jahreszeit, _w.Uhr.UhrzeitInMinuten / 60);

    /// <summary>Die Festmärkte dieses Jahres (nach der Jahreszeit, in der Reihenfolge der Daten).</summary>
    public IEnumerable<FestmarktDaten> FestmaerkteDiesesJahr => Daten?.Festmaerkte.Where(m => m.Jahreszeit == Jahreszeit) ?? Enumerable.Empty<FestmarktDaten>();

    /// <summary>
    /// Wie viele Besucher mehr als sonst in der Stadt sind (1 = wie immer; während eines Festmarkts 1 + <see cref="FestmarktDaten.Besucher"/>).
    /// Nur für die Darstellung (Passanten, Stände) – der Kern rechnet nichts damit.
    /// </summary>
    public double Besucherfaktor => 1 + (FestmarktJetzt?.Besucher ?? 0);

    /// <summary>Faktor auf die Leistung dieses Betriebs in dieser Jahreszeit (Arbeit im Freien; 1 ohne Abschnitt).</summary>
    public double Arbeitsfaktor(Betrieb betrieb) => Arbeitsfaktor(betrieb.Art, Jahreszeit);

    public double Arbeitsfaktor(Betriebsart art, Jahreszeit jahreszeit) => _arbeit[(int)jahreszeit].GetValueOrDefault(art, 1);

    /// <summary>Faktor auf die Nachfrage der Stadt nach dieser Ware jetzt: Jahreszeit (im strengen Winter verstärkt) mal Festmarkt.</summary>
    public double Nachfragefaktor(string ware) => NachfrageFaktor(ware, Jahreszeit, IstStrengerWinter, FestmarktJetzt);

    /// <summary>Faktor auf die Grundversorgung dieser Ware jetzt (Herbst: Ernte).</summary>
    public double Angebotsfaktor(string ware) => Daten?.Von(Jahreszeit).AngebotVon(ware) ?? 1;

    /// <summary>
    /// Nachfragefaktor einer Ware in dieser Lage: der Faktor der Jahreszeit f, im strengen Winter 1 + (f − 1) × (1 + Zuschlag), mal 1 + Zuschlag
    /// des Festmarkts.
    /// </summary>
    public double NachfrageFaktor(string ware, Jahreszeit jahreszeit, bool streng, FestmarktDaten? festmarkt)
    {
        if (Daten is null) return 1;
        var f = Daten.Von(jahreszeit).NachfrageVon(ware);
        if (streng && jahreszeit == Jahreszeit.Winter && Daten.StrengerWinter is { } s) f = 1 + (f - 1) * (1 + s.NachfrageZuschlag);
        return f * (1 + (festmarkt?.Nachfrage ?? 0));
    }

    /// <summary>Faktor des Schlamms auf ein Stück einer Fahrt (1 außer auf unbefestigten Wegen der Karte in einer Jahreszeit mit Schlamm).</summary>
    public double Schlamm(Fahrtstueck s)
    {
        if (Daten is null || s.Art != Fahrbahn.Strasse || s.Weg < 0) return 1;
        var d = Daten.Von(Jahreszeit);
        if (d.Schlamm.Count == 0) return 1;
        return d.SchlammVon(_w.Wegebau.BelagBei(s.Weg, s.Stelle));
    }

    /// <summary>Faktor des Schlamms auf diesen Belag jetzt (für Anzeige und Darstellung; 1 auf Kies und Pflaster).</summary>
    public double Schlamm(Belagstufe stufe) => Daten?.Von(Jahreszeit).SchlammVon(stufe) ?? 1;

    /// <summary>Faktor auf die feste Fahrzeit aus dem Umland (Landwege, die nicht auf der Karte liegen).</summary>
    public double SchlammUmland => Daten?.Von(Jahreszeit).SchlammUmland ?? 1;

    /// <summary>Gäste der Wirtshäuser in der Stunde, die zu dieser Zeit endete: 1 + Zuschlag des Festmarkts dieser Stunde.</summary>
    public double Gaestefaktor(Spielzeit zeit)
    {
        if (Daten is null || Daten.Festmaerkte.Count == 0) return 1;
        // In der ersten Minute eines Jahres endete die letzte Stunde des Vorjahres – mit dessen Jahreszeit.
        var jahreszeit = zeit.MinuteImTag == 0 ? JahreszeitVon(zeit.Jahr - 1) : zeit.Jahreszeit;
        return 1 + (FestmarktUm(jahreszeit, zeit.StundeDieEndete)?.Gaeste ?? 0);
    }

    /// <summary>Die Werte für Furten und Fähren, solange der Fluss zu ist: die Fähre dauert <see cref="StrengerWinterDaten.FaehreMinuten"/>.</summary>
    internal WasserDaten? MitEis(WasserDaten? wasser)
    {
        if (wasser is null || Daten?.StrengerWinter is not { } s || !FlussZugefroren) return wasser;
        if (!ReferenceEquals(_eisWasserVon, wasser))
        {
            _eisWasserVon = wasser;
            _eisWasser = new WasserDaten { FurtMinuten = wasser.FurtMinuten, FaehreMinuten = s.FaehreMinuten };
        }
        return _eisWasser;
    }

    private FestmarktDaten? FestmarktUm(Jahreszeit jahreszeit, int stunde) =>
        Daten?.Festmaerkte.FirstOrDefault(m => m.Jahreszeit == jahreszeit && stunde >= m.Von && stunde < m.Bis);

    /// <summary>
    /// Startwert des Winters aus Startwert der Partie und Jahr, gut gemischt (ganzzahlig, auf jedem Rechner gleich). Mit einem schlicht
    /// linearen Startwert (Startwert × a + Jahr × b) kamen strenge Winter beim .NET-Generator in festem Takt – alle drei Winter.
    /// </summary>
    internal static int Mischen(int startwert, int jahr)
    {
        unchecked
        {
            var h = (uint)startwert * 0x9E3779B1u ^ (uint)jahr * 0x85EBCA77u ^ 0x5EA5_0A11u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (int)(h & 0x7FFF_FFFF);
        }
    }

    private static Jahreszeit JahreszeitVon(int jahr) => (Jahreszeit)(((jahr - Spielzeit.StartJahr) % 4 + 4) % 4);

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zu Beginn jeder Minute und nach dem Laden (aus <see cref="Stadtwirtschaft.EinwohnerZaehlen"/>): die Faktoren der Jahreszeit und des
    /// Festmarkts an jedem Marktstand – nur wenn sich die Lage geändert hat.
    /// </summary>
    internal void MarktSetzen()
    {
        if (Daten is null) return;
        var fest = FestmarktJetzt;
        var stand = (Jahreszeit, IstStrengerWinter, fest is null ? -1 : Daten.Festmaerkte.IndexOf(fest));
        if (_marktStand == stand) return;
        _marktStand = stand;
        foreach (var s in _w.Markt.Staende)
        {
            s.Jahreszeitfaktor = NachfrageFaktor(s.Ware, stand.Item1, stand.Item2, fest);
            s.Angebotsfaktor = Daten.Von(stand.Item1).AngebotVon(s.Ware);
        }
    }

    /// <summary>
    /// Der Jahreswechsel (die Uhr steht schon auf dem neuen Jahr): Der Schlamm hängt an der Jahreszeit, also rechnet der Kern die Fahrzeiten neu,
    /// und die Darstellung bekommt Bescheid (dasselbe Ereignis wie für einen geänderten Belag).
    /// </summary>
    internal void Jahreswechsel()
    {
        if (Daten is null) return;
        _w.Wegebau.ZeitfaktorGeaendert();
    }

    /// <summary>
    /// Jede Spielminute, vor Karren und Schiffen: Meldungen zum Beginn der Jahreszeit, der Festmärkte und des Eises; solange der Fluss zu ist, steht
    /// die Uhr jedes Schiffs, das nicht am Kai liegt bzw. lädt, still (Beginn und Ende rücken mit – so bleibt es im Bild, wo es ist).
    /// </summary>
    internal void Minute(Spielzeit zeit)
    {
        if (Daten is null) return;
        var m = zeit.MinuteImTag;
        if (m == 0) Melden(Jahresmeldung(zeit));
        foreach (var f in Daten.Festmaerkte.Where(f => f.Jahreszeit == zeit.Jahreszeit))
        {
            if (m == f.Von * 60 - Spielzeit.TagesbeginnMinute)
                Melden($"Festmarkt: {f.Name} bis {f.Bis} Uhr – Händler und Käufer kommen von weit her, die Nachfrage am Markt steigt" +
                       (f.Gaeste > 0 ? ", die Wirtshäuser füllen sich." : "."));
            else if (m == f.Bis * 60 - Spielzeit.TagesbeginnMinute)
                Melden($"Festmarkt: {f.Name} ist vorbei.");
        }
        if (!HatFluss || WinterVon(zeit.Jahr) is not { Streng: true } l) return;
        var fluss = Flussname!;
        if (m == l.ZufrierenMinute)
        {
            var unterwegs = _w.Schiffahrt.Schiffe.Count(s => s.Zustand != SchiffZustand.AmKai);
            Melden($"Der {fluss} ist zugefroren! Hafen und Fähre sind geschlossen" +
                   (unterwegs > 0 ? $", {unterwegs} {(unterwegs == 1 ? "Schiff liegt" : "Schiffe liegen")} im Eis fest" : "") +
                   (l.BisJahresende ? " – bis zum Ende des Winters." : " – bis das Eis bricht."));
            _w.Wegebau.ZeitfaktorGeaendert(); // die Fähre kostet nun die Wartezeit im Eis
        }
        else if (m == l.TauwetterMinute && !l.BisJahresende)
        {
            Melden($"Eisgang auf dem {fluss}: Der Fluss ist wieder offen, Hafen und Fähre arbeiten wieder.");
            _w.Wegebau.ZeitfaktorGeaendert();
        }
        if (!l.ZuUm(m)) return;
        foreach (var schiff in _w.Schiffahrt.Schiffe)
            if (schiff.Zustand != SchiffZustand.AmKai)
            {
                schiff.Beginn++;
                schiff.Ende++;
            }
    }

    private void Melden(string text) => _w.Melden(text);

    /// <summary>Was zu Beginn eines Jahres über die Jahreszeit gemeldet wird – nach den Daten.</summary>
    private string Jahresmeldung(Spielzeit zeit)
    {
        var j = zeit.Jahreszeit;
        var d = Daten!.Von(j);
        var teile = new List<string>();
        string Liste(IEnumerable<string> namen) => namen.ToList() switch
        {
            [] => "",
            [var a] => a,
            var l => string.Join(", ", l.Take(l.Count - 1)) + " und " + l[^1],
        };
        static IEnumerable<string> Nach(Dictionary<string, double> werte, bool mehr) =>
            werte.Where(x => mehr ? x.Value > 1 : x.Value < 1).OrderByDescending(x => Math.Abs(x.Value - 1)).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key);
        string Arten(bool mehr) => Liste(d.Arbeit.Where(x => mehr ? x.Value > 1 : x.Value < 1).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => Enum.TryParse<Betriebsart>(x.Key, out var a) && _w.Daten.Betriebsarten.TryGetValue(a, out var bd) ? bd.Name : x.Key));
        if (Liste(Nach(d.Nachfrage, true)) is { Length: > 0 } gefragt) teile.Add($"{gefragt} sind gefragt");
        if (Liste(Nach(d.Angebot, true)) is { Length: > 0 } ernte) teile.Add($"die Ernte bringt {ernte} auf den Markt");
        if (Arten(true) is { Length: > 0 } mehr) teile.Add($"{mehr} bringen mehr");
        if (Arten(false) is { Length: > 0 } weniger) teile.Add($"{weniger} arbeiten langsamer");
        if (d.Schlamm.Values.Any(x => x > 1) || d.SchlammUmland > 1)
            teile.Add("die Erdwege sind aufgeweicht, Karren fahren dort langsamer (Kies und Pflaster nicht)");
        foreach (var f in Daten.Festmaerkte.Where(f => f.Jahreszeit == j))
            teile.Add($"{f.Name} von {f.Von} bis {f.Bis} Uhr");
        var text = $"{KalenderTexte.Name(j)} {zeit.Jahr}" + (teile.Count > 0 ? ": " + string.Join("; ", teile) + "." : ".");
        if (WinterVon(zeit.Jahr) is { Streng: true } l)
            text += HatFluss
                ? $" Ein strenger Winter: Eis treibt auf dem {Flussname}, gegen {(l.ZufrierenMinute + Spielzeit.TagesbeginnMinute) / 60} Uhr friert er zu."
                : " Ein strenger Winter.";
        return text;
    }
}
