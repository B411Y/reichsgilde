using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Eine Eigenschaft eines Vorarbeiters, wie sie die Oberfläche zeigt (als Karte zur Wahl oder am Vorarbeiter).</summary>
/// <param name="Gruppe">Ware, Führung oder Betrieb.</param>
/// <param name="Wirkung">Die Wirkung in einem Satz.</param>
public sealed record EigenschaftZeile(string Kennung, string Name, string Gruppe, string Wirkung);

/// <summary>
/// Personal wie in Gilde 1 (PR #224, Abschnitt „personal“): Laufbahnen mit drei Stufen, nur Anfänger einstellbar, Beförderung von Hand
/// mit Gesellen- bzw. Vorarbeiterbrief, sobald die Erfahrung reicht, höchstens ein Vorarbeiter je Betrieb mit einer Eigenschaft aus drei
/// gezogenen. Dazu Name und Aussehen jedes Arbeiters. Ohne Abschnitt bleibt die alte Beförderung (<see cref="Werkstattwesen"/>).
///
/// Zufall ohne eigenen Zustand: Name, Aussehen und die drei Eigenschaften würfelt ein Generator, dessen Startwert aus Betrieb und Nummer
/// (bzw. Name) folgt. So verschieben sie keine anderen Würfe, ein Spielstand braucht keinen Zähler, und Neuladen zieht dieselben Eigenschaften
/// (kein Neuwürfeln).
/// </summary>
public sealed class Personalwesen
{
    private readonly Stadtwirtschaft _w;

    /// <summary>Die Daten (Abschnitt „personal“); null: alte Beförderung aus „werkstatt“.</summary>
    public PersonalDaten? Daten { get; }

    public bool IstAktiv => Daten is not null;

    internal Personalwesen(Stadtwirtschaft w, PersonalDaten? daten)
    {
        _w = w;
        Daten = daten;
    }

    private string Waehrung => _w.Daten.Allgemein.Waehrung;
    private long Jetzt => _w.Uhr.VergangeneMinuten;

    // ── Laufbahn und Stufe ──────────────────────────────────────────────────

    /// <summary>Die Laufbahn der Leute dieses Betriebs (null ohne Abschnitt „personal“).</summary>
    public LaufbahnDaten? Laufbahn(Betrieb b) => _w.Daten.Laufbahn(b.Art);

    /// <summary>Stufe 1–3 dieser Arbeitskraft (0: ohne Stufen – Unfreie, Orks, oder ohne Abschnitt „personal“).</summary>
    public int Stufe(string arbeitskraft) => Daten?.StufeVon(arbeitskraft) ?? 0;

    /// <summary>Ist er ein Anfänger (Lehrling, Knecht, Taschendieb, Rekrut)? Ohne Abschnitt „personal“: ein Lehrling.</summary>
    public bool IstAnfaenger(Arbeiter a) => Daten is { } d ? d.StufeVon(a.Arbeitskraft) == 1 : a.Arbeitskraft == "Lehrling";

    /// <summary>Wen dieser Betrieb einstellen kann: den Anfänger seiner Laufbahn (null ohne Abschnitt „personal“).</summary>
    public string? Anfaenger(Betrieb b) => Laufbahn(b)?.Anfaenger;

    /// <summary>Der Anfänger der Laufbahn einer Betriebsart (für den Start der Partie); ohne Abschnitt „personal“: <paramref name="sonst"/>.</summary>
    public string AnfaengerFuer(Betriebsart art, string sonst) => _w.Daten.Laufbahn(art)?.Anfaenger ?? sonst;

    /// <summary>
    /// Darf dieser Betrieb eine Arbeitskraft dieser Art neu einstellen (Befehle <see cref="PersonalSetzen"/> und <see cref="ArbeiterEinstellen"/>)?
    /// Nur Anfänger seiner Laufbahn und Arbeitskräfte ohne Stufen (Unfreie, Orks – das Statut prüft der Betrieb). null: Es geht.
    /// </summary>
    public string? EinstellenVerboten(Betrieb b, string arbeitskraft)
    {
        if (Daten is not { } d || Laufbahn(b) is not { } l) return null;
        var stufe = d.StufeVon(arbeitskraft);
        if (stufe == 0 || arbeitskraft == l.Anfaenger) return null;
        if (!l.Stufen.Contains(arbeitskraft))
            return $"Ein {arbeitskraft} arbeitet nicht in einer {b.Artdaten.Name} – hier lernen {Mehrzahl(l.Anfaenger)}.";
        return $"Eingestellt werden nur {Mehrzahl(l.Anfaenger)} – ein {arbeitskraft} wird man durch Beförderung.";
    }

