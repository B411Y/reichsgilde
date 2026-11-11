using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;
using M = Reichsgilde.MusikModell;

namespace Reichsgilde;

/// <summary>
/// Die <b>dynamische Musik</b> (Bus „Musik“): ein Knoten unter dem <see cref="KlangDienst"/> (läuft über Szenenwechsel hinweg, darum gibt es keinen Bruch
/// zwischen Hauptmenü, Ladebildschirm und Partie). Was gespielt wird, rechnet <see cref="MusikModell"/> (Lage Tag oder Nacht, Jahreszeit, Volk des
/// eigenen Hauses, Pausen, Ereignisse); dieser Knoten führt die Befehle aus:
///   • zwei bis drei Spieler zum Überblenden (gleiche Leistung: sin-Kurve), ein Stück am Stück, dazwischen die Pause;
///   • <b>Ducking</b>: Spricht der Haushofmeister (<see cref="KlangDienst.AnsageSpielt"/>), geht die Musik in 0,35 s um 9 dB herunter und danach in 1,5 s zurück;
///   • fehlende Dateien lösen keinen Absturz aus: das Stück zählt als gespielt (Pause), eine Warnung steht einmal im Log;
///   • im Hauptmenü ein ruhiges Stück (<c>menue</c> in <c>daten/musik.json</c>), nach seinem Ende 25 s Pause.
/// Die Partie hängt sich über <see cref="PartieAnschliessen"/> an (Eingaben je Bild: Uhr, Jahreszeit, Volk, Zustände); Ereignisse kommen über
/// <see cref="Melden"/>. Eigener Zufall (<see cref="Random"/>), nie der der Simulation. Einstellungen: Häufigkeit in <see cref="KlangDienst"/>
/// (Reiter Ton), Lautstärke über den Bus Musik.
/// Startoption <c>--musiktest=&lt;lage&gt;</c> (tag, nacht, tod, hochzeit, kampf, gefahr, einbruch, geburt, aufstieg, sklaven, aufstand, messe, kirche[:katholisch|evangelisch|ahtnisch],
/// werkstatt[:handwerk|dienstleistung], menue; <c>selbst</c> prüft nur die Rechnung): spielt sofort
/// ein Stück dieser Lage und schreibt „MUSIK …“-Zeilen ins Log.
/// </summary>
public partial class Musikspieler : Node
{
    public const string Ordner = "res://assets/audio/musik/";
    public const string Pfad = "res://daten/musik.json";
    /// <summary>Pegel der Musik gegenüber dem Dateipegel (−17 LUFS): in der Partie unter dem Stadtklang und den Ansagen, im Hauptmenü etwas lauter.</summary>
    public const float PartieDb = -9f, MenueDb = -4f;
    /// <summary>So tief geht die Musik, solange eine Ansage läuft (dB), Anstieg (Sekunden zum Ziel) beim Absenken und Zurückkehren.</summary>
    public const float DuckDb = -9f, DuckAbSekunden = 0.35f, DuckAufSekunden = 1.5f;
    private const double MenuePause = 25;

    public static Musikspieler? Aktiv { get; private set; }

    /// <summary>
    /// Wie weit die Musik die Stadt-Atmosphäre gerade absenkt (dB, 0 oder negativ): während eines Ereignisstücks <see cref="M.AtmoDuckDb"/>, weich hin und zurück.
    /// <see cref="KlangUmgebung"/> zieht den Wert von den Bussen Draußen, Stadt und Innen ab; es gab dort noch kein Ducking (auch kein Glocken-Ducking), dies ist der gemeinsame Weg.
    /// </summary>
    public static float AtmoDuck { get; private set; }

    /// <summary>
    /// Die Eingaben der Partie (null: keine laufende Partie): Uhr in Minuten, Jahreszeit, Volk des Oberhaupts, Zustand (Sklaven, Aufstand), Berufsgruppen des Hauses,
    /// Glaube des Oberhaupts (Name im Datenfeld <c>glaube</c> oder null), Ort der Kamera, Glaube der Kirche, in der sie ist, und ob Messe ist.
    /// </summary>
    public sealed record PartieEingabe(double UhrMinute, Jahreszeit Jahreszeit, Volk? Volk, M.Anlass Zustand,
        M.Berufsgruppe Beruf = M.Berufsgruppe.Keine, string? Glaube = null, M.Ort Ort = M.Ort.Draussen, string? KirchenGlaube = null, bool Messe = false);

