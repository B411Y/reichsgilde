using System.Text.Json.Serialization;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

// ═════════════════════════════════════════════════════════════════════════════
//  Wer am Anfang einer Partie in der Stadt wohnt und arbeitet (Abschnitt „start“ der Karte, Format 2)
//
//  Bis zum 17.11.2026 legte die Karte die KI-Häuser fest (Name, Volk, Wappen, Farbe, Startbetriebe auf Plätzen der Karte); solche Karten und
//  Spielstände laden weiter. Seitdem nennt eine Karte keine Häuser mehr – jede Partie würfelt ihre KI-Gegner (Partiebesetzung) –, nur noch
//  die Umland-Startplätze („umland“) und die Familiennamen der Stadt („hausnamen“). Ohne Karte gilt Startbelegung.Ersatz – die Häuser, die es vor Format 2 fest im Code gab.
//  Wie viele Gesellen und Umlandkräfte ein Startbetrieb bekommt, bleibt eine Regel des Kerns (Partieaufbau).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Startbetrieb eines KI-Hauses: welche Betriebsart, auf welchem Platz der Karte (nur im Umland), was er zuerst herstellt.</summary>
public sealed class Startbetrieb
{
    /// <summary>
    /// Kennung des Umlandbetriebs in der Karte (dessen <c>betrieb</c> ist der Name des Betriebs) – leer für die Werkstatt in der Stadt: Sie hat
    /// keinen Platz in der Karte, die Partie lost sie in eine Bauzone (<see cref="Startortlos"/>, seit 16.11.2026). Nur die Karte in einem alten
    /// Spielstand nennt hier noch einen Werkstattplatz.
    /// </summary>
    public string Platz { get => _platz; init => _platz = value ?? ""; }
    private readonly string _platz = "";

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

    /// <summary>
    /// Glaube des Oberhaupts; null = Startglaube des Volkes (wie vor der Startaufstellung). Die gelosten Häuser einer Partie haben ihn immer
    /// (<see cref="Startaufstellung"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Glaube.Konfession? Glaube { get; init; }

    public Geschlecht Geschlecht { get; init; }
    public Wappen Wappen { get; init; } = Wappen.Vorlagen[0];

    /// <summary>
    /// Farbe des Hauses in der Stadt (Fahnen, Dächer), als „#rrggbb“. Nur die Darstellung liest sie. In einer Kartendatei steht sie seit dem
    /// 17.11.2026 nicht mehr (ältere Karten nennen sie noch, dann gilt sie weiter): Beim Aufbau der Partie bekommt jedes Haus die Hausfarbe
    /// seines Wappens (<see cref="Wappen.Hausfarbe"/>, <see cref="Startbelegung.MitHausfarben"/>).
    /// </summary>
    public string? Farbe { get; init; }

    /// <summary>Die Farbe, die das Haus trägt: die feste einer älteren Karte, sonst die seines Wappens.</summary>
    [JsonIgnore]
    public string Hausfarbe => Farbe ?? Wappen.Hausfarbe();

    /// <summary>Kennung des Wohnhausplatzes in der Karte.</summary>
    public string? Wohnhaus { get; init; }

    /// <summary>Die Startbetriebe in dieser Reihenfolge (zuerst die Werkstatt).</summary>
    public List<Startbetrieb> Betriebe { get; init; } = new();

    /// <summary>
    /// Ein Haus, das nicht in der Karte steht, sondern für einen freien Hof gewürfelt wird (<see cref="Partiebesetzung"/>): Name, Beruf, Volk
    /// und Glaube würfelt die <see cref="Startaufstellung"/>; Wappen, Farbe und Geschlecht hat es schon. Nur während des Aufbaus.
    /// </summary>
    [JsonIgnore]
    internal bool Gewuerfelt { get; init; }
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

    /// <summary>
    /// Farbe des Spielerhauses, als „#rrggbb“. In einer Kartendatei steht sie seit dem 17.11.2026 nicht mehr (ältere Karten nennen sie noch,
    /// dann gilt sie weiter): Beim Aufbau der Partie bekommt die Partiekarte hier die Hausfarbe des Wappens (<see cref="Wappen.Hausfarbe"/>,
    /// <see cref="Startbelegung.MitHausfarben"/>) – so steht sie im Spielstand und ändert sich im laufenden Spiel nicht.
    /// </summary>
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

    /// <summary>
    /// Die Umland-Startplätze der Karte (Abschnitt „umland“, seit 17.11.2026 – die Karten nennen keine festen Häuser mehr): Umlandbetriebe der
    /// Karte (Kennung und Betriebsart), die mit den gewürfelten KI-Häusern ins Spiel kommen – mit N gewürfelten Häusern die ersten N, in dieser
    /// Reihenfolge (so viele wie früher mit den ersten N Häusern der Karte). Wer welchen bekommt, lost die <see cref="Startaufstellung"/>; die
    /// übrigen stehen frei zur Pacht. Null: keine. Nie in der Partiekarte eines Spielstands.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Startbetrieb>? Umland { get; init; }

