using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

// ═════════════════════════════════════════════════════════════════════════════
//  Figuren-Baukasten (reine Logik, ohne Godot – ein Test übersetzt die Datei mit, FigurenBaukastenTests)
//
//  Eine Quelle für das Aussehen aller Figuren: Aus den Personendaten des Kerns (Volk, Geschlecht, Alter, Erbgut, Haar, Rolle)
//  entsteht ein festes Rezept (FigurRezept). Die Tracht kommt aus EINEM Katalog (FigurenBaukasten.Tracht): Ändert sich dort ein
//  Teil, ändert es sich bei Passanten, Fuhrleuten, Familie, Ratsherren und in den Innenräumen zugleich. Die Godot-Seite
//  (Stadtansicht.Baukasten.cs) baut aus dem Rezept die Figur. Alles ist nur Darstellung: kein Kern-Zufall, nichts im Spielstand;
//  was gewürfelt wird, kommt aus einem stabilen Hash der Kennung (FNV-1a) bzw. der Nummer der Person.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Kopfbedeckungen der Tracht (Teile der Figurmodelle; Probe-Rig: <c>Zub_stroh</c>, <c>Zub_barett</c>, <c>Zub_eisenhut</c>, Gugel, Haube, Gebende).</summary>
public enum Kopfbedeckung { Keine, Strohhut, Barett, Kapuze, Haube, Schleier, Eisenhut }

/// <summary>Was eine Figur trägt (Probe-Rig: <c>Zub_kiepe</c>, <c>Zub_buendel</c>, <c>Zub_tasche</c>, <c>Zub_stab</c>).</summary>
public enum Last { Keine, Kiepe, Buendel, Tasche, Stab, Hellebarde }

/// <summary>Eine Farbe ohne Godot (sRGB, 0–1); die Godot-Seite macht daraus eine <c>Color</c>.</summary>
public readonly record struct Farbwert(float R, float G, float B);

/// <summary>
/// Die Teile einer Tracht: Gewandfarbe (null: nach der Nummer aus der Palette), Kopfbedeckung, Last, Schürze, gebeugter Gang, Kopffarbe (null: wie das Gewand),
/// Amtskette (Bürgermeister, <see cref="FigurenBaukasten.Amtstracht"/>).
/// </summary>
public sealed record Trachtteile(Farbwert? Gewand, Kopfbedeckung Kopf, Last Last, bool Schuerze = false, bool Gebeugt = false, Farbwert? Kopffarbe = null,
    bool Amtskette = false);

/// <summary>
/// Das Rezept einer Figur: alles, was man ihr ansieht. Gleiche Person → gleiches Rezept, drinnen wie draußen.
/// </summary>
/// <param name="Kennung">Stabile Kennung der Person („E12“ Einwohner, „P7“ Familienperson, „A:Schmiede Ulm:3“ Arbeiter, „F123“ Fuhrmann, „S:…“ Statist).</param>
/// <param name="Nummer">Figurnummer für alle Streuungen der Darstellung (Startphase, Farben aus Paletten): fest aus der Kennung.</param>
/// <param name="Kind">Nicht volljährig (kleiner gezeigt).</param>
/// <param name="Grau">Ab <see cref="FigurenBaukasten.GrauAb"/> Jahren graues Haar.</param>
/// <param name="Haar">Haarfarbe: aus dem Aussehen der Person, wo es das gibt, sonst aus den Farb-Genen (<see cref="FigurenBaukasten.HaarAusGenen"/>).</param>
/// <param name="Rolle">Schlüssel im Trachtkatalog (Rollen des Kerns wie „Knecht“, dazu „Oberhaupt“, „Familie“, „Arbeiter.…“, „Fuhrmann“, „Gast“).</param>
/// <param name="Erbgut">Echtes Erbgut der Person (Familie) oder fest gewürfelt aus der Figurnummer (alle anderen).</param>
public sealed record FigurRezept(string Kennung, int Nummer, Volk Volk, bool Frau, bool Kind, bool Grau, Haarfarbe Haar, string Rolle, Trachtteile Tracht, Erbgut Erbgut);

public static class FigurenBaukasten
{
    /// <summary>Ab diesem Alter (Menschenjahre) graues Haar.</summary>
    public const int GrauAb = 55;

