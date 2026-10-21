namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Befehle der Kartenwerkstatt mit Rückgängig und Wiederholen (Befehlsmuster)
//
//  Jede Änderung am Entwurf ist ein Befehl, der weiß, wie er sich ausführt und wie er sich zurücknimmt. Die Geschichte hält die
//  ausgeführten Befehle (höchstens 200); Rückgängig nimmt den letzten zurück, Wiederholen führt ihn erneut aus. Ein neuer Befehl
//  nach einem Rückgängig verwirft, was wiederholbar war – wie in jedem Zeichenprogramm.
//  Schritt 2 und 3 (Wege, Flächen, Mauer, Start) bekommen eigene Befehle nach demselben Muster.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Änderung am Entwurf, die sich zurücknehmen lässt.</summary>
public interface IKartenbefehl
{
    /// <summary>Für die Knöpfe „Rückgängig: …“ (z. B. „Werkstattplatz gesetzt“).</summary>
    string Beschreibung { get; }

    void Ausfuehren(Kartenentwurf entwurf);
    void Zuruecknehmen(Kartenentwurf entwurf);
}

/// <summary>Ein neues Gebäude an einer Stelle der Reihenfolge.</summary>
internal sealed class GebaeudeSetzen(EntwurfGebaeude gebaeude, int index) : IKartenbefehl
{
    public string Beschreibung => $"{gebaeude.Name} gesetzt";
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.Einfuegen(gebaeude, index);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.Entfernen(gebaeude);
}

/// <summary>Ein Gebäude weg; zurückgenommen steht es wieder an seiner Stelle der Reihenfolge.</summary>
internal sealed class GebaeudeLoeschen(EntwurfGebaeude gebaeude, int index) : IKartenbefehl
{
    public string Beschreibung => $"{gebaeude.Name} gelöscht";
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.Entfernen(gebaeude);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.Einfuegen(gebaeude, index);
}

/// <summary>Werte eines Gebäudes vorher und nachher (Punkt, Drehung, Name, Betrieb …); das Gebäude selbst bleibt dasselbe Objekt.</summary>
internal sealed class GebaeudeAendern(string beschreibung, EntwurfGebaeude gebaeude, EntwurfGebaeude vorher, EntwurfGebaeude nachher) : IKartenbefehl
{
    public string Beschreibung => beschreibung;
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.Uebernehmen(gebaeude, nachher);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.Uebernehmen(gebaeude, vorher);
}

/// <summary>Rückgängig und Wiederholen für einen Entwurf.</summary>
public sealed class Kartengeschichte
{
    /// <summary>So viele Schritte lassen sich zurücknehmen; ältere fallen weg.</summary>
    public const int Grenze = 200;

    private readonly Kartenentwurf _entwurf;
    private readonly LinkedList<IKartenbefehl> _zurueck = new();
    private readonly Stack<IKartenbefehl> _vor = new();

    /// <summary>Der Stand beim letzten Speichern: der Befehl, der da zuoberst lag (null: der Stand beim Öffnen).</summary>
    private IKartenbefehl? _gespeichert;

    /// <summary>Der Stand, auf den alles Rückgängig zurückführt: der zuletzt an der Grenze weggefallene Befehl (null: beim Öffnen).</summary>
    private IKartenbefehl? _boden;

    /// <summary>Der gespeicherte Stand ist nicht mehr erreichbar (überschrieben oder an der Grenze weggefallen).</summary>
    private bool _gespeichertVerloren;

    internal Kartengeschichte(Kartenentwurf entwurf) => _entwurf = entwurf;

    public bool KannRueckgaengig => _zurueck.Count > 0;
    public bool KannWiederholen => _vor.Count > 0;

    /// <summary>Was Rückgängig zurücknähme (für den Knopf) – oder null.</summary>
    public string? RueckgaengigText => _zurueck.Last?.Value.Beschreibung;

    /// <summary>Was Wiederholen wiederholte – oder null.</summary>
    public string? WiederholenText => _vor.Count > 0 ? _vor.Peek().Beschreibung : null;

    /// <summary>Wie viele Schritte sich zurücknehmen lassen.</summary>
    public int Schritte => _zurueck.Count;

    /// <summary>Der jetzige Stand, benannt nach dem Befehl, der ihn hergestellt hat.</summary>
    private IKartenbefehl? Stand => _zurueck.Last?.Value ?? _boden;

    /// <summary>Ist der Entwurf seit dem Öffnen bzw. dem letzten <see cref="Gespeichert"/> geändert (Rückfrage beim Schließen)?</summary>
    public bool IstGeaendert => _gespeichertVerloren || !ReferenceEquals(Stand, _gespeichert);

    /// <summary>Merkt sich den jetzigen Stand als gespeichert.</summary>
    public void Gespeichert()
    {
        _gespeichert = Stand;
        _gespeichertVerloren = false;
    }

    /// <summary>Führt einen Befehl aus und legt ihn in die Geschichte; was wiederholbar war, fällt weg.</summary>
    public void Ausfuehren(IKartenbefehl befehl)
    {
        befehl.Ausfuehren(_entwurf);
        // Ein gespeicherter Stand, der nur noch durch Wiederholen erreichbar war, ist jetzt verloren.
        if (_gespeichert is not null && _vor.Contains(_gespeichert)) _gespeichertVerloren = true;
        _vor.Clear();
        _zurueck.AddLast(befehl);
        if (_zurueck.Count > Grenze)
        {
            if (ReferenceEquals(_boden, _gespeichert)) _gespeichertVerloren = true;
            _boden = _zurueck.First!.Value;
            _zurueck.RemoveFirst();
        }
    }

    public bool Rueckgaengig()
    {
        if (_zurueck.Last is not { } letzter) return false;
        _zurueck.RemoveLast();
        letzter.Value.Zuruecknehmen(_entwurf);
        _vor.Push(letzter.Value);
        return true;
    }

    public bool Wiederholen()
    {
        if (!_vor.TryPop(out var befehl)) return false;
        befehl.Ausfuehren(_entwurf);
        _zurueck.AddLast(befehl);
        return true;
    }
}
