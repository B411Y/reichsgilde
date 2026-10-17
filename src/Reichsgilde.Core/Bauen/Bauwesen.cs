using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Bauen;

/// <summary>Was auf einem Grundstück steht (oder entsteht).</summary>
public enum Grundstuecksart
{
    /// <summary>Wohnhaus eines Hauses – bewohnt, leerstehend oder im Bau.</summary>
    Wohnhaus,

    /// <summary>Eine neu gebaute Werkstatt (die Werkstätten der Karte stehen in <see cref="Stadtkarte.Feste"/>).</summary>
    Werkstatt,

    /// <summary>Freier Bauplatz nach einem Abriss – Wiese mit Pflöcken, für jeden bebaubar.</summary>
    Bauplatz,
}

/// <summary>Was auf einer Baustelle gerade geschieht.</summary>
public enum Bauarbeit
{
    Neubau,

    /// <summary>Ein gekaufter Leerstand wird hergerichtet; danach zieht der Käufer ein.</summary>
    Sanierung,

    /// <summary>Ein gekaufter Leerstand wird abgerissen; danach ist hier ein Bauplatz.</summary>
    Abriss,
}

/// <summary>Zustand eines Grundstücks für die Darstellung.</summary>
public enum Bauzustand
{
    Bewohnt,

    /// <summary>Das Haus ist bankrott oder erloschen – vernagelt, zu kaufen oder abzureißen.</summary>
    Leerstand,

    /// <summary>Eine fertige, neu gebaute Werkstatt.</summary>
    Werkstatt,

    /// <summary>Gerüst und Holzstapel: Neubau, Sanierung oder Abriss läuft bis <see cref="Grundstueck.FertigAb"/>.</summary>
    Baustelle,

    Bauplatz,
}

/// <summary>
/// Ein Grundstück, das der Kern kennt: die Wohnhäuser der Häuser (Startplätze aus der Karte und Neubauten),
/// neu gebaute Werkstätten und Bauplätze nach Abriss. Die Darstellung liest Lage, Größe, Drehung und Zustand.
/// </summary>
public sealed class Grundstueck
{
    internal Grundstueck(int nummer, Grundriss grundriss, Grundstuecksart art, Lage? lage, string? kennung)
    {
        Nummer = nummer;
        Grundriss = grundriss;
        Art = art;
        Lage = lage;
        Kennung = kennung;
    }

    /// <summary>Feste Nummer – bleibt über Speichern und Laden gleich.</summary>
    public int Nummer { get; }

    public Grundriss Grundriss { get; }
    public Grundstuecksart Art { get; internal set; }

    /// <summary>In welcher Bauzone es liegt (bzw. die nächste) – für die Frage, was auf dem Bauplatz später stehen darf.</summary>
    public Lage? Lage { get; }

    /// <summary>Kennung des Wohnhausplatzes in der Karte, wenn es ein Startplatz ist (sonst null).</summary>
    public string? Kennung { get; }

    /// <summary>
    /// Wohnhaus: wer darin wohnt (im Leerstand das bankrotte oder erloschene Haus) bzw. wer nach dem Bau oder der Sanierung einzieht.
    /// Abriss: wer abreißen lässt. Werkstatt im Bau: der Bauherr (danach gehört sie dem Betrieb – <see cref="Betrieb"/>). Bauplatz: null.
    /// </summary>
    public Konto? Besitzer { get; internal set; }

    internal int GespeicherteStufe { get; set; }

    /// <summary>Stufe des Wohnhauses (0 = Fachwerkhaus …): bewohnt die des Bewohners, auf der Baustelle die, die entsteht bzw. abgerissen wird.</summary>
    public int Stufe => Art == Grundstuecksart.Wohnhaus && Arbeit is null && Besitzer is { } b ? b.WohnhausStufe : GespeicherteStufe;

    /// <summary>Werkstatt: Name des Betriebs (schon während des Baus fest).</summary>
    public string? Betrieb { get; internal set; }

    /// <summary>Werkstatt: welche Betriebsart hier entsteht bzw. entstand (Stufe 1).</summary>
    public Betriebsart? Betriebsart { get; internal set; }

    /// <summary>Was auf der Baustelle geschieht (null = keine Baustelle).</summary>
    public Bauarbeit? Arbeit { get; internal set; }

    /// <summary>Ab dieser Spielminute ist die Baustelle fertig (null = keine Baustelle).</summary>
    public long? FertigAb { get; internal set; }

    public Bauzustand Zustand =>
        Arbeit is not null ? Bauzustand.Baustelle
        : Art switch
        {
            Grundstuecksart.Bauplatz => Bauzustand.Bauplatz,
            Grundstuecksart.Werkstatt => Bauzustand.Werkstatt,
            _ => Besitzer is { IstAktiv: true } ? Bauzustand.Bewohnt : Bauzustand.Leerstand,
        };
}

/// <summary>
/// Was ein Neubau kosten würde und ob er hier geht – für die Vorschau im Baumodus (grün/rot mit Grund) und die Rückfrage.
/// </summary>
/// <param name="Grundriss">Die Fläche, die das Gebäude belegen würde (inkl. Vorplatz).</param>
/// <param name="Gebaeude">Was gebaut wird, z. B. „Bürgerhaus“ oder „Gießerei“.</param>
/// <param name="Betrieb">Werkstatt: der Name, den sie hier bekäme (z. B. „Gießerei an der Ostgasse“); Wohnhaus: null.</param>
/// <param name="Preis">Wohnhaus: Hauswert der Zielstufe; Werkstatt: Preis einer freien Werkstatt der Stufe 1.</param>
/// <param name="Anrechnung">Wohnhaus: Hauswert des alten Hauses, der angerechnet wird (es wird nach dem Umzug abgerissen); Werkstatt: 0.</param>
/// <param name="Kosten">Was beim Baubeginn bezahlt wird: Preis − Anrechnung, nie unter 0.</param>
/// <param name="Unterhalt">Werkstatt: ein Jahr Unterhalt, der zusätzlich bar da sein muss (wie beim Kauf); Wohnhaus: jährlicher Unterhalt.</param>
/// <param name="BauzeitMinuten">Bauzeit in Spielminuten (1 Spieljahr = 1.020).</param>
/// <param name="Verboten">Warum es nicht geht – oder null.</param>
public sealed record Bauvorschau(
    Grundriss Grundriss, string Gebaeude, string? Betrieb, double Preis, double Anrechnung, double Kosten, double Unterhalt,
    int BauzeitMinuten, string? Verboten);