    private static string Mehrzahl(string kraft) => kraft switch
    {
        "Lehrling" => "Lehrlinge",
        "Knecht" => "Knechte",
        "Taschendieb" => "Taschendiebe",
        "Rekrut" => "Rekruten",
        _ => kraft,
    };

    // ── Name und Aussehen ───────────────────────────────────────────────────

    /// <summary>
    /// Würfelt Name, Volk, Geschlecht, Aussehen und Alter eines neuen Arbeiters – fest aus Betrieb und Nummer, ohne andere Würfe zu verschieben.
    /// Orks (Arbeitskraft „Ork“) sind Orks; sonst mit <see cref="PersonalDaten.EigenesVolk"/> das Volk des Hauses, ansonsten eines der Völker
    /// mit Namen. Anfänger sind 14–18 Menschenjahre alt, Gesellen 20–29, Vorarbeiter 28–37, Arbeitskräfte ohne Stufen 18–32.
    /// </summary>
    internal void Steckbrief(Betrieb b, Arbeiter a)
    {
        var z = new Zufallsquelle(Startwert($"{b.Name}#{a.Nummer}"));
        var voelker = Namensvoelker();
        var haus = _w.Familien.Von(b.Konto)?.Oberhaupt.Volk;
        var eigen = z.NextDouble();
        var irgendeins = z.Next(1000);
        var frau = z.NextDouble() < (Daten?.Frauen ?? 0.15);
        Volk volk;
        if (Enum.TryParse<Volk>(a.Arbeitskraft, out var kraftVolk)) volk = kraftVolk; // „Ork“
        else if (haus is { } h && voelker.Contains(h) && eigen < (Daten?.EigenesVolk ?? 0.6)) volk = h;
        else
        {
            var freie = voelker.Where(v => v is not (Volk.Ork or Volk.Oger)).ToList();
            volk = freie.Count > 0 ? freie[irgendeins % freie.Count] : Volk.Mensch;
        }
        a.Volk = volk;
        a.Geschlecht = frau ? Geschlecht.Frau : Geschlecht.Mann;
        var (vornamen, familiennamen) = Namen(volk, a.Geschlecht);
        var vorname = z.Next(1000);
        var familienname = z.Next(1000);
        a.Name = vornamen.Count == 0 || familiennamen.Count == 0 ? ""
            : $"{vornamen[vorname % vornamen.Count]} {familiennamen[familienname % familiennamen.Count]}";
        a.Aussehen = Aussehenspalette.Wuerfeln(z, volk);
        var alter = z.Next(10);
        a.AlterBeimEinstellen = Stufe(a.Arbeitskraft) switch
        {
            1 => 14 + alter / 2,
            2 => 20 + alter,
            3 => 28 + alter,
            _ => 18 + alter * 1.5,
        };
    }

    /// <summary>Völker, für die es Namen gibt (dynastie.voelker und personal.namen).</summary>
    private List<Volk> Namensvoelker()
    {
        var liste = _w.Daten.Dynastie.Voelker.Keys.ToList();
        foreach (var v in Daten?.Namen.Keys ?? Enumerable.Empty<Volk>())
            if (!liste.Contains(v)) liste.Add(v);
        return liste.OrderBy(v => v).ToList();
    }

    private (IReadOnlyList<string> Vornamen, IReadOnlyList<string> Familiennamen) Namen(Volk volk, Geschlecht g)
    {
        if (_w.Daten.Dynastie.Voelker.TryGetValue(volk, out var v))
            return (g == Geschlecht.Frau ? v.VornamenFrau : v.VornamenMann, v.Familiennamen);
        if (Daten?.Namen.TryGetValue(volk, out var n) == true)
            return (g == Geschlecht.Frau ? n.VornamenFrau : n.VornamenMann, n.Familiennamen);
        return (Array.Empty<string>(), Array.Empty<string>());
    }

