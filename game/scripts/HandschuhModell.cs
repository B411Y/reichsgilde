namespace Reichsgilde;

/// <summary>Die drei Haltungen des 3D-Handschuhs (Namen = Namen der Schleifen in der .glb).</summary>
public enum HandPose { Ruhe, Zeigen, Greifen }

/// <summary>
/// Die reine Ablauflogik des 3D-Handschuhs (<see cref="HandschuhZeiger"/>) ohne Godot, damit sie sich testen lässt (<c>HandschuhModellTests.cs</c>):
/// welche Haltung die Hand haben soll, in welcher Reihenfolge Übergänge und das Tippen laufen und wann der Rückfall auf den 2D-Zeiger kommt.
/// Namen der Clips stehen in der .glb: Schleifen <c>Ruhe</c>, <c>Zeigen</c>, <c>Greifen</c>, Einmalclips <c>Tippen</c> und <c>Uebergang_&lt;von&gt;_&lt;nach&gt;</c>;
/// ihre Längen liest der Spielcode aus der Datei (keine festen Bildzahlen).
/// </summary>
public static class HandschuhModell
{
    /// <summary>Ab dieser Zeigergröße (Pixel) gilt der 3D-Handschuh; darunter bleibt der 2D-Zeiger (Vorgabe des Auftrags).</summary>
    public const int MindestGroesse = 48;

    /// <summary>Wie weit (Pixel) die Maus bei gedrückter Taste wandern muss, bis aus dem Klick ein Ziehen (Greifen) wird.</summary>
    public const double ZiehSchwelle = 8.0;

    /// <summary>Welche Haltung gewünscht ist: Ziehen = Greifen, sonst über Anklickbarem Zeigen, sonst Ruhe.</summary>
    public static HandPose Wunsch(bool ziehen, bool ueberAnklickbarem) => ziehen ? HandPose.Greifen : ueberAnklickbarem ? HandPose.Zeigen : HandPose.Ruhe;

    public static string Schleife(HandPose pose) => pose.ToString();

    public static string Uebergang(HandPose von, HandPose nach) => $"Uebergang_{von}_{nach}";

    /// <summary>Zeigerpunkt: die Spitze des Zeigefingers (Anker_Spitze) liegt genau auf der Mausposition; gesucht ist die linke obere Ecke des Bildes.</summary>
    public static (double X, double Y) BildEcke(double mausX, double mausY, double spitzeX, double spitzeY) => (mausX - spitzeX, mausY - spitzeY);

    /// <summary>Wie viel Neigung (-1..1) eine Mausgeschwindigkeit (Pixel je Sekunde) macht; ab etwa 1800 Pixel je Sekunde voll.</summary>
    public static double NeigungsStaerke(double geschwindigkeit) => Math.Clamp(geschwindigkeit / 1800.0, -1.0, 1.0);

    /// <summary>Gleitet <paramref name="aktuell"/> unabhängig von der Bildrate zum <paramref name="ziel"/> (Zeitkonstante in Sekunden); Trägheit der Neigung.</summary>
    public static double Gleiten(double aktuell, double ziel, double dt, double zeitkonstante) =>
        aktuell + (ziel - aktuell) * (1.0 - Math.Exp(-Math.Max(dt, 0.0) / Math.Max(zeitkonstante, 1e-6)));
}

/// <summary>
/// Reihenfolge der Clips. Die Hand ist immer in einer Haltung (<see cref="Pose"/>); wechselt der Wunsch, läuft der Übergangsclip, danach die Schleife der neuen Haltung.
/// Ein Klick (Tippen) geht nur aus der Haltung Zeigen: ist die Hand anders, läuft erst der Übergang dorthin. Wird während des Wartens gezogen (Greifen), entfällt das Tippen.
/// </summary>
public sealed class HandschuhAblauf
{
    private HandPose _ende;
    private bool _tippen;

    public HandPose Pose { get; private set; } = HandPose.Ruhe;

    /// <summary>Ein Einmalclip (Übergang oder Tippen) läuft gerade; die Schleife der Haltung wartet, bis <see cref="EinmalFertig"/> gerufen wird.</summary>
    public bool EinmalLaeuft { get; private set; }

