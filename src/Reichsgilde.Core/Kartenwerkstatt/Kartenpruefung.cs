using System.Text.RegularExpressions;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Kartenregeln in C# – damit die Kartenwerkstatt live prüft, was bisher nur das Python-Werkzeug prüfte
//
//  Quelle der Regeln: tools/karten/karte.py („Prüfungen nach den Kartenregeln“, gelten für jede Karte) und tools/karten/greybox.py
//  („Prüfungen der Greybox“: Abstände der Bauzonen, Werkstätten der Stadt, Gassen über Häusern, Wege über Umlandbetrieben).
//  Die Rechnung ist dieselbe (Kartengeometrie), damit beide Seiten dieselben Befunde liefern. Nicht übernommen ist die
//  Kapazitätsprüfung für Köln (tools/karten/kapazitaet.py) – sie bleibt bis zum Umzug (Schritt 4) im Werkzeug.
//
//  Zwei Wege hinein:
//    • Pruefen(entwurf)        – die ganze Karte, nach jedem Befehl (Fehlerliste, rote Gebäude),
//    • PruefeStelle(entwurf, …) – nur ein Gebäude an einer Stelle gegen den Rest (Umriss grün/rot beim Ziehen, ohne den Entwurf zu ändern).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Wie schwer ein Befund wiegt.</summary>
public enum Schwere
{
    /// <summary>Verstößt gegen eine Regel (die Karte lässt sich speichern, aber nicht ausprobieren).</summary>
    Fehler,

    /// <summary>Auffällig, aber erlaubt (z. B. der Rand eines Felsbergs über einem Nachbarn).</summary>
    Hinweis,
}

/// <summary>
/// Ein Befund der Prüfung: Schwere, Regel (kurze Kennung, z. B. „ueberlappung“), Text auf Deutsch, die betroffenen Gebäude (Kennungen,
/// zum Rot-Zeichnen), ein Ort zum Hinspringen (null, wenn es keinen gibt, z. B. bei der Startbelegung ohne Platz) und die betroffenen
/// übrigen Teile (Wege, Bauzonen, Naturflächen, Pflanzen – zum Rot-Zeichnen; leer, wenn es nur um Gebäude geht).
/// </summary>
public sealed record Befund(Schwere Schwere, string Regel, string Text, IReadOnlyList<string> Gebaeude, Kartenort? Ort, IReadOnlyList<EntwurfTeil>? Teile = null)
{
    /// <summary>Die betroffenen Wege, Flächen und Pflanzen (nie null).</summary>
    public IReadOnlyList<EntwurfTeil> Andere => Teile ?? Array.Empty<EntwurfTeil>();

    public override string ToString() => (Schwere == Schwere.Hinweis ? "Hinweis: " : "") + Text;
}

/// <summary>
/// Welche Regeln die Prüfung anlegt. Die allgemeinen Regeln (wie <c>karte.py</c>) gelten immer; die übrigen hängen an der Karte
/// (<see cref="Fuer"/>): Köln wie <c>koeln.py</c>, die Greybox wie <c>greybox.py</c>, neue Karten mit den Stadtregeln ohne die
/// Greybox-Besonderheiten.
/// </summary>
public sealed record Pruefregeln
{
    /// <summary>So viele Startgrundstücke (Werkstatt- und Wohnhausplatz „S&lt;n&gt;_…“) müssen gleich weit vom Markt liegen (0 = keine; Köln 8).</summary>
    public int Startgrundstuecke { get; init; }

    /// <summary>True/False verlangt eine geschlossene bzw. offene Stadtmauer (erste Mauer); null = beides erlaubt.</summary>
    public bool? MauerGeschlossen { get; init; }

    /// <summary>
    /// Die Regeln aus <c>greybox.py</c>, die für jede Stadt Sinn ergeben: Bauzonen auf dem Raster, mit Abstand zu Gebäuden, Wegen und Mauer,
    /// ohne Überlappung; Probeplan; Werkstätten mit 2 m Abstand und nicht auf der Mauer; Eingang der Wohnhausplätze am Weg; keine Gasse über
    /// ein Haus der Stadt; Wege über Umlandbetrieben (Hinweis).
    /// </summary>
    public bool Stadtregeln { get; init; }

    /// <summary>Greybox: jeder Werkstattplatz 16 × 14 m mit der Tür nach Süden, und an seinem Hof beginnt seine Gasse.</summary>
    public bool Werkstattnorm { get; init; }

    /// <summary>So viele Wohnhausplätze muss es geben, die ersten vier gleich weit vom Markt (±10 %); 0 = nicht prüfen. Greybox: 8.</summary>
    public int Wohnhausplaetze { get; init; }

    /// <summary>Mindestens so viele Plätze zu 16 × 14 m im Probeplan (Werkstatt und Stadthof), Wohnhäuser und Stadthöfe (Greybox 6, 8, 2).</summary>
    public int MindestWerkstaetten { get; init; }

    public int MindestWohnhaeuser { get; init; }
    public int MindestStadthoefe { get; init; }

    /// <summary>Für neue Karten: allgemeine Regeln und Stadtregeln.</summary>
    public static readonly Pruefregeln Standard = new() { Stadtregeln = true };

    /// <summary>Wie <c>tools/karten/greybox.py</c>.</summary>
    public static readonly Pruefregeln Greybox = new()
    {
        MauerGeschlossen = true, Stadtregeln = true, Werkstattnorm = true, Wohnhausplaetze = 8,
        MindestWerkstaetten = 6, MindestWohnhaeuser = 8, MindestStadthoefe = 2,
    };

    /// <summary>Wie <c>tools/karten/koeln.py</c> (ohne die Kapazität, die bleibt im Werkzeug).</summary>
    public static readonly Pruefregeln Koeln = new() { Startgrundstuecke = 8, MauerGeschlossen = false };

    /// <summary>Die Regeln zu einer Kartenkennung.</summary>
    public static Pruefregeln Fuer(string kennung) => kennung switch
    {
        "greybox" => Greybox,
        "koeln" => Koeln,
        _ => Standard,
    };
}

/// <summary>Prüft einen <see cref="Kartenentwurf"/> nach den Kartenregeln (siehe Kopf der Datei).</summary>
public static class Kartenpruefung
{
    /// <summary>Meter Vorplatz an der Vorderseite jeder Grundfläche.</summary>
    public const double Vorplatz = 2;

    /// <summary>Der Hof eines Werkstattplatzes liegt im Vorplatz, so weit hinter der Vorderkante.</summary>
    public const double HofHinterKante = 0.5;

    /// <summary>Höchstens so weit liegt der Hof eines Werkstattplatzes von der Mitte eines Weges (Eingangsregel des Kerns).</summary>
    public const double Strassennah = 7;

    /// <summary>So weit darf ein Tor neben seiner Mauerlinie liegen.</summary>
    public const double Tornah = 1;

    /// <summary>Höchstens so weit liegt eine Bauzone von einer Karrenstraße.</summary>
    public const double Anschluss = 8;

    // Stadtregeln (greybox.py)
    public const double AbstandStrasse = 0.5, AbstandFeste = 2, AbstandMauer = 2, AbstandWerkstatt = 2;

    /// <summary>Prüft die ganze Karte. Leer = keine Befunde.</summary>
    public static List<Befund> Pruefen(Kartenentwurf entwurf) => new Lauf(entwurf).Alles();

