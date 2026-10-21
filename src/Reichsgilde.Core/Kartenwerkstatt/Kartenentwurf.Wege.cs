using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Wege im Kartenentwurf: anlegen, Stützpunkte bearbeiten, Eigenschaften, Kreuzungen (Einrasten)
//
//  Ein Weg wird aus Stützpunkten gezeichnet; mit „glatt“ läuft eine weiche Kurve hindurch (Wegkurve). Beginnt oder endet ein Weg an einem
//  anderen (näher als der Fangradius), rastet er ein: auf dessen Stützpunkt, sonst auf den nächsten Punkt seiner Linie – dort bekommt der
//  andere Weg im selben Schritt einen Stützpunkt, damit sich beide wirklich treffen (das Gassennetz verbindet nur gemeinsame Punkte sicher).
//  Punkte landen auf 5 cm wie in der Datei.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Wo ein Punkt an einem Weg einrastet: der Weg, der Ort (auf 5 cm), der Stützpunkt, wenn es einer ist (sonst null: ein Punkt auf der Linie,
/// der Weg bekommt dort beim Einrasten einen Stützpunkt), und der Abstand vom gefragten Punkt.
/// </summary>
public readonly record struct Wegfang(EntwurfWeg Weg, Kartenort Ort, int? Stuetzpunkt, double Abstand);

public sealed partial class Kartenentwurf
{
    /// <summary>So nah (Meter) muss ein Wegende an einem anderen Weg liegen, damit es einrastet.</summary>
    public const double Fangradius = 2;

    /// <summary>
    /// Wo der Punkt (x, z) an einem Weg einrastet – null, wenn kein Weg näher als <paramref name="fangradius"/> liegt. Ein Stützpunkt im
    /// Fangradius geht vor (der nächste), sonst der nächste Punkt einer Linie (des Verlaufs). <paramref name="ausser"/> zählt nicht mit
    /// (der Weg, der gerade bearbeitet wird). Für die Vorschau beim Zeichnen; die Befehle rasten genauso ein.
    /// </summary>
    public Wegfang? Einrasten(double x, double z, EntwurfWeg? ausser = null, double fangradius = Fangradius)
    {
        var p = new Kartenort(x, z);
        Wegfang? stuetz = null, linie = null;
        foreach (var w in _wege)
        {
            if (ReferenceEquals(w, ausser) || w.Punkte.Count == 0) continue;
            for (var i = 0; i < w.Punkte.Count; i++)
            {
                var d = p.Abstand(w.Punkte[i]);
                if (d <= fangradius && (stuetz is null || d < stuetz.Value.Abstand)) stuetz = new Wegfang(w, w.Punkte[i], i, d);
            }
            if (stuetz is not null) continue; // ein Stützpunkt schlägt jede Linie
            var (_, ort, abstand) = w.NaechsteStelle(p);
            if (abstand <= fangradius && (linie is null || abstand < linie.Value.Abstand))
                linie = new Wegfang(w, Gerundet(ort), null, abstand);
        }
        return stuetz ?? linie;
    }

    // ── Befehle ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt einen Weg an: Klasse, Stützpunkte (mindestens zwei verschiedene), glatt oder gerade, Name (Vorgabe: Anzeigename der Klasse mit
    /// Nummer), Breite (0 = <see cref="Kartenweg.Standardbreite"/>; nie unter der <see cref="Kartenweg.Mindestbreite"/>). Mit
    /// <paramref name="einrasten"/> rasten Anfang und Ende an anderen Wegen ein (<see cref="Einrasten"/>); ein Schritt.
    /// </summary>
    /// <exception cref="ArgumentException">Mit weniger als zwei verschiedenen Punkten.</exception>
    public EntwurfWeg WegAnlegen(Wegklasse klasse, IReadOnlyList<Kartenort> punkte, bool glatt = true, string? name = null, double breite = 0, bool einrasten = true)
    {
        var liste = OhneDoppelte(punkte.Select(Gerundet));
        if (liste.Count < 2) throw new ArgumentException("Ein Weg braucht mindestens zwei verschiedene Punkte.", nameof(punkte));
        var weg = new EntwurfWeg
        {
            Name = string.IsNullOrWhiteSpace(name) ? FreierName(Kartenweg.Anzeigename(klasse), _wege.Select(w => w.Name)) : name,
            Klasse = klasse, Breite = breite > 0 ? Math.Max(breite, Kartenweg.Mindestbreite(klasse)) : Kartenweg.Standardbreite(klasse),
        };
        weg.KurveSetzen(glatt ? Wegkurve.Glatt : null);
        var befehle = new List<IKartenbefehl>();
        if (einrasten)
        {
            var nachbarn = new Dictionary<EntwurfWeg, EntwurfWeg>(ReferenceEqualityComparer.Instance);
            liste[0] = Rasten(liste[0], null, nachbarn);
            liste[^1] = Rasten(liste[^1], null, nachbarn);
            liste = OhneDoppelte(liste);
            if (liste.Count < 2) throw new ArgumentException("Beide Enden rasten am selben Punkt ein.", nameof(punkte));
            befehle.AddRange(NachbarBefehle(nachbarn));
        }
        weg.PunkteSetzen(liste);
        befehle.Insert(0, new TeilSetzen(weg, _wege.Count));
        Geschichte.Ausfuehren(befehle.Count == 1 ? befehle[0] : new Sammelbefehl($"{weg.Name} gesetzt", befehle));
        return weg;
    }