    /// <summary>Der Spielcode meldet einen Klick (Taste gedrückt, noch kein Ziehen).</summary>
    public void TippenMerken() => _tippen = true;

    /// <summary>Welcher Clip jetzt starten soll (Einmalclip), oder null, wenn nichts zu tun ist (die Schleife der Haltung läuft weiter).</summary>
    public string? Entscheiden(HandPose wunsch)
    {
        if (EinmalLaeuft) return null;
        if (wunsch == HandPose.Greifen) _tippen = false;
        if (_tippen)
        {
            if (Pose != HandPose.Zeigen) return Starten(HandschuhModell.Uebergang(Pose, HandPose.Zeigen), HandPose.Zeigen);
            _tippen = false;
            return Starten("Tippen", HandPose.Zeigen);
        }
        return Pose != wunsch ? Starten(HandschuhModell.Uebergang(Pose, wunsch), wunsch) : null;
    }

    private string Starten(string clip, HandPose ende)
    {
        EinmalLaeuft = true;
        _ende = ende;
        return clip;
    }

    /// <summary>Der Einmalclip ist zu Ende: die Hand steht jetzt in der Endhaltung des Clips.</summary>
    public void EinmalFertig()
    {
        if (!EinmalLaeuft) return;
        Pose = _ende;
        EinmalLaeuft = false;
    }
}

/// <summary>
/// Wann der 3D-Handschuh auf den 2D-Zeiger zurückfällt: mittlere Bildrate über ein Fenster von etwa 2 Sekunden unter <see cref="Unter"/>.
/// Zurück zum 3D-Handschuh erst nach <see cref="Sperre"/> Sekunden und bei mindestens <see cref="Ueber"/> Bildern je Sekunde (Hysterese, kein Flackern);
/// nach <see cref="MaxAusfaelle"/> Rückfällen in einer Sitzung bleibt es beim 2D-Zeiger. Die ersten <see cref="Schonzeit"/> Sekunden und die <see cref="NachRuckeln"/> Sekunden nach einem einzelnen Hänger über <see cref="Ruckelgrenze"/> Sekunden zählen nicht (Laden einer Karte).
/// </summary>
public sealed class HandschuhLeistung
{
    public const double Fensterdauer = 2.0, Unter = 40.0, Ueber = 52.0, Schonzeit = 4.0, Sperre = 10.0, Ruckelgrenze = 0.4, NachRuckeln = 3.0;
    public const int MaxAusfaelle = 3;

    private double _gesamt, _zeit, _sperreBis, _ruheBis;
    private int _bilder;

    public bool Rueckfall { get; private set; }
    public int Ausfaelle { get; private set; }
    public double LetzteFps { get; private set; }

    /// <summary>Einmal je Bild mit der echten Zeit seit dem letzten Bild; <paramref name="dreiDLaeuft"/>: der 3D-Handschuh wird gerade gezeichnet.</summary>
    public void Takt(double dt, bool dreiDLaeuft)
    {
        if (dt <= 0) return;
        _gesamt += dt;
        if (dt > Ruckelgrenze)
        {
            // Ein einzelner Hänger (Karte laden, Fenster verschoben) ist keine langsame Bildrate: Fenster verwerfen, danach kurz Ruhe.
            _zeit = 0;
            _bilder = 0;
            _ruheBis = _gesamt + NachRuckeln;
            return;
        }
        _zeit += dt;
        _bilder++;
        if (_zeit < Fensterdauer) return;
        LetzteFps = _bilder / _zeit;
        _zeit = 0;
        _bilder = 0;
        if (_gesamt < Schonzeit || _gesamt < _ruheBis) return;
        if (!Rueckfall)
        {
            if (dreiDLaeuft && LetzteFps < Unter)
            {
                Rueckfall = true;
                Ausfaelle++;
                _sperreBis = _gesamt + Sperre;
            }
        }
        else if (Ausfaelle < MaxAusfaelle && _gesamt >= _sperreBis && LetzteFps >= Ueber)
            Rueckfall = false;
    }
}
