using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Bauen;

/// <summary>Was auf einem Grundstück steht (oder entsteht).</summary>
public enum Grundstuecksart
{
    /// <summary>Wohnhaus eines Hauses – bewohnt, leerstehend oder im Bau.</summary>
    Wohnhaus,

    /// <summary>Eine neu gebaute Werkstatt (die Werkstattplätze der Karte stehen in <see cref="Stadtkarte.Gebaeude"/>).</summary>
    Werkstatt,

    /// <summary>Freier Bauplatz nach einem Abriss – Wiese mit Pflöcken, für jeden bebaubar.</summary>
    Bauplatz,
}

/// <summary>
/// Wo ein Betrieb steht: auf einem festen Platz der Karte (<see cref="Gebaeude"/>: Werkstattplatz oder Umlandbetrieb) oder auf einem
/// Grundstück (<see cref="Grundstueck"/>: neu gebaute Werkstatt). Genau eins von beiden ist gesetzt.
/// </summary>
public sealed record Betriebsplatz(string Betrieb, Kartengebaeude? Gebaeude, Grundstueck? Grundstueck)
{
    /// <summary>Mitte der Fläche [x, z].</summary>
    public (double X, double Z) Punkt => Gebaeude is { } g ? (g.Punkt[0], g.Punkt[1]) : (Grundstueck!.Grundriss.X, Grundstueck.Grundriss.Z);

    /// <summary>Drehung in Grad wie Godot <c>Rotation.Y</c> (0 = Vorderseite nach Süden).</summary>
    public double DrehungGrad => Gebaeude?.Drehung ?? Grundstueck!.Grundriss.Drehung * 90;
}

