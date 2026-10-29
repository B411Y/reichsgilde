using System.Text.Json.Serialization;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

// ═════════════════════════════════════════════════════════════════════════════
//  Wer am Anfang einer Partie in der Stadt wohnt und arbeitet (Abschnitt „start“ der Karte, Format 2)
//
//  Die Karte legt die KI-Häuser fest (Name, Volk, Wappen, Farbe, Wohnhausplatz, Startbetriebe auf Plätzen der Karte)
//  und den Startplatz des Spielers. Ohne Karte gilt Startbelegung.Ersatz – die Häuser, die es vor Format 2 fest im Code gab.
//  Wie viele Gesellen und Umlandkräfte ein Startbetrieb bekommt, bleibt eine Regel des Kerns (Partieaufbau).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Startbetrieb eines KI-Hauses: auf welchem Platz der Karte, welche Betriebsart, was er zuerst herstellt.</summary>
public sealed class Startbetrieb
{
    /// <summary>Kennung des Gebäudes in der Karte (Werkstattplatz oder Umlandbetrieb). Dessen <c>betrieb</c> ist der Name des Betriebs.</summary>
    public string Platz { get; init; } = "";

    /// <summary>Betriebsart (Stufe 1 bzw. Umland), z. B. „Giesserei“ oder „Mine“.</summary>
    public Betriebsart Art { get; init; }

    /// <summary>
    /// Was die Werkstatt zuerst herstellt (fehlt: die Voreinstellung des Betriebs). Ein alter Warenname aus einer eigenen Karte
    /// („Rapier“) wird beim Lesen zum heutigen (<see cref="Warenumbenennung"/>, PR #232) – für Partie, Kartenprüfung und Kartenwerkstatt.
    /// </summary>
    public string? Produkt { get => _produkt; init => _produkt = value is null ? null : Warenumbenennung.NeuerName(value); }
    private readonly string? _produkt;

    /// <summary>Ohne Karte: der Name des Betriebs, „{art}“ = Name der Betriebsart (mit Karte steht der Name am Platz).</summary>
    [JsonIgnore]
    internal string? ErsatzName { get; init; }
}

/// <summary>Ein KI-Haus am Start.</summary>
public sealed class Starthaus
{
    /// <summary>Name des Hauses („Haus Eisenbart“) – zugleich der Inhaber des Kontos.</summary>
    public string Name { get; init; } = "";

    public Volk Volk { get; init; }
    public Geschlecht Geschlecht { get; init; }
    public Wappen Wappen { get; init; } = Wappen.Vorlagen[0];

    /// <summary>Farbe des Hauses in der Stadt (Fahnen, Dächer), als „#rrggbb“. Nur die Darstellung liest sie.</summary>
    public string? Farbe { get; init; }

    /// <summary>Kennung des Wohnhausplatzes in der Karte.</summary>
    public string? Wohnhaus { get; init; }

    /// <summary>Die Startbetriebe in dieser Reihenfolge (zuerst die Werkstatt).</summary>
    public List<Startbetrieb> Betriebe { get; init; } = new();
}

/// <summary>Wo der Spieler anfängt: der Platz der ersten Werkstatt und ihr Name.</summary>
public sealed class Startwerkstatt
{
    /// <summary>Kennung des Werkstattplatzes in der Karte.</summary>
    public string Platz { get; init; } = "";

    /// <summary>Name des Betriebs; „{art}“ wird durch den Namen der Betriebsart ersetzt („{art} am Weinmarkt“ → „Gießerei am Weinmarkt“).</summary>
    public string Name { get; init; } = "{art} am Weinmarkt";

    /// <summary>Der Name der Werkstatt dieser Betriebsart.</summary>
    public string NameFuer(string artName) => Name.Replace("{art}", artName);
}

/// <summary>Der Start des Spielers.</summary>
public sealed class Spielerstart
{
    /// <summary>Kennung des Wohnhausplatzes in der Karte.</summary>
    public string? Wohnhaus { get; init; }

    /// <summary>Farbe des Spielerhauses, als „#rrggbb“.</summary>
    public string? Farbe { get; init; }

