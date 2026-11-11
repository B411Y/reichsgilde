using System.Text.Json;
using System.Text.Json.Serialization;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die <b>Rechnung der dynamischen Musik</b> ohne Godot (damit sie sich in den Tests prüfen lässt; <c>MusikTests</c>): welches Stück zu welcher Lage,
/// wann Pause ist, wie sich Ereignisse vor die Grundmusik schieben. Der Knoten <see cref="Musikspieler"/> führt die Befehle aus (zwei Spieler zum
/// Überblenden, Ducking, Laden der Dateien). Nichts hier berührt den Kern: Zufall nur aus dem Zufall der Oberfläche (eigene <see cref="Random"/>-Quelle
/// des Aufrufers), die Lage kommt aus den Konstanten der <see cref="Spielzeit"/>.
///
/// <para><b>Regeln</b> (Nutzer, 10.11.2026):
/// 1. Tag: im Wechsel die Stücke ohne Jahreszeit und Volk, die der laufenden Jahreszeit und das des Volkes, dem das Oberhaupt des eigenen Hauses angehört.
/// 2. Nacht (ab <see cref="Spielzeit"/>-Tagesphase „Dunkle Nacht“ bis zum Morgen): die Nachtstücke.
/// 3. Nach jedem Stück eine Pause (<see cref="PauseSekunden"/>), nie dasselbe Stück zweimal hintereinander.
/// 4. Wechsel der Lage blendet 4 bis 8 Sekunden über; ein Ereignis blendet die Grundmusik aus und danach wieder ein.
/// 5. Ereignisse haben Vorrang (<see cref="Anlass"/>): Tod, Kampf, Gefahr, Einbruch, Aufstand, Hochzeit, Geburt, Aufstieg, Sklaven (nach dem Rang).</para>
///
/// <para><b>Runde 2</b> (Nutzer, 11.11.2026): Im Tageswechsel (<see cref="TagWaehlen"/>) kommen die Stücke des <b>Volks des Spielers</b> etwa jedes dritte Mal
/// (<see cref="GewichtVolk"/>), die anderer Völker nie; Stücke des <b>Berufs</b> (<see cref="Berufsgruppe"/>, öfter in der eigenen Werkstatt) und selten des
/// <b>Glaubens</b>. Im Kirchenraum und zur Messe spielt das Stück des Kirchenglaubens (<see cref="LageVon"/>). Ahtnisch ist vorbereitet, aber stumm,
/// bis der Kern das Ahtnentum kennt (<see cref="GlaubeName"/>).</para>
/// </summary>
public static class MusikModell
{
    // ── Daten ───────────────────────────────────────────────────────────────

    /// <summary>Ein Stück: Datei unter <c>assets/audio/musik/</c>, Anzeigetitel, Lage (tag, nacht, tod, event, glaube, menue), Zusatz (Jahreszeit oder Ereignis), Volk, Beruf (Handwerk, Dienstleistung) und Glaube.</summary>
    public sealed record Stueck(string Datei, string Titel, string Lage, string? Zusatz, string? Volk, double Dauer, double Lufs = 0, string? Beruf = null, string? Glaube = null);

    private sealed class Datei
    {
        [JsonPropertyName("menue")] public string Menue { get; set; } = "";
        [JsonPropertyName("stuecke")] public List<StueckDaten> Stuecke { get; set; } = new();
    }

    private sealed class StueckDaten
    {
        [JsonPropertyName("datei")] public string Datei { get; set; } = "";
        [JsonPropertyName("titel")] public string Titel { get; set; } = "";
        [JsonPropertyName("lage")] public string Lage { get; set; } = "";
        [JsonPropertyName("zusatz")] public string? Zusatz { get; set; }
        [JsonPropertyName("volk")] public string? Volk { get; set; }
        [JsonPropertyName("dauer")] public double Dauer { get; set; }
        [JsonPropertyName("lufs")] public double Lufs { get; set; }
        [JsonPropertyName("beruf")] public string? Beruf { get; set; }
        [JsonPropertyName("glaube")] public string? Glaube { get; set; }
    }

    /// <summary>Liest <c>daten/musik.json</c>: die Stücke und das Stück des Hauptmenüs (leer, wenn es keines gibt). Kaputter Text ergibt eine leere Liste.</summary>
    public static (List<Stueck> Stuecke, string Menue) Laden(string json)
    {
        try
        {
            var d = JsonSerializer.Deserialize<Datei>(json);
            if (d is null) return (new List<Stueck>(), "");
            return (d.Stuecke.Where(s => s.Datei.Length > 0)
                .Select(s => new Stueck(s.Datei, s.Titel, s.Lage, s.Zusatz, s.Volk, s.Dauer, s.Lufs, s.Beruf, s.Glaube)).ToList(), d.Menue);
        }
        catch (JsonException)
        {
            return (new List<Stueck>(), "");
        }
    }

