using System.Text.Json;
using System.Text.Json.Serialization;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Kartendatei einer Stadt (game/karten/<kennung>.json, Format 2)
//
//  Geschrieben von der Kartenwerkstatt oder dem Kartenskript (tools/Kartenbau), beschrieben in docs/LEVELDESIGN.md, „Kartenformat 2“.
//  Die Karte ist die einzige Quelle dafür, WO etwas steht: Gebäude (mit Verweis auf den Gebäudekatalog), Mauern mit Toren,
//  Wege, Bauzonen, Natur, Wasser und die Belegung am Start. WAS ein Gebäude ist (Modell, Grundfläche), steht im Katalog.
//  Der Kern rechnet mit Wegen, Bauzonen, Gebäudeflächen, Wohnhausplätzen und dem Start; alles Übrige liest er nur
//  für die Darstellung mit, damit Spielstand und Bild dieselbe Karte haben.
//
//  Koordinaten: Meter auf dem Boden, x = Osten, z = Süden – wie in der Stadtansicht.
//  Wie bei wirtschaft.json bekommt der Kern den Text, nicht den Pfad: Godot liest die Datei (res://),
//  die Tests lesen sie von der Platte.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Lage einer Bauzone (LEVELDESIGN §2). In der Datei kleingeschrieben („kern“, „mauerring“ …).</summary>
public enum Lage
{
    Kern,
    Mauerring,
    Vorstadt,
    Umland,
}

/// <summary>
/// Klasse eines Weges (LEVELDESIGN §5). Land-, Haupt-, Ring- und Wallstraße und Feldweg tragen Karren, Gassen, Leinpfade und Fußwege nur
/// Fußgänger. In der Datei kleingeschrieben („land“, „fussweg“).
/// </summary>
public enum Wegklasse
{
    Land,
    Haupt,
    Ring,
    Wall,
    Gasse,
    Leinpfad,

    /// <summary>Unbefestigter Weg im Umland, zu Hof, Grube oder Feld (Karren fahren hier; seit der Kartenwerkstatt, Schritt 2).</summary>
    Feldweg,

    /// <summary>Pfad nur für Fußgänger, etwa zu Weide und Wald (seit der Kartenwerkstatt, Schritt 2).</summary>
    Fussweg,
}

/// <summary>
/// Ein Weg der Karte: Name, Klasse und Stützpunkte der Mittellinie. Ohne <see cref="Kurve"/> verbinden gerade Strecken die Punkte, mit
/// <c>"glatt"</c> eine weiche Kurve (<see cref="Wegkurve"/>); gerechnet wird immer mit dem <see cref="Verlauf"/>.
/// </summary>
public sealed class Kartenweg
{
    public string Name { get; init; } = "";
    public Wegklasse Klasse { get; init; }

    /// <summary>Die Stützpunkte der Mittellinie, je Punkt [x, z].</summary>
    public List<double[]> Linie { get; init; } = new();

    /// <summary>Gefahrenfaktor um 1,0 (Vorschlag Leveldesign, noch ohne Wirkung).</summary>
    public double Gefahr { get; init; } = 1;

    /// <summary>Gezeichnete Breite in Metern (0 = nach der Klasse, <see cref="Standardbreite"/>).</summary>
    public double Breite { get; init; }

    /// <summary>„glatt“: weiche Kurve durch die Stützpunkte; null: gerade Strecken (wie bis zur Kartenwerkstatt, Schritt 2).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kurve { get; init; }

    /// <summary>
    /// Wo der Weg einen anderen Belag hat als die Vorgabe seiner Klasse (<see cref="Wegbelag.Vorgabe"/>: Karrenstraßen Fahrweg, sonst
    /// Trampelpfad) – Abschnitte in Metern entlang des <see cref="Verlauf"/>, spätere vor früheren (seit der Kartenwerkstatt, Schritt 5d).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Belagstelle>? Belag { get; init; }

    /// <summary>Läuft der Weg als weiche Kurve?</summary>
    [JsonIgnore]
    public bool IstGlatt => Kurve == Wegkurve.Glatt;

    private Wegverlauf? _verlauf;

    /// <summary>
    /// Die Mittellinie, wie Spiel, Prüfung und Bauwesen sie sehen: gerade = die Stützpunkte, glatt = die Kurve, etwa alle Meter ein Punkt
    /// (<see cref="Wegkurve.Verlauf"/>).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<double[]> Verlauf => (_verlauf ??= Wegkurve.Verlauf(Linie, IstGlatt)).Punkte;

    /// <summary>Die Länge des Verlaufs in Metern.</summary>
    [JsonIgnore]
    public double Laenge => Wegkurve.Laenge(Verlauf);

    /// <summary>Die gezeichnete Breite: aus der Karte, sonst nach der Klasse.</summary>
    [JsonIgnore]
    public double BreiteWirksam => Breite > 0 ? Breite : Standardbreite(Klasse);

    /// <summary>Fahren hier Karren? (Werkstätten brauchen ihren Eingang an einer Karrenstraße.)</summary>
    [JsonIgnore]
    public bool IstKarrenstrasse => TraegtKarren(Klasse);

    /// <summary>Fahren auf Wegen dieser Klasse Karren?</summary>
    public static bool TraegtKarren(Wegklasse k) => k is Wegklasse.Land or Wegklasse.Haupt or Wegklasse.Ring or Wegklasse.Wall or Wegklasse.Feldweg;

    /// <summary>Breite eines Weges ohne Angabe in der Karte (Meter) – wie die Stadtansicht sie zeichnet.</summary>
    public static double Standardbreite(Wegklasse k) => k switch
    {
        Wegklasse.Haupt => 5,
        Wegklasse.Land or Wegklasse.Wall => 4,
        Wegklasse.Ring or Wegklasse.Gasse or Wegklasse.Feldweg => 3,
        _ => 2, // Leinpfad, Fußweg
    };

    /// <summary>
    /// So schmal darf ein Weg dieser Klasse höchstens werden (Meter; LEVELDESIGN „Wege und Wegfindung“): Karrenstraßen 4 m (Ring 3 m), Feldweg
    /// 3 m, Gasse 2 m, Leinpfad und Fußweg 1,5 m. Die Greybox hält das ein (Landstraßen 4 m, Fußwege als Gasse 2 m).
    /// </summary>
    public static double Mindestbreite(Wegklasse k) => k switch
    {
        Wegklasse.Land or Wegklasse.Haupt or Wegklasse.Wall => 4,
        Wegklasse.Ring or Wegklasse.Feldweg => 3,
        Wegklasse.Gasse => 2,
        _ => 1.5, // Leinpfad, Fußweg
    };