    private Func<PartieEingabe?>? _partie;
    private bool _menue;
    private List<M.Stueck> _stuecke = new();
    private string _menueDatei = "";
    private M.Steuerung? _steuerung;
    private readonly Random _zufall = new();
    private readonly List<Spur> _spuren = new();
    private readonly HashSet<string> _gewarnt = new();
    private float _duck, _duckZiel;
    private double _menuePause = 1;
    private string? _menueLaeuft;
    private double _spurAlter;
    private bool _geladen;
    private string? _test;
    private double? _testUhr;
    private M.Anlass _testZustand;
    private M.Ort? _testOrt;
    private string? _testGlaube;
    private M.Berufsgruppe? _testBeruf;
    private bool _testMesse;
    private bool _log;

    /// <summary>Was zuletzt befohlen wurde (für das Entwicklerfenster).</summary>
    public string LetzterBefehl { get; private set; } = "";

    private sealed class Spur
    {
        public AudioStreamPlayer Spieler = null!;
        public M.Stueck Stueck = null!;
        public float Fortschritt, Ziel = 1, Rate;
        public float Grundpegel;
        public double Alter;
    }

    public override void _Ready()
    {
        Aktiv = this;
        ProcessMode = ProcessModeEnum.Always;
        var args = OS.GetCmdlineUserArgs();
        _test = args.FirstOrDefault(a => a.StartsWith("--musiktest="))?["--musiktest=".Length..];
        _log = _test is not null || args.Any(a => a == "--musiklog=1");
        Laden();
        if (_test == "selbst")
        {
            var gut = Selbsttest(t => GD.Print(t));
            GetTree().Quit(gut ? 0 : 1);
        }
    }

    public override void _ExitTree()
    {
        foreach (var s in _spuren)
        {
            s.Spieler.Stop();
            s.Spieler.Stream = null;
        }
        _spuren.Clear();
        if (Aktiv == this) { Aktiv = null; AtmoDuck = 0; }
    }

    private void Laden()
    {
        if (_geladen) return;
        _geladen = true;
        try
        {
            var text = Godot.FileAccess.FileExists(Pfad) ? Godot.FileAccess.GetFileAsString(Pfad) : "";
            (_stuecke, _menueDatei) = M.Laden(text);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Musikspieler: {Pfad} nicht lesbar ({e.Message}); es spielt keine Musik.");
        }
        if (_log) GD.Print($"MUSIK {_stuecke.Count} Stücke geladen, Menü: {(_menueDatei.Length > 0 ? _menueDatei : "keines")}");
    }

    // ── Anschluss ───────────────────────────────────────────────────────────

    /// <summary>Hängt die Partie an (<c>null</c>: ab hier keine Partie mehr). Eine neue Partie beginnt mit frischer Steuerung (Pause von 4 s, keine Sperren).</summary>
    public static void PartieAnschliessen(Func<PartieEingabe?>? quelle)
    {
        if (Aktiv is not { } m) return;
        m._partie = quelle;
        if (quelle is null) m.Ausblenden(2);
        m._steuerung = quelle is null ? null : new M.Steuerung(m._stuecke, m._zufall.NextDouble);
        if (m._steuerung is not null && m._test is { } t) m.TestStarten(t);
        if (quelle is not null) m.MenueVerlassen();
    }

    /// <summary>Das Hauptmenü ist da (<c>true</c>) oder weg: nur dann spielt das Menüstück.</summary>
    public static void MenueSetzen(bool an)
    {
        if (Aktiv is not { } m) return;
        m._menue = an;
        if (an) m._menuePause = 1;
        else m.MenueVerlassen();
    }

    /// <summary>Ein Augenblick geschah (Tod, Hochzeit, Kampf im eigenen Haus): Er schiebt sich vor die Grundmusik.</summary>
    public static void Melden(M.Anlass anlass)
    {
        if (Aktiv is not { } m || m._steuerung is null) return;
        m._steuerung.Melden(anlass);
        if (m._log) GD.Print($"MUSIK Meldung {anlass}");
    }

    private void MenueVerlassen()
    {
        if (_menueLaeuft is null) return;
        _menueLaeuft = null;
        foreach (var s in _spuren) { s.Ziel = 0; s.Rate = 1 / 3f; }
    }