/// <summary>
/// Wo ein Haus auf einem Grundstück wirklich steht: Mitte, Größe (Breite an der Gasse, Tiefe nach hinten) und Drehung in Grad wie Godot
/// <c>Rotation.Y</c> (0 = Vorderseite nach Süden, 90 = nach Osten – beliebige Winkel). Auf einem schrägen Wohnhausplatz der Karte ist der
/// <see cref="Grundriss"/> des Grundstücks nur das umschließende Rechteck (für Überlappung und Bauen); das Haus steht wie der Platz.
/// </summary>
public readonly record struct Hausflaeche(double X, double Z, double Breite, double Tiefe, double DrehungGrad)
{
    /// <summary>Richtung zur Gasse (Einheitsvektor).</summary>
    public (double X, double Z) Vorne => (Math.Sin(DrehungGrad * Math.PI / 180), Math.Cos(DrehungGrad * Math.PI / 180));

    /// <summary>Mitte der Vorderkante – dort ist der Eingang.</summary>
    public (double X, double Z) Eingang => (X + Vorne.X * Tiefe / 2, Z + Vorne.Z * Tiefe / 2);

    /// <summary>Die Fläche eines Grundrisses (Drehung in 90°-Schritten).</summary>
    public static Hausflaeche Von(Grundriss g) => new(g.X, g.Z, g.Breite, g.Tiefe, g.Drehung * 90);
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

    /// <summary>Leerstand: seit dieser Spielminute steht das Haus leer (Bankrott oder Erlöschen des Bewohners) – sonst null.</summary>
    public long? LeerSeit { get; internal set; }

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

    /// <summary>
    /// Wo ein Betrieb (oder eine freie Liegenschaft) steht: sein fester Platz in der Karte (<see cref="Stadtkarte.PlatzVon"/>) oder das
    /// Grundstück einer neu gebauten Werkstatt. Null ohne Karte oder wenn die Karte den Namen nicht kennt.
    /// </summary>
    public Betriebsplatz? PlatzVon(string betrieb) =>
        Karte?.PlatzVon(betrieb) is { } fest ? new Betriebsplatz(betrieb, fest, null)
        : Werkstatt(betrieb) is { } g ? new Betriebsplatz(betrieb, null, g)
        : null;

    /// <summary>Freie Bauplätze nach Abriss.</summary>
    public IEnumerable<Grundstueck> Bauplaetze => _grundstuecke.Where(g => g.Art == Grundstuecksart.Bauplatz);

    /// <summary>Das leerstehende Wohnhaus eines bankrotten oder erloschenen Hauses (null: lebt noch, schon verkauft oder kein Platz).</summary>
    public Grundstueck? Leerstand(Konto leer) => !leer.IstAktiv ? Wohnhaus(leer) : null;

    /// <summary>
    /// Seit welcher Spielminute das Wohnhaus dieses Hauses leer steht – die Minute des Bankrotts oder Erlöschens (null: kein Leerstand).
    /// Die KI-Häuser kaufen einen Leerstand erst nach einem Spieljahr (1.020 Minuten), damit der Spieler Vorrang hat.
    /// Ein Leerstand aus einem alten Spielstand (ohne gespeicherten Zeitpunkt) zählt ab dem Laden.
    /// </summary>
    public long? LeerSeit(Konto leer) => Leerstand(leer) is { } g ? g.LeerSeit ?? _w.Uhr.VergangeneMinuten : null;

    /// <summary>Die Stufe, die das Haus nach dem laufenden Neubau oder der Sanierung bewohnt (null: kein Umzug).</summary>
    public int? UmzugZiel(Konto k) => Umzug(k)?.GespeicherteStufe;

    /// <summary>Ein bankrottes oder erloschenes Haus, dessen Wohnhaus verkauft oder abgerissen ist: kein Hauswert, kein Leerstand.</summary>
    internal bool HausAufgegeben(Konto k) => IstAktiv && !k.IstAktiv && Wohnhaus(k) is null && Umzug(k) is null;

    /// <summary>
    /// Entsteht diese Wohnhausstufe nur als Neubau oder durch Kauf eines Leerstands, nicht als Ausbau am Platz (Stadtpalais, Stadthof)?
    /// Nur, wo gebaut wird – ohne Karte (etwa in Köln) bleibt der Ausbau zu allen Stufen erlaubt, sonst käme dort niemand zum Palais.
    /// </summary>
    public bool NurNeubau(int stufe) => IstAktiv && stufe >= 0 && stufe < Daten!.Wohnhaus.Count && Daten.Wohnhaus[stufe].NurNeubau;

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
        if (Wohnhausplatz(befehl) is { } platz) return platz.Grundriss;
        var m = Mass(befehl);
        return new Grundriss(befehl.X, befehl.Z, m?.Breite ?? 0, m?.Tiefe ?? 0, ((befehl.Drehung % 4) + 4) % 4);
    }

    /// <summary>
    /// Wo das Haus eines Grundstücks steht (<see cref="Hausflaeche"/>): auf einem Wohnhausplatz der Karte (Startplatz, auch als Bauplatz nach
    /// einem Abriss) in Größe und Drehung des Platzes – er darf schräg stehen –, sonst der <see cref="Grundstueck.Grundriss"/>.
    /// </summary>
    public Hausflaeche Flaeche(Grundstueck g) =>
        Karte?.GebaeudeMit(g.Kennung) is { Baustein.Groesse: { Length: 2 } m } platz
            ? new Hausflaeche(platz.Punkt[0], platz.Punkt[1], m[0] * platz.Massstab, m[1] * platz.Massstab, platz.Drehung)
            : Hausflaeche.Von(g.Grundriss);

    /// <summary>Wo das Gebäude des Befehls stünde: eingepasst auf einen Wohnhausplatz wie der Platz, sonst der <see cref="Grundriss(BauBefehl)"/>.</summary>
    public Hausflaeche Flaeche(BauBefehl befehl) => Wohnhausplatz(befehl) is { } platz ? Flaeche(platz) : Hausflaeche.Von(Grundriss(befehl));

    /// <summary>Ist der Bauplatz aus einem Wohnhausplatz der Karte entstanden (Abriss oder Umzug)? Dann nimmt er nur Wohnhäuser auf, eingepasst.</summary>
    public static bool IstWohnhausplatz(Grundstueck g) => g.Art == Grundstuecksart.Bauplatz && g.Kennung is not null;

    /// <summary>
    /// Der Wohnhausplatz (als Bauplatz), auf den dieser Wohnhaus-Neubau eingepasst wird – null, wenn die Mitte nicht genau auf einem liegt.
    /// Ein Wohnhausplatz nimmt jedes Wohnhaus jeder Stufe auf, in genau seiner Fläche und Drehung (wie der Ausbau am Platz);
    /// die Normmaße aus „bauen.wohnhaus“ gelten nur in den Bauzonen (Entscheidung des Teamleiters vom 17.10.2026).
    /// </summary>
    public Grundstueck? Wohnhausplatz(BauBefehl befehl) => befehl is not WohnhausBauen ? null
        : Bauplaetze.FirstOrDefault(p => IstWohnhausplatz(p) && Math.Abs(p.Grundriss.X - befehl.X) < 1e-6 && Math.Abs(p.Grundriss.Z - befehl.Z) < 1e-6);

    /// <summary>
    /// Rastet die Mausposition ein: ein Wohnhaus auf einen Wohnhausplatz, über dem die Maus steht (Mitte und Drehung des Platzes);
    /// sonst auf einen Bauplatz, auf dem das Gebäude ganz Platz hat (mit der Vorderkante bündig, wenn es gleich gedreht ist);
    /// sonst aufs 2-m-Raster. Gibt den Befehl mit den eingerasteten Koordinaten (und beim Wohnhausplatz seiner Drehung) zurück.
    /// </summary>
    public BauBefehl Einrasten(BauBefehl befehl)
    {
        if (Daten is not { } d || Mass(befehl) is null) return befehl;
        if (befehl is WohnhausBauen && Bauplaetze.FirstOrDefault(p => IstWohnhausplatz(p) && p.Grundriss.Enthaelt(befehl.X, befehl.Z)) is { } wohnplatz)
            return befehl with { X = wohnplatz.Grundriss.X, Z = wohnplatz.Grundriss.Z, Drehung = wohnplatz.Grundriss.Drehung };
        var g = Grundriss(befehl);
        foreach (var platz in Bauplaetze.Where(p => !IstWohnhausplatz(p)))
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
        // Wohnhausplätze sind nur eingepasst bebaubar: Dann zählt genau dieser eine, sonst keiner (und keiner darf berührt werden).
        var eingepasst = Wohnhausplatz(befehl);
        if (eingepasst is null && Bauplaetze.Any(p => IstWohnhausplatz(p) && g.Ueberlappt(p.Grundriss)))
            return befehl is WerkstattBauen
                ? "Auf einem Wohnhausplatz stehen nur Wohnhäuser."
                : "Ein Wohnhaus kommt genau auf den Wohnhausplatz – nicht daneben oder halb darauf.";
        var bauplaetze = eingepasst is not null ? new List<Grundstueck> { eingepasst } : Bauplaetze.Where(p => !IstWohnhausplatz(p)).ToList();

        Grundstueck? BauplatzBei((double X, double Z) f) => bauplaetze.FirstOrDefault(p => p.Grundriss.Enthaelt(f.X, f.Z));
        if (!g.AufDemRaster(d.Raster) && !felder.All(f => BauplatzBei(f) is not null))
            return $"Gebaut wird auf dem Raster von {d.Raster:0.#} m – oder genau auf einem Bauplatz.";

        foreach (var anderes in _grundstuecke.Where(x => x.Art != Grundstuecksart.Bauplatz))
            if (g.Ueberlappt(anderes.Grundriss))
                return $"Hier steht schon {Bezeichnung(anderes)}.";
        foreach (var fest in karte.Gebaeude)
        {
            if (fest.Art == Bausteinart.Wohnhausplatz && _grundstuecke.Any(x => x.Kennung == fest.Kennung)) continue; // vergebener Startplatz: als Grundstück geprüft
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

    /// <summary>
    /// Alle Stellen, an denen dieses Haus das Gebäude der Vorlage jetzt bauen darf: eingerastete Befehle, für die <see cref="BauVerboten"/> null ist.
    /// Lage und Drehung der Vorlage zählen nicht, nur das Gebäude (Wohnhausstufe bzw. Betriebsart). Feste Reihenfolge:
    ///   1. Bauplätze nach Nummer – ein Wohnhausplatz (nur für Wohnhäuser) in seiner Drehung, ein anderer Bauplatz je Drehung 0–3 eingerastet;
    ///   2. die Rasterstellen in den Bauzonen von Norden nach Süden und von Westen nach Osten (nach der nordwestlichen Ecke), je Stelle die Drehungen 0–3.
    /// Leer, wenn das Haus das Gebäude gar nicht bauen darf (<see cref="BauherrVerboten"/>). Für die KI, die nur selten fragt:
    /// Auf der Greybox prüft es einige tausend Stellen (Rechenzeit siehe Test „Bauorte“).
    /// </summary>
    public IReadOnlyList<BauBefehl> Bauorte(Konto konto, BauBefehl vorlage) =>
        BauherrVerboten(konto, vorlage) is not null ? Array.Empty<BauBefehl>() : FreieOrte(vorlage);

    /// <summary>Wie <see cref="Bauorte"/>, aber ohne Bauherrn: nur die Prüfung des Platzes (<see cref="PlatzVerboten"/>).</summary>
    private IReadOnlyList<BauBefehl> FreieOrte(BauBefehl vorlage)
    {
        if (!IstAktiv || Mass(vorlage) is not { } mass) return Array.Empty<BauBefehl>();
        var d = Daten!;
        var orte = new List<BauBefehl>();
        var geprueft = new HashSet<(double, double, int)>();
        void Pruefen(BauBefehl b)
        {
            if (geprueft.Add((b.X, b.Z, b.Drehung)) && PlatzVerboten(b) is null) orte.Add(b);
        }

        var bauplaetze = Bauplaetze.OrderBy(p => p.Nummer).ToList();
        foreach (var platz in bauplaetze)
        {
            var p = platz.Grundriss;
            if (!IstWohnhausplatz(platz))
                for (var drehung = 0; drehung < 4; drehung++) Pruefen(Einrasten(vorlage with { X = p.X, Z = p.Z, Drehung = drehung }));
            else if (vorlage is WohnhausBauen)
                Pruefen(vorlage with { X = p.X, Z = p.Z, Drehung = p.Drehung });
        }

        // Rasterstellen: Das Gebiet umfasst alle passenden Bauzonen und die Bauplätze. Vorab genügt ein billiger Blick auf die vier Eckfelder:
        // Liegt eines weder auf einem Bauplatz noch in einer passenden Zone, lehnt PlatzVerboten die Stelle sicher ab.
        var zonen = Karte!.Bauzonen.Where(z => mass.Lagen.Contains(z.Art)).ToList();
        var freie = bauplaetze.Where(p => !IstWohnhausplatz(p)).ToList();
        var ecken = zonen.SelectMany(z => z.Flaeche.Select(e => (X: e[0], Z: e[1])))
            .Concat(freie.SelectMany(p => new[] { (X: p.Grundriss.X - p.Grundriss.HalbX, Z: p.Grundriss.Z - p.Grundriss.HalbZ),
                                                   (X: p.Grundriss.X + p.Grundriss.HalbX, Z: p.Grundriss.Z + p.Grundriss.HalbZ) }))
            .ToList();
        if (ecken.Count == 0) return orte;
        var r = d.Raster;
        var (x0, x1) = (Math.Floor(ecken.Min(e => e.X) / r) * r, ecken.Max(e => e.X));
        var (z0, z1) = (Math.Floor(ecken.Min(e => e.Z) / r) * r, ecken.Max(e => e.Z));
        bool Bebaubar(double fx, double fz) => freie.Any(p => p.Grundriss.Enthaelt(fx, fz)) || zonen.Any(zone => zone.Enthaelt(fx, fz));
        for (var ez = z0; ez < z1; ez += r)
            for (var ex = x0; ex < x1; ex += r)
                for (var drehung = 0; drehung < 4; drehung++)
                {
                    var g = new Grundriss(ex, ez, mass.Breite, mass.Tiefe, drehung);
                    var (x, z) = (ex + g.HalbX, ez + g.HalbZ); // Mitte; (ex, ez) ist die nordwestliche Ecke
                    if (x + g.HalbX > x1 + 1e-6 || z + g.HalbZ > z1 + 1e-6) continue;
                    var (ax, az) = (g.HalbX - r / 2, g.HalbZ - r / 2); // von der Mitte zu den Mitten der Eckfelder
                    if (!Bebaubar(x - ax, z - az) || !Bebaubar(x + ax, z - az) || !Bebaubar(x - ax, z + az) || !Bebaubar(x + ax, z + az)) continue;
                    var befehl = vorlage with { X = x, Z = z, Drehung = drehung };
                    if (Wohnhausplatz(befehl) is null) Pruefen(befehl); // genau auf einem Wohnhausplatz: schon oben, in seiner Drehung
                }
        return orte;
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

    /// <summary>Werkstatt-Neubau: der Kaufpreis der Stufe 1 (<c>kaufpreis</c> der Art, siehe <see cref="Stadtwirtschaft.Grundpreis"/>).</summary>
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
        var basis = weg is null ? artName : $"{artName} {Lagebezeichnung(weg.Name)}";
        var name = basis;
        for (var n = 2; Vergeben(name); n++)
            name = $"{basis} {Roemisch(n)}";
        return name;
    }

    private bool Vergeben(string name) =>
        _w.Betriebe.Any(b => b.Name == name) || _w.Daten.Liegenschaften.Any(l => l.Name == name) || _w.FreieLiegenschaften.Any(l => l.Name == name)
        || _grundstuecke.Any(g => g.Betrieb == name) || Karte?.Gebaeude.Any(f => f.Name == name || f.Betrieb == name) == true;

    /// <summary>
    /// Wo eine neue Werkstatt liegt, kurz und mit passender Präposition – nach dem Namen der nächsten Karrenstraße:
    ///   • „Ostgasse“ → „an der Ostgasse“, „Heumarkt“ → „am Heumarkt“ (grob nach der Endung: maskulin/neutral „am“, sonst „an der“),
    ///   • Zufahrten wie „Weg zum Abzweig Steinbruch am Felsenhang“ → „am Felsenhang“ (die Lage des Ziels),
    ///   • ohne Lage im Ziel: „Weg zu Grube Eisenbart“ → „am Weg zur Grube Eisenbart“.
    /// </summary>
    internal static string Lagebezeichnung(string strasse)
    {
        var zufahrt = System.Text.RegularExpressions.Regex.Match(strasse, @"^Weg zu[mr]?\s+(?:Abzweig\s+)?(?<ziel>.+)$");
        if (!zufahrt.Success)
            return $"{Praeposition(strasse)} {strasse}";
        var ziel = zufahrt.Groups["ziel"].Value;
        var lage = System.Text.RegularExpressions.Regex.Match(ziel, @"\s(?<lage>(?:am|an der|im|beim|auf der|vor dem) .+)$");
        if (lage.Success)
            return lage.Groups["lage"].Value;
        var erstesWort = ziel.Split(' ')[0];
        return $"am Weg {(erstesWort.EndsWith('e') ? "zur" : "zum")} {ziel}"; // Grube, Mühle → zur; Steinbruch, Holzschlag → zum
    }

    /// <summary>„am Heumarkt“, aber „an der Ostgasse“ – grob nach der Endung.</summary>
    private static string Praeposition(string strasse)
    {
        var klein = strasse.ToLowerInvariant();
        return klein.StartsWith("weg ")
               || new[] { "markt", "platz", "weg", "pfad", "steig", "wall", "graben", "ring", "damm", "hof", "steg", "berg", "kai", "tor", "ufer", "feld", "bach", "hang", "wald", "forst" }.Any(klein.EndsWith)
            ? "am" : "an der";
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
        g.LeerSeit = null;
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
        g.LeerSeit = null;
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

    private Grundstueck Neu(Grundriss grundriss, Grundstuecksart art, string? kennung, Lage? lage = null)
    {
        var g = new Grundstueck(_naechsteNummer++, grundriss, art, lage ?? Karte?.ZoneBei(grundriss.X, grundriss.Z)?.Art, kennung);
        _grundstuecke.Add(g);
        return g;
    }

    private void HauswerteAktualisieren(params Konto[] konten)
    {
        foreach (var k in konten) _w.Wohnen.HauswertAktualisieren(k);
    }

    /// <summary>Ein Haus scheidet aus (Bankrott oder Erlöschen): Sein Wohnhaus steht ab jetzt leer.</summary>
    internal void LeerstandBeginnt(Konto k)
    {
        if (Wohnhaus(k) is { LeerSeit: null } g) g.LeerSeit = _w.Uhr.VergangeneMinuten;
    }

    /// <summary>Jede Spielminute: Leerstand ohne Zeitpunkt (alter Spielstand) zählt ab jetzt; fertige Baustellen übernehmen.</summary>
    internal void Minute(Spielzeit zeit)
    {
        foreach (var g in _grundstuecke)
            if (g.LeerSeit is null && g.Art == Grundstuecksart.Wohnhaus && g.Arbeit is null && g.Besitzer is { IstAktiv: false })
                g.LeerSeit = zeit.VergangeneMinuten;
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
        g.LeerSeit = null;
        Abgerissen?.Invoke(g);
    }

    // ── Startplätze, Speichern, Laden ────────────────────────────────────────

    /// <summary>Das Haus mit dieser Nummer in der Konto-Reihenfolge bekommt den passenden Wohnhausplatz der Karte (wenn es ihn gibt).</summary>
    internal void StartplatzVergeben(Konto k, int nummer)
    {
        if (!IstAktiv || Wohnhaus(k) is not null) return;
        var plaetze = Karte!.Wohnhausplaetze;
        if (nummer < 0 || nummer >= plaetze.Count || _grundstuecke.Any(g => g.Kennung == plaetze[nummer].Kennung)) return;
        var g = Neu(plaetze[nummer].Grundriss!.Value, Grundstuecksart.Wohnhaus, plaetze[nummer].Kennung, plaetze[nummer].Lage);
        g.Besitzer = k;
    }

    /// <summary>
    /// Nach dem Laden: Werkstätten in der Stadt, für die die Karte keinen Platz mehr hat (alte Spielstände, z. B. die zehn Werkstätten
    /// zum Kauf, die seit PR #162 gestrichen sind), bekommen ein Grundstück an der ersten freien Stelle der Bauzonen – wie eine fertig
    /// gebaute Werkstatt (<see cref="FreieOrte"/>, feste Reihenfolge, also bei jedem Laden gleich). Gibt die Namen zurück, für die
    /// keine Stelle frei war. Ohne Bauwesen geschieht nichts.
    /// </summary>
    internal List<string> OhnePlatzEinordnen(IEnumerable<(string Name, Betriebsart Art)> werkstaetten)
    {
        var ohne = new List<string>();
        if (!IstAktiv) return ohne;
        foreach (var (name, art) in werkstaetten)
        {
            if (PlatzVon(name) is not null) continue;
            if (FreieOrte(new WerkstattBauen(art, 0, 0, 0)).FirstOrDefault() is not { } ort)
            {
                ohne.Add(name);
                continue;
            }
            var g = Neu(Grundriss(ort), Grundstuecksart.Werkstatt, null);
            g.Betrieb = name;
            g.Betriebsart = art;
            // Bauplätze unter der Werkstatt sind vergeben (wie beim Neubau).
            foreach (var platz in Bauplaetze.Where(p => p.Grundriss.Ueberlappt(g.Grundriss)).ToList())
                _grundstuecke.Remove(platz);
        }
        return ohne;
    }

    internal BauenStand? Sichern(Func<Konto, int> nummer) => !IstAktiv ? null : new BauenStand
    {
        NaechsteNummer = _naechsteNummer,
        Grundstuecke = _grundstuecke.Select(g => new GrundstueckStand
        {
            Nummer = g.Nummer, X = g.Grundriss.X, Z = g.Grundriss.Z, Breite = g.Grundriss.Breite, Tiefe = g.Grundriss.Tiefe, Drehung = g.Grundriss.Drehung,
            Art = g.Art, Lage = g.Lage, Kennung = g.Kennung, Besitzer = g.Besitzer is { } b ? nummer(b) : null, Stufe = g.GespeicherteStufe,
            Betrieb = g.Betrieb, Betriebsart = g.Betriebsart, Arbeit = g.Arbeit, FertigAb = g.FertigAb, LeerSeit = g.LeerSeit,
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
                Betrieb = gs.Betrieb, Betriebsart = gs.Betriebsart, Arbeit = gs.Arbeit, FertigAb = gs.FertigAb, LeerSeit = gs.LeerSeit,
            });
    }
}
