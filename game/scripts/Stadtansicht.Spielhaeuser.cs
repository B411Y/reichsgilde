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
/// Jedes Spielhaus bekommt einen <b>Wohnhausplatz der Karte</b> (in der Greybox ein Doppelplatz am Ring, 17 m Front); welchen, legt der Kern
/// fest (Grundstücke, Stadtansicht.Bauen.cs). So passen auch Stadtpalais und Hof. Wohnhausplätze ohne Spielhaus nehmen Einwohnerhäuser auf
/// (Stadtansicht.Wohnviertel.cs).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>So viele Spielhäuser zeigt das Hauptmenü zur Zierde.</summary>
    private const int MusterSpielhaeuser = 4;

    /// <summary>
    /// Im Hauptmenü: Spielhäuser in verschiedenen Stufen auf den ersten Wohnhausplätzen der Karte, in den Farben der Startbelegung (Spieler, dann die
    /// KI-Häuser), damit man sieht, was es gibt.
    /// </summary>
    private void SpielhaeuserMuster()
    {
        if (_karte is null) return;
        var start = _karte.Startbelegung;
        var farben = start.Haeuser.Select(h => h.Farbe).Prepend(start.Spieler.Farbe).ToList();
        var plaetze = _karte.Wohnhausplaetze.Take(MusterSpielhaeuser).ToList();
        for (var i = 0; i < plaetze.Count; i++)
        {
            var g = plaetze[i];
            var groesse = GroesseVon(g);
            var farbe = i < farben.Count && farben[i] is { } text && Color.HtmlIsValid(text) ? Color.FromHtml(text) : i == 0 ? Spieler : Rivale;
            SpielhausModell(Punkt3(g.Punkt), DrehungVon(g), groesse.X, groesse.Y, groesse.Y / 2, Math.Max(0, 4 - i), false, false, false, farbe, null, null);
        }
    }

    public static string WohnhausKennung(Konto k) => "wohnhaus:" + k.Inhaber;

    // ═════════════════════════════════════════════════════════════════════════
    //  Modelle der Stufen
    // ═════════════════════════════════════════════════════════════════════════

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
        if (Katalog?.Wohnhaus(stufe) is { Modell: { } datei } baustein)
            ModellEinsetzen(datei, g, new Vector3(0, 0, vorne - ModellFront(baustein)),
                new ModellZustand(Geruest: imBau || brand, Leerstand: leer, Brand: brand, Besitzer: farbe), ankerKennung ?? kennung);
        else
            GD.PushError($"Das Wohnhaus der Stufe {stufe} fehlt im Gebäudekatalog.");

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

    /// <summary>
    /// Wo die Hausfront in den Modellen liegt: Der Ursprung ist die Mitte der belegten Fläche (Tiefe aus dem Gebäudekatalog, mit Vorplatz),
    /// davon 2 m Vorplatz – die Front liegt also bei z = Tiefe/2 − 2 (+3 bei 10 m, +5 beim Stadthof). Auf einem Neubau sitzt das Modell damit
    /// genau in der Mitte; auf den Wohnhausplätzen der Karte (8 m tief, Front an der Gasse) steht die Front an der Gasse und der Rest ragt nach hinten.
    /// </summary>
    private static float ModellFront(Reichsgilde.Core.Bauen.Baustein baustein) => (float)(baustein.Groesse?[1] ?? 10) / 2 - Vorplatz;

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