    /// <summary>
    /// Prüft nur ein Gebäude an einer Stelle gegen den Rest der Karte – für den Umriss beim Setzen und Ziehen (grün = keine Fehler).
    /// Der Entwurf ändert sich nicht. <paramref name="kennung"/>: das Gebäude, das gerade gezogen wird (es zählt nicht als Nachbar);
    /// null beim Setzen eines neuen. Punkt und Drehung werden genommen, wie sie sind (die Darstellung rundet vorher, wenn sie will).
    /// </summary>
    public static List<Befund> PruefeStelle(Kartenentwurf entwurf, string katalog, double x, double z, double drehung, double massstab = 1, string? kennung = null)
    {
        var alt = entwurf.GebaeudeMit(kennung);
        var probe = alt?.Kopie() ?? new EntwurfGebaeude { Kennung = kennung ?? "(neu)", Name = entwurf.Katalog.Finde(katalog)?.Anzeigename ?? katalog };
        (probe.Katalog, probe.X, probe.Z, probe.Drehung, probe.Massstab) = (katalog, x, z, drehung, massstab);
        probe.Baustein = entwurf.Katalog.Finde(katalog);
        return new Lauf(entwurf).Stelle(probe, alt);
    }

    /// <summary>Gezeichnete Breite eines Weges: aus der Karte, sonst nach der Klasse (wie die Stadtansicht).</summary>
    public static double BreiteVon(Kartenweg w) => w.BreiteWirksam;

    /// <summary>Ab so vielen Metern Abstand zur Mitte eines Weges steht eine Pflanze nicht mehr „auf“ ihm (Rand zur halben Breite).</summary>
    public const double PflanzeAmWeg = 0.5;

    /// <summary>Ein Prüflauf: bereitet Wege, Mauern und Zonen einmal vor.</summary>
    private sealed class Lauf
    {
        private readonly Kartenentwurf _e;
        private readonly Pruefregeln _r;
        private readonly List<Befund> _b = new();
        private readonly List<(EntwurfWeg Weg, Kartenort[] Linie, double Breite, Rahmen Rahmen)> _wege;
        private readonly List<(Kartenmauer Mauer, Kartenort[] Linie, Rahmen Rahmen)> _mauern;
        private readonly List<(EntwurfBauzone Zone, Kartenort[] Flaeche, Rahmen Rahmen)> _zonen;
        private Dictionary<EntwurfBauzone, HashSet<(int, int)>>? _felder;
        private readonly Kartenort[]? _stadtgebiet;
        private readonly Dictionary<EntwurfGebaeude, (IReadOnlyList<Kartenort> Ecken, Rahmen Rahmen)> _ecken = new(ReferenceEqualityComparer.Instance);

        public Lauf(Kartenentwurf e)
        {
            (_e, _r) = (e, e.Regeln);
            // Wege mit ihrem Verlauf (bei glatten Wegen die abgetastete Kurve) – so sehen Spiel und Bauwesen sie
            _wege = e.Wege.Where(w => w.Punkte.Count > 0).Select(w => (w, l: w.Verlauf.ToArray())).Select(x => (x.w, x.l, x.w.BreiteWirksam, Rahmen.Um(x.l))).ToList();
            // Mauerlinien, geschlossene mit dem ersten Punkt hinten dran
            _mauern = e.Mauern.Where(m => m.Linie.Count > 0)
                .Select(m => (m, m.Linie.Select(Kartenort.Aus).Concat(m.Geschlossen ? new[] { Kartenort.Aus(m.Linie[0]) } : Array.Empty<Kartenort>()).ToArray()))
                .Select(x => (x.m, x.Item2, Rahmen.Um(x.Item2))).ToList();
            _zonen = e.Bauzonen.Where(z => z.Ecken.Count > 0).Select(z => (z, z.Ecken.ToArray(), Rahmen.Um(z.Ecken))).ToList();
            // Die Stadt: innerhalb der ersten geschlossenen Mauer (Greybox: ±50 m)
            if (e.Mauern.FirstOrDefault(m => m.Geschlossen && m.Linie.Count >= 3) is { } stadtmauer)
                _stadtgebiet = stadtmauer.Linie.Select(Kartenort.Aus).ToArray();
        }

        private IEnumerable<(EntwurfWeg Weg, Kartenort[] Linie, double Breite, Rahmen Rahmen)> Karrenwege => _wege.Where(w => w.Weg.IstKarrenstrasse);

        /// <summary>Die Rasterfelder jeder Bauzone (einmal je Lauf).</summary>
        private Dictionary<EntwurfBauzone, HashSet<(int, int)>> Felder => _felder ??= _zonen.ToDictionary(z => z.Zone, z => FelderVon(z.Flaeche));

        private IReadOnlyList<Kartenort> Ecken(EntwurfGebaeude g) => Flaeche(g).Ecken;

        /// <summary>Ecken und Rahmen eines Gebäudes (einmal je Lauf).</summary>
        private (IReadOnlyList<Kartenort> Ecken, Rahmen Rahmen) Flaeche(EntwurfGebaeude g)
        {
            if (_ecken.TryGetValue(g, out var f)) return f;
            var e = g.Ecken();
            return _ecken[g] = (e, e.Count > 0 ? Rahmen.Um(e) : default);
        }

        /// <summary>Der kürzeste Abstand von p zu einer Linie unter den Wegen – Wege, deren Rahmen schon weiter weg ist als der beste, rechnet er nicht genau.</summary>
        private static double NaechsterWeg(Kartenort p, IEnumerable<(EntwurfWeg Weg, Kartenort[] Linie, double Breite, Rahmen Rahmen)> wege)
        {
            var best = double.MaxValue;
            var punkt = new Rahmen(p.X, p.Z, p.X, p.Z);
            foreach (var w in wege)
                if (punkt.Abstand(w.Rahmen) < best)
                    best = Math.Min(best, Kartengeometrie.AbstandLinie(p, w.Linie));
            return best;
        }

        private bool InDerStadt(Kartenort p) => _stadtgebiet is not null && Kartengeometrie.ImVieleck(p, _stadtgebiet);

        private void Fehler(string regel, string text, Kartenort? ort, params string[] gebaeude) => _b.Add(new Befund(Schwere.Fehler, regel, text, gebaeude, ort));
        private void Hinweis(string regel, string text, Kartenort? ort, params string[] gebaeude) => _b.Add(new Befund(Schwere.Hinweis, regel, text, gebaeude, ort));

        /// <summary>Ein Fehler an Wegen, Flächen oder Pflanzen (die Darstellung zeichnet sie rot), dazu betroffene Gebäude.</summary>
        private void FehlerAn(string regel, string text, Kartenort? ort, IReadOnlyList<EntwurfTeil> teile, params string[] gebaeude) =>
            _b.Add(new Befund(Schwere.Fehler, regel, text, gebaeude, ort, teile));

        private void HinweisAn(string regel, string text, Kartenort? ort, IReadOnlyList<EntwurfTeil> teile, params string[] gebaeude) =>
            _b.Add(new Befund(Schwere.Hinweis, regel, text, gebaeude, ort, teile));

        private static string Wer(EntwurfGebaeude g) => g.Name == g.Kennung || g.Name == "" ? g.Kennung : $"{g.Kennung} ({g.Name})";
        private static string M(double x) => Kartenschreiber.Zahl(Math.Round(x, 1)).Replace('.', ',');

