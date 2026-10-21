using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Wege im Kartenentwurf: anlegen, Stützpunkte bearbeiten, Eigenschaften, Kreuzungen (Einrasten)
//
//  Ein Weg wird aus Stützpunkten gezeichnet; mit „glatt“ läuft eine weiche Kurve hindurch (Wegkurve). Beginnt oder endet ein Weg an einem
//  Ziel (näher als der Fangradius), rastet er ein – zuerst an einem Ziel, das kein Weg ist: dem Hof eines Werkstattplatzes, der Mitte eines
//  Tores (Torfang) oder einem Halteplatz, den die Darstellung nennt (Karrenplätze, Gatter); sonst an einem anderen Weg: auf dessen
//  Stützpunkt, sonst auf den nächsten Punkt seiner Linie – dort bekommt der andere Weg im selben Schritt einen Stützpunkt, damit sich beide
//  wirklich treffen (das Gassennetz verbindet nur gemeinsame Punkte sicher). Punkte dazwischen rasten nur in Tore ein (ein Weg durch das
//  Tor). Punkte landen auf 5 cm wie in der Datei.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Woran ein Punkt einrastet.</summary>
public enum Fangziel
{
    /// <summary>Ein Stützpunkt eines anderen Weges.</summary>
    Stuetzpunkt,

    /// <summary>Ein Punkt auf der Linie eines anderen Weges (er bekommt dort einen Stützpunkt).</summary>
    Linie,

    /// <summary>Der Hof eines Werkstattplatzes (dort beginnt seine Gasse).</summary>
    Hof,

    /// <summary>Die Mitte eines Tores.</summary>
    Tor,

    /// <summary>Ein Halteplatz, den die Darstellung nennt (<see cref="Kartenentwurf.Fangpunkte"/>: Karrenplätze, Gatter).</summary>
    Halteplatz,
}

/// <summary>
/// Wo ein Punkt einrastet: der Weg (null bei Hof, Tor, Halteplatz), der Ort (auf 5 cm), der Stützpunkt, wenn es einer ist (null: ein Punkt
/// auf der Linie – der Weg bekommt dort beim Einrasten einen Stützpunkt – oder kein Weg), und der Abstand vom gefragten Punkt. Dazu woran
/// (<see cref="Ziel"/>) und, je nach Ziel, das Gebäude (Hof, Halteplatz) bzw. Mauer und Tor.
/// </summary>
public readonly record struct Wegfang(EntwurfWeg? Weg, Kartenort Ort, int? Stuetzpunkt, double Abstand)
{
    public Fangziel Ziel { get; init; }
    public EntwurfGebaeude? Gebaeude { get; init; }
    public EntwurfMauer? Mauer { get; init; }

    /// <summary>Index des Tores in <see cref="EntwurfMauer.Tore"/> (nur bei <see cref="Fangziel.Tor"/>).</summary>
    public int? Tor { get; init; }

    /// <summary>Rastet der Punkt an einem Weg ein (Stützpunkt oder Linie)?</summary>
    public bool AmWeg => Weg is not null;
}

public sealed partial class Kartenentwurf
{
    /// <summary>So nah (Meter) muss ein Wegende an einem anderen Weg liegen, damit es einrastet.</summary>
    public const double Fangradius = 2;

    /// <summary>
    /// Weitere Fangpunkte eines Gebäudes, die nur die Darstellung kennt (Karrenplätze und Gatter der Umlandbetriebe aus den Modellen) – null:
    /// keine. Wegenden rasten dort ein wie an einem Hof (<see cref="Fangziel.Halteplatz"/>).
    /// </summary>
    public Func<EntwurfGebaeude, IEnumerable<Kartenort>>? Fangpunkte { get; set; }

