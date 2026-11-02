using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Felsstücke verschieben, drehen, skalieren, aufsetzen und neu würfeln (Fels-Plan R2, 02.11.2026)
//
//  Für den Gizmo und die Zahlenfelder der Werkstatt (R3). Alles wirkt auf ein Stück oder mehrere und ist ein Schritt bei Rückgängig:
//  • FelsTransformieren mit Felswerte: Werte absolut setzen (null bleibt) – für alle dieselben oder je Stück eigene (so gibt der Gizmo seine
//    Ergebnisse zurück).
//  • FelsTransformieren mit Felsaenderung: relativ – erst skalieren, dann drehen, dann verschieben. Mehrere Stücke um ihre gemeinsame Mitte.
//    Die Höhe eines Stücks ist dabei sein Versatz (Meter über der Lage aus Boden und Einsinken): Wer ein Stück verschiebt, nimmt es über den
//    Boden mit; DY und Drehungen um x oder z ändern den Versatz.
//  • FelsEinrasten: auf den Boden setzen (der tiefste Punkt der Unterkante steckt Einsinken · Höhe tief), wahlweise an der Hangnormale ausgerichtet.
//  • FelsNeuWuerfeln: wie „Reapply“ im Foliage-Werkzeug von Unreal – gesetzte Stücke mit den Zufallsbereichen des Pinsels neu würfeln.
//  Der Boden um den Fels (Felsboden) bleibt dabei liegen, wie beim Verschieben und Drehen (entschieden 02.11.2026).
//  Alles gerundet wie in der Datei; Winkel über DetMathe (Felsraum) – gleiche Befehle, gleiche Bytes auf jedem Rechner.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Absolute Werte für Felsstücke (null: bleibt): Ort <paramref name="X"/>/<paramref name="Z"/>, <paramref name="Versatz"/> (Meter), die drei
/// Winkel (Grad: <paramref name="Drehung"/> um die Senkrechte, <paramref name="Kippen"/> um x, <paramref name="Rollen"/> um z),
/// <paramref name="Massstab"/>, Faktoren je Achse, <paramref name="Neigung"/> zum Hang und <paramref name="Einsinken"/>.
/// </summary>
public sealed record Felswerte(double? X = null, double? Z = null, double? Versatz = null, double? Drehung = null, double? Kippen = null,
    double? Rollen = null, double? Massstab = null, double? AchseX = null, double? AchseY = null, double? AchseZ = null, double? Neigung = null,
    double? Einsinken = null);

/// <summary>
/// Eine relative Änderung (siehe Kopf der Datei): verschieben um <paramref name="DX"/>, <paramref name="DY"/> (Versatz), <paramref name="DZ"/>;
/// drehen um <paramref name="Drehachse"/> um <paramref name="Winkel"/> Grad – in Weltachsen oder in den Achsen des Stücks
/// (<paramref name="Weltachsen"/>); skalieren mit Faktoren je Achse des Stücks (alle gleich: der Maßstab, sonst die Achsen). Mit
/// <paramref name="UmMitte"/> drehen und skalieren mehrere Stücke um ihre gemeinsame Mitte (die Mitten rücken mit), sonst jedes um sich selbst.
/// </summary>
public sealed record Felsaenderung(double DX = 0, double DY = 0, double DZ = 0, Felsachse? Drehachse = null, double Winkel = 0,
    double FaktorX = 1, double FaktorY = 1, double FaktorZ = 1, bool UmMitte = true, bool Weltachsen = true);

/// <summary>Was „Neu würfeln“ neu zieht (ohne Angabe: Größe, Drehung und die Werte, für die der Pinsel einen Bereich hat; siehe FelsNeuWuerfeln).</summary>
[Flags]
public enum Felswurf
{
    Keins = 0,
    Groesse = 1,
    Achsen = 2,
    Drehung = 4,
    Kippen = 8,
    Rollen = 16,
    Versatz = 32,
    Einsinken = 64,
    Neigung = 128,
    Alle = Groesse | Achsen | Drehung | Kippen | Rollen | Versatz | Einsinken | Neigung,
}

public sealed partial class Kartenentwurf
{
    // ── Abfragen für die Werkstatt ───────────────────────────────────────────

    /// <summary>Ein Stück im Raum: Ursprung, Drehung (ganz und nur die Winkel der Datei), Größe je Achse, Hüllkasten (Felsraum.Rahmen).</summary>
    public Felsrahmen FelsRahmen(EntwurfFels f) => Felsraum.Rahmen(f.AlsLage(), _hoehen);

