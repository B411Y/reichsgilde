using Godot;

namespace Reichsgilde;

/// <summary>
/// Die zentrale Tonausgabe (Autoload). Hier laufen alle Töne durch, damit Busse, Lautstärken und Mengen an einer Stelle stehen:
///   • <b>Busse</b> (<c>default_bus_layout.tres</c>): Master, Musik, Umgebung, Effekte, Oberflaeche, Stimmen – alle an Master. Die Namen stehen nur
///     hier als Konstanten; Godot fällt bei einem falschen Busnamen still auf Master zurück, darum prüft <see cref="BusseTesten"/> beim Start.
///   • <b>Regler</b> (Einstellungen, Reiter „Ton“): je Bus Prozent und Stumm, dazu „Ton im Hintergrund stumm“. Gespeichert in
///     <c>user://einstellungen.cfg</c>, Abschnitt „ton“. Master und alle Busse bleiben bei 0 dB oder darunter (kein Verstärken).
///   • <b>Abspielen:</b> <see cref="OberflaecheSpielen"/> (2D), <see cref="WeltSpielen"/> (3D an einer Stelle, mit Reichweite und Obergrenze
///     gleichzeitiger Töne), <see cref="SchleifeStarten"/> (Umgebung und Musik, mit Überblendung).
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

    /// <summary>Ansagen des Haushofmeisters: höchstens so viele warten; kommt eine weitere, verfällt die älteste (zu dichte Folge).</summary>
    public const int AnsagenWarteschlange = 2;
    /// <summary>Pause in Sekunden zwischen zwei Ansagen.</summary>
    public const double AnsagenAbstand = 8.0;
    private static bool _ansagenAn = true;
    private readonly Queue<AudioStream> _ansagen = new();
    private AudioStreamPlayer? _ansageSpieler;
    private double _ansageFrei;

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

    /// <summary>Schreibt eine Warnung ins Log für jeden erwarteten Bus, der im Layout fehlt (sonst spielt Godot still über Master).</summary>
    private static void BusseTesten()
    {
        foreach (var (bus, _, _) in Regelbusse)
            if (AudioServer.GetBusIndex(bus) < 0)
                GD.PushWarning($"KlangDienst: Audiobus „{bus}“ fehlt in default_bus_layout.tres – Töne dieses Busses laufen über Master.");
    }

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
            _instanz._ansagen.Clear();
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

    /// <summary>
    /// Ansage des Haushofmeisters (Bus Stimmen): immer nur eine zur Zeit, mindestens <see cref="AnsagenAbstand"/> Sekunden Pause danach.
    /// Läuft schon eine, wartet die neue (höchstens <see cref="AnsagenWarteschlange"/>); ist die Schlange voll, verfällt die älteste.
    /// Bei ausgeschalteten Ansagen passiert nichts.
    /// </summary>
    public static void AnsageSpielen(AudioStream? ton)
    {
        if (ton is null || _instanz is null || !_ansagenAn) return;
        if (_instanz._ansageSpieler?.Playing == true || Time.GetTicksMsec() / 1000.0 < _instanz._ansageFrei)
        {
            while (_instanz._ansagen.Count >= AnsagenWarteschlange) _instanz._ansagen.Dequeue();
            _instanz._ansagen.Enqueue(ton);
            return;
        }
        _instanz.AnsageStarten(ton);
    }

    private void AnsageStarten(AudioStream ton)
    {
        if (_ansageSpieler is null)
        {
            _ansageSpieler = new AudioStreamPlayer { Bus = BusStimmen };
            AddChild(_ansageSpieler);
            _ansageSpieler.Finished += () => _ansageFrei = Time.GetTicksMsec() / 1000.0 + AnsagenAbstand;
        }
        _ansageSpieler.Stream = ton;
        _ansageSpieler.Play();
    }

    public override void _Process(double delta)
    {
        if (_ansagen.Count == 0 || _ansageSpieler?.Playing == true || Time.GetTicksMsec() / 1000.0 < _ansageFrei) return;
        AnsageStarten(_ansagen.Dequeue());
    }

    /// <summary>
    /// Startet eine Schleife (Umgebung oder Musik) unter einem Namen und blendet sie ein. Läuft unter dem Namen schon eine, wird sie
    /// ausgeblendet und durch die neue ersetzt. <paramref name="ton"/> sollte sich selbst wiederholen (Schleife in der Importeinstellung).
    /// </summary>
    public static void SchleifeStarten(string name, AudioStream? ton, string bus = BusUmgebung, float einblendSekunden = 2f, float db = 0f)
    {
        if (ton is null || _instanz is null) return;
        SchleifeStoppen(name, einblendSekunden);
        var spieler = new AudioStreamPlayer { Stream = ton, Bus = bus, VolumeDb = MindestDb };
        _instanz.AddChild(spieler);
        spieler.Play();
        _instanz.Ueberblenden(spieler, Math.Min(db, 0f), einblendSekunden);
        _instanz._schleifen[name] = new SchleifenSpieler(spieler);
    }

    /// <summary>Blendet die Schleife dieses Namens aus und gibt sie danach frei.</summary>
    public static void SchleifeStoppen(string name, float ausblendSekunden = 2f)
    {
        if (_instanz is null || !_instanz._schleifen.Remove(name, out var alt)) return;
        _instanz.Ueberblenden(alt.Spieler, MindestDb, ausblendSekunden, danachFreigeben: true);
    }

    /// <summary>Ändert die Lautstärke einer laufenden Schleife weich (z. B. Umgebung je nach Zoom).</summary>
    public static void SchleifeLautstaerke(string name, float db, float sekunden = 1f)
    {
        if (_instanz is null || !_instanz._schleifen.TryGetValue(name, out var s)) return;
        _instanz.Ueberblenden(s.Spieler, Math.Min(db, 0f), sekunden);
    }

    private void Ueberblenden(AudioStreamPlayer spieler, float zielDb, float sekunden, bool danachFreigeben = false)
    {
        var tween = CreateTween();
        tween.TweenProperty(spieler, "volume_db", zielDb, Math.Max(sekunden, 0.01f));
        if (danachFreigeben) tween.TweenCallback(Callable.From(spieler.QueueFree));
    }

    private sealed record SchleifenSpieler(AudioStreamPlayer Spieler);
}
