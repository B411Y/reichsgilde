using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Bodentexturen im Kartenentwurf (Kartenwerkstatt, Schritt 5e)
//
//  Wie das Gelände (Kartenentwurf.Gelaende.cs): Das Bodenraster (Bodenraster, Feld „boden“) ändert nur der Texturpinsel (Bodenpinsel) –
//  jeder Strich ist ein Schritt mit Rückgängig, der sich nur die berührten Kacheln (64 × 64 m) gepackt merkt. Beim Ziehen ändert sich das
//  Raster sofort, erst „Beenden“ legt den Schritt in die Geschichte. Jede Änderung wird als Kartenteil „Boden“ mit ihrem Rechteck gemeldet;
//  dort ändern sich auch Splatmap und Deko (Bodendeko).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Der Boden als Teil der Karte – nur für die Meldung <see cref="Kartenaenderung"/> (das Rechteck steht in ihrem Bereich).</summary>
public sealed class EntwurfBoden : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Boden;
    public override string Bezeichnung => "Boden";

    private static InvalidOperationException Kacheln() => new("Der Boden ändert sich kachelweise (Kartenentwurf.Boden.cs), nicht als Teil.");
    internal override EntwurfTeil KopieTeil() => throw Kacheln();
    internal override void UebernimmTeil(EntwurfTeil werte) => throw Kacheln();
    internal override bool GleicheWerte(EntwurfTeil anderer) => ReferenceEquals(this, anderer);
    internal override JsonObject AlsJson() => throw Kacheln();
}

/// <summary>Kacheln des Bodenrasters vorher und nachher (gepackt) – ein Texturstrich als Schritt.</summary>
internal sealed class BodenAendern : IKartenbefehl
{
    private readonly List<((int I, int J) Kachel, string?[]? Vorher, string?[]? Nachher)> _kacheln;
    private readonly Kartenrechteck _bereich;

    public BodenAendern(string beschreibung, List<((int I, int J) Kachel, string?[]? Vorher, string?[]? Nachher)> kacheln, Kartenrechteck bereich) =>
        (Beschreibung, _kacheln, _bereich) = (beschreibung, kacheln, bereich);

    public string Beschreibung { get; }
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.BodenKachelnSetzen(_kacheln.Select(k => (k.Kachel, k.Nachher)), _bereich);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.BodenKachelnSetzen(_kacheln.Select(k => (k.Kachel, k.Vorher)), _bereich);
}

public sealed partial class Kartenentwurf
{
    /// <summary>Das gespeicherte Bodenraster (Feld <c>boden</c>). Nur lesen – ändern über den Texturpinsel.</summary>
    public Bodenraster Boden => _boden;

    /// <summary>Der Teil, mit dem Änderungen am Boden gemeldet werden.</summary>
    public EntwurfBoden BodenTeil => _bodenTeil;

    /// <summary>
    /// Die Deko der Bodentexturen, wie das Spiel sie zeigt (<see cref="Bodendeko"/>): einmal je Änderung bauen, dann kachelweise abfragen.
    /// <paramref name="frei"/> aus <see cref="Freiraum"/>.
    /// </summary>
    public Bodendeko BodendekoVon(Naturfreiraum frei) => new(_boden, Katalog, frei);

    private Bodenstrich? _bodenstrich;

    /// <summary>Läuft gerade ein Texturstrich?</summary>
    public bool BodenStrichLaeuft => _bodenstrich is not null;

    /// <summary>Beginnt einen Texturstrich (ein laufender wird vorher beendet). Danach <see cref="BodenStrichWeiter"/> je Mausbewegung.</summary>
    public void BodenStrichBeginnen(Bodenpinsel pinsel)
    {
        BodenStrichBeenden();
        _bodenstrich = new Bodenstrich(_boden, pinsel, Kartengrenzen.Aussen, _hoehen);
    }

    /// <summary>
    /// Führt den Strich bis zu diesem Punkt weiter (der erste Punkt setzt den ersten Tupfer). Das Raster ändert sich sofort; gemeldet wird
    /// das geänderte Rechteck (Kartenteil Boden). Gibt es zurück – null: nichts geändert.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ohne <see cref="BodenStrichBeginnen"/>.</exception>
    public Kartenrechteck? BodenStrichWeiter(Kartenort p)
    {
        var strich = _bodenstrich ?? throw new InvalidOperationException("Es läuft kein Texturstrich (erst BodenStrichBeginnen).");
        var bereich = strich.Weiter(p);
        if (bereich is { } b)
        {
            Vormerken(new Kartenaenderung(Aenderungsart.Geaendert, _bodenTeil, b));
            MeldungenAusloesen();
        }
        return bereich;
    }

    /// <summary>Beendet den Strich: Er wird ein Schritt in der Geschichte. False: es lief keiner oder er änderte nichts.</summary>
    public bool BodenStrichBeenden()
    {
        if (_bodenstrich is not { } strich) return false;
        _bodenstrich = null;
        var kacheln = strich.Aenderung.Kacheln();
        if (kacheln.Count == 0 || strich.Aenderung.Bereich is not { } bereich) return false;
        var text = strich.Pinsel.IstRadierer ? "Boden radiert" : $"{Katalog.TexturDaten(strich.Pinsel.Textur)?.Name ?? strich.Pinsel.Textur.ToString()} gemalt";
        Geschichte.Ausfuehren(new BodenAendern(text, kacheln, bereich));
        return true;
    }

    /// <summary>Ein ganzer Texturstrich in einem Aufruf (Kartenskript, Tests): Beginnen, alle Punkte, Beenden – ein Schritt.</summary>
    public bool BodenMalen(Bodenpinsel pinsel, IReadOnlyList<Kartenort> strich)
    {
        BodenStrichBeginnen(pinsel);
        foreach (var p in strich) BodenStrichWeiter(p);
        return BodenStrichBeenden();
    }

    /// <summary>Setzt Kacheln des Bodenrasters (für den Befehl) und meldet das Rechteck.</summary>
    internal void BodenKachelnSetzen(IEnumerable<((int I, int J) Kachel, string?[]? Werte)> kacheln, Kartenrechteck bereich)
    {
        foreach (var (k, w) in kacheln) _boden.KachelSetzen(k, w);
        Vormerken(new Kartenaenderung(Aenderungsart.Geaendert, _bodenTeil, bereich));
    }
}