/// <summary>Was Kauf oder Abriss eines leerstehenden Wohnhauses kosten würde – für die Tafel mit „Kaufen“ und „Abreißen“.</summary>
/// <param name="Haus">Das bankrotte oder erloschene Haus, dem der Leerstand gehörte.</param>
/// <param name="Gebaeude">Stufe des Hauses, z. B. „Steinhaus“.</param>
/// <param name="Stufe">Stufe als Zahl (0 = Fachwerkhaus).</param>
/// <param name="Hauswert">Summe der Ausbaupreise bis zu dieser Stufe.</param>
/// <param name="Kaufpreis">Kauf (50 % des Hauswerts).</param>
/// <param name="Sanierung">Sanierung nach dem Kauf (20 %).</param>
/// <param name="Anrechnung">Hauswert des eigenen Hauses – es wird nach dem Umzug abgerissen.</param>
/// <param name="KaufSumme">Kauf + Sanierung − Anrechnung, nie unter 0 (keine Auszahlung).</param>
/// <param name="SanierungMinuten">So lange wird saniert; danach zieht der Käufer ein.</param>
/// <param name="Abriss">Abrisskosten (10 %, mindestens 100).</param>
/// <param name="AbrissSumme">Kauf + Abriss.</param>
/// <param name="AbrissMinuten">So lange dauert der Abriss; danach ist hier ein Bauplatz.</param>
/// <param name="KaufVerboten">Warum der Kauf nicht geht – oder null.</param>
/// <param name="AbrissVerboten">Warum der Abriss nicht geht – oder null.</param>
public sealed record Leerstandsvorschau(
    string Haus, string Gebaeude, int Stufe, double Hauswert, double Kaufpreis, double Sanierung, double Anrechnung, double KaufSumme,
    int SanierungMinuten, double Abriss, double AbrissSumme, int AbrissMinuten, string? KaufVerboten, string? AbrissVerboten);