    // ── Farben des Katalogs (sRGB) ─────────────────────────────────────────
    public static readonly Farbwert Leinen = new(0.78f, 0.74f, 0.64f);
    public static readonly Farbwert Schwarz = new(0.12f, 0.11f, 0.12f);
    public static readonly Farbwert Stadtrot = new(0.72f, 0.12f, 0.12f);
    public static readonly Farbwert Ratsrot = new(0.42f, 0.10f, 0.12f);
    public static readonly Farbwert Barettschwarz = new(0.12f, 0.10f, 0.10f);
    /// <summary>Gold der Amtskette des Bürgermeisters.</summary>
    public static readonly Farbwert Amtsgold = new(0.90f, 0.74f, 0.30f);

    // ═════════════════════════════════════════════════════════════════════
    //  Der Trachtkatalog – die eine Stelle für Kleidung und Zubehör
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Tracht nach Rolle (Schlüssel wie im Kern, z. B. „Knecht“ auch für die Magd). Woran man jemanden erkennt: Ratsherr roter Mantel und Barett,
    /// Mönch braune Kutte mit Kapuze (Nonne schwarz mit weißem Schleier), Bettler graue Lumpen und gebeugt, Hirte Stab und Strohhut, Feldarbeiter
    /// Strohhut, Holzsammler Reisigbündel, Krämer Kiepe, Bote blaues Wams mit Tasche, Knecht und Magd Schürze, Verwitwete schwarz, Bürgerkinder
    /// kräftige Farben mit Barett. Dazu die Häuser: das Oberhaupt in der Farbe seines Hauses mit Barett (Frauen Haube), die übrige Familie in
    /// einfacher Tracht; die Arbeiter der Werkstätten mit Lederschürze nach der Stufe ihrer Laufbahn; der Fuhrmann mit Strohhut.
    /// </summary>
    /// <param name="hausfarbe">Farbe des Hauses (nur „Oberhaupt“ trägt sie).</param>
    public static Trachtteile Tracht(string rolle, bool frau, Farbwert? hausfarbe = null) => rolle switch
    {
        "Ratsherr" => new(new Farbwert(0.55f, 0.12f, 0.12f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Barett, Last.Keine, Kopffarbe: frau ? Leinen : Schwarz),
        "Bürgersohn" => new(frau ? new Farbwert(0.45f, 0.22f, 0.50f) : new Farbwert(0.18f, 0.30f, 0.58f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Barett, Last.Keine, Kopffarbe: frau ? Leinen : Schwarz),
        "Mönch" => frau ? new(Schwarz, Kopfbedeckung.Schleier, Last.Keine, Kopffarbe: new Farbwert(0.92f, 0.92f, 0.88f))
                        : new(new Farbwert(0.36f, 0.24f, 0.14f), Kopfbedeckung.Kapuze, Last.Keine),
        "Bettler" => new(new Farbwert(0.36f, 0.35f, 0.33f), Kopfbedeckung.Keine, Last.Keine, Gebeugt: true),
        "Witwer" => new(Schwarz, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Kopffarbe: Schwarz),
        "Hirte" => new(new Farbwert(0.40f, 0.36f, 0.24f), Kopfbedeckung.Strohhut, Last.Stab),
        "Feldarbeiter" => new(new Farbwert(0.58f, 0.50f, 0.36f), Kopfbedeckung.Strohhut, Last.Keine),
        "Holzsammler" => new(new Farbwert(0.42f, 0.30f, 0.20f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Buendel, Kopffarbe: Leinen),
        "Krämer" => new(new Farbwert(0.24f, 0.42f, 0.26f), frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Kiepe, Kopffarbe: Leinen),
        "Bote" => new(new Farbwert(0.20f, 0.32f, 0.62f), Kopfbedeckung.Keine, Last.Tasche),
        "Torwächter" => new(Stadtrot, Kopfbedeckung.Eisenhut, Last.Hellebarde, Kopffarbe: new Farbwert(0.45f, 0.45f, 0.48f)),
        "Knecht" => new(null, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Schuerze: true, Kopffarbe: Leinen),
        "Oberhaupt" => new(hausfarbe, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Barett, Last.Keine, Kopffarbe: frau ? Leinen : Barettschwarz),
        "Fuhrmann" => new(null, Kopfbedeckung.Strohhut, Last.Keine, Kopffarbe: Leinen),
        "Barde" => new(new Farbwert(0.22f, 0.36f, 0.34f), Kopfbedeckung.Barett, Last.Keine, Kopffarbe: Schwarz),
        "Prediger" => new(new Farbwert(0.88f, 0.85f, 0.76f), Kopfbedeckung.Keine, Last.Keine),
        "Arbeiter.Anfaenger" => Arbeitertracht(new Farbwert(0.62f, 0.55f, 0.40f), frau),
        "Arbeiter.Vorarbeiter" => Arbeitertracht(new Farbwert(0.30f, 0.21f, 0.15f), frau),
        "Arbeiter.Ork" => Arbeitertracht(new Farbwert(0.30f, 0.27f, 0.22f), frau),
        "Arbeiter.Leibeigener" => Arbeitertracht(new Farbwert(0.45f, 0.43f, 0.39f), frau),
        "Arbeiter" => Arbeitertracht(new Farbwert(0.42f, 0.30f, 0.20f), frau),
        _ => new(null, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Kopffarbe: Leinen), // Tagelöhner, Familie, Gäste und neue Rollen
    };

    /// <summary>
    /// Die Tracht im Amt des Bürgermeisters: seine eigene Tracht (Hausfarbe bzw. Ratsrot) mit der goldenen Amtskette und ohne Kopfbedeckung
    /// (Nutzer 14.11.: Kette statt Barett). Gilt im Ratssaal und in der Ratssitzung.
    /// </summary>
    public static Trachtteile Amtstracht(Trachtteile eigene) => eigene with { Kopf = Kopfbedeckung.Keine, Amtskette = true };

    private static Trachtteile Arbeitertracht(Farbwert gewand, bool frau) =>
        new(gewand, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Schuerze: true, Kopffarbe: Leinen);

    /// <summary>Katalogschlüssel eines Arbeiters nach der Stufe seiner Laufbahn (<see cref="Arbeiterbild"/>), nicht nach dem Namen der Arbeitskraft.</summary>
    public static string ArbeiterRolle(Arbeiterbild bild) => bild switch
    {
        { Anfaenger: true } => "Arbeiter.Anfaenger",
        { Vorarbeiter: true } => "Arbeiter.Vorarbeiter",
        { Volk: Volk.Ork } => "Arbeiter.Ork",
        { Arbeitskraft: "Leibeigener" } => "Arbeiter.Leibeigener",
        _ => "Arbeiter",
    };

    // ═════════════════════════════════════════════════════════════════════
    //  Rezepte aus den Personendaten des Kerns (nur lesend)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Ein Einwohner (Passant, Gläubiger im Dom, Torwächter): Figurnummer = Nummer des Einwohners, Erbgut fest daraus, Haar aus den Genen.</summary>
    public static FigurRezept Von(Einwohner e, int jahr)
    {
        var frau = e.Geschlecht == Geschlecht.Frau;
        var erbgut = ErbgutFuer(e.Volk, e.Nummer);
        return new FigurRezept($"E{e.Nummer}", e.Nummer, e.Volk, frau, false, e.Alter(jahr) >= GrauAb, HaarAusGenen(erbgut, e.Volk), e.Rolle,
            Tracht(e.Rolle, frau), erbgut);
    }

    /// <summary>Eine Person der Familie (Familienbuch): ihr echtes Erbgut, ihr Haar wie im Porträt; das Oberhaupt in der Farbe des Hauses.</summary>
    public static FigurRezept Von(Person p, int jahr, bool volljaehrig, bool oberhaupt, Farbwert hausfarbe)
    {
        var frau = p.Geschlecht == Geschlecht.Frau;
        var rolle = oberhaupt ? "Oberhaupt" : "Familie";
        return new FigurRezept(p.Kennung, Figurnummer(p.Kennung), p.Volk, frau, !volljaehrig, p.Alter(jahr) >= GrauAb, HaarVon(p.Aussehen.Haar, p.Erbgut, p.Volk),
            rolle, Tracht(rolle, frau, hausfarbe), p.Erbgut);
    }

    /// <summary>Ein Arbeiter eines Betriebs: Kennung aus Betrieb und Nummer, Haar wie im Porträt, Tracht nach der Stufe der Laufbahn.</summary>
    public static FigurRezept Von(ArbeiterZeile z, string betrieb) =>
        Arbeiter(betrieb, z.Nummer, Arbeiterbild.Von(z), z.Aussehen.Haar, z.Menschenjahre);

    /// <summary>Wie <see cref="Von(ArbeiterZeile, string)"/>, für einen <see cref="Reichsgilde.Core.Wirtschaft.Arbeiter"/> des Kerns.</summary>
    public static FigurRezept Von(Arbeiter a, string betrieb, Personalwesen? personal, long jetzt) =>
        Arbeiter(betrieb, a.Nummer, Arbeiterbild.Von(a, personal), a.Aussehen.Haar, a.Menschenjahre(jetzt));

    private static FigurRezept Arbeiter(string betrieb, int nummer, Arbeiterbild bild, Haarfarbe haar, double menschenjahre)
    {
        var kennung = $"A:{betrieb}:{nummer}";
        var figurnummer = Figurnummer(kennung);
        var frau = bild.Frau == true; // Orks und Oger haben nur das Männermodell (Arbeiterbild)
        var erbgut = ErbgutFuer(bild.Volk, figurnummer);
        var rolle = ArbeiterRolle(bild);
        return new FigurRezept(kennung, figurnummer, bild.Volk, frau, false, menschenjahre >= GrauAb, HaarVon(haar, erbgut, bild.Volk), rolle, Tracht(rolle, frau), erbgut);
    }

    /// <summary>Der Fuhrmann eines Karrens (Kern: abgeleitet, nicht gespeichert): Figurnummer = sein Startwert, Haar aus seinem Aussehen.</summary>
    public static FigurRezept Von(Fuhrmann f)
    {
        var frau = f.Geschlecht == Geschlecht.Frau;
        var erbgut = ErbgutFuer(f.Volk, f.Startwert);
        return new FigurRezept($"F{f.Startwert}", f.Startwert, f.Volk, frau, false, false, HaarVon(f.Aussehen.Haar, erbgut, f.Volk), "Fuhrmann",
            Tracht("Fuhrmann", frau), erbgut);
    }

    /// <summary>
    /// Ein Ratsherr der Stadt ohne Haus (Daten „politik“): Volk, Geschlecht und Haar aus den Daten, sonst Mensch, Mann, Haar aus den Genen.
    /// Häuser im Rat zeigen ihr Oberhaupt (<see cref="Von(Person, int, bool, bool, Farbwert)"/>).
    /// </summary>
    public static FigurRezept Ratsherr(string name, Volk volk, Geschlecht? geschlecht, Haarfarbe? haar)
    {
        var kennung = $"R:{name}";
        var nummer = Figurnummer(kennung);
        var frau = geschlecht == Geschlecht.Frau;
        var erbgut = ErbgutFuer(volk, nummer);
        return new FigurRezept(kennung, nummer, volk, frau, false, false, haar is { } h ? HaarVon(h, erbgut, volk) : HaarAusGenen(erbgut, volk), "Ratsherr",
            Tracht("Ratsherr", frau), erbgut);
    }

    /// <summary>
    /// Ein Statist ohne Person im Kern (Gast der Schänke, Gläubiger der Messe): fest je Platz (<paramref name="platz"/>), Volk und Geschlecht gibt der Raum vor.
    /// </summary>
    public static FigurRezept Statist(string platz, Volk volk, bool frau, string rolle = "Gast", Kopfbedeckung? kopf = null, bool grau = false)
    {
        var kennung = $"S:{platz}";
        var nummer = Figurnummer(kennung);
        var erbgut = ErbgutFuer(volk, nummer);
        frau = frau && volk is not (Volk.Ork or Volk.Oger); // Orks und Oger haben nur das Männermodell
        var tracht = Tracht(rolle, frau);
        if (kopf is { } k) tracht = tracht with { Kopf = k };
        return new FigurRezept(kennung, nummer, volk, frau, false, grau, HaarAusGenen(erbgut, volk), rolle, tracht, erbgut);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Blick und Sitz – eine Konvention für alle Figuren
    // ═════════════════════════════════════════════════════════════════════

    // Plätze (Arbeitsplatz.Drehung in den Räumen aus Grundkörpern, Anker der Raummodelle) blicken bei Drehung 0 nach −Z; beide Figurmodelle
    // (altes Rig und Passanten-Skelett) blicken nach +Z. Darum bekommt das Modell immer eine halbe Drehung mehr als der Platz.
    // Sitzen: Das alte Rig hat in „Sitzen“ das Hüftgelenk über dem Ursprung, das Passanten-Skelett 23–30 cm dahinter. Sitzpunkt ist darum
    // bei beiden das Hüftgelenk (Stadtansicht.Baukasten.cs, SitzVersatz) – sonst sitzt die Figur in Lehne und Bank (Nutzer 14.11.).

    /// <summary>Drehung des Modells (um Y) für einen Platz mit dieser Drehung.</summary>
    public static float Modelldrehung(float platzDrehung) => platzDrehung + MathF.PI;

    /// <summary>Wohin ein Platz mit dieser Drehung blickt (x, z), Länge 1.</summary>
    public static (float X, float Z) Vorwaerts(float platzDrehung) => (-MathF.Sin(platzDrehung), -MathF.Cos(platzDrehung));

    /// <summary>Die Drehung eines Platzes bei (<paramref name="vonX"/>, <paramref name="vonZ"/>), der zum Ziel blickt.</summary>
    public static float DrehungZu(float vonX, float vonZ, float zielX, float zielZ) => MathF.Atan2(-(zielX - vonX), -(zielZ - vonZ));

    /// <summary>Winkel (Grad, 0–180) zwischen dem Blick eines Platzes und der Richtung zum Ziel.</summary>
    public static float Blickfehler(float platzDrehung, float vonX, float vonZ, float zielX, float zielZ)
    {
        var (vx, vz) = Vorwaerts(platzDrehung);
        var dx = zielX - vonX;
        var dz = zielZ - vonZ;
        var laenge = MathF.Sqrt(dx * dx + dz * dz);
        if (laenge < 1e-6f) return 0;
        return MathF.Acos(Math.Clamp((vx * dx + vz * dz) / laenge, -1f, 1f)) * 180f / MathF.PI;
    }

    /// <summary>
    /// Wie weit vor dem Hüftgelenk die Rückseite des Unterschenkels unter der Sitzfläche liegt (Spielmeter, kleinster Wert über die Schleife „Sitzen“) –
    /// je Figurdatei aus tools/modelle/passanten_sitzen.py (Bericht „schienbein_spiel_m“; nach einem Neubau hier nachtragen, sitzpruefung.py liest
    /// dieselben Werte). Fehlt eine Datei, reicht ihr Unterschenkel über jede Sitzkante des Spiels hinaus (Menschen, Elben, Oger).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, float> SitzSchienbein = new Dictionary<string, float>
    {
        ["passant_probe_ork"] = 0.301f, ["passant_probe_zwerg"] = 0.087f, ["passant_probe_zwergin"] = 0.116f,
        ["passant_probe_halbling"] = 0.127f, ["passant_probe_halblingin"] = 0.111f,
    };

    /// <summary>
    /// Vorrücken (Spielmeter): Kleine Völker haben kurze Oberschenkel; ihr Unterschenkel hängt von der Sitzkante herab. Mit dem Hüftgelenk über der Sitzmitte
    /// stieße er an die Vorderkante tiefer Sitze – die Figur rückt so weit vor, dass er 1 cm davor bleibt. <paramref name="vorderkante"/>: Abstand der
    /// Sitzvorderkante von der Sitzmitte.
    /// </summary>
    public static float VorrueckenFuer(string datei, float vorderkante) =>
        SitzSchienbein.TryGetValue(System.IO.Path.GetFileNameWithoutExtension(datei), out var s) ? MathF.Max(0f, vorderkante + 0.01f - s) : 0f;

    /// <summary>Name des Formschlüssels der Figuren (Stoff unter den Oberschenkeln auf die Sitzfläche, tools/modelle/passanten_sitzen.py).</summary>
    public const string SitzSchluesselName = "Sitzen";

    /// <summary>
    /// Die sitzenden Arbeitsclips (aus den Definitionen in tools/modelle/figuren.py, übertragen von passanten_arbeit.py): in ihnen fällt der Rock
    /// wie im Clip „Sitzen“ (Entscheidung Teamleiter 14.11.2026: ohne Formschlüssel stand er waagrecht über die Knie).
    /// </summary>
    public static readonly IReadOnlySet<string> SitzendeArbeitsclips = new HashSet<string>
        { "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen", "Putzen", "Rechnen", "Kutschieren" };

    /// <summary>Wert des Formschlüssels „Sitzen“ für einen Clip: 1 im Clip „Sitzen“ und in den sitzenden Arbeitsclips, sonst 0 (beim Verlassen zurück).</summary>
    public static float SitzSchluessel(string klip) => klip == "Sitzen" || SitzendeArbeitsclips.Contains(klip) ? 1f : 0f;

    /// <summary>
    /// Name der Zielform für einen Clip (Rock folgt dem Knie, Nutzer 15.11.2026): je Sitzhaltung eine eigene, „Sitzen“ bzw. „Sitzen_&lt;Clip&gt;“
    /// (tools/modelle/passanten_sitzen.py); null bei allen anderen Clips (dann stehen alle Zielformen auf 0).
    /// </summary>
    public static string? SitzForm(string klip) => klip == "Sitzen" ? SitzSchluesselName : SitzendeArbeitsclips.Contains(klip) ? $"{SitzSchluesselName}_{klip}" : null;

    /// <summary>Alle Zielformen der Figuren in fester Reihenfolge (Sitzen, dann die sitzenden Arbeitsclips).</summary>
    public static IReadOnlyList<string> SitzFormen { get; } =
        new[] { "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen", "Putzen", "Rechnen", "Kutschieren" }.Select(c => $"{SitzSchluesselName}_{c}").Prepend(SitzSchluesselName).ToArray();

    /// <summary>Höhe der Sitzflächen in den Räumen aus Grundkörpern (Wohnstube, Kontor, Ratssaal, Dom): Gesäß des Mannes im Clip „Sitzen“
    /// bei Füßen auf dem Boden, mit <see cref="SitzLuft"/> (Prüfung tools/modelle/sitzpruefung.py).</summary>
    public const float Sitzflaeche = 0.44f;

    /// <summary>Oberkante der Bänke in den Raummodellen von Schänke und Kirche (wirt_innen.glb, prediger_innen.glb).</summary>
    public const float ModellSitzflaeche = 0.43f;

    /// <summary>Luft zwischen Gesäß (Sitzhöhe des Clips) und Sitzfläche: Darunter beginnt die Hüfte die Bank zu berühren.</summary>
    public const float SitzLuft = 0.01f;

    /// <summary>
    /// Höhe des Modellursprungs (Fußsohle) über dem Boden des Platzes, damit das Gesäß knapp über der Sitzfläche liegt: Sitzfläche + Luft − Gesäßhöhe
    /// der Figur; nie unter dem Boden (auf einer niedrigeren Bank schwebt das Gesäß knapp darüber, die Füße stehen).
    /// </summary>
    public static float SitzUrsprung(float sitzflaeche, float gesaesshoehe) => MathF.Max(0, sitzflaeche + SitzLuft - gesaesshoehe);

    // ═════════════════════════════════════════════════════════════════════
    //  Hilfen
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Feste Figurnummer aus der Kennung (FNV-1a über die Zeichen; in jedem Lauf und auf jedem System gleich), 2 000 000 bis 51 999 999.</summary>
    public static int Figurnummer(string kennung)
    {
        var h = 2166136261u;
        foreach (var c in kennung)
        {
            h ^= c;
            h = unchecked(h * 16777619u); // der Überlauf gehört zum Verfahren (auch in Projekten mit Überlaufprüfung)
        }
        return 2_000_000 + (int)(h % 50_000_000u);
    }

    /// <summary>Erbgut einer Figur ohne eigenes Erbgut im Kern: fest aus Volk und Figurnummer (wie bisher bei den Passanten), nichts im Spielstand.</summary>
    public static Erbgut ErbgutFuer(Volk volk, int nummer) => Erbgut.Wuerfeln(Erbgutvorlage.Standard, 7000 + nummer, volk);

    /// <summary>Haar wie im Porträt, wo die Person eines hat (nicht „kahl“); sonst aus den Farb-Genen.</summary>
    private static Haarfarbe HaarVon(Haarfarbe haar, Erbgut erbgut, Volk volk) => haar != Haarfarbe.Kahl ? haar : HaarAusGenen(erbgut, volk);

    /// <summary>Haarfarben von dunkel nach hell (Rot steht außerhalb der Reihe).</summary>
    private static readonly Haarfarbe[] HellReihe =
        { Haarfarbe.Schwarz, Haarfarbe.Dunkelbraun, Haarfarbe.Braun, Haarfarbe.Kastanie, Haarfarbe.Blond, Haarfarbe.Hellblond, Haarfarbe.Silberblond };

    /// <summary>Ab diesem Wert des Gens „haar_rot“ (0–255) ist das Haar rot, wenn das Volk rotes Haar kennt.</summary>
    public const int RotAb = 192;

    /// <summary>
    /// Haarfarbe aus den Farb-Genen, nur aus der Palette des Volks (<see cref="Aussehenspalette.Haarfarben"/>): mit viel „haar_rot“ rot, sonst nach
    /// „haar_helligkeit“ die passende Stufe der übrigen Farben von dunkel nach hell. Kennt das Volk nur „kahl“, bleibt es kahl.
    /// </summary>
    public static Haarfarbe HaarAusGenen(Erbgut erbgut, Volk volk)
    {
        var palette = Aussehenspalette.Haarfarben(volk);
        if (palette.Contains(Haarfarbe.Rot) && erbgut.SichtbarerWert("haar_rot") >= RotAb) return Haarfarbe.Rot;
        var reihe = HellReihe.Where(palette.Contains).ToArray();
        if (reihe.Length == 0) return palette.Contains(Haarfarbe.Rot) ? Haarfarbe.Rot : Haarfarbe.Kahl;
        return reihe[Math.Min(reihe.Length - 1, erbgut.SichtbarerWert("haar_helligkeit") * reihe.Length / 256)];
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Prüfhilfe --figurprobe=<volk>:<m|w>:<clip> (nur zum Ansehen; Stadtansicht.Pruefhilfe.cs): welche Figur in welchem Clip
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Was die Prüfhilfe <c>--figurprobe</c> zeigen soll: Volk, Geschlecht und Clip der Passanten-Dateien.</summary>
    public sealed record FigurProbe(Volk Volk, bool Frau, string Clip);

    /// <summary>Die Clips der Passanten-Dateien: die zwölf der Passanten und die 35 Arbeitsbewegungen (tools/modelle/passanten_arbeit.py, Namen wie im alten Rig).</summary>
    public static readonly IReadOnlyList<string> FigurProbeClips = new[]
    {
        "Stehen", "Gehen", "Tragen", "Reden", "Sitzen", "Hacken", "Buecken", "Kauern", "Betteln", "Ziehen", "Nebenhergehen", "SitzenBock",
        "Haemmern", "Blasebalg", "Schleifen", "Feinarbeit", "Hobeln", "Saegen", "Drechseln", "Gatter", "Behauen", "Bildhauen",
        "Moersern", "Ruehren", "Destillieren", "Mischen", "Pressen", "Zapfen", "Trinken", "Laute", "Kerzenziehen", "Schoepfen",
        "Schreiben", "Predigen", "Beten", "Knien", "Knacken", "Zaehlen", "Schlafen", "Putzen", "Fechten", "Rechnen",
        "Wiegen", "Kutschieren", "Hueten", "Eilen", "Gebeugt",
    };

    /// <summary>
    /// Liest <c>&lt;volk&gt;:&lt;m|w&gt;:&lt;clip&gt;</c> (z. B. „Zwerg:m:Hobeln“, Groß- und Kleinschreibung egal). null bei unbekanntem Volk, Geschlecht
    /// oder Clip und bei einer Ogerin (die Oger-Datei hat nur Männer).
    /// </summary>
    public static FigurProbe? FigurProbeLesen(string? text)
    {
        var teile = (text ?? "").Split(':');
        if (teile.Length != 3 || int.TryParse(teile[0], out _) || !Enum.TryParse<Volk>(teile[0], true, out var volk) || !Enum.IsDefined(volk)) return null;
        bool? frau = teile[1].ToLowerInvariant() switch { "m" => false, "w" => true, _ => null };
        var clip = FigurProbeClips.FirstOrDefault(c => string.Equals(c, teile[2], StringComparison.OrdinalIgnoreCase));
        if (frau is not { } f || clip is null || (f && volk == Volk.Oger)) return null;
        return new FigurProbe(volk, f, clip);
    }
}
