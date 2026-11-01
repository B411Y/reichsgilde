using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Wie schnell sich Figuren und Karren zeigen: feste Geschwindigkeiten in Metern je echter Sekunde bei Tempo 1 (Nutzer, 31.10.2026).
/// Die Figuren der Stadtansicht sind nur Darstellung: Der Kern sagt, <i>wann</i> jemand losgeht und ankommt, die Darstellung
/// geht in ihrem eigenen Tempo und kommt, wenn die Zeit knapp ist, eben später an. Bei höherem Spieltempo (2, 4, 8) laufen alle
/// entsprechend schneller. Karren fahren im festen Tempo ihrer Art (<see cref="Karrentempo"/>, <see cref="Fahrprofil"/>); der Kern rechnet ihre
/// Fahrzeit seit Datenversion 65 mit demselben Tempo nach Weglänge auf derselben Strecke (Stadtansicht.Karren.cs, Messzeile <c>[karren] Profil</c>
/// mit <c>--karrenmessung=1</c>).
/// Eine Regel für Figuren und Karren (1.11.2026): Meter je echter Sekunde bei der <b>eingestellten</b> Rundendauer
/// (<see cref="MeterJeSpielminute"/>) – so sieht man im Bild immer dieselben Geschwindigkeiten, wie lang man eine Runde auch einstellt. Der Kern
/// rechnet die Fahrzeit der Karren dagegen fest mit der Standard-Rundendauer (<see cref="Taktgeber.SekundenJeSpielminute"/>), damit die
/// Wirtschaft nicht an einer Menüeinstellung hängt. Bei 15 Minuten je Runde kommen Bild und Kern also gleichzeitig an; bei längeren Runden ist
/// der Karren früher am Markt und wartet, bei kürzeren später und kehrt sofort um (<see cref="Karrenzug"/>). Ohne Godot, damit ein Test die Datei
/// mitübersetzen kann (<c>GehtempoTests</c>).
/// </summary>
public static class Gehtempo
{
    /// <summary>Normales Gehen (Meter je Sekunde): ein Mensch auf der Straße.</summary>
    public const float Gehen = 1.3f;

    /// <summary>Alte, Bettler und Witwer gehen gebeugt und langsam.</summary>
    public const float Gebeugt = 1.0f;

    /// <summary>Boten haben es eilig.</summary>
    public const float Bote = 1.8f;

    /// <summary>Wer eine Last auf dem Rücken trägt (Kiepe, Bündel), geht um diesen Anteil langsamer.</summary>
    public const float TragenFaktor = 0.9f;

    /// <summary>Jede Person geht um bis zu so viel (Anteil) schneller oder langsamer als der Grundwert, fest nach der Nummer.</summary>
    public const float Streubreite = 0.1f;

    /// <summary>Wer auf dem Markt schlendert (Meter je Sekunde): gemächlicher als wer ein Ziel hat.</summary>
    public const float Schlendern = 0.9f;

    /// <summary>Die Nachtwache auf Streife: gemächlich, sie schauen sich um.</summary>
    public const float Streife = 1.1f;

    /// <summary>Wer unter Zeitdruck eilt, geht höchstens so viel mal schneller als sein eigenes Tempo.</summary>
    public const double EilenHoechstens = 1.3;

    /// <summary>
    /// Meter je Spielminute für ein Tempo von <paramref name="meterJeSekunde"/> Metern je echter Sekunde bei Tempo 1 und der eingestellten
    /// Rundendauer <paramref name="rundenDauerSekunden"/> – die Regel für alles, was sich im Bild bewegt (Figuren, Karren, Streife). Mit der
    /// Standard-Rundendauer (15 Minuten) genau <see cref="KarrenDaten.MeterJeMinute"/> des Kerns, Bit für Bit.
    /// </summary>
    public static double MeterJeSpielminute(double meterJeSekunde, double rundenDauerSekunden) =>
        meterJeSekunde * (rundenDauerSekunden / Spielzeit.MinutenProTag);

    /// <summary>Der Grundwert einer Person: Boten eilen, Bettler, Witwer und alle, die gebeugt gehen, sind langsam, wer trägt, ist etwas langsamer.</summary>
    public static float Grund(string rolle, bool gebeugt, bool traegt)
    {
        var grund = rolle == "Bote" ? Bote : gebeugt || rolle is "Bettler" or "Witwer" ? Gebeugt : Gehen;
        return traegt && rolle != "Bote" ? grund * TragenFaktor : grund;
    }

