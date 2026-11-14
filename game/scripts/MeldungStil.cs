using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Brücke zum Thema der Meldungen (<c>res://scenes/meldung_thema.tres</c>), gebaut wie <see cref="RatStil"/>: Aussehen steht nur dort,
/// dieser Code kennt nur die <b>Namen</b> der Typ-Varianten („MeldungVerlaufKnopf“ …) und der Maße – nie eine Farbe, Größe oder einen Rand.
/// </summary>
public static class MeldungStil
{
    private static Theme? _thema;

    /// <summary>Das Thema, einmal geladen.</summary>
    public static Theme Thema => _thema ??= GD.Load<Theme>("res://scenes/meldung_thema.tres");

    public const string Masse = "MeldungMasse";

    /// <summary>Ein Maß der Familie (z. B. „verlauf_breite“).</summary>
    public static int Mass(string name) => Thema.GetConstant(name, Masse);
}
