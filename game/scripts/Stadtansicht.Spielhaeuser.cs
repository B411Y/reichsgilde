using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Wohnhäuser der Spielhäuser (Spieler und KI) – siebter Teil der <c>partial class Stadtansicht</c>.
///
/// Der Kern (<c>Wohnungswesen</c>) führt je Haus die Stufe (Fachwerkhaus, Bürgerhaus, Steinhaus, Stadtpalais, Hof),
/// einen laufenden Ausbau (<c>FertigAb</c>) und einen Brandschaden (<c>WohnhausInstandBis</c>). Die Darstellung liest das
/// jedes Bild, baut das Haus aber nur neu, wenn sich etwas davon ändert.
///
/// Jedes Spielhaus bekommt einen <b>Doppelplatz am Ring</b> (zwei Plätze nebeneinander, 17 m Front) – die Lagen nächst
/// dem Markt. So passen auch Stadtpalais und Hof. Diese Plätze fehlen den Einwohnern, deren Häuser rücken weiter
/// nach außen bzw. in die Vorstadt.
/// </summary>
public partial class Stadtansicht
{
    private List<Hausplatz>? _spielhausPlaetze;

    /// <summary>
    /// Die Doppelplätze der Spielhäuser am Ring: je zwei benachbarte Ringplätze derselben Seite, die nächsten am Markt zuerst.
    /// Die Karte (Wohnhausplätze in <c>greybox.json</c>) bildet diese Plätze ab; hier werden sie nur noch gebraucht, um sie den
    /// Einwohnern wegzunehmen (<see cref="BelegteRingplaetze"/>) und für die Muster-Häuser im Hauptmenü.
    /// Gibt die Mitte (zwischen den beiden Plätzen) zurück.
    /// </summary>
    private List<Hausplatz> SpielhausPlaetze(int anzahl)
    {
        if (_spielhausPlaetze is not null) return _spielhausPlaetze;
        var ring = RingPlaetze();
        var paare = new List<(Hausplatz A, Hausplatz B, Hausplatz Mitte)>();
        for (var i = 0; i + 1 < ring.Count; i++)
        {
            var (a, b) = (ring[i], ring[i + 1]);
            if (a.Vorne != b.Vorne || a.Mitte.DistanceTo(b.Mitte) > PlatzBreite + 0.1f) continue;
            paare.Add((a, b, new Hausplatz((a.Mitte + b.Mitte) / 2, a.Vorne, true)));
        }
        var gewaehlt = new List<(Hausplatz A, Hausplatz B, Hausplatz Mitte)>();
        foreach (var paar in paare.OrderBy(p => p.Mitte.Mitte.Length()).ThenBy(p => p.Mitte.Mitte.X).ThenBy(p => p.Mitte.Mitte.Z))
        {
            if (gewaehlt.Count >= anzahl) break;
            if (gewaehlt.Any(g => g.A == paar.A || g.A == paar.B || g.B == paar.A || g.B == paar.B)) continue;
            gewaehlt.Add(paar);
        }
        BelegteRingplaetze = gewaehlt.SelectMany(g => new[] { g.A, g.B }).ToHashSet();
        return _spielhausPlaetze = gewaehlt.Select(g => g.Mitte).ToList();
    }

    /// <summary>Ringplätze, die ein Spielhaus belegt – die bekommen keine Einwohner.</summary>
    private HashSet<Hausplatz> BelegteRingplaetze { get; set; } = new();

    /// <summary>Wie viele Spielhäuser einen Platz brauchen: alle Konten, sobald es Wohnhäuser gibt; im Hauptmenü vier zur Zierde.</summary>
    private int AnzahlSpielhaeuser() =>
        _simulation?.Wirtschaft is { } w ? (w.Wohnen.IstAktiv ? w.Konten.Count : 0) : 4;

    /// <summary>Im Hauptmenü: vier Spielhäuser in verschiedenen Stufen, damit man sieht, was es gibt.</summary>
    private void SpielhaeuserMuster()
    {
        var plaetze = SpielhausPlaetze(4);
        var farben = new[] { Spieler, Rivale, ZweiterRivale, DritterRivale };
        for (var i = 0; i < plaetze.Count; i++)
            SpielhausModell(plaetze[i].Mitte, Mathf.Atan2(plaetze[i].Vorne.X, plaetze[i].Vorne.Z), DoppelplatzBreite, PlatzTiefe, PlatzTiefe / 2,
                            4 - i, false, false, false, farben[i], null, null);
    }

    public static string WohnhausKennung(Konto k) => "wohnhaus:" + k.Inhaber;

    // ═════════════════════════════════════════════════════════════════════════
    //  Modelle der Stufen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Breite eines Doppelplatzes am Ring – so breit sind auch die Wohnhausplätze der Karte (17 m).</summary>
    private const float DoppelplatzBreite = 17f;

