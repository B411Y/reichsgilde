using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// <b>Die Ansagen des Haushofmeisters</b> (Ton Schritt 3): bei wichtigen Ereignissen des <b>eigenen</b> Hauses spricht eine feste Stimme
/// einen kurzen Satz („Mein Herr, ein Glied Eures Hauses ist verschieden.“, bei einer Spielerin „Meine Herrin …“). Der Name steht nie im Satz,
/// er steht in der Textmeldung.
///
/// Ablauf: Ein Ereignis kommt als Textmeldung des Kerns (<see cref="Meldung"/>, erkannt von <see cref="KlangAnsagenErkennung"/>) oder direkt
/// von der <c>Hauptszene</c> (<see cref="Ereignis"/>: Aufstieg im Stand, Wahl, Ratssitzung, Jahresende). Je Ereignis stehen in
/// <c>daten/ansagen.json</c> der Rang (1 = Tod, größere Zahlen unwichtiger), eine Sperre gegen Wiederholung und zwei bis drei Satzvarianten, je in
/// zwei Fassungen (Herr/Herrin; die Fassung folgt dem Geschlecht des Oberhaupts). Die Aufnahmen liegen in
/// <c>assets/audio/stimmen/haushofmeister/&lt;ereignis&gt;_&lt;nr&gt;[_herrin].ogg</c>. Gespielt wird über <see cref="KlangDienst.AnsageSpielen"/>:
/// eine Ansage zur Zeit, 20 s Echtzeit Pause danach, nur die wichtigste wartet, Überholtes verfällt.
///
/// Fehlt die Datei (die Aufnahmen kommen später), bleibt es still; im Entwicklermodus steht eine Zeile im Log. Zum Ausprobieren ersetzt ein
/// kurzer Glockenton von Kenney die fehlende Aufnahme – nur mit der Startoption <c>--ansageplatzhalter=1</c>. <c>--ansagetest=…</c> löst Ansagen aus.
/// </summary>
public static class KlangAnsagen
{
    public const string Pfad = "res://daten/ansagen.json";
    private const string PlatzhalterTon = "res://assets/audio/geraeusche/oberflaeche/impact_bell_heavy_003.ogg";

    private sealed class Datei
    {
        [JsonPropertyName("ordner")] public string Ordner { get; set; } = "";
        [JsonPropertyName("abstand")] public double Abstand { get; set; } = KlangDienst.StandardAnsagenAbstand;
        [JsonPropertyName("verfall")] public double Verfall { get; set; } = KlangDienst.StandardAnsagenVerfall;
        [JsonPropertyName("ereignisse")] public List<EreignisDaten> Ereignisse { get; set; } = new();
    }