    /// <summary>Anzeigename der Klasse („Landstraße“, „Fußweg“ …).</summary>
    public static string Anzeigename(Wegklasse k) => k switch
    {
        Wegklasse.Land => "Landstraße",
        Wegklasse.Haupt => "Hauptstraße",
        Wegklasse.Ring => "Ringstraße",
        Wegklasse.Wall => "Wallstraße",
        Wegklasse.Gasse => "Gasse",
        Wegklasse.Leinpfad => "Leinpfad",
        Wegklasse.Feldweg => "Feldweg",
        _ => "Fußweg",
    };

    /// <summary>Kürzester Abstand eines Punktes zur Mittellinie (Meter, auf dem <see cref="Verlauf"/>).</summary>
    public double Abstand(double x, double z)
    {
        var linie = Verlauf;
        var best = double.MaxValue;
        for (var i = 0; i + 1 < linie.Count; i++)
            best = Math.Min(best, Geometrie.AbstandZurStrecke(x, z, linie[i][0], linie[i][1], linie[i + 1][0], linie[i + 1][1]));
        return linie.Count == 1 ? Math.Sqrt(Math.Pow(x - linie[0][0], 2) + Math.Pow(z - linie[0][1], 2)) : best;
    }
}

/// <summary>Eine Bauzone: eine Fläche zwischen den Straßen, in der frei auf dem Raster gebaut wird (LEVELDESIGN §3).</summary>
public sealed class Bauzone
{
    public string Kennung { get; init; } = "";
    public Lage Art { get; init; }

    /// <summary>Umriss als Vieleck, je Punkt [x, z] (auf dem 2-m-Raster).</summary>
    public List<double[]> Flaeche { get; init; } = new();

    /// <summary>Liegt der Punkt in der Zone?</summary>
    public bool Enthaelt(double x, double z) => Geometrie.ImVieleck(x, z, Flaeche);

    /// <summary>Abstand eines Punktes zum Rand der Zone (0, wenn er drin liegt).</summary>
    public double Abstand(double x, double z)
    {
        if (Enthaelt(x, z)) return 0;
        var best = double.MaxValue;
        for (var i = 0; i < Flaeche.Count; i++)
        {
            var (a, b) = (Flaeche[i], Flaeche[(i + 1) % Flaeche.Count]);
            best = Math.Min(best, Geometrie.AbstandZurStrecke(x, z, a[0], a[1], b[0], b[1]));
        }
        return best;
    }
}

/// <summary>
/// Ein Gebäude der Karte: welcher Baustein des Katalogs (<see cref="Katalog"/>) wo steht. Plätze (Werkstatt-, Wohnhaus-, Einwohnerplatz)
/// haben kein festes Modell – das Spiel wählt es nach Beruf, Stufe oder Hausart.
/// </summary>
public sealed class Kartengebaeude
{
    /// <summary>Eindeutig in der Karte (z. B. „Wohnhaus1“, „Grube_Eisenbart“). Spielstände merken sich Wohnhausplätze unter ihr.</summary>
    public string Kennung { get; init; } = "";

    /// <summary>Kennung des Bausteins im Gebäudekatalog (z. B. „werkstattplatz“, „grube“, „dom“).</summary>
    public string Katalog { get; init; } = "";

    /// <summary>Anzeigename (z. B. „Rathaus“, „Weinmarkt“; Umlandstellen: „Weide“, „Feld“, „Wald“ wie im Kern).</summary>
    public string Name { get; init; } = "";

    /// <summary>Mitte der Grundfläche [x, z] (inklusive Vorplatz).</summary>
    public double[] Punkt { get; init; } = { 0, 0 };

    /// <summary>Grad um die Hochachse wie Godot <c>Rotation.Y</c>: Die Vorderseite zeigt bei 0 nach Süden (+z), bei 90 nach Osten (+x) … – beliebige Winkel.</summary>
    public double Drehung { get; init; }

    /// <summary>Größenfaktor des Modells und der Grundfläche (1 = wie im Katalog), z. B. ein kleinerer Felsberg.</summary>
    public double Massstab { get; init; } = 1;

    /// <summary>Werkstattplatz eines KI-Hauses oder Umlandbetrieb: der Name des Betriebs, der hier steht (genau wie im Spiel bzw. in „liegenschaften“).</summary>
    public string? Betrieb { get; init; }

    /// <summary>Wohnhausplatz: in welcher Lage er liegt (für den Bauplatz nach einem Abriss). Fehlt sie, gilt die nächste Bauzone.</summary>
    public Lage? Lage { get; init; }

    /// <summary>Konfession einer Kirche (nur gelesen).</summary>
    public string? Konfession { get; init; }

    /// <summary>Der Baustein aus dem Katalog (nach <see cref="Stadtkarte.Pruefen"/> gesetzt; null, wenn die Kennung unbekannt ist).</summary>
    [JsonIgnore]
    public Baustein? Baustein { get; internal set; }

    /// <summary>Die Art des Bausteins (<see cref="Bausteinart"/>) oder "", solange er nicht aufgelöst ist.</summary>
    [JsonIgnore]
    public string Art => Baustein?.Art ?? "";

    /// <summary>Steht die Vorderseite in einer der vier Himmelsrichtungen?</summary>
    [JsonIgnore]
    public bool IstRechtwinklig => Math.Abs(Drehung / 90 - Math.Round(Drehung / 90)) < 1e-6;

    /// <summary>
    /// Die Fläche, die das Gebäude für den Kern belegt (Größe des Bausteins mal <see cref="Massstab"/>). Bei schrägen Gebäuden
    /// das umschließende Rechteck mit Drehung 0. Null ohne Baustein oder ohne Größe.
    /// </summary>
    [JsonIgnore]
    public Grundriss? Grundriss
    {
        get
        {
            if (Baustein?.Groesse is not { Length: 2 } g) return null;
            var (b, t) = (g[0] * Massstab, g[1] * Massstab);
            if (IstRechtwinklig)
                return new Grundriss(Punkt[0], Punkt[1], b, t, (int)(((Math.Round(Drehung / 90) % 4) + 4) % 4));
            var w = Drehung * Math.PI / 180;
            var (c, s) = (Math.Abs(Math.Cos(w)), Math.Abs(Math.Sin(w)));
            return new Grundriss(Punkt[0], Punkt[1], b * c + t * s, b * s + t * c, 0);
        }
    }
}

