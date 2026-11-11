using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Fläche füllen nach Regeln im Kartenentwurf (Felspinsel P6; Regeln und Voreinstellungen in Bauen/Felsfuellung.cs)
//
//  FelsFuellenSchaetzen: wie viele Stücke etwa (schnell, aus einem Probengitter – für die Warnung über der Obergrenze, bevor gerechnet wird).
//  FelsFuellenVorschau: genau die Stücke, die FelsFuellen setzen würde – ohne etwas zu ändern (gleicher Stand, Fläche, Startwert: dieselben).
//  FelsFuellen: setzt sie in einem Schritt (Sammelbefehl, mit dem Boden um den Fels) – Rückgängig nimmt alles zurück. Die Obergrenze
//  (Felsfuellung.Obergrenze) ist eine Warnung der Werkstatt, kein Verbot: Der Befehl füllt immer.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Stück der Füllung: die Lage, aus welcher Regel (Nummer in <see cref="Felsfuellung.Regeln"/>) und aus welchem Treffer (für „Verbinden“).</summary>
public readonly record struct Felsfuellstueck(Felslage Lage, int Regel, int Treffer);

public sealed partial class Kartenentwurf
{
    /// <summary>So viele Proben höchstens im Gitter für Schätzung, Wirkfläche und Höhe der Fläche.</summary>
    private const int FuellProbenMax = 40_000;

    /// <summary>Der Startwert einer Füllung ohne eigenen: aus der ersten Ecke des Umrisses und der Zahl der Stücke (wie <see cref="FelsStartwert"/>).</summary>
    public int FelsFuellenStartwert(Felsflaeche flaeche) => flaeche.Umriss.Count == 0 ? 1 : FelsStartwert(flaeche.Umriss[0]);

    /// <summary>
    /// Wie viele Stücke die Füllung etwa setzt (Dichte mal Wirkfläche je Regel durch die mittlere Grundfläche eines Treffers; „auf Fels“ aus den großen
    /// Stücken, die schon stehen oder die Regeln davor etwa setzen). Schnell genug für jede Änderung in der Tafel; die genaue Zahl gibt die Vorschau.
    /// </summary>
    public int FelsFuellenSchaetzen(Felsfuellung fuellung, Felsflaeche flaeche)
    {
        if (!flaeche.IstGueltig) return 0;
        var proben = FuellProben(flaeche, out var zelle);
        var (hMin, hMax) = FuellHoehen(proben);
        var salz = FelsFuellenStartwert(flaeche);
        // Grundfläche großer Stücke (Träger für „auf Fels“), die schon in der Fläche stehen
        var traeger = _felsen.Where(f => f.Radius >= Felsfuellung.AufFelsRadius && flaeche.Enthaelt(f.X, f.Z))
            .Sum(f => Math.PI * Sq(Felsfuellung.AufFelsInnen * f.Radius));
        var summe = 0.0;
        foreach (var (nr, r) in fuellung.Kette())
        {
            var p = FelsBegrenzt(r.Pinsel);
            double wirk;
            if (r.AufFels) wirk = traeger;
            else
            {
                var aus = p.Aussparen ? Felsaussparung.Von(this, p.AussparenBauzonen) : null;
                wirk = 0;
                foreach (var (x, z) in proben)
                {
                    if (aus?.Belegt(x, z, 0) == true) continue;
                    wirk += zelle * FuellFaktor(r, p, x, z, hMin, hMax, FuellSalz(salz, nr));
                }
            }
            var treffer = p.Dichte * wirk / Math.Max(1e-3, r.MittlererPlatz);
            summe += treffer * r.StueckeJeTreffer;
            // die großen Stücke dieser Regel tragen später „auf Fels“
            if (!r.AufFels && r.Groessenrang >= Felsfuellung.AufFelsRadius) traeger += p.Dichte * wirk * Sq(Felsfuellung.AufFelsInnen);
        }
        return (int)Math.Round(summe);
    }

    /// <summary>
    /// Die Stücke, die <see cref="FelsFuellen"/> setzen würde (gleicher Stand, gleiche Fläche und Füllung, gleicher Startwert – dieselben) – ohne etwas
    /// zu ändern. Leer bei einer Fläche unter drei Ecken oder <see cref="Felsflaeche.FlaecheMin"/>.
    /// </summary>
    public IReadOnlyList<Felsfuellstueck> FelsFuellenVorschau(Felsfuellung fuellung, Felsflaeche flaeche, int? startwert = null) =>
        FuellungRechnen(fuellung, flaeche, startwert ?? FelsFuellenStartwert(flaeche)).Select(x => x.Stueck).ToList();