    // ── Lage ────────────────────────────────────────────────────────────────

    /// <summary>Wann der Morgen beginnt (Minuten nach Mitternacht): der Sonnenaufgang des Lichts in der Stadtansicht (06:00, siehe Stadtansicht.Licht.cs).</summary>
    public const int MorgenMinute = 6 * 60;

    /// <summary>Tag oder Nacht nach der Uhr: Nacht ist die „Dunkle Nacht“ der <see cref="Spielzeit.Tagesphase"/> (ab 20:00) und der Morgen vor dem Sonnenaufgang.</summary>
    public static bool IstNacht(double uhrMinute)
    {
        var minute = (int)Math.Floor(uhrMinute);
        var phase = new Spielzeit(Math.Clamp(minute - Spielzeit.TagesbeginnMinute, 0, Spielzeit.MinutenProTag - 1)).Tagesphase;
        return phase == Tagesphase.DunkleNacht || minute < MorgenMinute;
    }

    /// <summary>Jahreszeit als Name im Datenfeld <c>zusatz</c> (umschrieben).</summary>
    public static string ZusatzVon(Jahreszeit j) => j switch
    {
        Jahreszeit.Fruehling => "fruehling",
        Jahreszeit.Sommer => "sommer",
        Jahreszeit.Herbst => "herbst",
        _ => "winter",
    };

    /// <summary>Volk als Name im Datenfeld <c>volk</c> (Orks und Oger haben kein eigenes Stück).</summary>
    public static string? VolkName(Volk? v) => v switch
    {
        Volk.Mensch => "Menschen",
        Volk.Zwerg => "Zwerge",
        Volk.Elb => "Elben",
        Volk.Halbling => "Halblinge",
        _ => null,
    };

    // ── Lautstärke der Ereignisse ───────────────────────────────────────────

    /// <summary>
    /// Ereignisstücke (Lage <c>event</c>: Einbruch, Geburt, Gefahr, Kampf, Aufstieg, Hochzeit, Aufstand, Sklaven) spielen um so viele dB lauter (Nutzer, 11.11.2026);
    /// Tod und alle anderen Stücke nicht. Das ist eine <b>Wiedergabe-Verstärkung</b>, die Dateien bleiben unverändert (−17 LUFS, True-Peak höchstens −3,3 dBTP).
    /// Sie kommt zum Pegel der Partie (−9 dB) hinzu, der Regler „Musik“ wirkt weiter auf alles.
    /// </summary>
    public const double EreignisDb = 3;

    /// <summary>Verstärkung (dB) beim Abspielen eines Stücks: <see cref="EreignisDb"/> für Ereignisstücke, sonst 0.</summary>
    public static double LautstaerkeDb(Stueck s) => s.Lage == "event" ? EreignisDb : 0;

    /// <summary>So viel (dB, negativ) senkt sich die Stadt-Atmosphäre, solange ein Ereignisstück läuft; Abfall in <see cref="AtmoAb"/>, Rückkehr in <see cref="AtmoAuf"/> Sekunden.</summary>
    public const double AtmoDuckDb = -4, AtmoAb = 0.6, AtmoAuf = 2.5;

    // ── Volk, Beruf, Glaube, Ort ────────────────────────────────────────────

    /// <summary>Zu welcher Gruppe die Berufe des Spielers zählen (mehrere gleichzeitig, wenn er Berufe beider Gruppen beherrscht).</summary>
    [Flags]
    public enum Berufsgruppe { Keine = 0, Handwerk = 1, Dienstleistung = 2 }

    /// <summary>Handwerk: Schmied, Tischler, Steinmetz, Alchimist, Parfumeur. Dienstleistung: Wirt, Prediger, Geldleiher, Gardist, Dieb (Nutzer, 11.11.2026).</summary>
    public static Berufsgruppe GruppeVon(string? beruf) => beruf switch
    {
        "Schmied" or "Tischler" or "Steinmetz" or "Alchimist" or "Parfumeur" => Berufsgruppe.Handwerk,
        "Wirt" or "Prediger" or "Geldleiher" or "Gardist" or "Dieb" => Berufsgruppe.Dienstleistung,
        _ => Berufsgruppe.Keine,
    };