/// <summary>Ein Tor in einer Mauer.</summary>
public sealed class Kartentor
{
    public string Name { get; init; } = "";
    public double[] Punkt { get; init; } = { 0, 0 };
    public bool SchliesstNachts { get; init; } = true;
}

/// <summary>Eine Mauer: Linie aus Punkten (offen oder geschlossen), gebaut aus einem Mauer-Baukasten des Katalogs, mit ihren Toren.</summary>
public sealed class Kartenmauer
{
    public string Name { get; init; } = "";

    /// <summary>Die Punkte der Mauerlinie [x, z]; jeder Punkt ist ein Knick oder ein Ende.</summary>
    public List<double[]> Linie { get; init; } = new();

    /// <summary>Läuft die letzte Strecke zurück zum ersten Punkt (Ring)?</summary>
    public bool Geschlossen { get; init; }

    /// <summary>Kennung des Mauer-Baukastens im Katalog (Art „mauer“).</summary>
    public string Katalog { get; init; } = "stadtmauer";

    /// <summary>Die Tore dieser Mauer; ein Tor sitzt auf der Strecke, der es am nächsten liegt.</summary>
    public List<Kartentor> Tore { get; init; } = new();
}

/// <summary>Ein Teil einer Naturgruppe: aus welchen Arten (Katalog-Kennung → Gewicht) und wie viele Kopien [von, bis].</summary>
public sealed class Naturteil
{
    public Dictionary<string, double> Arten { get; init; } = new();
    public int[] Anzahl { get; init; } = { 1, 1 };
}

/// <summary>Eine Gruppenvorlage: An jedem Streupunkt wird eine Vorlage nach Gewicht gewählt, ihre Teile in einer Scheibe mit dem Radius verteilt.</summary>
public sealed class Naturgruppe
{
    public double Gewicht { get; init; } = 1;

    /// <summary>Radius der Scheibe in Metern (0 = alles genau auf dem Streupunkt, z. B. einzelne Bäume).</summary>
    public double Radius { get; init; }

    public List<Naturteil> Teile { get; init; } = new();
}

/// <summary>Eine Wald- oder Streufläche: ein Vieleck, in dem die Darstellung aus dem Startwert Pflanzen verteilt.</summary>
public sealed class Naturflaeche
{
    public string Name { get; init; } = "";

    /// <summary>„wald“ oder „streu“ – nur zur Beschreibung, die Wirkung kommt aus den Gruppen.</summary>
    public string Art { get; init; } = "";

    public List<double[]> Flaeche { get; init; } = new();

    /// <summary>Löcher im Vieleck, in denen nichts wächst.</summary>
    public List<List<double[]>> Aussparungen { get; init; } = new();

    /// <summary>Streupunkte je 1.000 m² (vor dem Aussortieren: Auf Wegen, Gebäuden, Bauzonen und in der Stadt wächst nichts).</summary>
    public double Dichte { get; init; }

    /// <summary>Startwert des Zufalls – gleiche Karte, gleiches Bild.</summary>
    public int Startwert { get; init; }

    public List<Naturgruppe> Gruppen { get; init; } = new();
}

/// <summary>Eine einzeln gesetzte Pflanze (oder ein Stein …).</summary>
public sealed class Pflanze
{
    /// <summary>Kennung des Bausteins im Katalog (Art „natur“).</summary>
    public string Katalog { get; init; } = "";
    public double[] Punkt { get; init; } = { 0, 0 };
    public double Drehung { get; init; }
    public double Massstab { get; init; } = 1;

    /// <summary>Welche Variante (1 … Varianten des Bausteins); 0 = vom Zufall.</summary>
    public int Variante { get; init; }
}

/// <summary>
/// Eine Feldfläche (seit der Kartenwerkstatt, Schritt 5a; vor allem für den Randstreifen): ein Vieleck, das die Darstellung in Ackerstreifen
/// teilt (<see cref="FeldVerteilung"/>) – Streifen von <see cref="Streifen"/> Metern Breite in Richtung <see cref="Richtung"/>, in Stücke von
/// etwa <see cref="Laenge"/> Metern geschnitten, jedes Stück mit einer Frucht nach Gewicht. Nur Bild, keine Wirkung auf die Partie.
/// </summary>
public sealed class Feldflaeche
{
    public string Name { get; init; } = "";
    public List<double[]> Flaeche { get; init; } = new();

    /// <summary>Löcher im Vieleck (dort kein Acker).</summary>
    public List<List<double[]>> Aussparungen { get; init; } = new();

    /// <summary>Richtung der Streifen in Grad wie Godot <c>Rotation.Y</c>: 0 = die Streifen laufen Nord–Süd (entlang z), 90 = Ost–West.</summary>
    public double Richtung { get; init; }

    /// <summary>Breite eines Ackerstreifens in Metern.</summary>
    public double Streifen { get; init; } = FeldVerteilung.StreifenVorgabe;

    /// <summary>Mittlere Länge eines Stücks in Metern (jedes Stück zwischen 60 und 140 % davon).</summary>
    public double Laenge { get; init; } = FeldVerteilung.LaengeVorgabe;

    /// <summary>Startwert des Zufalls – gleiche Fläche, gleiche Äcker.</summary>
    public int Startwert { get; init; }

    /// <summary>Frucht → Gewicht (<see cref="FeldVerteilung.Fruechte"/>: weizen, roggen, gerste, hafer, flachs, brache).</summary>
    public Dictionary<string, double> Fruechte { get; init; } = new();
}

/// <summary>Die Natur der Karte: Flächen mit Dichte und Mischung, einzelne Pflanzen und Felder.</summary>
public sealed class Kartennatur
{
    public List<Naturflaeche> Flaechen { get; init; } = new();
    public List<Pflanze> Pflanzen { get; init; } = new();

    /// <summary>Feldflächen (seit Schritt 5a; fehlt in älteren Karten).</summary>
    public List<Feldflaeche> Felder { get; init; } = new();
}

