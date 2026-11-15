using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Start in den Bauzonen (Entscheidung des Nutzers vom 15.11.2026: „nur noch Bauzonen“)
//
//  Es gibt keine Hofstellen, Startplätze und Bauplätze mehr. Wohnhaus und Werkstatt eines Hauses stehen von Anfang an auf Grundstücken in
//  den Bauzonen – genau wie spätere Neubauten:
//
//    • KI-Häuser (und der Spieler ohne Wahl) bekommen ihre Stellen aus dem Los (Partie/Startortlos.cs): StartwohnhausSetzen und
//      StartwerkstattSetzen legen die Grundstücke an – sofort fertig, kostenlos.
//    • Der Spieler mit Startwahl (NeuePartie.Startwahl) hat anfangs weder Wohnhaus noch Werkstatt. Solange die Wahl offen ist
//      (StartwahlVon), ist sein Baubefehl die Startform: erst WohnhausBauen (seine Stufe), dann WerkstattBauen (die Werkstatt seines
//      Berufs) – je einmal, sofort fertig, kostenlos, an jeder Stelle, an der auch ein Neubau stehen dürfte. Mit der Werkstatt entsteht
//      der Betrieb mit seinen Startleuten. Die Zeit hält die Oberfläche an; der Kern verlangt es nicht.
//    • Alte Spielstände: Werkstätten auf Werkstattplätzen der Karte werden beim Laden Grundstücke derselben Fläche (AltePlaetzeUmwandeln).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die offene Startwahl eines Hauses (<see cref="Bauwesen.StartwahlVon"/>).</summary>
/// <param name="WohnhausOffen">Wahr: Als Nächstes wählt das Haus den Platz seines Wohnhauses; falsch: den seiner Werkstatt.</param>
/// <param name="Werkstatt">Die Werkstatt, die das Haus danach setzt (die seines Startberufs).</param>
/// <param name="Vorlage">Der Baubefehl, der jetzt dran ist (ohne Ort): für <see cref="Bauwesen.Vorschau"/>, <see cref="Bauwesen.Einrasten"/>
/// und <see cref="Bauwesen.Bauorte"/>; mit Ort und Drehung ist er der Befehl der Startform.</param>
public sealed record Startwahl(bool WohnhausOffen, Betriebsart Werkstatt, BauBefehl Vorlage);

public sealed partial class Bauwesen
{
    /// <summary>Häuser, die den Platz von Wohnhaus und Werkstatt noch wählen, mit der Werkstatt ihres Startberufs (in der Reihenfolge des Eröffnens).</summary>
    private readonly List<(Konto Konto, Betriebsart Art)> _startwahl = new();

    /// <summary>Liegt diese Lage im Stadtinneren (innerhalb der Mauern)? Kern und Mauerring – dort lost das Startortlos die Häuser.</summary>
    public static bool ImStadtinneren(Lage lage) => lage is Lage.Kern or Lage.Mauerring;

    /// <summary>
    /// Die offene Startwahl dieses Hauses – oder null (KI-Häuser, Spieler ohne Wahl, oder beides ist gesetzt). Solange sie offen ist, baut das
    /// Haus nur die Startform (<see cref="Startwahl.Vorlage"/>): sofort fertig und kostenlos.
    /// </summary>
    public Startwahl? StartwahlVon(Konto konto)
    {
        foreach (var (k, art) in _startwahl)
        {
            if (k != konto) continue;
            var wohnhaus = Wohnhaus(konto) is null;
            return new Startwahl(wohnhaus, art, wohnhaus ? new WohnhausBauen(konto.WohnhausStufe, 0, 0, 0) : new WerkstattBauen(art, 0, 0, 0));
        }
        return null;
    }

    /// <summary>
    /// Die Stellen, an denen dieses Haus den Teil seiner Startwahl setzen darf, der jetzt dran ist (wie <see cref="Bauorte"/> für
    /// <see cref="Startwahl.Vorlage"/>) – leer ohne offene Startwahl.
    /// </summary>
    public IReadOnlyList<BauBefehl> Startorte(Konto konto) =>
        StartwahlVon(konto) is { } wahl ? Bauorte(konto, wahl.Vorlage) : Array.Empty<BauBefehl>();