        public List<Befund> Alles()
        {
            var gebaeude = _e.Gebaeude;
            foreach (var g in gebaeude) Einzeln(g);
            for (var i = 0; i < gebaeude.Count; i++)
                for (var j = i + 1; j < gebaeude.Count; j++)
                    Paar(gebaeude[i], gebaeude[j]);
            foreach (var doppelt in gebaeude.GroupBy(g => g.Kennung).Where(x => x.Count() > 1))
                Fehler("kennung", $"Die Kennung „{doppelt.Key}“ gibt es {doppelt.Count()}-mal.", doppelt.First().Punkt, doppelt.Key);
            foreach (var doppelt in gebaeude.Where(g => g.Betrieb is not null).GroupBy(g => g.Betrieb).Where(x => x.Count() > 1))
                Fehler("betrieb", $"Der Betrieb „{doppelt.Key}“ steht an mehreren Plätzen ({string.Join(", ", doppelt.Select(g => g.Kennung))}).",
                    doppelt.First().Punkt, doppelt.Select(g => g.Kennung).ToArray());

            Wege();
            ZonenFormen();
            Bauzonen();
            if (_r.Stadtregeln) Probeplan();
            if (_r.Wohnhausplaetze > 0) WohnhausplaetzeZaehlen();
            if (_r.Startgrundstuecke > 0) Startgrundstuecke();
            Vorkommen();
            Mauern();
            Haefen();
            Natur();
            Pflanzen();
            if (_e.Start is { } s) Start(s);
            Wegenetz();
            return _b;
        }

        public List<Befund> Stelle(EntwurfGebaeude probe, EntwurfGebaeude? alt)
        {
            Einzeln(probe);
            foreach (var g in _e.Gebaeude)
                if (!ReferenceEquals(g, alt)) Paar(probe, g);
            if (probe.Betrieb is { } betrieb && _e.Gebaeude.FirstOrDefault(g => g.Betrieb == betrieb && !ReferenceEquals(g, alt)) is { } andere)
                Fehler("betrieb", $"Der Betrieb „{betrieb}“ steht schon auf {andere.Kennung}.", probe.Punkt, probe.Kennung, andere.Kennung);
            return _b;
        }

        // ── Ein Gebäude für sich ─────────────────────────────────────────────

        private void Einzeln(EntwurfGebaeude g)
        {
            var b = g.Baustein;
            var ort = g.Punkt;
            if (b is null)
            {
                Fehler("katalog", $"{Wer(g)}: Den Baustein „{g.Katalog}“ gibt es im Katalog nicht.", ort, g.Kennung);
                return;
            }
            if (!Bausteinart.ImGebaeude.Contains(b.Art))
                Fehler("katalog", $"{Wer(g)}: Ein Baustein der Art „{b.Art}“ ({g.Katalog}) steht nicht unter den Gebäuden.", ort, g.Kennung);
            if (b.Groesse is null) Fehler("katalog", $"{Wer(g)}: Der Baustein „{g.Katalog}“ hat keine Größe.", ort, g.Kennung);
            if (string.IsNullOrWhiteSpace(g.Kennung)) Fehler("kennung", $"Ein Gebäude ({g.Katalog}) hat keine Kennung.", ort, g.Kennung);
            if (g.Massstab <= 0) Fehler("massstab", $"{Wer(g)}: Der Maßstab muss größer als 0 sein.", ort, g.Kennung);
            if (b.Art == Bausteinart.Wohnhausplatz && !g.IstRechtwinklig)
                Fehler("rechtwinklig", $"{Wer(g)}: Ein Wohnhausplatz steht rechtwinklig (Drehung 0, 90, 180 oder 270, nicht {M(g.Drehung)}).", ort, g.Kennung);
            if (b.Art == Bausteinart.Umlandbetrieb && string.IsNullOrWhiteSpace(g.Betrieb))
                Fehler("betrieb", $"{Wer(g)}: Ein Umlandbetrieb braucht den Namen seines Betriebs.", ort, g.Kennung);
            if (g.Groesse is null || g.Massstab <= 0) return;

            if (b.Art == Bausteinart.Werkstattplatz) Werkstatthof(g);
            if (!_r.Stadtregeln) return;

            if (b.Art == Bausteinart.Wohnhausplatz) WohnhausEingang(g);
            if (b.Art == Bausteinart.Werkstattplatz) Werkstatt(g);
            if (InDerStadt(g.Punkt))
            {
                if (g.Katalog != "markt") GassenUeberHaus(g);
            }
            else if (b.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle) WegeUeberUmland(g);
            ZonenAbstand(g);
        }

        /// <summary>Der Hof eines Werkstattplatzes liegt höchstens 7 m von der Mitte eines Weges (karte.py, Regel 3).</summary>
        private void Werkstatthof(EntwurfGebaeude g)
        {
            var hof = g.Hof!.Value;
            if (_wege.Count == 0)
            {
                Fehler("hof", $"{Wer(g)}: Der Hof braucht einen Weg (höchstens {M(Strassennah)} m), die Karte hat noch keinen.", hof, g.Kennung);
                return;
            }
            var n = NaechsterWeg(hof, _wege);
            if (n > Strassennah)
                Fehler("hof", $"{Wer(g)}: Der Hof {hof} liegt {M(n)} m vom nächsten Weg (höchstens {M(Strassennah)} m).", hof, g.Kennung);
        }

        /// <summary>Der Eingang eines Wohnhausplatzes liegt am Weg: höchstens 1,5 m vom Rand (greybox.py, Regel 4).</summary>
        private void WohnhausEingang(EntwurfGebaeude g)
        {
            var eingang = g.Eingang!.Value;
            if (_wege.Count == 0 || _wege.Min(w => Kartengeometrie.AbstandLinie(eingang, w.Linie) - w.Breite / 2) > 1.5)
                Fehler("eingang", $"{Wer(g)}: Der Eingang liegt nicht an einem Weg (höchstens 1,5 m vom Wegrand).", eingang, g.Kennung);
        }

        /// <summary>Werkstattplatz: Greybox-Maße, eigene Gasse am Hof, nicht auf der Mauer (greybox.py, Regel 5).</summary>
        private void Werkstatt(EntwurfGebaeude g)
        {
            if (_r.Werkstattnorm)
            {
                var (w, t) = g.Groesse!.Value;
                if (w != 16 || t != 14 || g.Drehung != 0)
                    Fehler("werkstattnorm", $"{Wer(g)}: {M(w)} × {M(t)} m mit Drehung {M(g.Drehung)} – in dieser Karte ist jede Werkstatt 16 × 14 m mit der Tür nach Süden.",
                        g.Punkt, g.Kennung);
                var hof = g.Hof!.Value;
                if (!_wege.Any(x => x.Weg.Klasse == Wegklasse.Gasse && x.Linie[0].Abstand(hof) < 1e-3))
                    Fehler("gasse", $"{Wer(g)}: Keine Gasse beginnt am Hof {hof}.", hof, g.Kennung);
            }
            var haus = g.Ecken(ohneVorplatz: true);
            foreach (var (m, linie, _) in _mauern)
                if (Kartengeometrie.AbstandVielecke(haus, linie, false) < 1)
                    Fehler("mauer", $"{Wer(g)}: Die Werkstatt steht auf der Mauer „{m.Name}“.", g.Punkt, g.Kennung);
        }

        /// <summary>Keine Gasse läuft über ein Haus der Stadt, keine Straße über eine Werkstatt (greybox.py, Regel 6).</summary>
        private void GassenUeberHaus(EntwurfGebaeude g)
        {
            var haus = g.Ecken(ohneVorplatz: true);
            var rahmen = Rahmen.Um(haus);
            var istWerkstatt = g.Art == Bausteinart.Werkstattplatz;
            foreach (var (w, linie, breite, wr) in _wege)
            {
                var gasse = w.Klasse == Wegklasse.Gasse;
                if (!gasse && !istWerkstatt || rahmen.Abstand(wr) >= breite / 2) continue;
                // Eine Gasse, die an der Tür dieses Hauses beginnt, gehört zu ihm.
                if (gasse && Kartengeometrie.AbstandVielecke(haus, new[] { linie[0] }, false) < 3) continue;
                var a = Kartengeometrie.AbstandVielecke(haus, linie, false);
                if (a < breite / 2)
                    FehlerAn("weg-ueber-haus", $"„{w.Name}“ läuft über {Wer(g)}: {M(a)} m zur Mitte, {M(breite)} m breit.", g.Punkt, new[] { w }, g.Kennung);
            }
        }