/// <summary>
/// Fluss, Bach (Linie) oder See, Teich (Fläche) – seit der Kartenwerkstatt, Schritt 5c (<see cref="Wasserform"/>, docs/LEVELDESIGN.md, „Wasser“).
/// Ein Fluss fließt in Zeichenrichtung; jeder Stützpunkt ist [x, z], [x, z, breite] oder [x, z, breite, tiefe] (fehlt die Breite, gilt
/// <see cref="Breite"/>, sonst die Vorgabe der Art; fehlt die Tiefe, gilt <see cref="Tiefe"/>, sonst eine aus der Breite). Ein See hat einen
/// Umriss und einen Wasserstand (Höhe des Spiegels in Metern; fehlt er, liegt der Spiegel 0,4 m unter dem tiefsten Punkt des Ufers).
/// </summary>
public sealed class Gewaesser
{
    /// <summary>„fluss“, „bach“ (Linie) oder „see“, „teich“ (Fläche).</summary>
    public string Art { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Fluss, Bach: die Stützpunkte der Mittellinie in Fließrichtung, je Punkt [x, z] mit optional Breite und Tiefe.</summary>
    public List<double[]>? Linie { get; init; }

    /// <summary>Fluss, Bach: Breite für Punkte ohne eigene (0 = nach der Art, <see cref="Wasserform.Vorgabebreite"/>).</summary>
    public double Breite { get; init; }

    /// <summary>Tiefe in Metern für Punkte ohne eigene (Fluss) bzw. die größte Tiefe (See); null = aus der Breite bzw. 2 m.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Tiefe { get; init; }

    /// <summary>Fluss, Bach: Strömung in der Mitte in m/s; null = nach der Art (Bach 0,5, Fluss 1).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Stroemung { get; init; }

    /// <summary>See, Teich: der Umriss.</summary>
    public List<double[]>? Flaeche { get; init; }

    /// <summary>See, Teich: die Höhe des Wasserspiegels in Metern; null = 0,4 m unter dem tiefsten Punkt des Ufers.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Wasserstand { get; init; }

    /// <summary>Ist es ein Fluss oder Bach (Linie)? Sonst ein See oder Teich (Fläche).</summary>
    [JsonIgnore]
    public bool IstLinie => Wasserform.IstFliessend(Art);
}

/// <summary>Gelände der Art „feld“, „wald“, „huegel“, „wildnis“ (Köln).</summary>
public sealed class Gelaende
{
    public string Art { get; init; } = "";
    public string Name { get; init; } = "";
    public List<double[]> Flaeche { get; init; } = new();
}

/// <summary>Ein Hafen: Kaimauer, Anleger, Kran.</summary>
public sealed class Hafen
{
    public string Name { get; init; } = "";
    public List<double[]> Kante { get; init; } = new();
    public List<double[]> Anleger { get; init; } = new();
    public double[]? Kran { get; init; }
}

/// <summary>
/// Eine Fährstelle (<c>art</c> „faehre“, seit Schritt 5c): zwei Anleger <see cref="Von"/> und <see cref="Bis"/> an beiden Ufern, verbunden mit je
/// einem Weg. Einträge mit „bruecke“ stammen aus Format 1 (Köln) – Brücken entstehen seit Schritt 5c von selbst, wo ein Weg einen Fluss quert.
/// </summary>
public sealed class Uebergang
{
    public const string Faehre = "faehre", Bruecke = "bruecke";

    public string Art { get; init; } = "";
    public string Name { get; init; } = "";
    public double[] Von { get; init; } = { 0, 0 };
    public double[] Bis { get; init; } = { 0, 0 };
}

/// <summary>
/// Ein Fundort eines Rohstoffbetriebs (<c>vorkommen</c> der Datei). Der Kern liest ihn nur für die Größe einer Karte ohne <c>groesse</c>
/// (<see cref="Stadtkarte.Kartengrenzen"/>) – damit Spielstand und Kartenwerkstatt dieselbe ableiten.
/// </summary>
public sealed class Vorkommenstelle
{
    public string Kennung { get; init; } = "";
    public string Betrieb { get; init; } = "";
    public double[] Punkt { get; init; } = { 0, 0 };
}

/// <summary>Der Blick beim Start der Partie.</summary>
public sealed class Kartenkamera
{
    public double[] Punkt { get; init; } = { 0, 0 };
    public double Zoom { get; init; }

    /// <summary>Blickrichtung in Grad (0 = wie heute).</summary>
    public double Drehung { get; init; }
}

/// <summary>Die Kartendatei ist nicht lesbar oder widerspricht sich. Enthält alle gefundenen Fehler.</summary>
public sealed class KartenFehler : Exception
{
    public IReadOnlyList<string> Fehler { get; }

    public KartenFehler(IReadOnlyList<string> fehler)
        : base("Die Kartendatei ist fehlerhaft:\n  • " + string.Join("\n  • ", fehler))
    {
        Fehler = fehler;
    }
}

/// <summary>
/// Die Karte einer Stadt mit ihrem Gebäudekatalog. Liegt im Spielstand mit (wie die Wirtschaftsdaten), damit eine Partie mit genau
/// ihrer Karte weiterläuft – die Darstellung zeichnet deshalb die Karte der Partie (<see cref="Stadtwirtschaft.Karte"/>).
/// </summary>
public sealed class Stadtkarte
{
    /// <summary>Das Format, das dieser Leser kennt. Format 1 (bis 21.10.2026) liest er nicht mehr – alte Spielstände bekommen die neue Karte.</summary>
    public const int BekanntesFormat = 2;

    public int Format { get; init; } = BekanntesFormat;
    public string Kennung { get; init; } = "";
    public string Stadt { get; init; } = "";

    /// <summary>Rastergröße der Bauzonen in Metern (2).</summary>
    public double Raster { get; init; } = 2;

    public List<Kartengebaeude> Gebaeude { get; init; } = new();
    public List<Kartenmauer> Mauern { get; init; } = new();
    public List<Kartenweg> Wege { get; init; } = new();
    public List<Bauzone> Bauzonen { get; init; } = new();
    public Kartennatur Natur { get; init; } = new();
    public List<Gewaesser> Wasser { get; init; } = new();
    public List<Gelaende> Gelaende { get; init; } = new();
    public List<Hafen> Haefen { get; init; } = new();
    public List<Uebergang> Uebergaenge { get; init; } = new();

    /// <summary>Fundorte der Rohstoffbetriebe (nur für <see cref="Kartengrenzen"/> gelesen).</summary>
    public List<Vorkommenstelle> Vorkommen { get; init; } = new();

    /// <summary>Wer am Start wo wohnt und arbeitet – null: <see cref="Partie.Startbelegung.Ersatz"/>.</summary>
    public Startbelegung? Start { get; init; }

    public Kartenkamera? Kamera { get; init; }