    /// <summary>Beim Aufbau der Partie: Dieses Haus wählt Wohnhaus und Werkstatt selbst (ohne Bauwesen geschieht nichts).</summary>
    internal void StartwahlEroeffnen(Konto konto, Betriebsart werkstatt)
    {
        if (IstAktiv && _startwahl.All(s => s.Konto != konto)) _startwahl.Add((konto, werkstatt));
    }

    /// <summary>Warum dieser Befehl nicht die Startform ist, die jetzt dran ist – oder null.</summary>
    private string? StartformVerboten(Konto konto, Startwahl wahl, BauBefehl befehl)
    {
        if (wahl.WohnhausOffen)
        {
            var stufen = _w.Wohnen.Daten!.Stufen;
            var name = stufen[Math.Clamp(konto.WohnhausStufe, 0, stufen.Count - 1)].Name;
            return befehl is WohnhausBauen w && w.Stufe == konto.WohnhausStufe ? null
                : $"{konto.Inhaber} wählt zuerst den Platz für das Wohnhaus ({name}), danach den der Werkstatt.";
        }
        return befehl is WerkstattBauen b && b.Art == wahl.Werkstatt ? null
            : $"{konto.Inhaber} wählt jetzt den Platz für die Werkstatt ({(_w.Daten.KenntArt(wahl.Werkstatt) ? _w.Daten.Art(wahl.Werkstatt).Name : wahl.Werkstatt.ToString())}).";
    }

    /// <summary>Die Startform (geprüft in <see cref="Bauen"/>): das Wohnhaus bzw. die Werkstatt steht sofort, nichts wird bezahlt.</summary>
    private Befehlsergebnis StartformBauen(Konto konto, Startwahl wahl, BauBefehl befehl)
    {
        var flaeche = Grundriss(befehl);
        if (wahl.WohnhausOffen)
        {
            var haus = StartwohnhausSetzen(konto, flaeche);
            BauFertig?.Invoke(haus);
            return Befehlsergebnis.Ok($"{konto.Inhaber} bezieht das {_w.Wohnen.Stufe(konto)!.Name}. Jetzt den Platz der Werkstatt wählen.");
        }
        var name = NeuerName(wahl.Werkstatt, flaeche);
        var betrieb = _w.BetriebEroeffnen(name, wahl.Werkstatt, konto);
        var g = StartwerkstattSetzen(name, wahl.Werkstatt, flaeche);
        _w.Einstellen(betrieb, _w.Personal.AnfaengerFuer(wahl.Werkstatt, "Geselle"), _w.Personal.StartAnzahl(wahl.Werkstatt, 2));
        _startwahl.RemoveAll(s => s.Konto == konto);
        BauFertig?.Invoke(g);
        _w.ErworbenMelden(betrieb);
        return Befehlsergebnis.Ok($"{name} von {konto.Inhaber} steht.");
    }

    /// <summary>Das Wohnhaus eines Hauses am Start: ein Grundstück an dieser Stelle, sofort bewohnt (Stufe des Hauses).</summary>
    internal Grundstueck StartwohnhausSetzen(Konto konto, Grundriss flaeche)
    {
        var g = Neu(flaeche, Grundstuecksart.Wohnhaus, null);
        g.Besitzer = konto;
        HauswerteAktualisieren(konto);
        return g;
    }

    /// <summary>Die Werkstatt eines Betriebs am Start: ein Grundstück an dieser Stelle wie eine fertig gebaute Werkstatt.</summary>
    internal Grundstueck StartwerkstattSetzen(string betrieb, Betriebsart art, Grundriss flaeche)
    {
        var g = Neu(flaeche, Grundstuecksart.Werkstatt, null);
        g.Betrieb = betrieb;
        g.Betriebsart = art;
        return g;
    }