        /// <summary>
        /// Umlandbetrieb oder -stelle: Der eigene Weg („Weg zu …“, „Fußweg …“) kommt von vorn an den Halteplatz (Fehler); fremde Wege über
        /// die Fläche sind nur ein Hinweis – so zeichnet die Stadtansicht sie seit jeher (greybox.py, Regel 7).
        /// </summary>
        private void WegeUeberUmland(EntwurfGebaeude g)
        {
            var haus = g.Ecken(ohneVorplatz: true);
            var flaeche = Ecken(g);
            var rahmen = Rahmen.Um(flaeche);
            foreach (var (w, linie, breite, wr) in _wege)
            {
                if (rahmen.Abstand(wr) >= breite / 2) continue;
                if (w.Name == $"Weg zu {g.Name}" || w.Name == $"Fußweg {g.Name}")
                {
                    if (linie.Take(linie.Length - 1).Any(p => Kartengeometrie.ImVieleck(p, flaeche)))
                        FehlerAn("umlandweg", $"„{w.Name}“ kommt nicht von vorn an den Halteplatz von {Wer(g)}.", linie[^1], new[] { w }, g.Kennung);
                    continue;
                }
                if (Kartengeometrie.AbstandVielecke(haus, linie, false) < breite / 2)
                    HinweisAn("weg-ueber-umland", $"„{w.Name}“ läuft über {Wer(g)}.", g.Punkt, new[] { w }, g.Kennung);
            }
        }

        /// <summary>Jede Bauzone hält 2 m Abstand zu jedem Gebäude (greybox.py, Regel 1).</summary>
        private void ZonenAbstand(EntwurfGebaeude g)
        {
            var ecken = Ecken(g);
            var rahmen = Rahmen.Um(ecken);
            foreach (var (z, flaeche, zr) in _zonen)
            {
                if (rahmen.Abstand(zr) >= AbstandFeste) continue;
                var a = Kartengeometrie.AbstandVielecke(flaeche, ecken);
                if (a < AbstandFeste)
                    FehlerAn("zone-gebaeude", $"Bauzone {z.Kennung}: nur {M(a)} m zu {Wer(g)}, mindestens {M(AbstandFeste)} m.", g.Punkt, new[] { z }, g.Kennung);
            }
        }

        // ── Zwei Gebäude ─────────────────────────────────────────────────────

        private void Paar(EntwurfGebaeude a, EntwurfGebaeude b)
        {
            var ((pa, ra), (pb, rb)) = (Flaeche(a), Flaeche(b));
            if (pa.Count == 0 || pb.Count == 0) return;
            var beideWerkstatt = _r.Stadtregeln && a.Art == Bausteinart.Werkstattplatz && b.Art == Bausteinart.Werkstattplatz;
            // Weit auseinander? Dann gibt es weder Überlappung noch zu wenig Abstand (spart die genaue Rechnung bei den meisten Paaren).
            if (ra.Abstand(rb) >= (beideWerkstatt ? AbstandWerkstatt : 0.01)) return;

            // Ein Felsberg „Berg_X“ läuft hinter dem Felshang seiner Grube X aus und überdeckt deren Rückseite.
            if ("Berg_" + a.Kennung != b.Kennung && "Berg_" + b.Kennung != a.Kennung && Kartengeometrie.Ueberlappen(pa, pb))
            {
                var ort = new Kartenort((a.X + b.X) / 2, (a.Z + b.Z) / 2);
                if (a.Art == Bausteinart.Berg || b.Art == Bausteinart.Berg)
                    Hinweis("ueberlappung", $"{Wer(a)} und {Wer(b)} überlappen (Rand des Felsbergs).", ort, a.Kennung, b.Kennung);
                else
                    Fehler("ueberlappung", $"{Wer(a)} und {Wer(b)} überlappen.", ort, a.Kennung, b.Kennung);
            }
            if (beideWerkstatt && Kartengeometrie.AbstandVielecke(pa, pb) < AbstandWerkstatt)
                Fehler("werkstattabstand", $"{Wer(a)} und {Wer(b)}: weniger als {M(AbstandWerkstatt)} m Abstand zwischen den Werkstätten.",
                    new Kartenort((a.X + b.X) / 2, (a.Z + b.Z) / 2), a.Kennung, b.Kennung);
        }

        // ── Die Karte als Ganzes ─────────────────────────────────────────────

        /// <summary>Bauzonen: an einer Karrenstraße (karte.py, Regel 4); mit den Stadtregeln auf dem Raster, mit Abstand zu Wegen und Mauer, ohne Überlappung.</summary>
        private void Bauzonen()
        {
            var karren = Karrenwege.ToList();
            foreach (var (z, flaeche, zr) in _zonen)
            {
                var mitte = Kartengeometrie.Mitte(flaeche);
                if (karren.Count == 0)
                {
                    FehlerAn("zone-anschluss", $"Bauzone {z.Kennung}: Die Karte hat keine Karrenstraße.", mitte, new[] { z });
                    continue;
                }
                var n = flaeche.Min(p => NaechsterWeg(p, karren));
                if (n > Anschluss)
                    FehlerAn("zone-anschluss", $"Bauzone {z.Kennung}: Die nächste Karrenstraße ist {M(n)} m entfernt (höchstens {M(Anschluss)} m).", mitte, new[] { z });
                if (!_r.Stadtregeln) continue;

                foreach (var p in flaeche.Where(p => Kartengeometrie.Mod(p.X, _e.Raster) != 0 || Kartengeometrie.Mod(p.Z, _e.Raster) != 0))
                    FehlerAn("zone-raster", $"Bauzone {z.Kennung}: Die Ecke {p} liegt nicht auf dem {M(_e.Raster)}-m-Raster.", p, new[] { z });
                foreach (var (w, linie, breite, wr) in _wege)
                {
                    if (zr.Abstand(wr) >= breite / 2 + AbstandStrasse) continue;
                    var a = Kartengeometrie.AbstandVielecke(flaeche, linie, false);
                    if (a < breite / 2 + AbstandStrasse)
                        FehlerAn("zone-weg", $"Bauzone {z.Kennung}: nur {M(a)} m zur Mitte von „{w.Name}“ (Weg {M(breite)} m breit, mindestens {M(breite / 2 + AbstandStrasse)} m).", mitte, new EntwurfTeil[] { z, w });
                }
                foreach (var (m, linie, mr) in _mauern)
                {
                    if (zr.Abstand(mr) >= AbstandMauer) continue;
                    var a = Kartengeometrie.AbstandVielecke(flaeche, linie, false);
                    if (a < AbstandMauer) FehlerAn("zone-mauer", $"Bauzone {z.Kennung}: nur {M(a)} m zur Mauer „{m.Name}“, mindestens {M(AbstandMauer)} m.", mitte, new[] { z });
                }
                // nächster Straßenrand: Wege, deren Rahmen schon weiter weg ist als der beste bisher, braucht es nicht genau
                var rand = double.MaxValue;
                foreach (var w in karren.OrderBy(w => zr.Abstand(w.Rahmen) - w.Breite / 2))
                    if (zr.Abstand(w.Rahmen) - w.Breite / 2 < rand)
                        rand = Math.Min(rand, Kartengeometrie.AbstandVielecke(flaeche, w.Linie, false) - w.Breite / 2);
                if (rand > Anschluss)
                    FehlerAn("zone-anschluss", $"Bauzone {z.Kennung}: Die nächste Karrenstraße ist {M(rand)} m vom Straßenrand entfernt (höchstens {M(Anschluss)} m).", mitte, new[] { z });
            }
            if (!_r.Stadtregeln) return;
            var felder = _zonen.Select(z => (z.Zone, Felder: Felder[z.Zone])).ToList();
            for (var i = 0; i < felder.Count; i++)
                for (var j = i + 1; j < felder.Count; j++)
                    if (felder[i].Felder.Overlaps(felder[j].Felder))
                        FehlerAn("zone-ueberlappung", $"Die Bauzonen {felder[i].Zone.Kennung} und {felder[j].Zone.Kennung} überlappen.",
                            Kartengeometrie.Mitte(_zonen[i].Flaeche), new[] { felder[i].Zone, felder[j].Zone });
        }