    public static Berufsgruppe GruppeVon(IEnumerable<string> berufe) => berufe.Aggregate(Berufsgruppe.Keine, (g, b) => g | GruppeVon(b));

    /// <summary>Name der Berufsgruppe im Datenfeld <c>beruf</c>.</summary>
    public static string? BerufName(Berufsgruppe g) => g switch { Berufsgruppe.Handwerk => "Handwerk", Berufsgruppe.Dienstleistung => "Dienstleistung", _ => null };

    private static Berufsgruppe GruppeNamens(string name) => name == "Handwerk" ? Berufsgruppe.Handwerk : name == "Dienstleistung" ? Berufsgruppe.Dienstleistung : Berufsgruppe.Keine;

    /// <summary>
    /// Glaube als Name im Datenfeld <c>glaube</c>. <b>Ahtnisch ist vorbereitet, aber stumm</b>: Der Kern kennt nur Katholisch, Evangelisch und den Naturglauben der
    /// Orks und Oger (der hier keinem Stück entspricht); sobald es das Ahtnentum im Kern gibt, hier den Fall <c>"Ahtnisch"</c> zurückgeben – die Stücke liegen schon da.
    /// </summary>
    public static string? GlaubeName(Konfession? k) => k switch
    {
        Konfession.Katholisch => "Katholisch",
        Konfession.Evangelisch => "Evangelisch",
        _ => null,
    };

    /// <summary>Wo sich die Kamera befindet: draußen, in einer eigenen Werkstatt (Innenansicht eines Betriebs des Spielers) oder im Kirchenraum (Kirche, Dom).</summary>
    public enum Ort { Draussen, EigeneWerkstatt, Kirche }

    /// <summary>
    /// Läuft gerade die Messe? Dieselbe Spanne wie im Kern (<c>Kirchenwesen.IstMessezeit</c>, dort nicht öffentlich): eine halbe Stunde vor der Messestunde
    /// <paramref name="messeUm"/> bis eine Stunde danach. Ohne Kirchen (<paramref name="messeUm"/> kleiner 0) nie.
    /// </summary>
    public static bool IstMesse(double uhrMinute, int messeUm) => messeUm >= 0 && uhrMinute >= messeUm * 60 - 30 && uhrMinute < messeUm * 60 + 60;

    // ── Auswahl ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Anteile im Tageswechsel: Volksstücke des Spielers (etwa jedes dritte Stück), Berufsstücke (im Tageswechsel und, öfter, in der eigenen Werkstatt),
    /// Stücke des eigenen Glaubens (selten), Jahreszeit. Den Rest teilen die neutralen Stücke.
    /// </summary>
    public const double GewichtVolk = 1.0 / 3, GewichtBeruf = 0.15, GewichtBerufWerkstatt = 0.45, GewichtGlaube = 0.06, GewichtJahreszeit = 0.2;

    /// <summary>Stücke ohne jede Besonderheit: Tag, ohne Jahreszeit, Volk, Beruf, Glaube.</summary>
    private static bool IstNeutral(Stueck s) => s.Lage == "tag" && s.Zusatz is null && s.Volk is null && s.Beruf is null && s.Glaube is null;

    /// <summary>
    /// Tagesstücke, die in Frage kommen: die neutralen, die der <paramref name="jahreszeit"/>, das Volk des Spielers (andere Völker nie), die Berufsgruppe
    /// <paramref name="beruf"/> (andere Gruppe nie) und die Stücke des <paramref name="glaube"/>ns (Lage glaube; ohne Glaube oder ohne Stück keines).
    /// </summary>
    public static List<Stueck> TagKandidaten(IEnumerable<Stueck> alle, Jahreszeit jahreszeit, Volk? volk, Berufsgruppe beruf = Berufsgruppe.Keine, string? glaube = null)
    {
        var zeit = ZusatzVon(jahreszeit);
        var name = VolkName(volk);
        return alle.Where(s => IstNeutral(s)
            || (s.Lage == "tag" && (s.Zusatz == zeit || (s.Volk is not null && s.Volk == name) || (s.Beruf is not null && (beruf & GruppeNamens(s.Beruf)) != 0)))
            || (s.Lage == "glaube" && glaube is not null && s.Glaube == glaube)).ToList();
    }

    public static List<Stueck> NachtKandidaten(IEnumerable<Stueck> alle) => alle.Where(s => s.Lage == "nacht").ToList();

    /// <summary>Stücke des Glaubens (Lage glaube, z. B. Katholisch); ohne <paramref name="glaube"/> keine.</summary>
    public static List<Stueck> GlaubensKandidaten(IEnumerable<Stueck> alle, string? glaube) =>
        glaube is null ? new List<Stueck>() : alle.Where(s => s.Lage == "glaube" && s.Glaube == glaube).ToList();

