namespace Reichsgilde;

/// <summary>Wohin der Übergang führt: in ein Gebäude hinein oder wieder hinaus in die Stadtansicht.</summary>
public enum UebergangRichtung { Betreten, Verlassen }

/// <summary>Die Stufen des Übergangs (Reihenfolge = Ablauf).</summary>
public enum UebergangPhase
{
    /// <summary>Nichts los.</summary>
    Ruhe,
    /// <summary>Die Maus schwebt über einem betretbaren Gebäude: der Raum wird schon vorbereitet.</summary>
    Vorladen,
    /// <summary>Nur mit 3D-Handschuh: die Hand drückt die Klinke (<see cref="InnenraumUebergang.KlinkeSekunden"/>).</summary>
    Klinke,
    /// <summary>Das Bild wird schwarz.</summary>
    BlendeAus,
    /// <summary>Schwarz: der Raum wird (in Schritten über mehrere Bilder) aufgebaut.</summary>
    Laden,
    /// <summary>Schwarz: der Raum steht und muss noch <see cref="InnenraumUebergang.MindestBilder"/> Bilder gezeichnet haben (Shader, Puffer).</summary>
    Warten,
    /// <summary>Das Bild wird wieder hell.</summary>
    BlendeEin,
    /// <summary>Geschafft; der nächste Übergang darf beginnen.</summary>
    Fertig,
}

/// <summary>
/// Der Zustandsautomat der Schwarzblende beim Betreten und Verlassen eines Innenraums, ohne Godot (damit er sich testen lässt: <c>InnenraumUebergangTests.cs</c>).
/// Zweck: das Nachladen beim ersten Betreten (neue Shader, Puffer, Figuren) liegt im Schwarzen statt als Standbild im Spiel.
/// Ablauf: Ruhe, Vorladen (Hover), [Klinke], BlendeAus, Laden, Warten, BlendeEin, Fertig. Das Herausblenden beginnt erst, wenn der Raum
/// fertig ist <b>und</b> danach mindestens <see cref="MindestBilder"/> Bilder gezeichnet wurden. Der Spielcode ruft <see cref="Schritt"/> jedes Bild,
/// meldet mit <see cref="LadenFertig"/> den fertigen Raum und liest <see cref="Alpha"/> (0 = durchsichtig, 1 = schwarz). Zeiten sind benannte Konstanten.
/// </summary>
public sealed class InnenraumUebergang
{
    /// <summary>Dauer des Hineinblendens ins Schwarz und des Herausblendens (Sekunden).</summary>
    public const double BlendeAusSekunden = 0.4, BlendeEinSekunden = 0.4;

    /// <summary>Klinke drücken (Sekunden, 3D-Handschuh; Nutzer: 0,6 bis 0,8).</summary>
    public const double KlinkeSekunden = 0.7;

    /// <summary>So viele Bilder muss der fertige Raum im Schwarzen gezeichnet haben, bevor das Herausblenden beginnt.</summary>
    public const int MindestBilder = 2;

    /// <summary>Notbremse: Nach so vielen Sekunden Übergang wird trotz fehlender Meldung herausgeblendet, damit nie ein schwarzer Schirm bleibt.</summary>
    public const double HoechstdauerSekunden = 12.0;

    public UebergangPhase Phase { get; private set; } = UebergangPhase.Ruhe;
    public UebergangRichtung Richtung { get; private set; }

    /// <summary>Kennung des Gebäudes (beim Betreten, beim Schweben); beim Verlassen null.</summary>
    public string? Ziel { get; private set; }

    /// <summary>Schwärze 0..1 (sanft: Anfang und Ende langsam).</summary>
    public double Alpha { get; private set; }

    /// <summary>Hat die Hand die Klinke zu drücken (nur mit 3D-Handschuh und Klinken-Clip)?</summary>
    public bool KlinkeNoetig { get; private set; }

    /// <summary>Ein Übergang läuft (von der Klinke bis zum Ende des Herausblendens): Eingaben in die Stadt sperren.</summary>
    public bool Laufend => Phase is UebergangPhase.Klinke or UebergangPhase.BlendeAus or UebergangPhase.Laden or UebergangPhase.Warten or UebergangPhase.BlendeEin;

    /// <summary>Die Maus ist über einem Gebäude, dessen Raum noch nicht vorbereitet wurde.</summary>
    public bool VorladenNoetig => Phase == UebergangPhase.Vorladen && Ziel is not null && Ziel != _vorbereitet;

    /// <summary>Wie viele Sekunden der Übergang gedauert hat (für Messung und Notbremse).</summary>
    public double Gesamtdauer { get; private set; }

    private double _t;
    private double _klinkeDauer = KlinkeSekunden;
    private long _fertigBei;
    private string? _vorbereitet;

    /// <summary>Hover: die Maus steht über dem Gebäude <paramref name="kennung"/> (oder null = über nichts Betretbarem). Wirkt nur außerhalb eines Übergangs.</summary>
    public void Schweben(string? kennung)
    {
        if (Laufend) return;
        Phase = kennung is null ? UebergangPhase.Ruhe : UebergangPhase.Vorladen;
        Ziel = kennung;
    }