        /// <summary>Die Rasterfelder, deren Mitte in der Fläche liegt.</summary>
        private HashSet<(int, int)> FelderVon(IReadOnlyList<Kartenort> poly)
        {
            var r = _e.Raster;
            var felder = new HashSet<(int, int)>();
            for (var i = (int)Math.Floor(poly.Min(p => p.X) / r); i <= (int)Math.Floor(poly.Max(p => p.X) / r); i++)
                for (var j = (int)Math.Floor(poly.Min(p => p.Z) / r); j <= (int)Math.Floor(poly.Max(p => p.Z) / r); j++)
                    if (Kartengeometrie.ImVieleck(new Kartenort((i + 0.5) * r, (j + 0.5) * r), poly))
                        felder.Add((i, j));
            return felder;
        }

        /// <summary>
        /// Der Probeplan: Jedes Gebäude liegt ganz in seiner Zone, überlappt kein anderes, ein Stadthof nur in Kern oder Mauerring, und die
        /// Vorplatzreihe hat mindestens 2 Felder höchstens 7 m von einer passenden Straße (Eingangsregel des Kerns; greybox.py, Regel 3).
        /// </summary>
        private void Probeplan()
        {
            var r = _e.Raster;
            var zonen = _zonen.GroupBy(z => z.Zone.Kennung).ToDictionary(z => z.Key, z => (z.First().Zone, Felder: Felder[z.First().Zone]));
            var belegt = new Dictionary<(int, int), string>();
            foreach (var p in _e.Probeplan)
            {
                var (wx, wz) = Kartengeometrie.Mod(p.Drehung, 180) == 0 ? (p.Breite, p.Tiefe) : (p.Tiefe, p.Breite);
                var (x0, z0, x1, z1) = (p.Punkt.X - wx / 2, p.Punkt.Z - wz / 2, p.Punkt.X + wx / 2, p.Punkt.Z + wz / 2);
                var wer = $"Probeplan {p.Art} in {p.Zone} ({M(x0)}, {M(z0)})";
                if (!zonen.TryGetValue(p.Zone, out var zone))
                {
                    Fehler("probeplan", $"Probeplan {p.Art}: Die Zone {p.Zone} gibt es nicht.", p.Punkt);
                    continue;
                }
                var zellen = new List<(int, int)>();
                for (var i = (int)Math.Floor(x0 / r); i < (int)Math.Floor(x1 / r); i++)
                    for (var j = (int)Math.Floor(z0 / r); j < (int)Math.Floor(z1 / r); j++)
                        zellen.Add((i, j));
                if (!zellen.All(zone.Felder.Contains)) Fehler("probeplan", $"{wer} liegt nicht ganz in der Zone.", p.Punkt);
                foreach (var zelle in zellen)
                {
                    if (belegt.TryGetValue(zelle, out var anderer))
                    {
                        Fehler("probeplan", $"{wer} überlappt {anderer}.", p.Punkt);
                        break;
                    }
                    belegt[zelle] = $"{p.Art} in {p.Zone} ({M(x0)}, {M(z0)})";
                }
                if (p.Art == "Stadthof" && zone.Zone.Art is not (Lage.Kern or Lage.Mauerring))
                    Fehler("probeplan", $"{wer}: Ein Stadthof steht nur in Kern oder Mauerring (Zone ist {zone.Zone.Art}).", p.Punkt);

                var reihe = Vorplatzreihe(x0, z0, x1, z1, p.Drehung);
                if (reihe is null)
                {
                    Fehler("probeplan", $"{wer}: Drehung {M(p.Drehung)} ist nicht rechtwinklig.", p.Punkt);
                    continue;
                }
                var wege = (p.Art == "Wohnhaus" ? _wege : Karrenwege).ToList();
                var nah = reihe.Count(c => NaechsterWeg(c, wege) <= Strassennah);
                if (nah < 2)
                    Fehler("probeplan", $"{wer}: nur {nah} Felder der Vorplatzreihe höchstens {M(Strassennah)} m von der Straßenmitte (mindestens 2).", p.Punkt);
            }
            int Zahl(string art) => _e.Probeplan.Count(p => p.Art == art);
            if (Zahl("Stadthof") < _r.MindestStadthoefe)
                Fehler("probeplan", $"Nur {Zahl("Stadthof")} Stadthof-Plätze im Probeplan (verlangt {_r.MindestStadthoefe}).", null);
            if (Zahl("Werkstatt") + Zahl("Stadthof") < _r.MindestWerkstaetten)
                Fehler("probeplan", $"Nur {Zahl("Werkstatt") + Zahl("Stadthof")} Plätze zu 16 × 14 m im Probeplan (verlangt {_r.MindestWerkstaetten}).", null);
            if (Zahl("Wohnhaus") < _r.MindestWohnhaeuser)
                Fehler("probeplan", $"Nur {Zahl("Wohnhaus")} Wohnhaus-Plätze im Probeplan (verlangt {_r.MindestWohnhaeuser}).", null);
        }

        /// <summary>Mittelpunkte der Rasterfelder in der Reihe an der Vorderseite (eine Feldtiefe = 2 m Vorplatz); null bei schräger Drehung.</summary>
        private List<Kartenort>? Vorplatzreihe(double x0, double z0, double x1, double z1, double drehung)
        {
            var r = _e.Raster;
            var xs = Enumerable.Range(0, (int)Math.Floor((x1 - x0) / r)).Select(i => x0 + r * (i + 0.5));
            var zs = Enumerable.Range(0, (int)Math.Floor((z1 - z0) / r)).Select(j => z0 + r * (j + 0.5));
            return Kartengeometrie.Mod(drehung, 360) switch
            {
                0 => xs.Select(x => new Kartenort(x, z1 - r / 2)).ToList(),
                180 => xs.Select(x => new Kartenort(x, z0 + r / 2)).ToList(),
                90 => zs.Select(z => new Kartenort(x1 - r / 2, z)).ToList(),
                270 => zs.Select(z => new Kartenort(x0 + r / 2, z)).ToList(),
                _ => null,
            };
        }