    /// <summary>
    /// Familiennamen dieser Stadt (Abschnitt „hausnamen“, ohne „Haus“ davor; seit 17.11.2026): Ein gewürfeltes KI-Haus zieht seinen Namen aus
    /// diesen und den Familiennamen seines Volkes (<see cref="Partiebesetzung.NameWuerfeln"/>), keiner doppelt. Null: nur die Namen des Volkes.
    /// Nie in der Partiekarte eines Spielstands.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Hausnamen { get; init; }

    /// <summary>
    /// Die Belegung einer Partie seit „nur noch Bauzonen“ (15.11.2026, <see cref="Stadtkarte.OhneStadtplaetze"/>): Sie nennt nur noch, wer
    /// mitspielt (Name, Wappen, Farbe, Umlandbetriebe). Wo die Häuser in der Stadt wohnen und arbeiten, steht als Grundstück im Spielstand –
    /// die Plätze hier (Wohnhaus, Werkstatt) gelten nicht und werden nicht geprüft. Nie in einer Kartendatei.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OhnePlaetze { get; internal set; }

    /// <summary>Die Namen aller KI-Häuser (für das Menü: Diesen Namen darf der Spieler nicht nehmen).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Rivalennamen => Haeuser.Select(h => h.Name).ToList();

    /// <summary>Die Wappen aller KI-Häuser (im Menü nicht wählbar).</summary>
    [JsonIgnore]
    public IReadOnlyList<Wappen> Rivalenwappen => Haeuser.Select(h => h.Wappen).ToList();

    /// <summary>
    /// Dieselbe Belegung, in der jedes KI-Haus seine Farbe nennt (<see cref="Starthaus.Hausfarbe"/>: die feste einer älteren Karte, sonst
    /// die seines Wappens) und der Spieler die seines Wappens <paramref name="spielerWappen"/>, wenn die Karte keine feste nennt (17.11.2026).
    /// So steht jede Farbe in der Partiekarte und im Spielstand. Häuser, die ihre Farbe schon nennen, bleiben dieselben Objekte.
    /// </summary>
    internal Startbelegung MitHausfarben(Wappen spielerWappen) => new()
    {
        Spieler = Spieler.Farbe is not null ? Spieler
            : new Spielerstart { Wohnhaus = Spieler.Wohnhaus, Farbe = spielerWappen.Hausfarbe(), Werkstatt = Spieler.Werkstatt, JeBeruf = Spieler.JeBeruf },
        Haeuser = Haeuser.Select(h => h.Farbe is not null ? h : new Starthaus
        {
            Name = h.Name, Volk = h.Volk, Glaube = h.Glaube, Geschlecht = h.Geschlecht, Wappen = h.Wappen, Farbe = h.Hausfarbe, Wohnhaus = h.Wohnhaus,
            Betriebe = h.Betriebe, Gewuerfelt = h.Gewuerfelt,
        }).ToList(),
        OhnePlaetze = OhnePlaetze,
    };

    /// <summary>Die Farbe eines Hauses („#rrggbb“) – des Spielers, wenn <paramref name="istSpieler"/>, sonst des KI-Hauses mit diesem Namen; null, wenn keine.</summary>
    public string? Farbe(string inhaber, bool istSpieler = false) =>
        istSpieler ? Spieler.Farbe : Haeuser.FirstOrDefault(h => h.Name == inhaber)?.Farbe;

    /// <summary>
    /// Ohne Karte: die drei KI-Häuser, die es vor Format 2 fest im Code gab (Eisenbart, Eichwald, Kieselbach), ohne Plätze.
    /// Die Werte stehen genauso im Abschnitt „start“ der Greybox-Karte.
    /// </summary>
    public static readonly Startbelegung Ersatz = new()
    {
        Spieler = new Spielerstart { Werkstatt = new Startwerkstatt { Name = "{art} am Weinmarkt" } },
        Haeuser =
        {
            new Starthaus
            {
                Name = Partieaufbau.Rivalenname, Volk = Partieaufbau.RivalenVolk, Geschlecht = Geschlecht.Mann, Wappen = Wappen.Eisenbart,
                Betriebe =
                {
                    new Startbetrieb { Art = Betriebsart.Giesserei, Produkt = "Dolch", ErsatzName = "Gießerei Eisenbart" },
                    new Startbetrieb { Art = Betriebsart.Mine, ErsatzName = "Grube Eisenbart" },
                },
            },
            new Starthaus
            {
                Name = Partieaufbau.ZweiterRivale, Volk = Partieaufbau.ZweiterRivaleVolk, Geschlecht = Geschlecht.Frau, Wappen = Wappen.Eichwald,
                Betriebe =
                {
                    new Startbetrieb { Art = Betriebsart.Tischlerei, ErsatzName = "{art} Eichwald" },
                    new Startbetrieb { Art = Betriebsart.Holzfaellerei, ErsatzName = "{art} Eichwald" },
                },
            },
            new Starthaus
            {
                Name = Partieaufbau.DritterRivale, Volk = Partieaufbau.DritterRivaleVolk, Geschlecht = Geschlecht.Mann, Wappen = Wappen.Kieselbach,
                Betriebe =
                {
                    new Startbetrieb { Art = Betriebsart.Maurerei, ErsatzName = "{art} Kieselbach" },
                    new Startbetrieb { Art = Betriebsart.Steinbruch, ErsatzName = "{art} Kieselbach" },
                },
            },
        },
    };
}
