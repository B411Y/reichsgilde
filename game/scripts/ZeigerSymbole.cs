namespace Reichsgilde;

/// <summary>Wo der Wechsel Hand ↔ Messing-I gerade steht (<see cref="SymbolWechsel"/>).</summary>
public enum SymbolZustand { Hand, HandSchrumpft, IWaechst, I, ISchrumpft, HandWaechst }

/// <summary>
/// Der Wechsel des 3D-Zeigers über Textfeldern, ohne Godot (Tests: <c>ZeigerSymboleTests.cs</c>): Kommt die Maus über ein Textfeld, schrumpft die Hand
/// (um die Fingerspitze) bis sie weg ist, dann wächst der Messing-Textcursor (römisches I) von klein auf normal; verlässt die Maus das Textfeld, schrumpft
/// das I bis weg, dann wächst die Hand wieder. Je Richtung <see cref="DauerSekunden"/>, Kurve Smoothstep wie die übrigen Übergänge (<see cref="SchlossEinwuchs.Sanft"/>).
/// Intern ist alles ein Weg 0..2 (0 = Hand voll, 1 = beides weg, 2 = I voll), der mit fester Geschwindigkeit zum Ziel läuft: eine Umkehr mitten im
/// Wachsen oder Schrumpfen geht vom selben Punkt zurück, ohne Sprung. Hand und I sind nie zugleich sichtbar.
/// </summary>
public sealed class SymbolWechsel
{
    /// <summary>Dauer je Richtung (Hand schrumpfen, I wachsen, I schrumpfen, Hand wachsen), Wunsch des Nutzers 0,1 s (doppelt so schnell wie zuerst 0,2 s).</summary>
    public const double DauerSekunden = 0.1;

    /// <summary>0 = Hand voll da, 1 = Hand und I weg, 2 = I voll da.</summary>
    public double Weg { get; private set; }

    /// <summary>Steht die Maus (im letzten Schritt) über einem Textfeld?</summary>
    public bool UeberText { get; private set; }

    /// <summary>Ein Bild weiter: <paramref name="ueberText"/> = die Maus steht über einem Textfeld.</summary>
    public void Schritt(double dt, bool ueberText)
    {
        UeberText = ueberText;
        var ziel = ueberText ? 2.0 : 0.0;
        var schritt = Math.Max(dt, 0) / DauerSekunden;
        Weg = ziel > Weg ? Math.Min(ziel, Weg + schritt) : Math.Max(ziel, Weg - schritt);
    }

    /// <summary>Größe der Hand (1 = normal, 0 = weg).</summary>
    public double HandGroesse => Weg >= 1 ? 0 : 1 - SchlossEinwuchs.Sanft(Weg);

    /// <summary>Größe des Messing-I (1 = normal, 0 = weg).</summary>
    public double IGroesse => Weg <= 1 ? 0 : SchlossEinwuchs.Sanft(Weg - 1);

    /// <summary>Zeigerpunkt ist die Mitte des I-Balkens (sonst die Fingerspitze); gewechselt wird, wenn beides weg ist, darum springt nichts Sichtbares.</summary>
    public bool ZeigerpunktIstI => Weg >= 1;

    public SymbolZustand Zustand =>
        Weg <= 0 ? SymbolZustand.Hand : Weg >= 2 ? SymbolZustand.I
        : Weg < 1 ? (UeberText ? SymbolZustand.HandSchrumpft : SymbolZustand.HandWaechst)
        : UeberText ? SymbolZustand.IWaechst : SymbolZustand.ISchrumpft;
}

/// <summary>Welche Marke neben der Fingerspitze steht: das Fragezeichen bei einem Tooltip, die Windrose über einer verschiebbaren Fläche (Stammbaum); beide am selben Platz, die Windrose geht vor.</summary>
public static class ZeigerMarken
{
    public static (bool Fragezeichen, bool Windrose) Waehlen(bool hilfe, bool verschiebbar) => (hilfe && !verschiebbar, verschiebbar);
}