    /// <summary>Der Grundwert mit der Streuung der Person: <paramref name="zufall"/> (0 bis 1) wählt zwischen −10 % und +10 %.</summary>
    public static float Mit(float grund, float zufall) => grund * (1 - Streubreite + 2 * Streubreite * Math.Clamp(zufall, 0f, 1f));

    /// <summary>
    /// Plant den Gang einer Figur mit ihrem eigenen Tempo. <paramref name="meterJeMinute"/> ist ihr Tempo in Metern je Spielminute.
    ///   • Reicht die Zeit zwischen <paramref name="abmarsch"/> und <paramref name="ankunft"/> (Kern), geht sie so spät los, dass sie genau zur
    ///     Ankunft da ist.
    ///   • Reicht sie nicht, geht sie beim Abmarsch los und beeilt sich – höchstens <see cref="EilenHoechstens"/> mal so schnell.
    ///   • Reicht auch das nicht, kommt sie später an, als der Kern sie zählt (die Darstellung hat dafür den Rückstand: Stadtansicht.Passanten.cs).
    /// </summary>
    public static (double Start, double Ende) Planen(double laenge, double meterJeMinute, double abmarsch, double ankunft)
    {
        var gemuetlich = laenge / Math.Max(meterJeMinute, 1e-9); // Minuten im eigenen Tempo
        var verfuegbar = ankunft - abmarsch;
        return gemuetlich <= verfuegbar ? (ankunft - gemuetlich, ankunft) : (abmarsch, abmarsch + Math.Max(verfuegbar, gemuetlich / EilenHoechstens));
    }

    /// <summary>
    /// Eine Figur, die schon unterwegs ist und weitergeht (ihr neuer Weg schließt an die Stelle an, an der sie ist): Sie geht sofort los, ohne zu
    /// warten, und endet nach ihrer eigenen Gehzeit – es sei denn, der Kern erwartet sie früher, dann beeilt sie sich wie bei <see cref="Planen"/>.
    /// </summary>
    public static (double Start, double Ende) Weitergehen(double laenge, double meterJeMinute, double jetzt, double ankunft)
    {
        var gemuetlich = laenge / Math.Max(meterJeMinute, 1e-9);
        var verfuegbar = ankunft - jetzt;
        return (jetzt, jetzt + (gemuetlich <= verfuegbar ? gemuetlich : Math.Max(verfuegbar, gemuetlich / EilenHoechstens)));
    }
}

/// <summary>
/// Wie ihr Weg die Geschwindigkeit der Karren ändert. Das Tempo der Karrenart (Ochse 0,8, Handkarren 1,0, Pferd 1,3 m/s) und die Grenze für den
/// Ausbau (höchstens 15 % schneller) stehen seit Datenversion 65 im Kern (<see cref="KarrenDaten.Tempo"/>, <c>wegebau.ausbauHoechstens</c>,
/// <c>Stadtwirtschaft.Zeitfaktor</c>): Der Kern rechnet die Fahrzeit mit denselben Zahlen, nach denen der Karren hier fährt.
/// </summary>
public static class Karrentempo
{
    /// <summary>Wie viel mal so schnell ein Stück gefahren wird, das der Kern <paramref name="zeitfaktor"/> mal so lang rechnet (Steigung mal Belag).</summary>
    public static double Geschwindigkeitsfaktor(double zeitfaktor) => zeitfaktor <= 0 ? 1 : 1 / zeitfaktor;

    /// <summary>Ein Stück der Fahrt: sein Anteil an der Länge, sein Geschwindigkeitsfaktor, die Minuten, die der Karren davor wartet (Fähre), und die Minuten mehr (Furt).</summary>
    public readonly record struct Stueck(double Anteil, double Faktor, double WartenMinuten = 0, double ZusatzMinuten = 0);

