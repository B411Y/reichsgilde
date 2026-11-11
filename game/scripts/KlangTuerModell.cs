namespace Reichsgilde;

/// <summary>Welcher Türklang zu welchem Übergang gehört (ohne Godot, damit er sich testen lässt: <c>KlangTuerTests.cs</c>).</summary>
public enum TuerKlang { Auf, Zu }

public static class KlangTuerModell
{
    /// <summary>Hinein (Betreten) = Tür auf, hinaus (Verlassen) = Tür zu.</summary>
    public static TuerKlang Fuer(UebergangRichtung richtung) => richtung == UebergangRichtung.Betreten ? TuerKlang.Auf : TuerKlang.Zu;
}
