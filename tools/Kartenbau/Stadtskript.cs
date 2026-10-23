using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Kartenbau;

// ═════════════════════════════════════════════════════════════════════════════
//  Stadtskript: eine Stadt als C#-Klasse (je Stadt eine Datei unter Staedte/)
//
//  Ein Stadtskript legt eine neue Karte an und füllt sie mit denselben Befehlen, die die Kartenwerkstatt benutzt (Kartenentwurf: Setzen,
//  MauerAnlegen, TorSetzen, WegAnlegen, BauzoneAnlegen, NaturflaecheAnlegen, HausHinzufuegen …). So entsteht der Grundriss einer Stadt nach
//  ihrem historischen Vorbild per Code; verfeinert wird er danach in der Kartenwerkstatt, die dieselbe Datei öffnet.
//  Muster: Staedte/Probestadt.cs. Aufruf: dotnet run --project tools/Kartenbau -- bauen probestadt
//
//  Die Hilfen hier sind bewusst schlicht: eine Häuserreihe an einer geraden Straße, eine Ringgasse innerhalb der Mauer, ein Platz mit Umriss,
//  ein Hügel und Rauschen für das Gelände (die Pinsel selbst: E.GelaendePinseln, E.Rampe).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Stadt als Skript. Unterklassen nennen Kennung, Name und Größe und legen in <see cref="Anlegen"/> alles an.</summary>
public abstract class Stadtskript
{
    /// <summary>Kennung der Karte (Dateiname <c>game/karten/&lt;kennung&gt;.json</c>), klein und ohne Umlaute.</summary>
    public abstract string Kennung { get; }

    /// <summary>Anzeigename der Stadt.</summary>
    public abstract string Stadt { get; }

    /// <summary>Eine Zeile für die Liste der Skripte.</summary>
    public virtual string Beschreibung => "";

    /// <summary>Größe des Spielfelds in Metern um den Ursprung (Marktplatz): Breite in x, Tiefe in z (400 × 300 bis 2.000 × 2.000).</summary>
    public virtual (double Breite, double Tiefe) Groesse => (400, 300);

    /// <summary>Breite des Randstreifens um das Spielfeld (Meter, 100–600; ohne Angabe die Vorgabe 300, dann steht sie nicht in der Datei).</summary>
    public virtual double? Rand => null;

    /// <summary>
    /// Startwert für den Rand-Vorschlag (Felder und Waldstücke im Randstreifen, <see cref="Kartenentwurf.RandVorschlagen"/>), den das Skript nach
    /// <see cref="Anlegen"/> macht – null: keiner (der Randstreifen bleibt Wiese, bis jemand ihn gestaltet).
    /// </summary>
    public virtual int? RandVorschlag => null;

    /// <summary>Der Entwurf, an dem <see cref="Anlegen"/> arbeitet.</summary>
    protected Kartenentwurf E { get; private set; } = null!;

    /// <summary>Legt Mauer, Tore, Wege, Plätze, Bauzonen, Natur und Startbelegung an (mit den Befehlen von <see cref="E"/>).</summary>
    protected abstract void Anlegen();

    /// <summary>Führt das Skript aus: eine neue, leere Karte, dann <see cref="Anlegen"/>. Geschrieben wird hier nichts.</summary>
    public Kartenentwurf Bauen(Gebaeudekatalog katalog, Wirtschaftsdaten daten)
    {
        E = Kartenentwurf.Neu(Kennung, Stadt, Groesse.Breite, Groesse.Tiefe, katalog);
        E.Daten = daten;
        if (Rand is { } rand) E.RandbreiteSetzen(rand);
        Anlegen();
        if (RandVorschlag is { } startwert) E.RandVorschlagen(startwert);
        return E;
    }

    /// <summary>Alle Stadtskripte dieses Programms (jede nicht abstrakte Unterklasse mit leerem Konstruktor), nach Kennung.</summary>
    public static IReadOnlyList<Stadtskript> Alle() => typeof(Stadtskript).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(Stadtskript)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
        .Select(t => (Stadtskript)Activator.CreateInstance(t)!).OrderBy(s => s.Kennung, StringComparer.Ordinal).ToList();