    /// <summary>
    /// Verschiebt einen Stützpunkt. Mit <paramref name="einrasten"/> rastet ein Anfangs- oder Endpunkt an anderen Wegen ein (der Nachbar bekommt
    /// dort im selben Schritt einen Stützpunkt).
    /// </summary>
    public void StuetzpunktVerschieben(EntwurfWeg weg, int index, double x, double z, bool einrasten = true)
    {
        Pruefen(weg, index, weg.Punkte.Count);
        var ort = Gerundet(new Kartenort(x, z));
        var nachbarn = new Dictionary<EntwurfWeg, EntwurfWeg>(ReferenceEqualityComparer.Instance);
        if (einrasten && (index == 0 || index == weg.Punkte.Count - 1)) ort = Rasten(ort, weg, nachbarn);
        var eigener = Aenderung(weg, w => w.PunkteSetzen(w.Punkte.Select((p, i) => i == index ? ort : p)));
        Buendeln($"{weg.Bezeichnung}: Stützpunkt verschoben", eigener, NachbarBefehle(nachbarn));
    }

    /// <summary>Fügt einen Stützpunkt an der Stelle <paramref name="index"/> ein (0 = vor dem ersten, Anzahl = hinter dem letzten).</summary>
    public void StuetzpunktEinfuegen(EntwurfWeg weg, int index, double x, double z)
    {
        Pruefen(weg, index, weg.Punkte.Count + 1);
        var ort = Gerundet(new Kartenort(x, z));
        Aendern(weg, "Stützpunkt eingefügt", w => w.PunkteSetzen(w.Punkte.Take(index).Append(ort).Concat(w.Punkte.Skip(index))));
    }

    /// <summary>
    /// Fügt einen Stützpunkt dort ein, wo der Punkt (x, z) am nächsten an der Linie liegt (Klick auf die Linie) – auf der Linie selbst, im
    /// richtigen Abschnitt. Gibt den Index des neuen Stützpunkts zurück (−1: dort steht schon einer).
    /// </summary>
    public int StuetzpunktEinfuegenBei(EntwurfWeg weg, double x, double z)
    {
        Pruefen(weg, 0, 1);
        var (index, ort, _) = weg.NaechsteStelle(new Kartenort(x, z));
        ort = Gerundet(ort);
        if (weg.Punkte.Any(p => p.Abstand(ort) < 1e-3)) return -1;
        StuetzpunktEinfuegen(weg, index, ort.X, ort.Z);
        return index;
    }

    /// <summary>Löscht einen Stützpunkt; false (kein Schritt), wenn der Weg dann weniger als zwei hätte.</summary>
    public bool StuetzpunktLoeschen(EntwurfWeg weg, int index)
    {
        Pruefen(weg, index, weg.Punkte.Count);
        if (weg.Punkte.Count <= 2) return false;
        Aendern(weg, "Stützpunkt gelöscht", w => w.PunkteSetzen(w.Punkte.Where((_, i) => i != index)));
        return true;
    }

    /// <summary>Verschiebt den ganzen Weg um (dx, dz) Meter (ohne Einrasten).</summary>
    public void WegVerschieben(EntwurfWeg weg, double dx, double dz) =>
        Aendern(weg, "verschoben", w => w.PunkteSetzen(w.Punkte.Select(p => Gerundet(new Kartenort(p.X + dx, p.Z + dz)))));

    public void WegUmbenennen(EntwurfWeg weg, string name) => Aendern(weg, "umbenannt", w => w.Name = name);

    /// <summary>Ändert die Klasse; liegt die Breite unter der Mindestbreite der neuen Klasse, wird sie im selben Schritt angehoben.</summary>
    public void WegKlasseSetzen(EntwurfWeg weg, Wegklasse klasse) => Aendern(weg, "Art geändert", w =>
    {
        w.Klasse = klasse;
        if (w.Breite > 0 && w.Breite < Kartenweg.Mindestbreite(klasse)) w.Breite = Kartenweg.Mindestbreite(klasse);
    });

    /// <summary>Setzt die Breite (Meter, auf 5 cm), nie unter der Mindestbreite der Klasse; 0 oder weniger = nach der Klasse (Feld entfällt).</summary>
    public void WegBreiteSetzen(EntwurfWeg weg, double breite) =>
        Aendern(weg, "Breite geändert", w => w.Breite = breite <= 0 ? 0 : Math.Max(Kartenraster.Punkt(breite), w.Mindestbreite));