    /// <summary>Höhe und Normale des Bodens an einem Ort (für Treffen und Einrasten).</summary>
    public (double Hoehe, Felsvektor Normale) BodenBei(Kartenort ort) => Felsraum.Boden(_hoehen, ort.X, ort.Z);

    /// <summary>Die gemeinsame Mitte von Stücken (Ort und Versatz gemittelt) – um sie drehen und skalieren mehrere Stücke.</summary>
    public static Felsvektor FelsMitte(IReadOnlyCollection<EntwurfFels> felsen)
    {
        if (felsen.Count == 0) return new Felsvektor(0, 0, 0);
        double x = 0, y = 0, z = 0;
        foreach (var f in felsen) (x, y, z) = (x + f.X, y + f.Versatz, z + f.Z);
        return new Felsvektor(x / felsen.Count, y / felsen.Count, z / felsen.Count);
    }

    /// <summary>Die gemeinsame Mitte von Stücken in der Welt (Ort und Höhe der Aufsetzpunkte, <see cref="Felsraum.KontaktY"/>, gemittelt) – um sie drehen und skalieren mehrere Stücke in Weltlage.</summary>
    public Felsvektor FelsMitteWelt(IReadOnlyCollection<EntwurfFels> felsen)
    {
        if (felsen.Count == 0) return new Felsvektor(0, 0, 0);
        double x = 0, y = 0, z = 0;
        foreach (var f in felsen) (x, y, z) = (x + f.X, y + Felsraum.KontaktY(f.AlsLage(), _hoehen), z + f.Z);
        return new Felsvektor(x / felsen.Count, y / felsen.Count, z / felsen.Count);
    }

    // ── Transformieren ───────────────────────────────────────────────────────

    /// <summary>Setzt für alle Stücke dieselben Werte (null bleibt). Ein Schritt; nichts, wenn sich nichts ändert. Stücke außerhalb des Entwurfs fallen weg.</summary>
    public void FelsTransformieren(IEnumerable<EntwurfFels> felsen, Felswerte werte) =>
        FelsTransformieren(felsen.Select(f => (f, werte)), "verändert");

    /// <summary>Setzt je Stück eigene Werte (das Ergebnis des Gizmos). Ein Schritt; <paramref name="was"/>: „verschoben“, „gedreht“ … für den Text.</summary>
    public void FelsTransformieren(IEnumerable<(EntwurfFels Fels, Felswerte Werte)> werte, string was = "verändert")
    {
        FelsStrichBeenden();
        var befehle = new List<TeilAendern>();
        var gesehen = new HashSet<EntwurfFels>(ReferenceEqualityComparer.Instance);
        foreach (var (f, w) in werte)
        {
            if (!gesehen.Add(f) || IndexVon(f) < 0) continue;
            if (Aenderung(f, n => Einsetzen(n, w)) is { } b) befehle.Add(b);
        }
        Schritt(befehle, was);
    }

    /// <summary>
    /// Ändert Stücke relativ (siehe <see cref="Felsaenderung"/>). Ein Schritt.
    /// <para>Mehrere Stücke um ihre Mitte (Entscheid 02.11.2026): <b>Skalieren</b> und <b>Drehen um Welt-x oder Welt-z</b> rechnen in Weltlage – die
    /// Mitte ist der Mittelpunkt der Stücke samt Höhe, jedes Stück rückt dort hin, wo der Gizmo es zeichnet, und sein Versatz wird am neuen Ort aus
    /// der Höhe dort zurückgerechnet (<see cref="Felsraum.VersatzFuer"/>); so landet auf unebenem Boden nichts meterweit neben dem Drehpunkt. Das
    /// <b>Drehen um die Senkrechte</b> bleibt am Boden: Der Versatz bleibt, die Stücke gehen mit dem Gelände. Ein einzelnes Stück dreht und
    /// skaliert um sich selbst.</para>
    /// </summary>
    public void FelsTransformieren(IEnumerable<EntwurfFels> felsen, Felsaenderung aenderung)
    {
        var (werte, was) = FelsWerte(Geordnet(felsen), aenderung);
        FelsTransformieren(werte, was);
    }

    /// <summary>
    /// Wie die Stücke aussähen, wenn <paramref name="aenderung"/> gälte – ohne etwas zu ändern: für die Vorschau beim Ziehen am Gizmo (die Darstellung zeigt
    /// die Stücke dort, bis Loslassen den Befehl gibt). Gerundet und begrenzt wie der Befehl.
    /// </summary>
    public IReadOnlyList<(EntwurfFels Fels, Felslage Lage)> FelsVorschau(IEnumerable<EntwurfFels> felsen, Felsaenderung aenderung)
    {
        var (werte, _) = FelsWerte(Geordnet(felsen), aenderung);
        var aus = new List<(EntwurfFels, Felslage)>(werte.Count);
        foreach (var (f, w) in werte)
        {
            var kopie = (EntwurfFels)f.KopieTeil();
            Einsetzen(kopie, w);
            aus.Add((f, kopie.AlsLage()));
        }
        return aus;
    }