    /// <summary>
    /// Welche Grundlage spielt gerade: <c>"kirche"</c> (Kirchenraum, oder Messe; nur wenn es ein Stück dieses Glaubens gibt), sonst <c>"nacht"</c> oder <c>"tag"</c>.
    /// Im Kirchenraum gilt der Glaube der Kirche (<see cref="Eingabe.KirchenGlaube"/>, ersatzweise der des Spielers), zur Messe der Glaube des Spielers.
    /// </summary>
    public static string LageVon(IReadOnlyList<Stueck> alle, Eingabe e)
    {
        if (e.Ort == Ort.Kirche && GlaubensKandidaten(alle, e.KirchenGlaube ?? e.Glaube).Count > 0) return "kirche";
        if (IstNacht(e.UhrMinute)) return "nacht";
        if (e.Messe && GlaubensKandidaten(alle, e.Glaube).Count > 0) return "kirche";
        return "tag";
    }

    /// <summary>
    /// Die Rechnung des Tageswechsels (reine Funktion, mit festem Zufall prüfbar): erst die Gruppe nach Gewicht (Volk des Spielers ein Drittel, Beruf, Glaube,
    /// Jahreszeit, den Rest die neutralen Stücke; leere Gruppen fallen weg), dann ein Stück der Gruppe ohne das zuletzt gespielte (<see cref="Waehlen"/>).
    /// Ohne Stück null.
    /// </summary>
    public static Stueck? TagWaehlen(IReadOnlyList<Stueck> alle, Eingabe e, IReadOnlyList<string> zuletzt, Func<double> zufall)
    {
        var zeit = ZusatzVon(e.Jahreszeit);
        var volkName = VolkName(e.Volk);
        var letztes = zuletzt.Count > 0 ? zuletzt[^1] : null;
        List<Stueck> Frei(Func<Stueck, bool> f)
        {
            var ohne = alle.Where(f).Where(s => s.Datei != letztes).ToList();
            return ohne;
        }
        var gruppen = new List<(double Gewicht, List<Stueck> Stuecke)>
        {
            (GewichtVolk, Frei(s => s.Lage == "tag" && s.Volk is not null && s.Volk == volkName)),
            (e.Ort == Ort.EigeneWerkstatt ? GewichtBerufWerkstatt : GewichtBeruf, Frei(s => s.Lage == "tag" && s.Beruf is not null && (e.Beruf & GruppeNamens(s.Beruf)) != 0)),
            (GewichtGlaube, Frei(s => s.Lage == "glaube" && e.Glaube is not null && s.Glaube == e.Glaube)),
            (GewichtJahreszeit, Frei(s => s.Lage == "tag" && s.Zusatz == zeit)),
        };
        var neutral = Frei(IstNeutral);
        var rest = Math.Max(1 - gruppen.Where(g => g.Stuecke.Count > 0).Sum(g => g.Gewicht), 0.05);
        if (neutral.Count > 0) gruppen.Add((rest, neutral));
        gruppen = gruppen.Where(g => g.Stuecke.Count > 0).ToList();
        if (gruppen.Count == 0) return Waehlen(TagKandidaten(alle, e.Jahreszeit, e.Volk, e.Beruf, e.Glaube), zuletzt, zufall);
        var wurf = zufall() * gruppen.Sum(g => g.Gewicht);
        foreach (var (gewicht, stuecke) in gruppen)
        {
            if (wurf < gewicht) return Waehlen(stuecke, zuletzt, zufall);
            wurf -= gewicht;
        }
        return Waehlen(gruppen[^1].Stuecke, zuletzt, zufall);
    }

    /// <summary>Das Grundstück zur Grundlage <paramref name="lage"/> (<see cref="LageVon"/>).</summary>
    public static Stueck? GrundWaehlen(IReadOnlyList<Stueck> alle, string lage, Eingabe e, IReadOnlyList<string> zuletzt, Func<double> zufall) => lage switch
    {
        "nacht" => Waehlen(NachtKandidaten(alle), zuletzt, zufall),
        "kirche" => Waehlen(GlaubensKandidaten(alle, e.Ort == Ort.Kirche ? e.KirchenGlaube ?? e.Glaube : e.Glaube), zuletzt, zufall),
        _ => TagWaehlen(alle, e, zuletzt, zufall),
    };