    /// <summary>Ein fester Startwert aus einem Text (FNV-1a – anders als <c>string.GetHashCode</c> in jedem Lauf gleich).</summary>
    internal static int Startwert(string text)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in text) h = (h ^ c) * 16777619u;
            return (int)h;
        }
    }

    // ── Beförderung ─────────────────────────────────────────────────────────

    /// <summary>Wozu er befördert würde (Name der Arbeitskraft der nächsten Stufe; null: Es gibt keine).</summary>
    public string? NaechsteStufe(Betrieb b, Arbeiter a)
    {
        if (Laufbahn(b) is not { } l) return null;
        var stufe = Stufe(a.Arbeitskraft);
        return stufe is 1 or 2 ? l.Stufen[stufe] : null;
    }

    /// <summary>Erfahrungsstunden, die er für die nächste Stufe braucht (null: Es gibt keine).</summary>
    public double? ErfahrungFuerNaechste(Betrieb b, Arbeiter a) =>
        Daten is { } d && NaechsteStufe(b, a) is not null ? d.Schwelle(Stufe(a.Arbeitskraft) + 1) : null;

    /// <summary>Was der Brief zur nächsten Stufe kostet (null: Es gibt keine).</summary>
    public double? BriefKosten(Betrieb b, Arbeiter a) =>
        Daten is { } d && NaechsteStufe(b, a) is not null ? d.Brief(Stufe(a.Arbeitskraft) + 1) : null;

    /// <summary>
    /// Erfahrungsstunden, die er bei der jetzigen Arbeit in einem Spieljahr sammelt: Tempo × Laune × Arbeitsstunden, als Anfänger
    /// × <see cref="Betrieb.AnfaengerErfahrungFaktor"/>.
    /// </summary>
    public double ErfahrungJeJahr(Betrieb b, Arbeiter a)
    {
        var stufe = Stufe(a.Arbeitskraft);
        if (stufe == 0) return 0;
        return _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort) * b.TempoFaktor(a) * b.Arbeitsminuten / 60.0
               * (stufe == 1 ? b.AnfaengerErfahrungFaktor : 1);
    }

    /// <summary>In wie vielen Spieljahren er bei der jetzigen Arbeit die nächste Stufe erreichen kann (0: schon jetzt; null: keine nächste Stufe).</summary>
    public double? JahreBisNaechste(Betrieb b, Arbeiter a)
    {
        if (ErfahrungFuerNaechste(b, a) is not { } noetig) return null;
        var rest = noetig - a.Erfahrung;
        if (rest <= 0) return 0;
        var jeJahr = ErfahrungJeJahr(b, a);
        return jeJahr > 0 ? rest / jeJahr : null;
    }

    /// <summary>Die Arbeiter der dritten Stufe in diesem Betrieb (Vorarbeiter, Großknecht, Meisterdieb, Feldwebel).</summary>
    public IEnumerable<Arbeiter> Vorarbeiter(Betrieb b) => b.Arbeiter.Where(a => Stufe(a.Arbeitskraft) == 3);

    /// <summary>Warum er (noch) nicht befördert werden kann – null: Es geht (Erfahrung, Brief, Statut, höchstens ein Vorarbeiter).</summary>
    public string? BefoerdernVerboten(Betrieb b, int nummer)
    {
        if (Daten is not { } d) return _w.Werkstaetten.BefoerdernVerboten(b, nummer);
        if (b.ArbeiterNr(nummer) is not { } a) return $"In {b.Name} gibt es keinen Arbeiter Nr. {nummer}.";
        if (Laufbahn(b) is null) return $"In {b.Name} wird nicht befördert.";
        var wer = Wer(a);
        var stufe = Stufe(a.Arbeitskraft);
        if (stufe == 0) return $"{wer} ist {a.Arbeitskraft} – Unfreie und Orks haben keine Stufen.";
        if (NaechsteStufe(b, a) is not { } zu) return $"{wer} ist schon {a.Arbeitskraft} – höher geht es nicht.";
        if (!_w.Daten.Arbeitskraefte.TryGetValue(zu, out var kraft) || !kraft.IstErlaubt(_w.Statut, b.Ort))
            return $"Ein {zu} darf hier nicht arbeiten.";
        var noetig = d.Schwelle(stufe + 1);
        if (a.Erfahrung < noetig)
        {
            var jahre = JahreBisNaechste(b, a);
            return $"{wer} braucht {Zahlenformat.Ganz(noetig)} Stunden Erfahrung für den {zu}, hat {Zahlenformat.Ganz(Math.Floor(a.Erfahrung))}" +
                   (jahre is { } j ? $" – noch etwa {Jahre(j)}." : ".");
        }
        if (stufe + 1 == 3 && Vorarbeiter(b).Count() >= d.VorarbeiterJeBetrieb)
            return d.VorarbeiterJeBetrieb == 1
                ? $"{b.Name} hat schon einen {zu}: {Wer(Vorarbeiter(b).First())}."
                : $"{b.Name} hat schon {d.VorarbeiterJeBetrieb}× {zu}.";
        var brief = d.Brief(stufe + 1);
        if (b.Konto.Vermoegen < brief) return $"Der Brief zum {zu} kostet {Zahlenformat.Ganz(brief)} {Waehrung}.";
        return null;
    }

    internal Befehlsergebnis Befoerdern(Betrieb b, ArbeiterBefoerdern befehl)
    {
        if (Daten is not { } d) return _w.Werkstaetten.Befoerdern(b, befehl);
        if (BefoerdernVerboten(b, befehl.Arbeiter) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var a = b.ArbeiterNr(befehl.Arbeiter)!;
        var wer = Wer(a);
        var stufe = Stufe(a.Arbeitskraft) + 1;
        var zu = NaechsteStufe(b, a)!;
        var brief = d.Brief(stufe);
        if (brief > 0) b.PersonalBuchen(brief);
        b.Befoerdern(a, zu);
        var lohn = b.LohnJeArbeiter(zu);
        var text = $"{b.Name}: {wer} ist nun {zu} (Brief {Zahlenformat.Ganz(brief)} {Waehrung}, Lohn ab jetzt {Zahlenformat.Ganz(lohn)} {Waehrung} im Jahr).";
        if (stufe == 3)
        {
            var wahl = EigenschaftenZiehen(b, a);
            b.EigenschaftSetzen(a, null, wahl);
            text += $" Zur Wahl stehen: {string.Join(", ", wahl.Select(k => b.EigenschaftVon(k)?.Name ?? k))}.";
        }
        _w.Melden(text);
        return Befehlsergebnis.Ok(text);
    }

    /// <summary>
    /// Die drei Eigenschaften zur Wahl: aus jeder Gruppe der Laufbahn eine. Der Generator startet fest aus Betrieb, Nummer und Name –
    /// dieselbe Person zieht immer dieselben (kein Neuwürfeln durch Laden).
    /// </summary>
    internal IReadOnlyList<string> EigenschaftenZiehen(Betrieb b, Arbeiter a)
    {
        if (Laufbahn(b) is not { } l) return Array.Empty<string>();
        var z = new Zufallsquelle(Startwert($"{b.Name}#{a.Nummer}#{a.Name}#Eigenschaft"));
        return l.Eigenschaften.Select(g => g.Stuecke[z.Next(g.Stuecke.Count)].Kennung).ToList();
    }

    public string? EigenschaftWaehlenVerboten(Betrieb b, int nummer, string kennung)
    {
        if (Daten is null) return "In dieser Stadt haben Vorarbeiter keine Eigenschaften.";
        if (b.ArbeiterNr(nummer) is not { } a) return $"In {b.Name} gibt es keinen Arbeiter Nr. {nummer}.";
        if (a.EigenschaftWahl is not { } wahl) return $"{Wer(a)} hat nichts zu wählen.";
        if (!wahl.Contains(kennung)) return $"„{kennung}“ steht für {Wer(a)} nicht zur Wahl ({string.Join(", ", wahl)}).";
        return null;
    }

    internal Befehlsergebnis EigenschaftWaehlen(Betrieb b, EigenschaftWaehlen befehl)
    {
        if (EigenschaftWaehlenVerboten(b, befehl.Arbeiter, befehl.Eigenschaft) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var a = b.ArbeiterNr(befehl.Arbeiter)!;
        b.EigenschaftSetzen(a, befehl.Eigenschaft);
        var e = b.EigenschaftVon(befehl.Eigenschaft)!;
        return Befehlsergebnis.Ok($"{b.Name}: {Wer(a)} ist {e.Name} – {e.Wirkung.Text()}.");
    }

    /// <summary>„Hans Bauer (Lehrling Nr. 2)“ – oder ohne Namen „Lehrling Nr. 2“.</summary>
    public static string Wer(Arbeiter a) => a.Name == "" ? $"{a.Arbeitskraft} Nr. {a.Nummer}" : $"{a.Name} ({a.Arbeitskraft} Nr. {a.Nummer})";

    private static string Jahre(double jahre)
    {
        var text = jahre < 0.95 ? Zahlenformat.Kommazahl(jahre) : Zahlenformat.Ganz(Math.Round(jahre));
        return $"{text} {(text == "1" ? "Jahr" : "Jahre")}";
    }

    // ── Abfragen ────────────────────────────────────────────────────────────

    /// <summary>Eine Eigenschaft für die Oberfläche (aus der Laufbahn des Betriebs oder irgendeiner; null, wenn es sie nicht gibt).</summary>
    public EigenschaftZeile? Eigenschaft(Betrieb b, string kennung)
    {
        if (b.EigenschaftVon(kennung) is not { } e) return null;
        var gruppe = Laufbahn(b)?.GruppeVon(kennung) ?? Daten?.Laufbahnen.Select(l => l.GruppeVon(kennung)).FirstOrDefault(g => g is not null) ?? "";
        return new EigenschaftZeile(e.Kennung, e.Name, gruppe, e.Wirkung.Text());
    }

    /// <summary>Alle Eigenschaften, die ein Vorarbeiter dieses Betriebs haben kann, nach Gruppen (Ware, Führung, Betrieb).</summary>
    public IReadOnlyList<EigenschaftZeile> Eigenschaften(Betrieb b) =>
        Laufbahn(b)?.Eigenschaften.SelectMany(g => g.Stuecke.Select(e => new EigenschaftZeile(e.Kennung, e.Name, g.Gruppe, e.Wirkung.Text()))).ToList()
        ?? (IReadOnlyList<EigenschaftZeile>)Array.Empty<EigenschaftZeile>();

    // ── Was eine Eigenschaft einbringt (für die KI) ─────────────────────────

    /// <summary>
    /// Was diese Eigenschaft dem Betrieb im Jahr einbringt, mit den Arbeitern und Waren von jetzt (<see cref="Werkstattwesen.MehrgewinnDurch"/>,
    /// wie bei der Einrichtung). Ohne Gulden-Wirkung (Lager, Razzia, Spuren …) 0.
    /// </summary>
    public double MehrgewinnJeJahr(Betrieb b, string kennung) =>
        b.EigenschaftVon(kennung) is { } e ? _w.Werkstaetten.MehrgewinnDurch(b, e.Wirkung) : 0;

    /// <summary>
    /// Was ein Vorarbeiter diesem Betrieb im Mittel einbrächte: der Erwartungswert der besten der drei gezogenen Eigenschaften (je Gruppe
    /// gleich wahrscheinlich) – für die KI, ob sich der Vorarbeiterbrief lohnt. Sie kennt die Ziehung nicht vorher.
    /// </summary>
    public double ErwarteterVorarbeiterGewinn(Betrieb b)
    {
        if (Laufbahn(b) is not { } l || l.Eigenschaften.Count == 0) return 0;
        var gruppen = l.Eigenschaften.Select(g => g.Stuecke.Select(e => _w.Werkstaetten.MehrgewinnDurch(b, e.Wirkung)).ToList()).ToList();
        // Erwartungswert des Höchsten: über alle Kombinationen (höchstens eine Handvoll je Gruppe).
        var summe = 0.0;
        var anzahl = 0;
        void Durch(int i, double hoechst)
        {
            if (i == gruppen.Count) { summe += hoechst; anzahl++; return; }
            foreach (var wert in gruppen[i]) Durch(i + 1, Math.Max(hoechst, wert));
        }
        Durch(0, 0);
        return anzahl > 0 ? summe / anzahl : 0;
    }
}