    /// <summary>
    /// Startplatzvergabe.Vorgabe (Tests und Vergleiche auf alten Karten): das Wohnhaus auf einem Wohnhausplatz der Karte – ein Grundstück in
    /// Fläche, Lage und Kennung des Platzes, wie in einem alten Spielstand.
    /// </summary>
    internal Grundstueck StartwohnhausSetzen(Konto konto, Kartengebaeude platz)
    {
        var g = Neu(platz.Grundriss!.Value, Grundstuecksart.Wohnhaus, platz.Kennung, platz.Lage);
        g.Besitzer = konto;
        HauswerteAktualisieren(konto);
        return g;
    }

    /// <summary>Startplatzvergabe.Vorgabe: die Werkstatt auf einem Werkstattplatz der Karte – ein Grundstück wie nach <see cref="AltePlaetzeUmwandeln"/>.</summary>
    internal Grundstueck StartwerkstattSetzen(string betrieb, Betriebsart art, Kartengebaeude platz)
    {
        var g = Neu(platz.Grundriss!.Value, Grundstuecksart.Werkstatt, platz.Kennung);
        g.Betrieb = betrieb;
        g.Betriebsart = art;
        return g;
    }

    /// <summary>Wie eine Werkstatt dieser Art an dieser Stelle hieße (nach der nächsten Karrenstraße, eindeutig) – für die Werkstatt des Spielers am gelosten Ort.</summary>
    internal string Startwerkstattname(Betriebsart art, Grundriss flaeche) => NeuerName(art, flaeche);

    /// <summary>
    /// Alle Stellen, an denen am Start das Gebäude der Vorlage stehen kann (<see cref="FreieOrte"/>, feste Reihenfolge), und darunter die, deren
    /// ganze Fläche im Stadtinneren liegt (<see cref="ImStadtinneren"/>).
    /// </summary>
    internal (List<Grundriss> Alle, List<Grundriss> Innen) Startstellen(BauBefehl vorlage)
    {
        var (alle, innen) = (new List<Grundriss>(), new List<Grundriss>());
        if (!IstAktiv) return (alle, innen);
        var raster = Daten!.Raster;
        var karte = Karte!;
        foreach (var ort in FreieOrte(vorlage))
        {
            var g = Grundriss(ort);
            alle.Add(g);
            // die erste Zone am Feld zählt – wie in PlatzVerboten
            if (g.Felder(raster).All(f => karte.Bauzonen.FirstOrDefault(z => z.Enthaelt(f.X, f.Z)) is { } zone && ImStadtinneren(zone.Art)))
                innen.Add(g);
        }
        return (alle, innen);
    }

    /// <summary>
    /// Nach dem Laden eines alten Spielstands (vor „nur noch Bauzonen“): Jede Werkstatt in der Stadt, die auf einem Werkstattplatz der Karte
    /// steht (ein Betrieb oder eine freie Werkstatt zum Kauf), wird ein Grundstück derselben Fläche mit der Kennung des Platzes – gleicher
    /// Betrieb, gleiche Stelle, gleiche Drehung (<see cref="PlatzVon"/> führt weiter zum Platz der Karte). Die Wohnhäuser sind schon
    /// Grundstücke. Steht dort schon ein Grundstück (neuer Spielstand, zweites Laden), geschieht nichts.
    /// </summary>
    internal void AltePlaetzeUmwandeln(IEnumerable<(string Name, Betriebsart Art)> werkstaetten)
    {
        if (!IstAktiv) return;
        foreach (var (name, art) in werkstaetten)
        {
            if (Werkstatt(name) is not null) continue;
            if (Karte!.PlatzVon(name) is not { Art: Bausteinart.Werkstattplatz, Grundriss: { } flaeche } platz) continue;
            if (_grundstuecke.Any(x => x.Kennung == platz.Kennung)) continue;
            var g = Neu(flaeche, Grundstuecksart.Werkstatt, platz.Kennung);
            g.Betrieb = name;
            g.Betriebsart = art;
        }
    }
}