    /// <summary>
    /// Die Stücke einer Fahrt aus den Stücken des Kerns (<see cref="Fahrtstueck"/>): ihr Zeitfaktor (Steigung mal Belag) wird zum Geschwindigkeitsfaktor,
    /// Furt und Fähre halten auf. <paramref name="zeitfaktor"/> liefert den Zeitfaktor des Stücks (Steigung und Belag, 1 = Bezug). Ohne Stücke: ein
    /// einziges ebenes.
    /// </summary>
    public static List<Stueck> Stuecke(IReadOnlyList<Fahrtstueck>? fahrt, Func<Fahrtstueck, double> zeitfaktor, double furtMinuten, double faehreMinuten)
    {
        var laenge = fahrt?.Sum(s => s.Laenge) ?? 0;
        if (fahrt is null || fahrt.Count == 0 || laenge <= 0) return new List<Stueck> { new(1, 1) };
        var liste = new List<Stueck>();
        var vorher = Fahrbahn.Strasse;
        foreach (var s in fahrt)
        {
            double wartet = 0, langsam = 0;
            if (s.Art != vorher)
            {
                if (s.Art == Fahrbahn.Faehre) wartet = faehreMinuten;
                else if (s.Art == Fahrbahn.Furt) langsam = furtMinuten;
            }
            vorher = s.Art;
            liste.Add(new Stueck(s.Laenge / laenge, Geschwindigkeitsfaktor(zeitfaktor(s)), wartet, langsam));
        }
        return liste;
    }
}

/// <summary>
/// Die Fahrt eines Karrens mit fester Geschwindigkeit, Stück für Stück: wann (Spielminuten seit der Abfahrt) er wie weit ist. Der Weg
/// wird als Anteil (0 bis 1) der Länge geführt. Die Stadtansicht fährt ohne <c>dauer</c>: Das Tempo bleibt fest (Ochse 0,8, Hand 1,0, Pferd 1,3 m/s).
/// Bei der Standard-Rundendauer kommt der Karren mit dem Kern an (bis auf dessen Rundung auf ganze Minuten) – außer an der Höchstdauer
/// (<c>logistik.weglaenge.hoechstMinuten</c>): Kappt der Kern eine lange Fahrt, fährt der Karren im Bild trotzdem in seinem Tempo weiter und kommt
/// später an; die Ware ist dann schon verkauft, er kehrt am Markt sofort um (<see cref="Karrenzug"/>). Mit <c>dauer</c> ließe sich das Profil auf
/// genau eine Zeit strecken oder stauchen – das Tempo wäre dann nicht mehr fest (nur für Tests).
/// </summary>
public sealed class Fahrprofil
{
    private readonly double[] _zeit, _weg;

    /// <summary>Wie lange die Fahrt dauert (Spielminuten) und wie lang der Weg ist (Meter).</summary>
    public double Dauer { get; }
    public double Laenge { get; }

    /// <param name="stuecke">Die Stücke der Fahrt (Anteile ergeben zusammen 1).</param>
    /// <param name="laenge">Die Länge des Weges in Metern.</param>
    /// <param name="meterJeMinute">Das Tempo auf dem Bezugsbelag in Metern je Spielminute.</param>
    /// <param name="dauer">Die Fahrzeit des Kerns (Spielminuten); null oder höchstens 0: die Zeit, die sich aus Länge und Tempo ergibt.</param>
    public Fahrprofil(IReadOnlyList<Karrentempo.Stueck> stuecke, double laenge, double meterJeMinute, double? dauer = null)
    {
        Laenge = laenge;
        var summe = stuecke.Sum(s => s.Anteil);
        var (zeit, weg) = (new List<double> { 0 }, new List<double> { 0 });
        foreach (var s in stuecke)
        {
            if (s.WartenMinuten > 0)
            {
                zeit.Add(zeit[^1] + s.WartenMinuten);
                weg.Add(weg[^1]);
            }
            var anteil = s.Anteil / summe;
            zeit.Add(zeit[^1] + anteil * laenge / Math.Max(meterJeMinute * s.Faktor, 1e-9) + s.ZusatzMinuten);
            weg.Add(weg[^1] + anteil);
        }
        if (dauer is > 0 && zeit[^1] > 0)
        {
            var mal = dauer.Value / zeit[^1];
            for (var i = 0; i < zeit.Count; i++) zeit[i] *= mal;
        }
        _zeit = zeit.ToArray();
        _weg = weg.ToArray();
        Dauer = _zeit[^1];
    }

