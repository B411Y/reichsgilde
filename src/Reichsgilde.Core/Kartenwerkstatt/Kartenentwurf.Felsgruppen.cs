using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Felsgruppen im Entwurf (Felspinsel P5)
//
//  Zwei Dinge heißen hier Gruppe:
//  • Die Gruppe in der Karte: eine Nummer am Stück („gruppe“, 1, 2, …; 0 fehlt in der Datei). Stücke mit derselben Nummer wählt die Werkstatt
//    zusammen („Ganze Gruppe wählen“), „Gruppe auflösen“ nimmt die Nummer weg. Ein Stempel oder Strich mit „Verbinden“ gibt den Stücken eines Treffers
//    eine neue Nummer; Kopieren gibt den Kopien eine neue (sie gehören nicht zu ihren Vorbildern). Die Nummer ändert weder Lage noch Gelände.
//  • Die Vorlage im Katalog (Bauen/Felskatalog.cs): FelsGruppenstuecke macht aus gewählten Stücken die Stücke einer eigenen Gruppe – das größte
//    wird die Mitte, die anderen liegen relativ dazu. Der Katalog liegt außerhalb der Karte; der Entwurf liest ihn nie.
//  Neue Nummern: eins über der größten in der Karte – gleicher Entwurf, gleiche Nummern (deterministisch, ohne Dictionary-Reihenfolge).
// ═════════════════════════════════════════════════════════════════════════════

public sealed partial class Kartenentwurf
{
    /// <summary>Die nächste freie Gruppennummer: eins über der größten in der Karte.</summary>
    public int FelsGruppeNeu()
    {
        var max = 0;
        foreach (var f in _felsen) max = Math.Max(max, f.Gruppe);
        return max + 1;
    }

    /// <summary>Setzt die Nummer an neuen Stücken (noch nicht im Entwurf: Stempel, Strich, Kopie).</summary>
    private static void FelsGruppeGeben(IEnumerable<EntwurfFels> neue, int nummer)
    {
        foreach (var f in neue) f.Gruppe = nummer;
    }

    /// <summary>
    /// Kopien (noch nicht im Entwurf): Kopien, die eine Nummer teilen, bekommen eine neue gemeinsame (je alte Nummer eine, in der Reihenfolge der
    /// Liste); eine Kopie allein aus ihrer Gruppe bekommt keine.
    /// </summary>
    private void FelsGruppenNeuNummern(IReadOnlyList<EntwurfFels> kopien)
    {
        var naechste = FelsGruppeNeu();
        var alte = kopien.Select(f => f.Gruppe).Where(n => n != 0).Distinct().ToList(); // Reihenfolge des ersten Auftretens
        foreach (var alt in alte)
        {
            var teil = kopien.Where(f => f.Gruppe == alt).ToList();
            FelsGruppeGeben(teil, teil.Count > 1 ? naechste++ : 0);
        }
    }

    /// <summary>
    /// Die ganzen Gruppen dieser Stücke: alle Stücke, die eine Nummer mit einem von ihnen teilen, dazu die Stücke selbst (auch ohne Gruppe) – in der
    /// Reihenfolge des Entwurfs. Zum Wählen einer ganzen Gruppe.
    /// </summary>
    public IReadOnlyList<EntwurfFels> FelsGruppeVon(IEnumerable<EntwurfFels> felsen)
    {
        var liste = Geordnet(felsen);
        var nummern = liste.Select(f => f.Gruppe).Where(n => n != 0).ToHashSet();
        var selbst = new HashSet<EntwurfFels>(liste, ReferenceEqualityComparer.Instance);
        return _felsen.Where(f => nummern.Contains(f.Gruppe) || selbst.Contains(f)).ToList(); // in der Reihenfolge des Entwurfs
    }

    /// <summary>Wie viele Gruppen (verschiedene Nummern) unter diesen Stücken sind.</summary>
    public int FelsGruppenAnzahl(IEnumerable<EntwurfFels> felsen) => felsen.Select(f => f.Gruppe).Where(n => n != 0).Distinct().Count();