    private sealed class EreignisDaten
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("prioritaet")] public int Prioritaet { get; set; } = 5;
        [JsonPropertyName("sperre")] public double Sperre { get; set; } = 60;
        [JsonPropertyName("varianten")] public List<VarianteDaten> Varianten { get; set; } = new();
    }

    private sealed class VarianteDaten
    {
        [JsonPropertyName("datei_herr")] public string DateiHerr { get; set; } = "";
        [JsonPropertyName("datei_herrin")] public string DateiHerrin { get; set; } = "";
    }

    private static Datei? _daten;
    private static readonly Dictionary<string, double> _zuletzt = new();
    private static readonly Dictionary<string, int> _letzteVariante = new();
    private static readonly RandomNumberGenerator _zufall = new();

    /// <summary>Startoption <c>--ansageplatzhalter=1</c>: statt einer fehlenden Aufnahme klingt ein kurzer Glockenton; schaltet auch das Log an.</summary>
    public static bool Platzhalter { get; set; }

    /// <summary>Fragt, ob der Entwicklermodus gilt (Entwicklerbefehle erlaubt); setzt die <c>Hauptszene</c>. Nur dann (oder mit Startoption) schreibt das Log.</summary>
    public static Func<bool>? Entwicklermodus { get; set; }

    private static bool Testmodus;

    private static bool LogAn => Platzhalter || Testmodus || (Entwicklermodus?.Invoke() ?? false);

    private static void Log(string zeile)
    {
        if (LogAn) GD.Print(zeile);
    }

    /// <summary>Alle Kennungen aus der Datei (leer, wenn sie sich nicht laden ließ).</summary>
    public static IReadOnlyList<string> Kennungen => Laden() ? _daten!.Ereignisse.Select(e => e.Id).ToList() : new List<string>();

    private static bool Laden()
    {
        if (_daten is not null) return true;
        try
        {
            _daten = JsonSerializer.Deserialize<Datei>(Godot.FileAccess.GetFileAsString(Pfad));
        }
        catch (Exception fehler)
        {
            GD.PushWarning($"KlangAnsagen: {Pfad} lässt sich nicht lesen: {fehler.Message}");
        }
        if (_daten is null) return false;
        KlangDienst.AnsagenAbstand = _daten.Abstand;
        KlangDienst.AnsagenVerfall = _daten.Verfall;
        KlangDienst.AnsagenLog = Log;
        return true;
    }

    // ── Auslöser ────────────────────────────────────────────────────────────

    /// <summary>Eine Textmeldung des Kerns: sagt der Haushofmeister dazu etwas an, und betrifft sie das eigene Haus? (Aufruf aus <c>Hauptszene</c>.)</summary>
    public static void Meldung(string text, Stadtwirtschaft wirtschaft, Konto spieler)
    {
        if (!KlangDienst.AnsagenAn) return;
        if (KlangAnsagenErkennung.Erkennen(text, wirtschaft, spieler) is { } kennung)
            Ansagen(kennung, KlangAnsagenErkennung.Herrin(wirtschaft, spieler));
    }

    /// <summary>Ein Ereignis des eigenen Hauses, das nicht als Text kommt (Aufstieg, Wahl, Ratssitzung, Jahresende).</summary>
    public static void Ereignis(string kennung, Stadtwirtschaft wirtschaft, Konto spieler)
    {
        if (KlangDienst.AnsagenAn) Ansagen(kennung, KlangAnsagenErkennung.Herrin(wirtschaft, spieler));
    }

    /// <summary>
    /// Sagt das Ereignis an: wählt eine Variante (nie zweimal dieselbe hintereinander), lädt die Aufnahme und gibt sie dem
    /// <see cref="KlangDienst"/>. <paramref name="sperreAufheben"/> gilt nur für den Test.
    /// </summary>
    public static KlangDienst.AnsageErgebnis? Ansagen(string kennung, bool herrin, bool sperreAufheben = false)
    {
        if (!Laden() || !KlangDienst.AnsagenAn) return null;
        var ereignis = _daten!.Ereignisse.FirstOrDefault(e => e.Id == kennung);
        if (ereignis is null || ereignis.Varianten.Count == 0)
        {
            Log($"ANSAGE {kennung}: unbekanntes Ereignis");
            return null;
        }
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (!sperreAufheben && _zuletzt.TryGetValue(kennung, out var davor) && jetzt - davor < ereignis.Sperre)
        {
            Log($"ANSAGE {kennung}: gesperrt (vor {jetzt - davor:0} s schon angesagt, Sperre {ereignis.Sperre:0} s)");
            return null;
        }

        var nummer = _zufall.RandiRange(0, ereignis.Varianten.Count - 1);
        if (ereignis.Varianten.Count > 1 && _letzteVariante.TryGetValue(kennung, out var zuletzt) && nummer == zuletzt)
            nummer = (nummer + 1) % ereignis.Varianten.Count;
        _letzteVariante[kennung] = nummer;

        var variante = ereignis.Varianten[nummer];
        var pfad = _daten.Ordner + (herrin ? variante.DateiHerrin : variante.DateiHerr);
        var ton = ResourceLoader.Exists(pfad) ? ResourceLoader.Load<AudioStream>(pfad) : null;
        var ersatz = false;
        if (ton is null)
        {
            if (!Platzhalter)
            {
                Log($"ANSAGE {kennung}: Datei fehlt, übersprungen ({pfad})");
                return null;
            }
            ton = ResourceLoader.Load<AudioStream>(PlatzhalterTon);
            ersatz = true;
        }

        var ergebnis = KlangDienst.AnsageSpielen(ton, ereignis.Prioritaet, kennung);
        if (ergebnis != KlangDienst.AnsageErgebnis.Verworfen) _zuletzt[kennung] = jetzt;
        var text = ergebnis switch
        {
            KlangDienst.AnsageErgebnis.Gespielt => "gespielt",
            KlangDienst.AnsageErgebnis.Wartet => "wartet (läuft gerade eine Ansage oder die Pause ist nicht um)",
            _ => "verworfen (eine wichtigere wartet oder Ansagen sind aus)",
        };
        Log($"ANSAGE {kennung}: {text} – Rang {ereignis.Prioritaet}, {(herrin ? "Herrin" : "Herr")}, Variante {nummer + 1}{(ersatz ? ", Platzhalterton" : "")}");
        return ergebnis;
    }

    /// <summary>
    /// Wahl im Rat: ist das eigene Haus Kandidat (oder Amtsinhaber), sagt der Haushofmeister Sieg oder Niederlage an. Nur in der laufenden
    /// Sitzung, nicht in der Wiederholung (der Aufrufer prüft das).
    /// </summary>
    public static void Wahlergebnis(Sitzungspunkt punkt, Stadtwirtschaft wirtschaft, Konto spieler)
    {
        if (punkt.Thema != Sitzungsthema.Wahl || punkt.Ergebnis?.Gewaehlt is not { } gewaehlt) return;
        if (!punkt.Kandidaten.Any(k => k.Haus == spieler)) return;
        Ereignis(gewaehlt == spieler.Inhaber ? KlangAnsagenErkennung.Id.WahlGewonnen : KlangAnsagenErkennung.Id.WahlVerloren, wirtschaft, spieler);
    }

    // ── Selbsttest ──────────────────────────────────────────────────────────

    /// <summary>
    /// Startoption <c>--ansagetest=&lt;liste&gt;</c>: löst Ansagen nacheinander aus (alle 0,5 s, ohne Wiederholsperre) und schreibt je Ansage eine Zeile
    /// <c>ANSAGE …</c> ins Log. Die Liste: Kennungen mit Komma (<c>tod_familie,feuer</c>), <c>alle</c> (alle Ereignisse in der Reihenfolge der Datei – zeigt Vorrang
    /// und Pause), <c>dateien</c> (je Ereignis, wie viele Aufnahmen vorhanden sind; löst nichts aus). Das Geschlecht: <c>:herrin</c> hinter der Liste.
    /// </summary>
    public static async void Testen(Node knoten, string auftrag, bool spielerinImSpiel)
    {
        Testmodus = true; // der Test schreibt immer ins Log
        var herrin = spielerinImSpiel;
        if (auftrag.EndsWith(":herrin")) { herrin = true; auftrag = auftrag[..^7]; }
        else if (auftrag.EndsWith(":herr")) { herrin = false; auftrag = auftrag[..^5]; }
        if (!Laden()) { GD.Print("ANSAGETEST: ansagen.json fehlt oder ist fehlerhaft"); return; }

        if (auftrag == "dateien")
        {
            var fehlen = 0;
            foreach (var e in _daten!.Ereignisse)
            {
                var da = e.Varianten.Count(v => ResourceLoader.Exists(_daten.Ordner + v.DateiHerr)) + e.Varianten.Count(v => ResourceLoader.Exists(_daten.Ordner + v.DateiHerrin));
                fehlen += e.Varianten.Count * 2 - da;
                GD.Print($"ANSAGETEST {e.Id}: Rang {e.Prioritaet}, Sperre {e.Sperre:0} s, {da}/{e.Varianten.Count * 2} Aufnahmen");
            }
            GD.Print($"ANSAGETEST fertig: {_daten.Ereignisse.Count} Ereignisse, {fehlen} Aufnahmen fehlen");
            return;
        }

        var kennungen = auftrag == "alle" ? _daten!.Ereignisse.Select(e => e.Id).ToList() : auftrag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        await knoten.ToSignal(knoten.GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        foreach (var kennung in kennungen)
        {
            GD.Print($"ANSAGETEST löst aus: {kennung}");
            Ansagen(kennung, herrin, sperreAufheben: true);
            await knoten.ToSignal(knoten.GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        }
        GD.Print($"ANSAGETEST fertig: {kennungen.Count} Ansagen ausgelöst");
        await knoten.ToSignal(knoten.GetTree().CreateTimer(60.0), SceneTreeTimer.SignalName.Timeout); // die wartende Ansage soll noch ins Log kommen
        Testmodus = false;
    }
}