    /// <summary>Stücke eines Anlasses: Tod aus <c>tod</c>, die anderen aus <c>event</c> mit ihrem Zusatz.</summary>
    public static List<Stueck> AnlassKandidaten(IEnumerable<Stueck> alle, Anlass a) => a == Anlass.Tod
        ? alle.Where(s => s.Lage == "tod").ToList()
        : alle.Where(s => s.Lage == "event" && s.Zusatz == AnlassName(a)).ToList();

    /// <summary>
    /// Wählt zufällig ein Stück: nicht das zuletzt gespielte, und wenn genug da sind, auch nicht eines der <paramref name="zuletzt"/> (neueste zuletzt).
    /// <paramref name="zufall"/> liefert Zahlen in [0, 1). Gibt es kein Stück, null.
    /// </summary>
    public static Stueck? Waehlen(IReadOnlyList<Stueck> kandidaten, IReadOnlyList<string> zuletzt, Func<double> zufall)
    {
        if (kandidaten.Count == 0) return null;
        var frei = kandidaten.Where(s => !zuletzt.TakeLast(Math.Min(2, Math.Max(kandidaten.Count - 1, 1))).Contains(s.Datei)).ToList();
        if (frei.Count == 0) frei = kandidaten.Where(s => zuletzt.Count == 0 || s.Datei != zuletzt[^1]).ToList();
        if (frei.Count == 0) frei = kandidaten.ToList();
        return frei[Math.Min((int)(zufall() * frei.Count), frei.Count - 1)];
    }

    // ── Häufigkeit und Pausen ───────────────────────────────────────────────

    /// <summary>Wie oft Musik spielt (Einstellungen, Reiter Ton).</summary>
    public enum Haeufigkeit { Immer, Oft, Selten, NurEreignisse, Aus }

    public static string Name(Haeufigkeit h) => h switch
    {
        Haeufigkeit.Immer => "Immer",
        Haeufigkeit.Oft => "Oft",
        Haeufigkeit.Selten => "Selten",
        Haeufigkeit.NurEreignisse => "Nur Ereignisse",
        _ => "Aus",
    };

    /// <summary>
    /// Pause nach einem Stück in <b>Echtzeit-Sekunden</b>. Der Auftrag nennt „1 bis 3 Spielminuten“ für die kurze Stufe: Eine Spielminute dauert bei Tempo 1
    /// nur gut 0,8 s, das wären 1 bis 3 Sekunden und kaum eine Pause; hier gelten 1 bis 3 <b>Minuten</b> Echtzeit (Tempo ändert sie nicht, Musik läuft in
    /// Echtzeit). Wer es anders will, ändert nur diese Tabelle.
    /// </summary>
    public static (double Min, double Max) PauseGrenzen(Haeufigkeit h) => h switch
    {
        Haeufigkeit.Immer => (60, 180),     // 1–3 min
        Haeufigkeit.Oft => (180, 360),      // 3–6 min
        Haeufigkeit.Selten => (480, 900),   // 8–15 min
        _ => (60, 180),
    };

    public static double PauseSekunden(Haeufigkeit h, double zufall)
    {
        var (min, max) = PauseGrenzen(h);
        return min + (max - min) * Math.Clamp(zufall, 0, 1);
    }

    /// <summary>Dauer der Überblendung bei einem Wechsel der Lage: 4 bis 8 Sekunden.</summary>
    public static double WechselBlende(double zufall) => 4 + 4 * Math.Clamp(zufall, 0, 1);

    // ── Anlässe ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Ereignisse, die sich vor die Grundmusik schieben. Der Rang (kleiner = wichtiger) entscheidet, welches ein anderes unterbricht. Tod, Kampf, Gefahr (Brand,
    /// Überfall), Einbruch, Hochzeit, Geburt und Aufstieg (Stand, Meisterbrief, Amt gewonnen) sind <b>Augenblicke</b> (das Stück läuft dann zu Ende), Sklaven und
    /// Aufstand <b>Zustände</b> (das Stück endet, wenn der Zustand vorbei ist). Kampf kommt aus den Vorfällen des Kerns (<see cref="AnlassAusVorfall"/>).
    /// </summary>
    public enum Anlass { Keiner, Tod, Kampf, Gefahr, Einbruch, Aufstand, Hochzeit, Geburt, Aufstieg, Sklaven }

    public static int Rang(Anlass a) => a switch { Anlass.Tod => 1, Anlass.Kampf => 2, Anlass.Gefahr => 3, Anlass.Einbruch => 4, Anlass.Aufstand => 5, Anlass.Hochzeit => 6, Anlass.Geburt => 7, Anlass.Aufstieg => 8, Anlass.Sklaven => 9, _ => 99 };