    /// <summary>
    /// [Breite in x, Tiefe in z] des Spielfelds in Metern (seit der Kartenwerkstatt, Schritt 5a: 400 × 300 bis 2.000 × 2.000 in 50-m-Schritten).
    /// Fehlt sie (ältere Karten und Spielstände), leitet <see cref="Kartengrenzen"/> sie aus dem Inhalt ab.
    /// </summary>
    public double[]? Groesse { get; init; }

    /// <summary>Mitte des Spielfelds [x, z]; fehlt sie, liegt das Spielfeld um den Ursprung (den Marktplatz).</summary>
    public double[]? Mitte { get; init; }

    /// <summary>Breite des Randstreifens um das Spielfeld in Metern; fehlt sie, <see cref="Bauen.Kartengrenzen.RandVorgabe"/> (300).</summary>
    public double? Rand { get; init; }

    /// <summary>
    /// Das Höhenraster (seit der Kartenwerkstatt, Schritt 5b; <see cref="Hoehenraster"/>) – null oder leer: flach. Steht im Spielstand in
    /// derselben gepackten Form wie in der Kartendatei.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Hoehenraster? Hoehen { get; init; }

    private Gelaendeform? _gelaendeform;

    /// <summary>
    /// Das Gelände, wie die Darstellung es zeichnet (<see cref="Bauen.Gelaendeform"/>): Höhe des Bodens mit dem Bett der Wege, Mauern und
    /// Gewässer, Profile, Fundamente, das Wasser (<see cref="Gelaendeform.Wasser"/>). Ohne <see cref="Hoehen"/> und Wasser überall 0.
    /// </summary>
    [JsonIgnore]
    public Gelaendeform Gelaendeform => _gelaendeform ??= new Gelaendeform(Hoehen ?? new Hoehenraster(),
        () => Wege.Where(w => w.Linie.Count >= 2).Select(w => new Bettlinie(w.Verlauf.Select(Kartenort.Aus).ToList(), w.BreiteWirksam))
            .Concat(Mauern.Where(m => m.Linie.Count >= 2).Select(m => new Bettlinie(m.Linie.Select(Kartenort.Aus).ToList(), Gelaendeform.Mauerbett, m.Geschlossen)))
            .ToList(),
        () => Wasser,
        () => Gebaeude.Where(g => g.Baustein is { AmUfer: true, Groesse: { Length: 2 } }).Select(g => Kaimauer.Von(g.Name, g.Punkt[0], g.Punkt[1], g.Drehung,
            g.Baustein!.Groesse![0] * g.Massstab, g.Baustein.Groesse[1] * g.Massstab)).ToList());

    private Wasserplan? _wasserplan;

    /// <summary>Brücken und Furten (wo Wege Flüsse queren), Fähren und Wege im See (<see cref="Bauen.Wasserplan"/>, Schritt 5c).</summary>
    [JsonIgnore]
    public Wasserplan Wasserplan => _wasserplan ??= Bauen.Wasserplan.Von(this);

    /// <summary>Das Fundament eines Gebäudes der Karte (ganze Grundfläche mit Vorplatz): Höhe des Bodens im Haus und Tiefe des Sockels.</summary>
    public Fundament Fundament(Kartengebaeude g) =>
        Gelaendeform.Fundament(Bauen.Kartengrenzen.GebaeudeEcken(g.Baustein, g.Punkt[0], g.Punkt[1], g.Drehung, g.Massstab));

    private Kartengrenzen? _grenzen;

    /// <summary>
    /// Spielfeld, Randstreifen und Kamera-Grenzen der Karte (<see cref="Bauen.Kartengrenzen"/>): aus <see cref="Groesse"/>, <see cref="Mitte"/> und
    /// <see cref="Rand"/>, ohne Größe aus dem Inhalt abgeleitet (wie die Kartenwerkstatt).
    /// </summary>
    [JsonIgnore]
    public Kartengrenzen Kartengrenzen => _grenzen ??= GrenzenRechnen();

    private Kartengrenzen GrenzenRechnen()
    {
        var rand = Rand is > 0 ? Rand.Value : Bauen.Kartengrenzen.RandVorgabe;
        if (Groesse is { Length: 2 } g && g[0] > 0 && g[1] > 0)
            return new Kartengrenzen(g[0], g[1], Mitte is { Length: 2 } m ? new Kartenort(m[0], m[1]) : default, rand);
        // [x, z] – ein Stützpunkt eines Flusses kann Breite und Tiefe dahinter haben
        static IEnumerable<Kartenort> Orte(IEnumerable<double[]>? p) => (p ?? Enumerable.Empty<double[]>()).Where(x => x is { Length: >= 2 }).Select(Kartenort.Aus);
        var spiel = Gebaeude.SelectMany(x => Bauen.Kartengrenzen.GebaeudeEcken(x.Baustein, x.Punkt[0], x.Punkt[1], x.Drehung, x.Massstab) is { Count: > 0 } e
                ? e : Orte(new[] { x.Punkt }))
            .Concat(Mauern.SelectMany(m => Orte(m.Linie).Concat(Orte(m.Tore.Select(t => t.Punkt)))))
            .Concat(Bauzonen.SelectMany(z => Orte(z.Flaeche))).Concat(Orte(Vorkommen.Select(v => v.Punkt)))
            .Concat(Haefen.SelectMany(h => Orte(h.Kante).Concat(Orte(h.Anleger)).Concat(Orte(h.Kran is null ? null : new[] { h.Kran }))))
            .Concat(Uebergaenge.SelectMany(u => Orte(new[] { u.Von, u.Bis }))).Concat(Orte(Kamera is null ? null : new[] { Kamera.Punkt }));
        var rest = Wege.SelectMany(w => Orte(w.Verlauf))
            .Concat(Natur.Flaechen.SelectMany(n => Orte(n.Flaeche))).Concat(Natur.Felder.SelectMany(n => Orte(n.Flaeche)))
            .Concat(Orte(Natur.Pflanzen.Select(p => p.Punkt)))
            .Concat(Wasser.SelectMany(w => Orte(w.Linie).Concat(Orte(w.Flaeche)))).Concat(Gelaende.SelectMany(x => Orte(x.Flaeche)));
        return Bauen.Kartengrenzen.Ableiten(spiel, rest, rand);
    }

    /// <summary>Der Gebäudekatalog, mit dem die Karte gelesen wurde (steht nicht in der Kartendatei, aber im Spielstand).</summary>
    [JsonInclude]
    public Gebaeudekatalog? Katalog { get; internal set; }

