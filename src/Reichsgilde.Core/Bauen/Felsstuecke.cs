using System.Text.Json.Serialization;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Felsstücke in der Karte (Felspinsel F3)
//
//  Unser Gelände ist ein Höhenraster mit 2 m Abstand: Böschungen gehen, senkrechte Wände nicht. Felswände entstehen darum aus Felsstücken –
//  3D-Modellen aus natur/natur_felsen.glb (F1), die halb im Hang stecken. Die Karte speichert je Stück Lage, Form, Gestein, Größe, Drehung,
//  Neigung zum Hang, Einsinktiefe und ein leichtes Kippen; dazu eine Grundgesteinsart je Karte (der Hang-Fels im Boden, einzelne Stücke dürfen
//  abweichen). Felsstücke sperren nichts (Nutzer 01.11.2026): Bauen, Wege und Bauzonen auf Fels bleiben erlaubt.
//
//  Datei (Feld „felsen“, freiwillig): { "grundgestein": "grauwacke", "stuecke": [ { "form": "wand", "punkt": [x, z], "drehung": 12.5,
//  "massstab": 1.1, "neigung": 0.5, "einsinken": 0.3, "kippen": [2.1, -3.4] }, … ] } – „gestein“ nur, wenn ein Stück vom Grundgestein abweicht,
//  „kippen“ nur, wenn es nicht 0 ist. Ohne Stücke und ohne Grundgestein fällt „felsen“ weg: alte Karten bleiben Byte für Byte gleich.
//
//  Lage im Raum (für die Darstellung, F5): Das Modell hat seinen Ursprung in der Mitte der Grundfläche, y = 0 ist die Unterkante, die Schauseite
//  zeigt nach +Z. Gedreht wird um die Senkrechte wie Gebäude und Pflanzen (Kartengeometrie.Dreh). „neigung“ (0 … 1) ist der Anteil, um den sich
//  das Stück zur Hangnormalen neigt (0 = steht senkrecht, 1 = liegt ganz am Hang an); „kippen“ sind zwei kleine Winkel in Grad um die eigene x-
//  und z-Achse; „einsinken“ (0 … 0,9) ist der Anteil der Höhe, um den das Stück unter den Boden reicht (Höhe des Bodens unter der Mitte).
//
//  Der Streu-Pinsel und der Stempel (Felsstreuung): gleicher Strich, gleicher Startwert, gleiche Karte – gleiche Stücke, auf jedem Rechner. Darum
//  Zufall nur aus der Zufallsquelle des Strichs, Winkel nur über DetMathe, alle Werte gerundet wie in der Datei (5 cm, 0,1°, 0,01).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Felsstück der Karte (Stadtkarte; siehe Kopf der Datei).</summary>
public sealed class Felsstueck
{
    /// <summary>Kennung der Form (<see cref="Felsformen"/>: „wand“, „kuppe“ …).</summary>
    public string Form { get; init; } = "";

    /// <summary>Gesteinsart, wenn sie vom Grundgestein der Karte abweicht (sonst null).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Gestein { get; init; }

    public double[] Punkt { get; init; } = { 0, 0 };

    /// <summary>Drehung um die Senkrechte in Grad (0 … 360).</summary>
    public double Drehung { get; init; }

    public double Massstab { get; init; } = 1;

    /// <summary>Anteil der Neigung zum Hang (0 … 1).</summary>
    public double Neigung { get; init; }

    /// <summary>Anteil der Höhe unter dem Boden (0 … 0,9).</summary>
    public double Einsinken { get; init; } = Felsformen.EinsinkenVorgabe;

    /// <summary>Kippen um die eigene x- und z-Achse in Grad – null: gerade.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Kippen { get; init; }
}

/// <summary>Die Felsen der Karte: Grundgestein (null: Vorgabe <see cref="Felsformen.GrundgesteinVorgabe"/>) und die Stücke.</summary>
public sealed class Kartenfelsen
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Grundgestein { get; init; }

    public List<Felsstueck> Stuecke { get; init; } = new();
}

/// <summary>Eine Felsform aus <c>natur_felsen.glb</c> (F1): Kennung, Name, Knoten im Modell (<c>&lt;Knoten&gt;_&lt;Stufe&gt;</c>), Maße in Metern.</summary>
/// <param name="Gerichtet">Hat die Form eine Schauseite (+Z), die hangabwärts zeigen soll (Wand, Rippe, Überhang, Felsnase)?</param>
public sealed record Felsform(string Kennung, string Name, string Knoten, double Breite, double Tiefe, double Hoehe, bool Gerichtet)
{
    /// <summary>Der Radius einer Kreisfläche gleich der Grundfläche (Meter, bei Maßstab 1) – für den Abstand der Stücke untereinander.</summary>
    public double Radius => 0.5 * Math.Sqrt(Breite * Tiefe);
}

/// <summary>Ein Stück einer festen Gruppe: Form, Lage vor der Drehung der Gruppe (Meter, +Z = hangabwärts), eigene Drehung, Maßstab, Einsinken dazu.</summary>
public sealed record Gruppenstueck(string Form, double X, double Z, double Drehung, double Massstab, double Einsinken = 0);

