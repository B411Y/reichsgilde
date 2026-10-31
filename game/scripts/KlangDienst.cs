using Godot;

namespace Reichsgilde;

/// <summary>
/// Die zentrale Tonausgabe (Autoload). Hier laufen alle Töne durch, damit Busse, Lautstärken und Mengen an einer Stelle stehen:
///   • <b>Busse</b> (<c>default_bus_layout.tres</c>): Master, Musik, Umgebung, Effekte, Oberflaeche, Stimmen – alle an Master. Die Namen stehen nur
///     hier als Konstanten; Godot fällt bei einem falschen Busnamen still auf Master zurück, darum prüft <see cref="BusseTesten"/> beim Start.
///   • <b>Regler</b> (Einstellungen, Reiter „Ton“): je Bus Prozent und Stumm, dazu „Ton im Hintergrund stumm“. Gespeichert in
///     <c>user://einstellungen.cfg</c>, Abschnitt „ton“. Master und alle Busse bleiben bei 0 dB oder darunter (kein Verstärken).
///   • <b>Abspielen:</b> <see cref="OberflaecheSpielen"/> (2D), <see cref="WeltSpielen"/> (3D an einer Stelle, mit Reichweite und Obergrenze
///     gleichzeitiger Töne), <see cref="SchleifeStarten"/> (Umgebung und Musik, mit Überblendung oder vom Aufrufer geregelt).
///   • <b>Unterbusse der Umgebung</b> (<see cref="Unterbusse"/>) für <see cref="KlangUmgebung"/>.
/// Der Ton selbst kommt als <see cref="AudioStream"/> (später <see cref="AudioStreamRandomizer"/> mit Varianten) vom Aufrufer.
/// </summary>
public partial class KlangDienst : Node
{
    public const string BusMaster = "Master";
    public const string BusMusik = "Musik";
    public const string BusUmgebung = "Umgebung";
    public const string BusEffekte = "Effekte";
    public const string BusOberflaeche = "Oberflaeche";
    public const string BusStimmen = "Stimmen";

    /// <summary>
    /// Unterbusse der Umgebung (Ton Schritt 4, <see cref="KlangUmgebung"/>). Godot erlaubt Sends nur an Busse mit kleinerer Nummer, darum diese
    /// Reihenfolge im Layout: UmgebungDraussen (6) → Umgebung, UmgebungStadt (7), UmgebungRufNah (8) und UmgebungRufFern (9) → UmgebungDraussen.
    /// Draußen trägt die Dämpfung in Räumen, im Dom und in der Pause (Pegel und Tiefpass), Stadt die Ferne beim Herauszoomen, die Rufbusse
    /// Seite (Panner) und Tiefpass des einzelnen Rufs. Der Bus Umgebung selbst bleibt ohne Effekt (dort hängt Schritt 7 das Ducking an);
    /// spätere Innengeräusche (Schritt 6) laufen direkt auf Umgebung, also ungedämpft.
    /// </summary>
    public const string BusUmgebungDraussen = "UmgebungDraussen";
    public const string BusUmgebungStadt = "UmgebungStadt";
    public const string BusUmgebungRufNah = "UmgebungRufNah";
    public const string BusUmgebungRufFern = "UmgebungRufFern";

    /// <summary>Die Unterbusse mit ihrem Ziel und den Effekten, die <see cref="BusseTesten"/> erwartet (Effekt 0 Tiefpass; Rufbusse: 0 Panner, 1 Tiefpass).</summary>
    public static readonly (string Bus, string Ziel, bool MitPanner)[] Unterbusse =
    {
        (BusUmgebungDraussen, BusUmgebung, false),
        (BusUmgebungStadt, BusUmgebungDraussen, false),
        (BusUmgebungRufNah, BusUmgebungDraussen, true),
        (BusUmgebungRufFern, BusUmgebungDraussen, true),
    };

    /// <summary>Die Regler der Einstellungen in ihrer Reihenfolge: Bus, Anzeigename, Schlüssel in der Datei.</summary>
    public static readonly (string Bus, string Name, string Schluessel)[] Regelbusse =
    {
        (BusMaster, "Gesamt", "gesamt"),
        (BusMusik, "Musik", "musik"),
        (BusUmgebung, "Umgebung", "umgebung"),
        (BusEffekte, "Effekte", "effekte"),
        (BusOberflaeche, "Oberfläche", "oberflaeche"),
        (BusStimmen, "Stimmen", "stimmen"),
    };

