namespace Reichsgilde;

/// <summary>Die vier Haltungen des 3D-Handschuhs (Text: die Kante der flachen Hand zeigt zum Betrachter, über Textfeldern) (Namen = Namen der Schleifen in der .glb).</summary>
public enum HandPose { Ruhe, Zeigen, Greifen, Text, Bereit, Winken }

/// <summary>
/// Die reine Ablauflogik des 3D-Handschuhs (<see cref="HandschuhZeiger"/>) ohne Godot, damit sie sich testen lässt (<c>HandschuhModellTests.cs</c>):
/// welche Haltung die Hand haben soll, in welcher Reihenfolge Übergänge und das Tippen laufen und ob der 3D-Handschuh gilt.
/// Namen der Clips stehen in der .glb: Schleifen <c>Ruhe</c>, <c>Zeigen</c>, <c>Greifen</c>, Einmalclips <c>Tippen</c> und <c>Uebergang_&lt;von&gt;_&lt;nach&gt;</c>;
/// ihre Längen liest der Spielcode aus der Datei (keine festen Bildzahlen).
/// </summary>
public static class HandschuhModell
{
    /// <summary>Ab dieser Zeigergröße (Pixel) gilt der 3D-Handschuh; darunter bleibt der 2D-Zeiger (Vorgabe des Auftrags).</summary>
    public const int MindestGroesse = 48;

    /// <summary>Wie weit (Pixel) die Maus bei gedrückter Taste wandern muss, bis aus dem Klick ein Ziehen (Greifen) wird.</summary>
    public const double ZiehSchwelle = 5.0;

    /// <summary>Abspieltempo der Einmalclips (1 = wie in der Datei): Übergänge Ruhe/Zeigen/Greifen, etwa 45 % kürzer (vorher 1,0).</summary>
    public const double UebergangTempo = 1.8;

    /// <summary>Wunschdauer (Sekunden) der Übergänge in die Ziehen-Faust und zurück (Greifen; vorher bei Tempo 1,8 etwa 0,69 s Ruhe→Greifen, 0,56 s Greifen→Ruhe, 0,28 s Zeigen↔Greifen); das Tempo wird aus der Cliplänge der Datei berechnet.</summary>
    public const double FaustUebergangSekunden = 0.12;

    /// <summary>Abspieltempo des Klick-Tippens, etwa 45 % kürzer (vorher 1,5).</summary>
    public const double TippenTempo = 2.7;

    /// <summary>Überblendzeit (Sekunden) in einen Einmalclip (vorher 0,06) und zurück in die Schleife (vorher 0,10).</summary>
    public const double BlendEinmal = 0.03, BlendSchleife = 0.05;

    /// <summary>Gilt der 3D-Handschuh? Allein die Einstellung, die Datei und die Zeigergröße entscheiden; die Bildrate nie (Nutzer, 11.11.2026).</summary>
    public static bool Gilt(bool einstellungAn, bool dateiDa, int groesse) => einstellungAn && dateiDa && groesse >= MindestGroesse;

    /// <summary>Welche Haltung gewünscht ist: Ziehen = Greifen, sonst über Anklickbarem Zeigen, sonst Ruhe.</summary>
    public static HandPose Wunsch(bool ziehen, bool ueberAnklickbarem) => Wunsch(ziehen, ueberAnklickbarem, false);

    /// <summary>Wie oben, mit Text: über einem Textfeld (heute der I-Strich-Zeiger) steht die Hand in der Haltung Text, außer beim Ziehen.</summary>
    public static HandPose Wunsch(bool ziehen, bool ueberAnklickbarem, bool ueberText) => Wunsch(ziehen, ueberAnklickbarem, ueberText, false);

    /// <summary>Wie oben, mit Tür: über einem betretbaren Gebäude steht die Hand in der Haltung Bereit (offene Hand über dem Türschloss) statt Zeigen.</summary>
    public static HandPose Wunsch(bool ziehen, bool ueberAnklickbarem, bool ueberText, bool ueberTuer) => Wunsch(ziehen, ueberAnklickbarem, ueberText, ueberTuer, false, false);

    /// <summary>
    /// Wie oben, mit Auswahlrechteck und Winken: Zieht der Spieler ein <b>Auswahlrechteck</b> (<paramref name="rahmen"/>), zeigt der Handschuh Zeigen statt der Ziehen-Faust; Dinge greifen und verschieben (Ware, Fenster, Kamera) bleiben bei der Faust.
    /// <paramref name="winken"/> (Easter Egg, <see cref="WinkenLogik"/>) hat Vorrang vor allem.
    /// </summary>
    public static HandPose Wunsch(bool ziehen, bool ueberAnklickbarem, bool ueberText, bool ueberTuer, bool rahmen, bool winken) =>
        winken ? HandPose.Winken : ziehen && rahmen ? HandPose.Zeigen : ziehen ? HandPose.Greifen : ueberText ? HandPose.Text : ueberTuer ? HandPose.Bereit : ueberAnklickbarem ? HandPose.Zeigen : HandPose.Ruhe;

    /// <summary>Gibt es einen Übergangsclip zwischen den Haltungen? Greifen liegt nicht neben Text und Bereit (Weg über Ruhe), mit Winken schon.</summary>
    public static bool HatUebergang(HandPose von, HandPose nach) =>
        von != nach && !(von == HandPose.Greifen && nach is HandPose.Text or HandPose.Bereit) && !(nach == HandPose.Greifen && von is HandPose.Text or HandPose.Bereit);

