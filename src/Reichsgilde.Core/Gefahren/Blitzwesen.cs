using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Speichern;
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

    /// <summary>Halbe Ausdehnung der Grundfläche in x bzw. z (Meter, achsparallel; 0 = unbekannt) – für die Nachbarn beim Überspringen.</summary>
    public double HalbX { get; init; }

    /// <summary>Siehe <see cref="HalbX"/>.</summary>
    public double HalbZ { get; init; }

    /// <summary>Abstand der Grundflächen (Kante zu Kante, achsparallel; 0, wenn sie sich berühren) im Quadrat.</summary>
    public double AbstandImQuadrat(Blitzziel b)
    {
        var dx = Math.Max(0, Math.Abs(X - b.X) - HalbX - b.HalbX);
        var dz = Math.Max(0, Math.Abs(Z - b.Z) - HalbZ - b.HalbZ);
        return dx * dx + dz * dz;
    }
}

/// <summary>Woher ein Feuer kommt: Blitzschlag oder vom brennenden Nachbarhaus übergesprungen.</summary>
public enum Brandursache { Blitz, Uebersprung }

/// <summary>Ein Blitzeinschlag (oder ein übergesprungenes Feuer): wann, wo, bis wann es brennt und wie schwer.</summary>
/// <param name="Minute">Spielminute des Einschlags bzw. des Übersprungs.</param>
/// <param name="BrandEnde">Ab dieser Spielminute brennt es nicht mehr (gelöscht oder ausgebrannt).</param>
public sealed record Blitzeinschlag(long Minute, long BrandEnde, Blitzziel Ziel)
{
    /// <summary>Wie schwer (0 = leicht … 1 = schwer): aus der gewürfelten Brenndauer, mit Wächter kleiner (<see cref="Blitzwesen.Schwere"/>).</summary>
    public double Schwere { get; init; }

    /// <summary>Blitzschlag oder Übersprung.</summary>
    public Brandursache Ursache { get; init; }

    /// <summary>Übersprung: Name des Gebäudes, von dem das Feuer kam (sonst null).</summary>
    public string? Von { get; init; }

    /// <summary>Ist das Feuer vom Nachbarhaus übergesprungen (kein Blitz, keine Glocke, keine eigene Löschkette)?</summary>
    public bool IstUebersprung => Ursache == Brandursache.Uebersprung;

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
/// <b>Schaden (Variante B, Nutzer 3.11.2026):</b> Eine Werkstatt repariert ab dem Einschlag statt zu arbeiten (<see cref="Betrieb.SabotageMinuten"/>,
/// wie nach einer Sabotage), zahlt eine Reparatur nach ihrem Bauwert und verliert einen Teil ihres Lagers; ein Wohnhaus verliert bis zur
/// Instandsetzung sein Ansehen (<see cref="Konto.WohnhausInstandBis"/>) und zahlt eine Reparatur nach der Formel der Brandstiftung. Wie schwer,
/// folgt aus der schon gewürfelten Brenndauer (<see cref="Schwere"/>, kein neuer Wurf). Wächter und Hauswächter löschen mit (Feuer kürzer und
/// leichter), das Löschfass bzw. die Brandschutz-Einrichtung (<see cref="Betrieb.BrandFaktor"/>) senkt den Schaden. Kein Ausbrennen. Kirchen,
/// Rathaus, Einwohner- und Kulissenhäuser brennen nur im Bild (ihre Wirkung kommt in einem eigenen PR).
///
/// <b>Überspringen</b> (<see cref="BlitzDaten.Sprung"/>): Je volle Spielstunde des Feuers kann es auf jedes Nachbarhaus überspringen (Grundflächen
/// höchstens <see cref="BlitzSprungDaten.Abstand"/> auseinander); ein übergesprungenes Feuer brennt kürzer, ist leichter und springt nicht weiter.
/// Ob und wann, wird beim Einschlag gewürfelt; der Sprung wartet bis zu seiner Minute (<see cref="Anstehend"/>).
///
/// <b>Zufall und Spielstand:</b> Jeder Wurf kommt aus einer <see cref="Zufallsquelle"/>, deren Startwert aus dem Startwert der Partie und der
/// Minute des Gewitters folgt (wie der <see cref="Wetterplan"/>). So verbraucht der Blitz keinen Wurf eines anderen Generators. Die Einschläge und
/// die anstehenden Sprünge stehen im Spielstand (<see cref="BlitzStand"/>) und werden beim Laden übernommen. Nur Spielstände mit Daten 68/69 ohne
/// dieses Feld rechnen sie wie früher aus dem Wetterplan nach; ältere beginnen ohne Einschläge.
/// </summary>
public sealed class Blitzwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly int _startwert;
    private readonly Wetterplan _plan;
    private readonly List<Blitzeinschlag> _einschlaege = new();