    private void TestStarten(string t)
    {
        switch (t)
        {
            case "tag": _testUhr = 12 * 60; _steuerung?.PauseBeenden(); break;
            case "nacht": _testUhr = 22 * 60; _steuerung?.PauseBeenden(); break;
            case "tod": _steuerung?.Melden(M.Anlass.Tod); break;
            case "hochzeit": _steuerung?.Melden(M.Anlass.Hochzeit); break;
            case "kampf": _steuerung?.Melden(M.Anlass.Kampf); break;
            case "gefahr": _steuerung?.Melden(M.Anlass.Gefahr); break;
            case "einbruch": _steuerung?.Melden(M.Anlass.Einbruch); break;
            case "geburt": _steuerung?.Melden(M.Anlass.Geburt); break;
            case "aufstieg": _steuerung?.Melden(M.Anlass.Aufstieg); break;
            case "messe": _testMesse = true; _testGlaube = "Katholisch"; _testUhr = 10 * 60; _steuerung?.PauseBeenden(); break;
            case "kirche" or "kirche:katholisch" or "kirche:evangelisch" or "kirche:ahtnisch":
                _testOrt = M.Ort.Kirche; _testGlaube = t.Contains(':') ? char.ToUpper(t[7]) + t[8..] : "Katholisch"; _steuerung?.PauseBeenden(); break;
            case "werkstatt" or "werkstatt:handwerk" or "werkstatt:dienstleistung":
                _testOrt = M.Ort.EigeneWerkstatt; _testUhr = 12 * 60;
                _testBeruf = t.EndsWith("dienstleistung") ? M.Berufsgruppe.Dienstleistung : M.Berufsgruppe.Handwerk; _steuerung?.PauseBeenden(); break;
            case "sklaven": _testZustand = M.Anlass.Sklaven; break;
            case "aufstand": _testZustand = M.Anlass.Aufstand; break;
        }
    }