    /// <summary>Der Spielcode hat den Raum zu <paramref name="kennung"/> vorbereitet.</summary>
    public void VorladenFertig(string kennung) => _vorbereitet = kennung;

    /// <summary>Der Raum muss neu vorbereitet werden (Gebäude verkauft, umgebaut).</summary>
    public void VorladenVergessen() => _vorbereitet = null;

    /// <summary>Beginnt den Übergang (Doppelklick, Rechtsklick, Esc). Läuft schon einer, passiert nichts (false). <paramref name="klinkeDauer"/>: so lange dauert die Klinke (aus den Clips der Hand; Vorgabe <see cref="KlinkeSekunden"/>).</summary>
    public bool Starten(UebergangRichtung richtung, string? ziel, bool klinke, double klinkeDauer = KlinkeSekunden)
    {
        if (Laufend) return false;
        Richtung = richtung;
        Ziel = ziel;
        KlinkeNoetig = klinke && richtung == UebergangRichtung.Betreten;
        _klinkeDauer = klinkeDauer;
        Gesamtdauer = 0;
        _t = 0;
        Alpha = 0;
        Phase = KlinkeNoetig ? UebergangPhase.Klinke : UebergangPhase.BlendeAus;
        return true;
    }

    /// <summary>
    /// Abbrechen (zum Beispiel das Gebäude ist weg): Vor dem Laden geht es zurück (aus der Klinke sofort in die Ruhe, aus dem Hineinblenden ohne Sprung
    /// wieder hinaus); wenn der Raum schon gebaut wird, läuft der Übergang zu Ende (der Zustand der Stadt darf nicht halb bleiben). Gibt zurück, ob abgebrochen wurde.
    /// </summary>
    public bool Abbrechen()
    {
        switch (Phase)
        {
            case UebergangPhase.Klinke:
                Phase = UebergangPhase.Ruhe;
                Alpha = 0;
                return true;
            case UebergangPhase.BlendeAus:
                // aus dem Schwarz zurück: die Zeit der Blende ein so setzen, dass die Schwärze gleich bleibt
                Phase = UebergangPhase.BlendeEin;
                _t = Umkehr(1.0 - Alpha) * BlendeEinSekunden;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Der Spielcode hat den Raum aufgebaut; <paramref name="gezeichnet"/> = Zahl der bisher gezeichneten Bilder (<c>Engine.GetFramesDrawn()</c>).</summary>
    public void LadenFertig(long gezeichnet)
    {
        if (Phase != UebergangPhase.Laden) return;
        _fertigBei = gezeichnet;
        Phase = UebergangPhase.Warten;
    }

    /// <summary>Ein Bild weiter: <paramref name="dt"/> Sekunden, <paramref name="gezeichnet"/> = bisher gezeichnete Bilder.</summary>
    public void Schritt(double dt, long gezeichnet)
    {
        dt = Math.Max(dt, 0);
        if (Laufend) Gesamtdauer += dt;
        switch (Phase)
        {
            case UebergangPhase.Klinke:
                _t += dt;
                if (_t >= _klinkeDauer) { _t = 0; Phase = UebergangPhase.BlendeAus; }
                break;
            case UebergangPhase.BlendeAus:
                _t += dt;
                Alpha = Sanft(_t / BlendeAusSekunden);
                if (_t >= BlendeAusSekunden) { Alpha = 1; _t = 0; Phase = UebergangPhase.Laden; }
                break;
            case UebergangPhase.Laden:
                if (Gesamtdauer >= HoechstdauerSekunden) { _fertigBei = gezeichnet; Phase = UebergangPhase.Warten; } // Notbremse: der Aufbau meldet sich nicht
                break;
            case UebergangPhase.Warten:
                if (gezeichnet - _fertigBei >= MindestBilder || Gesamtdauer >= HoechstdauerSekunden) { _t = 0; Phase = UebergangPhase.BlendeEin; }
                break;
            case UebergangPhase.BlendeEin:
                _t += dt;
                Alpha = 1.0 - Sanft(_t / BlendeEinSekunden);
                if (_t >= BlendeEinSekunden) { Alpha = 0; _t = 0; Phase = UebergangPhase.Fertig; }
                break;
            case UebergangPhase.Fertig:
                Phase = UebergangPhase.Ruhe;
                break;
        }
    }

    /// <summary>Sanfter Anstieg 0..1 (Smoothstep): Anfang und Ende der Blende langsam, damit kein Ruck im Bild liegt.</summary>
    public static double Sanft(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    /// <summary>Umkehrung von <see cref="Sanft"/> (Newton), um beim Abbrechen ohne Sprung weiterzublenden.</summary>
    private static double Umkehr(double y)
    {
        var x = Math.Clamp(y, 0, 1);
        for (var i = 0; i < 12; i++)
        {
            var abl = 6 * x * (1 - x);
            if (abl < 1e-6) break;
            x = Math.Clamp(x - (Sanft(x) - y) / abl, 0, 1);
        }
        return x;
    }
}