    public static bool IstZustand(Anlass a) => a is Anlass.Sklaven or Anlass.Aufstand;

    public static string AnlassName(Anlass a) => a switch
    {
        Anlass.Tod => "tod", Anlass.Kampf => "kampf", Anlass.Gefahr => "gefahr", Anlass.Einbruch => "dieb", Anlass.Aufstand => "aufstand", Anlass.Hochzeit => "hochzeit",
        Anlass.Geburt => "geburt", Anlass.Aufstieg => "levelup", Anlass.Sklaven => "sklaven", _ => "",
    };

    /// <summary>
    /// Welcher Anlass gehört zu einer Meldung oder einem Ereignis des eigenen Hauses (<see cref="KlangAnsagenErkennung.Erkennen"/> hat Art und Haus schon geprüft)?
    /// Tod im Haus, Heirat, Geburt (Kind im Haus), Einbruch (Diebstahl, der gelingt), Brand und Überfall, die gelingen, ergeben Gefahr; Aufstieg im Stand,
    /// Meisterbrief und gewonnene Wahl (Amt) ergeben Aufstieg. „Gefahrenmeldung der Wache“ meldet der Kern noch nicht (offen, siehe Bereichsdatei).
    /// </summary>
    public static Anlass AnlassAusAnsage(string? ansageKennung) => ansageKennung switch
    {
        KlangAnsagenErkennung.Id.TodOberhaupt or KlangAnsagenErkennung.Id.TodFamilie => Anlass.Tod,
        KlangAnsagenErkennung.Id.Heirat => Anlass.Hochzeit,
        KlangAnsagenErkennung.Id.Geburt => Anlass.Geburt,
        KlangAnsagenErkennung.Id.Diebstahl => Anlass.Einbruch,
        KlangAnsagenErkennung.Id.Ueberfall or KlangAnsagenErkennung.Id.Feuer => Anlass.Gefahr,
        KlangAnsagenErkennung.Id.StandAufstieg or KlangAnsagenErkennung.Id.Meisterpruefung or KlangAnsagenErkennung.Id.WahlGewonnen => Anlass.Aufstieg,
        _ => Anlass.Keiner,
    };

    /// <summary>
    /// <b>Kampf</b> (Nutzer, 11.11.2026), aus den Vorfällen des Kerns (<c>Vorfallbuch.Neu</c>, nur lesend), nur wenn das eigene Haus beteiligt ist:
    /// ein <b>Überfall auf einen eigenen Karren</b>, den die Eskorte oder das Geleit der Wache abwehrt (Erfolg = false) oder bei dem eine Wache Geleit stellte;
    /// ein <b>Einbruch</b> (Lager oder Wohnhaus), den ein Wächter vereitelt (Waechter und kein Erfolg); ein <b>Dieb, den die Stadtwache stellt</b>
    /// (Festnahme am Markt; der Täter gehört zum eigenen Haus, oder die eigene Wache fasste ihn). Ein gelungener Überfall oder Einbruch ist kein Kampf
    /// (Gefahr bzw. Einbruch, aus der Meldung, <see cref="AnlassAusAnsage"/>).
    /// </summary>
    public static Anlass AnlassAusVorfall(Vorfall v, Konto spieler)
    {
        switch (v.Art)
        {
            case Vorfallart.Ueberfall when v.Opfer == spieler && (!v.Erfolg || v.Wache is not null):
            case Vorfallart.Einbruch or Vorfallart.Hauseinbruch when v.Opfer == spieler && v.Waechter && !v.Erfolg:
            case Vorfallart.Taschendiebstahl when v.IstFestnahme && (v.Taeterhaus == spieler || v.Wache == spieler):
                return Anlass.Kampf;
            default:
                return Anlass.Keiner;
        }
    }

    // ── Die Steuerung ───────────────────────────────────────────────────────

    /// <summary>Was die Steuerung dem Spieler befiehlt.</summary>
    public enum BefehlArt { Starten, Ausblenden }

    /// <summary>
    /// <see cref="BefehlArt.Starten"/>: <see cref="Stueck"/> einblenden über <see cref="Einblende"/> s, alles andere über <see cref="AltAus"/> s ausblenden.
    /// <see cref="BefehlArt.Ausblenden"/>: alles über <see cref="AltAus"/> s ausblenden.
    /// </summary>
    public readonly record struct Befehl(BefehlArt Art, Stueck? Stueck, double Einblende, double AltAus, Anlass Anlass = Anlass.Keiner, bool Wechsel = false);

