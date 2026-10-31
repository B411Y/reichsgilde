using System.Text.Json;
using Godot;
using Reichsgilde.Core.Zeit;
using M = Reichsgilde.KlangUmgebungModell;

namespace Reichsgilde;

/// <summary>
/// Die <b>Umgebungsklänge der Stadt</b> (Ton Schritt 4): Schleifen (Stadtleben, Abend, Vögel, Nacht, Wind, Laub, Grillen, Frösche, Fluss, Regen,
/// Schankstube) und Einzelrufe (Hahn, Hund, Kauz, Amsel, Pferd, Geflügel, Passanten, fernes Handwerk, Krähen, Gesprächsfetzen an Gruppen, Horn
/// der Streife, Donner), dazu die Glocken vom Dom. Nur in der Partie (Kind der <c>Hauptszene</c>, auch in der Probepartie), nicht im
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
    /// <summary>
    /// Füllt das Momentbild der Menschen in der Stadt (Stadtansicht.Klang.cs); gibt false zurück, wenn es keines gibt (dann gilt „alles voll“ wie
    /// früher). <see cref="KlangStadtlebenModell"/> rechnet daraus mit dem Zoom die Pegel und Orte.
    /// </summary>
    public Func<KlangStadtlebenModell.Momentbild, bool>? LebenQuelle;
    /// <summary>Nur für die Hörprobe: Uhr (Stunde) und Zoom der Stadtansicht vorgeben.</summary>
    public Action<float>? UhrVorgeben, ZoomVorgeben;
    /// <summary>Nur für die Hörprobe: Wetterlage (0 klar, 1 Regen, 2 Gewitter, 3 Schnee), Jahreszeit und Blickpunkt der Kamera vorgeben.</summary>
    public Action<int, Jahreszeit>? WetterVorgeben;
    public Action<float, float>? FokusVorgeben;
    /// <summary>
    /// Die Umgebung um den Blickpunkt (Stadt gegen Natur, <see cref="KlangUmweltModell"/>) für den Umkreis des Zooms; null ohne Karte. Wird auch in
    /// der Hörprobe mit der echten Karte benutzt, damit der Weg von der Stadt ins Land echt klingt.
    /// </summary>
    public Func<float, KlangUmweltModell.Lage?>? UmweltQuelle;
    /// <summary>Der Blickpunkt der Kamera am Boden (Meter), für die Hörprobe.</summary>
    public Func<(float X, float Z)>? FokusQuelle;
    /// <summary>Zeit seit dem letzten Blitz (Sekunden; negativ = gerade keiner): Die steigende Flanke löst den Donner aus.</summary>
    public Func<float>? BlitzQuelle;
    /// <summary>Abstand des laufenden Blitzes in Kilometern (derselbe Wurf wie die Helligkeit im Bild) und der Fortschritt des Gewitters (−1 = keines).</summary>
    public Func<float>? BlitzAbstandQuelle, GewitterQuelle;
    /// <summary>Der laufende Blitz ist ein direkter Einschlag: Abstand (km) und Seite im Bild, sonst null.</summary>
    public Func<(float Km, float Seite)?>? EinschlagQuelle;
    /// <summary>Nur für die Hörprobe: Fortschritt des Gewitters vorgeben (null = aus dem Wetter) und einen Blitz mit diesem Abstand (km) auslösen.</summary>
    public Action<float?>? GewitterVorgeben;
    public Action<float>? BlitzVorgeben;
    /// <summary>Nur für die Hörprobe: einen direkten Blitzeinschlag am Marktplatz auslösen.</summary>
    public Action? EinschlagVorgeben;

    private readonly M.Zustand _zustand = new();
    private readonly KlangStadtlebenModell.Momentbild _momentbild = new();
    private readonly KlangStadtlebenModell.Zustand _leben = new();
    private M.RufPlaner _planer = new((int)(Time.GetTicksUsec() & 0x7fffffff)); // eigener Zufall, nie der der Simulation (Lockstep)
    private readonly Dictionary<string, AudioStream> _toene = new();
    private readonly List<(string Name, string Pfad)> _ausstehend = new();
    private readonly float[] _gesetzt = Enumerable.Repeat(float.NaN, M.Schichten.Length).ToArray();
    private readonly double[] _stummSeit = new double[M.Schichten.Length];
    private readonly bool[] _angehalten = new bool[M.Schichten.Length];
    private readonly List<M.Ruf> _neueRufe = new();
    private readonly M.GlockenPlaner _glocken = new((int)((Time.GetTicksUsec() >> 3) & 0x7fffffff));
    private readonly List<M.Glocke> _neueGlocken = new();
    private AudioStreamPlayer _rufNah = null!, _rufFern = null!, _rufGespraech = null!, _glockenSpieler = null!;
    private AudioEffectPanner? _panNah, _panFern, _panSchenke, _panGespraech, _panGlocke, _panDonner;
    private AudioEffectLowPassFilter? _tiefpassDraussen, _tiefpassStadt, _tiefpassSchenke, _tiefpassGespraech, _tiefpassGlocke, _tiefpassDonner;
    private int _busDraussen = -1, _busStadt = -1, _busSchenke = -1, _busInnen = -1;
    private float _draussenDb = float.NaN, _draussenTp = float.NaN, _stadtDb = float.NaN, _stadtTp = float.NaN;
    private float _schenkeDb = float.NaN, _schenkeSeite = float.NaN, _schenkeTp = float.NaN, _innenDb = float.NaN;
    private float _blitzVorher = -1;
    private bool _laeuft, _beendet;
    private int _logStufe;
    private bool _fahrt, _kurzeFahrt, _gewitterFahrt, _einschlagFahrt, _einschlagGeschehen;
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
    public static string GlockenPfad(string datei) => Ordner + "glocken/" + datei + ".ogg";

    /// <summary>Alle Ruf-Dateien je Art (nah und fern) als Namen ohne Endung; bei den Gesprächen die Liste aus <c>game/daten/gespraeche.json</c>.</summary>
    public static IEnumerable<string> RufDateien(M.Rufart r)
    {
        if (r.Bedarf == M.RufBedarf.Gruppe)
        {
            foreach (var d in M.GespraechsDateien()) yield return d;
            yield break;
        }
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
            if (arg == "--umgebungfahrt=2") (_fahrt, _kurzeFahrt) = (true, true);
            if (arg == "--umgebungfahrt=3") (_fahrt, _gewitterFahrt) = (true, true);
            if (arg == "--umgebungfahrt=4") (_fahrt, _einschlagFahrt) = (true, true);
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
        GespraecheLaden(); // die austauschbare Liste der Gesprächsfetzen, vor dem Anfordern der Dateien
        // Alle Dateien im Hintergrund laden (ResourceLoader-Faden): während des Ladebildschirms, nie erst beim Rufen
        foreach (var s in M.Schichten) Anfordern(s.Name, SchichtPfad(s.Name));
        foreach (var r in M.Rufarten)
            foreach (var d in RufDateien(r)) Anfordern(d, RufPfad(d));
        foreach (var g in M.Glockenarten)
            for (var v = 1; v <= g.Varianten; v++) Anfordern($"{g.Datei}_{v}", GlockenPfad($"{g.Datei}_{v}"));
        _rufNah = new AudioStreamPlayer { Name = "RufNah", Bus = KlangDienst.BusUmgebungRufNah };
        _rufFern = new AudioStreamPlayer { Name = "RufFern", Bus = KlangDienst.BusUmgebungRufFern };
        _rufGespraech = new AudioStreamPlayer { Name = "RufGespraech", Bus = KlangDienst.BusUmgebungGespraech };
        _glockenSpieler = new AudioStreamPlayer { Name = "Glocke", Bus = KlangDienst.BusUmgebungGlocke };
        AddChild(_rufNah);
        AddChild(_rufFern);
        AddChild(_rufGespraech);
        AddChild(_glockenSpieler);
        _busDraussen = AudioServer.GetBusIndex(KlangDienst.BusUmgebungDraussen);
        _busStadt = AudioServer.GetBusIndex(KlangDienst.BusUmgebungStadt);
        _tiefpassDraussen = _busDraussen >= 0 ? AudioServer.GetBusEffect(_busDraussen, 0) as AudioEffectLowPassFilter : null;
        _tiefpassStadt = _busStadt >= 0 ? AudioServer.GetBusEffect(_busStadt, 0) as AudioEffectLowPassFilter : null;
        var nah = AudioServer.GetBusIndex(KlangDienst.BusUmgebungRufNah);
        var fern = AudioServer.GetBusIndex(KlangDienst.BusUmgebungRufFern);
        _panNah = nah >= 0 ? AudioServer.GetBusEffect(nah, 0) as AudioEffectPanner : null;
        _panFern = fern >= 0 ? AudioServer.GetBusEffect(fern, 0) as AudioEffectPanner : null;
        _busSchenke = AudioServer.GetBusIndex(KlangDienst.BusUmgebungSchenke);
        _panSchenke = _busSchenke >= 0 ? AudioServer.GetBusEffect(_busSchenke, 0) as AudioEffectPanner : null;
        _tiefpassSchenke = _busSchenke >= 0 ? AudioServer.GetBusEffect(_busSchenke, 1) as AudioEffectLowPassFilter : null;
        var gesp = AudioServer.GetBusIndex(KlangDienst.BusUmgebungGespraech);
        var glocke = AudioServer.GetBusIndex(KlangDienst.BusUmgebungGlocke);
        _panGespraech = gesp >= 0 ? AudioServer.GetBusEffect(gesp, 0) as AudioEffectPanner : null;
        _tiefpassGespraech = gesp >= 0 ? AudioServer.GetBusEffect(gesp, 1) as AudioEffectLowPassFilter : null;
        _panGlocke = glocke >= 0 ? AudioServer.GetBusEffect(glocke, 0) as AudioEffectPanner : null;
        _tiefpassGlocke = glocke >= 0 ? AudioServer.GetBusEffect(glocke, 1) as AudioEffectLowPassFilter : null;
        _busInnen = AudioServer.GetBusIndex(KlangDienst.BusUmgebungInnen);
        var donner = AudioServer.GetBusIndex(KlangDienst.BusUmgebungDonner);
        _panDonner = donner >= 0 ? AudioServer.GetBusEffect(donner, 0) as AudioEffectPanner : null;
        _tiefpassDonner = donner >= 0 ? AudioServer.GetBusEffect(donner, 1) as AudioEffectLowPassFilter : null;
        _planer.Laenge = name => _toene.TryGetValue(name, out var t) ? t.GetLength() : 3.0;
        if (_fahrt) _planer = new M.RufPlaner(4711) { Laenge = name => _toene.TryGetValue(name, out var t) ? t.GetLength() : 3.0 };
    }

    /// <summary>
    /// Liest die Liste der Gesprächsfetzen aus <c>res://daten/gespraeche.json</c> (Anlass → Dateien unter <c>rufe/</c>, ohne Endung). Die Datei
    /// ersetzt die Voreinstellung des Modells; Einträge ohne Aufnahme werden übersprungen. So lassen sich die freien Aufnahmen später durch Gespräche der
    /// Stimm-KI ersetzen, ohne den Code anzufassen.
    /// </summary>
    private static void GespraecheLaden()
    {
        const string pfad = "res://daten/gespraeche.json";
        if (!Godot.FileAccess.FileExists(pfad)) return;
        try
        {
            using var f = Godot.FileAccess.Open(pfad, Godot.FileAccess.ModeFlags.Read);
            using var doc = JsonDocument.Parse(f.GetAsText());
            if (!doc.RootElement.TryGetProperty("anlaesse", out var anlaesse)) return;
            var neu = new Dictionary<string, string[]>();
            foreach (var a in anlaesse.EnumerateObject())
            {
                var liste = a.Value.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "" && ResourceLoader.Exists(RufPfad(x))).ToArray();
                if (liste.Length > 0) neu[a.Name] = liste;
            }
            if (neu.Count > 0) M.Gespraeche = neu;
        }
        catch (Exception e)
        {
            GD.PushWarning($"KlangUmgebung: {pfad} nicht lesbar ({e.Message}) – es gilt die Voreinstellung.");
        }
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
        _rufGespraech.Stop();
        _glockenSpieler.Stop();
        _laeuft = false;
        if (_logStufe >= 2) GD.Print($"UMGEBUNG Ende: alle Schleifen blenden in {sekunden:0.0} s aus");
    }

    public override void _ExitTree()
    {
        Ausblenden(0.8f); // die Schleifen hängen am KlangDienst (Autoload): die Blende läuft über den Szenenwechsel hinaus weiter
        if (Aktiv == this) Aktiv = null;
        Freigeben();
    }

    /// <summary>
    /// Gibt die verwalteten Verweise auf Godot-Ressourcen frei (geladene Klänge, Effekte der Busse): Im exportierten Spiel räumt beim Beenden niemand die C#-Hüllen
    /// auf, und Godot meldet sonst „resources still in use at exit“ (der Rauchtest der CI wertet das als Fehler). Die Klänge, die noch ausblenden, gehören nativ
    /// den Spielern des <see cref="KlangDienst"/> und bleiben davon unberührt.
    /// </summary>
    private void Freigeben()
    {
        foreach (var ton in _toene.Values) ton.Dispose();
        _toene.Clear();
        // Ladeaufträge, die beim Beenden noch laufen, abwarten und das Ergebnis loslassen: Sonst bleiben die Klänge beim Abbau „in Benutzung“.
        foreach (var (_, pfad) in _ausstehend)
            ResourceLoader.LoadThreadedGet(pfad)?.Dispose();
        _ausstehend.Clear();
        foreach (var effekt in new AudioEffect?[] { _panNah, _panFern, _panSchenke, _panGespraech, _panGlocke, _panDonner, _tiefpassDraussen, _tiefpassStadt, _tiefpassSchenke, _tiefpassGespraech, _tiefpassGlocke, _tiefpassDonner })
            effekt?.Dispose();
        (_panNah, _panFern, _panSchenke, _panGespraech, _panGlocke, _panDonner) = (null, null, null, null, null, null);
        (_tiefpassDraussen, _tiefpassStadt, _tiefpassSchenke, _tiefpassGespraech, _tiefpassGlocke, _tiefpassDonner) = (null, null, null, null, null, null);
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
        // Gewitter: zieht es heran, wird der Regen stärker und es kommen Böen (nur der Ton, Wetterdaten werden nur gelesen)
        if (GewitterQuelle?.Invoke() is { } fortschritt and >= 0f)
            eingaben = eingaben with { Wetter = M.GewitterWetter(eingaben.Himmel, fortschritt, _zeit) };
        // Die Menschen der Stadt: Momentbild → Lage (weich geglättet). Ohne Quelle gilt „alles voll“ (Probepartie ohne Stadtansicht)
        if (_fahrt ? FahrtMomentbild(eingaben) : LebenQuelle?.Invoke(_momentbild) == true)
            eingaben = eingaben with { Leben = _leben.Schritt(_momentbild, eingaben.Innen == M.Innenart.Draussen ? eingaben.Zoom : 60f, delta) };
        LetzteEingaben = eingaben;
        var aus = _zustand.Schritt(eingaben, delta);
        Anwenden(aus, delta);
        _neueRufe.Clear();
        if (!_fahrt || _fahrtZeit >= FahrtZufallAb) _planer.Schritt(eingaben, aus, delta, _neueRufe);
        foreach (var ruf in _neueRufe) Abspielen(ruf);
        // Ein Blitz (steigende Flanke der Blitzzeit) → der Donner folgt mit der Verzögerung des Schalls
        if (BlitzQuelle?.Invoke() is { } blitz)
        {
            if (blitz >= 0 && _blitzVorher < 0 && !eingaben.Hintergrund)
            {
                var km = BlitzAbstandQuelle?.Invoke() ?? -1f;
                var einschlag = EinschlagQuelle?.Invoke();
                var verzug = einschlag is { } ein ? _planer.EinschlagMelden(ein.Km, ein.Seite) : _planer.BlitzMelden(km);
                if (einschlag is { } e2) km = e2.Km;
                if (_logStufe >= 2) GD.Print($"UMGEBUNG BLITZ t={_zeit:0.00} km={km:0.00} {(einschlag is not null ? "EINSCHLAG " : "")}verzoegerung={(verzug < 0 ? "kein Donner (Wetterleuchten)" : verzug.ToString("0.0") + " s")} gewitter={(GewitterQuelle?.Invoke() ?? -1f):0.00}");
            }
            _blitzVorher = blitz;
        }
        // Glocken vom Dom: zu den Zeiten der Regeln (Arbeitsbeginn, Messe, Feierabend, Schankschluss)
        _neueGlocken.Clear();
        if (!_fahrt || _fahrtZeit >= FahrtZufallAb) _glocken.Schritt(eingaben, aus, _neueGlocken);
        foreach (var g in _neueGlocken) GlockeSpielen(g);
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
                KlangDienst.SchleifeStarten(Praefix + s.Name, ton, BusName(s.Bus),
                                            einblendSekunden: 0, db: KlangDienst.MindestDb, zufallsStart: !_fahrt);
        GD.Print($"UMGEBUNG Start: {M.Schichten.Count(s => _toene.ContainsKey(s.Name))} Schleifen, {_toene.Count - M.Schichten.Length} Rufdateien geladen" +
                 (KlangDienst.BusseTesten() ? ", Unterbusse in Ordnung" : ", Unterbusse FEHLERHAFT (siehe Warnung)"));
    }

    private static string BusName(M.Bus bus) => bus switch
    {
        M.Bus.Stadt => KlangDienst.BusUmgebungStadt,
        M.Bus.Schenke => KlangDienst.BusUmgebungSchenke,
        M.Bus.Innen => KlangDienst.BusUmgebungInnen,
        _ => KlangDienst.BusUmgebungDraussen,
    };

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
        if (_panSchenke is not null && (float.IsNaN(_schenkeSeite) || Math.Abs(aus.SchenkeSeite - _schenkeSeite) > 0.005f))
            _panSchenke.Pan = _schenkeSeite = aus.SchenkeSeite;
        if (_busSchenke >= 0 && (float.IsNaN(_schenkeDb) || Math.Abs(aus.SchenkeDb - _schenkeDb) > 0.05f))
            AudioServer.SetBusVolumeDb(_busSchenke, _schenkeDb = aus.SchenkeDb);
        if (_tiefpassSchenke is not null && (float.IsNaN(_schenkeTp) || Math.Abs(aus.SchenkeTiefpass / _schenkeTp - 1) > 0.02f))
            _tiefpassSchenke.CutoffHz = _schenkeTp = aus.SchenkeTiefpass;
        if (_busInnen >= 0 && (float.IsNaN(_innenDb) || Math.Abs(aus.InnenDb - _innenDb) > 0.05f))
            AudioServer.SetBusVolumeDb(_busInnen, _innenDb = aus.InnenDb);
        if (_tiefpassDraussen is not null && (float.IsNaN(_draussenTp) || Math.Abs(aus.DraussenTiefpass / _draussenTp - 1) > 0.01f))
            _tiefpassDraussen.CutoffHz = _draussenTp = aus.DraussenTiefpass;
        if (_tiefpassStadt is not null && (float.IsNaN(_stadtTp) || Math.Abs(aus.StadtTiefpass / _stadtTp - 1) > 0.01f))
            _tiefpassStadt.CutoffHz = _stadtTp = aus.StadtTiefpass;
    }

    private void Abspielen(M.Ruf ruf)
    {
        if (!_toene.TryGetValue(ruf.Datei, out var ton)) return;
        var spieler = ruf.Bahn switch { 2 => _rufGespraech, 1 => _rufFern, _ => _rufNah };
        var panner = ruf.Bahn switch { 2 => _panGespraech, 1 => _panFern, _ => _panNah };
        if (panner is not null) panner.Pan = ruf.Seite;
        if (ruf.Bahn == 2 && _tiefpassGespraech is not null && ruf.Tiefpass > 0) _tiefpassGespraech.CutoffHz = ruf.Tiefpass; // dumpfer mit dem Abstand der Gruppe
        if (M.Rufarten[ruf.Art].Bedarf == M.RufBedarf.Donner)
        {
            // Donner: ein eigener Spieler je Schlag, damit sich Grollen überlagern dürfen (Bus und Seite wie die Rufe)
            if (_panDonner is not null) _panDonner.Pan = ruf.Seite;
            if (_tiefpassDonner is not null && ruf.Tiefpass > 0) _tiefpassDonner.CutoffHz = ruf.Tiefpass; // die Luft schluckt die Höhen: fern dumpf
            var d = new AudioStreamPlayer { Stream = ton, Bus = KlangDienst.BusUmgebungDonner, VolumeDb = M.AbspielDb(ruf), PitchScale = ruf.Tonhoehe };
            d.Finished += d.QueueFree;
            AddChild(d);
            d.Play();
        }
        else
        {
            spieler.Stream = ton;
            spieler.VolumeDb = M.AbspielDb(ruf); // mit Ausgleich für den Panner (sonst ist ein Ruf am Rand bis zu 5 dB lauter)
            spieler.PitchScale = ruf.Tonhoehe;
            spieler.Play();
        }
        var art = M.Rufarten[ruf.Art];
        LetzterRuf = $"{art.Name}: {ruf.Datei}, {(ruf.Bahn == 2 ? "Gespräch" : ruf.Fern ? "fern" : "nah")}, Seite {ruf.Seite:+0.00;-0.00}, {ruf.Db:0.0} dB (bei {_zeit:0} s)";
        if (_logStufe >= 2)
            GD.Print($"UMGEBUNG RUF t={_zeit:0.00} art={art.Name} datei={ruf.Datei} {(ruf.Bahn == 2 ? "gespraech" : ruf.Fern ? "fern" : "nah")} seite={ruf.Seite:0.00} db={ruf.Db:0.0} spieler={M.AbspielDb(ruf):0.0} " +
                     $"tonhoehe={ruf.Tonhoehe:0.000} laenge={ton.GetLength():0.00} uhr={UhrText(LetzteEingaben.UhrMinute)} b={LetzteEingaben.Bezug:0.00} S={LetzteEingaben.Zoom:0} pause={(LetzteEingaben.Pausiert ? 1 : 0)}");
    }

    /// <summary>Einen Ruf dieser Art jetzt (Entwicklerreiter): nah, außer die Art kennt nur fern oder die Zoomstufe ist schon fern.</summary>
    public void JetztRufen(int art)
    {
        var r = M.Rufarten[art];
        var fern = r.FernAb <= 0 || LetzteEingaben.Zoom >= r.FernAb;
        Abspielen(_planer.Erzwingen(art, fern, LetzteEingaben.Zoom));
    }

    /// <summary>Ein Läuten spielen: Seite des Doms, Pegel und Tiefpass nach dem Abstand (das Modell hat sie gerechnet).</summary>
    private void GlockeSpielen(M.Glocke g)
    {
        if (!_toene.TryGetValue(g.Datei, out var ton)) return;
        if (_panGlocke is not null) _panGlocke.Pan = g.Seite;
        if (_tiefpassGlocke is not null) _tiefpassGlocke.CutoffHz = g.Tiefpass;
        _glockenSpieler.Stream = ton;
        _glockenSpieler.VolumeDb = g.Db;
        _glockenSpieler.PitchScale = g.Tonhoehe;
        _glockenSpieler.Play();
        var art = M.Glockenarten[g.Art];
        LetzterRuf = $"Glocke {art.Name}: {g.Datei}, Seite {g.Seite:+0.00;-0.00}, {g.Db:0.0} dB, {g.Tiefpass:0} Hz (bei {_zeit:0} s)";
        if (_logStufe >= 2)
            GD.Print($"UMGEBUNG GLOCKE t={_zeit:0.00} art={art.Name} datei={g.Datei} seite={g.Seite:0.00} spieler={g.Db:0.0} tiefpass={g.Tiefpass:0} tonhoehe={g.Tonhoehe:0.000} laenge={ton.GetLength():0.00} uhr={UhrText(LetzteEingaben.UhrMinute)}");
    }

    /// <summary>Ein Läuten dieser Art jetzt (Entwicklerreiter): am Ort des Doms, wie die Lage gerade ist.</summary>
    public void JetztLaeuten(int art) => GlockeSpielen(_glocken.Erzwingen(art, LetzteEingaben.Lage));

    /// <summary>Die Menschen der Stadt als Log-Text (auch für den Entwicklerreiter): Zahlen im Umkreis und die Pegel 0–1.</summary>
    public static string LebenText(KlangStadtlebenModell.Lage l) => l.Gemessen
        ? $"leben[leute={l.LeuteImUmkreis} markt={l.MarktleuteImUmkreis} gaeste={l.GaesteImUmkreis} arbeiter={l.ArbeiterImUmkreis} geher={l.GeherImUmkreis} gruppen={l.GruppenImUmkreis} wachen={l.WachOrte.Count} | " +
          $"menge={l.Menge:0.00} markt={l.Markt:0.00} schenke={l.Schenke:0.00}@{l.SchenkeSeite:+0.00;-0.00} nah={l.SchenkeNah:0.00} werk={l.Werkstatt:0.00} geher={l.Geher:0.00} gespraech={l.Gespraech:0.00} wache={l.Wache:0.00}] " +
          $"umwelt[stadt={l.Umwelt.Stadt:0.00} baum={l.Umwelt.Baum:0.00} feld={l.Umwelt.Feld:0.00} wasser={l.Umwelt.Wasser:0.00}] dom[{(l.HatDom ? $"{l.DomAbstand:0} m @{l.DomSeite:+0.00;-0.00}" : "–")}]"
        : "leben[ungemessen]";

    private static string UhrText(float minute) => $"{(int)(minute / 60):00}:{(int)(minute % 60):00}";

    private string LogZeile(in M.Eingaben e, M.Ausgabe a) =>
        $"UMGEBUNG t={_zeit:0} uhr={UhrText(e.UhrMinute)} b={e.Bezug:0.00} jz={e.Jahreszeit} S={e.Zoom:0} Smax={e.ZoomGroesst:0} N={a.Naehe:0.00} H={a.Hoehe:0.00} " +
        $"innen={e.Innen} pause={(e.Pausiert ? 1 : 0)} tps={e.TicksProSekunde:0.0} sprung={(a.Sprungblende ? 1 : 0)} " +
        $"wetter[regen={e.Himmel.Regen:0.00} schnee={e.Himmel.Schnee:0.00} wind={e.Himmel.Wind:0.00} blitze={e.Himmel.Blitze:0.00}] " +
        $"stadt={a.StadtDb:0.0}dB/{a.StadtTiefpass:0}Hz draussen={a.DraussenDb:0.0}dB/{a.DraussenTiefpass:0}Hz schleifen={KlangDienst.SchleifenAnzahl.Benannt} " +
        LebenText(e.Lage) + " | " +
        string.Join(" ", M.Schichten.Select((s, i) => $"{s.Name}={(a.Db[i] <= M.AusDb ? "aus" : a.Db[i].ToString("0.0"))}{(_angehalten[i] ? "(angehalten)" : "")}"));

    // ── Hörprobe (--umgebungfahrt=1) ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ein Haltepunkt der Hörprobe. Uhr (Stunden), Zoom und Blickpunkt (Meter) gleiten linear bis zum nächsten Punkt; Wetter (0 klar, 1 Regen, 2 Gewitter,
    /// 3 Schnee), Jahreszeit (0 Frühling … 3 Winter) und Innen (1 = in der Schankstube) gelten ab dem Punkt bis zum nächsten.
    /// </summary>
    public readonly record struct FahrtPunkt(float T, float Stunde, float Zoom, float X, float Z, int Wetter = 0, int Jahreszeit = 0, int Innen = 0);

    /// <summary>
    /// Die feste Fahrt der Hörprobe 3 auf der Karte <c>greybox</c> (Markt im Ursprung, Dom bei (−20|−11), Fluss im Süden, Wald im Südosten): Die Menschen liefert
    /// <see cref="KlangStadtlebenModell.Szenario"/> aus der Uhr, Wetter und Jahreszeit kommen aus der echten Stadtansicht, die Umgebung (Stadt gegen
    /// Natur) aus der echten Karte – dieselben Kurven wie im Spiel, nur Uhr, Zoom, Blickpunkt, Wetter und Menschen sind vorgegeben.
    /// </summary>
    public static readonly FahrtPunkt[] FahrtPunkte =
    {
        // 1. Früher Morgen am Markt: leere Straßen, Vogelchor, Hahn; Straßen füllen sich; 8 Uhr Morgenglocke zum Arbeitsbeginn (bei etwa 23 s)
        new(0, 6.4f, 40, 0, 0), new(16, 7.7f, 40, 0, 0), new(32, 8.4f, 40, 0, 0),
        // 2. Markttreiben, Gesprächsfetzen an den Gruppen (Zoom 30), um 10 Uhr die Messglocke (bei etwa 67 s)
        new(46, 9.0f, 30, -5, 0), new(80, 10.6f, 30, 5, 0), new(100, 11.6f, 40, 10, 0),
        // 3. Von der Stadt aufs Land: Wiese, Wald, Fluss (Mittag): das Stadtleben verklingt, Laub, Plätschern
        new(116, 12.0f, 40, 10, 0), new(134, 12.4f, 40, 150, 10), new(146, 12.8f, 40, 190, 60), new(160, 13.2f, 40, 120, 150), new(176, 14.0f, 40, 120, 150),
        // 4. Frühlingsabend am Fluss: Frösche, Abendgesang (Sprung der Uhr, 3 s Blende)
        new(176.01f, 19.2f, 40, 120, 150), new(196, 19.9f, 40, 120, 150),
        // 5. Sommernacht auf der Wiese: Grillen
        new(196.01f, 21.2f, 40, 175, 10, 0, 1), new(214, 21.8f, 40, 175, 10, 0, 1),
        // 6. Winterlicher Vormittag, Schneefall auf dem Feld: Schneestille, Krähen
        new(214.01f, 10.8f, 40, 175, 10, 3, 3), new(238, 11.4f, 40, 175, 10, 3, 3),
        // 7. Frühlingsabend in der Stadt: die Straßen leeren sich, 20 Uhr Abendglocke (bei etwa 259 s), die Gespräche werden leiser
        new(238.01f, 18.8f, 40, 0, 0), new(262, 20.2f, 40, 0, 0),
        // 8. Zur Schänke (links im Bild) und ganz heran: durch die Tür gedämpft, dann hell und laut
        new(276, 20.5f, 40, -12, 6), new(290, 20.8f, 25, -24, 10),
        // 9. In der Schankstube: laut und lebendig (Innenansicht)
        new(290.01f, 20.9f, 25, -24, 10, 0, 0, 1), new(316, 21.5f, 25, -24, 10, 0, 0, 1),
        // 10. Wieder draußen: 22 Uhr Schankschluss und Feuerglocke (bei etwa 327 s), die Gäste gehen, die Gasse wird still
        new(316.01f, 21.6f, 30, -24, 10), new(338, 22.4f, 30, -10, 8),
        // 11. Nacht: Nachtwächter mit Laterne und Horn, sonst Stille
        new(338.01f, 22.5f, 40, 0, 10), new(372, 23.0f, 40, 30, 12),
        // 12. Regentag in der Stadt: Regen auf Pflaster und Dächern, die Stadt dumpf (Sprung auf den Frühlingsnachmittag)
        new(372.01f, 14.0f, 30, 0, 0, 1), new(400, 14.6f, 30, 0, 0, 1),
        // 13. Sommergewitter: Blitz und Donner
        new(400.01f, 15.0f, 40, 0, 0, 2, 1), new(432, 15.4f, 40, 0, 0, 2, 1),
    };
    public const float FahrtZufallAb = 0f, FahrtPauseAb = 1e9f, FahrtEnde = 432f;

    /// <summary>
    /// Die kurze Hörprobe <c>--umgebungfahrt=2</c> (192 s, nur Glocken und Donner, je mit etwas Stadt davor): Morgenglocke um 8 Uhr (bei etwa 16 s), Abendglocke
    /// um 20 Uhr (bei etwa 67 s), Feuerglocke um 22 Uhr (bei etwa 111 s), dann Gewitter in der Stadt (ab 146 s) mit nahem und fernem Donner.
    /// </summary>
    public static readonly FahrtPunkt[] FahrtPunkteKurz =
    {
        new(0, 7.4f, 40, 0, 0), new(14, 7.9f, 40, 0, 0), new(30, 8.5f, 40, 0, 0), new(48, 9.2f, 40, 0, 0),
        new(48.01f, 19.0f, 40, 0, 0), new(64, 19.9f, 40, 0, 0), new(100, 21.0f, 40, 0, 0),
        new(104.01f, 21.6f, 40, 0, 0), new(112, 22.05f, 40, 0, 0), new(134, 22.8f, 40, 0, 0),
        new(136.01f, 14.6f, 40, 0, 0, 0, 1), new(146, 14.9f, 40, 0, 0, 0, 1),
        new(146.01f, 15.0f, 40, 0, 0, 2, 1), new(192, 15.6f, 40, 0, 0, 2, 1),
    };
    public const float FahrtEndeKurz = 192f;

    /// <summary>
    /// Die Hörprobe <c>--umgebungfahrt=3</c> (250 s, Glocken und Gewitter): Abendglocke um 20 Uhr (bei etwa 8 s), Messglocke um 10 Uhr (bei etwa 58 s), dann ein Gewitter, das
    /// von 110 bis 220 s heranzieht, über die Stadt geht und weiterzieht (<see cref="GewitterVon"/>–<see cref="GewitterBis"/>: Fortschritt 0–1). Die Blitze fallen im
    /// Abstand, den das Gewitter vorgibt (am Rand 25 km: Wetterleuchten ohne Donner, dazwischen mittel, in der Mitte nah); Regen und Böen steigen vor dem nahen Donner.
    /// </summary>
    public static readonly FahrtPunkt[] FahrtPunkteGewitter =
    {
        new(0, 19.8f, 40, 0, 0), new(16, 20.4f, 40, 0, 0), new(46, 21.0f, 40, 0, 0),
        new(46.01f, 9.2f, 40, 0, 0), new(60, 10.2f, 40, 0, 0), new(100, 11.2f, 40, 0, 0),
        new(100.01f, 14.4f, 40, 0, 0, 0, 1), new(110, 14.7f, 40, 0, 0, 0, 1),
        new(110.01f, 15.0f, 40, 0, 0, 2, 1), new(250, 17.0f, 40, 0, 0, 2, 1),
    };
    public const float FahrtEndeGewitter = 250f, GewitterVon = 110f, GewitterBis = 230f;

    /// <summary>
    /// Der Schnipsel <c>--umgebungfahrt=4</c> (60 s): ein normales Gewitter über der Stadt (Blitze nie näher als 1 km, kräftiger, nicht brachialer Donner), bei 40 s ein direkter
    /// Blitzeinschlag am Marktplatz (die brachiale Einschlag-Aufnahme).
    /// </summary>
    public static readonly FahrtPunkt[] FahrtPunkteEinschlag = { new(0, 15.0f, 40, 0, 0, 2, 1), new(60, 15.3f, 40, 0, 0, 2, 1) };
    public const float FahrtEndeEinschlag = 60f, EinschlagBei = 40f;

    /// <summary>Feste Rufe der Fahrt (Sekunde, Art, fern); alle übrigen plant der Planer mit festem Zufall aus den Menschen des Szenarios.</summary>
    public static readonly (float T, string Art, bool Fern)[] FahrtRufe =
    {
        (3, "Hahn", false), (11, "Hahn", false),
        (222, "Kraehe", false), (229, "Kraehe", true),        // im Winter, damit sie in der Probe vorkommen (der Planer ruft sie im Winter etwa alle 45 s)
        (347, "Nachtwaechter", true), (365, "Nachtwaechter", false), // das Horn der Streife, ebenso (sonst im Mittel alle 150 s)
    };

    private bool FahrtMomentbild(in M.Eingaben e)
    {
        var f = FokusQuelle?.Invoke() ?? (0f, 0f);
        var zoom = e.Innen == M.Innenart.Draussen ? e.Zoom : 60f;
        KlangStadtlebenModell.Szenario.Fuellen(_momentbild, e.UhrMinute / 60f, zoom, f.X, f.Z);
        _momentbild.Umwelt = UmweltQuelle?.Invoke(zoom);
        if (e.Innen == M.Innenart.Schankstube) _momentbild.InnenGaeste = Math.Max(KlangStadtlebenModell.Szenario.Zaehlen(e.UhrMinute / 60f).Gaeste, 5);
        return true;
    }

    private int _fahrtPunkt = -1;

    private M.Eingaben FahrtSchritt(M.Eingaben e, double delta)
    {
        _fahrtZeit += delta;
        var punkte = _einschlagFahrt ? FahrtPunkteEinschlag : _gewitterFahrt ? FahrtPunkteGewitter : _kurzeFahrt ? FahrtPunkteKurz : FahrtPunkte;
        if (_fahrtZeit >= (_einschlagFahrt ? FahrtEndeEinschlag : _gewitterFahrt ? FahrtEndeGewitter : _kurzeFahrt ? FahrtEndeKurz : FahrtEnde))
        {
            GD.Print("UMGEBUNG Fahrt fertig");
            GetTree().Quit();
            return e;
        }
        var t = (float)_fahrtZeit;
        var k = 0;
        while (k < punkte.Length - 2 && punkte[k + 1].T <= t) k++;
        var p0 = punkte[k];
        var p1 = punkte[k + 1];
        var f = Math.Clamp((t - p0.T) / Math.Max(p1.T - p0.T, 0.001f), 0f, 1f);
        if (k != _fahrtPunkt)
        {
            _fahrtPunkt = k;
            WetterVorgeben?.Invoke(p0.Wetter, (Jahreszeit)p0.Jahreszeit); // Wetter und Jahreszeit der Stadtansicht gelten ab hier (sofort)
            GD.Print($"UMGEBUNG Fahrt Abschnitt {k} bei {t:0} s: Wetter {p0.Wetter}, Jahreszeit {p0.Jahreszeit}, Innen {p0.Innen}");
        }
        float L(float a, float b) => a + (b - a) * f;
        var stunde = L(p0.Stunde, p1.Stunde);
        var zoom = L(p0.Zoom, p1.Zoom);
        UhrVorgeben?.Invoke(stunde);
        ZoomVorgeben?.Invoke(zoom);
        FokusVorgeben?.Invoke(L(p0.X, p1.X), L(p0.Z, p1.Z));
        if (_einschlagFahrt && !_einschlagGeschehen && t >= EinschlagBei) { _einschlagGeschehen = true; EinschlagVorgeben?.Invoke(); }
        if (_einschlagFahrt) GewitterVorgeben?.Invoke(0.5f); // über der Stadt: die nächsten normalen Blitze (ab 1 km)
        if (_gewitterFahrt) GewitterVorgeben?.Invoke(t >= GewitterVon ? Math.Clamp((t - GewitterVon) / (GewitterBis - GewitterVon), 0f, 1f) : null);
        while (!_kurzeFahrt && !_gewitterFahrt && !_einschlagFahrt && _fahrtRuf < FahrtRufe.Length && FahrtRufe[_fahrtRuf].T <= t)
        {
            var (_, art, fern) = FahrtRufe[_fahrtRuf++];
            Abspielen(_planer.Erzwingen(Array.FindIndex(M.Rufarten, r => r.Name == art), fern, e.Zoom));
        }
        return e with { UhrMinute = stunde * 60f, Zoom = zoom, Pausiert = t >= FahrtPauseAb, Innen = p0.Innen == 1 ? M.Innenart.Schankstube : M.Innenart.Draussen };
    }
}