        /// <summary>So viele Wohnhausplätze wie Häuser, die ersten vier gleich weit vom Markt (±10 %; greybox.py, Regel 4).</summary>
        private void WohnhausplaetzeZaehlen()
        {
            var plaetze = _e.Gebaeude.Where(g => g.Art == Bausteinart.Wohnhausplatz).ToList();
            if (plaetze.Count != _r.Wohnhausplaetze)
                Fehler("wohnhausplaetze", $"Die Karte hat {plaetze.Count} Wohnhausplätze statt {_r.Wohnhausplaetze}.", null);
            var vorn = plaetze.Take(4).ToList();
            if (vorn.Count == 0) return;
            var markt = new Kartenort(0, 0);
            var mittel = vorn.Average(g => g.Punkt.Abstand(markt));
            foreach (var g in vorn.Where(g => Math.Abs(g.Punkt.Abstand(markt) - mittel) > 0.10 * mittel))
                Fehler("wohnhausplaetze", $"{Wer(g)}: {M(g.Punkt.Abstand(markt))} m zum Markt, die ersten vier im Mittel {M(mittel)} m (±10 %).", g.Punkt, g.Kennung);
        }

        /// <summary>Köln: genau n Startgrundstücke (Werkstatt- und Wohnhausplatz „S&lt;n&gt;_…“), gleich weit vom Markt (±10 %; karte.py, Regel 5).</summary>
        private void Startgrundstuecke()
        {
            var starts = _e.Gebaeude.Select(g => (g, m: Regex.Match(g.Kennung, @"^(S\d+)_(Werkstatt|Wohnhaus)$"))).Where(x => x.m.Success)
                .GroupBy(x => x.m.Groups[1].Value, x => x.g).ToList();
            if (starts.Count != _r.Startgrundstuecke)
                Fehler("startgrundstuecke", $"Die Karte hat {starts.Count} Startgrundstücke statt {_r.Startgrundstuecke}.", null);
            if (starts.Count == 0) return;
            var markt = new Kartenort(0, 0);
            var abstand = starts.ToDictionary(s => s.Key, s => new Kartenort(s.Average(g => g.X), s.Average(g => g.Z)).Abstand(markt));
            var mittel = abstand.Values.Average();
            foreach (var s in starts.Where(s => Math.Abs(abstand[s.Key] - mittel) > 0.10 * mittel))
                Fehler("startgrundstuecke", $"Startgrundstück {s.Key}: {M(abstand[s.Key])} m zum Markt, im Mittel {M(mittel)} m (±10 %).",
                    s.First().Punkt, s.Select(g => g.Kennung).ToArray());
        }

        /// <summary>Jedes Vorkommen liegt an einem Weg oder in der Fläche eines Umlandbetriebs (karte.py, Regel 6).</summary>
        private void Vorkommen()
        {
            var umland = _e.Gebaeude.Where(g => g.Art == Bausteinart.Umlandbetrieb).Select(Ecken).Where(e => e.Count > 0).ToList();
            foreach (var v in _e.Vorkommen)
            {
                var n = NaechsterWeg(v.Punkt, _wege);
                if (n > 2 && !umland.Any(e => Kartengeometrie.ImVieleck(v.Punkt, e)))
                    Fehler("vorkommen", $"Vorkommen {v.Kennung}: {(n == double.MaxValue ? "kein Weg in der Karte" : M(n) + " m vom nächsten Weg")}.", v.Punkt);
            }
        }

        /// <summary>
        /// Mauern: Baukasten im Katalog, die Stadtmauer geschlossen bzw. offen, jedes Tor auf seiner Linie, Wege kreuzen nur an einem Tor
        /// (karte.py, Regel 7).
        /// </summary>
        private void Mauern()
        {
            for (var i = 0; i < _e.Mauern.Count; i++)
            {
                var m = _e.Mauern[i];
                var ort = m.Linie.Count > 0 ? Kartenort.Aus(m.Linie[0]) : (Kartenort?)null;
                if (m.Linie.Count < 2) Fehler("mauer", $"Mauer „{m.Name}“: Die Linie braucht mindestens zwei Punkte.", ort);
                if (_e.Katalog.Finde(m.Katalog) is not { Art: Bausteinart.Mauer })
                    Fehler("mauer", $"Mauer „{m.Name}“: Den Mauer-Baukasten „{m.Katalog}“ gibt es im Katalog nicht.", ort);
                if (i == 0 && _r.MauerGeschlossen is { } soll && m.Geschlossen != soll)
                    Fehler("mauer", $"Mauer „{m.Name}“: In dieser Karte ist die Stadtmauer {(soll ? "geschlossen" : "offen")}.", ort);
            }
            foreach (var (m, linie, _) in _mauern)
            {
                var tore = m.Tore.Where(t => t.Punkt is { Length: 2 }).Select(t => (t, Ort: Kartenort.Aus(t.Punkt))).ToList();
                foreach (var (t, ort) in tore)
                {
                    var n = Kartengeometrie.AbstandLinie(ort, linie);
                    if (n > Tornah) Fehler("tor", $"Tor „{t.Name}“: {M(n)} m neben der Linie der Mauer „{m.Name}“ (höchstens {M(Tornah)} m).", ort);
                }
                foreach (var (w, wl, _, _) in _wege)
                    for (var a = 0; a + 1 < wl.Length; a++)
                        for (var c = 0; c + 1 < linie.Length; c++)
                            if (Kartengeometrie.Schnitt(wl[a], wl[a + 1], linie[c], linie[c + 1]) is { } s
                                && (tore.Count == 0 ? double.MaxValue : tore.Min(t => s.Abstand(t.Ort))) > 3)
                                FehlerAn("tor", $"„{w.Name}“ kreuzt die Mauer „{m.Name}“ ohne Tor bei {s}.", s, new[] { w });
            }
        }

        /// <summary>Anleger und Kran liegen an der Kaimauer (karte.py, Regel 8).</summary>
        private void Haefen()
        {
            foreach (var h in _e.Haefen)
            {
                var kante = h.Kante.Select(Kartenort.Aus).ToArray();
                if (kante.Length == 0) continue;
                foreach (var p in h.Anleger.Concat(h.Kran is null ? Array.Empty<double[]>() : new[] { h.Kran }).Select(Kartenort.Aus))
                    if (Kartengeometrie.AbstandLinie(p, kante) > 2)
                        Fehler("hafen", $"{h.Name}: Anleger oder Kran bei {p} liegt nicht an der Kaimauer.", p);
            }
        }

        /// <summary>
        /// Natur: Flächen mit mindestens drei Ecken, Dichte und Gruppen, jede Art ein Natur-Baustein des Katalogs (karte.py, Regel 9); Umriss
        /// und Aussparungen kreuzen sich nicht selbst; einzelne Pflanzen aus dem Katalog.
        /// </summary>
        private void Natur()
        {
            bool IstNatur(string k) => _e.Katalog.Finde(k) is { Art: Bausteinart.Natur };
            foreach (var n in _e.Naturflaechen)
            {
                var ort = n.Ecken.Count > 0 ? Kartengeometrie.Mitte(n.Ecken) : (Kartenort?)null;
                var teil = new[] { n };
                if (n.Ecken.Count < 3) FehlerAn("natur", $"Naturfläche „{n.Name}“: Die Fläche braucht mindestens drei Punkte.", ort, teil);
                if (n.Dichte <= 0 || n.Gruppen.Count == 0) FehlerAn("natur", $"Naturfläche „{n.Name}“: Dichte oder Gruppen fehlen.", ort, teil);
                foreach (var t in n.Gruppen.SelectMany(g => g.Teile))
                {
                    if (t.Anzahl is not { Length: 2 } a || a[0] < 0 || a[1] < a[0])
                        FehlerAn("natur", $"Naturfläche „{n.Name}“: anzahl ist [von, bis] mit 0 ≤ von ≤ bis.", ort, teil);
                    foreach (var art in t.Arten.Keys.Where(k => !IstNatur(k)))
                        FehlerAn("natur", $"Naturfläche „{n.Name}“: „{art}“ ist keine Natur-Art im Katalog.", ort, teil);
                }
                SelbstSchnitt($"Naturfläche „{n.Name}“", n.Ecken, n);
                for (var i = 0; i < n.Aussparungen.Count; i++)
                {
                    if (n.Aussparungen[i].Count < 3) FehlerAn("natur", $"Naturfläche „{n.Name}“: Aussparung {i + 1} braucht mindestens drei Punkte.", ort, teil);
                    SelbstSchnitt($"Naturfläche „{n.Name}“, Aussparung {i + 1}", n.Aussparungen[i], n);
                }
            }
            foreach (var p in _e.Pflanzen.Where(p => !IstNatur(p.Katalog)))
                FehlerAn("natur", $"Pflanze bei {p.Punkt}: „{p.Katalog}“ ist keine Natur-Art im Katalog.", p.Punkt, new[] { p });
        }