    /// <summary>Weiche Kurve durch die Stützpunkte (true) oder gerade Strecken (false).</summary>
    public void WegKurveSetzen(EntwurfWeg weg, bool glatt) =>
        Aendern(weg, glatt ? "geglättet" : "begradigt", w => w.KurveSetzen(glatt ? Wegkurve.Glatt : null));

    /// <summary>Löscht einen Weg (Rückgängig setzt ihn an dieselbe Stelle der Reihenfolge zurück).</summary>
    public void WegLoeschen(EntwurfWeg weg) => TeilEntfernen(weg);

    // ── Hilfen ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Rastet einen Punkt ein: auf einem Stützpunkt bleibt der Nachbar, wie er ist; auf einer Linie bekommt eine Kopie des Nachbarn (in
    /// <paramref name="nachbarn"/>, für mehrere Enden am selben Weg) dort einen Stützpunkt.
    /// </summary>
    private Kartenort Rasten(Kartenort p, EntwurfWeg? ausser, Dictionary<EntwurfWeg, EntwurfWeg> nachbarn)
    {
        if (Einrasten(p.X, p.Z, ausser) is not { } fang) return p;
        if (fang.Stuetzpunkt is not null) return fang.Ort;
        if (!nachbarn.TryGetValue(fang.Weg, out var kopie)) nachbarn[fang.Weg] = kopie = (EntwurfWeg)fang.Weg.KopieTeil();
        var (index, ort, _) = kopie.NaechsteStelle(fang.Ort);
        ort = Gerundet(ort);
        if (!kopie.Punkte.Any(q => q.Abstand(ort) < 1e-3))
            kopie.PunkteSetzen(kopie.Punkte.Take(index).Append(ort).Concat(kopie.Punkte.Skip(index)));
        return ort;
    }

    private static IEnumerable<IKartenbefehl> NachbarBefehle(Dictionary<EntwurfWeg, EntwurfWeg> nachbarn) =>
        nachbarn.Select(n => (IKartenbefehl)new TeilAendern($"{n.Key.Bezeichnung}: Kreuzung", n.Key, n.Key.KopieTeil(), n.Value));

    /// <summary>Führt den eigenen Befehl und die der Nachbarn als einen Schritt aus (nichts, wenn sich nichts ändert).</summary>
    private void Buendeln(string beschreibung, IKartenbefehl? eigener, IEnumerable<IKartenbefehl> weitere)
    {
        var alle = (eigener is null ? Enumerable.Empty<IKartenbefehl>() : new[] { eigener }).Concat(weitere).ToList();
        if (alle.Count == 0) return;
        Geschichte.Ausfuehren(alle.Count == 1 && alle[0] is TeilAendern t ? t.Benannt(beschreibung) : new Sammelbefehl(beschreibung, alle));
    }

    /// <summary>Löscht einen Teil (Rückgängig setzt ihn an dieselbe Stelle seiner Liste zurück).</summary>
    private void TeilEntfernen(EntwurfTeil teil)
    {
        var i = IndexVon(teil);
        if (i < 0) throw new ArgumentException($"„{teil.Bezeichnung}“ gehört nicht (mehr) zu diesem Entwurf.", nameof(teil));
        Geschichte.Ausfuehren(new TeilLoeschen(teil, i));
    }

    private void Pruefen(EntwurfTeil teil, int index, int anzahl)
    {
        if (!Enthaelt(teil)) throw new ArgumentException($"„{teil.Bezeichnung}“ gehört nicht (mehr) zu diesem Entwurf.", nameof(teil));
        if (index < 0 || index >= anzahl) throw new ArgumentOutOfRangeException(nameof(index), index, $"zwischen 0 und {anzahl - 1}");
    }

    /// <summary>Ein Punkt auf 5 cm wie in der Datei.</summary>
    internal static Kartenort Gerundet(Kartenort p) => new(Kartenraster.Punkt(p.X), Kartenraster.Punkt(p.Z));

    /// <summary>Die Punkte ohne gleiche Nachbarn (ein doppelter Klick setzt keinen zweiten Punkt).</summary>
    private static List<Kartenort> OhneDoppelte(IEnumerable<Kartenort> punkte)
    {
        var liste = new List<Kartenort>();
        foreach (var p in punkte)
            if (liste.Count == 0 || liste[^1].Abstand(p) > 1e-6) liste.Add(p);
        return liste;
    }

    /// <summary>„Gasse“, „Gasse 2“, „Gasse 3“ … – der erste freie Name.</summary>
    private static string FreierName(string basis, IEnumerable<string> belegt)
    {
        var namen = belegt.ToHashSet();
        if (!namen.Contains(basis)) return basis;
        var n = 2;
        while (namen.Contains($"{basis} {n}")) n++;
        return $"{basis} {n}";
    }
}
