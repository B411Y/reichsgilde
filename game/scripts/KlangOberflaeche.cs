using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Klänge der Oberfläche (Bus „Oberflaeche“): Holz, Leder, Papier, Münzen und Glocke statt Elektronik. Jedes <see cref="Ereignis"/>
/// hat zwei bis vier Varianten in einem <see cref="AudioStreamRandomizer"/> (zufällige Wahl, leicht schwankende Tonhöhe und Lautstärke),
/// damit sich nichts abgenutzt anhört. Die Dateien liegen in <c>assets/audio/geraeusche/oberflaeche/</c> (Herkunft: dortige HERKUNFT.md).
///
/// Zentral eingebaut, nicht in jeder Tafel:
///   • <see cref="Einrichten"/> hängt sich an <see cref="SceneTree.NodeAdded"/> und gibt jedem Knopf seinen Klick (Reiter einer Knopfgruppe:
///     Blättern; Knöpfe mit dem Hinweis <c>klang</c> = „still“ spielen selbst (Pause und Tempo, auch per Taste)),
///   • <see cref="Fensterrahmen"/> spielt Öffnen und Schließen jedes Fensters,
///   • <c>Hauptszene.Protokollieren</c> ruft <see cref="Meldung"/>: abgelehnt „✗“ = Holzklopfer, Gefahr „⚠“ = Glocke, Kauf oder Zahlung = Münzen,
///   • Bauen, Siegel (Rat) und Tempo rufen <see cref="Spielen"/> an ihrer Stelle.
/// Eine Sperre je Ereignis verhindert Gepolter, wenn viele Tafeln auf einmal auf- oder zugehen; die ersten Sekunden nach dem Start sind still.
/// Hover bleibt bewusst stumm (die Knöpfe wechseln ohnehin die Farbe).
/// </summary>
public static class KlangOberflaeche
{
    public enum Ereignis { Klick, Reiter, FensterAuf, FensterZu, Muenzen, Fehler, Meldung, Siegel, Bauen, Tempo }

    public const string Ordner = "res://assets/audio/geraeusche/oberflaeche/";
    /// <summary>Sekunden nach dem Start, in denen nichts spielt (Fenster, die beim Aufbau auf- und zugehen).</summary>
    private const double Anlauf = 1.5;
    /// <summary>Mindestabstand in Sekunden zwischen zwei Klängen desselben Ereignisses.</summary>
    private const double Sperre = 0.06;

    /// <summary>Datei (ohne .ogg), Lautstärke in dB; je Ereignis Tonhöhe der Mitte (1 = wie aufgenommen) und Schwankung der Tonhöhe nach oben und unten.</summary>
    private static readonly Dictionary<Ereignis, (string[] Dateien, float Db, float Hoehe, float Streuung)> Tabelle = new()
    {
        [Ereignis.Klick] = (new[] { "handle_small_leather", "handle_small_leather_2", "belt_handle_1" }, -6f, 1.0f, 0.06f),
        [Ereignis.Reiter] = (new[] { "book_flip_1", "book_flip_2", "book_flip_3" }, -8f, 1.0f, 0.05f),
        [Ereignis.FensterAuf] = (new[] { "book_open", "cloth_1", "cloth_2" }, -6f, 1.0f, 0.04f),
        [Ereignis.FensterZu] = (new[] { "book_close", "cloth_3", "cloth_4" }, -7f, 0.95f, 0.04f),
        [Ereignis.Muenzen] = (new[] { "handle_coins", "handle_coins_2" }, -6f, 1.0f, 0.05f),
        [Ereignis.Fehler] = (new[] { "impact_wood_medium_000", "impact_wood_medium_001", "impact_wood_medium_002" }, -4f, 0.85f, 0.03f),
        [Ereignis.Meldung] = (new[] { "impact_bell_heavy_002", "impact_bell_heavy_003", "impact_bell_heavy_004" }, -12f, 1.5f, 0.03f),
        [Ereignis.Siegel] = (new[] { "drop_leather", "book_place_1", "book_place_2" }, -5f, 0.9f, 0.03f),
        [Ereignis.Bauen] = (new[] { "impact_plank_medium_000", "impact_plank_medium_001", "impact_plank_medium_002", "chop" }, -5f, 1.0f, 0.05f),
        [Ereignis.Tempo] = (new[] { "metal_click", "metal_latch" }, -9f, 1.0f, 0.04f),
    };

    /// <summary>Dateien (ohne .ogg, in <see cref="Ordner"/>), Lautstärke in dB und Tonhöhe eines Ereignisses – für den Reiter „Klänge“ des Entwicklerfensters.</summary>
    public static (IReadOnlyList<string> Dateien, float Db, float Hoehe) Angaben(Ereignis ereignis)
    {
        var (dateien, db, hoehe, _) = Tabelle[ereignis];
        return (dateien, db, hoehe);
    }

    private static readonly Dictionary<Ereignis, AudioStreamRandomizer?> _klaenge = new();
    private static readonly Dictionary<Ereignis, double> _zuletzt = new();
    private static double _start = -1;

    /// <summary>Hängt die Knöpfe des ganzen Baums an (läuft einmal, aus dem Autoload <see cref="Fenster"/>).</summary>
    public static void Einrichten(SceneTree baum)
    {
        _start = Time.GetTicksMsec() / 1000.0;
        baum.NodeAdded += knoten =>
        {
            if (knoten is not BaseButton knopf) return;
            knopf.Pressed += () => KnopfGedrueckt(knopf);
        };
    }