        /// <summary>
        /// Ein Vieleck kreuzt sich selbst (zwei Kanten, die nicht benachbart sind, schneiden sich) – ein Hinweis mit dem Schnittpunkt: Der Kern
        /// rechnet mit solchen Flächen weiter (was „drin“ ist, entscheidet das Strahlverfahren), gemeint ist es aber selten so. Köln hat eine
        /// solche Bauzone (M5) aus dem Werkzeug.
        /// </summary>
        private void SelbstSchnitt(string wer, IReadOnlyList<Kartenort> poly, EntwurfTeil teil)
        {
            var n = poly.Count;
            if (n < 4) return;
            for (var i = 0; i < n; i++)
                for (var j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue; // die letzte Kante grenzt an die erste
                    if (Kartengeometrie.Schnitt(poly[i], poly[(i + 1) % n], poly[j], poly[(j + 1) % n]) is { } s)
                    {
                        HinweisAn("flaeche-kreuzt", $"{wer}: Der Umriss kreuzt sich selbst bei {s}.", s, new[] { teil });
                        return;
                    }
                }
        }

        /// <summary>
        /// Wege: mindestens zwei Punkte, nicht schmaler als die Mindestbreite der Klasse; eine glatte Kurve läuft über kein Haus (außer dem, an
        /// dessen Tür oder Hof sie beginnt oder endet). Gerade Wege prüfen die Regeln oben (Gassen über Häusern, Wege über Umlandbetrieben).
        /// </summary>
        private void Wege()
        {
            foreach (var w in _e.Wege)
            {
                var ort = w.Punkte.Count > 0 ? w.Punkte[0] : (Kartenort?)null;
                if (w.Punkte.Count < 2)
                    FehlerAn("weg-punkte", $"„{w.Name}“: Ein Weg braucht mindestens zwei Punkte.", ort, new[] { w });
                if (w.Breite > 0 && w.Breite < w.Mindestbreite - 1e-9)
                    FehlerAn("weg-breite", $"„{w.Name}“: {M(w.Breite)} m breit, eine {Kartenweg.Anzeigename(w.Klasse)} hat mindestens {M(w.Mindestbreite)} m.", ort, new[] { w });
            }
            foreach (var (w, linie, breite, wr) in _wege.Where(x => x.Weg.IstGlatt && x.Linie.Length >= 2))
                foreach (var g in _e.Gebaeude)
                {
                    var haus = g.Ecken(ohneVorplatz: true);
                    if (haus.Count == 0 || g.Art == Bausteinart.Sonderbau && g.Katalog == "markt") continue;
                    if (Rahmen.Um(haus).Abstand(wr) >= breite / 2) continue;
                    // Ein Weg, der an diesem Haus beginnt oder endet (Tür, Hof, Halteplatz), gehört zu ihm.
                    if (Kartengeometrie.AbstandVielecke(Ecken(g), new[] { linie[0] }, false) < 3
                        || Kartengeometrie.AbstandVielecke(Ecken(g), new[] { linie[^1] }, false) < 3) continue;
                    var a = Kartengeometrie.AbstandVielecke(haus, linie, false);
                    if (a < breite / 2)
                        FehlerAn("kurve-ueber-haus", $"Die Kurve von „{w.Name}“ läuft über {Wer(g)}: {M(a)} m zur Mitte, {M(breite)} m breit.", g.Punkt, new[] { w }, g.Kennung);
                }
        }

        /// <summary>Bauzonen: Kennung eindeutig, mindestens drei Ecken, der Umriss kreuzt sich nicht selbst.</summary>
        private void ZonenFormen()
        {
            foreach (var z in _e.Bauzonen)
            {
                if (z.Ecken.Count < 3)
                    FehlerAn("zone-form", $"Bauzone {z.Kennung}: Die Fläche braucht mindestens drei Punkte.", z.Ecken.Count > 0 ? z.Ecken[0] : null, new[] { z });
                SelbstSchnitt($"Bauzone {z.Kennung}", z.Ecken, z);
            }
            foreach (var doppelt in _e.Bauzonen.GroupBy(z => z.Kennung).Where(x => x.Count() > 1))
                FehlerAn("zone-kennung", $"Die Bauzone „{doppelt.Key}“ gibt es {doppelt.Count()}-mal.", Kartengeometrie.Mitte(doppelt.First().Ecken), doppelt.ToArray());
        }

        /// <summary>Eine einzeln gesetzte Pflanze auf einem Gebäude oder einem Weg (Hinweis: Sie steht dort trotzdem).</summary>
        private void Pflanzen()
        {
            if (_e.Pflanzen.Count == 0) return;
            var gebaeude = _e.Gebaeude.Select(g => (g, h: Naturhindernis.Von(g.Baustein, g.X, g.Z, g.Drehung, g.Massstab))).Where(x => x.h is not null).ToList();
            foreach (var p in _e.Pflanzen)
            {
                var auf = gebaeude.FirstOrDefault(x => x.h!.Value.Enthaelt(p.X, p.Z, 0)).g;
                if (auf is not null)
                    HinweisAn("pflanze-frei", $"{p.Bezeichnung} bei {p.Punkt} steht auf {Wer(auf)}.", p.Punkt, new[] { p }, auf.Kennung);
                else if (_wege.FirstOrDefault(w => Kartengeometrie.AbstandLinie(p.Punkt, w.Linie) < w.Breite / 2 + PflanzeAmWeg) is { Weg: not null } weg)
                    HinweisAn("pflanze-frei", $"{p.Bezeichnung} bei {p.Punkt} steht auf „{weg.Weg.Name}“.", p.Punkt, new EntwurfTeil[] { p, weg.Weg });
            }
        }

