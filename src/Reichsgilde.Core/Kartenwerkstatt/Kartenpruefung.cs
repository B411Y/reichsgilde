using System.Text.RegularExpressions;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Kartenregeln in C# – damit die Kartenwerkstatt live prüft, was bisher nur das Python-Werkzeug prüfte
//
//  Herkunft der Regeln: das Python-Werkzeug bis Schritt 4 (tools/karten/karte.py, „Prüfungen nach den Kartenregeln“, für jede Karte;
//  tools/karten/greybox.py, „Prüfungen der Greybox“: Abstände der Bauzonen, Werkstätten der Stadt, Gassen über Häusern, Wege über
//  Umlandbetrieben; tools/karten/kapazitaet.py, die Kapazität). Seit Schritt 4 (PR #174) gibt es nur noch diese Regeln: Die
//  Kartenwerkstatt und das Kartenskript tools/Kartenbau prüfen beide hiermit. Die Kapazität rechnet Kapazitaetspruefung.cs, den Katalog
//  gegen die Modelle prüft Katalogpruefung.cs (nur das Kartenskript, er hängt an keiner Karte). Seit Schritt 5b (Gelände) dazu die
//  Hangsperre (Regel „hang“) für Plätze und Bauzonen, seit Schritt 5c das Wasser (Gebäude und Bauzonen nicht im Wasser, Häfen am Ufer,
//  Flüsse, Seen, Brücken, Fähren, Wege im See).
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
    /// ohne Überlappung; Probeplan; Werkstätten mit 2 m Abstand und nicht auf der Mauer; keine Gasse über ein Haus der Stadt; Wege über
    /// Umlandbetrieben (Hinweis). Werkstatt- und Wohnhausplätze stehen frei: in jeder Drehung und ohne Abstandsregel zum Weg (Entscheidung
    /// des Nutzers vom 26.10.2026, PR #209) – nur wer im Spiel baut, braucht eine Karrenstraße (Probeplan, Bauwesen).
    /// </summary>
    public bool Stadtregeln { get; init; }

    /// <summary>So viele Wohnhausplätze muss es geben, die ersten vier gleich weit vom Markt (±10 %); 0 = nicht prüfen. Greybox: 8.</summary>
    public int Wohnhausplaetze { get; init; }

    /// <summary>Mindestens so viele Plätze zu 16 × 14 m im Probeplan (Werkstatt und Stadthof), Wohnhäuser und Stadthöfe (Greybox 6, 8, 2).</summary>
    public int MindestWerkstaetten { get; init; }

    public int MindestWohnhaeuser { get; init; }
    public int MindestStadthoefe { get; init; }

    /// <summary>
    /// Wie schwer es wiegt, wenn die Gebäude einer Partie mit 8 Häusern nicht in die Bauzonen passen (<see cref="Kapazitaetspruefung"/>); null =
    /// nicht prüfen (Greybox: dort prüft der Probeplan). Köln: Fehler; neue Karten: Hinweis, damit eine kleine Karte sich ausprobieren lässt.
    /// </summary>
    public Schwere? Kapazitaet { get; init; }

    /// <summary>Für neue Karten: allgemeine Regeln und Stadtregeln, die Kapazität als Hinweis.</summary>
    public static readonly Pruefregeln Standard = new() { Stadtregeln = true, Kapazitaet = Schwere.Hinweis };

    /// <summary>Wie bisher <c>tools/karten/greybox.py</c> (statt der Kapazität der Probeplan).</summary>
    public static readonly Pruefregeln Greybox = new()
    {
        MauerGeschlossen = true, Stadtregeln = true, Wohnhausplaetze = 8,
        MindestWerkstaetten = 6, MindestWohnhaeuser = 8, MindestStadthoefe = 2,
    };

    /// <summary>Wie bisher <c>tools/karten/koeln.py</c>: 8 Startgrundstücke, die Stadtmauer offen (am Rhein), die Kapazität ein Fehler.</summary>
    public static readonly Pruefregeln Koeln = new() { Startgrundstuecke = 8, MauerGeschlossen = false, Kapazitaet = Schwere.Fehler };

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

    /// <summary>Höchstens so weit liegt die Vorplatzreihe eines Gebäudes im Probeplan von der Straßenmitte (Bauen im Spiel, wie der Kern).</summary>
    public const double Strassennah = 7;

    /// <summary>So weit darf ein Tor neben seiner Mauerlinie liegen.</summary>
    public const double Tornah = 1;

    /// <summary>Höchstens so weit liegt eine Bauzone von einer Karrenstraße.</summary>
    public const double Anschluss = 8;

    // Stadtregeln (greybox.py)
    public const double AbstandStrasse = 0.5, AbstandFeste = 2, AbstandMauer = 2, AbstandWerkstatt = 2;

    /// <summary>
    /// Hangsperre (Schritt 5b): Steht ein Platz oder eine Bauzone auf einem Hang mit mehr als 15 % Gefälle, ist das ein Fehler; ab 8 % ein
    /// Hinweis (der Sockel wird hoch). Gemessen wird mit <see cref="Gelaendeform.Hang"/> (steilstes 8-×-8-m-Fenster unter dem Umriss).
    /// </summary>
    public const double HangFehler = 0.15, HangHinweis = 0.08;

    /// <summary>Für diese Plätze gilt die Hangsperre (Bauzonen auch): Werkstatt-, Wohnhaus-, Einwohner- und Umlandplätze.</summary>
    public static readonly IReadOnlySet<string> AmHangGesperrt = new HashSet<string>
    {
        Bausteinart.Werkstattplatz, Bausteinart.Wohnhausplatz, Bausteinart.Einwohnerplatz, Bausteinart.Umlandbetrieb, Bausteinart.Umlandstelle,
    };

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
        private readonly List<(EntwurfMauer Mauer, Kartenort[] Linie, Rahmen Rahmen)> _mauern;
        private readonly List<(EntwurfBauzone Zone, Kartenort[] Flaeche, Rahmen Rahmen)> _zonen;
        private Dictionary<EntwurfBauzone, HashSet<(int, int)>>? _felder;
        private readonly Kartenort[]? _stadtgebiet;
        private readonly Kartengrenzen _grenzen;
        private readonly Dictionary<EntwurfGebaeude, (IReadOnlyList<Kartenort> Ecken, Rahmen Rahmen)> _ecken = new(ReferenceEqualityComparer.Instance);

        public Lauf(Kartenentwurf e)
        {
            (_e, _r, _grenzen) = (e, e.Regeln, e.Kartengrenzen);
            // Wege mit ihrem Verlauf (bei glatten Wegen die abgetastete Kurve) – so sehen Spiel und Bauwesen sie
            _wege = e.Wege.Where(w => w.Punkte.Count > 0).Select(w => (w, l: w.Verlauf.ToArray())).Select(x => (x.w, x.l, x.w.BreiteWirksam, Rahmen.Um(x.l))).ToList();
            // Mauerlinien, geschlossene mit dem ersten Punkt hinten dran
            _mauern = e.Mauern.Where(m => m.Ecken.Count > 0).Select(m => (m, l: m.Linie.ToArray())).Select(x => (x.m, x.l, Rahmen.Um(x.l))).ToList();
            _zonen = e.Bauzonen.Where(z => z.Ecken.Count > 0).Select(z => (z, z.Ecken.ToArray(), Rahmen.Um(z.Ecken))).ToList();
            // Die Stadt: innerhalb der ersten geschlossenen Mauer (Greybox: ±50 m)
            if (e.Mauern.FirstOrDefault(m => m.Geschlossen && m.Ecken.Count >= 3) is { } stadtmauer)
                _stadtgebiet = stadtmauer.Ecken.ToArray();
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
            ZonenAmHang();
            if (_r.Stadtregeln) Probeplan();
            if (_r.Wohnhausplaetze > 0) WohnhausplaetzeZaehlen();
            if (_r.Startgrundstuecke > 0) Startgrundstuecke();
            Marktplaetze();
            Vorkommen();
            Mauern();
            Haefen();
            Wasser();
            Natur();
            Feldflaechen();
            Pflanzen();
            Grenzen();
            Start();
            Wegenetz();
            Einwohnerplaetze();
            if (_r.Kapazitaet is { } schwere) _b.AddRange(_e.Kapazitaetsrechnung(schwere).Befunde);
            return _b;
        }

        /// <summary>
        /// Eine Karte ohne Einwohnerplätze ist erlaubt (Köln hat keine), aber im Spiel teilen sich dann alle Einwohnerhäuser einen Platz – ein
        /// Hinweis, damit das beim Bauen einer neuen Stadt nicht übersehen wird.
        /// </summary>
        private void Einwohnerplaetze()
        {
            if (_e.Gebaeude.All(g => g.Art != Bausteinart.Einwohnerplatz))
                Hinweis("einwohnerplaetze", "Die Karte hat keine Einwohnerplätze – im Spiel stehen dann alle Einwohnerhäuser an einem Platz.", null);
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
            if (b.Art == Bausteinart.Umlandbetrieb && string.IsNullOrWhiteSpace(g.Betrieb))
                Fehler("betrieb", $"{Wer(g)}: Ein Umlandbetrieb braucht den Namen seines Betriebs.", ort, g.Kennung);
            if (g.Groesse is null || g.Massstab <= 0) return;

            ImSpielfeld(g);
            AmHang(g);
            AmWasser(g);
            AufMauer(g);
            if (!_r.Stadtregeln) return;

            if (InDerStadt(g.Punkt))
            {
                if (g.Katalog != "markt") GassenUeberHaus(g);
            }
            else if (b.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle) WegeUeberUmland(g);
            ZonenAbstand(g);
        }

        /// <summary>
        /// Ein Gebäude steht ganz im Spielfeld (Schritt 5a): teilweise außerhalb der Karte ist es ein Fehler „ausserhalb“, im Randstreifen ein
        /// Fehler „rand“ – dort ist nur Natur, Felder und Wege erlaubt.
        /// </summary>
        private void ImSpielfeld(EntwurfGebaeude g)
        {
            var ecken = Ecken(g);
            if (ecken.Any(p => !_grenzen.InDerKarte(p.X, p.Z)))
                Fehler("ausserhalb", $"{Wer(g)} liegt (teilweise) außerhalb der Karte.", g.Punkt, g.Kennung);
            else if (ecken.Any(p => !_grenzen.ImSpielfeld(p.X, p.Z)))
                Fehler("rand", $"{Wer(g)} steht (teilweise) im Randstreifen – dort sind nur Natur, Felder und Wege erlaubt.", g.Punkt, g.Kennung);
        }

        /// <summary>Hangsperre (Schritt 5b): ein Platz auf mehr als 15 % Gefälle ist ein Fehler, ab 8 % ein Hinweis.</summary>
        private void AmHang(EntwurfGebaeude g)
        {
            if (_e.Gelaendeform.IstFlach || !AmHangGesperrt.Contains(g.Art)) return;
            var hang = _e.Gelaendeform.Hang(Ecken(g));
            if (hang.Gefaelle > HangFehler)
                Fehler("hang", $"{Wer(g)} steht am Hang: {Prozent(hang.Gefaelle)} % Gefälle (höchstens {Prozent(HangFehler)} %).", hang.Ort, g.Kennung);
            else if (hang.Gefaelle > HangHinweis)
                Hinweis("hang", $"{Wer(g)} steht am Hang: {Prozent(hang.Gefaelle)} % Gefälle – ab {Prozent(HangHinweis)} % wird der Sockel hoch.", hang.Ort, g.Kennung);
        }

        /// <summary>
        /// Wasser (Schritt 5c): Kein Gebäude steht im Wasser – die Grundfläche mit Vorplatz überdeckt keinen Fluss und keinen See. Ein Gebäude am
        /// Ufer (<see cref="Baustein.AmUfer"/>, der Hafen) steht mit der Rückseite am Wasser (das Modell: Weg von vorn, Kai und Kähne hinten): Die
        /// Mitte seiner Rückseite liegt höchstens <see cref="Kartenentwurf.HafenUfer"/> Meter vom Wasser (Regel „ufer“).
        /// </summary>
        private void AmWasser(EntwurfGebaeude g)
        {
            var wasser = _e.Wasserform;
            if (!wasser.IstLeer && wasser.Ueberlappt(Ecken(g)) is { } nass)
                Fehler("wasser", $"{Wer(g)} steht im Wasser („{nass.Name}“ bei {nass.Ort}).", nass.Ort, g.Kennung);
            if (!g.Baustein!.AmUfer || g.Groesse is not var (_, t)) return;
            var hinten = g.Lokal(0, -t / 2);
            var abstand = wasser.Uferabstand(hinten.X, hinten.Z);
            if (abstand > Kartenentwurf.HafenUfer)
                Fehler("ufer", abstand == double.MaxValue
                    ? $"{Wer(g)} steht nicht am Ufer: Hinter seiner Rückseite {hinten} ist kein Wasser (höchstens {M(Kartenentwurf.HafenUfer)} m entfernt)."
                    : $"{Wer(g)} steht nicht am Ufer: Die Rückseite {hinten} liegt {M(abstand)} m vom Wasser (höchstens {M(Kartenentwurf.HafenUfer)} m).",
                    hinten, g.Kennung);
        }

        /// <summary>Hangsperre für Bauzonen: die steilste Stelle in der Zone.</summary>
        private void ZonenAmHang()
        {
            if (_e.Gelaendeform.IstFlach) return;
            foreach (var (z, flaeche, _) in _zonen)
            {
                if (flaeche.Length < 3) continue;
                var hang = _e.Gelaendeform.Hang(flaeche);
                if (hang.Gefaelle > HangFehler)
                    FehlerAn("hang", $"Bauzone {z.Kennung}: {Prozent(hang.Gefaelle)} % Gefälle bei {hang.Ort} (höchstens {Prozent(HangFehler)} %).", hang.Ort, new[] { z });
                else if (hang.Gefaelle > HangHinweis)
                    HinweisAn("hang", $"Bauzone {z.Kennung}: {Prozent(hang.Gefaelle)} % Gefälle bei {hang.Ort} – ab {Prozent(HangHinweis)} % werden die Sockel hoch.",
                        hang.Ort, new[] { z });
            }
        }

        /// <summary>Ein Anteil als Prozent mit einer Stelle („12,5“).</summary>
        private static string Prozent(double anteil) => M(anteil * 100);

        /// <summary>
        /// Kein Gebäude steht auf einer Mauer: das Haus (ohne Vorplatz) mindestens 1 m von der Mauerlinie (greybox.py, Regel 5, für Werkstätten;
        /// seit Schritt 3 für alle Gebäude außer Felsbergen, deren Rand ohnehin über Nachbarn läuft). Mit den Stadtregeln ein Fehler, sonst ein
        /// Hinweis (in Köln stehen St. Gereon und ein Leerstand auf der Mauer).
        /// </summary>
        private void AufMauer(EntwurfGebaeude g)
        {
            if (g.Art == Bausteinart.Berg) return;
            var haus = g.Ecken(ohneVorplatz: true);
            var rahmen = Rahmen.Um(haus);
            foreach (var (m, linie, mr) in _mauern)
                if (rahmen.Abstand(mr) < 1 && Kartengeometrie.AbstandVielecke(haus, linie, false) < 1)
                {
                    var text = $"{Wer(g)} steht auf der Mauer „{m.Name}“.";
                    if (_r.Stadtregeln) FehlerAn("mauer", text, g.Punkt, new[] { m }, g.Kennung);
                    else HinweisAn("mauer", text, g.Punkt, new[] { m }, g.Kennung);
                }
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

        /// <summary>
        /// Wie weit ein Punkt von der Mitte des nächsten Marktplatzes liegt (Luftlinie; ohne Marktplatz vom Ursprung, wo der Markt bis Oktober 2026
        /// immer lag). Seit dem 31.10.2026 darf eine Stadt mehrere Marktplätze haben; „gleich weit vom Markt“ heißt dann: vom jeweils nächsten.
        /// </summary>
        private double ZumMarkt(Kartenort p)
        {
            var maerkte = _e.Gebaeude.Where(g => g.Katalog == Stadtkarte.MarktKatalog).Select(g => g.Punkt).ToList();
            return maerkte.Count == 0 ? p.Abstand(new Kartenort(0, 0)) : maerkte.Min(m => p.Abstand(m));
        }

        /// <summary>So viele Wohnhausplätze wie Häuser, die ersten vier gleich weit vom (nächsten) Markt (±10 %; greybox.py, Regel 4).</summary>
        private void WohnhausplaetzeZaehlen()
        {
            var plaetze = _e.Gebaeude.Where(g => g.Art == Bausteinart.Wohnhausplatz).ToList();
            if (plaetze.Count != _r.Wohnhausplaetze)
                Fehler("wohnhausplaetze", $"Die Karte hat {plaetze.Count} Wohnhausplätze statt {_r.Wohnhausplaetze}.", null);
            var vorn = plaetze.Take(4).ToList();
            if (vorn.Count == 0) return;
            var mittel = vorn.Average(g => ZumMarkt(g.Punkt));
            foreach (var g in vorn.Where(g => Math.Abs(ZumMarkt(g.Punkt) - mittel) > 0.10 * mittel))
                Fehler("wohnhausplaetze", $"{Wer(g)}: {M(ZumMarkt(g.Punkt))} m zum Markt, die ersten vier im Mittel {M(mittel)} m (±10 %).", g.Punkt, g.Kennung);
        }

        /// <summary>
        /// Köln: genau n Startgrundstücke (Werkstatt- und Wohnhausplatz „S&lt;n&gt;_…“), gleich weit vom Markt (±10 %; karte.py, Regel 5) – mit mehreren
        /// Marktplätzen jedes vom nächsten.
        /// </summary>
        private void Startgrundstuecke()
        {
            var starts = _e.Gebaeude.Select(g => (g, m: Regex.Match(g.Kennung, @"^(S\d+)_(Werkstatt|Wohnhaus)$"))).Where(x => x.m.Success)
                .GroupBy(x => x.m.Groups[1].Value, x => x.g).ToList();
            if (starts.Count != _r.Startgrundstuecke)
                Fehler("startgrundstuecke", $"Die Karte hat {starts.Count} Startgrundstücke statt {_r.Startgrundstuecke}.", null);
            if (starts.Count == 0) return;
            var abstand = starts.ToDictionary(s => s.Key, s => ZumMarkt(new Kartenort(s.Average(g => g.X), s.Average(g => g.Z))));
            var mittel = abstand.Values.Average();
            foreach (var s in starts.Where(s => Math.Abs(abstand[s.Key] - mittel) > 0.10 * mittel))
                Fehler("startgrundstuecke", $"Startgrundstück {s.Key}: {M(abstand[s.Key])} m zum Markt, im Mittel {M(mittel)} m (±10 %).",
                    s.First().Punkt, s.Select(g => g.Kennung).ToArray());
        }

        /// <summary>
        /// Jeder Marktplatz wird von einer Karrenstraße berührt (Hinweis, seit den mehreren Marktplätzen am 31.10.2026): Sonst hängt der Kern ihn an die
        /// nächste Straße, und das Stück dorthin zählt für die Fahrzeit nicht (<c>Karrennetz.Marktlinien</c>, <c>Karrennetz.Fahrt</c>).
        /// </summary>
        private void Marktplaetze()
        {
            foreach (var g in _e.Gebaeude.Where(g => g.Katalog == Stadtkarte.MarktKatalog))
            {
                var ecken = g.Ecken();
                if (ecken.Count == 0) continue;
                var beruehrt = _wege.Any(w => Kartenweg.TraegtKarren(w.Weg.Klasse) && w.Linie.Length >= 2
                                              && Kartengeometrie.AbstandVielecke(ecken, w.Linie, false) <= Karrennetz.Fang);
                if (!beruehrt)
                    Hinweis("markt", $"{Wer(g)}: Keine Karrenstraße berührt den Marktplatz – die Karren fahren zur nächsten Straße, das letzte Stück zählt nicht.", g.Punkt, g.Kennung);
            }
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
        /// (karte.py, Regel 7); eine Mauer ohne Tor ist ein Hinweis (niemand kommt hinein oder hinaus).
        /// </summary>
        private void Mauern()
        {
            for (var i = 0; i < _e.Mauern.Count; i++)
            {
                var m = _e.Mauern[i];
                var ort = m.Ecken.Count > 0 ? m.Ecken[0] : (Kartenort?)null;
                var teil = new[] { m };
                if (m.Ecken.Count < 2) FehlerAn("mauer", $"Mauer „{m.Name}“: Die Linie braucht mindestens zwei Punkte.", ort, teil);
                else if (m.Geschlossen && m.Ecken.Count < 3) FehlerAn("mauer", $"Mauer „{m.Name}“: Eine geschlossene Mauer braucht mindestens drei Ecken.", ort, teil);
                if (_e.Katalog.Finde(m.Katalog) is not { Art: Bausteinart.Mauer })
                    FehlerAn("mauer", $"Mauer „{m.Name}“: Den Mauer-Baukasten „{m.Katalog}“ gibt es im Katalog nicht.", ort, teil);
                if (i == 0 && _r.MauerGeschlossen is { } soll && m.Geschlossen != soll)
                    FehlerAn("mauer", $"Mauer „{m.Name}“: In dieser Karte ist die Stadtmauer {(soll ? "geschlossen" : "offen")}.", ort, teil);
                if (m.Tore.Count == 0 && m.Ecken.Count >= 2)
                    HinweisAn("mauer-tor", $"Mauer „{m.Name}“ hat kein Tor – Wege und Karren kommen nicht hindurch.", ort, teil);
            }
            foreach (var (m, linie, _) in _mauern)
            {
                foreach (var t in m.Tore)
                {
                    var n = Kartengeometrie.AbstandLinie(t.Punkt, linie);
                    if (n > Tornah) FehlerAn("tor", $"Tor „{t.Name}“: {M(n)} m neben der Linie der Mauer „{m.Name}“ (höchstens {M(Tornah)} m).", t.Punkt, new[] { m });
                }
                foreach (var (w, wl, _, _) in _wege)
                    for (var a = 0; a + 1 < wl.Length; a++)
                        for (var c = 0; c + 1 < linie.Length; c++)
                            if (Kartengeometrie.Schnitt(wl[a], wl[a + 1], linie[c], linie[c + 1]) is { } s
                                && (m.Tore.Count == 0 ? double.MaxValue : m.Tore.Min(t => s.Abstand(t.Punkt))) > 3)
                                FehlerAn("tor", $"„{w.Name}“ kreuzt die Mauer „{m.Name}“ ohne Tor bei {s}.", s, new EntwurfTeil[] { w, m });
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
        /// Wasser (Schritt 5c): Flüsse mit mindestens zwei Punkten, Breite 0,5–300 m, Tiefe 0,1–20 m; ein Fluss, der sich mehr als 2 m ins Gelände
        /// einschneidet, läuft wohl bergauf (Hinweis – der Spiegel steigt nie, siehe Wasserform). Seen mit mindestens drei Ecken, die sich nicht
        /// selbst kreuzen (Hinweis); ein fester Wasserstand über dem Ufer (Hinweis). Keine Bauzone im Wasser. Brücken über 60 m (Hinweis), Wege, die
        /// durchs Wasser laufen, ohne es zu queren (Hinweis), Wege durch einen See (Fehler). Fähren: beide Anleger an Land, höchstens 15 m vom
        /// Wasser, dazwischen Wasser (sonst „Fähre ohne zwei Ufer“), an jedem Anleger ein Weg (Hinweis). Alte Brückeneinträge (Hinweis).
        /// </summary>
        private void Wasser()
        {
            var form = _e.Wasserform;
            foreach (var f in _e.Fluesse)
            {
                var teil = new EntwurfTeil[] { f };
                var wer = $"„{f.Bezeichnung}“";
                var ort = f.Punkte.Count > 0 ? f.Punkte[0] : (Kartenort?)null;
                if (f.Punkte.Count < 2) FehlerAn("fluss", $"{wer}: Ein Fluss braucht mindestens zwei Punkte.", ort, teil);
                for (var i = 0; i < f.Punkte.Count; i++)
                {
                    if (f.BreiteAn(i) < Wasserform.MindestBreite || f.BreiteAn(i) > Wasserform.HoechstBreite)
                        FehlerAn("fluss", $"{wer}: Am Punkt {f.Punkte[i]} ist er {M(f.BreiteAn(i))} m breit (erlaubt {M(Wasserform.MindestBreite)} bis {M(Wasserform.HoechstBreite)} m).", f.Punkte[i], teil);
                    if (f.TiefeAn(i) < Wasserform.MindestTiefe || f.TiefeAn(i) > Wasserform.HoechstTiefe)
                        FehlerAn("fluss", $"{wer}: Am Punkt {f.Punkte[i]} ist er {M(f.TiefeAn(i))} m tief (erlaubt {M(Wasserform.MindestTiefe)} bis {M(Wasserform.HoechstTiefe)} m).", f.Punkte[i], teil);
                }
            }
            foreach (var l in form.Laeufe.Where(l => l.GroessterEinschnitt > Wasserform.Schluchtgrenze))
                HinweisAn("fluss-bergauf", $"„{l.Name}“ läuft bergauf: Bei {l.EinschnittOrt} liegt der Spiegel {M(l.GroessterEinschnitt)} m unter dem Gelände, das Bett " +
                                           "schneidet sich ein (Wasser fließt nie bergauf). Richtung umkehren oder das Gelände senken?", l.EinschnittOrt, new[] { _e.Gewaesser[l.Nummer] });
            foreach (var s in _e.Seen)
            {
                var ort = s.Ecken.Count > 0 ? Kartengeometrie.Mitte(s.Ecken) : (Kartenort?)null;
                if (s.Ecken.Count < 3) FehlerAn("see", $"„{s.Bezeichnung}“: Ein See braucht mindestens drei Ecken.", ort, new[] { s });
                if (s.Tiefe is { } t && (t < Wasserform.MindestTiefe || t > Wasserform.HoechstTiefe))
                    FehlerAn("see", $"„{s.Bezeichnung}“: {M(t)} m tief (erlaubt {M(Wasserform.MindestTiefe)} bis {M(Wasserform.HoechstTiefe)} m).", ort, new[] { s });
                SelbstSchnitt($"„{s.Bezeichnung}“", s.Ecken, s);
            }
            foreach (var s in form.Seen.Where(s => !s.StandAutomatisch && s.Wasserstand > s.TiefstesUfer + 0.05))
                HinweisAn("see-ufer", $"„{s.Name}“: Der Wasserstand ({M(s.Wasserstand)} m) liegt über dem Ufer bei {s.TiefstesUferOrt} ({M(s.TiefstesUfer)} m) – " +
                                      "dort liefe der See über.", s.TiefstesUferOrt, new[] { _e.Gewaesser[s.Nummer] });
            if (!form.IstLeer)
                foreach (var (z, flaeche, _) in _zonen)
                    if (flaeche.Length >= 3 && form.Ueberlappt(flaeche) is { } nass)
                        FehlerAn("wasser", $"Bauzone {z.Kennung} liegt im Wasser („{nass.Name}“ bei {nass.Ort}).", nass.Ort, new[] { z });

            var plan = _e.Wasserplan;
            foreach (var q in plan.Querungen)
            {
                var weg = new EntwurfTeil[] { _e.Wege[q.Weg] };
                if (q.IstBruecke && q.Weite > Wasserplan.LangeBruecke)
                    HinweisAn("bruecke-lang", $"„{q.WegName}“: Die Brücke über „{q.GewaesserName}“ bei {q.Mitte} spannt {M(q.Weite)} m von Ufer zu Ufer (ab {M(Wasserplan.LangeBruecke)} m sehr lang).",
                        q.Mitte, weg);
                if (!q.Quert)
                    HinweisAn("weg-im-wasser", $"„{q.WegName}“ läuft bei {q.Mitte} {M(q.Spannweite)} m durch „{q.GewaesserName}“, ohne ihn zu queren.", q.Mitte, weg);
            }
            foreach (var x in plan.Sperren)
                FehlerAn("weg-see", $"„{x.WegName}“ läuft bei {x.Ort} durch „{x.See}“ – über einen See führt kein Weg (eine Fähre setzt über).", x.Ort,
                    new[] { _e.Wege[x.Weg] });
            foreach (var f in plan.Faehren)
            {
                var teil = new EntwurfTeil[] { _e.Faehren[f.Nummer] };
                var wer = $"Fähre „{f.Name}“";
                if (f.VonImWasser || f.BisImWasser)
                    FehlerAn("faehre", $"{wer}: Der Anleger {(f.VonImWasser ? f.Von : f.Bis)} steht im Wasser – eine Fähre braucht zwei Ufer.", f.VonImWasser ? f.Von : f.Bis, teil);
                else if (f.Gewaesser is null)
                    FehlerAn("faehre", $"{wer}: Zwischen den Anlegern liegt kein Wasser – eine Fähre braucht zwei Ufer.", f.Von, teil);
                else if (Math.Max(f.VonUfer, f.BisUfer) > Kartenentwurf.AnlegerUfer)
                {
                    var (p, d) = f.VonUfer >= f.BisUfer ? (f.Von, f.VonUfer) : (f.Bis, f.BisUfer);
                    FehlerAn("faehre", $"{wer}: Der Anleger {p} steht {(d == double.MaxValue ? "weit" : M(d) + " m")} vom Wasser (höchstens {M(Kartenentwurf.AnlegerUfer)} m) " +
                                       "– eine Fähre braucht zwei Ufer.", p, teil);
                }
                foreach (var p in new[] { f.Von, f.Bis })
                    if (NaechsterWeg(p, _wege) > Karrennetz.Fang)
                        HinweisAn("faehre-weg", $"{wer}: Am Anleger {p} endet kein Weg (höchstens {M(Karrennetz.Fang)} m) – so setzt niemand über.", p, teil);
            }
            foreach (var u in _e.Faehren.Where(u => !u.IstFaehre))
                HinweisAn("bruecke-alt", $"„{u.Bezeichnung}“ (uebergaenge, art „{u.Art}“): Brücken entstehen seit Schritt 5c von selbst, wo ein Weg einen Fluss quert – " +
                                         "der Eintrag zählt nur noch für das Wegenetz.", u.Von, new[] { u });
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
        /// Felder (Schritt 5a): mindestens drei Ecken, Streifen und Stücklänge in ihren Grenzen, mindestens eine bekannte Frucht mit Gewicht, jede
        /// Frucht bekannt; Umriss und Aussparungen kreuzen sich nicht selbst (Hinweis).
        /// </summary>
        private void Feldflaechen()
        {
            foreach (var f in _e.Felder)
            {
                var ort = f.Ecken.Count > 0 ? Kartengeometrie.Mitte(f.Ecken) : (Kartenort?)null;
                var teil = new[] { f };
                var wer = $"Feld „{f.Bezeichnung}“";
                if (f.Ecken.Count < 3) FehlerAn("feld", $"{wer}: Die Fläche braucht mindestens drei Punkte.", ort, teil);
                if (f.Streifen < FeldVerteilung.StreifenMin || f.Streifen > FeldVerteilung.StreifenMax)
                    FehlerAn("feld", $"{wer}: Streifen {M(f.Streifen)} m breit, erlaubt {M(FeldVerteilung.StreifenMin)} bis {M(FeldVerteilung.StreifenMax)} m.", ort, teil);
                if (f.Laenge < FeldVerteilung.LaengeMin || f.Laenge > FeldVerteilung.LaengeMax)
                    FehlerAn("feld", $"{wer}: Stücke {M(f.Laenge)} m lang, erlaubt {M(FeldVerteilung.LaengeMin)} bis {M(FeldVerteilung.LaengeMax)} m.", ort, teil);
                foreach (var k in f.Fruechte.Keys.Where(k => !FeldVerteilung.Fruechte.ContainsKey(k)))
                    FehlerAn("feld", $"{wer}: Eine Frucht „{k}“ gibt es nicht.", ort, teil);
                if (!f.Fruechte.Any(x => x.Value > 0 && FeldVerteilung.Fruechte.ContainsKey(x.Key)))
                    FehlerAn("feld", $"{wer}: Es fehlt eine Frucht mit Gewicht.", ort, teil);
                SelbstSchnitt(wer, f.Ecken, f);
                for (var i = 0; i < f.Aussparungen.Count; i++)
                {
                    if (f.Aussparungen[i].Count < 3) FehlerAn("feld", $"{wer}: Aussparung {i + 1} braucht mindestens drei Punkte.", ort, teil);
                    SelbstSchnitt($"{wer}, Aussparung {i + 1}", f.Aussparungen[i], f);
                }
            }
        }

        /// <summary>
        /// Größe und Grenzen (Schritt 5a): Das Spielfeld ist 400 × 300 bis 2.000 × 2.000 m groß, der Rand 100 bis 600 m breit. Bespielbares
        /// (Bauzonen, Mauern mit Toren, Vorkommen, Häfen, Übergänge) liegt ganz im Spielfeld, sonst „rand“ bzw. „ausserhalb“ (Gebäude prüft
        /// <see cref="ImSpielfeld"/>); Wege, Natur, Felder, Pflanzen, Wasser und Gelände dürfen in den Rand, aber nicht aus der Karte hinaus;
        /// der Kamera-Start liegt im Spielfeld.
        /// </summary>
        private void Grenzen()
        {
            var g = _grenzen;
            if (g.Breite < Kartengrenzen.MinBreite - 1e-9 || g.Tiefe < Kartengrenzen.MinTiefe - 1e-9 || g.Breite > Kartengrenzen.MaxSeite + 1e-9 || g.Tiefe > Kartengrenzen.MaxSeite + 1e-9)
                Fehler("groesse", $"Die Karte ist {M(g.Breite)} × {M(g.Tiefe)} m groß, erlaubt sind {M(Kartengrenzen.MinBreite)} × {M(Kartengrenzen.MinTiefe)} " +
                                  $"bis {M(Kartengrenzen.MaxSeite)} × {M(Kartengrenzen.MaxSeite)} m.", null);
            if (g.Randbreite < Kartengrenzen.RandMin - 1e-9 || g.Randbreite > Kartengrenzen.RandMax + 1e-9)
                Fehler("groesse", $"Der Randstreifen ist {M(g.Randbreite)} m breit, erlaubt sind {M(Kartengrenzen.RandMin)} bis {M(Kartengrenzen.RandMax)} m.", null);

            // Bespielbares: ganz im Spielfeld
            void Spiel(string wer, IEnumerable<Kartenort> punkte, EntwurfTeil? teil = null)
            {
                var liste = punkte.ToList();
                var aussen = liste.Cast<Kartenort?>().FirstOrDefault(p => !g.InDerKarte(p!.Value.X, p.Value.Z));
                var imRand = liste.Cast<Kartenort?>().FirstOrDefault(p => !g.ImSpielfeld(p!.Value.X, p.Value.Z));
                var teile = teil is null ? Array.Empty<EntwurfTeil>() : new[] { teil };
                if (aussen is { } a) FehlerAn("ausserhalb", $"{wer} liegt (teilweise) außerhalb der Karte, etwa bei {a}.", a, teile);
                else if (imRand is { } r) FehlerAn("rand", $"{wer} liegt (teilweise) im Randstreifen, etwa bei {r} – dort sind nur Natur, Felder und Wege erlaubt.", r, teile);
            }
            foreach (var z in _e.Bauzonen) Spiel($"Bauzone {z.Kennung}", z.Ecken, z);
            foreach (var m in _e.Mauern) Spiel($"Mauer „{m.Name}“", m.Ecken.Concat(m.Tore.Select(t => t.Punkt)), m);
            foreach (var v in _e.Vorkommen) Spiel($"Vorkommen {v.Kennung}", new[] { v.Punkt });
            foreach (var h in _e.Haefen)
                Spiel(h.Name, EntwurfTeil.Orte(h.Kante).Concat(EntwurfTeil.Orte(h.Anleger)).Concat(EntwurfTeil.Orte(h.Kran is null ? null : new[] { h.Kran })));
            foreach (var u in _e.Faehren) Spiel($"„{u.Bezeichnung}“", new[] { u.Von, u.Bis }, u);

            // Alles andere: in der Karte (der Rand gehört dazu)
            void Karte(string wer, IEnumerable<Kartenort> punkte, EntwurfTeil? teil = null)
            {
                foreach (var p in punkte)
                    if (!g.InDerKarte(p.X, p.Z))
                    {
                        FehlerAn("ausserhalb", $"{wer} reicht über die Karte hinaus, etwa bei {p}.", p, teil is null ? Array.Empty<EntwurfTeil>() : new[] { teil });
                        return;
                    }
            }
            foreach (var w in _e.Wege) Karte($"„{w.Name}“", w.Verlauf, w);
            foreach (var n in _e.Naturflaechen) Karte($"Naturfläche „{n.Name}“", n.Ecken, n);
            foreach (var f in _e.Felder) Karte($"Feld „{f.Bezeichnung}“", f.Ecken, f);
            foreach (var p in _e.Pflanzen) Karte($"{p.Bezeichnung}", new[] { p.Punkt }, p);
            foreach (var f in _e.Fluesse) Karte($"„{f.Bezeichnung}“", f.Punkte, f);
            foreach (var s in _e.Seen) Karte($"„{s.Bezeichnung}“", s.Ecken, s);
            foreach (var x in _e.Gelaende) Karte(x.Name, EntwurfTeil.Orte(x.Flaeche));

            if (_e.Kamera is { Punkt: { Length: 2 } k } && !g.ImSpielfeld(k[0], k[1]))
                Fehler("kamera", $"Der Kamera-Start {new Kartenort(k[0], k[1])} liegt außerhalb des Spielfelds.", new Kartenort(k[0], k[1]));
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
                // Belag (Schritt 5d): Ein Abschnitt hinter dem Ende wirkt nicht – etwa nachdem der Weg kürzer wurde.
                foreach (var b in w.Belag.Where(b => w.Punkte.Count >= 2 && b.Von >= w.Laenge - 0.05))
                    HinweisAn("belag", $"„{w.Name}“: Der Belag {Wegbelag.Anzeigename(b.Stufe)} ab {M(b.Von)} m liegt hinter dem Wegende ({M(w.Laenge)} m) – er wirkt nicht.",
                        w.Punkte[^1], new[] { w });
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

        /// <summary>
        /// Die Startbelegung (karte.py, Regel 10, und Stadtkarte.Pruefen; seit Schritt 3 mehr): Plätze gibt es und sie haben die richtige Art,
        /// nichts doppelt; der Spieler hat einen Startplatz und ein Wohnhaus, jedes Haus ein Wohnhaus und mindestens einen Betrieb; ein bis acht
        /// KI-Häuser; die Betriebsart passt zum Platz (Werkstattplatz: Werkstatt der Stufe 1 in der Stadt), die erste Ware zur Betriebsart, der
        /// Beruf eines Startplatzes gibt es (die drei letzten nur mit <see cref="Kartenentwurf.Daten"/>). Ohne Startbelegung gilt im Spiel der
        /// Ersatz ohne Plätze – mit den Stadtregeln ein Fehler (der Spieler hätte keinen Startplatz), sonst ein Hinweis (Köln).
        /// </summary>
        private void Start()
        {
            var s = _e.Start;
            var teil = new EntwurfTeil[] { s };
            if (s.IstLeer)
            {
                var text = "Start: Die Karte hat keine Startbelegung – der Startplatz des Spielers fehlt, im Spiel gälten die drei Häuser des Ersatzes ohne Plätze.";
                if (_r.Stadtregeln) FehlerAn("start-fehlt", text, null, teil);
                else HinweisAn("start-fehlt", text, null, teil);
                return;
            }
            var daten = _e.Daten;
            var nachKennung = _e.Gebaeude.GroupBy(g => g.Kennung).ToDictionary(g => g.Key, g => g.First());
            EntwurfGebaeude? Platz(string? k) => k is null ? null : nachKennung!.GetValueOrDefault(k);
            Kartenort? Ort(string? k) => Platz(k)?.Punkt;
            string[] Betroffen(string? k) => Platz(k) is { } g ? new[] { g.Kennung } : Array.Empty<string>();
            void StartFehler(string text, string? kennung) => FehlerAn("start", text, Ort(kennung), teil, Betroffen(kennung));

            void Wohnhaus(string wer, string? kennung)
            {
                if (kennung is null) StartFehler($"Start, {wer}: Es fehlt das Wohnhaus.", null);
                else if (Platz(kennung) is not { Art: Bausteinart.Wohnhausplatz })
                    StartFehler($"Start, {wer}: „{kennung}“ ist kein Wohnhausplatz der Karte.", kennung);
            }
            Wohnhaus("Spieler", s.SpielerWohnhaus);
            if (s.SpielerWerkstatt is null) StartFehler("Start, Spieler: Der Startplatz der Werkstatt fehlt.", null);
            foreach (var (beruf, w) in s.JeBeruf.Select(e => (e.Key, (EntwurfStartplatz?)e.Value)).Prepend(("", s.SpielerWerkstatt)))
            {
                if (w is null) continue;
                var wer = $"Start, Spieler{(beruf == "" ? "" : $" ({beruf})")}";
                if (Platz(w.Platz) is not { Art: Bausteinart.Werkstattplatz })
                    StartFehler($"{wer}: „{w.Platz}“ ist kein Werkstattplatz der Karte.", w.Platz);
                if (beruf != "" && daten is not null && !daten.Berufe.ContainsKey(beruf))
                    StartFehler($"{wer}: Den Beruf „{beruf}“ gibt es nicht.", w.Platz);
            }
            if (s.SpielerFarbe is { } sf && !IstFarbe(sf)) StartFehler($"Start, Spieler: Die Farbe „{sf}“ ist nicht #rrggbb.", null);

            if (s.Haeuser.Count == 0) StartFehler("Start: Es braucht mindestens ein KI-Haus (das erste spielt immer mit).", null);
            if (s.Haeuser.Count > Kartenentwurf.HoechstensHaeuser) StartFehler($"Start: Höchstens {Kartenentwurf.HoechstensHaeuser} KI-Häuser, nicht {s.Haeuser.Count}.", null);
            foreach (var h in s.Haeuser)
            {
                if (string.IsNullOrWhiteSpace(h.Name)) StartFehler("Start: Ein Haus hat keinen Namen.", null);
                Wohnhaus(h.Name, h.Wohnhaus);
                if (h.Wappen.Fehler() is { } wf) StartFehler($"Start, {h.Name}: {wf}", null);
                if (h.Farbe is { } hf && !IstFarbe(hf)) StartFehler($"Start, {h.Name}: Die Farbe „{hf}“ ist nicht #rrggbb.", null);
                if (h.Betriebe.Count == 0) StartFehler($"Start, {h.Name}: Ein Haus braucht mindestens einen Betrieb.", null);
                foreach (var b in h.Betriebe)
                {
                    var platz = Platz(b.Platz);
                    if (platz is not { Art: Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb })
                        StartFehler($"Start, {h.Name}: „{b.Platz}“ ist weder Werkstattplatz noch Umlandbetrieb der Karte.", b.Platz);
                    else if (string.IsNullOrWhiteSpace(platz.Betrieb))
                        StartFehler($"Start, {h.Name}: Am Platz „{b.Platz}“ fehlt der Name des Betriebs.", b.Platz);
                    else if (platz.Art == Bausteinart.Umlandbetrieb && platz.Baustein!.BetriebsartWert != b.Art)
                        StartFehler($"Start, {h.Name}: Auf „{b.Platz}“ steht {platz.Baustein.Anzeigename}, kein Platz für {b.Art}.", b.Platz);
                    else if (platz.Art == Bausteinart.Werkstattplatz && daten is not null && !_e.BetriebsartenFuer(platz).Contains(b.Art))
                        StartFehler($"Start, {h.Name}: {b.Art} ist keine Werkstatt der Stufe 1 in der Stadt – sie passt nicht auf den Werkstattplatz „{b.Platz}“.", b.Platz);
                    if (b.Produkt is { } ware && daten is not null && daten.KenntArt(b.Art) && !daten.Art(b.Art).Produkte.Contains(ware))
                        StartFehler($"Start, {h.Name}: {b.Art} stellt „{ware}“ nicht her.", b.Platz);
                }
            }
            foreach (var doppelt in s.Haeuser.GroupBy(h => h.Name).Where(x => x.Count() > 1))
                StartFehler($"Start: Das Haus „{doppelt.Key}“ steht doppelt.", null);
            foreach (var doppelt in s.Haeuser.GroupBy(h => h.Wappen).Where(x => x.Count() > 1))
                StartFehler($"Start: Zwei Häuser führen dasselbe Wappen ({string.Join(", ", doppelt.Select(h => h.Name))}).", null);
            var spielerPlaetze = s.JeBeruf.Select(e => e.Value.Platz).Concat(s.SpielerWerkstatt is { } sw ? new[] { sw.Platz } : Array.Empty<string>()).Distinct();
            foreach (var doppelt in s.Haeuser.SelectMany(h => h.Betriebe.Select(b => b.Platz)).Concat(spielerPlaetze).GroupBy(p => p).Where(x => x.Count() > 1))
                StartFehler($"Start: Der Platz „{doppelt.Key}“ ist mehrfach vergeben.", doppelt.Key);
            foreach (var doppelt in s.Haeuser.Select(h => h.Wohnhaus).Prepend(s.SpielerWohnhaus).OfType<string>().GroupBy(p => p).Where(x => x.Count() > 1))
                StartFehler($"Start: Der Wohnhausplatz „{doppelt.Key}“ ist mehrfach vergeben.", doppelt.Key);
        }

        private static bool IstFarbe(string f) => Regex.IsMatch(f, "^#[0-9a-fA-F]{6}$");

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