/// <summary>Eine feste Gruppe aus 3 bis 8 Stücken (Wand, Kuppe, Rippe) – ein Stempel setzt sie mit einem Klick.</summary>
public sealed record Felsgruppe(string Kennung, string Name, IReadOnlyList<Gruppenstueck> Stuecke);

/// <summary>Formen, Gesteinsarten und Gruppen der Felsstücke (siehe Kopf der Datei).</summary>
public static class Felsformen
{
    /// <summary>Einsinken eines neuen Stücks: 30 % seiner Höhe (Plan: 20–40 %, damit nie eine Unterkante schwebt).</summary>
    public const double EinsinkenVorgabe = 0.3;

    /// <summary>Höchstens so viel der Höhe darf ein Stück einsinken.</summary>
    public const double EinsinkenMax = 0.9;

    /// <summary>So weit kippt ein Stück höchstens zufällig um seine Achsen (Grad, Plan: ±8°).</summary>
    public const double KippenMax = 8;

    /// <summary>Kleinster und größter Maßstab eines Stücks.</summary>
    public const double MassstabMin = 0.25, MassstabMax = 4;

    /// <summary>Das Grundgestein einer Karte, die keins nennt.</summary>
    public const string GrundgesteinVorgabe = "grauwacke";

    /// <summary>Die Gesteinsarten (Kennungen wie in <c>game/daten/gestein.json</c>, ein Test vergleicht).</summary>
    public static readonly IReadOnlyList<string> Gesteine = new[] { "grauwacke", "trachyt", "buntsandstein", "kalkstein" };

    /// <summary>Die acht Formen aus F1 (Maße aus <c>tools/modelle/natur_felsen.py</c>, Übergabe F1).</summary>
    public static readonly IReadOnlyList<Felsform> Alle = new Felsform[]
    {
        new("wand", "Felswand", "Wand", 9.0, 3.2, 6.6, true),
        new("kuppe", "Felskuppe", "Kuppe", 5.9, 5.6, 3.2, false),
        new("rippe", "Felsrippe", "Rippe", 2.7, 9.7, 3.6, true),
        new("block", "Felsblock", "Block", 2.6, 2.4, 2.0, false),
        new("platte", "Felsplatte", "Platte", 3.1, 2.5, 0.8, false),
        new("ueberhang", "Überhang", "Ueberhang", 6.4, 4.5, 4.7, true),
        new("schuttbrocken", "Schuttbrocken", "Schuttbrocken", 0.63, 0.53, 0.42, false),
        new("felsnase", "Felsnase", "Felsnase", 3.2, 4.8, 3.2, true),
    };

    /// <summary>
    /// Die festen Gruppen: Wand (Felswand mit Seitenstücken, Block und Schutt am Fuß), Kuppe (Kuppe mit Blöcken und Platte), Rippe (drei Rippen
    /// nebeneinander, Schutt hangabwärts). +Z ist hangabwärts, die Schauseite.
    /// </summary>
    public static readonly IReadOnlyList<Felsgruppe> Gruppen = new Felsgruppe[]
    {
        new("wand", "Felswand", new Gruppenstueck[]
        {
            new("wand", 0, 0, 0, 1), new("wand", -6.6, 0.9, 14, 0.75), new("wand", 6.2, 1.1, -11, 0.8),
            new("block", -2.4, 3.4, 35, 0.8, 0.05), new("schuttbrocken", 1.6, 4.0, 70, 1.4), new("schuttbrocken", 3.9, 3.3, 140, 1.1),
        }),
        new("kuppe", "Felskuppe", new Gruppenstueck[]
        {
            new("kuppe", 0, 0, 0, 1), new("block", 3.6, 1.6, 40, 0.9), new("block", -3.3, -1.0, -25, 0.7),
            new("platte", 0.6, 3.8, 15, 0.9, -0.1), new("schuttbrocken", -2.0, 3.4, 200, 1.3),
        }),
        new("rippe", "Felsrippe", new Gruppenstueck[]
        {
            new("rippe", 0, 0, 0, 1), new("rippe", -3.2, -1.0, 6, 0.8), new("rippe", 3.0, 0.8, -5, 0.85),
            new("schuttbrocken", -0.8, 6.2, 30, 1.3), new("schuttbrocken", 1.6, 5.6, 110, 1.1), new("platte", 0.4, 7.4, 20, 0.6, -0.1),
        }),
    };

    /// <summary>Die Form mit dieser Kennung – oder null.</summary>
    public static Felsform? Finde(string? kennung) => Alle.FirstOrDefault(f => f.Kennung == kennung);

    /// <summary>Die Gruppe mit dieser Kennung – oder null.</summary>
    public static Felsgruppe? Gruppe(string? kennung) => Gruppen.FirstOrDefault(g => g.Kennung == kennung);

    /// <summary>Gibt es diese Gesteinsart?</summary>
    public static bool IstGestein(string? kennung) => kennung is not null && Gesteine.Contains(kennung);
}