    /// <summary>Die neuen Werte je Stück für eine relative Änderung und der Text für den Schritt (die Rechnung, siehe <see cref="FelsTransformieren(IEnumerable{EntwurfFels}, Felsaenderung)"/>).</summary>
    private (List<(EntwurfFels, Felswerte)> Werte, string Was) FelsWerte(List<EntwurfFels> liste, Felsaenderung aenderung)
    {
        var a = aenderung;
        var skaliert = a.FaktorX != 1 || a.FaktorY != 1 || a.FaktorZ != 1;
        var gedreht = a.Drehachse is not null && a.Winkel != 0;
        var verschoben = a.DX != 0 || a.DY != 0 || a.DZ != 0;
        // Weltlage: mehrere Stücke um ihre Mitte, sobald skaliert oder um x/z gedreht wird
        var welt = liste.Count > 1 && a.UmMitte && (skaliert || (gedreht && a.Weltachsen && a.Drehachse != Felsachse.Y));
        var mitte = welt ? FelsMitteWelt(liste) : FelsMitte(liste);
        var werte = new List<(EntwurfFels, Felswerte)>(liste.Count);
        foreach (var f in liste)
        {
            var l = f.AlsLage();
            var ort = new Felsvektor(f.X, welt ? Felsraum.KontaktY(l, _hoehen) : f.Versatz, f.Z);
            var (drehung, kippen, rollen) = (f.Drehung, f.KippenX, f.KippenZ);
            double? massstab = null, ax = null, ay = null, az = null;
            if (skaliert)
            {
                if (a.FaktorX == a.FaktorY && a.FaktorY == a.FaktorZ) massstab = f.Massstab * a.FaktorX;
                else (ax, ay, az) = (f.AchseX * a.FaktorX, f.AchseY * a.FaktorY, f.AchseZ * a.FaktorZ);
                if (a.UmMitte)
                    ort = new Felsvektor(mitte.X + (ort.X - mitte.X) * a.FaktorX, mitte.Y + (ort.Y - mitte.Y) * a.FaktorY, mitte.Z + (ort.Z - mitte.Z) * a.FaktorZ);
            }
            if (gedreht)
            {
                var achse = a.Drehachse!.Value;
                var um = Felsbasis.Um(achse, a.Winkel);
                var eigen = Felsbasis.AusEuler(drehung, kippen, rollen);
                if (a.Weltachsen)
                {
                    // Die ganze Drehung ist Hangneigung · eigen: in der Welt gedreht wird sie um · H(alt) · eigen; die neue Hangneigung am neuen Ort
                    // kommt wieder heraus (eigen' = H(neu)ᵀ · um · H(alt) · eigen). Ohne Neigung um die Senkrechte genau: (Y + w) · X · Z.
                    var vorher = Felsraum.Hangneigung(_hoehen, f.X, f.Z, f.Neigung);
                    var neuOrt = a.UmMitte ? mitte + um.Mal(ort - mitte) : ort;
                    var nachher = Felsraum.Hangneigung(_hoehen, neuOrt.X + a.DX, neuOrt.Z + a.DZ, f.Neigung);
                    if (achse == Felsachse.Y && vorher == Felsbasis.Einheit && nachher == Felsbasis.Einheit) drehung += a.Winkel;
                    else (drehung, kippen, rollen) = (nachher.Transponiert * um * vorher * eigen).EulerNahe(drehung, kippen, rollen);
                    ort = neuOrt;
                }
                else if (achse == Felsachse.Z) rollen += a.Winkel; // (Y · X · Z) · Z = Y · X · (Z + w)
                else (drehung, kippen, rollen) = (eigen * um).EulerNahe(drehung, kippen, rollen);
            }
            double? versatz;
            if (welt)
            {
                // zurück auf den Versatz am neuen Ort
                var neu = l with { X = ort.X + a.DX, Z = ort.Z + a.DZ };
                versatz = Felsraum.VersatzFuer(neu, ort.Y, _hoehen) + a.DY;
            }
            else
            {
                ort += new Felsvektor(a.DX, a.DY, a.DZ);
                versatz = ort.Y;
            }
            var (x, z) = welt ? (ort.X + a.DX, ort.Z + a.DZ) : (ort.X, ort.Z);
            // nur, was sich ändert (ein Wert außerhalb der Grenzen aus einer Datei bleibt beim bloßen Verschieben, wie er ist)
            werte.Add((f, new Felswerte(
                X: verschoben || x != f.X ? x : null, Z: verschoben || z != f.Z ? z : null, Versatz: versatz != f.Versatz ? versatz : null,
                Drehung: gedreht ? drehung : null, Kippen: gedreht ? kippen : null, Rollen: gedreht ? rollen : null,
                Massstab: massstab, AchseX: ax, AchseY: ay, AchseZ: az)));
        }
        var was = (verschoben ? 1 : 0) + (gedreht ? 1 : 0) + (skaliert ? 1 : 0) > 1 ? "verändert"
            : verschoben ? "verschoben" : gedreht ? "gedreht" : "skaliert";
        return (werte, was);
    }