    /// <summary>
    /// Was die Steuerung je Schritt wissen muss: Uhr, Jahreszeit, Volk des Spielers, Häufigkeit, Zustand (Sklaven, Aufstand), Berufsgruppe(n) des Spielers,
    /// sein Glaube (<see cref="GlaubeName"/>), Ort der Kamera, Glaube der Kirche, in der die Kamera ist, und ob Messe ist.
    /// </summary>
    public readonly record struct Eingabe(double UhrMinute, Jahreszeit Jahreszeit, Volk? Volk, Haeufigkeit Haeufigkeit, Anlass Zustand = Anlass.Keiner,
        Berufsgruppe Beruf = Berufsgruppe.Keine, string? Glaube = null, Ort Ort = Ort.Draussen, string? KirchenGlaube = null, bool Messe = false);

    /// <summary>
    /// Der Ablauf der Musik als Zustandsmaschine: <see cref="Schritt"/> bekommt die vergangene Echtzeit und die Eingaben und gibt Befehle zurück.
    /// Die Uhr liefert der Aufrufer (<c>dt</c>); nichts hier hängt an Godot.
    /// </summary>
    public sealed class Steuerung
    {
        public const double AnlassGueltig = 10;       // so lange wartet ein gemeldeter Augenblick auf seinen Platz
        public const double AnlassSperre = 120;       // Wiederholsperre nach dem Ende des Stücks, Echtzeit-Sekunden
        public const double ZustandMindest = 20;      // ein Zustandsstück läuft mindestens so lange
        public const double AnlassEinblende = 2.5, AnlassAusblende = 3;
        public const double GrundEinblende = 4, EreignisEnde = 2, AnfangsPause = 4;
        /// <summary>Betritt der Spieler eine Kirche, endet die Pause nach höchstens so vielen Sekunden.</summary>
        public const double KirchePause = 6;

        private readonly IReadOnlyList<Stueck> _alle;
        private readonly Func<double> _zufall;
        private readonly List<string> _zuletzt = new();
        private double _jetzt, _pause = AnfangsPause, _laufZeit;
        private bool _inKirche;
        private readonly Dictionary<Anlass, double> _gemeldetBis = new(), _gesperrtBis = new();

        public Steuerung(IReadOnlyList<Stueck> stuecke, Func<double> zufall)
        {
            _alle = stuecke;
            _zufall = zufall;
        }

        /// <summary>Das gerade laufende Stück (null in der Pause).</summary>
        public Stueck? Laufend { get; private set; }
        /// <summary>Welcher Anlass spielt gerade (<see cref="Anlass.Keiner"/>: Grundmusik oder nichts).</summary>
        public Anlass LaufenderAnlass { get; private set; }
        /// <summary>Die Grundlage, für die das laufende Grundstück gewählt wurde: <c>tag</c>, <c>nacht</c> oder <c>kirche</c> (<see cref="LageVon"/>); sonst null.</summary>
        public string? LaufendeLage { get; private set; }
        /// <summary>Echtzeit-Sekunden bis zum nächsten Stück (nur in der Pause sinnvoll).</summary>
        public double PauseRest => Laufend is null ? Math.Max(_pause, 0) : 0;
        /// <summary>Wie viele Sekunden das laufende Stück schon läuft.</summary>
        public double LaufZeit => _laufZeit;

        /// <summary>Ein Augenblick geschah (Tod, Hochzeit, Kampf): er wartet <see cref="AnlassGueltig"/> Sekunden auf seinen Platz.</summary>
        public void Melden(Anlass a)
        {
            if (a != Anlass.Keiner) _gemeldetBis[a] = _jetzt + AnlassGueltig;
        }

        /// <summary>Zum Ausprobieren: das nächste Grundstück sofort (ohne Pause).</summary>
        public void PauseBeenden() => _pause = 0;

        /// <summary>Die Pause neu würfeln (nach dem Stück, oder wenn die Häufigkeit gewechselt hat: nicht länger als ihr Höchstwert).</summary>
        private void NeuePause(Haeufigkeit h) => _pause = PauseSekunden(h, _zufall());

