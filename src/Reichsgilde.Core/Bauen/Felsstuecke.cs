using System.Text.Json.Serialization;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Felsstücke in der Karte (Felspinsel F3)
//
//  Unser Gelände ist ein Höhenraster mit 2 m Abstand: Böschungen gehen, senkrechte Wände nicht. Felswände entstehen darum aus Felsstücken –
//  3D-Modellen aus natur/natur_felsen.glb (F1, 8 Formen) und natur/natur_felswand.glb (R4, 11 große Stücke), die halb im Hang stecken. Die Karte speichert je Stück Lage, Form, Gestein, Größe, Drehung,
//  Neigung zum Hang, Einsinktiefe und ein leichtes Kippen; dazu eine Grundgesteinsart je Karte (der Hang-Fels im Boden, einzelne Stücke dürfen
//  abweichen). Felsstücke sperren nichts (Nutzer 01.11.2026): Bauen, Wege und Bauzonen auf Fels bleiben erlaubt.
//
//  Datei (Feld „felsen“, freiwillig): { "grundgestein": "grauwacke", "stuecke": [ { "form": "wand", "punkt": [x, z], "drehung": 12.5,
//  "massstab": 1.1, "neigung": 0.5, "einsinken": 0.3, "kippen": [2.1, -3.4] }, … ] } – „gestein“ nur, wenn ein Stück vom Grundgestein abweicht,
//  „kippen“ nur, wenn es nicht 0 ist. Ohne Stücke und ohne Grundgestein fällt „felsen“ weg: alte Karten bleiben Byte für Byte gleich.
//  Seit R2 (Fels-Plan, 02.11.2026) dazu, beide nur, wenn sie nicht der Vorgabe entsprechen: „achsen“ [sx, sy, sz] – die Größe je Achse als
//  Faktoren auf „massstab“ (Breite, Höhe, Tiefe; je 0,25 … 4, auf 0,01) – und „versatz“, die Höhe in Metern über der Lage aus dem Boden
//  (± 20 m, auf 5 cm). „kippen“ [x, z] reicht jetzt von −180 bis 180° (vorher ±8°): x ist das Kippen (Pitch), z das Rollen (Roll).
//  Zusammen mit „drehung“ (Gieren, um die Senkrechte) sind das volle Euler-Winkel in der Reihenfolge Y · X · Z (Bauen/Felsraum.cs).
//
//  Lage im Raum (für die Darstellung, F5): Das Modell hat seinen Ursprung in der Mitte der Grundfläche, y = 0 ist die Unterkante, die Schauseite
//  zeigt nach +Z. Gedreht wird um die Senkrechte wie Gebäude und Pflanzen (Kartengeometrie.Dreh). „neigung“ (0 … 1) ist der Anteil, um den sich
//  das Stück zur Hangnormalen neigt (0 = steht senkrecht, 1 = liegt ganz am Hang an); „kippen“ sind zwei Winkel in Grad um die eigene x-
//  und z-Achse; „einsinken“ (0 … 0,9) ist der Anteil der Höhe, um den das Stück unter den Boden reicht (Höhe des Bodens unter der Mitte);
//  „versatz“ hebt (oder senkt) es danach senkrecht. Die ganze Lage im Raum rechnet Felsraum (für die Werkstatt und die Darstellung gleich).
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

    /// <summary>Kippen (x) und Rollen (z) um die eigenen Achsen in Grad (−180 … 180) – null: gerade.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Kippen { get; init; }

    /// <summary>Größe je Achse [Breite, Höhe, Tiefe] als Faktoren auf <see cref="Massstab"/> (0,25 … 4) – null: gleichmäßig (R2).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Achsen { get; init; }

    /// <summary>Höhenversatz in Metern (± 20) über der Lage aus Boden und Einsinken – 0 fehlt in der Datei (R2).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Versatz { get; init; }
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
/// <param name="Modell">Das Modell im Ordner <c>game/assets/modelle/</c> ohne Endung: F1 <c>natur/natur_felsen</c>, der Felswand-Satz (R4) <c>natur/natur_felswand</c>.</param>
/// <param name="EinsinkenFaktor">Wie viel vom Einsinken des Pinsels diese Form nimmt (1: alles; die großen Wände stecken nur 5–15 % tief, Gesimse setzen auf).</param>
/// <param name="Ueberlappung">So weit dürfen sich die Kreise der Grundfläche dieser Form mit denen der Nachbarn überdecken (Anteil der Summe der Radien).</param>
/// <param name="HangFormt">Hat die Form eine offene Rückseite, hinter der das Gelände steigen muss („Hang mitformen“, R5)?</param>
/// <param name="Schichtneigung">Neigung der Schichtfugen in der Wandebene in Grad (+ = nach +X steigend; aus dem Extra <c>schicht_neigung</c> des Knotens): die Bänder im Shader folgen ihr.</param>
/// <param name="Fusslinie">Wo die Vorderseite am Boden steht (z im Modell, Meter bei Maßstab 1; der Schuttfuß davor zählt nicht) – nur für „Hang mitformen“.</param>
/// <param name="Rueckwand">Wo die offene Rückseite endet (z im Modell, negativ): Dahinter muss das Gelände bis zur Oberkante reichen – nur für „Hang mitformen“.</param>
public sealed record Felsform(string Kennung, string Name, string Knoten, double Breite, double Tiefe, double Hoehe, bool Gerichtet,
    string Modell = Felsformen.ModellF1, double EinsinkenFaktor = 1, double Ueberlappung = Felspinsel.Ueberlappung, bool HangFormt = false,
    double Schichtneigung = 0, double Fusslinie = 0, double Rueckwand = 0)
{
    /// <summary>
    /// Der Radius des Kreises für Abstand und Dichte (Meter, bei Maßstab 1): ½ · √(Breite · Tiefe) – die Kreisfläche ist die der eingeschriebenen
    /// Ellipse (π/4 der Rechteckfläche), denn die Felsen sind rund und füllen ihr Rechteck nicht.
    /// </summary>
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

    /// <summary>So weit kippt ein Stück zufällig um seine Achsen, wenn der Pinsel keinen Bereich nennt (Grad, Plan: ±8°).</summary>
    public const double KippenMax = 8;

    /// <summary>Grenze für Kippen und Rollen in der Datei (Grad, −180 … 180; R2).</summary>
    public const double KippenGrenze = 180;

    /// <summary>Kleinster und größter Faktor je Achse („achsen“, R2).</summary>
    public const double AchseMin = 0.25, AchseMax = 4;

    /// <summary>Größter Höhenversatz nach oben wie nach unten (Meter, „versatz“, R2).</summary>
    public const double VersatzMax = 20;

    /// <summary>Kleinster und größter Maßstab eines Stücks.</summary>
    public const double MassstabMin = 0.25, MassstabMax = 4;

    /// <summary>Das Grundgestein einer Karte, die keins nennt.</summary>
    public const string GrundgesteinVorgabe = "grauwacke";

    /// <summary>Die Gesteinsarten (Kennungen wie in <c>game/daten/gestein.json</c>, ein Test vergleicht).</summary>
    public static readonly IReadOnlyList<string> Gesteine = new[] { "grauwacke", "trachyt", "buntsandstein", "kalkstein" };

    /// <summary>Die Modelle der Formen (Pfad ohne Endung unter <c>game/assets/modelle/</c>).</summary>
    public const string ModellF1 = "natur/natur_felsen", ModellWand = "natur/natur_felswand";

    /// <summary>
    /// Die Formen: die acht aus F1 (Maße aus <c>tools/modelle/natur_felsen.py</c>) und die elf des Felswand-Satzes (R4, <c>tools/modelle/natur_felswand.py</c>;
    /// Maße aus dem Netz der mittleren Stufe). Alle Formen haben ihren Ursprung in der Mitte der Grundfläche, die Schauseite zeigt nach +Z.
    /// </summary>
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
        // Felswand-Satz (R4): große Stücke mit offener Rückseite (sie stecken im Hang), Wände überlappen zu 40–70 %
        new("wand_gross_1", "Große Wand 1", "WandGross1", 20.4, 9.2, 16.0, true, ModellWand, 0.33, 0.6, true, 30.4, 2.0, -5.0),
        new("wand_gross_2", "Große Wand 2 (Säulenfels)", "WandGross2", 14.0, 10.0, 18.7, true, ModellWand, 0.33, 0.6, true, -19.3, 2.9, -5.0),
        new("wand_gross_3", "Große Wand 3", "WandGross3", 18.1, 10.4, 12.0, true, ModellWand, 0.33, 0.6, true, -21.6, 2.6, -5.5),
        new("wand_mittel_1", "Mittlere Wand 1", "WandMittel1", 9.2, 5.7, 7.9, true, ModellWand, 0.33, 0.55, true, -37.9, 0.9, -3.0),
        new("wand_mittel_2", "Mittlere Wand 2 (Eckstück)", "WandMittel2", 6.4, 5.1, 10.0, true, ModellWand, 0.33, 0.55, true, 24.6, 0.8, -3.0),
        new("wand_mittel_3", "Mittlere Wand 3", "WandMittel3", 8.0, 6.3, 5.1, true, ModellWand, 0.33, 0.55, true, -18.9, 1.6, -3.2),
        new("gesims_1", "Gesims 1", "Gesims1", 10.1, 3.9, 2.0, true, ModellWand, 0, 0.5),
        new("gesims_2", "Gesims 2", "Gesims2", 8.5, 3.8, 3.1, true, ModellWand, 0, 0.5),
        new("pfeiler_1", "Pfeiler", "Pfeiler1", 6.2, 5.9, 17.4, false, ModellWand, 0.25, 0.45, false, -10.7),
        new("pfeilerbuendel", "Pfeilerbündel", "Pfeilerbuendel", 10.2, 8.9, 13.9, false, ModellWand, 0.25, 0.45, true, -9.2, 4.0, -4.0),
        new("schuttkegel", "Schuttkegel", "Schuttkegel", 14.3, 8.8, 3.9, true, ModellWand, 0.1, 0.7, false, -30),
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
        // Große Wand (R4/R5): drei große Wände nebeneinander (rund ein Drittel überlappt), davor zwei mittlere und Schuttkegel; sinnvoll mit „Hang mitformen“
        new("wandgross", "Große Wand", new Gruppenstueck[]
        {
            new("wand_gross_1", 0, 0, 0, 1), new("wand_gross_2", -11.5, 1.0, 6, 0.95), new("wand_gross_3", 12.5, 1.5, -7, 0.95),
            new("wand_mittel_1", -5, 9, 10, 1), new("wand_mittel_3", 6.5, 8.5, -8, 1),
            new("schuttkegel", 0, 7.5, 0, 1), new("schuttkegel", -11, 8, 15, 0.8),
        }),
    };

    /// <summary>Die Form mit dieser Kennung – oder null.</summary>
    public static Felsform? Finde(string? kennung) => Alle.FirstOrDefault(f => f.Kennung == kennung);

    /// <summary>Die Gruppe mit dieser Kennung – oder null.</summary>
    public static Felsgruppe? Gruppe(string? kennung) => Gruppen.FirstOrDefault(g => g.Kennung == kennung);

    /// <summary>Gibt es diese Gesteinsart?</summary>
    public static bool IstGestein(string? kennung) => kennung is not null && Gesteine.Contains(kennung);
}