    /// <summary>Gewürfelte Sprünge, die erst noch kommen – nach Minute, bei gleicher Minute in der Reihenfolge des Würfelns.</summary>
    private readonly List<Blitzeinschlag> _anstehend = new();

    /// <summary>Schalter: −1 = noch nicht nachgerechnet (neu oder nach dem Laden), sonst die Minute nach dem letzten Lauf von <see cref="Minute"/>.</summary>
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
    /// Die Einschläge des laufenden Jahres und noch brennende des Vorjahres (ältere fallen zum Jahreswechsel weg), samt übergesprungener Feuer,
    /// der jüngste zuletzt. Stehen im Spielstand.
    /// </summary>
    public IReadOnlyList<Blitzeinschlag> Einschlaege
    {
        get
        {
            Nachrechnen(_w.Uhr.VergangeneMinuten);
            return _einschlaege;
        }
    }

    /// <summary>Sprünge, die schon gewürfelt sind, aber erst noch kommen (für Tests; das Bild zeigt sie erst, wenn sie in <see cref="Einschlaege"/> stehen).</summary>
    public IReadOnlyList<Blitzeinschlag> Anstehend => _anstehend;

    /// <summary>Das Feuer, das gerade in diesem Gebäude brennt (Kennung wie <see cref="Blitzziel.Kennung"/>) – oder null.</summary>
    public Blitzeinschlag? Brand(string kennung) =>
        Einschlaege.LastOrDefault(e => e.Ziel.Kennung == kennung && e.BrenntUm(_w.Uhr.VergangeneMinuten));

    /// <summary>
    /// Das Feuer (Blitz oder Übersprung), nach dem das Wohnhaus dieses Hauses gerade instand gesetzt wird – oder null (dann war es eine
    /// Brandstiftung oder ist vorbei). Für das Bild: Brandbeginn statt aus der Dauer zu raten.
    /// </summary>
    public Blitzeinschlag? WohnhausBrand(Konto k) =>
        k.WohnhausInstandBis is { } bis && Daten is { } d
            ? Einschlaege.LastOrDefault(e => e.Ziel.Bewohner == k && e.BrandEnde + d.InstandMinuten == bis)
            : null;

    /// <summary>
    /// Wie schwer ein Feuer mit dieser gewürfelten Dauer ist: 0 beim kürzesten (<see cref="BlitzDaten.DauerMinuten"/>[0]) … 1 beim längsten.
    /// Kein eigener Wurf – die Dauer ist ohnehin gewürfelt.
    /// </summary>
    public double Schwere(int dauer)
    {
        var d = Daten!.DauerMinuten;
        return d[1] > d[0] ? Math.Clamp((dauer - d[0]) / (double)(d[1] - d[0]), 0, 1) : 1;
    }