    /// <summary>
    /// Wo das Ende eines Weges am Punkt (x, z) einrastet – null, wenn nichts näher als <paramref name="fangradius"/> liegt. Zuerst die Ziele,
    /// die kein Weg sind (das nächste): Hof eines Werkstattplatzes, Mitte eines Tores (bis <see cref="Torfang"/>), Halteplatz
    /// (<see cref="Fangpunkte"/>); läuft durch die Tormitte schon ein Weg, rastet das Ende dort an ihm ein (<see cref="Wegfang.Tor"/> ist dann
    /// gesetzt). Sonst ein Weg: ein Stützpunkt im Fangradius geht vor (der nächste), sonst der nächste Punkt einer Linie (des Verlaufs).
    /// <paramref name="ausser"/> zählt nicht mit (der Weg, der gerade bearbeitet wird). Für die Vorschau beim Zeichnen; die Befehle
    /// rasten genauso ein.
    /// </summary>
    public Wegfang? Einrasten(double x, double z, EntwurfWeg? ausser = null, double fangradius = Fangradius)
    {
        var p = new Kartenort(x, z);
        if (ZielFangen(p, fangradius) is not { } ziel) return WegFangen(p, ausser, fangradius);
        // Läuft schon ein Weg durch die Tormitte, rastet das Ende an ihm ein (Stützpunkt bzw. Linie) – so hängt der neue Weg im Netz.
        if (ziel.Ziel == Fangziel.Tor && WegFangen(ziel.Ort, ausser, 0.05) is { } weg)
            return weg with { Abstand = ziel.Abstand, Mauer = ziel.Mauer, Tor = ziel.Tor };
        return ziel;
    }

    /// <summary>Wo ein Stützpunkt zwischen Anfang und Ende einrastet: nur in der Mitte eines Tores (bis <see cref="Torfang"/>), sonst null.</summary>
    public Wegfang? TorEinrasten(double x, double z) =>
        TorBei(x, z) is var (m, tor, d) ? new Wegfang(null, m.Tore[tor].Punkt, null, d) { Ziel = Fangziel.Tor, Mauer = m, Tor = tor } : null;

    /// <summary>Das nächste Ziel, das kein Weg ist (Hof, Tor, Halteplatz) – null, wenn keins nah genug liegt.</summary>
    private Wegfang? ZielFangen(Kartenort p, double fangradius)
    {
        Wegfang? best = null;
        void Pruefe(Kartenort ort, double radius, Func<double, Wegfang> fang)
        {
            var d = p.Abstand(ort);
            if (d <= radius && (best is null || d < best.Value.Abstand)) best = fang(d);
        }
        foreach (var g in _gebaeude)
        {
            if (g.Art == Bausteinart.Werkstattplatz && g.Hof is { } hof)
                Pruefe(hof, fangradius, d => new Wegfang(null, Gerundet(hof), null, d) { Ziel = Fangziel.Hof, Gebaeude = g });
            if (Fangpunkte is not null)
                foreach (var halt in Fangpunkte(g))
                    Pruefe(halt, fangradius, d => new Wegfang(null, Gerundet(halt), null, d) { Ziel = Fangziel.Halteplatz, Gebaeude = g });
        }
        foreach (var m in _mauern)
            for (var i = 0; i < m.Tore.Count; i++)
            {
                var tor = i;
                Pruefe(m.Tore[i].Punkt, Math.Max(fangradius, Torfang), d => new Wegfang(null, m.Tore[tor].Punkt, null, d) { Ziel = Fangziel.Tor, Mauer = m, Tor = tor });
            }
        return best;
    }

