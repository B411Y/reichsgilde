using Godot;
using Reichsgilde.Core.Zeit;
using M = Reichsgilde.KlangUmgebungModell;

namespace Reichsgilde;

/// <summary>
/// Die <b>Umgebungsklänge der Stadt</b> (Ton Schritt 4): neun Schleifen (Stadtleben, Abend, Vögel, Nacht, Wind) und Einzelrufe (Hahn, Hund,
/// Kauz, Amsel, Pferd, Geflügel, Passanten, fernes Handwerk). Nur in der Partie (Kind der <c>Hauptszene</c>, auch in der Probepartie), nicht im
/// Hauptmenü und nicht in der Kartenwerkstatt. Was wann wie laut klingt, rechnet <see cref="KlangUmgebungModell"/>; dieser Knoten lädt die Dateien
/// (im Hintergrund, schon während des Ladebildschirms), spielt ab und stellt die Busse:
///   • Schleifen über <see cref="KlangDienst.SchleifeStarten"/> ohne Einblenden, an zufälliger Stelle; die Pegel setzt er jedes Bild
///     (<see cref="KlangDienst.SchleifePegelSetzen"/>, nur bei Änderung über 0,1 dB). Stumme Schichten werden nach 10 s angehalten.
///   • Stadtschichten auf <c>UmgebungStadt</c> (Ferne beim Herauszoomen), alles andere auf <c>UmgebungDraussen</c> (Räume, Dom, Pause).
///   • Rufe über zwei feste Spieler auf <c>UmgebungRufNah</c>/<c>UmgebungRufFern</c> (Seite über den Panner des Busses, fern mit Tiefpass 2,5 kHz) –
///     unabhängig von Kamera, Raumkamera und freier Kamera.
/// Startoptionen: <c>--umgebungtest=2</c> schreibt je Sekunde die Pegel und jeden Ruf ins Log; <c>--umgebungfahrt=1</c> fährt die feste Hörprobe
/// (<see cref="FahrtPunkte"/>) und beendet das Spiel danach (mit <c>--write-movie</c> wird daraus eine Aufnahme).
/// </summary>
public partial class KlangUmgebung : Node
{
    public const string Ordner = "res://assets/audio/umgebung/";
    private const string Praefix = "umgebung:";

    /// <summary>Die laufende Umgebung (für den Entwicklerreiter), sonst null.</summary>
    public static KlangUmgebung? Aktiv { get; private set; }

    /// <summary>Liefert die Eingaben dieses Bildes oder null, solange die Partie noch nicht bereit ist (Ladebildschirm).</summary>
    public Func<M.Eingaben?>? Quelle;
    /// <summary>Nur für die Hörprobe: Uhr (Stunde) und Zoom der Stadtansicht vorgeben.</summary>
    public Action<float>? UhrVorgeben, ZoomVorgeben;

    private readonly M.Zustand _zustand = new();
    private M.RufPlaner _planer = new((int)(Time.GetTicksUsec() & 0x7fffffff)); // eigener Zufall, nie der der Simulation (Lockstep)
    private readonly Dictionary<string, AudioStream> _toene = new();
    private readonly List<(string Name, string Pfad)> _ausstehend = new();
    private readonly float[] _gesetzt = Enumerable.Repeat(float.NaN, M.Schichten.Length).ToArray();
    private readonly double[] _stummSeit = new double[M.Schichten.Length];
    private readonly bool[] _angehalten = new bool[M.Schichten.Length];
    private readonly List<M.Ruf> _neueRufe = new();
    private AudioStreamPlayer _rufNah = null!, _rufFern = null!;
    private AudioEffectPanner? _panNah, _panFern;
    private AudioEffectLowPassFilter? _tiefpassDraussen, _tiefpassStadt;
    private int _busDraussen = -1, _busStadt = -1;
    private float _draussenDb = float.NaN, _draussenTp = float.NaN, _stadtDb = float.NaN, _stadtTp = float.NaN;
    private bool _laeuft, _beendet;
    private int _logStufe;
    private bool _fahrt;
    private double _zeit, _logUhr, _fahrtZeit;
    private int _fahrtRuf;