    /// <summary>
    /// Setzt Stücke auf den Boden (Felsraum.Aufsetzen: der tiefste Punkt der Unterkante steckt Einsinken · Höhe tief); mit <paramref name="ausrichten"/> vorher an der Hangnormale
    /// ausgerichtet (Neigung 1, Kippen und Rollen 0). Ein Schritt.
    /// </summary>
    public void FelsEinrasten(IEnumerable<EntwurfFels> felsen, bool ausrichten = false)
    {
        var werte = new List<(EntwurfFels, Felswerte)>();
        foreach (var f in Geordnet(felsen))
        {
            var l = f.AlsLage();
            if (ausrichten) l = l with { Neigung = 1, KippenX = 0, KippenZ = 0 };
            var versatz = Felsraum.Aufsetzen(l, _hoehen);
            werte.Add((f, ausrichten ? new Felswerte(Versatz: versatz, Kippen: 0, Rollen: 0, Neigung: 1) : new Felswerte(Versatz: versatz)));
        }
        FelsTransformieren(werte, "aufgesetzt");
    }

    /// <summary>
    /// Würfelt Stücke mit den Zufallsbereichen des Pinsels neu (wie ein Stück, das der Pinsel an derselben Stelle setzte; „Reapply“). Nur, was in
    /// <paramref name="was"/> steht, ändert sich; Ort, Form und Gestein bleiben. Ohne Angabe (<see cref="WurfVorgabe"/>) Größe und Drehung und
    /// nur die Werte, für die der Pinsel einen Bereich hat – so setzt ein Pinsel ohne Achsen-Bereich gestreckte Stücke nicht zurück. Ohne Startwert
    /// einer aus dem ersten Stück. Die Stücke ziehen in der Reihenfolge des Entwurfs aus einer Quelle – gleiche Auswahl, gleicher Pinsel, gleicher
    /// Startwert: gleiche Werte. Ein Schritt.
    /// </summary>
    public void FelsNeuWuerfeln(IEnumerable<EntwurfFels> felsen, Felspinsel pinsel, Felswurf? was = null, int? startwert = null)
    {
        var wurf = was ?? WurfVorgabe(pinsel);
        var liste = Geordnet(felsen);
        if (liste.Count == 0) return;
        var p = FelsBegrenzt(pinsel) with { Gruppe = null, Radierer = false };
        var zufall = new Zufallsquelle(startwert ?? FelsStartwert(liste[0].Punkt));
        var werte = new List<(EntwurfFels, Felswerte)>(liste.Count);
        foreach (var f in liste)
        {
            var l = Felsstreuung.Treffer(p with { Formen = new[] { f.Form } }, zufall, _hoehen, f.X, f.Z)[0];
            bool Ja(Felswurf w) => (wurf & w) != 0;
            werte.Add((f, new Felswerte(
                Versatz: Ja(Felswurf.Versatz) ? l.Versatz : null, Drehung: Ja(Felswurf.Drehung) ? l.Drehung : null,
                Kippen: Ja(Felswurf.Kippen) ? l.KippenX : null, Rollen: Ja(Felswurf.Rollen) ? l.KippenZ : null,
                Massstab: Ja(Felswurf.Groesse) ? l.Massstab : null,
                AchseX: Ja(Felswurf.Achsen) ? l.AchseX : null, AchseY: Ja(Felswurf.Achsen) ? l.AchseY : null, AchseZ: Ja(Felswurf.Achsen) ? l.AchseZ : null,
                Neigung: Ja(Felswurf.Neigung) ? l.Neigung : null, Einsinken: Ja(Felswurf.Einsinken) ? l.Einsinken : null)));
        }
        FelsTransformieren(werte, "neu gewürfelt");
    }

    // ── Hilfen ───────────────────────────────────────────────────────────────