    private const string Datei = "user://einstellungen.cfg";
    private const int StandardProzent = 80;
    /// <summary>Unter diesem Wert in dB gilt ein Bus als stumm (0 % wird zu −80 dB und zusätzlich stumm geschaltet).</summary>
    public const float MindestDb = -80f;

    /// <summary>Höchstzahl gleichzeitiger Töne je 3D-Spieler und Reichweite in Metern, wenn der Aufrufer nichts anderes nennt.</summary>
    public const int StandardGleichzeitig = 4;
    public const float StandardReichweite = 40f;

    private static KlangDienst? _instanz;
    private static readonly int[] _prozent = Enumerable.Repeat(StandardProzent, Regelbusse.Length).ToArray();
    private static readonly bool[] _stumm = new bool[Regelbusse.Length];
    private static bool _hintergrundStumm = true;
    private static bool _imHintergrund;

    private readonly Dictionary<string, SchleifenSpieler> _schleifen = new();

    /// <summary>Pause in Echtzeit-Sekunden zwischen dem Ende einer Ansage des Haushofmeisters und der nächsten (unabhängig vom Spieltempo).</summary>
    public const double StandardAnsagenAbstand = 20.0;
    /// <summary>So lange wartet eine Ansage auf ihren Platz (Echtzeit-Sekunden); danach ist sie überholt und verfällt.</summary>
    public const double StandardAnsagenVerfall = 45.0;
    /// <summary>Die geltenden Werte (<c>KlangAnsagen</c> setzt sie aus <c>daten/ansagen.json</c>).</summary>
    public static double AnsagenAbstand = StandardAnsagenAbstand, AnsagenVerfall = StandardAnsagenVerfall;
    /// <summary>Für das Log der Ansagen (Verfall, Start nach Wartezeit); wird nur im Entwicklermodus oder bei einer Startoption gesetzt.</summary>
    public static Action<string>? AnsagenLog;

    /// <summary>Was aus einer angefragten Ansage wurde.</summary>
    public enum AnsageErgebnis { Gespielt, Wartet, Verworfen }

    private static bool _ansagenAn = true;
    private AudioStreamPlayer? _ansageSpieler;
    private bool _ansageLaeuft;
    private double _ansageFrei, _ansageStart;
    private (AudioStream Ton, int Rang, string Name, double Bis)? _wartend;

    /// <summary>Ansagen des Haushofmeisters an/aus (Standard an).</summary>
    public static bool AnsagenAn => _ansagenAn;

    /// <summary>Ton im Hintergrund stumm (Standard an): Verliert das Fenster den Fokus, wird der Master stumm.</summary>
    public static bool HintergrundStumm => _hintergrundStumm;

    public static int Prozent(int bus) => _prozent[bus];
    public static bool Stumm(int bus) => _stumm[bus];

    // ── Umrechnung (rein, ohne Godot-Aufruf) ────────────────────────────────

    /// <summary>Lineare Prozent (0–100) in Dezibel: 100 % = 0 dB (nie darüber), 50 % ≈ −6 dB, 0 % = <see cref="MindestDb"/>.</summary>
    public static float ProzentZuDb(int prozent)
    {
        if (prozent <= 0) return MindestDb;
        return Math.Max(MindestDb, 20f * MathF.Log10(Math.Min(prozent, 100) / 100f));
    }