    /// <summary>Wie weit (0 bis 1) ist der Karren nach dieser Zeit seit der Abfahrt?</summary>
    public double WegBei(double minute)
    {
        minute = Math.Clamp(minute, 0, Dauer);
        var i = Stueck(minute);
        var t = (minute - _zeit[i]) / Math.Max(1e-9, _zeit[i + 1] - _zeit[i]);
        return _weg[i] + t * (_weg[i + 1] - _weg[i]);
    }

    /// <summary>Nach welcher Zeit ist der Karren (zum ersten Mal) so weit? Die Umkehrung von <see cref="WegBei"/>.</summary>
    public double ZeitBei(double weg)
    {
        weg = Math.Clamp(weg, 0, 1);
        for (var i = 0; i + 1 < _weg.Length; i++)
            if (_weg[i + 1] >= weg && _weg[i + 1] > _weg[i])
                return _zeit[i] + (weg - _weg[i]) / (_weg[i + 1] - _weg[i]) * (_zeit[i + 1] - _zeit[i]);
        return Dauer;
    }

    /// <summary>Wie schnell fährt der Karren nach dieser Zeit (Meter je Spielminute)? 0, solange er an einer Fähre wartet.</summary>
    public double MeterJeMinute(double minute)
    {
        var i = Stueck(Math.Clamp(minute, 0, Dauer));
        return (_weg[i + 1] - _weg[i]) * Laenge / Math.Max(1e-9, _zeit[i + 1] - _zeit[i]);
    }

    private int Stueck(double minute)
    {
        var i = Array.BinarySearch(_zeit, minute);
        if (i < 0) i = ~i - 1;
        return Math.Clamp(i, 0, _zeit.Length - 2);
    }
}

/// <summary>Wo ein Karren auf seiner sichtbaren Fahrt ist.</summary>
public enum Etappe
{
    /// <summary>Am Hof des Betriebs.</summary>
    Hof,
    /// <summary>Auf dem Weg zum Markt.</summary>
    Hin,
    /// <summary>Am Markt (wartet, bis der Kern verkauft hat).</summary>
    Markt,
    /// <summary>Auf dem Rückweg.</summary>
    Zurueck,
}

/// <summary>
/// Die sichtbare Fahrt eines Karrens. Der Kern entscheidet, wann ein Karren fährt, und rechnet die Fahrzeit in Spielminuten – seit Datenversion 65
/// nach Weglänge und Karrenart auf derselben Strecke, die das Bild fährt. Das Bild fährt im festen Tempo der Art; früher oder später am Markt als
/// der Kern ist der Karren nur an der Höchstdauer des Kerns, bei einer anderen als der Standard-Rundendauer (<see cref="Gehtempo.MeterJeSpielminute"/>)
/// und mit alten Daten (dann sucht das Bild seinen Weg im Gassennetz). So gleicht der Zug es aus:
///   • Jede Fahrt, die der Kern beginnt (Wechsel nach „zum Markt“), merkt sich der Zug als offen. Am Hof fährt er sie, sobald er da ist.
///     Fährt er noch, verfallen verpasste Fahrten nicht – aber es bleibt höchstens <b>eine</b> offen, die übrigen werden übersprungen.
///   • Am Markt wartet der Zug, solange der Kern ihn noch dort wähnt, und kehrt sofort um, wenn der Kern verkauft hat.
///   • Sieht keiner hin (Karren und der Ort, wo der Kern ihn hätte, liegen außerhalb des Bildes), gilt der Kern: <see cref="Abgleichen"/>
///     setzt den Zug auf den Ort des Kerns. So springt nie ein Karren im Bild, und wer wegschaut, findet den Betrieb im Stand der Simulation.
/// </summary>
public sealed class Karrenzug
{
    public Etappe Etappe { get; private set; } = Etappe.Hof;

    /// <summary>Spielminuten seit Beginn der Etappe (nur bei Hin und Zurück).</summary>
    public double Minute { get; private set; }

    /// <summary>Der Kern hat eine Fahrt begonnen, die der Zug noch nicht gefahren ist.</summary>
    public bool FahrtOffen { get; private set; }

    private KarrenZustand _kern = KarrenZustand.AmBetrieb;