    /// <summary>
    /// Füllt die Fläche nach den Regeln (ein Schritt; „Boden mitmalen“ je Regel). Ohne Startwert einer aus der ersten Ecke. Setzt immer – auch über
    /// <see cref="Felsfuellung.Obergrenze"/> (die Werkstatt fragt vorher nach). Gibt die neuen Stücke zurück (leer: nichts gesetzt, kein Schritt).
    /// </summary>
    public IReadOnlyList<EntwurfFels> FelsFuellen(Felsfuellung fuellung, Felsflaeche flaeche, int? startwert = null)
    {
        FelsStrichBeenden();
        BodenStrichBeenden();
        var liste = FuellungRechnen(fuellung, flaeche, startwert ?? FelsFuellenStartwert(flaeche));
        if (liste.Count == 0) return Array.Empty<EntwurfFels>();
        // „Verbinden“: die Stücke eines Treffers bekommen eine gemeinsame Nummer (in der Reihenfolge der Treffer)
        var naechste = FelsGruppeNeu();
        var regeln = fuellung.Regeln;
        foreach (var gruppe in liste.GroupBy(x => x.Stueck.Treffer))
        {
            var stuecke = gruppe.ToList();
            if (stuecke.Count < 2 || !regeln[stuecke[0].Stueck.Regel].Pinsel.Verbinden) continue;
            foreach (var x in stuecke) x.Fels.Gruppe = naechste;
            naechste++;
        }
        var befehle = liste.Select((x, i) => (IKartenbefehl)new TeilSetzen(x.Fels, _felsen.Count + i)).ToList();
        var boden = new Felsbodenstrich(_boden, _hoehen, Kartengrenzen.Aussen);
        var mitBoden = false;
        foreach (var x in liste)
        {
            if (!regeln[x.Stueck.Regel].Pinsel.Boden) continue;
            boden.Malen(Felsfuss.Von(x.Fels));
            mitBoden = true;
        }
        if (mitBoden && FelsBodenBefehl(boden, "Boden um den Fels") is { } b) befehle.Add(b);
        Geschichte.Ausfuehren(new Sammelbefehl($"Fläche gefüllt ({fuellung.Name}): {liste.Count} Felsstücke", befehle));
        return liste.Select(x => x.Fels).ToList();
    }