    /// <summary>Die Belegung am Start: die aus der Karte, sonst der eingebaute Ersatz.</summary>
    [JsonIgnore]
    public Startbelegung Startbelegung => Start ?? Startbelegung.Ersatz;

    /// <summary>Das Gebäude mit dieser Kennung – oder null.</summary>
    public Kartengebaeude? GebaeudeMit(string? kennung) => kennung is null ? null : Gebaeude.FirstOrDefault(g => g.Kennung == kennung);

    /// <summary>Alle Gebäude einer Art (<see cref="Bausteinart"/>), in der Reihenfolge der Datei.</summary>
    public IEnumerable<Kartengebaeude> GebaeudeDerArt(string art) => Gebaeude.Where(g => g.Art == art);

    /// <summary>
    /// Die Wohnhausplätze in Konto-Reihenfolge: der des Spielers, dann die der KI-Häuser aus <see cref="Start"/> (auch wenn ein Haus nicht
    /// mitspielt), danach die übrigen Wohnhausplätze in der Reihenfolge der Datei (für weitere Häuser).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Kartengebaeude> Wohnhausplaetze
    {
        get
        {
            var alle = GebaeudeDerArt(Bausteinart.Wohnhausplatz).ToList();
            if (Start is not { } s) return alle;
            var vorn = new[] { s.Spieler.Wohnhaus }.Concat(s.Haeuser.Select(h => h.Wohnhaus))
                .Select(k => alle.FirstOrDefault(g => g.Kennung == k)).OfType<Kartengebaeude>().Distinct().ToList();
            return vorn.Concat(alle.Except(vorn)).ToList();
        }
    }

    /// <summary>
    /// Der feste Platz eines Betriebs (oder einer freien Liegenschaft) in der Karte: das Gebäude mit diesem <see cref="Kartengebaeude.Betrieb"/>,
    /// sonst der Startplatz des Spielers, wenn der Name zu ihm passt („Gießerei am Weinmarkt“). Null: kein fester Platz – neu gebaute
    /// Werkstätten stehen auf Grundstücken (<see cref="Bauwesen.PlatzVon"/>).
    /// </summary>
    public Kartengebaeude? PlatzVon(string betrieb)
    {
        if (Gebaeude.FirstOrDefault(g => g.Betrieb == betrieb) is { } fest) return fest;
        var s = Startbelegung.Spieler;
        foreach (var w in s.JeBeruf.Values.Prepend(s.Werkstatt))
            if (PasstZumMuster(betrieb, w.Name) && GebaeudeMit(w.Platz) is { } platz) return platz;
        return null;
    }

    /// <summary>„Gießerei am Weinmarkt“ passt zu „{art} am Weinmarkt“: Anfang und Ende stimmen, dazwischen steht etwas.</summary>
    private static bool PasstZumMuster(string name, string muster)
    {
        var i = muster.IndexOf("{art}", StringComparison.Ordinal);
        if (i < 0) return name == muster;
        var (vorn, hinten) = (muster[..i], muster[(i + 5)..]);
        return name.Length > vorn.Length + hinten.Length && name.StartsWith(vorn, StringComparison.Ordinal) && name.EndsWith(hinten, StringComparison.Ordinal);
    }