    /// <summary>
    /// Verbindet Stücke zu einer Gruppe: Alle bekommen eine neue gemeinsame Nummer (wie Strg+G in Unreal). Ein Schritt mit Rückgängig; 0 und nichts
    /// geändert bei weniger als zwei Stücken im Entwurf. Gibt die Nummer zurück.
    /// </summary>
    public int FelsGruppieren(IEnumerable<EntwurfFels> felsen)
    {
        FelsStrichBeenden();
        var liste = Geordnet(felsen);
        if (liste.Count < 2) return 0;
        var nummer = FelsGruppeNeu();
        GruppeSetzen(liste, nummer, $"{liste.Count} Felsstücke zur Gruppe verbunden");
        return nummer;
    }

    /// <summary>
    /// Löst die ganzen Gruppen dieser Stücke auf (<see cref="FelsGruppeVon"/>: auch Stücke der Gruppe, die nicht übergeben sind): Die Nummer fällt weg,
    /// die Stücke bleiben, wo sie sind. Ein Schritt; false, wenn keins eine Gruppe hatte.
    /// </summary>
    public bool FelsGruppeAufloesen(IEnumerable<EntwurfFels> felsen)
    {
        FelsStrichBeenden();
        var liste = FelsGruppeVon(felsen).Where(f => f.Gruppe != 0).ToList();
        if (liste.Count == 0) return false;
        GruppeSetzen(liste, 0, $"Gruppe von {liste.Count} Felsstücken aufgelöst");
        return true;
    }

    private void GruppeSetzen(IReadOnlyList<EntwurfFels> liste, int nummer, string text)
    {
        var befehle = new List<IKartenbefehl>();
        foreach (var f in liste)
            if (Aenderung(f, n => n.Gruppe = nummer) is { } b) befehle.Add(b);
        if (befehle.Count > 0) Geschichte.Ausfuehren(new Sammelbefehl(text, befehle));
    }

    /// <summary>
    /// Die Stücke einer eigenen Gruppe aus diesen Felsstücken (für <see cref="Felskatalog.Ablegen"/>): Das größte (Grundfläche mal Höhe, bei Gleichstand
    /// das erste im Entwurf) wird die Mitte – Lage 0, 0 und Drehung 0 –, die anderen liegen und drehen sich relativ dazu (in seinem Sinn gedreht, +Z
    /// hangabwärts). Größe, Achsen, Kippen, Rollen und Versatz werden übernommen; das Einsinken als Unterschied zum Einsinken eines neuen Stücks (so
    /// kommt die Formation mit den Vorgaben des Pinsels genau wieder); das Gestein nur, wenn es vom Grundgestein abweicht. Reihenfolge: die Mitte
    /// zuerst, dann wie im Entwurf. Leer, wenn keins der Stücke im Entwurf ist.
    /// </summary>
    public IReadOnlyList<Gruppenstueck> FelsGruppenstuecke(IEnumerable<EntwurfFels> felsen)
    {
        var liste = Geordnet(felsen);
        if (liste.Count == 0) return Array.Empty<Gruppenstueck>();
        static double Groesse(EntwurfFels f) => f.Felsform is { } form
            ? form.Breite * form.Tiefe * form.Hoehe * f.Massstab * f.Massstab * f.Massstab * f.AchseX * f.AchseY * f.AchseZ : 0;
        var mitte = liste[0];
        foreach (var f in liste)
            if (Groesse(f) > Groesse(mitte)) mitte = f;
        var reihe = new List<EntwurfFels> { mitte };
        reihe.AddRange(liste.Where(f => !ReferenceEquals(f, mitte)));
        var aus = new List<Gruppenstueck>(reihe.Count);
        foreach (var f in reihe)
        {
            // die Lage im Sinn der Mitte: um −Drehung der Mitte zurückgedreht (Kartengeometrie.Dreh wie beim Stempel)
            var o = Kartengeometrie.Dreh(f.X - mitte.X, f.Z - mitte.Z, -mitte.Drehung);
            var einsinken = f.Einsinken - Felsformen.EinsinkenVorgabe * (f.Felsform?.EinsinkenFaktor ?? 1);
            aus.Add(Felskatalog.Gerundet(new Gruppenstueck(f.Form, o.X, o.Z, f.Drehung - mitte.Drehung, f.Massstab, einsinken,
                Felsformen.IstGestein(f.Gestein) && f.Gestein != _grundgestein.Wirksam ? f.Gestein : null,
                f.KippenX, f.KippenZ, f.AchseX, f.AchseY, f.AchseZ, f.Versatz)));
        }
        return aus;
    }
}