    // ── Rechnung ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Die Füllung, Regel für Regel in der Kette (Felsfuellung.Kette): je Regel Versuche an zufälligen Orten im Rechteck der Fläche (bei „auf Fels“
    /// auf zufälligen Trägern), bis die Dichte der Wirkfläche gedeckt ist, die Versuche aufgebraucht sind oder <see cref="Felsfuellung.FehlversucheMax"/>
    /// Treffer in Folge abgewiesen wurden. Ein Ort zählt mit dem Faktor der Kette (Filter, Höhe, Rauschen; weicher Rand: ein Wurf). Alle Würfe aus
    /// einer Quelle, Nachbarn über das Suchraster der Stücke (vorhandene und neue).
    /// </summary>
    private List<(Felsfuellstueck Stueck, EntwurfFels Fels)> FuellungRechnen(Felsfuellung fuellung, Felsflaeche flaeche, int startwert)
    {
        var aus = new List<(Felsfuellstueck, EntwurfFels)>();
        if (!flaeche.IstGueltig) return aus;
        var zufall = new Zufallsquelle(startwert);
        var proben = FuellProben(flaeche, out var zelle);
        var (hMin, hMax) = FuellHoehen(proben);
        var raster = new Felsraster(_felsen);
        var rechteck = Math.Max(1, (flaeche.X1 - flaeche.X0) * (flaeche.Z1 - flaeche.Z0));
        Felsaussparung? aussparen = null, aussparenZonen = null;
        var treffer = 0;
        // Träger für „auf Fels“: große Stücke in der Fläche (vorhandene in der Reihenfolge der Liste, dann die neuen)
        var traeger = _felsen.Where(f => f.Radius >= Felsfuellung.AufFelsRadius && flaeche.Enthaelt(f.X, f.Z)).ToList();
        foreach (var (nr, r) in fuellung.Kette())
        {
            var p = FelsBegrenzt(r.Pinsel);
            var salz = FuellSalz(startwert, nr);
            var aussparung = !p.Aussparen ? null
                : p.AussparenBauzonen ? aussparenZonen ??= Felsaussparung.Von(this, true) : aussparen ??= Felsaussparung.Von(this, false);
            double wirk;
            if (r.AufFels) wirk = traeger.Sum(f => Math.PI * Sq(Felsfuellung.AufFelsInnen * f.Radius));
            else
            {
                wirk = 0;
                foreach (var (x, z) in proben)
                    if (aussparung?.Belegt(x, z, 0) != true) wirk += zelle * FuellFaktor(r, p, x, z, hMin, hMax, salz);
            }
            var ziel = p.Dichte * wirk;
            if (ziel <= 0 || (r.AufFels && traeger.Count == 0)) continue;
            var platz = Math.Max(1e-3, p.KleinsterPlatz);
            var versuche = (int)Math.Clamp(Math.Ceiling(3 * ziel / platz * (r.AufFels ? 1 : rechteck / Math.Max(1, wirk))), Felspinsel.VersucheMin, Felsfuellung.VersucheMax);
            var traegerZahl = traeger.Count; // neue große Stücke derselben Regel tragen erst die nächsten Regeln
            var belegt = 0.0;
            var fehl = 0;
            for (var v = 0; v < versuche && belegt < ziel && fehl < Felsfuellung.FehlversucheMax; v++)
            {
                double x, z;
                EntwurfFels? traegerFels = null;
                if (r.AufFels)
                {
                    traegerFels = traeger[zufall.Next(traegerZahl)];
                    var innen = Felsfuellung.AufFelsInnen * traegerFels.Radius;
                    (x, z) = (traegerFels.X + (2 * zufall.NextDouble() - 1) * innen, traegerFels.Z + (2 * zufall.NextDouble() - 1) * innen);
                    if (Sq(x - traegerFels.X) + Sq(z - traegerFels.Z) > innen * innen) continue;
                }
                else (x, z) = (flaeche.X0 + zufall.NextDouble() * (flaeche.X1 - flaeche.X0), flaeche.Z0 + zufall.NextDouble() * (flaeche.Z1 - flaeche.Z0));
                if (!flaeche.Enthaelt(x, z) || !Kartengrenzen.Aussen.Enthaelt(x, z)) continue;
                var faktor = r.AufFels ? Felsfuellung.Maske(r, x, z, Anteil(x, z, hMin, hMax), salz) : FuellFaktor(r, p, x, z, hMin, hMax, salz);
                if (faktor <= 0 || (faktor < 1 && zufall.NextDouble() >= faktor)) continue;
                var lagen = Felsstreuung.Treffer(p, zufall, _hoehen, x, z);
                if (traegerFels is not null) lagen = AufTraeger(lagen, traegerFels);
                if (lagen.Count == 0 || lagen.Any(l => !flaeche.Enthaelt(l.X, l.Z) || !Kartengrenzen.Aussen.Enthaelt(l.X, l.Z)
                        || FuellZuNah(raster, l, p.Abstand, r.AufFels) || aussparung?.Belegt(l.X, l.Z, l.Radius) == true))
                {
                    fehl++;
                    continue;
                }
                fehl = 0;
                foreach (var l in lagen)
                {
                    var neu = EntwurfFels.Aus(GesteinWieKarte(l));
                    raster.Dazu(neu);
                    aus.Add((new Felsfuellstueck(neu.AlsLage(), nr, treffer), neu));
                    belegt += Math.PI * l.Radius * l.Radius;
                    if (!r.AufFels && neu.Radius >= Felsfuellung.AufFelsRadius) traeger.Add(neu);
                }
                treffer++;
            }
        }
        return aus;
    }

    /// <summary>„Auf Fels setzen“: die Lagen ohne Neigung auf die Oberseite des Trägers (leer, wenn ein Stück nicht über ihm liegt).</summary>
    private List<Felslage> AufTraeger(List<Felslage> lagen, EntwurfFels traeger)
    {
        var rahmen = Felsraum.Rahmen(traeger.AlsLage(), _hoehen);
        var oben = rahmen.InWelt(new Felsvektor(0, Felsfuellung.AufFelsHoehe * rahmen.Groesse.Y, 0)).Y;
        var innen = Felsfuellung.AufFelsInnen * traeger.Radius;
        if (lagen.Any(l => Sq(l.X - traeger.X) + Sq(l.Z - traeger.Z) > innen * innen)) return new List<Felslage>();
        return lagen.Select(l => l with
        {
            Neigung = 0,
            Versatz = Kartenraster.Punkt(Math.Clamp(oben - _hoehen.Hoehe(l.X, l.Z), -Felsformen.VersatzMax, Felsformen.VersatzMax)),
        }).ToList();
    }

