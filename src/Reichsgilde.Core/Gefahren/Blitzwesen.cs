using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Gefahren;

/// <summary>
/// Ein Gebäude, in das der Blitz einschlagen kann: ein Bau der Karte (<see cref="Kartengebaeude"/>) oder ein Neubau (<see cref="Grundstueck"/>).
/// </summary>
/// <param name="Kennung">Kennung des Gebäudes in der Karte, bei Neubauten „grundstueck:&lt;Nummer&gt;“.</param>
/// <param name="Name">Für die Meldung: „Rathaus“, „die Schmiede am Markt“, „ein Bürgerhaus“ …</param>
/// <param name="X">Mitte der Grundfläche (Meter, wie die Karte).</param>
/// <param name="Z">Mitte der Grundfläche (Meter, wie die Karte).</param>
/// <param name="Hoehe">Höhe des Gebäudes (Meter, <see cref="BlitzDaten.Hoehen"/>).</param>
/// <param name="Boden">Höhe des Bodens dort (Meter).</param>
/// <param name="Betrieb">Werkstatt oder Umlandbetrieb: der Betrieb darin (sonst null).</param>
/// <param name="Bewohner">Wohnhaus eines Hauses: wer darin wohnt (sonst null).</param>
public sealed record Blitzziel(string Kennung, string Name, double X, double Z, double Hoehe, double Boden, Betrieb? Betrieb, Konto? Bewohner)
{
    /// <summary>Wem das Gebäude gehört (Werkstatt: dem Haus des Betriebs; Wohnhaus: dem Bewohner) – sonst null.</summary>
    public Konto? Besitzer => Betrieb?.Konto ?? Bewohner;
}

/// <summary>Ein Blitzeinschlag: wann, wo und bis wann es brennt.</summary>
/// <param name="Minute">Spielminute des Einschlags.</param>
/// <param name="BrandEnde">Ab dieser Spielminute brennt es nicht mehr (gelöscht oder ausgebrannt).</param>
public sealed record Blitzeinschlag(long Minute, long BrandEnde, Blitzziel Ziel)
{
    /// <summary>Brennt es zur Spielminute <paramref name="minute"/>?</summary>
    public bool BrenntUm(long minute) => minute >= Minute && minute < BrandEnde;
}