/// <summary>
/// Bauen in der Stadt (Entscheidungen des Nutzers vom 17.10.2026, LEVELDESIGN §3 und §4):
///
///   • <b>Grundstücke:</b> Jedes Haus wohnt auf einem Platz der Karte (Startplätze in Konto-Reihenfolge). Neubauten und
///     Abrisse ändern das; Bauplätze nach Abriss sind für jeden bebaubar – auch außerhalb der Bauzonen.
///   • <b>Bauprüfung:</b> Raster 2 m, Drehung in Vierteln, die Fläche (inkl. Vorplatz) liegt ganz in passenden Bauzonen oder auf Bauplätzen,
///     überdeckt nichts, und die Vorderseite liegt an einer passenden Straße (Werkstatt: Karrenstraße; Wohnhaus: auch Gasse).
///   • <b>Leerstand</b> (Wohnhaus eines bankrotten oder erloschenen Hauses) kaufen: 50 % + 20 % Sanierung − Hauswert des eigenen Hauses,
///     4 Stunden, dann Umzug; das alte Haus wird Bauplatz. Oder abreißen: 50 % + 10 %, danach Bauplatz.
///   • <b>Neubau:</b> Wohnhaus zum Hauswert der Zielstufe (das alte angerechnet, nach dem Umzug abgerissen) oder Werkstatt Stufe 1 zum
///     Preis einer freien Werkstatt. Bauzeit 1 bzw. 2 Spieljahre; bezahlt wird beim Baubeginn, während des Baus fallen keine Kosten an.
///
/// Ohne Karte, ohne Abschnitt „bauen“ oder ohne Wohnhäuser ist das Bauwesen aus – alles wie vorher.
/// </summary>
public sealed class Bauwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly List<Grundstueck> _grundstuecke = new();
    private int _naechsteNummer = 1;

    public BauDaten? Daten { get; }

    internal Bauwesen(Stadtwirtschaft w, BauDaten? daten)
    {
        _w = w;
        Daten = daten;
    }

    /// <summary>Die Karte der Stadt (wie <see cref="Stadtwirtschaft.Karte"/>).</summary>
    public Stadtkarte? Karte => _w.Karte;

    /// <summary>Wird gebaut? Dafür braucht es die Karte, den Abschnitt „bauen“ und Wohnhäuser.</summary>
    public bool IstAktiv => Daten is not null && Karte is not null && _w.Wohnen.IstAktiv;

    /// <summary>Alle Grundstücke, nach Nummer.</summary>
    public IReadOnlyList<Grundstueck> Grundstuecke => _grundstuecke;

    /// <summary>Ein Neubau, eine Sanierung oder ein Abriss hat begonnen (auch ein neues Grundstück).</summary>
    public event Action<Grundstueck>? BauBegonnen;

    /// <summary>Eine Baustelle ist fertig: Umzug ins neue Wohnhaus bzw. die Werkstatt öffnet (dazu <see cref="Stadtwirtschaft.BetriebErworben"/>).</summary>
    public event Action<Grundstueck>? BauFertig;

    /// <summary>Ein Haus wurde abgerissen – das Grundstück ist jetzt ein Bauplatz (nach Abriss oder nach dem Umzug aus dem alten Haus).</summary>
    public event Action<Grundstueck>? Abgerissen;

    /// <summary>Ein Bauplatz ist durch einen Neubau belegt und verschwindet aus <see cref="Grundstuecke"/>.</summary>
    public event Action<Grundstueck>? BauplatzBebaut;

    // ── Auskunft ─────────────────────────────────────────────────────────────

    /// <summary>Das Wohnhaus, in dem das Haus wohnt (bzw. das leersteht, wenn es bankrott oder erloschen ist) – null ohne Platz.</summary>
    public Grundstueck? Wohnhaus(Konto k) =>
        _grundstuecke.FirstOrDefault(g => g.Art == Grundstuecksart.Wohnhaus && g.Besitzer == k && g.Arbeit is null);

    /// <summary>Das Wohnhaus, das für dieses Haus gebaut oder saniert wird (null: keins).</summary>
    public Grundstueck? Umzug(Konto k) =>
        _grundstuecke.FirstOrDefault(g => g.Art == Grundstuecksart.Wohnhaus && g.Besitzer == k && g.Arbeit is Bauarbeit.Neubau or Bauarbeit.Sanierung);

    /// <summary>Das Grundstück einer neu gebauten (oder im Bau befindlichen) Werkstatt – null für die Werkstätten der Karte.</summary>
    public Grundstueck? Werkstatt(string betrieb) =>
        _grundstuecke.FirstOrDefault(g => g.Art == Grundstuecksart.Werkstatt && g.Betrieb == betrieb);

    /// <summary>Freie Bauplätze nach Abriss.</summary>
    public IEnumerable<Grundstueck> Bauplaetze => _grundstuecke.Where(g => g.Art == Grundstuecksart.Bauplatz);

    /// <summary>Das leerstehende Wohnhaus eines bankrotten oder erloschenen Hauses (null: lebt noch, schon verkauft oder kein Platz).</summary>
    public Grundstueck? Leerstand(Konto leer) => !leer.IstAktiv ? Wohnhaus(leer) : null;

    /// <summary>Die Stufe, die das Haus nach dem laufenden Neubau oder der Sanierung bewohnt (null: kein Umzug).</summary>
    public int? UmzugZiel(Konto k) => Umzug(k)?.GespeicherteStufe;

    /// <summary>Ein bankrottes oder erloschenes Haus, dessen Wohnhaus verkauft oder abgerissen ist: kein Hauswert, kein Leerstand.</summary>
    internal bool HausAufgegeben(Konto k) => IstAktiv && !k.IstAktiv && Wohnhaus(k) is null && Umzug(k) is null;

    /// <summary>Größe, Bauzeit und Lagen des Gebäudes, das der Befehl bauen will (null: unbekannte Stufe oder Art).</summary>
    public Gebaeudemass? Mass(BauBefehl befehl) => Daten is not { } d ? null : befehl switch
    {
        WohnhausBauen w when w.Stufe >= 0 && w.Stufe < d.Wohnhaus.Count => d.Wohnhaus[w.Stufe],
        WerkstattBauen => d.Werkstatt,
        _ => null,
    };

    /// <summary>Die Fläche, die das Gebäude an dieser Stelle belegen würde (inkl. Vorplatz).</summary>
    public Grundriss Grundriss(BauBefehl befehl)
    {
        var m = Mass(befehl);
        return new Grundriss(befehl.X, befehl.Z, m?.Breite ?? 0, m?.Tiefe ?? 0, ((befehl.Drehung % 4) + 4) % 4);
    }

    /// <summary>
    /// Rastet die Mausposition ein: auf einen Bauplatz, auf dem das Gebäude ganz Platz hat (mit der Vorderkante bündig, wenn es
    /// gleich gedreht ist), sonst aufs 2-m-Raster. Gibt den Befehl mit den eingerasteten Koordinaten zurück.
    /// </summary>
    public BauBefehl Einrasten(BauBefehl befehl)
    {
        if (Daten is not { } d || Mass(befehl) is null) return befehl;
        var g = Grundriss(befehl);
        foreach (var platz in Bauplaetze)
        {
            var p = platz.Grundriss;
            if (!p.Enthaelt(befehl.X, befehl.Z) || g.HalbX > p.HalbX + 1e-6 || g.HalbZ > p.HalbZ + 1e-6) continue;
            var (x, z) = (p.X, p.Z);
            if (g.Drehung == p.Drehung) // Vorderkanten bündig – der Vorplatz bleibt an der Straße
                (x, z) = (p.Eingang.X - g.Vorne.X * g.Tiefe / 2, p.Eingang.Z - g.Vorne.Z * g.Tiefe / 2);
            return befehl with { X = x, Z = z };
        }
        double Rasten(double mitte, double halb) => Math.Round((mitte - halb) / d.Raster) * d.Raster + halb;
        return befehl with { X = Rasten(befehl.X, g.HalbX), Z = Rasten(befehl.Z, g.HalbZ) };
    }

    // ── Prüfen ───────────────────────────────────────────────────────────────

    /// <summary>Warum dieses Haus hier nicht bauen darf (Bauherr und Platz) – oder null.</summary>
    public string? BauVerboten(Konto konto, BauBefehl befehl) => BauherrVerboten(konto, befehl) ?? PlatzVerboten(befehl);

    /// <summary>Warum dieses Haus das Gebäude gar nicht bauen darf (Stand, Handwerk, Geld …), egal wo – oder null. Für die Auswahl im Baumenü.</summary>
    public string? BauherrVerboten(Konto konto, BauBefehl befehl)
    {
        if (!IstAktiv) return "In dieser Stadt wird nicht gebaut.";
        if (!konto.IstAktiv) return $"{konto.Inhaber} ist nicht mehr im Spiel.";
        var waehrung = _w.Daten.Allgemein.Waehrung;
        switch (befehl)
        {
            case WohnhausBauen w:
            {
                var stufen = _w.Wohnen.Daten!.Stufen;
                if (w.Stufe < 0 || w.Stufe >= stufen.Count) return $"Eine Wohnhaus-Stufe {w.Stufe} gibt es nicht.";
                var ziel = stufen[w.Stufe];
                if (UmzugVerboten(konto) is { } umzug) return umzug;
                if (w.Stufe < konto.WohnhausStufe)
                    return $"{konto.Inhaber} wohnt schon in einem {_w.Wohnen.Stufe(konto)!.Name} – kleiner bauen geht nicht.";
                if (StandFehlt(konto, w.Stufe) is { } stand) return $"Ein {ziel.Name} darf erst bauen, wer mindestens {stand} ist.";
                var kosten = WohnhausKosten(konto, w.Stufe);
                if (konto.Vermoegen < kosten)
                    return $"Der Neubau ({ziel.Name}) kostet {Zahlenformat.Ganz(kosten)} {waehrung} – es fehlen {Zahlenformat.Ganz(kosten - konto.Vermoegen)}.";
                return null;
            }
            case WerkstattBauen b:
            {
                if (!_w.Daten.KenntArt(b.Art)) return $"Eine Betriebsart „{b.Art}“ gibt es nicht.";
                var art = _w.Daten.Art(b.Art);
                if (!art.InDerStadt) return $"Eine {art.Name} liegt vor der Stadt – dort wird gepachtet, nicht gebaut.";
                if (art.Stufe != 1) return $"Neu gebaut wird nur Stufe 1 – eine {art.Name} entsteht durch Ausbau.";
                var mindestStand = MindestStand(b.Art);
                if (konto.Stand < _w.Daten.StandStufe(mindestStand))
                    return $"Eine {art.Name} darf erst bauen, wer mindestens {mindestStand} ist – {konto.Inhaber} ist {_w.StandVon(konto).Name}.";
                if (_w.Daten.Berufe.Count > 0 && konto.Berufe.Count > 0 && !konto.Beherrscht(art.Beruf))
                    return $"Eine {art.Name} ist ein Betrieb der {art.Beruf}. {konto.Inhaber} beherrscht dieses Handwerk nicht – erst erlernen.";
                var preis = WerkstattPreis(b.Art);
                if (konto.Vermoegen < preis + art.Fixkosten)
                    return $"Für eine neue {art.Name} braucht es {Zahlenformat.Ganz(preis + art.Fixkosten)} {waehrung} " +
                           $"(Bau {Zahlenformat.Ganz(preis)} und ein Jahr Unterhalt).";
                return null;
            }
            default:
                return "Unbekannter Bauauftrag.";
        }
    }

    /// <summary>Warum das Gebäude an dieser Stelle nicht stehen kann (Bauzone, Überdeckung, Straße) – oder null. Für die rote Vorschau.</summary>
    public string? PlatzVerboten(BauBefehl befehl)
    {
        if (!IstAktiv) return "In dieser Stadt wird nicht gebaut.";
        if (Mass(befehl) is not { } mass) return "Dieses Gebäude kann man nicht bauen.";
        if (befehl.Drehung is < 0 or > 3) return "Gedreht wird in Vierteln: Drehung 0 bis 3.";
        var d = Daten!;
        var karte = Karte!;
        var g = Grundriss(befehl);
        var name = GebaeudeName(befehl);
        var felder = g.Felder(d.Raster).ToList();
        var bauplaetze = Bauplaetze.ToList();

        Grundstueck? BauplatzBei((double X, double Z) f) => bauplaetze.FirstOrDefault(p => p.Grundriss.Enthaelt(f.X, f.Z));
        if (!g.AufDemRaster(d.Raster) && !felder.All(f => BauplatzBei(f) is not null))
            return $"Gebaut wird auf dem Raster von {d.Raster:0.#} m – oder genau auf einem Bauplatz.";

        foreach (var anderes in _grundstuecke.Where(x => x.Art != Grundstuecksart.Bauplatz))
            if (g.Ueberlappt(anderes.Grundriss))
                return $"Hier steht schon {Bezeichnung(anderes)}.";
        foreach (var fest in karte.Feste)
        {
            if (fest.Art == "wohnhaus" && _grundstuecke.Any(x => x.Kennung == fest.Kennung)) continue; // vergebener Startplatz: als Grundstück geprüft
            if (fest.Grundriss is { } fg && g.Ueberlappt(fg))
                return $"Hier steht {(fest.Name != "" ? fest.Name : "ein fester Bau")}.";
        }

        foreach (var feld in felder)
        {
            var lage = BauplatzBei(feld)?.Lage ?? karte.Bauzonen.FirstOrDefault(z => z.Enthaelt(feld.X, feld.Z))?.Art;
            if (lage is null) return "Hier ist keine Bauzone – gebaut wird nur in den Bauzonen und auf Bauplätzen.";
            if (!mass.Lagen.Contains(lage.Value))
                return lage == Lage.Umland ? "Im Umland wird nicht gebaut." : $"Ein {name} steht nur {LagenText(mass.Lagen)}.";
        }

        var werkstatt = befehl is WerkstattBauen;
        var wege = karte.Wege.Where(w => w.IstKarrenstrasse || !werkstatt && w.Klasse == Wegklasse.Gasse).ToList();
        var vorplatz = g.Felder(d.Raster, nurVorplatz: true).ToList();
        var anDerStrasse = vorplatz.Count(f => wege.Any(w => w.Abstand(f.X, f.Z) <= d.StrassenAbstand));
        if (anDerStrasse < Math.Min(2, vorplatz.Count))
            return werkstatt
                ? "Die Vorderseite einer Werkstatt muss an einer Karrenstraße liegen (Haupt-, Ring- oder Wallstraße)."
                : "Die Vorderseite eines Wohnhauses muss an einer Straße oder Gasse liegen.";
        return null;
    }

    /// <summary>Was der Neubau kosten würde und ob er hier geht.</summary>
    public Bauvorschau Vorschau(Konto konto, BauBefehl befehl)
    {
        var g = Grundriss(befehl);
        var mass = Mass(befehl);
        var bauzeit = mass is null ? 0 : (int)Math.Round(mass.BauzeitJahre * Spielzeit.MinutenProTag);
        var verboten = BauVerboten(konto, befehl);
        switch (befehl)
        {
            case WohnhausBauen w when _w.Wohnen.Daten is { } wd && w.Stufe >= 0 && w.Stufe < wd.Stufen.Count:
            {
                var preis = _w.Wohnen.WertDerStufe(w.Stufe);
                var anrechnung = _w.Wohnen.Hauswert(konto);
                return new Bauvorschau(g, wd.Stufen[w.Stufe].Name, null, preis, anrechnung, Math.Max(0, preis - anrechnung),
                    wd.Stufen[w.Stufe].Unterhalt, bauzeit, verboten);
            }
            case WerkstattBauen b when _w.Daten.KenntArt(b.Art):
            {
                var preis = WerkstattPreis(b.Art);
                return new Bauvorschau(g, _w.Daten.Art(b.Art).Name, IstAktiv ? NeuerName(b.Art, g) : null, preis, 0, preis,
                    _w.Daten.Art(b.Art).Fixkosten, bauzeit, verboten);
            }
            default:
                return new Bauvorschau(g, "", null, 0, 0, 0, 0, 0, verboten ?? "Dieses Gebäude kann man nicht bauen.");
        }
    }

    /// <summary>Warum dieses Haus den Leerstand nicht kaufen kann – oder null.</summary>
    public string? LeerstandKaufVerboten(Konto konto, Konto leer)
    {
        if (LeerstandVerboten(konto, leer) is { } grund) return grund;
        if (UmzugVerboten(konto) is { } umzug) return umzug;
        var stufe = leer.WohnhausStufe;
        var name = _w.Wohnen.Daten!.Stufen[stufe].Name;
        if (stufe <= konto.WohnhausStufe)
            return $"Das {name} ist nicht größer als das {_w.Wohnen.Stufe(konto)!.Name} von {konto.Inhaber} – kaufen lohnt nur ein größeres Haus.";
        if (StandFehlt(konto, stufe) is { } stand) return $"Ein {name} darf erst beziehen, wer mindestens {stand} ist.";
        var summe = Leerstandspreise(konto, leer).KaufSumme;
        if (konto.Vermoegen < summe)
            return $"Kauf und Sanierung kosten {Zahlenformat.Ganz(summe)} {_w.Daten.Allgemein.Waehrung} – es fehlen {Zahlenformat.Ganz(summe - konto.Vermoegen)}.";
        return null;
    }

    /// <summary>Warum dieses Haus den Leerstand nicht kaufen und abreißen lassen kann – oder null.</summary>
    public string? LeerstandAbrissVerboten(Konto konto, Konto leer)
    {
        if (LeerstandVerboten(konto, leer) is { } grund) return grund;
        var summe = Leerstandspreise(konto, leer).AbrissSumme;
        if (konto.Vermoegen < summe)
            return $"Kauf und Abriss kosten {Zahlenformat.Ganz(summe)} {_w.Daten.Allgemein.Waehrung} – es fehlen {Zahlenformat.Ganz(summe - konto.Vermoegen)}.";
        return null;
    }

    /// <summary>Was Kauf und Abriss dieses Leerstands kosten würden – und ob sie gehen.</summary>
    public Leerstandsvorschau LeerstandVorschau(Konto konto, Konto leer) =>
        Leerstandspreise(konto, leer) with { KaufVerboten = LeerstandKaufVerboten(konto, leer), AbrissVerboten = LeerstandAbrissVerboten(konto, leer) };

    private Leerstandsvorschau Leerstandspreise(Konto konto, Konto leer)
    {
        var stufe = leer.WohnhausStufe;
        var stufen = _w.Wohnen.Daten?.Stufen;
        var name = stufen is null ? "" : stufen[Math.Clamp(stufe, 0, stufen.Count - 1)].Name;
        var l = Daten?.Leerstand ?? new LeerstandDaten();
        var wert = _w.Wohnen.WertDerStufe(stufe);
        var anrechnung = _w.Wohnen.Hauswert(konto);
        var kauf = wert * l.Kauf;
        var sanierung = wert * l.Sanierung;
        var abriss = Math.Max(l.AbrissMindestens, wert * l.Abriss);
        return new Leerstandsvorschau(leer.Inhaber, name, stufe, wert, kauf, sanierung, anrechnung, Math.Max(0, kauf + sanierung - anrechnung),
            _w.Wohnen.Daten?.BauzeitMinuten ?? 0, abriss, kauf + abriss, l.AbrissMinuten, null, null);
    }

    private string? LeerstandVerboten(Konto konto, Konto leer)
    {
        if (!IstAktiv) return "In dieser Stadt wird nicht gebaut.";
        if (!konto.IstAktiv) return $"{konto.Inhaber} ist nicht mehr im Spiel.";
        if (leer.IstAktiv) return $"{leer.Inhaber} wohnt noch im eigenen Haus.";
        if (Leerstand(leer) is null) return $"Das Wohnhaus von {leer.Inhaber} steht nicht mehr leer.";
        if (leer.WohnhausFertigAb is not null) return $"Am Wohnhaus von {leer.Inhaber} wird noch gebaut.";
        return null;
    }

    /// <summary>Läuft schon ein Ausbau oder ein Umzug? Dann kein zweites Wohnhaus-Vorhaben.</summary>
    private string? UmzugVerboten(Konto konto)
    {
        if (konto.WohnhausFertigAb is not null) return $"Am Wohnhaus von {konto.Inhaber} wird gerade ausgebaut.";
        if (Umzug(konto) is { } u)
            return $"{konto.Inhaber} {(u.Arbeit == Bauarbeit.Sanierung ? "lässt schon ein Haus sanieren" : "baut schon ein neues Wohnhaus")}.";
        return null;
    }

    /// <summary>Der Stand, den diese Wohnhausstufe mindestens verlangt (wie beim Ausbau: höchstens eine Stufe über dem eigenen Stand) – oder null.</summary>
    private string? StandFehlt(Konto konto, int stufe)
    {
        var noetig = _w.Wohnen.Daten!.Stufen[stufe].AbStand - 1;
        return konto.Stand < noetig ? _w.Daten.Staende[noetig].Name : null;
    }

    // ── Preise und Namen ────────────────────────────────────────────────────

    /// <summary>Wohnhaus-Neubau: Hauswert der Zielstufe minus Hauswert des alten Hauses, nie unter 0.</summary>
    private double WohnhausKosten(Konto konto, int stufe) => Math.Max(0, _w.Wohnen.WertDerStufe(stufe) - _w.Wohnen.Hauswert(konto));

    /// <summary>Werkstatt-Neubau: so viel wie eine freie Werkstatt der Stufe 1 (die günstigste dieser Art, sonst die günstigste der Stadt).</summary>
    public double WerkstattPreis(Betriebsart art) => _w.Grundpreis("", art);

    private string MindestStand(Betriebsart art) =>
        _w.Vorbild("", art)?.MindestStand ?? _w.Daten.Staende[Math.Min(1, _w.Daten.Staende.Count - 1)].Name;

    private string GebaeudeName(BauBefehl befehl) => befehl switch
    {
        WohnhausBauen w => _w.Wohnen.Daten!.Stufen[w.Stufe].Name,
        WerkstattBauen b => _w.Daten.Art(b.Art).Name,
        _ => "Gebäude",
    };

    private static string LagenText(List<Lage> lagen) => string.Join(" oder ", lagen.Select(l => l switch
    {
        Lage.Kern => "in Kernlage",
        Lage.Mauerring => "am Mauerring",
        Lage.Vorstadt => "in der Vorstadt",
        _ => "im Umland",
    }));

    private string Bezeichnung(Grundstueck g) => g.Art switch
    {
        Grundstuecksart.Werkstatt => g.Arbeit is null ? g.Betrieb ?? "eine Werkstatt" : $"die Baustelle von {g.Betrieb}",
        _ => g.Arbeit switch
        {
            null => $"das Wohnhaus von {g.Besitzer?.Inhaber}",
            Bauarbeit.Abriss => "ein Haus, das abgerissen wird",
            _ => $"die Baustelle von {g.Besitzer?.Inhaber}",
        },
    };

    /// <summary>
    /// Name einer neuen Werkstatt nach der nächsten Karrenstraße, z. B. „Gießerei an der Ostgasse“ oder „Tischlerei am Holzmarkt“ –
    /// gibt es ihn schon, mit römischer Zahl („… II“). Befehle nennen Betriebe beim Namen, deshalb muss er eindeutig sein.
    /// </summary>
    private string NeuerName(Betriebsart art, Grundriss g)
    {
        var (ex, ez) = g.Eingang;
        var weg = Karte?.Wege.Where(w => w.IstKarrenstrasse && w.Name != "")
            .OrderBy(w => w.Abstand(ex, ez)).ThenBy(w => w.Name, StringComparer.Ordinal).FirstOrDefault();
        var artName = _w.Daten.Art(art).Name;
        var basis = weg is null ? artName : $"{artName} {Praeposition(weg.Name)} {weg.Name}";
        var name = basis;
        for (var n = 2; Vergeben(name); n++)
            name = $"{basis} {Roemisch(n)}";
        return name;
    }

    private bool Vergeben(string name) =>
        _w.Betriebe.Any(b => b.Name == name) || _w.Daten.Liegenschaften.Any(l => l.Name == name) || _w.FreieLiegenschaften.Any(l => l.Name == name)
        || _grundstuecke.Any(g => g.Betrieb == name) || Karte?.Feste.Any(f => f.Name == name) == true;

    /// <summary>„am Heumarkt“, aber „an der Ostgasse“ – grob nach der Endung.</summary>
    private static string Praeposition(string strasse)
    {
        var klein = strasse.ToLowerInvariant();
        return new[] { "markt", "platz", "weg", "wall", "graben", "ring", "damm", "hof", "steg", "berg", "kai" }.Any(klein.EndsWith) ? "am" : "an der";
    }

    private static string Roemisch(int n) =>
        n is >= 2 and <= 10 ? new[] { "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[n - 2] : n.ToString();

    // ── Handeln ──────────────────────────────────────────────────────────────

    /// <summary>Befehl „Leerstand kaufen“: bezahlen, sanieren lassen, nach der Sanierung umziehen.</summary>
    internal Befehlsergebnis LeerstandKaufen(Konto konto, Konto leer, Spielzeit zeit)
    {
        if (LeerstandKaufVerboten(konto, leer) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var v = Leerstandspreise(konto, leer);
        var g = Leerstand(leer)!;
        if (v.KaufSumme > 0) konto.Investition(v.KaufSumme);
        g.GespeicherteStufe = v.Stufe;
        g.Besitzer = konto;
        g.Arbeit = Bauarbeit.Sanierung;
        g.FertigAb = zeit.VergangeneMinuten + v.SanierungMinuten;
        HauswerteAktualisieren(konto, leer);
        var waehrung = _w.Daten.Allgemein.Waehrung;
        _w.Melden($"{konto.Inhaber} kauft das leerstehende {v.Gebaeude} von {leer.Inhaber} und lässt es sanieren.");
        BauBegonnen?.Invoke(g);
        if (v.SanierungMinuten == 0) Minute(zeit);
        return Befehlsergebnis.Ok($"{konto.Inhaber} kauft das {v.Gebaeude} von {leer.Inhaber} ({Zahlenformat.Ganz(v.KaufSumme)} {waehrung} " +
                                  $"nach Anrechnung des alten Hauses). Nach der Sanierung ({Dauer(v.SanierungMinuten)}) zieht das Haus um; das alte wird abgerissen.");
    }

    /// <summary>Befehl „Leerstand abreißen“: kaufen, abreißen lassen – danach ist hier ein Bauplatz für jeden.</summary>
    internal Befehlsergebnis LeerstandAbreissen(Konto konto, Konto leer, Spielzeit zeit)
    {
        if (LeerstandAbrissVerboten(konto, leer) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var v = Leerstandspreise(konto, leer);
        var g = Leerstand(leer)!;
        konto.Investition(v.AbrissSumme);
        g.GespeicherteStufe = v.Stufe;
        g.Besitzer = konto;
        g.Arbeit = Bauarbeit.Abriss;
        g.FertigAb = zeit.VergangeneMinuten + v.AbrissMinuten;
        HauswerteAktualisieren(leer);
        _w.Melden($"{konto.Inhaber} kauft das leerstehende {v.Gebaeude} von {leer.Inhaber} und lässt es abreißen.");
        BauBegonnen?.Invoke(g);
        if (v.AbrissMinuten == 0) Minute(zeit);
        return Befehlsergebnis.Ok($"Das {v.Gebaeude} von {leer.Inhaber} wird abgerissen ({Zahlenformat.Ganz(v.AbrissSumme)} {_w.Daten.Allgemein.Waehrung}, " +
                                  $"{Dauer(v.AbrissMinuten)}). Danach ist dort ein Bauplatz.");
    }

    /// <summary>Befehl „Wohnhaus oder Werkstatt bauen“: bezahlen, die Baustelle beginnt.</summary>
    internal Befehlsergebnis Bauen(Konto konto, BauBefehl befehl, Spielzeit zeit)
    {
        if (BauVerboten(konto, befehl) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var v = Vorschau(konto, befehl);
        if (v.Kosten > 0) konto.Investition(v.Kosten);
        var g = Neu(v.Grundriss, befehl is WohnhausBauen ? Grundstuecksart.Wohnhaus : Grundstuecksart.Werkstatt, null);
        g.Besitzer = konto;
        g.Arbeit = Bauarbeit.Neubau;
        g.FertigAb = zeit.VergangeneMinuten + v.BauzeitMinuten;
        if (befehl is WohnhausBauen w) g.GespeicherteStufe = w.Stufe;
        if (befehl is WerkstattBauen b)
        {
            g.Betrieb = v.Betrieb;
            g.Betriebsart = b.Art;
        }
        // Bauplätze unter dem Neubau sind vergeben – auch ein Rest daneben (Bauplätze sind Rechtecke).
        foreach (var platz in Bauplaetze.Where(p => p.Grundriss.Ueberlappt(g.Grundriss)).ToList())
        {
            _grundstuecke.Remove(platz);
            BauplatzBebaut?.Invoke(platz);
        }
        HauswerteAktualisieren(konto);
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var was = v.Betrieb ?? $"ein {v.Gebaeude}";
        _w.Melden($"{konto.Inhaber} baut {was}.");
        BauBegonnen?.Invoke(g);
        if (v.BauzeitMinuten == 0) Minute(zeit);
        return Befehlsergebnis.Ok($"{konto.Inhaber} baut {was} ({Zahlenformat.Ganz(v.Kosten)} {waehrung}, fertig in {Dauer(v.BauzeitMinuten)})." +
                                  (befehl is WohnhausBauen ? " Bis dahin wohnt das Haus im alten, danach wird es abgerissen." : ""));
    }

    private static string Dauer(int minuten) =>
        minuten >= Spielzeit.MinutenProTag ? $"{minuten / (double)Spielzeit.MinutenProTag:0.#} {(minuten == Spielzeit.MinutenProTag ? "Jahr" : "Jahren")}"
        : minuten >= 60 ? $"{minuten / 60.0:0.#} Stunden" : $"{minuten} Minuten";

    private Grundstueck Neu(Grundriss grundriss, Grundstuecksart art, string? kennung)
    {
        var g = new Grundstueck(_naechsteNummer++, grundriss, art, Karte?.ZoneBei(grundriss.X, grundriss.Z)?.Art, kennung);
        _grundstuecke.Add(g);
        return g;
    }

    private void HauswerteAktualisieren(params Konto[] konten)
    {
        foreach (var k in konten) _w.Wohnen.HauswertAktualisieren(k);
    }

    /// <summary>Jede Spielminute: fertige Baustellen übernehmen.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (!_grundstuecke.Any(g => g.FertigAb is { } ab && zeit.VergangeneMinuten >= ab)) return;
        foreach (var g in _grundstuecke.Where(g => g.FertigAb is { } ab && zeit.VergangeneMinuten >= ab).ToList())
        {
            if (g.Arbeit == Bauarbeit.Abriss)
            {
                var bauherr = g.Besitzer?.Inhaber;
                ZumBauplatz(g);
                _w.Melden($"{bauherr} hat das alte Haus abreißen lassen – dort ist jetzt ein Bauplatz.");
            }
            else if (g.Art == Grundstuecksart.Wohnhaus)
                Einziehen(g);
            else
                WerkstattOeffnen(g);
        }
    }

    /// <summary>Umzug ins neue (oder sanierte) Wohnhaus; das alte wird abgerissen.</summary>
    private void Einziehen(Grundstueck g)
    {
        var k = g.Besitzer!;
        var alt = Wohnhaus(k);
        g.Arbeit = null;
        g.FertigAb = null;
        k.WohnhausStufe = g.GespeicherteStufe;
        k.WohnhausInstandBis = null; // ein abgebranntes altes Haus ist ab jetzt egal
        _w.Wohnen.HauswertAktualisieren(k);
        _w.Melden($"{k.Inhaber} bezieht das neue {_w.Wohnen.Stufe(k)!.Name}" + (alt is not null ? "; das alte Haus wird abgerissen." : "."));
        BauFertig?.Invoke(g);
        if (alt is not null) ZumBauplatz(alt);
    }

    /// <summary>Die neue Werkstatt öffnet – wie eine gekaufte (leer, mit Startkarren). Ist der Bauherr inzwischen bankrott oder erloschen, steht sie zum Verkauf.</summary>
    private void WerkstattOeffnen(Grundstueck g)
    {
        var k = g.Besitzer!;
        g.Arbeit = null;
        g.FertigAb = null;
        g.Besitzer = null;
        if (k.IstAktiv)
        {
            var betrieb = _w.BetriebEroeffnen(g.Betrieb!, g.Betriebsart!.Value, k);
            _w.Melden($"{g.Betrieb} von {k.Inhaber} ist fertig gebaut. Jetzt Personal einstellen!");
            BauFertig?.Invoke(g);
            _w.ErworbenMelden(betrieb);
        }
        else
        {
            _w.NeubauFreigeben(g.Betrieb!, g.Betriebsart!.Value, MindestStand(g.Betriebsart!.Value));
            _w.Melden($"{g.Betrieb} ist fertig gebaut – {k.Inhaber} ist nicht mehr im Spiel, die Werkstatt steht zum Verkauf.");
            BauFertig?.Invoke(g);
        }
    }

    private void ZumBauplatz(Grundstueck g)
    {
        g.Art = Grundstuecksart.Bauplatz;
        g.Besitzer = null;
        g.Arbeit = null;
        g.FertigAb = null;
        g.Betrieb = null;
        g.Betriebsart = null;
        Abgerissen?.Invoke(g);
    }

    // ── Startplätze, Speichern, Laden ────────────────────────────────────────

    /// <summary>Das Haus mit dieser Nummer in der Konto-Reihenfolge bekommt den passenden Wohnhausplatz der Karte (wenn es ihn gibt).</summary>
    internal void StartplatzVergeben(Konto k, int nummer)
    {
        if (!IstAktiv || Wohnhaus(k) is not null) return;
        var plaetze = Karte!.Wohnhausplaetze;
        if (nummer < 0 || nummer >= plaetze.Count || _grundstuecke.Any(g => g.Kennung == plaetze[nummer].Kennung)) return;
        var g = Neu(plaetze[nummer].Grundriss!.Value, Grundstuecksart.Wohnhaus, plaetze[nummer].Kennung);
        g.Besitzer = k;
    }

    internal BauenStand? Sichern(Func<Konto, int> nummer) => !IstAktiv ? null : new BauenStand
    {
        NaechsteNummer = _naechsteNummer,
        Grundstuecke = _grundstuecke.Select(g => new GrundstueckStand
        {
            Nummer = g.Nummer, X = g.Grundriss.X, Z = g.Grundriss.Z, Breite = g.Grundriss.Breite, Tiefe = g.Grundriss.Tiefe, Drehung = g.Grundriss.Drehung,
            Art = g.Art, Lage = g.Lage, Kennung = g.Kennung, Besitzer = g.Besitzer is { } b ? nummer(b) : null, Stufe = g.GespeicherteStufe,
            Betrieb = g.Betrieb, Betriebsart = g.Betriebsart, Arbeit = g.Arbeit, FertigAb = g.FertigAb,
        }).ToList(),
    };

    /// <summary>Nach dem Laden der Konten: Grundstücke herstellen – alte Spielstände bekommen die Startplätze in Konto-Reihenfolge.</summary>
    internal void Wiederherstellen(BauenStand? s, IReadOnlyList<Konto> konten)
    {
        if (!IstAktiv) return;
        if (s is null)
        {
            for (var i = 0; i < konten.Count; i++) StartplatzVergeben(konten[i], i);
            return;
        }
        _naechsteNummer = s.NaechsteNummer;
        foreach (var gs in s.Grundstuecke)
            _grundstuecke.Add(new Grundstueck(gs.Nummer, new Grundriss(gs.X, gs.Z, gs.Breite, gs.Tiefe, gs.Drehung), gs.Art, gs.Lage, gs.Kennung)
            {
                Besitzer = gs.Besitzer is int n ? konten[n] : null, GespeicherteStufe = gs.Stufe,
                Betrieb = gs.Betrieb, Betriebsart = gs.Betriebsart, Arbeit = gs.Arbeit, FertigAb = gs.FertigAb,
            });
    }
}