    internal static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }, // "klasse": "haupt" → Wegklasse.Haupt (ohne Rücksicht auf Groß/klein)
    };

    /// <summary>Liest die Karte aus dem JSON-Text der Kartendatei, löst ihre Gebäude im Katalog auf und prüft sie.</summary>
    /// <exception cref="KartenFehler">Wenn der Text kein gültiges JSON ist oder die Karte Fehler enthält (z. B. ein unbekannter Baustein).</exception>
    public static Stadtkarte AusJson(string json, Gebaeudekatalog katalog)
    {
        Stadtkarte? karte;
        try
        {
            karte = JsonSerializer.Deserialize<Stadtkarte>(json, Optionen);
        }
        catch (JsonException ex)
        {
            throw new KartenFehler(new[] { $"Kein gültiges JSON (Zeile {ex.LineNumber + 1}): {ex.Message}" });
        }
        if (karte is null)
            throw new KartenFehler(new[] { "Die Datei ist leer." });
        karte.Katalog = katalog;
        var fehler = karte.Pruefen();
        if (fehler.Count > 0)
            throw new KartenFehler(fehler);
        return karte;
    }

    /// <summary>Liest Karte und Katalog aus Dateien (für Tests und Werkzeuge).</summary>
    public static Stadtkarte AusDatei(string pfad, string katalogPfad) => AusJson(File.ReadAllText(pfad), Gebaeudekatalog.AusDatei(katalogPfad));

    /// <summary>Liest die Karte aus einer Datei, mit dem Katalog an seinem Platz: <c>karten/greybox.json</c> → <c>daten/katalog.json</c> daneben.</summary>
    public static Stadtkarte AusDatei(string pfad) => AusDatei(pfad, KatalogZu(pfad));

    /// <summary>Wo der Katalog zu einer Kartendatei liegt: <c>game/karten/x.json</c> → <c>game/daten/katalog.json</c>.</summary>
    public static string KatalogZu(string kartenpfad) =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(kartenpfad)))!, "daten", "katalog.json");

    /// <summary>
    /// Löst die Gebäude im Katalog auf und prüft, was Kern und Darstellung brauchen. Die Kartenregeln selbst (Straßenabstand, Tore,
    /// Kapazität) prüft schon das Werkzeug beim Schreiben. Leer = alles in Ordnung.
    /// </summary>
    public List<string> Pruefen()
    {
        var f = new List<string>();
        if (Format < BekanntesFormat)
            f.Add($"Format {Format} wird nicht mehr gelesen – die Karte mit dem Kartenwerkzeug neu schreiben (Format {BekanntesFormat}).");
        if (Format > BekanntesFormat) f.Add($"Format {Format} ist neuer als dieser Leser (kennt bis {BekanntesFormat}).");
        if (Raster <= 0) f.Add("raster muss größer als 0 sein.");
        if (Katalog is null) f.Add("Der Gebäudekatalog fehlt.");
        else f.AddRange(Katalog.Pruefen());

        static bool Punkt(double[]? p) => p is { Length: 2 };
        static bool Linie(IEnumerable<double[]> l) => l.All(Punkt);

        foreach (var w in Wege)
        {
            if (w.Linie.Count < 2) f.Add($"Weg „{w.Name}“: Die Linie braucht mindestens zwei Punkte.");
            if (!Linie(w.Linie)) f.Add($"Weg „{w.Name}“: Jeder Punkt hat genau zwei Zahlen [x, z].");
            if (w.Kurve is not null && !w.IstGlatt) f.Add($"Weg „{w.Name}“: kurve ist „{Wegkurve.Glatt}“ oder fehlt (nicht „{w.Kurve}“).");
            foreach (var b in w.Belag ?? new List<Belagstelle>())
                if (b.Von < 0 || b.Bis is { } bis && bis <= b.Von || b.Zustand is < 0 or > 1 || !Enum.IsDefined(b.Stufe))
                    f.Add($"Weg „{w.Name}“: Ein Belag braucht 0 ≤ von < bis, eine Stufe (trampelpfad, fahrweg, kies, pflaster) und zustand zwischen 0 und 1.");
        }
        foreach (var z in Bauzonen)
        {
            if (z.Flaeche.Count < 3) f.Add($"Bauzone {z.Kennung}: Die Fläche braucht mindestens drei Punkte.");
            if (!Linie(z.Flaeche)) f.Add($"Bauzone {z.Kennung}: Jeder Punkt hat genau zwei Zahlen [x, z].");
        }

        foreach (var g in Gebaeude)
        {
            var wer = $"Gebäude „{g.Kennung}“";
            if (string.IsNullOrWhiteSpace(g.Kennung)) f.Add($"Ein Gebäude ({g.Katalog}) hat keine Kennung.");
            g.Baustein = Katalog?.Finde(g.Katalog);
            if (g.Baustein is null)
                f.Add($"{wer}: Den Baustein „{g.Katalog}“ gibt es im Katalog nicht.");
            else if (!Bausteinart.ImGebaeude.Contains(g.Baustein.Art))
                f.Add($"{wer}: Ein Baustein der Art „{g.Baustein.Art}“ ({g.Katalog}) steht nicht unter gebaeude.");
            if (!Punkt(g.Punkt)) f.Add($"{wer}: punkt hat genau zwei Zahlen [x, z].");
            if (g.Massstab <= 0) f.Add($"{wer}: massstab muss größer als 0 sein.");
            if (g.Art == Bausteinart.Wohnhausplatz && !g.IstRechtwinklig)
                f.Add($"{wer}: Ein Wohnhausplatz steht rechtwinklig (drehung 0, 90, 180 oder 270).");
            if (g.Art == Bausteinart.Umlandbetrieb && string.IsNullOrWhiteSpace(g.Betrieb))
                f.Add($"{wer}: Ein Umlandbetrieb braucht den Namen seines Betriebs als betrieb.");
        }
        foreach (var doppelt in Gebaeude.GroupBy(g => g.Kennung).Where(x => x.Count() > 1))
            f.Add($"Gebäude: Die Kennung „{doppelt.Key}“ ist doppelt.");
        foreach (var doppelt in Gebaeude.Where(g => g.Betrieb is not null).GroupBy(g => g.Betrieb).Where(x => x.Count() > 1))
            f.Add($"Gebäude: Der Betrieb „{doppelt.Key}“ steht an mehreren Plätzen.");

        foreach (var m in Mauern)
        {
            if (m.Linie.Count < 2 || !Linie(m.Linie)) f.Add($"Mauer „{m.Name}“: Die Linie braucht mindestens zwei Punkte [x, z].");
            if (Katalog is not null && Katalog.Finde(m.Katalog) is not { Art: Bausteinart.Mauer })
                f.Add($"Mauer „{m.Name}“: Den Mauer-Baukasten „{m.Katalog}“ gibt es im Katalog nicht.");
            foreach (var t in m.Tore.Where(t => !Punkt(t.Punkt)))
                f.Add($"Tor „{t.Name}“: punkt hat genau zwei Zahlen [x, z].");
        }

        bool IstNatur(string kennung) => Katalog?.Finde(kennung) is { Art: Bausteinart.Natur };
        foreach (var n in Natur.Flaechen)
        {
            var wer = $"Naturfläche „{n.Name}“";
            if (n.Flaeche.Count < 3 || !Linie(n.Flaeche)) f.Add($"{wer}: Die Fläche braucht mindestens drei Punkte [x, z].");
            if (n.Dichte < 0) f.Add($"{wer}: dichte darf nicht negativ sein.");
            foreach (var teil in n.Gruppen.SelectMany(g => g.Teile))
            {
                if (teil.Anzahl is not { Length: 2 } a || a[0] < 0 || a[1] < a[0]) f.Add($"{wer}: anzahl ist [von, bis] mit 0 ≤ von ≤ bis.");
                if (Katalog is not null)
                    foreach (var art in teil.Arten.Keys.Where(k => !IstNatur(k)))
                        f.Add($"{wer}: Den Natur-Baustein „{art}“ gibt es im Katalog nicht.");
            }
        }
        if (Katalog is not null)
            foreach (var p in Natur.Pflanzen.Where(p => !IstNatur(p.Katalog)))
                f.Add($"Pflanze bei [{string.Join(", ", p.Punkt)}]: Den Natur-Baustein „{p.Katalog}“ gibt es im Katalog nicht.");

        foreach (var n in Natur.Felder)
        {
            var wer = $"Feld „{n.Name}“";
            if (n.Flaeche.Count < 3 || !Linie(n.Flaeche)) f.Add($"{wer}: Die Fläche braucht mindestens drei Punkte [x, z].");
            if (n.Streifen <= 0 || n.Laenge <= 0) f.Add($"{wer}: streifen und laenge müssen größer als 0 sein.");
        }
        foreach (var w in Wasser)
        {
            var wer = $"Gewässer „{w.Name}“";
            if (!Wasserform.Arten.Contains(w.Art)) f.Add($"{wer}: Die Art „{w.Art}“ gibt es nicht (erlaubt: {string.Join(", ", Wasserform.Arten)}).");
            else if (w.IstLinie && (w.Linie is not { Count: >= 2 } l || l.Any(p => p is not { Length: >= 2 and <= 4 })))
                f.Add($"{wer}: linie braucht mindestens zwei Punkte [x, z] (mit Breite und Tiefe [x, z, breite, tiefe]).");
            else if (!w.IstLinie && (w.Flaeche is not { Count: >= 3 } fl || !Linie(fl)))
                f.Add($"{wer}: flaeche braucht mindestens drei Punkte [x, z].");
            if (w.Breite < 0 || w.Tiefe is <= 0 || w.Stroemung is < 0) f.Add($"{wer}: breite, tiefe und stroemung dürfen nicht negativ sein (tiefe größer als 0).");
        }
        foreach (var u in Uebergaenge)
        {
            if (u.Art is not (Uebergang.Faehre or Uebergang.Bruecke)) f.Add($"Übergang „{u.Name}“: Die Art „{u.Art}“ gibt es nicht (faehre; bruecke aus Format 1).");
            if (!Punkt(u.Von) || !Punkt(u.Bis)) f.Add($"Übergang „{u.Name}“: von und bis haben genau zwei Zahlen [x, z].");
        }
        if (Groesse is not null && (Groesse.Length != 2 || Groesse.Any(x => x <= 0))) f.Add("groesse ist [Breite, Tiefe] mit zwei Zahlen größer als 0.");
        if (Mitte is not null && !Punkt(Mitte)) f.Add("mitte hat genau zwei Zahlen [x, z].");
        if (Rand is <= 0) f.Add("rand muss größer als 0 sein.");
        _grenzen = null; // die Gebäude haben jetzt ihren Baustein (für die abgeleitete Größe)

        if (Start is { } s) StartPruefen(s, f);
        return f;
    }

    private void StartPruefen(Startbelegung s, List<string> f)
    {
        void Wohnhaus(string wer, string? kennung)
        {
            if (kennung is not null && GebaeudeMit(kennung) is not { Art: Bausteinart.Wohnhausplatz })
                f.Add($"Start, {wer}: „{kennung}“ ist kein Wohnhausplatz der Karte.");
        }
        Wohnhaus("Spieler", s.Spieler.Wohnhaus);
        foreach (var (beruf, w) in s.Spieler.JeBeruf.Prepend(new("", s.Spieler.Werkstatt)))
            if (GebaeudeMit(w.Platz) is not { Art: Bausteinart.Werkstattplatz })
                f.Add($"Start, Spieler{(beruf == "" ? "" : $" ({beruf})")}: „{w.Platz}“ ist kein Werkstattplatz der Karte.");

        foreach (var h in s.Haeuser)
        {
            if (string.IsNullOrWhiteSpace(h.Name)) f.Add("Start: Ein Haus hat keinen Namen.");
            Wohnhaus(h.Name, h.Wohnhaus);
            if (h.Wappen.Fehler() is { } wf) f.Add($"Start, {h.Name}: {wf}");
            if (h.Betriebe.Count == 0) f.Add($"Start, {h.Name}: Ein Haus braucht mindestens einen Betrieb.");
            foreach (var b in h.Betriebe)
            {
                var platz = GebaeudeMit(b.Platz);
                if (platz is not { Art: Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb })
                    f.Add($"Start, {h.Name}: „{b.Platz}“ ist kein Werkstattplatz und kein Umlandbetrieb der Karte.");
                else if (string.IsNullOrWhiteSpace(platz.Betrieb))
                    f.Add($"Start, {h.Name}: Am Platz „{b.Platz}“ fehlt der Name des Betriebs (betrieb).");
                else if (platz.Art == Bausteinart.Umlandbetrieb && platz.Baustein!.BetriebsartWert != b.Art)
                    f.Add($"Start, {h.Name}: Auf „{b.Platz}“ steht {platz.Baustein.Anzeigename}, nicht {b.Art}.");
            }
        }
        foreach (var doppelt in s.Haeuser.GroupBy(h => h.Name).Where(x => x.Count() > 1))
            f.Add($"Start: Das Haus „{doppelt.Key}“ steht doppelt.");
        foreach (var doppelt in s.Haeuser.GroupBy(h => h.Wappen).Where(x => x.Count() > 1))
            f.Add($"Start: Zwei Häuser führen dasselbe Wappen ({string.Join(", ", doppelt.Select(h => h.Name))}).");
        var spielerPlaetze = s.Spieler.JeBeruf.Values.Prepend(s.Spieler.Werkstatt).Select(w => w.Platz).Distinct();
        foreach (var doppelt in s.Haeuser.SelectMany(h => h.Betriebe.Select(b => b.Platz)).Concat(spielerPlaetze).GroupBy(p => p).Where(x => x.Count() > 1))
            f.Add($"Start: Der Platz „{doppelt.Key}“ ist mehrfach vergeben.");
        foreach (var doppelt in s.Haeuser.Select(h => h.Wohnhaus).Prepend(s.Spieler.Wohnhaus).OfType<string>().GroupBy(p => p).Where(x => x.Count() > 1))
            f.Add($"Start: Der Wohnhausplatz „{doppelt.Key}“ ist mehrfach vergeben.");
    }

    /// <summary>Die Bauzone, in der der Punkt liegt – sonst die nächste (null ohne Bauzonen).</summary>
    public Bauzone? ZoneBei(double x, double z) =>
        Bauzonen.FirstOrDefault(b => b.Enthaelt(x, z))
        ?? Bauzonen.OrderBy(b => b.Abstand(x, z)).ThenBy(b => b.Kennung, StringComparer.Ordinal).FirstOrDefault();
}