    /// <summary>Wert zwischen [leicht, schwer] nach der Schwere.</summary>
    internal static double Anteil(double[] paar, double schwere) => paar[0] + (paar[1] - paar[0]) * schwere;

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
                _w.Melden($"{Gross(e.Ziel.Name)}{(e.Ziel.Besitzer is { } b ? $" ({b.Inhaber})" : "")} brennt nicht mehr – {(e.Ziel.Besitzer is not null ? "es bleiben Ruß und Schaden" : "es bleibt Ruß")}.");
        while (_anstehend.Count > 0 && _anstehend[0].Minute <= jetzt)
        {
            var sprung = _anstehend[0];
            _anstehend.RemoveAt(0);
            if (sprung.Minute == jetzt) Ueberspringen(sprung, jetzt);
        }
        if (IstGewitterminute(jetzt) && Wurf(jetzt, out var ziel, out var dauer, out var zufall))
            Einschlagen(ziel, jetzt, dauer, zufall, wirken: true);
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
                   ?? (gebaeude == "werkstatt" && haus is not null ? ziele.FirstOrDefault(z => z.Betrieb is { IstWerkstatt: true } b && b.Konto == haus) : null);
        if (ziel is null) return Befehlsergebnis.Abgelehnt($"Ein Gebäude „{gebaeude}“ gibt es hier nicht.");
        if (Einschlaege.Any(e => e.Ziel.Kennung == ziel.Kennung && e.BrenntUm(jetzt)))
            return Befehlsergebnis.Abgelehnt($"{ziel.Name} brennt schon.");
        var d = Daten!.DauerMinuten;
        var e = Einschlagen(ziel, jetzt, d[0] + zufall.Next(d[1] - d[0] + 1), zufall, wirken: true);
        return Befehlsergebnis.Ok($"Blitzschlag: {Gross(ziel.Name)} – es brennt {Zahlenformat.Knapp((e.BrandEnde - e.Minute) / 60.0)} Stunden.");
    }

    // ── Würfeln ─────────────────────────────────────────────────────────────

    private bool IstGewitterminute(long minute)
    {
        var (abschnitte, i, imTag) = _plan.Stelle(minute);
        return abschnitte[i].Lage == Wetterlage.Gewitter && abschnitte[i].Mitte == imTag;
    }

    /// <summary>Startwert des Zufalls für das Gewitter zu dieser Minute: aus dem Startwert der Partie und der Minute, sonst nichts.</summary>
    /// Gut durchmischt (<see cref="Dynastie.Zugwuerfel.Mischen"/>): Benachbarte Startwerte entscheiden sonst oft gleich (Befund der Prüfung).
    private int Saat(long minute) => Dynastie.Zugwuerfel.Mischen(_startwert, (int)minute, 0x426C69);

    /// <summary>
    /// Würfelt für das Gewitter zur Minute <paramref name="minute"/>: Schlägt es ein, wo, und wie lange brennt es (gewürfelte Dauer, ohne Wächter)?
    /// <paramref name="zufall"/> würfelt danach die Sprünge – hinter Chance, Ziel und Dauer, so ändern sich die alten Würfe nicht.
    /// </summary>
    private bool Wurf(long minute, out Blitzziel ziel, out int dauer, out Zufallsquelle zufall)
    {
        ziel = null!;
        dauer = 0;
        var d = Daten!;
        zufall = new Zufallsquelle(Saat(minute));
        if (zufall.NextDouble() >= d.Chance) return false;
        if (Waehlen(Ziele(), zufall.NextDouble()) is not { } z) return false;
        ziel = z;
        dauer = d.DauerMinuten[0] + zufall.Next(d.DauerMinuten[1] - d.DauerMinuten[0] + 1);
        return true;
    }

    /// <summary>Ein Gebäude nach Gewicht (Höhe über dem mittleren Boden der Stadt); <paramref name="wurf"/> 0 … 1.</summary>
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

    /// <summary>
    /// Gewicht eines Gebäudes: Höhe + Höhe des Bodens über dem Mittel der Stadt, mindestens 1 – hohe Bauten und Anhöhen trifft es öfter. Linear
    /// (Teamleiter 2.11.2026): Zum Quadrat bekamen Kirchen und Dom zwei Drittel aller Einschläge, die Häuser der Spieler fast nie einen.
    /// </summary>
    internal static double Gewicht(Blitzziel ziel, double mittlererBoden)
    {
        return Math.Max(1, ziel.Hoehe + ziel.Boden - mittlererBoden);
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
                    liste.Add(Flaeche(new Blitzziel(g.Kennung, name, x, z, h, Boden(x, z), b, null), g.Grundriss));
                    break;
                }
                case Bausteinart.Wohnhausplatz:
                {
                    // Der Platz zählt nur, wenn ein Haus darauf steht (bewohnt oder leer, keine Baustelle und kein Bauplatz)
                    if (!grundstueckNachKennung.TryGetValue(g.Kennung, out var gs) || gs.Art != Grundstuecksart.Wohnhaus || gs.Arbeit is not null) continue;
                    if (WohnhausZiel(gs, g.Kennung, katalog, d) is { } ziel) liste.Add(Flaeche(ziel with { Boden = Boden(ziel.X, ziel.Z) }, g.Grundriss ?? gs.Grundriss));
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
                    liste.Add(Flaeche(new Blitzziel(g.Kennung, name, x, z, h, Boden(x, z), null, null), g.Grundriss));
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
                liste.Add(Flaeche(new Blitzziel(kennung, WerkstattName(b), x, z, Hoehe(WerkstattModell(b)), Boden(x, z), b, null), gs.Grundriss));
            else if (gs.Art == Grundstuecksart.Wohnhaus && WohnhausZiel(gs, kennung, katalog, d) is { } ziel)
                liste.Add(Flaeche(ziel with { Boden = Boden(x, z) }, gs.Grundriss));
        }
        return liste;
    }

    private static Blitzziel Flaeche(Blitzziel ziel, Grundriss? g) => g is { } r ? ziel with { HalbX = r.HalbX, HalbZ = r.HalbZ } : ziel;

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

    /// <summary>Löscht hier ein Wächter (Werkstatt) bzw. Hauswächter (Wohnhaus) mit?</summary>
    private static bool Bewacht(Blitzziel ziel) => ziel.Betrieb?.Waechter == true || ziel.Bewohner?.Hauswaechter == true;

    /// <summary>Wie lange es brennt: gewürfelte Dauer, mit Wächter kürzer (mindestens 1 Minute).</summary>
    private int Brenndauer(Blitzziel ziel, double dauer) =>
        Math.Max(1, (int)Math.Round(dauer * (Bewacht(ziel) ? Daten!.WaechterFaktor : 1)));

    /// <summary>
    /// Der Blitz schlägt ein. Mit <paramref name="wirken"/> (im Lauf der Partie) bekommt das Gebäude seinen Schaden und es gibt eine Meldung;
    /// ohne (beim Nachrechnen für Spielstände mit Daten 68/69) wird der Einschlag nur gemerkt – der Schaden steht dann schon im Spielstand.
    /// <paramref name="dauer"/> ist die gewürfelte Dauer (ohne Wächter); <paramref name="zufall"/> würfelt danach die Sprünge.
    /// </summary>
    private Blitzeinschlag Einschlagen(Blitzziel ziel, long minute, int dauer, Zufallsquelle zufall, bool wirken, long nachrechnenBis = 0)
    {
        var bewacht = Bewacht(ziel) ? Daten!.WaechterFaktor : 1;
        var e = new Blitzeinschlag(minute, minute + Brenndauer(ziel, dauer), ziel) { Schwere = Schwere(dauer) * bewacht };
        _einschlaege.Add(e);
        if (Daten!.Sprung is { } sprung)
            foreach (var j in SpruengeWuerfeln(e, dauer, zufall, sprung))
                if (wirken || j.Minute >= nachrechnenBis) Einreihen(j);
                else _einschlaege.Add(j);
        if (!wirken) return e;
        var wem = ziel.Betrieb is { } bb ? $" ({bb.Konto.Inhaber})" : "";
        _w.Melden($"Blitzschlag! {Gross(ziel.Name)}{wem} brennt.{Wirken(e)}");
        return e;
    }

    /// <summary>Ein gewürfelter Sprung ist dran: Das Nachbarhaus fängt Feuer – wenn es nicht schon brennt und noch dasselbe Gebäude ist.</summary>
    private void Ueberspringen(Blitzeinschlag j, long jetzt)
    {
        if (_einschlaege.Any(e => e.Ziel.Kennung == j.Ziel.Kennung && e.BrenntUm(jetzt))) return;
        if (j.Ziel.Betrieb is { } b && (!_w.Betriebe.Contains(b) || !b.Konto.IstAktiv)) return; // verkauft oder aufgegeben: das Ziel stimmt nicht mehr
        if (j.Ziel.Bewohner is { IstAktiv: false }) return;
        _einschlaege.Add(j);
        var wem = j.Ziel.Betrieb is { } bb ? $" ({bb.Konto.Inhaber})" : "";
        _w.Melden($"Funkenflug! Das Feuer springt von {j.Von} auf {j.Ziel.Name}{wem} über.{Wirken(j)}");
    }

    private void Einreihen(Blitzeinschlag j)
    {
        var i = _anstehend.FindLastIndex(x => x.Minute <= j.Minute) + 1;
        _anstehend.Insert(i, j);
    }

    /// <summary>
    /// Würfelt beim Einschlag, ob und wann das Feuer überspringt: zur Mitte jeder vollen Spielstunde des Feuers je Nachbar (nach Kennung,
    /// ordinal) ein Wurf, solange er nicht schon getroffen ist. Chance mal Brandschutz des Nachbarn (Löschfass, Einrichtung) und mal
    /// <see cref="BlitzSprungDaten.WaechterFaktor"/>, wenn er einen Wächter hat. Das übergesprungene Feuer springt nicht weiter.
    /// </summary>
    private List<Blitzeinschlag> SpruengeWuerfeln(Blitzeinschlag e, int dauer, Zufallsquelle zufall, BlitzSprungDaten sp)
    {
        var spruenge = new List<Blitzeinschlag>();
        var stunden = (int)((e.BrandEnde - e.Minute) / 60);
        if (stunden == 0 || sp.ChanceJeStunde <= 0) return spruenge;
        var grenze = sp.Abstand * sp.Abstand;
        var nachbarn = Ziele().Where(z => z.Kennung != e.Ziel.Kennung && e.Ziel.AbstandImQuadrat(z) <= grenze)
            .OrderBy(z => z.Kennung, StringComparer.Ordinal).ToList();
        if (nachbarn.Count == 0) return spruenge;
        var getroffen = new bool[nachbarn.Count];
        for (var stunde = 0; stunde < stunden; stunde++)
        {
            var um = e.Minute + 60L * stunde + 30;
            for (var i = 0; i < nachbarn.Count; i++)
            {
                if (getroffen[i]) continue;
                var n = nachbarn[i];
                var chance = sp.ChanceJeStunde * Brandschutz(n) * (Bewacht(n) ? sp.WaechterFaktor : 1);
                if (zufall.NextDouble() >= chance) continue;
                getroffen[i] = true;
                var bewacht = Bewacht(n) ? Daten!.WaechterFaktor : 1;
                spruenge.Add(new Blitzeinschlag(um, um + Brenndauer(n, dauer * sp.DauerFaktor), n)
                {
                    Schwere = Schwere(dauer) * sp.SchwereFaktor * bewacht, Ursache = Brandursache.Uebersprung, Von = e.Ziel.Name,
                });
            }
        }
        return spruenge;
    }

    /// <summary>Brandschutz des Gebäudes: Löschfass der Werkstatt bzw. Brandschutz-Einrichtung des Wohnhauses (1 = keiner, kleiner = besser).</summary>
    private double Brandschutz(Blitzziel ziel) =>
        ziel.Betrieb?.BrandFaktor ?? (ziel.Bewohner is { } k && _w.Wohnen.Daten is not null ? _w.Wohnen.BrandFaktor(k) : 1);

    /// <summary>
    /// Der Schaden eines Feuers (Blitz oder Übersprung), gebucht in der Minute, in der es ausbricht; gibt den Satz für die Meldung zurück.
    /// Werkstatt: Stillstand, Reparatur nach Bauwert, Lager verbrennt anteilig. Wohnhaus: Ansehen fehlt bis zur Instandsetzung, Reparatur.
    /// </summary>
    private string Wirken(Blitzeinschlag e)
    {
        var d = Daten!;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (e.Ziel.Betrieb is { } b)
        {
            b.Sabotieren(d.StillstandMinuten);
            var schutz = b.BrandFaktor;
            var reparatur = Math.Round(Math.Max(d.Reparatur, Anteil(d.ReparaturAnteil, e.Schwere) * _w.Bauwert(b)) * schutz);
            var verbrannt = LagerVerbrennen(b, Anteil(d.LagerAnteil, e.Schwere) * schutz, out var warenwert);
            if (reparatur > 0 && b.Konto.IstAktiv) b.SchadenBuchen(reparatur);
            else reparatur = 0;
            if (reparatur + warenwert > 0) b.BrandBuchen(reparatur + warenwert);
            return $" {d.StillstandMinuten / 60} Arbeitsstunden steht die Arbeit still" +
                   (reparatur > 0 ? $", Reparatur {Zahlenformat.Ganz(reparatur)} {waehrung}" : "") +
                   (verbrannt.Count > 0 ? $"; verbrannt: {string.Join(", ", verbrannt.Select(v => $"{Zahlenformat.Ganz(v.Stueck)} {v.Ware}"))}" : "") + ".";
        }
        if (e.Ziel.Bewohner is { } k && _w.Wohnen.Daten is not null)
        {
            var bis = e.BrandEnde + d.InstandMinuten;
            _w.Wohnen.Brand(k, Math.Max(k.WohnhausInstandBis ?? 0, bis));
            var grund = _w.Gauner.IstAktiv && _w.Gauner.Daten.Wohnhaus is not null ? _w.Gauner.Brandschaden(k) : 0;
            var reparatur = Math.Round(Math.Max(d.WohnhausReparatur, Anteil(d.WohnhausAnteil, e.Schwere) * grund) * _w.Wohnen.BrandFaktor(k));
            if (reparatur > 0) k.Schaden(reparatur);
            return " Bis es instand gesetzt ist, fehlt das Ansehen des Hauses." +
                   (reparatur > 0 ? $" Reparatur {Zahlenformat.Ganz(reparatur)} {waehrung}." : "");
        }
        return "";
    }

    /// <summary>
    /// Von jeder Ware im Lager verbrennt dieser Anteil (ganze Stück, abgerundet), in fester Reihenfolge (Warenname, ordinal). Gibt die
    /// verbrannten Waren zurück, <paramref name="wert"/> ist ihr Marktwert (für den Wert des Löschfasses).
    /// </summary>
    private List<(string Ware, int Stueck)> LagerVerbrennen(Betrieb b, double anteil, out double wert)
    {
        var liste = new List<(string, int)>();
        wert = 0;
        if (anteil <= 0) return liste;
        foreach (var ware in b.Lager.Keys.Order(StringComparer.Ordinal).ToList())
        {
            var stueck = (int)Math.Floor(b.ImLager(ware) * Math.Min(1, anteil));
            if (stueck <= 0) continue;
            stueck = b.Auslagern(ware, stueck);
            liste.Add((ware, stueck));
            if (_w.Markt.Staende.FirstOrDefault(m => m.Ware == ware) is { } stand) wert += stueck * stand.Preis;
        }
        return liste;
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
            _einschlaege.RemoveAll(e => e.Minute < jahresbeginn && e.BrandEnde < bis); // strikt: in der Minute BrandEnde kommt noch die Meldung „brennt nicht mehr“
        if (_bekanntBis >= 0 || !IstAktiv) return;
        _bekanntBis = bis;
        // auch das Vorjahr: ein Feuer vom späten Abend brennt über den Jahreswechsel (23 → 4 Uhr) hinaus (Befund der Prüfung, gut ein Viertel der Einschläge)
        var jahr = (int)(bis / Spielzeit.MinutenProTag);
        if (jahr > 0)
            foreach (var minute in Gewitterminuten(jahr - 1))
                if (Wurf(minute, out var ziel, out var dauer, out var zufall) && minute + Brenndauer(ziel, dauer) >= bis)
                    Einschlagen(ziel, minute, dauer, zufall, wirken: false, nachrechnenBis: bis);
        foreach (var minute in Gewitterminuten(jahr))
            if (minute < bis && Wurf(minute, out var ziel, out var dauer, out var zufall))
                Einschlagen(ziel, minute, dauer, zufall, wirken: false, nachrechnenBis: bis);
    }

    // ── Spielstand ──────────────────────────────────────────────────────────

    internal BlitzStand Sichern(Func<Konto, int> nummer)
    {
        BlitzeinschlagStand Stand(Blitzeinschlag e) => new()
        {
            Minute = e.Minute, BrandEnde = e.BrandEnde, Kennung = e.Ziel.Kennung, Name = e.Ziel.Name, X = e.Ziel.X, Z = e.Ziel.Z,
            Hoehe = e.Ziel.Hoehe, Boden = e.Ziel.Boden, Betrieb = e.Ziel.Betrieb?.Name,
            Bewohner = e.Ziel.Bewohner is { } k ? nummer(k) : null, Schwere = e.Schwere, Ursache = e.Ursache, Von = e.Von,
        };
        return new BlitzStand { Einschlaege = Einschlaege.Select(Stand).ToList(), Spruenge = _anstehend.Select(Stand).ToList() };
    }

    /// <summary>
    /// Nach dem Laden (nach Konten und Betrieben): die gespeicherten Einschläge übernehmen. Ohne Feld: Daten 68 oder neuer (Spielstände aus der
    /// Zeit von PR #288) rechnen wie bisher aus dem Wetterplan nach; ältere beginnen ohne Einschläge (keine Feuer, wo nie ein Blitz einschlug).
    /// </summary>
    internal void Wiederherstellen(BlitzStand? s, IReadOnlyList<Konto> konten)
    {
        _einschlaege.Clear();
        _anstehend.Clear();
        if (s is null)
        {
            if (_w.Daten.Version < 68) _bekanntBis = _w.Uhr.VergangeneMinuten;
            return;
        }
        Blitzeinschlag Einschlag(BlitzeinschlagStand x)
        {
            var betrieb = x.Betrieb is { } name ? _w.Betriebe.FirstOrDefault(b => b.Name == name) : null;
            var bewohner = x.Bewohner is int i && i >= 0 && i < konten.Count ? konten[i] : null;
            return new Blitzeinschlag(x.Minute, x.BrandEnde, new Blitzziel(x.Kennung, x.Name, x.X, x.Z, x.Hoehe, x.Boden, betrieb, bewohner))
            {
                Schwere = x.Schwere, Ursache = x.Ursache, Von = x.Von,
            };
        }
        _einschlaege.AddRange(s.Einschlaege.Select(Einschlag));
        _anstehend.AddRange(s.Spruenge.Select(Einschlag));
        _bekanntBis = _w.Uhr.VergangeneMinuten;
    }
}