    // ── Start ───────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _instanz = this;
        ProcessMode = ProcessModeEnum.Always;
        BusseTesten();
        var test = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--umgebungtest="));
        if (test is "--umgebungtest=1" or "--umgebungtest=kaputt")
        {
            // Selbstprüfung der Umgebungsklänge ohne Spiel (auch headless): Kurven, Abläufe, Rufe, dazu die Unterbusse; Rückgabewert 0 = in Ordnung
            var busse = BusseTesten();
            GD.Print(busse ? "UMGEBUNGTEST Unterbusse in Ordnung" : "UMGEBUNGTEST FEHLER: Unterbusse (siehe Warnungen)");
            var gut = KlangUmgebungModell.Selbsttest(t => GD.Print(t), kaputt: test == "--umgebungtest=kaputt") && busse;
            GetTree().Quit(gut ? 0 : 1);
            return;
        }
        var datei = new ConfigFile();
        var geladen = datei.Load(Datei) == Error.Ok;
        for (var i = 0; i < Regelbusse.Length; i++)
        {
            _prozent[i] = geladen ? Math.Clamp(datei.GetValue("ton", Regelbusse[i].Schluessel, StandardProzent).AsInt32(), 0, 100) : StandardProzent;
            _stumm[i] = geladen && datei.GetValue("ton", Regelbusse[i].Schluessel + "_stumm", false).AsBool();
        }
        _ansagenAn = !geladen || datei.GetValue("ton", "ansagen", true).AsBool();
        _hintergrundStumm = !geladen || datei.GetValue("ton", "hintergrund_stumm", true).AsBool();
        for (var i = 0; i < Regelbusse.Length; i++) BusAnwenden(i);
    }

    /// <summary>
    /// Schreibt eine Warnung ins Log für jeden erwarteten Bus, der im Layout fehlt (sonst spielt Godot still über Master), und für jeden
    /// Unterbus der Umgebung mit falschem Ziel oder ohne seine Effekte. Gibt zurück, ob alles stimmt.
    /// </summary>
    public static bool BusseTesten()
    {
        var gut = true;
        foreach (var (bus, _, _) in Regelbusse)
            if (AudioServer.GetBusIndex(bus) < 0)
            {
                GD.PushWarning($"KlangDienst: Audiobus „{bus}“ fehlt in default_bus_layout.tres – Töne dieses Busses laufen über Master.");
                gut = false;
            }
        foreach (var (bus, ziel, panner) in Unterbusse)
        {
            var index = AudioServer.GetBusIndex(bus);
            var fehler = index < 0 ? "fehlt"
                : AudioServer.GetBusSend(index) != ziel ? $"sendet an „{AudioServer.GetBusSend(index)}“ statt „{ziel}“"
                : panner && AudioServer.GetBusEffect(index, 0) is not AudioEffectPanner ? "hat keinen Panner als Effekt 0"
                : AudioServer.GetBusEffect(index, panner ? 1 : 0) is not AudioEffectLowPassFilter ? "hat keinen Tiefpass"
                : null;
            if (fehler is null) continue;
            GD.PushWarning($"KlangDienst: Unterbus „{bus}“ {fehler} (default_bus_layout.tres) – die Umgebung klingt dann nicht wie geplant.");
            gut = false;
        }
        return gut;
    }

    /// <summary>Ist das Fenster im Hintergrund und darum der Ton stumm („Ton im Hintergrund stumm“)? Die Umgebung hält dann ihre Rufe an.</summary>
    public static bool HintergrundIstStumm => _hintergrundStumm && _imHintergrund;

    // ── Regler ──────────────────────────────────────────────────────────────

    public static void ProzentSetzen(int bus, int prozent, bool speichern = true)
    {
        _prozent[bus] = Math.Clamp(prozent, 0, 100);
        BusAnwenden(bus);
        if (speichern) Speichern(Regelbusse[bus].Schluessel, _prozent[bus]);
    }

    public static void StummSetzen(int bus, bool stumm, bool speichern = true)
    {
        _stumm[bus] = stumm;
        BusAnwenden(bus);
        if (speichern) Speichern(Regelbusse[bus].Schluessel + "_stumm", stumm);
    }

    public static void HintergrundStummSetzen(bool an, bool speichern = true)
    {
        _hintergrundStumm = an;
        BusAnwenden(0);
        if (speichern) Speichern("hintergrund_stumm", an);
    }

    public static void AnsagenSetzen(bool an, bool speichern = true)
    {
        _ansagenAn = an;
        if (!an && _instanz is not null)
        {
            _instanz._wartend = null;
            _instanz._ansageSpieler?.Stop();
        }
        if (speichern) Speichern("ansagen", an);
    }

    private static void BusAnwenden(int nummer)
    {
        var index = AudioServer.GetBusIndex(Regelbusse[nummer].Bus);
        if (index < 0) return;
        var stumm = _stumm[nummer] || _prozent[nummer] <= 0 || (nummer == 0 && _hintergrundStumm && _imHintergrund);
        AudioServer.SetBusVolumeDb(index, ProzentZuDb(_prozent[nummer]));
        AudioServer.SetBusMute(index, stumm);
    }

    private static void Speichern(string schluessel, Variant wert)
    {
        var datei = new ConfigFile();
        datei.Load(Datei);
        datei.SetValue("ton", schluessel, wert);
        datei.Save(Datei);
    }

    // ── Fokus ───────────────────────────────────────────────────────────────

    public override void _Notification(int was)
    {
        if (was == NotificationApplicationFocusOut || was == NotificationWMWindowFocusOut) FokusWechsel(true);
        else if (was == NotificationApplicationFocusIn || was == NotificationWMWindowFocusIn) FokusWechsel(false);
    }

    private static void FokusWechsel(bool imHintergrund)
    {
        _imHintergrund = imHintergrund;
        BusAnwenden(0);
    }

    // ── Abspielen ───────────────────────────────────────────────────────────

    /// <summary>Oberflächenklang (Klick, Papier, Siegel …): ohne Ort, einmal abspielen. Ohne Ton (null) passiert nichts.</summary>
    public static void OberflaecheSpielen(AudioStream? ton, float db = 0f, float tonhoehe = 1f)
    {
        if (ton is null || _instanz is null) return;
        var spieler = new AudioStreamPlayer { Stream = ton, Bus = BusOberflaeche, VolumeDb = Math.Min(db, 0f), PitchScale = tonhoehe };
        _instanz.AddChild(spieler);
        spieler.Finished += spieler.QueueFree;
        spieler.Play();
    }

    /// <summary>
    /// Weltklang an einer Stelle der Stadt. <paramref name="reichweite"/> ist die Entfernung in Metern, ab der nichts mehr zu hören ist,
    /// <paramref name="gleichzeitig"/> die Obergrenze gleichzeitiger Töne dieses Spielers. Der Spieler gibt sich nach dem Ton selbst frei.
    /// </summary>
    public static void WeltSpielen(AudioStream? ton, Vector3 ort, string bus = BusEffekte, float reichweite = StandardReichweite,
                                   int gleichzeitig = StandardGleichzeitig, float db = 0f, float tonhoehe = 1f)
    {
        if (ton is null || _instanz is null) return;
        var spieler = new AudioStreamPlayer3D
        {
            Stream = ton, Bus = bus, Position = ort, MaxDistance = reichweite, MaxPolyphony = gleichzeitig,
            UnitSize = Math.Max(reichweite / 8f, 1f), VolumeDb = Math.Min(db, 0f), PitchScale = tonhoehe,
        };
        _instanz.AddChild(spieler);
        spieler.Finished += spieler.QueueFree;
        spieler.Play();
    }

    private static double Jetzt() => Time.GetTicksMsec() / 1000.0;

    private bool AnsageFrei(double jetzt) => !_ansageLaeuft && jetzt >= _ansageFrei;

    /// <summary>
    /// Ansage des Haushofmeisters (Bus Stimmen): immer nur eine zur Zeit, danach mindestens <see cref="AnsagenAbstand"/> Sekunden Pause (Echtzeit).
    /// Ist gerade keine Ansage dran, spielt sie sofort. Sonst wartet sie, aber nur <b>eine</b> Ansage: die wichtigste. <paramref name="rang"/>
    /// 1 ist der höchste Rang (Tod), größere Zahlen sind unwichtiger; eine gleich wichtige ersetzt die wartende (sie ist aktueller), eine
    /// unwichtigere verfällt sofort. Wer länger als <see cref="AnsagenVerfall"/> Sekunden wartet, verfällt auch. Eine laufende Ansage wird nie
    /// unterbrochen. Bei ausgeschalteten Ansagen passiert nichts.
    /// </summary>
    public static AnsageErgebnis AnsageSpielen(AudioStream? ton, int rang = 5, string name = "")
    {
        if (ton is null || _instanz is null || !_ansagenAn) return AnsageErgebnis.Verworfen;
        var jetzt = Jetzt();
        if (_instanz.AnsageFrei(jetzt))
        {
            _instanz._wartend = null;
            _instanz.AnsageStarten(ton);
            return AnsageErgebnis.Gespielt;
        }
        if (_instanz._wartend is { } wartend && rang > wartend.Rang) return AnsageErgebnis.Verworfen;
        if (_instanz._wartend is { } alt) AnsagenLog?.Invoke($"ANSAGE {alt.Name}: ersetzt durch {name} (Rang {rang} vor {alt.Rang})");
        _instanz._wartend = (ton, rang, name, jetzt + AnsagenVerfall);
        return AnsageErgebnis.Wartet;
    }

    private void AnsageStarten(AudioStream ton)
    {
        if (_ansageSpieler is null)
        {
            _ansageSpieler = new AudioStreamPlayer { Bus = BusStimmen };
            AddChild(_ansageSpieler);
        }
        _ansageSpieler.Stream = ton;
        _ansageSpieler.Play();
        _ansageLaeuft = true;
        _ansageStart = Jetzt();
    }

    public override void _Process(double delta)
    {
        var jetzt = Jetzt();
        // Ende der Ansage: von hier an zählt die Pause (kein Signal, damit ein abgebrochener Spieler nichts hängen lässt)
        if (_ansageLaeuft && jetzt - _ansageStart > 0.2 && _ansageSpieler?.Playing != true)
        {
            _ansageLaeuft = false;
            _ansageFrei = jetzt + AnsagenAbstand;
        }
        if (_wartend is not { } wartend) return;
        if (jetzt > wartend.Bis)
        {
            _wartend = null;
            AnsagenLog?.Invoke($"ANSAGE {wartend.Name}: verfallen (länger als {AnsagenVerfall:0} s gewartet)");
        }
        else if (AnsageFrei(jetzt))
        {
            _wartend = null;
            AnsagenLog?.Invoke($"ANSAGE {wartend.Name}: kommt nach der Pause dran");
            AnsageStarten(wartend.Ton);
        }
    }

    /// <summary>
    /// Startet eine Schleife (Umgebung oder Musik) unter einem Namen und blendet sie ein. Läuft unter dem Namen schon eine, wird sie
    /// ausgeblendet und durch die neue ersetzt. <paramref name="ton"/> sollte sich selbst wiederholen (Schleife in der Importeinstellung).
    /// <paramref name="einblendSekunden"/> 0: kein Einblenden, die Schleife beginnt sofort mit <paramref name="db"/> – dann regelt der Aufrufer
    /// den Pegel selbst (<see cref="SchleifePegelSetzen"/>). <paramref name="zufallsStart"/>: an einer zufälligen Stelle beginnen (eigener Zufall
    /// des Tons, nie der der Simulation), damit zwei Starts nicht gleich klingen.
    /// </summary>
    public static void SchleifeStarten(string name, AudioStream? ton, string bus = BusUmgebung, float einblendSekunden = 2f, float db = 0f,
                                       bool zufallsStart = false)
    {
        if (ton is null || _instanz is null) return;
        SchleifeStoppen(name, einblendSekunden > 0 ? einblendSekunden : 0.5f);
        var spieler = new AudioStreamPlayer { Stream = ton, Bus = bus, VolumeDb = einblendSekunden > 0 ? MindestDb : Math.Min(db, 0f) };
        _instanz.AddChild(spieler);
        var start = zufallsStart ? (float)(_zufall.NextDouble() * Math.Max(ton.GetLength() - 0.1, 0)) : 0f;
        spieler.Play(start);
        var schleife = new SchleifenSpieler(spieler);
        _instanz._schleifen[name] = schleife;
        if (einblendSekunden > 0) schleife.Tween = _instanz.Ueberblenden(spieler, Math.Min(db, 0f), einblendSekunden);
    }

    /// <summary>
    /// Blendet die Schleife dieses Namens aus und gibt sie danach frei. Die Blende gehört danach nur noch dem alten Spieler: Eine neue Schleife
    /// gleichen Namens beendet sie nicht.
    /// </summary>
    public static void SchleifeStoppen(string name, float ausblendSekunden = 2f)
    {
        if (_instanz is null || !_instanz._schleifen.Remove(name, out var alt)) return;
        alt.Tween?.Kill();
        alt.Spieler.StreamPaused = false;
        alt.Tween = _instanz.Ueberblenden(alt.Spieler, MindestDb, ausblendSekunden, danachFreigeben: true);
    }

    /// <summary>Ändert die Lautstärke einer laufenden Schleife weich (z. B. Musik); eine laufende Blende dieser Schleife wird vorher beendet.</summary>
    public static void SchleifeLautstaerke(string name, float db, float sekunden = 1f)
    {
        if (_instanz is null || !_instanz._schleifen.TryGetValue(name, out var s)) return;
        s.Tween?.Kill();
        s.Tween = _instanz.Ueberblenden(s.Spieler, Math.Min(db, 0f), sekunden);
    }

    /// <summary>
    /// Setzt den Pegel einer Schleife sofort, ohne Blende (für Pegel, die sich in jedem Bild ändern, z. B. <see cref="KlangUmgebung"/>; die
    /// Glättung rechnet der Aufrufer). Eine laufende Blende wird beendet. Godot mischt die Änderung je Tonpuffer weich, es knackt nicht.
    /// </summary>
    public static void SchleifePegelSetzen(string name, float db)
    {
        if (_instanz is null || !_instanz._schleifen.TryGetValue(name, out var s)) return;
        if (s.Tween is not null)
        {
            s.Tween.Kill();
            s.Tween = null;
        }
        s.Spieler.VolumeDb = Math.Clamp(db, MindestDb, 0f);
    }

    /// <summary>Hält eine Schleife an oder lässt sie weiterlaufen (spart das Entschlüsseln stummer Schichten; die Stelle bleibt erhalten).</summary>
    public static void SchleifePausieren(string name, bool angehalten)
    {
        if (_instanz is not null && _instanz._schleifen.TryGetValue(name, out var s) && s.Spieler.StreamPaused != angehalten)
            s.Spieler.StreamPaused = angehalten;
    }

    /// <summary>Läuft eine Schleife unter diesem Namen (auch angehalten)?</summary>
    public static bool SchleifeLaeuft(string name) => _instanz is not null && _instanz._schleifen.ContainsKey(name);

    /// <summary>Pegel einer Schleife in dB (oder <see cref="MindestDb"/>, wenn es sie nicht gibt), z. B. für den Entwicklerreiter.</summary>
    public static float SchleifenPegel(string name) =>
        _instanz is not null && _instanz._schleifen.TryGetValue(name, out var s) ? s.Spieler.VolumeDb : MindestDb;

    /// <summary>Wie viele Schleifen laufen (benannte), und wie viele Spieler hängen insgesamt noch am Dienst (auch ausblendende)?</summary>
    public static (int Benannt, int Spieler) SchleifenAnzahl =>
        _instanz is null ? (0, 0) : (_instanz._schleifen.Count, _instanz.GetChildren().OfType<AudioStreamPlayer>().Count(p => p != _instanz._ansageSpieler));

    /// <summary>Eigener Zufall für Startstellen (nie der der Simulation: der Lockstep im Mehrspieler bleibt unberührt).</summary>
    private static readonly Random _zufall = new();

    private Tween Ueberblenden(AudioStreamPlayer spieler, float zielDb, float sekunden, bool danachFreigeben = false)
    {
        var tween = CreateTween();
        tween.TweenProperty(spieler, "volume_db", zielDb, Math.Max(sekunden, 0.01f));
        if (danachFreigeben) tween.TweenCallback(Callable.From(spieler.QueueFree));
        return tween;
    }

    /// <summary>Eine laufende Schleife mit ihrer Blende (die vor jeder neuen beendet wird, damit sich keine Tweens auf <c>volume_db</c> stapeln).</summary>
    private sealed class SchleifenSpieler(AudioStreamPlayer spieler)
    {
        public AudioStreamPlayer Spieler { get; } = spieler;
        public Tween? Tween { get; set; }
    }
}