    /// <summary>Die erste Werkstatt – für jeden Beruf, der unter <see cref="JeBeruf"/> nicht steht.</summary>
    public Startwerkstatt Werkstatt { get; init; } = new();

    /// <summary>Abweichende Startplätze je Beruf („Steinmetz“ → eigener Platz und Name).</summary>
    public Dictionary<string, Startwerkstatt> JeBeruf { get; init; } = new();

    /// <summary>Die erste Werkstatt für diesen Beruf.</summary>
    public Startwerkstatt WerkstattFuer(string beruf) => JeBeruf.GetValueOrDefault(beruf) ?? Werkstatt;
}

/// <summary>
/// Die Belegung der Stadt am Start: der Spieler und die KI-Häuser in Konto-Reihenfolge (das erste KI-Haus spielt immer mit,
/// die weiteren je nach Zahl der Rivalen im Menü).
/// </summary>
public sealed class Startbelegung
{
    public Spielerstart Spieler { get; init; } = new();
    public List<Starthaus> Haeuser { get; init; } = new();

    /// <summary>Die Namen aller KI-Häuser (für das Menü: Diesen Namen darf der Spieler nicht nehmen).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Rivalennamen => Haeuser.Select(h => h.Name).ToList();

    /// <summary>Die Wappen aller KI-Häuser (im Menü nicht wählbar).</summary>
    [JsonIgnore]
    public IReadOnlyList<Wappen> Rivalenwappen => Haeuser.Select(h => h.Wappen).ToList();

    /// <summary>Die Farbe eines Hauses („#rrggbb“) – des Spielers, wenn <paramref name="istSpieler"/>, sonst des KI-Hauses mit diesem Namen; null, wenn keine.</summary>
    public string? Farbe(string inhaber, bool istSpieler = false) =>
        istSpieler ? Spieler.Farbe : Haeuser.FirstOrDefault(h => h.Name == inhaber)?.Farbe;

    /// <summary>
    /// Ohne Karte: die drei KI-Häuser, die es vor Format 2 fest im Code gab (Eisenbart, Eichwald, Kieselbach), ohne Plätze.
    /// Die Werte stehen genauso im Abschnitt „start“ der Greybox-Karte.
    /// </summary>
    public static readonly Startbelegung Ersatz = new()
    {
        Spieler = new Spielerstart { Farbe = "#336bb8", Werkstatt = new Startwerkstatt { Name = "{art} am Weinmarkt" } },
        Haeuser =
        {
            new Starthaus
            {
                Name = Partieaufbau.Rivalenname, Volk = Partieaufbau.RivalenVolk, Geschlecht = Geschlecht.Mann, Wappen = Wappen.Eisenbart, Farbe = "#b33329",
                Betriebe =
                {
                    new Startbetrieb { Art = Betriebsart.Giesserei, Produkt = "Dolch", ErsatzName = "Gießerei Eisenbart" },
                    new Startbetrieb { Art = Betriebsart.Mine, ErsatzName = "Grube Eisenbart" },
                },
            },
            new Starthaus
            {
                Name = Partieaufbau.ZweiterRivale, Volk = Partieaufbau.ZweiterRivaleVolk, Geschlecht = Geschlecht.Frau, Wappen = Wappen.Eichwald, Farbe = "#388c47",
                Betriebe =
                {
                    new Startbetrieb { Art = Betriebsart.Tischlerei, ErsatzName = "{art} Eichwald" },
                    new Startbetrieb { Art = Betriebsart.Holzfaellerei, ErsatzName = "{art} Eichwald" },
                },
            },
            new Starthaus
            {
                Name = Partieaufbau.DritterRivale, Volk = Partieaufbau.DritterRivaleVolk, Geschlecht = Geschlecht.Mann, Wappen = Wappen.Kieselbach, Farbe = "#d9ad2e",
                Betriebe =
                {
                    new Startbetrieb { Art = Betriebsart.Maurerei, ErsatzName = "{art} Kieselbach" },
                    new Startbetrieb { Art = Betriebsart.Steinbruch, ErsatzName = "{art} Kieselbach" },
                },
            },
        },
    };
}