    // ── Je Bild ─────────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        var haeufigkeit = KlangDienst.MusikHaeufigkeit;
        if (_partie is not null && _steuerung is not null) PartieSchritt(delta, haeufigkeit);
        else if (_menue) MenueSchritt(delta, haeufigkeit);
        else if (_menueLaeuft is not null) MenueVerlassen();
        else if (_partie is null && !_menue && _spuren.Any(s => s.Ziel > 0)) Ausblenden(3);
        Spuren(delta);
    }

    private void PartieSchritt(double delta, M.Haeufigkeit haeufigkeit)
    {
        var e = _partie!();
        if (e is null) return; // Ladebildschirm: noch nichts
        var eingabe = new M.Eingabe(_testUhr ?? e.UhrMinute, e.Jahreszeit, e.Volk, _test is null ? haeufigkeit : M.Haeufigkeit.Immer,
            _testZustand != M.Anlass.Keiner ? _testZustand : e.Zustand, _testBeruf ?? e.Beruf, _testGlaube ?? e.Glaube, _testOrt ?? e.Ort,
            _testOrt == M.Ort.Kirche ? _testGlaube : e.KirchenGlaube, _testMesse || e.Messe);
        var zuEnde = _steuerung!.Laufend is { } l && !_spuren.Any(s => s.Stueck == l && s.Ziel > 0 && (s.Spieler.Playing || s.Alter < 0.3));
        var befehle = _steuerung.Schritt(delta, eingabe, zuEnde);
        foreach (var b in befehle) Ausfuehren(b, PartieDb);
    }

    private void MenueSchritt(double delta, M.Haeufigkeit haeufigkeit)
    {
        if (haeufigkeit is M.Haeufigkeit.Aus or M.Haeufigkeit.NurEreignisse)
        {
            if (_menueLaeuft is not null) MenueVerlassen();
            return;
        }
        if (_menueLaeuft is not null)
        {
            if (!_spuren.Any(s => s.Ziel > 0 && (s.Spieler.Playing || s.Alter < 0.3)))
            {
                _menueLaeuft = null;
                _menuePause = MenuePause;
            }
            return;
        }
        _menuePause -= delta;
        if (_menuePause > 0 || _menueDatei.Length == 0) return;
        var stueck = _stuecke.FirstOrDefault(s => s.Datei == _menueDatei);
        if (stueck is null) { _menuePause = 60; return; }
        _menueLaeuft = stueck.Datei;
        Ausfuehren(new M.Befehl(M.BefehlArt.Starten, stueck, 3, 3), MenueDb);
    }

    private void Ausfuehren(M.Befehl b, float pegelDb)
    {
        if (b.Art == M.BefehlArt.Ausblenden)
        {
            Ausblenden(b.AltAus);
            LetzterBefehl = $"Ausblenden {b.AltAus:0.#} s";
            if (_log) GD.Print($"MUSIK {LetzterBefehl}");
            return;
        }
        var stueck = b.Stueck!;
        Ausblenden(b.AltAus > 0 ? b.AltAus : 0.5);
        LetzterBefehl = $"{(b.Wechsel ? "Wechsel" : "Start")} {stueck.Datei}{(b.Anlass != M.Anlass.Keiner ? " (" + b.Anlass + ")" : "")}, ein {b.Einblende:0.#} s, alt aus {b.AltAus:0.#} s{(M.LautstaerkeDb(stueck) != 0 ? $", Verstärkung {M.LautstaerkeDb(stueck):+0.#;-0.#} dB" : "")}";
        if (_log) GD.Print($"MUSIK {LetzterBefehl}");
        var pfad = Ordner + stueck.Datei;
        AudioStream? ton = null;
        if (ResourceLoader.Exists(pfad)) ton = ResourceLoader.Load<AudioStream>(pfad);
        if (ton is null)
        {
            if (_gewarnt.Add(pfad)) GD.PushWarning($"Musikspieler: {pfad} fehlt oder lässt sich nicht laden; das Stück wird übersprungen.");
            return;
        }
        var spieler = new AudioStreamPlayer { Stream = ton, Bus = KlangDienst.BusMusik, VolumeDb = KlangDienst.MindestDb };
        AddChild(spieler);
        spieler.Play();
        _spuren.Add(new Spur { Spieler = spieler, Stueck = stueck, Fortschritt = 0, Ziel = 1, Rate = 1f / (float)Math.Max(b.Einblende, 0.05), Grundpegel = pegelDb + (float)M.LautstaerkeDb(stueck) });
    }

    private void Ausblenden(double sekunden)
    {
        foreach (var s in _spuren.Where(s => s.Ziel > 0))
        {
            s.Ziel = 0;
            s.Rate = 1f / (float)Math.Max(sekunden, 0.05);
        }
    }

    private void Spuren(double delta)
    {
        // Ducking: eine Ansage des Haushofmeisters (Bus Stimmen) senkt die Musik
        _duckZiel = KlangDienst.AnsageSpielt ? DuckDb : 0f;
        var schritt = (float)delta * Math.Abs(DuckDb) / (_duckZiel < _duck ? DuckAbSekunden : DuckAufSekunden);
        _duck = _duck < _duckZiel ? Math.Min(_duck + schritt, _duckZiel) : Math.Max(_duck - schritt, _duckZiel);
        // Stadt-Atmosphäre unter einem Ereignisstück leiser
        var atmoZiel = _spuren.Any(x => x.Ziel > 0 && x.Stueck.Lage == "event") ? (float)M.AtmoDuckDb : 0f;
        var atmoSchritt = (float)delta * (float)Math.Abs(M.AtmoDuckDb) / (float)(atmoZiel < AtmoDuck ? M.AtmoAb : M.AtmoAuf);
        AtmoDuck = AtmoDuck < atmoZiel ? Math.Min(AtmoDuck + atmoSchritt, atmoZiel) : Math.Max(AtmoDuck - atmoSchritt, atmoZiel);
        for (var i = _spuren.Count - 1; i >= 0; i--)
        {
            var s = _spuren[i];
            s.Alter += delta;
            var d = s.Rate * (float)delta;
            s.Fortschritt = s.Fortschritt < s.Ziel ? Math.Min(s.Fortschritt + d, s.Ziel) : Math.Max(s.Fortschritt - d, s.Ziel);
            var amplitude = MathF.Sin(s.Fortschritt * MathF.PI / 2f); // gleiche Leistung beim Überblenden
            s.Spieler.VolumeDb = amplitude <= 0.0005f ? KlangDienst.MindestDb : Math.Max(20f * MathF.Log10(amplitude) + s.Grundpegel + _duck, KlangDienst.MindestDb);
            if ((s.Ziel <= 0 && s.Fortschritt <= 0.0005f) || (!s.Spieler.Playing && s.Alter > 0.3))
            {
                s.Spieler.Stop();
                s.Spieler.Stream = null;
                s.Spieler.QueueFree();
                _spuren.RemoveAt(i);
            }
        }
    }

    // ── Entwicklerfenster und Prüfung ───────────────────────────────────────

    /// <summary>Was läuft und wann die nächste Pause endet, für das Entwicklerfenster (Reiter Klänge, Abschnitt Musik).</summary>
    public string Zustand()
    {
        var h = KlangDienst.MusikHaeufigkeit;
        var kopf = $"Häufigkeit {M.Name(h)} · Stücke {_stuecke.Count} · Ducking {_duck:0.0} dB · Atmo {AtmoDuck:0.0} dB";
        if (_steuerung is { } st && _partie is not null)
        {
            var nacht = st.LaufendeLage ?? "–";
            var laufend = st.Laufend is { } l
                ? $"läuft: {l.Titel} ({l.Lage}{(l.Zusatz is not null ? "/" + l.Zusatz : "")}{(l.Volk is not null ? "/" + l.Volk : "")}{(l.Beruf is not null ? "/" + l.Beruf : "")}{(l.Glaube is not null ? "/" + l.Glaube : "")}), {st.LaufZeit:0} von {l.Dauer:0} s"
                    + (st.LaufenderAnlass != M.Anlass.Keiner ? $" · Anlass {st.LaufenderAnlass}" : $" · für {nacht}")
                : h == M.Haeufigkeit.Aus ? "Musik aus"
                : h == M.Haeufigkeit.NurEreignisse ? "wartet auf ein Ereignis"
                : $"Pause: noch {st.PauseRest:0} s";
            return kopf + "\n" + laufend;
        }
        if (_menue) return kopf + "\nHauptmenü: " + (_menueLaeuft ?? (h is M.Haeufigkeit.Aus or M.Haeufigkeit.NurEreignisse ? "aus" : $"Pause noch {Math.Max(_menuePause, 0):0} s"));
        return kopf + "\nkeine Partie, kein Hauptmenü: still";
    }

    /// <summary>Alle Stücke der Liste (für den Entwicklerreiter): Stück und ob die Datei da ist.</summary>
    public IReadOnlyList<M.Stueck> Stuecke => _stuecke;

    /// <summary>Spielt ein Stück sofort allein (Entwicklerfenster „▶“): blendet die Grundmusik aus.</summary>
    public void Vorhoeren(M.Stueck stueck) => Ausfuehren(new M.Befehl(M.BefehlArt.Starten, stueck, 1, 1), PartieDb);

    /// <summary>
    /// Selbstprüfung ohne Spiel: Liste lesbar, jede Datei da, jedes Stück mit gültiger Lage, jede Lage (Tag, Nacht, Tod, vier Ereignisse) besetzt und das
    /// Menüstück vorhanden. <c>--musiktest=selbst</c>, Rückgabewert 0 = in Ordnung.
    /// </summary>
    private bool Selbsttest(Action<string> log)
    {
        var gut = true;
        void Fehler(string t) { log("MUSIKTEST FEHLER: " + t); gut = false; }
        if (_stuecke.Count == 0) Fehler("keine Stücke");
        foreach (var s in _stuecke)
        {
            var pfad = Ordner + s.Datei;
            if (!ResourceLoader.Exists(pfad) || ResourceLoader.Load<AudioStream>(pfad) is not { } ton) Fehler($"{pfad} fehlt oder ist nicht ladbar");
            else if (Math.Abs(ton.GetLength() - s.Dauer) > 1.5) Fehler($"{s.Datei}: Länge {ton.GetLength():0.0} s statt {s.Dauer:0.0} s");
        }
        foreach (var a in Enum.GetValues<M.Anlass>().Where(a => a != M.Anlass.Keiner))
            if (M.AnlassKandidaten(_stuecke, a).Count == 0) Fehler($"kein Stück für {a}");
        if (M.NachtKandidaten(_stuecke).Count == 0) Fehler("keine Nachtstücke");
        foreach (var j in Enum.GetValues<Jahreszeit>())
            if (M.TagKandidaten(_stuecke, j, null).Count < 3) Fehler($"zu wenige Tagesstücke im {j}");
        if (_stuecke.All(s => s.Datei != _menueDatei)) Fehler("Menüstück fehlt in der Liste");
        foreach (var g in new[] { "Katholisch", "Evangelisch", "Ahtnisch" })
            if (M.GlaubensKandidaten(_stuecke, g).Count == 0) Fehler($"kein Glaubensstück {g}");
        foreach (var b in new[] { M.Berufsgruppe.Handwerk, M.Berufsgruppe.Dienstleistung })
            if (!_stuecke.Any(s => s.Beruf == M.BerufName(b))) Fehler($"kein Berufsstück {b}");
        log(gut ? $"MUSIKTEST in Ordnung ({_stuecke.Count} Stücke)" : "MUSIKTEST FEHLER (siehe oben)");
        return gut;
    }
}