    /// <summary>Das Skript mit dieser Kennung (oder diesem Klassennamen, ohne Groß- und Kleinschreibung) – oder null.</summary>
    public static Stadtskript? Finde(string name) =>
        Alle().FirstOrDefault(s => string.Equals(s.Kennung, name, StringComparison.OrdinalIgnoreCase) || string.Equals(s.GetType().Name, name, StringComparison.OrdinalIgnoreCase));

    // ── Hilfen ───────────────────────────────────────────────────────────────

    /// <summary>Ein Punkt (x = Osten, z = Süden, Meter).</summary>
    protected static Kartenort P(double x, double z) => new(x, z);

    /// <summary>
    /// Eine Häuserreihe entlang der geraden Strecke <paramref name="von"/> → <paramref name="bis"/> (die Mitte einer Straße): Bausteine
    /// <paramref name="katalog"/> nebeneinander, mit der Vorderseite zur Straße, die Vorderkante <paramref name="abstand"/> Meter von der
    /// Straßenmitte, <paramref name="luecke"/> Meter zwischen zwei Häusern. <paramref name="links"/>: auf der linken Seite in Laufrichtung
    /// (von oben gesehen, Norden oben), sonst rechts. So viele, wie auf die Strecke passen (höchstens <paramref name="hoechstens"/>), mittig
    /// verteilt. Gibt die gesetzten Gebäude zurück (Kennung und Name vergibt der Entwurf; <paramref name="name"/> benennt sie „Name 1“, „Name 2“ …).
    /// </summary>
    protected List<EntwurfGebaeude> Haeuserreihe(string katalog, Kartenort von, Kartenort bis, bool links, double abstand, double luecke = 1,
        int hoechstens = int.MaxValue, string? name = null)
    {
        var b = E.Katalog.Finde(katalog)?.Groesse ?? throw new ArgumentException($"Der Baustein „{katalog}“ hat keine Größe.", nameof(katalog));
        var (breite, tiefe) = (b[0], b[1]);
        var laenge = von.Abstand(bis);
        var (ux, uz) = ((bis.X - von.X) / laenge, (bis.Z - von.Z) / laenge);
        // links in Laufrichtung (Norden oben): für die Richtung Osten (1, 0) ist das Norden (0, −1)
        var (nx, nz) = links ? (uz, -ux) : (-uz, ux);
        var anzahl = Math.Min(hoechstens, (int)Math.Floor((laenge + luecke) / (breite + luecke)));
        var rand = (laenge - (anzahl * breite + (anzahl - 1) * luecke)) / 2;
        var drehung = Math.Atan2(-nx, -nz) * 180 / Math.PI; // die Vorderseite zeigt zur Straße (−n)
        var liste = new List<EntwurfGebaeude>();
        for (var i = 0; i < anzahl; i++)
        {
            var s = rand + breite / 2 + i * (breite + luecke);
            var (x, z) = (von.X + ux * s + nx * (abstand + tiefe / 2), von.Z + uz * s + nz * (abstand + tiefe / 2));
            var g = E.Setzen(katalog, x, z, drehung);
            if (name is not null) E.Umbenennen(g.Kennung, $"{name} {i + 1}");
            liste.Add(g);
        }
        return liste;
    }

    /// <summary>
    /// Eine Ringgasse innerhalb einer geschlossenen Mauer: die Mauerlinie um <paramref name="abstand"/> Meter nach innen versetzt (jede Seite
    /// parallel, die Ecken dort, wo sich die versetzten Seiten schneiden), als geschlossener Weg (der letzte Punkt ist der erste).
    /// </summary>
    protected EntwurfWeg Ringgasse(EntwurfMauer mauer, double abstand, Wegklasse klasse = Wegklasse.Ring, string name = "Ringgasse", double breite = 0)
    {
        if (!mauer.Geschlossen) throw new ArgumentException("Eine Ringgasse braucht eine geschlossene Mauer.", nameof(mauer));
        var ring = Versetzt(mauer.Ecken, -abstand);
        return E.WegAnlegen(klasse, ring.Append(ring[0]).ToList(), glatt: false, name: name, breite: breite, einrasten: false);
    }

