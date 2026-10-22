using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Größe, Randstreifen und Kamera-Start im Kartenentwurf (Kartenwerkstatt, Schritt 5a)
//
//  Größe und Mitte des Spielfelds, die Breite des Randstreifens und der Blick beim Start stehen im Kopf der Karte (EntwurfKopf). Jede
//  Änderung ist ein Befehl mit Rückgängig; was danach außerhalb liegt, meldet die Prüfung (Kartenpruefung, Regeln „rand“, „ausserhalb“,
//  „kamera“). Der Rand-Vorschlag füllt den Randstreifen mit Feldern und Waldstücken – normale Einträge der Karte, die man übermalen kann.
// ═════════════════════════════════════════════════════════════════════════════

public sealed partial class Kartenentwurf
{
    /// <summary>
    /// Größe des Spielfelds: Breite (x) und Tiefe (z) auf 50 m gerundet, 400 × 300 m bis 2.000 × 2.000 m; <paramref name="mitte"/> (auf ganze
    /// Meter) verschiebt das Spielfeld, ohne Angabe bleibt die Mitte. Ein Schritt; ändert sich nichts, keiner.
    /// </summary>
    public void GroesseSetzen(double breite, double tiefe, Kartenort? mitte = null)
    {
        var (b, t) = (Kartengrenzen.SeiteRunden(breite, Kartengrenzen.MinBreite), Kartengrenzen.SeiteRunden(tiefe, Kartengrenzen.MinTiefe));
        var m = mitte is { } p ? new Kartenort(Kartenraster.Runden(p.X) + 0.0, Kartenraster.Runden(p.Z) + 0.0) : _kopf.Mitte;
        KopfAendern($"Kartengröße {Kartenschreiber.Zahl(b)} × {Kartenschreiber.Zahl(t)} m", n =>
        {
            (n.Breite, n.Tiefe, n.Mitte, n.GroesseInDatei) = (b, t, m, true);
            if (m != default) n.MitteInDatei = true;
        });
    }

    /// <summary>Breite des Randstreifens: auf 50 m gerundet, 100 bis 600 m (Vorgabe 300). Ein Schritt.</summary>
    public void RandbreiteSetzen(double meter)
    {
        var r = Kartengrenzen.RandRunden(meter);
        KopfAendern($"Randbreite {Kartenschreiber.Zahl(r)} m", n => (n.Randbreite, n.RandInDatei) = (r, true));
    }

    /// <summary>
    /// Der Blick beim Start: Punkt (auf 5 cm), Zoom (sichtbare Bildhöhe in Metern, auf 0,1) und Drehung in Grad (0 = die gewohnte Blickrichtung,
    /// auf 0,1°). Die Kartenwerkstatt übernimmt ihn per Knopf aus dem aktuellen Blick. Ein Schritt; außerhalb des Spielfelds ist er ein Fehler.
    /// </summary>
    public void KameraStartSetzen(Kartenort punkt, double zoom, double drehung = 0)
    {
        var p = Gerundet(punkt);
        var kamera = new Kartenkamera { Punkt = new[] { p.X, p.Z }, Zoom = Math.Round(zoom, 1), Drehung = Kartenraster.Drehung(drehung) };
        KopfAendern("Kamera-Start gesetzt", n => n.Kamera = kamera);
    }

    private void KopfAendern(string beschreibung, Action<EntwurfKopf> aenderung)
    {
        if (Aenderung(_kopf, aenderung) is { } befehl) Geschichte.Ausfuehren(befehl.Benannt(beschreibung));
    }

    // ── Rand-Vorschlag ───────────────────────────────────────────────────────

    /// <summary>Anteile im Rand-Vorschlag: Feld, Wald, Streu (der Rest bleibt Wiese).</summary>
    public const double VorschlagFeld = 0.55, VorschlagWald = 0.25, VorschlagStreu = 0.1;

    /// <summary>Früchte der vorgeschlagenen Felder mit Gewicht: Winterkorn, Sommerkorn, Brache und Flachs, mal rein, mal gemischt.</summary>
    private static readonly (double Gewicht, (string Frucht, double Anteil)[] Fruechte)[] FeldVorlagen =
    {
        (3, new[] { ("roggen", 1.0) }),
        (2, new[] { ("weizen", 1.0), ("roggen", 0.3) }),
        (2, new[] { ("hafer", 1.0), ("gerste", 0.6) }),
        (1, new[] { ("gerste", 1.0) }),
        (2, new[] { ("brache", 1.0) }),
        (1, new[] { ("flachs", 1.0), ("brache", 0.3) }),
        (2, new[] { ("roggen", 1.0), ("hafer", 0.7), ("brache", 0.4) }),
    };

