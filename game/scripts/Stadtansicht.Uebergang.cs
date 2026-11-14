using System.Diagnostics;
using Godot;

namespace Reichsgilde;

// Schwarzblende beim Betreten und Verlassen von Innenräumen (Uebergangsblende.cs, InnenraumUebergang.cs).
// Der Aufbau des Raums (Betreten) läuft im Schwarzen in kleinen Schritten; die Kamera springt dabei, statt zu fahren.
public partial class Stadtansicht
{
    private Uebergangsblende? _blende;
    private bool _kameraSprung;
    private bool _innenWarm; // der erste Innenraum der Sitzung ist einmal aufgebaut (der erste kostet etwa 140 ms, das gehört ins Schwarz, nicht in den Hover)

    /// <summary>Ein Übergang (Schwarzblende) läuft: Klicks in die Stadt zählen nicht.</summary>
    public bool UebergangLaeuft => _blende?.Laufend == true;

    /// <summary>Der Spieler zieht gerade ein Auswahlrechteck in der Welt (der Handschuh zeigt dann Zeigen statt der Faust).</summary>
    public bool RahmenZieht => _rahmenZieht;

    private Uebergangsblende Blende()
    {
        if (_blende is null) AddChild(_blende = new Uebergangsblende());
        return _blende;
    }

    /// <summary>Spielerwunsch (Doppelklick, Knopf): Gebäude betreten, mit Blende. Ohne Blende (<c>--blende=0</c>) wie <see cref="InnenansichtBetreten(string)"/>.</summary>
    public bool InnenansichtBetretenMitBlende(string kennung)
    {
        if (!Uebergangsblende.Aktiv) return InnenansichtBetreten(kennung);
        if (AnsichtZu(kennung) is not { HatInnenraum: true } ansicht || UebergangLaeuft) return false;
        if (_innen == ansicht) return true;
        var schritte = new List<(string, Action)>
        {
            ("Vorbereiten", () => { if (AnsichtZu(kennung) is { } a) InnenVorbereitenJetzt(a); }),
            ("Betreten", () => { _kameraSprung = true; try { InnenansichtBetreten(kennung); } finally { _kameraSprung = false; } }),
        };
        return Blende().Starten(UebergangRichtung.Betreten, kennung, schritte, klinke: true);
    }

    /// <summary>Spielerwunsch (Rechtsklick, Esc, Knopf „Zur Stadt“): Haus verlassen, mit Blende.</summary>
    public void InnenansichtVerlassenMitBlende()
    {
        if (_innen is null || UebergangLaeuft) return;
        if (!Uebergangsblende.Aktiv) { InnenansichtVerlassen(); return; }
        var schritte = new List<(string, Action)>
        {
            ("Verlassen", () => { _kameraSprung = true; try { InnenansichtVerlassen(); } finally { _kameraSprung = false; } }),
        };
        Blende().Starten(UebergangRichtung.Verlassen, null, schritte);
    }

    /// <summary>Hover: die Maus steht über dem betretbaren Gebäude <paramref name="kennung"/> (null = über nichts Betretbarem). Der Raum wird einmal vorbereitet, bevor der Doppelklick kommt.</summary>
    public void InnenSchweben(string? kennung)
    {
        if (!Uebergangsblende.Aktiv || _innen is not null || !_innenWarm) return;
        var blende = Blende();
        blende.Ablauf.Schweben(kennung);
        if (blende.Ablauf.VorladenNoetig && kennung is not null && AnsichtZu(kennung) is { HatInnenraum: true } ansicht)
        {
            InnenVorbereitenJetzt(ansicht);
            blende.Ablauf.VorladenFertig(kennung);
        }
    }

    /// <summary>Die Arbeit, die beim Betreten ohnehin anfällt (Stufenteile, Figuren, Lager, Auslage, Feuer), schon vorher im verborgenen Raum; beim Betreten ist alles bereit.</summary>
    private void InnenVorbereitenJetzt(InnenAnsicht ansicht)
    {
        if (_innen == ansicht) return;
        var uhr = Stopwatch.StartNew();
        InnenAuffuellen(ansicht);
        _innenWarm = true;
        if (Uebergangsblende.Messen) GD.Print($"[innenmessung] vorbereiten {ansicht.Kennung}: {uhr.Elapsed.TotalMilliseconds:0.0} ms");
        _blende?.Ablauf.VorladenFertig(ansicht.Kennung);
    }

    // ── Messung ─────────────────────────────────────────────────────────────