        public List<Befehl> Schritt(double dt, Eingabe e, bool stueckZuEnde)
        {
            var aus = new List<Befehl>();
            _jetzt += dt;
            if (Laufend is not null) _laufZeit += dt;

            if (stueckZuEnde && Laufend is not null)
            {
                var anlass = LaufenderAnlass;
                if (anlass != Anlass.Keiner) _gesperrtBis[anlass] = _jetzt + AnlassSperre;
                Laufend = null;
                LaufenderAnlass = Anlass.Keiner;
                LaufendeLage = null;
                if (anlass != Anlass.Keiner) _pause = EreignisEnde;
                else NeuePause(e.Haeufigkeit);
            }

            if (e.Haeufigkeit == Haeufigkeit.Aus)
            {
                if (Laufend is not null) Beenden(aus, 3);
                _pause = Math.Min(_pause, 0.5);
                return aus;
            }

            // Ein Zustand, der vorbei ist, beendet sein Stück (nach der Mindestzeit)
            if (Laufend is not null && IstZustand(LaufenderAnlass) && e.Zustand != LaufenderAnlass && _laufZeit >= ZustandMindest)
            {
                _gesperrtBis[LaufenderAnlass] = _jetzt + AnlassSperre;
                Beenden(aus, 6);
                _pause = EreignisEnde;
            }

            // Ereignisse
            var wunsch = Anlass.Keiner;
            foreach (var (a, bis) in _gemeldetBis.ToList())
            {
                if (bis < _jetzt) { _gemeldetBis.Remove(a); continue; }
                if (Rang(a) < Rang(wunsch)) wunsch = a;
            }
            if (e.Zustand != Anlass.Keiner && Rang(e.Zustand) < Rang(wunsch)) wunsch = e.Zustand;
            if (wunsch != Anlass.Keiner && wunsch != LaufenderAnlass && (Laufend is null || LaufenderAnlass == Anlass.Keiner || Rang(wunsch) < Rang(LaufenderAnlass))
                && _gesperrtBis.GetValueOrDefault(wunsch) <= _jetzt)
            {
                var stueck = Waehlen(AnlassKandidaten(_alle, wunsch), _zuletzt, _zufall);
                if (stueck is not null)
                {
                    _gemeldetBis.Remove(wunsch);
                    Starten(aus, stueck, AnlassEinblende, AnlassAusblende, wunsch, null, false);
                    return aus;
                }
                _gemeldetBis.Remove(wunsch); // es gibt kein Stück dazu: vergessen
            }

            // Grundmusik (nicht während eines Ereignisses; bei „nur Ereignisse“ gar nicht)
            if (LaufenderAnlass != Anlass.Keiner) return aus;
            if (e.Haeufigkeit == Haeufigkeit.NurEreignisse)
            {
                if (Laufend is not null) Beenden(aus, 4);
                return aus;
            }
            var lage = LageVon(_alle, e);
            var inKirche = lage == "kirche" && e.Ort == Ort.Kirche;
            var kircheBetreten = inKirche && !_inKirche;
            _inKirche = inKirche;
            if (Laufend is not null)
            {
                if (LaufendeLage is { } alt && alt != lage)
                {
                    var neu = GrundWaehlen(_alle, lage, e, _zuletzt, _zufall);
                    if (neu is not null)
                    {
                        var blende = WechselBlende(_zufall());
                        Starten(aus, neu, blende, blende, Anlass.Keiner, lage, true);
                    }
                    else LaufendeLage = lage; // keine Stücke der neuen Lage: das laufende zu Ende spielen
                }
                return aus;
            }
            // Pause: Häufigkeit hat sich geändert, die Pause nicht länger als der Höchstwert
            _pause = Math.Min(_pause, PauseGrenzen(e.Haeufigkeit).Max);
            // Wer die Kirche betritt, wartet nicht die ganze Pause ab (der Kirchenraum soll nach seinem Glauben klingen)
            if (kircheBetreten) _pause = Math.Min(_pause, KirchePause);
            _pause -= dt;
            if (_pause > 0) return aus;
            var stk = GrundWaehlen(_alle, lage, e, _zuletzt, _zufall);
            if (stk is null) { _pause = 30; return aus; }
            Starten(aus, stk, GrundEinblende, 0, Anlass.Keiner, lage, false);
            return aus;
        }

        private void Starten(List<Befehl> aus, Stueck stueck, double ein, double altAus, Anlass anlass, string? lage, bool wechsel)
        {
            Laufend = stueck;
            LaufenderAnlass = anlass;
            LaufendeLage = lage;
            _laufZeit = 0;
            _zuletzt.Add(stueck.Datei);
            if (_zuletzt.Count > 6) _zuletzt.RemoveAt(0);
            aus.Add(new Befehl(BefehlArt.Starten, stueck, ein, altAus, anlass, wechsel));
        }

        private void Beenden(List<Befehl> aus, double sekunden)
        {
            Laufend = null;
            LaufenderAnlass = Anlass.Keiner;
            LaufendeLage = null;
            aus.Add(new Befehl(BefehlArt.Ausblenden, null, 0, sekunden));
        }
    }
}