    private static void KnopfGedrueckt(BaseButton knopf)
    {
        var art = knopf.HasMeta("klang") ? knopf.GetMeta("klang").AsString() : "";
        if (art == "still") return;
        // Reiter einer Knopfgruppe (Einstellungen, Bericht …) blättern, alles andere klickt
        Spielen(knopf.ButtonGroup is not null ? Ereignis.Reiter : Ereignis.Klick);
    }

    /// <summary>
    /// Spielt das Ereignis einmal. Fehlt eine Datei, steht eine Warnung im Log und das Ereignis bleibt stumm
    /// (<see cref="KlangDienst.OberflaecheSpielen"/> nimmt <c>null</c> hin).
    /// </summary>
    public static void Spielen(Ereignis ereignis)
    {
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (_start < 0 || jetzt - _start < Anlauf) return;
        if (_zuletzt.TryGetValue(ereignis, out var davor) && jetzt - davor < Sperre) return;
        _zuletzt[ereignis] = jetzt;
        KlangDienst.OberflaecheSpielen(Klang(ereignis), Tabelle[ereignis].Db, Tabelle[ereignis].Hoehe);
    }

    /// <summary>Der Randomizer des Ereignisses (beim ersten Gebrauch geladen); <c>null</c>, wenn keine Datei zu laden war.</summary>
    public static AudioStreamRandomizer? Klang(Ereignis ereignis)
    {
        if (_klaenge.TryGetValue(ereignis, out var vorhanden)) return vorhanden;
        var (dateien, _, _, streuung) = Tabelle[ereignis];
        var zufall = new AudioStreamRandomizer
        {
            PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,
            RandomPitch = 1f + streuung,
            RandomVolumeOffsetDb = 1.5f,
        };
        var geladen = 0;
        foreach (var datei in dateien)
        {
            var pfad = Ordner + datei + ".ogg";
            if (ResourceLoader.Load(pfad) is AudioStream ton)
            {
                zufall.AddStream(-1, ton, 1f);
                geladen++;
            }
            else GD.PushWarning($"KlangOberflaeche: {pfad} fehlt oder lässt sich nicht laden.");
        }
        var ergebnis = geladen > 0 ? zufall : null;
        _klaenge[ereignis] = ergebnis;
        return ergebnis;
    }

    // ── Meldungen ───────────────────────────────────────────────────────────

    /// <summary>
    /// Klang zu einer Meldung der Stadt: „✗“ (abgelehnt) = dumpfer Holzklopfer, „⚠“ (Gefahr) = kleine Glocke, ein gelungener Kauf, Verkauf
    /// oder eine Zahlung (Satz mit Betrag in <paramref name="waehrung"/> und Kauf-Verb) = Münzen. Alles andere bleibt still.
    /// </summary>
    public static void Meldung(string meldung, string waehrung)
    {
        if (meldung.StartsWith('✗')) Spielen(Ereignis.Fehler);
        else if (meldung.StartsWith('⚠')) Spielen(Ereignis.Meldung);
        else if (IstZahlung(meldung, waehrung)) Spielen(Ereignis.Muenzen);
    }

    private static readonly string[] ZahlungsWoerter = { "gekauft", "verkauft", "bezahlt", "gezahlt", "erworben", "eingestellt", "Prämie", "ausgebaut", "angeheuert", "zahlt" };

    /// <summary>Steht ein Betrag in der Währung im Satz, zusammen mit einem Kauf- oder Zahlungswort?</summary>
    public static bool IstZahlung(string meldung, string waehrung) =>
        meldung.Contains(waehrung, StringComparison.Ordinal) && ZahlungsWoerter.Any(w => meldung.Contains(w, StringComparison.Ordinal));

    // ── Selbsttest ──────────────────────────────────────────────────────────

    /// <summary>
    /// Startoption <c>--klangtest=1</c>: spielt jedes Ereignis nacheinander und schreibt ins Log, wie viele Varianten geladen sind und wie lang sie
    /// sind; fehlt eine Datei, steht „FEHLT“ da. Ton hört der Klicktest nicht – das Log ist der Nachweis, dass alles lädt und startet.
    /// </summary>
    public static async void Testen(Node knoten)
    {
        _start = Time.GetTicksMsec() / 1000.0 - Anlauf - 1; // die Anlaufzeit gilt im Test nicht
        var fehler = 0;
        foreach (var ereignis in Enum.GetValues<Ereignis>())
        {
            var zufall = Klang(ereignis);
            var soll = Tabelle[ereignis].Dateien.Length;
            var ist = zufall?.StreamsCount ?? 0;
            var laengen = new List<string>();
            for (var i = 0; i < ist; i++) laengen.Add($"{zufall!.GetStream(i).GetLength():0.00}s");
            if (ist != soll) fehler++;
            GD.Print($"KLANGTEST {ereignis}: {ist}/{soll} Varianten ({string.Join(", ", laengen)}){(ist == soll ? "" : " FEHLT")}");
            _zuletzt.Remove(ereignis);
            Spielen(ereignis);
            await knoten.ToSignal(knoten.GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        }
        GD.Print($"KLANGTEST fertig: {Tabelle.Count} Ereignisse, {fehler} mit fehlenden Dateien");
    }
}