    /// <summary>Was „Neu würfeln“ ohne Angabe neu zieht: Größe und Drehung, dazu Achsen, Kippen, Rollen, Versatz nur mit Bereich im Pinsel.</summary>
    public static Felswurf WurfVorgabe(Felspinsel p) => Felswurf.Groesse | Felswurf.Drehung
        | (p.AchseX is not null || p.AchseY is not null || p.AchseZ is not null ? Felswurf.Achsen : 0)
        | (p.Kippbereich is not null ? Felswurf.Kippen : 0) | (p.Rollbereich is not null ? Felswurf.Rollen : 0)
        | (p.Versatzbereich is not null ? Felswurf.Versatz : 0);

    /// <summary>
    /// R5 („Hang mitformen“, nur wenn das Häkchen <see cref="Felspinsel.HangFormen"/> gesetzt ist): ein Befehl, der das Gelände hinter den neuen
    /// Wänden anhebt (<see cref="Felshang"/>). Er steht im selben Sammelbefehl wie Stücke und Boden – ein Schritt bei Rückgängig. Das Raster
    /// ändert sich dabei sofort (wie beim Boden um den Fels); null, wenn nichts zu heben ist.
    /// </summary>
    private IKartenbefehl? FelsHangBefehl(IReadOnlyList<EntwurfFels> neue, Felspinsel pinsel)
    {
        if (!pinsel.HangFormen) return null;
        var aenderung = Felshang.Formen(_hoehen, Kartengrenzen.Aussen, neue.Select(f => f.AlsLage()));
        if (aenderung is null) return null;
        var kacheln = aenderung.Kacheln();
        return kacheln.Count == 0 || aenderung.Bereich is not { } bereich ? null : new GelaendeAendern("Hang hinter dem Fels", kacheln, bereich);
    }

    /// <summary>Die Stücke, die zum Entwurf gehören, jedes einmal, in der Reihenfolge des Entwurfs.</summary>
    private List<EntwurfFels> Geordnet(IEnumerable<EntwurfFels> felsen) =>
        felsen.Distinct(ReferenceEqualityComparer.Instance).Cast<EntwurfFels>().Select(f => (f, i: IndexVon(f))).Where(x => x.i >= 0)
            .OrderBy(x => x.i).Select(x => x.f).ToList();

    /// <summary>Die Befehle als ein Schritt (keiner: nichts).</summary>
    private void Schritt(List<TeilAendern> befehle, string was)
    {
        if (befehle.Count == 0) return;
        if (befehle.Count == 1)
        {
            var teil = (EntwurfFels)befehle[0].Nachher;
            Geschichte.Ausfuehren(befehle[0].Benannt($"{teil.Bezeichnung} {was}"));
            return;
        }
        Geschichte.Ausfuehren(new Sammelbefehl($"{befehle.Count} Felsstücke {was}", befehle));
    }

    /// <summary>Setzt Werte in ein Stück (die Kopie im Befehl), in ihren Grenzen und gerundet wie in der Datei.</summary>
    private static void Einsetzen(EntwurfFels n, Felswerte w)
    {
        static double Faktor(double v, double min, double max) => Math.Round(Math.Clamp(v, min, max), 2) + 0.0; // + 0.0: keine „-0“
        if (w.X is { } x) n.X = Kartenraster.Punkt(x);
        if (w.Z is { } z) n.Z = Kartenraster.Punkt(z);
        if (w.Versatz is { } v) n.Versatz = Kartenraster.Punkt(Math.Clamp(v, -Felsformen.VersatzMax, Felsformen.VersatzMax));
        if (w.Drehung is { } d) n.Drehung = Kartenraster.Drehung(d);
        if (w.Kippen is { } k) n.KippenX = Felsraum.Winkel(k);
        if (w.Rollen is { } r) n.KippenZ = Felsraum.Winkel(r);
        if (w.Massstab is { } m) n.Massstab = Faktor(m, Felsformen.MassstabMin, Felsformen.MassstabMax);
        if (w.AchseX is { } ax) n.AchseX = Faktor(ax, Felsformen.AchseMin, Felsformen.AchseMax);
        if (w.AchseY is { } ay) n.AchseY = Faktor(ay, Felsformen.AchseMin, Felsformen.AchseMax);
        if (w.AchseZ is { } az) n.AchseZ = Faktor(az, Felsformen.AchseMin, Felsformen.AchseMax);
        if (w.Neigung is { } ne) n.Neigung = Faktor(ne, 0, 1);
        if (w.Einsinken is { } e) n.Einsinken = Faktor(e, 0, Felsformen.EinsinkenMax);
    }
}