    /// <summary>
    /// Schlägt eine Randlandschaft vor: Der Randstreifen wird in Stücke von etwa 150 m geteilt (bei 300 m Rand zwei Reihen); jedes Stück wird
    /// mit dem Zufall aus <paramref name="startwert"/> ein Feld (55 %, manchmal in zwei Felder mit verschiedener Richtung geteilt), ein Waldstück
    /// (25 %), eine Streufläche (10 %) oder bleibt Wiese. Zwischen den Stücken bleiben Raine von einigen Metern; Wege im Rand bleiben von selbst
    /// frei (Naturfreiraum). <paramref name="ersetzen"/>: vorher alle Natur- und Feldflächen löschen, die ganz im Rand liegen (ein früherer
    /// Vorschlag); Stücke, die eine verbliebene Fläche schon bedeckt, bleiben aus. Ohne Startwert einer aus der Kennung der Karte – gleiche Karte,
    /// gleicher Vorschlag. Ein Schritt. Gibt die neuen Flächen zurück.
    /// </summary>
    public IReadOnlyList<EntwurfFlaeche> RandVorschlagen(int? startwert = null, bool ersetzen = true)
    {
        var g = Kartengrenzen;
        var (spielfeld, aussen, rand) = (g.Spielfeld, g.Aussen, g.Randbreite);
        var zufall = new Random(startwert ?? (int)(NaturVerteilung.Streuung(Kennung.Length, Kennung.Aggregate(17L, (h, c) => h * 31 + c)) & 0x7fffffff));
        var befehle = new List<IKartenbefehl>();

        bool GanzImRand(EntwurfFlaeche f) => Kartenrechteck.Um(f.Ecken) is { } r && !r.Ueberdeckt(spielfeld);
        var weg = new List<EntwurfFlaeche>();
        if (ersetzen)
        {
            // von hinten nach vorn: Rückgängig setzt jede an ihre alte Stelle zurück
            foreach (var (f, i) in _naturflaechen.Select((f, i) => (f, i)).Where(x => GanzImRand(x.f)).Reverse().ToList())
            {
                befehle.Add(new TeilLoeschen(f, i));
                weg.Add(f);
            }
            foreach (var (f, i) in _felder.Select((f, i) => (f, i)).Where(x => GanzImRand(x.f)).Reverse().ToList())
            {
                befehle.Add(new TeilLoeschen(f, i));
                weg.Add(f);
            }
        }
        var bleiben = _naturflaechen.Cast<EntwurfFlaeche>().Concat(_felder).Where(f => !weg.Contains(f) && f.Ecken.Count >= 3).ToList();
        bool Bedeckt(Kartenort p) => bleiben.Any(f => Kartengeometrie.ImVieleck(p, f.Ecken) && !f.Aussparungen.Any(a => Kartengeometrie.ImVieleck(p, a)));

        var neueNatur = new List<EntwurfNaturflaeche>();
        var neueFelder = new List<EntwurfFeld>();
        var naturNamen = _naturflaechen.Where(f => !weg.Contains(f)).Select(f => f.Name).ToList();
        var feldNamen = _felder.Where(f => !weg.Contains(f)).Select(f => f.Name).ToList();
        var naturStart = _naturflaechen.Count == 0 ? 1000 : _naturflaechen.Max(n => n.Startwert) + 1;
        var feldStart = _felder.Count == 0 ? 2000 : _felder.Max(n => n.Startwert) + 1;

        // Die vier Bänder des Rands: Norden und Süden über die ganze Breite, Westen und Osten dazwischen
        var baender = new[]
        {
            (new Kartenrechteck(aussen.X0, aussen.Z0, aussen.X1, spielfeld.Z0), Quer: false),
            (new Kartenrechteck(aussen.X0, spielfeld.Z1, aussen.X1, aussen.Z1), Quer: false),
            (new Kartenrechteck(aussen.X0, spielfeld.Z0, spielfeld.X0, spielfeld.Z1), Quer: true),
            (new Kartenrechteck(spielfeld.X1, spielfeld.Z0, aussen.X1, spielfeld.Z1), Quer: true),
        };
        var reihen = rand >= 200 ? 2 : 1;
        foreach (var (band, quer) in baender)
        {
            var (lang, kurz) = quer ? (band.Tiefe, band.Breite) : (band.Breite, band.Tiefe);
            if (lang < 1 || kurz < 1) continue;
            var stuecke = Math.Max(1, (int)Math.Round(lang / (kurz / reihen * 1.2)));
            for (var i = 0; i < stuecke; i++)
                for (var j = 0; j < reihen; j++)
                {
                    var (a0, a1) = (lang * i / stuecke, lang * (i + 1) / stuecke);
                    var (b0, b1) = (kurz * j / reihen, kurz * (j + 1) / reihen);
                    var zelle = quer
                        ? new Kartenrechteck(band.X0 + b0, band.Z0 + a0, band.X0 + b1, band.Z0 + a1)
                        : new Kartenrechteck(band.X0 + a0, band.Z0 + b0, band.X0 + a1, band.Z0 + b1);
                    Stueck(zelle, quer);
                }
        }

        void Stueck(Kartenrechteck zelle, bool quer)
        {
            // Raine: jede Seite 3–10 m nach innen
            var r = new Kartenrechteck(zelle.X0 + Wurf(3, 10), zelle.Z0 + Wurf(3, 10), zelle.X1 - Wurf(3, 10), zelle.Z1 - Wurf(3, 10));
            var wahl = zufall.NextDouble();
            var teilen = zufall.NextDouble() < 0.5;
            var drehen = zufall.NextDouble();
            if (r.Breite < 20 || r.Tiefe < 20) return;
            if (Bedeckt(r.Mitte) || EckenVon(r).Any(Bedeckt)) return;
            if (wahl < VorschlagFeld)
            {
                // Die Streifen laufen längs oder quer zum Band, um bis zu 12° verdreht; geteilte Stücke bekommen zwei Richtungen.
                var basis = (quer ? 0 : 90) + (drehen < 0.5 ? 0 : 90);
                if (teilen && Math.Max(r.Breite, r.Tiefe) >= 80)
                {
                    var anteil = 0.4 + 0.2 * zufall.NextDouble();
                    var (h1, h2) = r.Breite >= r.Tiefe
                        ? (r with { X1 = r.X0 + r.Breite * anteil - 2 }, r with { X0 = r.X0 + r.Breite * anteil + 2 })
                        : (r with { Z1 = r.Z0 + r.Tiefe * anteil - 2 }, r with { Z0 = r.Z0 + r.Tiefe * anteil + 2 });
                    Feld(h1, basis);
                    Feld(h2, basis + 90);
                }
                else Feld(r, basis);
            }
            else if (wahl < VorschlagFeld + VorschlagWald) Natur(r, "wald");
            else if (wahl < VorschlagFeld + VorschlagWald + VorschlagStreu) Natur(r, "streu");
        }

        void Feld(Kartenrechteck r, double basis)
        {
            var f = NeuesFeld(EckenVon(r), null, basis + Wurf(-12, 12), feldNamen, feldStart++);
            feldNamen.Add(f.Name);
            f.Streifen = Math.Round(Wurf(6, 10) * 2) / 2;
            f.Laenge = Math.Round(Wurf(30, 60) / 5) * 5;
            var vorlage = NachGewicht(FeldVorlagen, v => v.Gewicht);
            f.FruechteSetzen(vorlage.Fruechte.Select(x => KeyValuePair.Create(x.Frucht, x.Anteil)));
            neueFelder.Add(f);
        }

        void Natur(Kartenrechteck r, string art)
        {
            // Ein unregelmäßiges Vieleck mit 7–10 Ecken in der Zelle
            var n = 7 + zufall.Next(4);
            var m = r.Mitte;
            var ecken = Enumerable.Range(0, n).Select(k =>
            {
                var winkel = (k + Wurf(-0.3, 0.3)) / n * Math.Tau;
                var weite = Wurf(0.65, 1);
                return new Kartenort(Math.Round(m.X + Math.Cos(winkel) * r.Breite / 2 * weite), Math.Round(m.Z + Math.Sin(winkel) * r.Tiefe / 2 * weite));
            }).ToList();
            var f = new EntwurfNaturflaeche
            {
                Name = FreierName(art == "wald" ? "Wald im Rand" : "Streu im Rand", naturNamen), Art = art,
                Dichte = art == "wald" ? WaldDichte : StreuDichte, Startwert = naturStart++,
            };
            naturNamen.Add(f.Name);
            f.GruppenSetzen(VorlageGruppen(art));
            f.VieleckSetzen(-1, Ecken(ecken, f));
            if (f.Ecken.Count >= 3) neueNatur.Add(f);
        }

        double Wurf(double von, double bis) => von + (bis - von) * zufall.NextDouble();
        T NachGewicht<T>(IReadOnlyList<T> liste, Func<T, double> gewicht)
        {
            var w = zufall.NextDouble() * liste.Sum(gewicht);
            foreach (var x in liste)
                if ((w -= gewicht(x)) < 0) return x;
            return liste[^1];
        }
        static List<Kartenort> EckenVon(Kartenrechteck r) => new()
        {
            new(Math.Round(r.X0), Math.Round(r.Z0)), new(Math.Round(r.X1), Math.Round(r.Z0)), new(Math.Round(r.X1), Math.Round(r.Z1)), new(Math.Round(r.X0), Math.Round(r.Z1)),
        };

        var (natur0, felder0) = (_naturflaechen.Count - weg.OfType<EntwurfNaturflaeche>().Count(), _felder.Count - weg.OfType<EntwurfFeld>().Count());
        befehle.AddRange(neueNatur.Select((f, i) => (IKartenbefehl)new TeilSetzen(f, natur0 + i)));
        befehle.AddRange(neueFelder.Select((f, i) => (IKartenbefehl)new TeilSetzen(f, felder0 + i)));
        if (befehle.Count > 0) Geschichte.Ausfuehren(new Sammelbefehl("Randlandschaft vorgeschlagen", befehle));
        return neueNatur.Cast<EntwurfFlaeche>().Concat(neueFelder).ToList();
    }
}