    /// <summary>Wo der Punkt an einem anderen Weg einrastet (Stützpunkt vor Linie) – null, wenn keiner nah genug liegt.</summary>
    private Wegfang? WegFangen(Kartenort p, EntwurfWeg? ausser, double fangradius)
    {
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
                linie = new Wegfang(w, Gerundet(ort), null, abstand) { Ziel = Fangziel.Linie };
        }
        return stuetz ?? linie;
    }

    // ── Befehle ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt einen Weg an: Klasse, Stützpunkte (mindestens zwei verschiedene), glatt oder gerade, Name (Vorgabe: Anzeigename der Klasse mit
    /// Nummer), Breite (0 = <see cref="Kartenweg.Standardbreite"/>; nie unter der <see cref="Kartenweg.Mindestbreite"/>). Mit
    /// <paramref name="einrasten"/> rasten Anfang und Ende ein (<see cref="Einrasten"/>: Hof, Tor, Halteplatz, andere Wege), die Punkte
    /// dazwischen nur in Tore; eine Gasse, die an einem Hof endet und nicht an einem beginnt, wird umgedreht (Werkstattnorm: die Gasse beginnt
    /// am Hof). Ein Schritt.
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
            var (anfangHof, endeHof) = (false, false);
            (liste[0], anfangHof) = Rasten(liste[0], null, nachbarn);
            (liste[^1], endeHof) = Rasten(liste[^1], null, nachbarn);
            for (var i = 1; i < liste.Count - 1; i++)
                if (TorEinrasten(liste[i].X, liste[i].Z) is { } tor) liste[i] = tor.Ort;
            liste = OhneDoppelte(liste);
            if (liste.Count < 2) throw new ArgumentException("Beide Enden rasten am selben Punkt ein.", nameof(punkte));
            if (klasse == Wegklasse.Gasse && endeHof && !anfangHof) liste.Reverse();
            befehle.AddRange(NachbarBefehle(nachbarn));
        }
        weg.PunkteSetzen(liste);
        befehle.Insert(0, new TeilSetzen(weg, _wege.Count));
        Geschichte.Ausfuehren(befehle.Count == 1 ? befehle[0] : new Sammelbefehl($"{weg.Name} gesetzt", befehle));
        return weg;
    }

    /// <summary>
    /// Verschiebt einen Stützpunkt. Mit <paramref name="einrasten"/> rastet ein Anfangs- oder Endpunkt ein wie beim Anlegen (an einem Weg bekommt
    /// der Nachbar dort im selben Schritt einen Stützpunkt), ein Punkt dazwischen nur in ein Tor.
    /// </summary>
    public void StuetzpunktVerschieben(EntwurfWeg weg, int index, double x, double z, bool einrasten = true)
    {
        Pruefen(weg, index, weg.Punkte.Count);
        var ort = Gerundet(new Kartenort(x, z));
        var nachbarn = new Dictionary<EntwurfWeg, EntwurfWeg>(ReferenceEqualityComparer.Instance);
        if (einrasten && (index == 0 || index == weg.Punkte.Count - 1)) ort = Rasten(ort, weg, nachbarn).Ort;
        else if (einrasten && TorEinrasten(ort.X, ort.Z) is { } tor) ort = tor.Ort;
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
    /// Rastet ein Wegende ein (<see cref="Einrasten"/>): an Hof, Tor, Halteplatz oder einem Stützpunkt bleibt der Rest, wie er ist; auf einer
    /// Linie bekommt eine Kopie des Nachbarn (in <paramref name="nachbarn"/>, für mehrere Enden am selben Weg) dort einen Stützpunkt. Gibt auch
    /// zurück, ob das Ende an einem Hof liegt.
    /// </summary>
    private (Kartenort Ort, bool Hof) Rasten(Kartenort p, EntwurfWeg? ausser, Dictionary<EntwurfWeg, EntwurfWeg> nachbarn)
    {
        if (Einrasten(p.X, p.Z, ausser) is not { } fang) return (p, false);
        if (fang.Weg is not { } weg || fang.Stuetzpunkt is not null) return (fang.Ort, fang.Ziel == Fangziel.Hof);
        if (!nachbarn.TryGetValue(weg, out var kopie)) nachbarn[weg] = kopie = (EntwurfWeg)weg.KopieTeil();
        var (index, ort, _) = kopie.NaechsteStelle(fang.Ort);
        ort = Gerundet(ort);
        if (!kopie.Punkte.Any(q => q.Abstand(ort) < 1e-3))
            kopie.PunkteSetzen(kopie.Punkte.Take(index).Append(ort).Concat(kopie.Punkte.Skip(index)));
        return (ort, false);
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