    /// <summary>
    /// Baut das Wohnhaus einer Stufe auf einer Fläche (Vorderseite lokal +Z, zur Gasse): die Plätze der Karte sind 17 × 8 m
    /// (Doppelplatz am Ring), Neubauten 8 × 10, 12 × 10 oder 16 × 14 m mit 2 m Vorplatz davor (<paramref name="vorne"/> ist die lokale
    /// z-Koordinate der Hausfront). Die Häuser kommen als Modelle aus Blender (0 Fachwerkhaus, 1 Bürgerhaus, 2 Steinhaus, 3 Stadtpalais,
    /// 4 Stadthof), eingesetzt über <see cref="ModellEinsetzen"/>; fehlt eine Datei, meldet das Spiel einen Fehler und das Haus fehlt.
    /// Bei laufendem Ausbau steht ein Gerüst davor, nach einem Brand ist das Dach rußig, im Leerstand sind Tür und Fenster vernagelt.
    /// <paramref name="ankerKennung"/> ist der Schlüssel für Tür- und Karrenanker (<see cref="TuerAnker"/>); ohne Angabe die <paramref name="kennung"/>.
    /// </summary>
    private Node3D SpielhausModell(Vector3 mitte, float drehungY, float platzBreite, float platzTiefe, float vorne,
                                   int stufe, bool imBau, bool brand, bool leer, Color? farbe, string? kennung, string? name, string? zusatz = null,
                                   string? ankerKennung = null)
    {
        var g = new Node3D { Name = "Spielhaus", Position = mitte, Rotation = new Vector3(0, drehungY, 0) };
        AddChild(g);
        if (stufe >= 0 && stufe < WohnhausDateien.Length)
            ModellEinsetzen(WohnhausDateien[stufe], g, new Vector3(0, 0, vorne - ModellFront(stufe)),
                new ModellZustand(Geruest: imBau || brand, Leerstand: leer, Brand: brand, Besitzer: farbe), ankerKennung ?? kennung);
        else
            GD.PushError($"Wohnhaus-Stufe {stufe} gibt es nicht.");

        if (kennung is not null)
        {
            Klickflaeche(kennung, new Vector3(0, 5, 0), new Vector3(Mathf.Min(16, platzBreite), 10, platzTiefe), g);
            var text = name + (zusatz is not null ? $" ({zusatz})" : imBau ? " (Ausbau)" : brand ? " (nach Brand)" : leer ? " (Leerstand)" : "");
            Beschriftung(text, new Vector3(0, 16, 0), 44, g, kennung);
        }
        return g;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lage der Wohnhaus-Modelle (Einsetzen: Stadtansicht.Modelle.cs)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Dateien der Wohnhaus-Modelle je Stufe (alle fünf sind Pflicht).</summary>
    private static readonly string[] WohnhausDateien = { "fachwerkhaus", "buergerhaus", "steinhaus", "stadtpalais", "stadthof" };

    /// <summary>Tiefe der belegten Fläche je Stufe in Metern, inklusive 2 m Vorplatz (LEVELDESIGN, Gebäudegrößen).</summary>
    private static readonly float[] ModellTiefe = { 10, 10, 10, 10, 14 };

    /// <summary>
    /// Wo die Hausfront in den Modellen liegt: Der Ursprung ist die Mitte der belegten Fläche, davon 2 m Vorplatz – die Front liegt
    /// also bei z = Tiefe/2 − 2 (+3 bei 10 m, +5 beim Stadthof). Auf einem Neubau sitzt das Modell damit genau in der Mitte; auf den
    /// Wohnhausplätzen der Karte (8 m tief, Front an der Gasse) steht die Front an der Gasse und der Rest ragt nach hinten.
    /// </summary>
    private static float ModellFront(int stufe) => ModellTiefe[stufe] / 2 - 2;

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahltafel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Text für die Auswahltafel eines Spielhaus-Wohnhauses. Null, wenn die Kennung keines ist.</summary>
    public string? WohnhausText(string kennung)
    {
        if (!kennung.StartsWith("wohnhaus:") || _simulation?.Wirtschaft is not { } w) return null;
        var g = w.Bauen.Grundstuecke.FirstOrDefault(x => x.Art == Reichsgilde.Core.Bauen.Grundstuecksart.Wohnhaus && x.Arbeit is null
                                                         && x.Besitzer is { } b && WohnhausKennung(b) == kennung);
        if (g?.Besitzer is not { } k) return null;
        var minute = _simulation.Zeit.VergangeneMinuten;
        var zeilen = new List<string> { $"Wohnhaus – {k.Inhaber}", "" };
        if (!k.IstAktiv)
        {
            zeilen.Add($"Leerstand: {k.Inhaber} {AusDemSpielGrund(k)}. Das {w.Wohnen.Stufe(k)?.Name} steht leer.");
            return string.Join("\n", zeilen);
        }
        zeilen.Add($"{w.Wohnen.Stufe(k)?.Name}" + (w.Wohnen.Stufe(k) is { Ansehen: > 0 } s ? $" · +{s.Ansehen:0} Ansehen" : ""));
        if (k.WohnhausFertigAb is { } fertig && fertig > minute)
            zeilen.Add($"Ausbau zum {w.Wohnen.NaechsteStufe(k)?.Name} – fertig {FertigText(fertig)}");
        if (k.WohnhausInstandBis is { } instand && instand > minute)
            zeilen.Add($"Nach einem Brand wird es instand gesetzt (bis {new Reichsgilde.Core.Zeit.Spielzeit(instand).UhrzeitText} Uhr) – bis dahin fehlt sein Ansehen.");
        if (k.Hauswaechter) zeilen.Add("Ein Hauswächter bewacht es.");
        return string.Join("\n", zeilen);
    }

    /// <summary>Warum ein Haus nicht mehr mitspielt: „ist bankrott" oder „ist erloschen" (ohne Erben).</summary>
    private static string AusDemSpielGrund(Konto k) => k.IstBankrott ? "ist bankrott" : "ist erloschen";

    /// <summary>„gegen 14:30 Uhr" am selben Spieltag, sonst das Datum.</summary>
    private string FertigText(long minute)
    {
        var zeit = new Reichsgilde.Core.Zeit.Spielzeit(minute);
        return zeit.Jahr == _simulation!.Zeit.Jahr ? "gegen " + zeit.UhrzeitText + " Uhr" : zeit.DatumText;
    }
}