    /// <summary>Wie <c>Felsstrich.ZuNah</c>: zu nah an einem Stück (vorhandene und schon gesetzte); bei „auf Fels“ zählen die Träger nicht.</summary>
    private static bool FuellZuNah(Felsraster raster, Felslage l, double abstand, bool aufFels)
    {
        var meine = Felsformen.Finde(l.Form)?.Ueberlappung ?? Felspinsel.Ueberlappung;
        foreach (var f in raster.Im(l.X, l.Z, (1 - Felspinsel.Ueberlappung) * (l.Radius + raster.GroessterRadius) + abstand))
        {
            if (aufFels && f.Radius >= Felsfuellung.AufFelsRadius) continue;
            var d = (1 - Math.Max(meine, f.Felsform?.Ueberlappung ?? Felspinsel.Ueberlappung)) * (l.Radius + f.Radius) + abstand;
            if (Sq(f.X - l.X) + Sq(f.Z - l.Z) < d * d) return true;
        }
        return false;
    }

    /// <summary>Der Faktor der Kette an einem Ort: Filter des Pinsels (Steigung, Höhe in Metern, Boden), dann Höhe in der Fläche und Rauschen.</summary>
    private double FuellFaktor(Felsregel r, Felspinsel p, double x, double z, double hMin, double hMax, int salz)
    {
        var f = Felsstreuung.Filterfaktor(p, _hoehen, _boden, x, z);
        return f <= 0 ? 0 : f * Felsfuellung.Maske(r, x, z, Anteil(x, z, hMin, hMax), salz);
    }

    /// <summary>Die Höhe am Ort als Anteil zwischen tiefstem und höchstem Punkt der Fläche (flache Fläche: 0).</summary>
    private double Anteil(double x, double z, double hMin, double hMax) =>
        hMax - hMin < 0.01 ? 0 : Math.Clamp((_hoehen.Hoehe(x, z) - hMin) / (hMax - hMin), 0, 1);

    private static int FuellSalz(int startwert, int regel) => unchecked(startwert * 31 + regel * 977 + 13);

    /// <summary>Probenpunkte in der Fläche (Mitten eines Gitters, höchstens <see cref="FuellProbenMax"/> im Rechteck), mit der Fläche je Probe.</summary>
    private List<(double X, double Z)> FuellProben(Felsflaeche a, out double zelle)
    {
        var (b, t) = (a.X1 - a.X0, a.Z1 - a.Z0);
        var schritt = Math.Max(Hoehenraster.Abstand, Math.Sqrt(Math.Max(1, b * t) / FuellProbenMax));
        zelle = schritt * schritt;
        var proben = new List<(double, double)>();
        for (var z = a.Z0 + schritt / 2; z < a.Z1; z += schritt)
            for (var x = a.X0 + schritt / 2; x < a.X1; x += schritt)
                if (a.Enthaelt(x, z) && Kartengrenzen.Aussen.Enthaelt(x, z)) proben.Add((x, z));
        // eine sehr kleine Fläche ohne Probe: ihre Ecken-Mitte zählt mit ihrem Inhalt
        if (proben.Count == 0 && a.IstGueltig)
        {
            var (mx, mz) = (a.Umriss.Average(p => p.X), a.Umriss.Average(p => p.Z));
            if (Kartengrenzen.Aussen.Enthaelt(mx, mz)) proben.Add((mx, mz));
            zelle = a.Flaeche;
        }
        return proben;
    }

    /// <summary>Tiefste und höchste Geländehöhe der Proben.</summary>
    private (double Min, double Max) FuellHoehen(List<(double X, double Z)> proben)
    {
        if (proben.Count == 0) return (0, 0);
        var (min, max) = (double.MaxValue, double.MinValue);
        foreach (var (x, z) in proben)
        {
            var h = _hoehen.Hoehe(x, z);
            (min, max) = (Math.Min(min, h), Math.Max(max, h));
        }
        return (min, max);
    }

    private static double Sq(double x) => x * x;
}