    /// <summary>Lässt den Zug <paramref name="minuten"/> Spielminuten weiterfahren; <paramref name="kern"/> ist der Zustand des Karrens im Kern.</summary>
    public void Weiter(double minuten, KarrenZustand kern, Fahrprofil hin, Fahrprofil zurueck)
    {
        if (kern == KarrenZustand.ZumMarkt && _kern != KarrenZustand.ZumMarkt) FahrtOffen = true;
        _kern = kern;
        for (var runde = 0; runde < 8; runde++) // eine Etappe je Durchgang; acht decken zwei Umläufe
        {
            switch (Etappe)
            {
                case Etappe.Hof:
                    if (!FahrtOffen) return;
                    FahrtOffen = false;
                    (Etappe, Minute) = (Etappe.Hin, 0);
                    break;
                case Etappe.Hin:
                    if (Minute + minuten < hin.Dauer) { Minute += minuten; return; }
                    minuten -= hin.Dauer - Minute;
                    (Etappe, Minute) = (Etappe.Markt, 0);
                    break;
                case Etappe.Markt:
                    if (kern == KarrenZustand.ZumMarkt) return; // der Kern hat noch nicht verkauft
                    (Etappe, Minute) = (Etappe.Zurueck, 0);
                    break;
                default:
                    if (Minute + minuten < zurueck.Dauer) { Minute += minuten; return; }
                    minuten -= zurueck.Dauer - Minute;
                    (Etappe, Minute) = (Etappe.Hof, 0);
                    break;
            }
        }
    }

    /// <summary>
    /// Setzt den Zug auf den Ort des Kerns: <paramref name="kernWeg"/> ist der Anteil des Weges vom Hof zum Markt (0 bis 1), an dem der Kern den
    /// Karren hat. Der Zug trägt keine offene Fahrt mehr in sich.
    /// </summary>
    public void Abgleichen(KarrenZustand kern, double kernWeg, Fahrprofil hin, Fahrprofil zurueck)
    {
        _kern = kern;
        FahrtOffen = false;
        switch (kern)
        {
            case KarrenZustand.AmBetrieb:
                (Etappe, Minute) = (Etappe.Hof, 0);
                break;
            case KarrenZustand.ZumMarkt:
                (Etappe, Minute) = kernWeg >= 1 ? (Etappe.Markt, 0) : (Etappe.Hin, hin.ZeitBei(kernWeg));
                break;
            default: // der Rückweg zählt vom Markt aus
                (Etappe, Minute) = kernWeg <= 0 ? (Etappe.Hof, 0) : (Etappe.Zurueck, zurueck.ZeitBei(1 - kernWeg));
                break;
        }
    }

    /// <summary>Der Anteil des Weges vom Hof zum Markt (0 bis 1), an dem der Zug sichtbar ist.</summary>
    public double Weg(Fahrprofil hin, Fahrprofil zurueck) => Etappe switch
    {
        Etappe.Hof => 0,
        Etappe.Hin => hin.WegBei(Minute),
        Etappe.Markt => 1,
        _ => 1 - zurueck.WegBei(Minute),
    };

    /// <summary>Wie schnell der Zug gerade fährt (Meter je Spielminute); 0 am Hof, am Markt und an der Fähre.</summary>
    public double MeterJeMinute(Fahrprofil hin, Fahrprofil zurueck) => Etappe switch
    {
        Etappe.Hin => hin.MeterJeMinute(Minute),
        Etappe.Zurueck => zurueck.MeterJeMinute(Minute),
        _ => 0,
    };

    /// <summary>Fährt der Zug dem Markt zu (Hin, oder er steht dort und kehrt gleich um)? Bestimmt, wohin der Karren blickt.</summary>
    public bool ZumMarkt => Etappe is Etappe.Hof or Etappe.Hin or Etappe.Markt;

    /// <summary>
    /// Die Profile wurden neu gerechnet (der Belag hat sich geändert): die Zeit bleibt an derselben Stelle des Weges.
    /// </summary>
    public void Umstellen(Fahrprofil altHin, Fahrprofil altZurueck, Fahrprofil neuHin, Fahrprofil neuZurueck)
    {
        if (Etappe == Etappe.Hin) Minute = neuHin.ZeitBei(altHin.WegBei(Minute));
        else if (Etappe == Etappe.Zurueck) Minute = neuZurueck.ZeitBei(altZurueck.WegBei(Minute));
    }
}