    /// <summary>Zweiter Name eines Übergangsclips (<c>Ruhe_Text</c> statt <c>Uebergang_Ruhe_Text</c>), falls die Datei die Aktionen kürzer nennt.</summary>
    public static string UebergangKurz(HandPose von, HandPose nach) => $"{von}_{nach}";

    /// <summary>Zeitkonstante (Sekunden), mit der der Zeigerpunkt von der Fingerspitze zum Textpunkt gleitet (und zurück), damit die Hand beim Wechsel nicht springt.</summary>
    public const double TextGewichtZeit = 0.08;

    /// <summary>Position der Zeichenfläche des Handschuhs in einem Unterfenster: Mausposition des Fensters minus Zeigerpunkt im Bild (beides in Einheiten dieses Fensters), damit die Fingerspitze genau auf der Maus liegt.</summary>
    public static (double X, double Y) FensterPosition(double mausX, double mausY, double imBildX, double imBildY) => (mausX - imBildX, mausY - imBildY);

    /// <summary>Drehung der Bühnenkamera um die Hochachse (Grad): der Zeigefinger steht im Spielbild schräg nach links oben (Haltung Zeigen, Ruhe, Greifen).</summary>
    public const double KameraDrehungGrad = -40.0;

    /// <summary>Eigene Drehung der Hand je Haltung (Grad um die Hochachse, <paramref name="textGewicht"/> 0 = andere Haltungen, 1 = Text): in der Haltung Text dreht die Hand mit der Kamera, damit sie aufrecht steht (die Pose selbst bleibt, wie der Nutzer sie gebaut hat).</summary>
    public static double PoseDrehungGrad(double textGewicht, double textDrehungGrad = KameraDrehungGrad) => textDrehungGrad * Math.Clamp(textGewicht, 0.0, 1.0);

    /// <summary>
    /// Sucht die Drehung der Hand um die Hochachse (Grad, -180 bis 180), bei der <paramref name="richtung"/> (Bildrichtung von Handgelenk zu Fingerspitze bei dieser Drehung, y nach unten) genau nach oben zeigt:
    /// Stulpe unten, Finger oben. Grob in 1-Grad-Schritten über den ganzen Kreis, dann fein (0,05 Grad).
    /// </summary>
    public static double AufrechtDrehungGrad(Func<double, (double X, double Y)> richtung)
    {
        static double Fehler((double X, double Y) v)
        {
            var laenge = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            return laenge < 1e-9 ? double.MaxValue : Math.Acos(Math.Clamp(-v.Y / laenge, -1.0, 1.0)); // Winkel zur Richtung nach oben (0, -1)
        }
        var beste = 0.0;
        var fehler = double.MaxValue;
        for (var g = -180.0; g <= 180.0; g += 1.0) { var f = Fehler(richtung(g)); if (f < fehler) { fehler = f; beste = g; } }
        for (var g = beste - 1.0; g <= beste + 1.0; g += 0.05) { var f = Fehler(richtung(g)); if (f < fehler) { fehler = f; beste = g; } }
        return beste;
    }

    /// <summary>Wie schräg (Grad) die Finger im Bild stehen, bei Kameradrehung und Handdrehung um die Hochachse: 0 = aufrecht.</summary>
    public static double FingerBildwinkelGrad(double kameraGrad, double handGrad) => Math.Abs(handGrad - kameraGrad);

    public static string Schleife(HandPose pose) => pose == HandPose.Bereit ? KlinkeFolge.ClipBereit : pose.ToString();

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

    /// <summary>Ein Klick, aus dem ein Ziehen wurde (Auswahlrechteck, Greifen), tippt nicht mehr.</summary>
    public void TippenVerwerfen() => _tippen = false;

    /// <summary>Setzt die Haltung von außen (nach der Klinke-Folge: die Hand steht wieder in Bereit).</summary>
    public void PoseSetzen(HandPose pose)
    {
        Pose = pose;
        EinmalLaeuft = false;
        _tippen = false;
    }

    /// <summary>Welcher Clip jetzt starten soll (Einmalclip), oder null, wenn nichts zu tun ist (die Schleife der Haltung läuft weiter).</summary>
    public string? Entscheiden(HandPose wunsch)
    {
        if (EinmalLaeuft) return null;
        if (wunsch == HandPose.Greifen) _tippen = false;
        if (wunsch == HandPose.Bereit && Pose == HandPose.Bereit) _tippen = false; // ein Klick auf die Tür tippt nicht: die Klinke-Folge folgt (KlinkeFolge)
        if (_tippen)
        {
            if (Pose != HandPose.Zeigen) return Starten(HandschuhModell.Uebergang(Pose, HandPose.Zeigen), HandPose.Zeigen);
            _tippen = false;
            return Starten("Tippen", HandPose.Zeigen);
        }
        if (Pose == wunsch) return null;
        // Greifen und Text haben keinen gemeinsamen Clip: erst zurück in Ruhe, dann weiter
        var ziel = HandschuhModell.HatUebergang(Pose, wunsch) ? wunsch : HandPose.Ruhe;
        return Starten(HandschuhModell.Uebergang(Pose, ziel), ziel);
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