        /// <summary>Die Startbelegung: Plätze gibt es und sie haben die richtige Art, nichts doppelt (karte.py, Regel 10, und Stadtkarte.Pruefen).</summary>
        private void Start(Startbelegung s)
        {
            var nachKennung = _e.Gebaeude.GroupBy(g => g.Kennung).ToDictionary(g => g.Key, g => g.First());
            EntwurfGebaeude? Platz(string? k) => k is null ? null : nachKennung.GetValueOrDefault(k);
            Kartenort? Ort(string? k) => Platz(k)?.Punkt;
            string[] Betroffen(string? k) => Platz(k) is { } g ? new[] { g.Kennung } : Array.Empty<string>();

            void Wohnhaus(string wer, string? kennung)
            {
                if (kennung is not null && Platz(kennung) is not { Art: Bausteinart.Wohnhausplatz })
                    Fehler("start", $"Start, {wer}: „{kennung}“ ist kein Wohnhausplatz der Karte.", Ort(kennung), Betroffen(kennung));
            }
            Wohnhaus("Spieler", s.Spieler.Wohnhaus);
            foreach (var (beruf, w) in s.Spieler.JeBeruf.Prepend(new("", s.Spieler.Werkstatt)))
                if (Platz(w.Platz) is not { Art: Bausteinart.Werkstattplatz })
                    Fehler("start", $"Start, Spieler{(beruf == "" ? "" : $" ({beruf})")}: „{w.Platz}“ ist kein Werkstattplatz der Karte.", Ort(w.Platz), Betroffen(w.Platz));

            foreach (var h in s.Haeuser)
            {
                if (string.IsNullOrWhiteSpace(h.Name)) Fehler("start", "Start: Ein Haus hat keinen Namen.", null);
                Wohnhaus(h.Name, h.Wohnhaus);
                if (h.Wappen.Fehler() is { } wf) Fehler("start", $"Start, {h.Name}: {wf}", null);
                if (h.Betriebe.Count == 0) Fehler("start", $"Start, {h.Name}: Ein Haus braucht mindestens einen Betrieb.", null);
                foreach (var b in h.Betriebe)
                {
                    var platz = Platz(b.Platz);
                    if (platz is not { Art: Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb })
                        Fehler("start", $"Start, {h.Name}: „{b.Platz}“ ist weder Werkstattplatz noch Umlandbetrieb der Karte.", Ort(b.Platz), Betroffen(b.Platz));
                    else if (string.IsNullOrWhiteSpace(platz.Betrieb))
                        Fehler("start", $"Start, {h.Name}: Am Platz „{b.Platz}“ fehlt der Name des Betriebs.", platz.Punkt, platz.Kennung);
                    else if (platz.Art == Bausteinart.Umlandbetrieb && platz.Baustein!.BetriebsartWert != b.Art)
                        Fehler("start", $"Start, {h.Name}: Auf „{b.Platz}“ steht {platz.Baustein.Anzeigename}, kein Platz für {b.Art}.", platz.Punkt, platz.Kennung);
                }
            }
            foreach (var doppelt in s.Haeuser.GroupBy(h => h.Name).Where(x => x.Count() > 1))
                Fehler("start", $"Start: Das Haus „{doppelt.Key}“ steht doppelt.", null);
            foreach (var doppelt in s.Haeuser.GroupBy(h => h.Wappen).Where(x => x.Count() > 1))
                Fehler("start", $"Start: Zwei Häuser führen dasselbe Wappen ({string.Join(", ", doppelt.Select(h => h.Name))}).", null);
            var spielerPlaetze = s.Spieler.JeBeruf.Values.Prepend(s.Spieler.Werkstatt).Select(w => w.Platz).Distinct();
            foreach (var doppelt in s.Haeuser.SelectMany(h => h.Betriebe.Select(b => b.Platz)).Concat(spielerPlaetze).GroupBy(p => p).Where(x => x.Count() > 1))
                Fehler("start", $"Start: Der Platz „{doppelt.Key}“ ist mehrfach vergeben.", Ort(doppelt.Key), Betroffen(doppelt.Key));
            foreach (var doppelt in s.Haeuser.Select(h => h.Wohnhaus).Prepend(s.Spieler.Wohnhaus).OfType<string>().GroupBy(p => p).Where(x => x.Count() > 1))
                Fehler("start", $"Start: Der Wohnhausplatz „{doppelt.Key}“ ist mehrfach vergeben.", Ort(doppelt.Key), Betroffen(doppelt.Key));
        }

        /// <summary>
        /// Das Wegenetz hängt zusammen: Kreuzungen und Berührungen (1 m), Brücken und Fähren verbinden (karte.py, Regel 11). Ein Weg hängt in
        /// sich zusammen; verglichen werden nur Wege, deren Rahmen sich nahe kommen (glatte Kurven haben viele kurze Strecken).
        /// </summary>
        private void Wegenetz()
        {
            const double fang = 1;
            var linien = _wege.Where(w => w.Linie.Length >= 2).Select(w => (Name: w.Weg.Name, w.Linie, w.Rahmen, Weg: (EntwurfWeg?)w.Weg)).ToList();
            foreach (var u in _e.Uebergaenge)
            {
                var l = new[] { Kartenort.Aus(u.Von), Kartenort.Aus(u.Bis) };
                linien.Add((u.Name, l, Rahmen.Um(l), null));
            }
            if (linien.Count == 0) return;

            var eltern = Enumerable.Range(0, linien.Count).ToArray();
            int Wurzel(int i)
            {
                while (eltern[i] != i) i = eltern[i] = eltern[eltern[i]];
                return i;
            }
            for (var i = 0; i < linien.Count; i++)
                for (var j = i + 1; j < linien.Count; j++)
                    if (Wurzel(i) != Wurzel(j) && linien[i].Rahmen.Abstand(linien[j].Rahmen) <= fang && Beruehren(linien[i].Linie, linien[j].Linie, fang))
                        eltern[Wurzel(i)] = Wurzel(j);
            // das größte Teil (nach Zahl der Strecken) gilt als das Netz, die übrigen sind abgetrennt
            var teile = Enumerable.Range(0, linien.Count).GroupBy(Wurzel).OrderByDescending(t => t.Sum(i => linien[i].Linie.Length - 1)).ToList();
            if (teile.Count <= 1) return;
            var ab = teile.Skip(1).SelectMany(t => t).ToList();
            FehlerAn("wegenetz", $"Das Wegenetz zerfällt in {teile.Count} Teile; abgetrennt u. a.: {string.Join(", ", ab.Select(i => linien[i].Name).Distinct().Take(3).Select(n => $"„{n}“"))}.",
                linien[teile[1].First()].Linie[0], ab.Select(i => linien[i].Weg).OfType<EntwurfTeil>().ToList());
        }

        /// <summary>Kommen sich zwei Linien auf <paramref name="fang"/> Meter nahe (Kreuzung oder Berührung)?</summary>
        private static bool Beruehren(Kartenort[] p, Kartenort[] q, double fang)
        {
            for (var i = 0; i + 1 < p.Length; i++)
            {
                var (a, b) = (p[i], p[i + 1]);
                for (var j = 0; j + 1 < q.Length; j++)
                {
                    var (c, d) = (q[j], q[j + 1]);
                    if (Math.Max(a.X, b.X) + fang < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) + fang < Math.Min(a.X, b.X)
                        || Math.Max(a.Z, b.Z) + fang < Math.Min(c.Z, d.Z) || Math.Max(c.Z, d.Z) + fang < Math.Min(a.Z, b.Z))
                        continue;
                    if (Kartengeometrie.AbstandStrecken(a, b, c, d) <= fang) return true;
                }
            }
            return false;
        }
    }

    /// <summary>Das achsparallele Rechteck um Punkte – für den schnellen Test „weit genug weg“ vor der genauen Rechnung.</summary>
    private readonly record struct Rahmen(double X0, double Z0, double X1, double Z1)
    {
        public static Rahmen Um(IReadOnlyList<Kartenort> p)
        {
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            foreach (var e in p) (x0, z0, x1, z1) = (Math.Min(x0, e.X), Math.Min(z0, e.Z), Math.Max(x1, e.X), Math.Max(z1, e.Z));
            return new Rahmen(x0, z0, x1, z1);
        }

        /// <summary>Kürzester Abstand der beiden Rechtecke – nie größer als der Abstand der Punkte darin.</summary>
        public double Abstand(Rahmen o)
        {
            var dx = Math.Max(0, Math.Max(o.X0 - X1, X0 - o.X1));
            var dz = Math.Max(0, Math.Max(o.Z0 - Z1, Z0 - o.Z1));
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}