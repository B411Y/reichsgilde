namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Befehle der Kartenwerkstatt mit Rückgängig und Wiederholen (Befehlsmuster)
//
//  Jede Änderung am Entwurf ist ein Befehl, der weiß, wie er sich ausführt und wie er sich zurücknimmt. Die Geschichte hält die
//  ausgeführten Befehle (höchstens 200); Rückgängig nimmt den letzten zurück, Wiederholen führt ihn erneut aus. Ein neuer Befehl
//  nach einem Rückgängig verwirft, was wiederholbar war – wie in jedem Zeichenprogramm.
//  Drei Grundbefehle reichen für alle Teile der Karte (Gebäude, Wege, Bauzonen, Naturflächen, Pflanzen): Setzen, Löschen und Ändern
//  (Werte vorher/nachher). Was in einem Schritt zusammengehört (ein Weg und der Stützpunkt, an dem er beim Nachbarn einrastet; ein
//  Pinselstrich mit vielen Pflanzen), bündelt der Sammelbefehl. Die Änderungen meldet der Entwurf erst, wenn die Geschichte den Schritt
//  eingetragen hat – so zeigt ein Knopf „Rückgängig“ im Ereignis schon den neuen Stand.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Änderung am Entwurf, die sich zurücknehmen lässt.</summary>
public interface IKartenbefehl
{
    /// <summary>Für die Knöpfe „Rückgängig: …“ (z. B. „Werkstattplatz gesetzt“).</summary>
    string Beschreibung { get; }

    void Ausfuehren(Kartenentwurf entwurf);
    void Zuruecknehmen(Kartenentwurf entwurf);
}

/// <summary>Ein neuer Teil (Gebäude, Weg, Fläche, Pflanze) an einer Stelle seiner Liste.</summary>
internal sealed class TeilSetzen(EntwurfTeil teil, int index) : IKartenbefehl
{
    public string Beschreibung => $"{teil.Bezeichnung} gesetzt";
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.Einfuegen(teil, index);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.Entfernen(teil);
}

/// <summary>Ein Teil weg; zurückgenommen steht er wieder an seiner Stelle der Reihenfolge.</summary>
internal sealed class TeilLoeschen(EntwurfTeil teil, int index) : IKartenbefehl
{
    public string Beschreibung => $"{teil.Bezeichnung} gelöscht";
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.Entfernen(teil);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.Einfuegen(teil, index);
}

/// <summary>Werte eines Teils vorher und nachher (Punkt, Name, Stützpunkte, Ecken …); der Teil selbst bleibt dasselbe Objekt.</summary>
internal sealed class TeilAendern(string beschreibung, EntwurfTeil teil, EntwurfTeil vorher, EntwurfTeil nachher) : IKartenbefehl
{
    public string Beschreibung => beschreibung;
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.Uebernehmen(teil, nachher);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.Uebernehmen(teil, vorher);

    /// <summary>Derselbe Befehl mit anderer Beschreibung.</summary>
    public TeilAendern Benannt(string text) => new(text, teil, vorher, nachher);

    /// <summary>Die Werte nachher (zum Vergleich, bevor der Befehl ausgeführt wird; nicht ändern).</summary>
    public EntwurfTeil Nachher => nachher;
}

/// <summary>Mehrere Befehle als ein Schritt (zurückgenommen in umgekehrter Reihenfolge).</summary>
internal sealed class Sammelbefehl(string beschreibung, IReadOnlyList<IKartenbefehl> befehle) : IKartenbefehl
{
    public string Beschreibung => beschreibung;

    public void Ausfuehren(Kartenentwurf entwurf)
    {
        foreach (var b in befehle) b.Ausfuehren(entwurf);
    }

    public void Zuruecknehmen(Kartenentwurf entwurf)
    {
        for (var i = befehle.Count - 1; i >= 0; i--) befehle[i].Zuruecknehmen(entwurf);
    }
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
        _entwurf.FelsStrichBeenden(); // ein laufender Felsstrich steht schon in der Datei: erst als Schritt eintragen, dann merken
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
        _entwurf.MeldungenAusloesen();
    }

    public bool Rueckgaengig()
    {
        _entwurf.FelsStrichBeenden(); // ein laufender Felsstrich wird zuerst ein Schritt (seine Indizes gelten nur auf seinem Stand)
        if (_zurueck.Last is not { } letzter) return false;
        _zurueck.RemoveLast();
        letzter.Value.Zuruecknehmen(_entwurf);
        _vor.Push(letzter.Value);
        _entwurf.MeldungenAusloesen();
        return true;
    }

    public bool Wiederholen()
    {
        _entwurf.FelsStrichBeenden();
        if (!_vor.TryPop(out var befehl)) return false;
        befehl.Ausfuehren(_entwurf);
        _zurueck.AddLast(befehl);
        _entwurf.MeldungenAusloesen();
        return true;
    }
}
