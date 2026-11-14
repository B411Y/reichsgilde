namespace Reichsgilde;

/// <summary>
/// Die Logik der zentralen Einblendung (<see cref="EinblendungAnzeige"/>) ohne Godot, damit sie sich testen lässt (<c>EinblendungsreiheTests</c>):
/// eine Reihe wartender Meldungen, höchstens eine läuft. Solange ein großes Fenster oder ein Brief offen ist (<see cref="Ruht"/>), ist die Einblendung
/// unsichtbar, die laufende <b>hält an</b> (sie wandert nicht weiter und kommt nicht an) und keine neue beginnt; ist das Fenster zu, geht es dort weiter,
/// wo es stand. Keine Meldung geht verloren: Nur wenn schon zu viele warten, kommt eine neue ohne Einblendung gleich in die Ecke.
/// </summary>
public sealed class Einblendungsreihe<T> where T : class
{
    private readonly Queue<T> _warten = new();

    /// <summary>Die Meldung, die gerade eingeblendet wird (null: keine).</summary>
    public T? Laufend { get; private set; }

    /// <summary>Ein großes Fenster oder ein Brief ist offen.</summary>
    public bool Ruht { get; private set; }

    /// <summary>Ist die Einblendung zu sehen? Nicht, solange etwas ruht.</summary>
    public bool Sichtbar => !Ruht;

    /// <summary>Hält die laufende Einblendung gerade an (Tween pausiert)?</summary>
    public bool Angehalten => Ruht && Laufend is not null;

    public int Wartend => _warten.Count;

    /// <summary>Reiht ein; false, wenn schon <paramref name="hoechstens"/> warten – dann kommt die Meldung ohne Einblendung gleich in die Ecke.</summary>
    public bool Einreihen(T eintrag, int hoechstens)
    {
        if (_warten.Count >= hoechstens) return false;
        _warten.Enqueue(eintrag);
        return true;
    }

    /// <summary>Die nächste Meldung, die jetzt beginnen darf (nichts läuft, nichts ruht); sonst null.</summary>
    public T? Beginnen()
    {
        if (Laufend is not null || Ruht || !_warten.TryDequeue(out var e)) return null;
        Laufend = e;
        return e;
    }

    /// <summary>Die laufende ist in der Ecke angekommen.</summary>
    public void Fertig(T eintrag)
    {
        if (ReferenceEquals(Laufend, eintrag)) Laufend = null;
    }

    /// <summary>Setzt, ob etwas ruht; true, wenn sich etwas geändert hat (dann anhalten bzw. fortsetzen).</summary>
    public bool RuhtSetzen(bool ruht)
    {
        if (Ruht == ruht) return false;
        Ruht = ruht;
        return true;
    }
}