    /// <summary>Die Eingaben und das Ergebnis des letzten Bildes (Entwicklerreiter).</summary>
    public M.Eingaben LetzteEingaben { get; private set; }
    public M.Ausgabe Letzte => _zustand.Aus;
    public M.RufPlaner Planer => _planer;
    public bool Laeuft => _laeuft;
    /// <summary>Der letzte Ruf als Text („Hahn 2, fern, links“) für den Entwicklerreiter.</summary>
    public string LetzterRuf { get; private set; } = "–";

    public static string SchichtPfad(string name) => Ordner + "schichten/" + name + ".ogg";
    public static string RufPfad(string datei) => Ordner + "rufe/" + datei + ".ogg";

    /// <summary>Alle Ruf-Dateien je Art (nah und fern) als Namen ohne Endung.</summary>
    public static IEnumerable<string> RufDateien(M.Rufart r)
    {
        for (var v = 1; v <= r.Varianten; v++) yield return $"{r.Datei}_{v}";
        if (r.FernDatei is not null)
            for (var v = 1; v <= r.FernVarianten; v++) yield return $"{r.FernDatei}_{v}";
    }

    public override void _Ready()
    {
        Aktiv = this;
        ProcessMode = ProcessModeEnum.Always; // die Pause der Partie ist der Takt, nicht der Baum – sicher ist sicher
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--umgebungtest=2") _logStufe = 2;
            if (arg == "--umgebungfahrt=1") _fahrt = true;
        }
        if (_fahrt)
        {
            _logStufe = 2;
            // Hörprobe: Ton nicht von Fokus, Ansagen oder gemerkten Reglern abhängig machen (nichts wird gespeichert)
            KlangDienst.HintergrundStummSetzen(false, speichern: false);
            KlangDienst.AnsagenSetzen(false, speichern: false);
            for (var i = 0; i < KlangDienst.Regelbusse.Length; i++)
            {
                KlangDienst.ProzentSetzen(i, 80, speichern: false);
                KlangDienst.StummSetzen(i, false, speichern: false);
            }
        }
        // Alle Dateien im Hintergrund laden (ResourceLoader-Faden): während des Ladebildschirms, nie erst beim Rufen
        foreach (var s in M.Schichten) Anfordern(s.Name, SchichtPfad(s.Name));
        foreach (var r in M.Rufarten)
            foreach (var d in RufDateien(r)) Anfordern(d, RufPfad(d));
        _rufNah = new AudioStreamPlayer { Name = "RufNah", Bus = KlangDienst.BusUmgebungRufNah };
        _rufFern = new AudioStreamPlayer { Name = "RufFern", Bus = KlangDienst.BusUmgebungRufFern };
        AddChild(_rufNah);
        AddChild(_rufFern);
        _busDraussen = AudioServer.GetBusIndex(KlangDienst.BusUmgebungDraussen);
        _busStadt = AudioServer.GetBusIndex(KlangDienst.BusUmgebungStadt);
        _tiefpassDraussen = _busDraussen >= 0 ? AudioServer.GetBusEffect(_busDraussen, 0) as AudioEffectLowPassFilter : null;
        _tiefpassStadt = _busStadt >= 0 ? AudioServer.GetBusEffect(_busStadt, 0) as AudioEffectLowPassFilter : null;
        var nah = AudioServer.GetBusIndex(KlangDienst.BusUmgebungRufNah);
        var fern = AudioServer.GetBusIndex(KlangDienst.BusUmgebungRufFern);
        _panNah = nah >= 0 ? AudioServer.GetBusEffect(nah, 0) as AudioEffectPanner : null;
        _panFern = fern >= 0 ? AudioServer.GetBusEffect(fern, 0) as AudioEffectPanner : null;
        _planer.Laenge = name => _toene.TryGetValue(name, out var t) ? t.GetLength() : 3.0;
        if (_fahrt) _planer = new M.RufPlaner(4711) { Laenge = name => _toene.TryGetValue(name, out var t) ? t.GetLength() : 3.0 };
    }

    private void Anfordern(string name, string pfad)
    {
        if (!ResourceLoader.Exists(pfad))
        {
            GD.PushWarning($"KlangUmgebung: {pfad} fehlt.");
            return;
        }
        ResourceLoader.LoadThreadedRequest(pfad, "AudioStream");
        _ausstehend.Add((name, pfad));
    }

    /// <summary>Blendet alles aus (vor einem Szenenwechsel, damit der Ton nicht mit dem letzten Pegel über das Laden hinweg steht).</summary>
    public void Ausblenden(float sekunden)
    {
        if (_beendet) return;
        _beendet = true;
        foreach (var s in M.Schichten) KlangDienst.SchleifeStoppen(Praefix + s.Name, sekunden);
        _rufNah.Stop();
        _rufFern.Stop();
        _laeuft = false;
        if (_logStufe >= 2) GD.Print($"UMGEBUNG Ende: alle Schleifen blenden in {sekunden:0.0} s aus");
    }

    public override void _ExitTree()
    {
        Ausblenden(0.8f); // die Schleifen hängen am KlangDienst (Autoload): die Blende läuft über den Szenenwechsel hinaus weiter
        if (Aktiv == this) Aktiv = null;
    }

    public override void _Process(double delta)
    {
        if (_beendet) return;
        for (var i = _ausstehend.Count - 1; i >= 0; i--)
        {
            var (name, pfad) = _ausstehend[i];
            var stand = ResourceLoader.LoadThreadedGetStatus(pfad);
            if (stand == ResourceLoader.ThreadLoadStatus.InProgress) continue;
            if (stand == ResourceLoader.ThreadLoadStatus.Loaded && ResourceLoader.LoadThreadedGet(pfad) is AudioStream ton) _toene[name] = ton;
            else GD.PushWarning($"KlangUmgebung: {pfad} lässt sich nicht laden ({stand}).");
            _ausstehend.RemoveAt(i);
        }
        if (_ausstehend.Count > 0 || Quelle?.Invoke() is not { } eingaben) return;
        if (!_laeuft) Starten();
        _zeit += delta;
        if (_fahrt) eingaben = FahrtSchritt(eingaben, delta);
        LetzteEingaben = eingaben;
        var aus = _zustand.Schritt(eingaben, delta);
        Anwenden(aus, delta);
        _neueRufe.Clear();
        if (!_fahrt || _fahrtZeit >= FahrtZufallAb) _planer.Schritt(eingaben, aus, delta, _neueRufe);
        foreach (var ruf in _neueRufe) Abspielen(ruf);
        if (_logStufe >= 2 && _zeit >= _logUhr)
        {
            _logUhr = Math.Floor(_zeit) + 1;
            GD.Print(LogZeile(eingaben, aus));
        }
    }

    private void Starten()
    {
        _laeuft = true;
        foreach (var s in M.Schichten)
            if (_toene.TryGetValue(s.Name, out var ton))
                KlangDienst.SchleifeStarten(Praefix + s.Name, ton, s.Bus == M.Bus.Stadt ? KlangDienst.BusUmgebungStadt : KlangDienst.BusUmgebungDraussen,
                                            einblendSekunden: 0, db: KlangDienst.MindestDb, zufallsStart: !_fahrt);
        GD.Print($"UMGEBUNG Start: {M.Schichten.Count(s => _toene.ContainsKey(s.Name))} Schleifen, {_toene.Count - M.Schichten.Length} Rufdateien geladen" +
                 (KlangDienst.BusseTesten() ? ", Unterbusse in Ordnung" : ", Unterbusse FEHLERHAFT (siehe Warnung)"));
    }

    private void Anwenden(M.Ausgabe aus, double delta)
    {
        for (var i = 0; i < M.Schichten.Length; i++)
        {
            var name = Praefix + M.Schichten[i].Name;
            var db = aus.Db[i];
            if (float.IsNaN(_gesetzt[i]) || Math.Abs(db - _gesetzt[i]) > 0.1f)
            {
                KlangDienst.SchleifePegelSetzen(name, db);
                _gesetzt[i] = db;
            }
            // Leistung: eine Schicht, die länger als 10 s stumm ist, wird angehalten (nachts und oben nicht 9 Ströme entschlüsseln)
            _stummSeit[i] = db <= M.AusDb && aus.Ziel[i] <= 0.001f ? _stummSeit[i] + delta : 0;
            var anhalten = _stummSeit[i] > 10;
            if (anhalten != _angehalten[i])
            {
                _angehalten[i] = anhalten;
                KlangDienst.SchleifePausieren(name, anhalten);
            }
        }
        if (_busDraussen >= 0 && (float.IsNaN(_draussenDb) || Math.Abs(aus.DraussenDb - _draussenDb) > 0.05f))
            AudioServer.SetBusVolumeDb(_busDraussen, _draussenDb = aus.DraussenDb);
        if (_busStadt >= 0 && (float.IsNaN(_stadtDb) || Math.Abs(aus.StadtDb - _stadtDb) > 0.05f))
            AudioServer.SetBusVolumeDb(_busStadt, _stadtDb = aus.StadtDb);
        if (_tiefpassDraussen is not null && (float.IsNaN(_draussenTp) || Math.Abs(aus.DraussenTiefpass / _draussenTp - 1) > 0.01f))
            _tiefpassDraussen.CutoffHz = _draussenTp = aus.DraussenTiefpass;
        if (_tiefpassStadt is not null && (float.IsNaN(_stadtTp) || Math.Abs(aus.StadtTiefpass / _stadtTp - 1) > 0.01f))
            _tiefpassStadt.CutoffHz = _stadtTp = aus.StadtTiefpass;
    }

    private void Abspielen(M.Ruf ruf)
    {
        if (!_toene.TryGetValue(ruf.Datei, out var ton)) return;
        var spieler = ruf.Fern ? _rufFern : _rufNah;
        var panner = ruf.Fern ? _panFern : _panNah;
        if (panner is not null) panner.Pan = ruf.Seite;
        spieler.Stream = ton;
        spieler.VolumeDb = Math.Min(ruf.Db, 0f);
        spieler.PitchScale = ruf.Tonhoehe;
        spieler.Play();
        var art = M.Rufarten[ruf.Art];
        LetzterRuf = $"{art.Name}: {ruf.Datei}, {(ruf.Fern ? "fern" : "nah")}, Seite {ruf.Seite:+0.00;-0.00}, {ruf.Db:0.0} dB (bei {_zeit:0} s)";
        if (_logStufe >= 2)
            GD.Print($"UMGEBUNG RUF t={_zeit:0.00} art={art.Name} datei={ruf.Datei} {(ruf.Fern ? "fern" : "nah")} seite={ruf.Seite:0.00} db={ruf.Db:0.0} " +
                     $"tonhoehe={ruf.Tonhoehe:0.000} laenge={ton.GetLength():0.00} uhr={UhrText(LetzteEingaben.UhrMinute)} b={LetzteEingaben.Bezug:0.00} S={LetzteEingaben.Zoom:0} pause={(LetzteEingaben.Pausiert ? 1 : 0)}");
    }

    /// <summary>Einen Ruf dieser Art jetzt (Entwicklerreiter): nah, außer die Art kennt nur fern oder die Zoomstufe ist schon fern.</summary>
    public void JetztRufen(int art)
    {
        var r = M.Rufarten[art];
        var fern = r.FernAb <= 0 || LetzteEingaben.Zoom >= r.FernAb;
        Abspielen(_planer.Erzwingen(art, fern, LetzteEingaben.Zoom));
    }

    private static string UhrText(float minute) => $"{(int)(minute / 60):00}:{(int)(minute % 60):00}";

    private string LogZeile(in M.Eingaben e, M.Ausgabe a) =>
        $"UMGEBUNG t={_zeit:0} uhr={UhrText(e.UhrMinute)} b={e.Bezug:0.00} jz={e.Jahreszeit} S={e.Zoom:0} Smax={e.ZoomGroesst:0} N={a.Naehe:0.00} H={a.Hoehe:0.00} " +
        $"innen={e.Innen} pause={(e.Pausiert ? 1 : 0)} tps={e.TicksProSekunde:0.0} sprung={(a.Sprungblende ? 1 : 0)} " +
        $"stadt={a.StadtDb:0.0}dB/{a.StadtTiefpass:0}Hz draussen={a.DraussenDb:0.0}dB/{a.DraussenTiefpass:0}Hz schleifen={KlangDienst.SchleifenAnzahl.Benannt} | " +
        string.Join(" ", M.Schichten.Select((s, i) => $"{s.Name}={(a.Db[i] <= M.AusDb ? "aus" : a.Db[i].ToString("0.0"))}{(_angehalten[i] ? "(angehalten)" : "")}"));

    // ── Hörprobe (--umgebungfahrt=1) ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Die feste Fahrt der Hörprobe: (Sekunde, Uhr in Stunden, Zoom; −1 = Höchstzoom der Karte). Dazwischen linear. Danach mittags am Startzoom
    /// mit zufälligen Rufen (fester Zufall), dann Pause. Dieselben Kurven wie im Spiel, nur Uhr und Zoom sind vorgegeben (Frühling).
    /// </summary>
    public static readonly (float T, float Stunde, float Zoom)[] FahrtPunkte =
    {
        (0, 5.75f, 30), (14, 6.25f, 30),          // Morgen nah: Vogelchor, Hahn, die Stadt erwacht
        (28, 7.5f, -1), (40, 12f, -1),            // herauszoomen bis ganz oben: Wind leicht und mittel, die Stadt fern und dumpf
        (50, 12.5f, 45), (54, 17.6f, 45),         // herein, Mittag nah: Stadtleben, Gemurmel; dann Abendrot (Abendgesang)
        (62, 20.6f, 60), (74, 22f, 60), (84, 22.8f, 60), // Abend: Schenkengemurmel, Amsel, Hund; Nacht: Kauz, ferner Hund
        (84.01f, 12f, 95), (234, 12f, 95), (324, 12f, 95), // Sprung auf Mittag am Startzoom (Sprungblende), 150 s Stadtleben, dann 90 s Pause
    };
    public const float FahrtZufallAb = 84f, FahrtPauseAb = 234f, FahrtEnde = 324f;

    /// <summary>Feste Rufe der Fahrt (Sekunde, Art, fern).</summary>
    public static readonly (float T, string Art, bool Fern)[] FahrtRufe =
    {
        (3, "Hahn", false), (8, "Passant", false), (11, "Hahn", false), (45, "Handwerk", true), (51, "Passant", false), (52.5f, "Gefluegel", false),
        (57, "Amsel", false), (60, "Hund", true), (66, "Passant", false), (71, "Kauz", false), (78, "Hund", true), (81, "Kauz", false),
    };

    private M.Eingaben FahrtSchritt(M.Eingaben e, double delta)
    {
        _fahrtZeit += delta;
        if (_fahrtZeit >= FahrtEnde)
        {
            GD.Print("UMGEBUNG Fahrt fertig");
            GetTree().Quit();
            return e;
        }
        var t = (float)_fahrtZeit;
        var k = 0;
        while (k < FahrtPunkte.Length - 2 && FahrtPunkte[k + 1].T <= t) k++;
        var (t0, u0, z0) = FahrtPunkte[k];
        var (t1, u1, z1) = FahrtPunkte[k + 1];
        var f = Math.Clamp((t - t0) / Math.Max(t1 - t0, 0.001f), 0f, 1f);
        float Zoom(float z) => z < 0 ? e.ZoomGroesst : z;
        var stunde = u0 + (u1 - u0) * f;
        var zoom = Zoom(z0) + (Zoom(z1) - Zoom(z0)) * f;
        UhrVorgeben?.Invoke(stunde);
        ZoomVorgeben?.Invoke(zoom);
        while (_fahrtRuf < FahrtRufe.Length && FahrtRufe[_fahrtRuf].T <= t)
        {
            var (_, art, fern) = FahrtRufe[_fahrtRuf++];
            Abspielen(_planer.Erzwingen(Array.FindIndex(M.Rufarten, r => r.Name == art), fern, e.Zoom));
        }
        return e with { UhrMinute = stunde * 60f, Zoom = zoom, Pausiert = t >= FahrtPauseAb, Jahreszeit = Jahreszeit.Fruehling };
    }
}