    /// <summary>
    /// Ein Platz mit Umriss: ein Rechteck <paramref name="breite"/> × <paramref name="tiefe"/> Meter um <paramref name="mitte"/>, rundum
    /// ein geschlossener Weg (z. B. die Gassen um den Markt). Gibt den Weg zurück; seine Ecken sind die Ecken des Platzes.
    /// </summary>
    protected EntwurfWeg Platz(Kartenort mitte, double breite, double tiefe, string name, Wegklasse klasse = Wegklasse.Gasse, double wegbreite = 0)
    {
        var (b, t) = (breite / 2, tiefe / 2);
        var ecken = new[] { P(mitte.X - b, mitte.Z - t), P(mitte.X + b, mitte.Z - t), P(mitte.X + b, mitte.Z + t), P(mitte.X - b, mitte.Z + t) };
        return E.WegAnlegen(klasse, ecken.Append(ecken[0]).ToList(), glatt: false, name: name, breite: wegbreite, einrasten: false);
    }

    /// <summary>
    /// Ein Hügel (oder mit negativer <paramref name="hoehe"/> eine Senke): ein Tupfer des Geländepinsels Heben bzw. Senken mit ganz weicher
    /// Kante – in der Mitte <paramref name="hoehe"/> Meter, zum <paramref name="radius"/> hin auslaufend. Das steilste Gefälle liegt auf halbem
    /// Radius und ist etwa 1,5 · Höhe / Radius (6 m auf 200 m: 4,5 %). Ein Schritt.
    /// </summary>
    protected void Huegel(Kartenort mitte, double radius, double hoehe) =>
        E.GelaendePinseln(new Pinsel(hoehe >= 0 ? Pinselart.Heben : Pinselart.Senken, radius, Math.Abs(hoehe), Kante: 1), new[] { mitte });

    /// <summary>
    /// Natürliche Unebenheit entlang eines Strichs (Geländepinsel Rauschen): Buckel von etwa <paramref name="massstab"/> Metern, bis zu
    /// <paramref name="staerke"/> Meter nach oben und unten, aus dem Startwert. Ein Schritt.
    /// </summary>
    protected void Rauschen(IReadOnlyList<Kartenort> strich, double radius, double staerke, double massstab, int startwert, double kante = 0.6) =>
        E.GelaendePinseln(new Pinsel(Pinselart.Rauschen, radius, staerke, kante, startwert, massstab), strich);

    /// <summary>
    /// Ein Vieleck um <paramref name="abstand"/> Meter versetzt: positiv nach außen, negativ nach innen. Jede Seite wandert parallel, die neuen
    /// Ecken liegen auf den Schnittpunkten der Nachbarseiten (gerade Fortsetzungen: die Ecke wandert nur).
    /// </summary>
    protected static List<Kartenort> Versetzt(IReadOnlyList<Kartenort> ecken, double abstand)
    {
        var n = ecken.Count;
        // Umlaufsinn: Mit x nach Osten und z nach Süden zeigt die linke Seite in Laufrichtung bei positiver Fläche nach außen.
        var flaeche = 0.0;
        for (var i = 0; i < n; i++) flaeche += ecken[i].X * ecken[(i + 1) % n].Z - ecken[(i + 1) % n].X * ecken[i].Z;
        var aussen = flaeche > 0 ? 1 : -1;
        (Kartenort A, Kartenort B) Seite(int i)
        {
            var (a, b) = (ecken[i], ecken[(i + 1) % n]);
            var l = a.Abstand(b);
            var (nx, nz) = ((b.Z - a.Z) / l * aussen * abstand, -(b.X - a.X) / l * aussen * abstand);
            return (P(a.X + nx, a.Z + nz), P(b.X + nx, b.Z + nz));
        }
        var liste = new List<Kartenort>();
        for (var i = 0; i < n; i++)
        {
            var (a1, b1) = Seite((i + n - 1) % n);
            var (a2, b2) = Seite(i);
            var (rx, rz, sx, sz) = (b1.X - a1.X, b1.Z - a1.Z, b2.X - a2.X, b2.Z - a2.Z);
            var nenner = rx * sz - rz * sx;
            if (Math.Abs(nenner) < 1e-9) liste.Add(a2); // gerade weiter: keine Ecke
            else
            {
                var t = ((a2.X - a1.X) * sz - (a2.Z - a1.Z) * sx) / nenner;
                liste.Add(P(a1.X + t * rx, a1.Z + t * rz));
            }
        }
        return liste;
    }
}