/// <summary>
/// Blitzschlag im Gewitter (Wunsch des Nutzers vom 2.11.2026, Festlegungen des Teamleiters): Mitten in jedem Gewitter – wenn es über der Stadt
/// steht (<see cref="Wetterabschnitt.Mitte"/>) – schlägt der Blitz mit <see cref="BlitzDaten.Chance"/> in ein Gebäude der Stadt ein. Jedes
/// Gebäude kann es treffen, hohe und solche auf Anhöhen öfter (<see cref="BlitzDaten.Hoehen"/>), die Häuser der Spieler eingeschlossen. Das
/// Gebäude brennt 1–3 Spielstunden (<see cref="BlitzDaten.DauerMinuten"/>), dann ist es gelöscht oder ausgebrannt. Kein Überspringen auf
/// Nachbarhäuser (später).
///
/// <b>Schaden (Platzhalter):</b> Eine Werkstatt repariert ab dem Einschlag statt zu arbeiten (<see cref="Betrieb.SabotageMinuten"/>, wie nach
/// einer Sabotage); ein Wohnhaus verliert bis zur Instandsetzung sein Ansehen (<see cref="Konto.WohnhausInstandBis"/>, wie nach einer
/// Brandstiftung). Kirchen, Rathaus, Einwohner- und Kulissenhäuser brennen nur im Bild.
///
/// <b>Zufall und Spielstand:</b> Jeder Wurf kommt aus einer <see cref="Zufallsquelle"/>, deren Startwert aus dem Startwert der Partie und der
/// Minute des Gewitters folgt (wie der <see cref="Wetterplan"/>). So verbraucht der Blitz keinen Wurf eines anderen Generators, und nichts davon
/// muss in den Spielstand: Der Schaden steht in den Feldern, die ohnehin gespeichert werden, und welche Feuer gerade brennen, rechnet der Kern
/// nach dem Laden aus dem Wetterplan nach (<see cref="Einschlaege"/>, nur fürs Bild und die Meldung „gelöscht“).
/// </summary>
public sealed class Blitzwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly int _startwert;
    private readonly Wetterplan _plan;
    private readonly List<Blitzeinschlag> _einschlaege = new();

    /// <summary>Bis zu dieser Spielminute (ausschließlich) sind die Einschläge bekannt; −1: noch nicht nachgerechnet (neu oder nach dem Laden).</summary>
    private long _bekanntBis = -1;

    internal Blitzwesen(Stadtwirtschaft w, BlitzDaten? daten, int startwert)
    {
        _w = w;
        Daten = daten;
        _startwert = startwert;
        _plan = new Wetterplan(startwert);
    }

    public BlitzDaten? Daten { get; }

    /// <summary>Schlägt der Blitz ein? Dafür braucht es den Abschnitt „blitz“ und die Karte (sonst weiß der Kern nicht, wo Gebäude stehen).</summary>
    public bool IstAktiv => Daten is not null && _w.Karte is not null;

    /// <summary>Der Wetterplan der Partie (derselbe wie in der Darstellung).</summary>
    public Wetterplan Wetter => _plan;

    /// <summary>
    /// Die Einschläge des laufenden Jahres (ältere fallen zum Jahreswechsel weg), der jüngste zuletzt. Nach dem Laden aus dem Wetterplan
    /// nachgerechnet; ein Einschlag über das Entwicklerfenster fehlt danach.
    /// </summary>
    public IReadOnlyList<Blitzeinschlag> Einschlaege
    {
        get
        {
            Nachrechnen(_w.Uhr.VergangeneMinuten);
            return _einschlaege;
        }
    }

    /// <summary>Das Feuer, das gerade in diesem Gebäude brennt (Kennung wie <see cref="Blitzziel.Kennung"/>) – oder null.</summary>
    public Blitzeinschlag? Brand(string kennung) =>
        Einschlaege.LastOrDefault(e => e.Ziel.Kennung == kennung && e.BrenntUm(_w.Uhr.VergangeneMinuten));

    /// <summary>
    /// Die Minuten dieses Jahres, zu denen ein Gewitter über der Stadt steht und der Blitz einschlagen kann (Spielminuten seit Spielbeginn).
    /// Für Tests und die Messung.
    /// </summary>
    public IEnumerable<long> Gewitterminuten(int jahr)
    {
        foreach (var a in _plan.Abschnitte(jahr))
            if (a.Lage == Wetterlage.Gewitter)
                yield return (long)jahr * Spielzeit.MinutenProTag + a.Mitte;
    }

    /// <summary>Jede Spielminute: Einschlag mitten im Gewitter, Meldung, wenn ein Feuer aus ist.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        var jetzt = zeit.VergangeneMinuten;
        Nachrechnen(jetzt);
        foreach (var e in _einschlaege)
            if (e.BrandEnde == jetzt)
                _w.Melden($"Das Feuer in {e.Ziel.Name} ist aus{(e.Ziel.Besitzer is { } b ? $" ({b.Inhaber})" : "")} – es bleiben Ruß und Schaden.");
        if (IstGewitterminute(jetzt) && Wurf(jetzt, out var ziel, out var dauer))
            Einschlagen(ziel, jetzt, dauer, wirken: true);
        _bekanntBis = jetzt + 1;
    }

    /// <summary>
    /// Entwicklerfenster und Startoption <c>--blitzschlag</c>: Der Blitz schlägt sofort ein – in das Gebäude mit dieser Kennung (oder den Betrieb
    /// mit diesem Namen, oder das Wohnhaus dieses Hauses) bzw. bei null in eines nach den Gewichten. Ohne Gewitter.
    /// </summary>
    internal Befehlsergebnis Entwicklerblitz(string? gebaeude, Konto? haus)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt schlägt kein Blitz ein (Abschnitt „blitz“ oder Karte fehlt).");
        var jetzt = _w.Uhr.VergangeneMinuten;
        Nachrechnen(jetzt);
        var ziele = Ziele();
        Blitzziel? ziel;
        var zufall = new Zufallsquelle(Saat(jetzt) ^ 0x5bd1e995);
        if (gebaeude is null)
            ziel = Waehlen(ziele, zufall.NextDouble());
        else
            ziel = ziele.FirstOrDefault(z => z.Kennung == gebaeude)
                   ?? ziele.FirstOrDefault(z => z.Betrieb?.Name == gebaeude)
                   ?? (gebaeude == "haus" && haus is not null ? ziele.FirstOrDefault(z => z.Bewohner == haus) : null)
                   ?? (gebaeude == "werkstatt" && haus is not null ? ziele.FirstOrDefault(z => z.Betrieb?.Konto == haus) : null);
        if (ziel is null) return Befehlsergebnis.Abgelehnt($"Ein Gebäude „{gebaeude}“ gibt es hier nicht.");
        if (Einschlaege.Any(e => e.Ziel.Kennung == ziel.Kennung && e.BrenntUm(jetzt)))
            return Befehlsergebnis.Abgelehnt($"{ziel.Name} brennt schon.");
        var d = Daten!.DauerMinuten;
        var e = Einschlagen(ziel, jetzt, d[0] + zufall.Next(d[1] - d[0] + 1), wirken: true);
        return Befehlsergebnis.Ok($"Blitzschlag in {ziel.Name} – es brennt {Zahlenformat.Knapp((e.BrandEnde - e.Minute) / 60.0)} Stunden.");
    }

    // ── Würfeln ─────────────────────────────────────────────────────────────

    private bool IstGewitterminute(long minute)
    {
        var (abschnitte, i, imTag) = _plan.Stelle(minute);
        return abschnitte[i].Lage == Wetterlage.Gewitter && abschnitte[i].Mitte == imTag;
    }

    /// <summary>Startwert des Zufalls für das Gewitter zu dieser Minute: aus dem Startwert der Partie und der Minute, sonst nichts.</summary>
    private int Saat(long minute) => unchecked(_startwert * 92821 + (int)minute * 68917 + 5381);

    /// <summary>Würfelt für das Gewitter zur Minute <paramref name="minute"/>: Schlägt es ein, wo, und wie lange brennt es?</summary>
    private bool Wurf(long minute, out Blitzziel ziel, out int dauer)
    {
        ziel = null!;
        dauer = 0;
        var d = Daten!;
        var zufall = new Zufallsquelle(Saat(minute));
        if (zufall.NextDouble() >= d.Chance) return false;
        if (Waehlen(Ziele(), zufall.NextDouble()) is not { } z) return false;
        ziel = z;
        dauer = d.DauerMinuten[0] + zufall.Next(d.DauerMinuten[1] - d.DauerMinuten[0] + 1);
        return true;
    }

    /// <summary>Ein Gebäude nach Gewicht (Höhe über dem mittleren Boden der Stadt, zum Quadrat); <paramref name="wurf"/> 0 … 1.</summary>
    internal static Blitzziel? Waehlen(IReadOnlyList<Blitzziel> ziele, double wurf)
    {
        if (ziele.Count == 0) return null;
        var mittel = 0.0;
        foreach (var z in ziele) mittel += z.Boden;
        mittel /= ziele.Count;
        var gewichte = new double[ziele.Count];
        var summe = 0.0;
        for (var i = 0; i < ziele.Count; i++)
            summe += gewichte[i] = Gewicht(ziele[i], mittel);
        var rest = wurf * summe;
        for (var i = 0; i < ziele.Count; i++)
            if ((rest -= gewichte[i]) < 0) return ziele[i];
        return ziele[^1];
    }

    /// <summary>Gewicht eines Gebäudes: (Höhe + Höhe des Bodens über dem Mittel der Stadt)², mindestens 1 – hohe Bauten und Anhöhen trifft es öfter.</summary>
    internal static double Gewicht(Blitzziel ziel, double mittlererBoden)
    {
        var h = Math.Max(1, ziel.Hoehe + ziel.Boden - mittlererBoden);
        return h * h;
    }

    // ── Ziele ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Alle Gebäude, die es treffen kann, in fester Reihenfolge (erst die Karte in ihrer Reihenfolge, dann die Neubauten nach Nummer): Werkstätten
    /// und Umlandbetriebe, bewohnte und leere Wohnhäuser, Einwohner- und Kulissenhäuser, Rathaus, Kirchen und die übrigen Sonderbauten. Nicht:
    /// Marktplatz (Höhe 0), Felsberge, Weiden, Felder, Wald, Bauplätze und Baustellen.
    /// </summary>
    public IReadOnlyList<Blitzziel> Ziele()
    {
        var liste = new List<Blitzziel>();
        if (_w.Karte is not { } karte || Daten is not { } d) return liste;
        var katalog = karte.Katalog;
        var bau = _w.Bauen;
        var betriebeAufPlaetzen = new Dictionary<string, Betrieb>();
        foreach (var b in _w.Betriebe)
            if (karte.PlatzVon(b.Name) is { } platz && !betriebeAufPlaetzen.ContainsKey(platz.Kennung))
                betriebeAufPlaetzen[platz.Kennung] = b;
        var grundstueckNachKennung = new Dictionary<string, Grundstueck>();
        foreach (var g in bau.Grundstuecke)
            if (g.Kennung is { } k) grundstueckNachKennung[k] = g;

        double Hoehe(string? kennung) => kennung is not null && d.Hoehen.TryGetValue(kennung, out var h) ? h : d.Standardhoehe;
        double Boden(double x, double z) => karte.Hoehen?.Hoehe(x, z) ?? 0;
        string WerkstattName(Betrieb b) => b.Name;
        string? WerkstattModell(Betrieb b) => katalog?.Werkstatt(b.Artdaten.Beruf)?.Kennung;

        foreach (var g in karte.Gebaeude)
        {
            var (x, z) = (g.Punkt[0], g.Punkt[1]);
            switch (g.Art)
            {
                case Bausteinart.Werkstattplatz:
                case Bausteinart.Umlandbetrieb:
                {
                    var b = betriebeAufPlaetzen.GetValueOrDefault(g.Kennung);
                    var h = g.Art == Bausteinart.Umlandbetrieb ? Hoehe(g.Katalog) : b is null ? Hoehe(g.Katalog) : Hoehe(WerkstattModell(b));
                    if (h <= 0) continue;
                    var name = b is not null ? WerkstattName(b) : g.Name != "" ? g.Name : "eine leere Werkstatt";
                    liste.Add(new Blitzziel(g.Kennung, name, x, z, h, Boden(x, z), b, null));
                    break;
                }
                case Bausteinart.Wohnhausplatz:
                {
                    // Der Platz zählt nur, wenn ein Haus darauf steht (bewohnt oder leer, keine Baustelle und kein Bauplatz)
                    if (!grundstueckNachKennung.TryGetValue(g.Kennung, out var gs) || gs.Art != Grundstuecksart.Wohnhaus || gs.Arbeit is not null) continue;
                    if (WohnhausZiel(gs, g.Kennung, katalog, d) is { } ziel) liste.Add(ziel with { Boden = Boden(ziel.X, ziel.Z) });
                    break;
                }
                case Bausteinart.Einwohnerplatz:
                case Bausteinart.Kulisse:
                case Bausteinart.Sonderbau:
                {
                    var h = Hoehe(g.Katalog);
                    if (h <= 0) continue;
                    var name = g.Art switch
                    {
                        Bausteinart.Sonderbau => g.Name != "" ? g.Name : g.Baustein?.Anzeigename ?? g.Kennung,
                        _ => "ein Bürgerhaus",
                    };
                    liste.Add(new Blitzziel(g.Kennung, name, x, z, h, Boden(x, z), null, null));
                    break;
                }
            }
        }
        foreach (var gs in bau.Grundstuecke.OrderBy(g => g.Nummer))
        {
            if (gs.Kennung is not null || gs.Arbeit is not null) continue; // Startplätze stehen oben; Baustellen brennen nicht
            var kennung = "grundstueck:" + gs.Nummer;
            var (x, z) = (gs.Grundriss.X, gs.Grundriss.Z);
            if (gs.Art == Grundstuecksart.Werkstatt && gs.Betrieb is { } name && _w.Betriebe.FirstOrDefault(b => b.Name == name) is { } b)
                liste.Add(new Blitzziel(kennung, WerkstattName(b), x, z, Hoehe(WerkstattModell(b)), Boden(x, z), b, null));
            else if (gs.Art == Grundstuecksart.Wohnhaus && WohnhausZiel(gs, kennung, katalog, d) is { } ziel)
                liste.Add(ziel with { Boden = Boden(x, z) });
        }
        return liste;
    }

    private Blitzziel? WohnhausZiel(Grundstueck gs, string kennung, Gebaeudekatalog? katalog, BlitzDaten d)
    {
        var bewohner = gs.Besitzer;
        var modell = katalog?.Wohnhaus(gs.Stufe)?.Kennung;
        var h = modell is not null && d.Hoehen.TryGetValue(modell, out var m) ? m : d.Standardhoehe;
        if (h <= 0) return null;
        var stufe = bewohner is not null ? _w.Wohnen.Stufe(bewohner)?.Name : null;
        var name = bewohner is { IstAktiv: true } ? $"{(stufe is null ? "das Wohnhaus" : "das " + stufe)} von {bewohner.Inhaber}" : "ein leeres Wohnhaus";
        return new Blitzziel(kennung, name, gs.Grundriss.X, gs.Grundriss.Z, h, 0, null, bewohner is { IstAktiv: true } ? bewohner : null);
    }

    // ── Einschlagen ─────────────────────────────────────────────────────────

    /// <summary>
    /// Der Blitz schlägt ein. Mit <paramref name="wirken"/> (im Lauf der Partie) bekommt das Gebäude seinen Schaden und es gibt eine Meldung;
    /// ohne (beim Nachrechnen nach dem Laden) wird der Einschlag nur gemerkt – der Schaden steht dann schon im Spielstand.
    /// </summary>
    private Blitzeinschlag Einschlagen(Blitzziel ziel, long minute, int dauer, bool wirken)
    {
        var e = new Blitzeinschlag(minute, minute + dauer, ziel);
        _einschlaege.Add(e);
        if (!wirken) return e;
        var d = Daten!;
        string folge;
        if (ziel.Betrieb is { } b)
        {
            b.Sabotieren(d.StillstandMinuten);
            if (d.Reparatur > 0 && b.Konto.IstAktiv) b.SchadenBuchen(d.Reparatur);
            folge = $" {d.StillstandMinuten / 60} Arbeitsstunden steht die Arbeit still" +
                    (d.Reparatur > 0 ? $", Reparatur {Zahlenformat.Ganz(d.Reparatur)} {_w.Daten.Allgemein.Waehrung}." : ".");
        }
        else if (ziel.Bewohner is { } k && _w.Wohnen.Daten is not null)
        {
            var bis = e.BrandEnde + d.InstandMinuten;
            _w.Wohnen.Brand(k, Math.Max(k.WohnhausInstandBis ?? 0, bis));
            if (d.WohnhausReparatur > 0) k.Schaden(d.WohnhausReparatur);
            folge = " Bis es instand gesetzt ist, fehlt das Ansehen des Hauses." +
                    (d.WohnhausReparatur > 0 ? $" Reparatur {Zahlenformat.Ganz(d.WohnhausReparatur)} {_w.Daten.Allgemein.Waehrung}." : "");
        }
        else folge = "";
        var wem = ziel.Betrieb is { } bb ? $" ({bb.Konto.Inhaber})" : "";
        _w.Melden($"Blitzschlag! {Gross(ziel.Name)}{wem} brennt.{folge}");
        return e;
    }

    private static string Gross(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>
    /// Nach dem Laden (oder beim ersten Blick): die Einschläge des laufenden Jahres bis vor <paramref name="bis"/> aus dem Wetterplan nachrechnen,
    /// ohne Schaden (der steht im Spielstand). Zum Jahreswechsel fallen die alten weg.
    /// </summary>
    private void Nachrechnen(long bis)
    {
        var jahresbeginn = bis / Spielzeit.MinutenProTag * Spielzeit.MinutenProTag;
        if (_einschlaege.Count > 0 && _einschlaege[0].Minute < jahresbeginn)
            _einschlaege.RemoveAll(e => e.Minute < jahresbeginn && e.BrandEnde <= bis);
        if (_bekanntBis >= 0 || !IstAktiv) return;
        _bekanntBis = bis;
        foreach (var minute in Gewitterminuten((int)(bis / Spielzeit.MinutenProTag)))
            if (minute < bis && Wurf(minute, out var ziel, out var dauer))
                Einschlagen(ziel, minute, dauer, wirken: false);
    }
}