    /// <summary><c>--blendetest=Kennung</c>: betritt das Gebäude mit der Schwarzblende, wartet, verlässt es wieder und schreibt Phasen, Dauern und Bildzahl ins Log (<c>[blende]</c>).</summary>
    public async void BlendeTestStarten(string kennung)
    {
        foreach (var richtung in new[] { UebergangRichtung.Betreten, UebergangRichtung.Verlassen })
        {
            var anfang = Time.GetTicksMsec();
            var bilder = Uebergangsblende.Gezeichnet();
            var ok = richtung == UebergangRichtung.Betreten ? InnenansichtBetretenMitBlende(kennung) : Verlassen();
            while (UebergangLaeuft) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"[blende] {richtung}: angenommen={ok}, {Time.GetTicksMsec() - anfang} ms, {Uebergangsblende.Gezeichnet() - bilder} Bilder, drinnen={(InnenKennung ?? "nein")}");
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("[blende] Ende");

        bool Verlassen() { InnenansichtVerlassenMitBlende(); return UebergangLaeuft; }
    }

    /// <summary><c>--blendeschleife=Kennung,n[,drin]</c>: betritt und verlässt das Gebäude n-mal mit der Schwarzblende und schreibt nach jedem Durchgang Zeichenaufrufe, Objekt-, Knoten- und Waisenknotenzahl sowie den Zustand (Innenansicht, Raum, aktive Kamera) ins Log (<c>[schleife]</c>).</summary>
    public async void BlendeSchleifeStarten(string kennung, int durchgaenge, bool amEndeDrinnen = false)
    {
        void Zeile(string was, int nr)
        {
            var kamera = GetViewport().GetCamera3D();
            GD.Print($"[schleife] {nr} {was}: Zeichenaufrufe {(long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, Objekte {(long)Performance.GetMonitor(Performance.Monitor.ObjectCount)}, Knoten {(long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}, Waisen {(long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)}, innen={(InnenKennung ?? "nein")}, raum={(_raum?.Kennung ?? "nein")}, kamera={kamera?.Name}");
        }
        for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Zeile("Start", 0);
        for (var nr = 1; nr <= durchgaenge; nr++)
        {
            var ok = InnenansichtBetretenMitBlende(kennung);
            while (UebergangLaeuft) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Zeile($"drinnen (angenommen={ok})", nr);
            if (amEndeDrinnen && nr == durchgaenge) break;
            InnenansichtVerlassenMitBlende();
            while (UebergangLaeuft) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Zeile("draußen", nr);
        }
        GD.Print("[schleife] Ende");
    }

    /// <summary>
    /// <c>--innenmessung=1</c> (ohne Vorbereitung) oder <c>=2</c> (mit Vorbereitung beim Hover): betritt jedes Gebäude mit Innenraum zweimal und
    /// schreibt je Gebäude die Dauer des Betretens auf dem Hauptfaden und die längste Bildzeit der folgenden Bilder ins Log (<c>[innenmessung]</c>).
    /// Ohne Fenster misst sie nur die Rechenzeit (kein Zeichnen); die Bildzeit mit Grafik misst die NAS.
    /// </summary>
    public async void InnenMessungStarten(bool vorbereiten)
    {
        var liste = _betriebe.Values.Cast<InnenAnsicht>().Concat(_sonderbauten.Values).Where(a => a.HatInnenraum).Take(8).ToList();
        GD.Print($"[innenmessung] Start: {liste.Count} Gebäude, {(vorbereiten ? "mit" : "ohne")} Vorbereitung");
        _kameraSprung = true;
        foreach (var ansicht in liste)
        {
            var zeiten = new List<string>();
            for (var runde = 1; runde <= 2; runde++)
            {
                double vor = 0;
                if (vorbereiten)
                {
                    var u = Stopwatch.StartNew();
                    InnenVorbereitenJetzt(ansicht);
                    vor = u.Elapsed.TotalMilliseconds;
                }
                var uhr = Stopwatch.StartNew();
                Betreten(ansicht);
                var betreten = uhr.Elapsed.TotalMilliseconds;
                var laengste = 0.0;
                var letzte = Time.GetTicksUsec();
                for (var i = 0; i < 6; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var jetzt = Time.GetTicksUsec();
                    laengste = Math.Max(laengste, (jetzt - letzte) / 1000.0);
                    letzte = jetzt;
                }
                zeiten.Add($"Runde {runde}: vorbereiten {vor:0.0} ms, betreten {betreten:0.0} ms, längstes Bild danach {laengste:0.0} ms");
                InnenansichtVerlassen();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            GD.Print($"[innenmessung] {ansicht.Kennung}: {string.Join("; ", zeiten)}");
        }
        _kameraSprung = false;
        GD.Print("[innenmessung] Ende");
    }
}