/// <summary>Kleine Rechenhilfen für Punkte und Flächen auf dem Boden.</summary>
internal static class Geometrie
{
    /// <summary>Kürzester Abstand vom Punkt p zur Strecke a–b.</summary>
    public static double AbstandZurStrecke(double px, double pz, double ax, double az, double bx, double bz)
    {
        var (dx, dz) = (bx - ax, bz - az);
        var l2 = dx * dx + dz * dz;
        var t = l2 == 0 ? 0 : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / l2, 0, 1);
        var (qx, qz) = (ax + t * dx - px, az + t * dz - pz);
        return Math.Sqrt(qx * qx + qz * qz);
    }

    /// <summary>Liegt der Punkt im Vieleck? (Strahlverfahren, wie im Kartenwerkzeug.)</summary>
    public static bool ImVieleck(double x, double z, IReadOnlyList<double[]> vieleck)
    {
        var drin = false;
        for (int i = 0, j = vieleck.Count - 1; i < vieleck.Count; j = i++)
        {
            var (x1, z1, x2, z2) = (vieleck[i][0], vieleck[i][1], vieleck[j][0], vieleck[j][1]);
            if ((z1 > z) != (z2 > z) && x < (x2 - x1) * (z - z1) / (z2 - z1) + x1)
                drin = !drin;
        }
        return drin;
    }
}
