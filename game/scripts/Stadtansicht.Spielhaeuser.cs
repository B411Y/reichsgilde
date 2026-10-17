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
    /// z-Koordinate der Hausfront). Modelle:
    /// 0 Fachwerkhaus 6 × 6, 1 Bürgerhaus 7 × 7 mit Giebel, 2 Steinhaus 8 × 8 mit drei Geschossen,
    /// 3 Stadtpalais bis 15 × 8 mit Erker und zwei Ecktürmchen, 4 Hof: Haupthaus hinten, zwei Flügel vorn, Mauer mit Tor.
    /// Bei laufendem Ausbau steht ein Gerüst davor, nach einem Brand ist das Dach rußig, im Leerstand sind Tür und Fenster vernagelt.
    /// </summary>
    private Node3D SpielhausModell(Vector3 mitte, float drehungY, float platzBreite, float platzTiefe, float vorne,
                                   int stufe, bool imBau, bool brand, bool leer, Color? farbe, string? kennung, string? name, string? zusatz = null)
    {
        var g = new Node3D { Name = "Spielhaus", Position = mitte, Rotation = new Vector3(0, drehungY, 0) };
        AddChild(g);
        var russ = new Color(0.16f, 0.14f, 0.13f);
        Color Dachfarbe(Color c) => brand ? russ : c;
        var dunkel = Stoff(new Color(0.18f, 0.13f, 0.10f));
        var glas = Stoff(new Color(0.20f, 0.22f, 0.26f));
        var bretter = Stoff(new Color(0.30f, 0.22f, 0.14f));

        // Ein Hausteil: Wand, Dach, Tür und Fensterreihen auf der Vorderseite.
        void Bau(Vector3 mitte, Vector3 masse, Color wand, Color dach, bool giebel, int geschosse, bool tuer)
        {
            Quader(mitte + new Vector3(0, masse.Y / 2, 0), masse, Stoff(wand), g);
            Dach(mitte + new Vector3(0, masse.Y, 0), new Vector3(masse.X + 0.6f, Mathf.Min(masse.X, masse.Z) * 0.55f, masse.Z + 0.6f), Stoff(Dachfarbe(dach)), quer: !giebel, eltern: g);
            var front = mitte.Z + masse.Z / 2 + 0.03f;
            if (tuer) Quader(new Vector3(mitte.X, 1.15f, front), new Vector3(1.3f, 2.3f, 0.1f), leer ? bretter : dunkel, g);
            var spalten = Mathf.Max(2, (int)(masse.X / 2.6f));
            for (var s = 0; s < geschosse; s++)
                for (var i = 0; i < spalten; i++)
                {
                    var x = mitte.X - masse.X / 2 + masse.X * (i + 0.5f) / spalten;
                    if (s == 0 && tuer && Mathf.Abs(x - mitte.X) < 1.2f) continue; // über der Tür kein Fenster im Erdgeschoss
                    Quader(new Vector3(x, 1.7f + s * 2.6f, front), new Vector3(0.9f, 1.2f, 0.1f), leer ? bretter : glas, g);
                }
        }

        switch (stufe)
        {
            case 0: // Fachwerkhaus
                Bau(new Vector3(0, 0, vorne - 3.2f), new Vector3(6, 5, 6), Fachwerk, Ziegel, false, 1, true);
                break;
            case 1: // Bürgerhaus
                Bau(new Vector3(0, 0, vorne - 3.7f), new Vector3(7, 7, 7), Putz, Ziegel, true, 2, true);
                break;
            case 2: // Steinhaus
                Bau(new Vector3(0, 0, vorne - 4.2f), new Vector3(8, 9, 8), Mauerstein, Schiefer, true, 3, true);
                break;
            case 3: // Stadtpalais (auf einem schmaleren Platz etwas schmaler)
            {
                var b = Mathf.Min(15, platzBreite - 1);
                Bau(new Vector3(0, 0, vorne - 4.2f), new Vector3(b, 10, 8), new Color(0.72f, 0.68f, 0.60f), Schiefer, false, 3, true);
                Quader(new Vector3(0, 6.2f, vorne + 0.5f), new Vector3(3, 2.6f, 1f), Stoff(Putz), g); // Erker
                if (!leer)
                    foreach (var x in new[] { -(b / 2 - 0.5f), b / 2 - 0.5f })
                        Turm(new Vector3(x, 0, vorne - 0.6f), 1.1f, 13, Dachfarbe(Schiefer), eltern: g);
                break;
            }
            default: // Hof
            {
                var b = Mathf.Min(16, platzBreite);
                Bau(new Vector3(0, 0, vorne - 6.2f), new Vector3(b, 10, 4), new Color(0.72f, 0.68f, 0.60f), Schiefer, false, 3, false);
                foreach (var x in new[] { -(b / 2 - 1.5f), b / 2 - 1.5f })
                    Bau(new Vector3(x, 0, vorne - 2.1f), new Vector3(3, 7, 4.2f), Mauerstein, Schiefer, true, 2, false);
                Quader(new Vector3(0, 1.4f, vorne - 0.15f), new Vector3(Mathf.Min(10, b - 3), 2.8f, 0.5f), Stoff(Mauerstein), g);   // Hofmauer
                Quader(new Vector3(0, 1.5f, vorne + 0.12f), new Vector3(2.6f, 3f, 0.1f), leer ? bretter : dunkel, g);  // Tor
                break;
            }
        }

        if (farbe is { } f)
        {
            // Wappenschild über der Tür und eine Fahne am Dach in der Hausfarbe
            var hoehe = stufe switch { 0 => 3.4f, 1 => 3.6f, 2 => 3.8f, 3 => 3.9f, _ => 3.6f };
            Quader(new Vector3(0, hoehe, vorne + 0.12f), new Vector3(0.9f, 1.0f, 0.1f), Stoff(f), g);
            var dachHoehe = stufe switch { 0 => 7.5f, 1 => 11, 2 => 13.5f, 3 => 14, _ => 14 };
            Quader(new Vector3(2.5f, dachHoehe + 1.2f, vorne - 2), new Vector3(0.1f, 2.4f, 0.1f), Stoff(Holz), g);
            Quader(new Vector3(3.15f, dachHoehe + 2, vorne - 2), new Vector3(1.2f, 0.75f, 0.05f), Stoff(f), g);
        }

        if (imBau || brand)
        {
            // Gerüst vor der Gassenseite: Stangen und zwei Laufbretter
            var holz = Stoff(Holz);
            var gb = Mathf.Min(12f, platzBreite - 2);
            foreach (var x in new[] { -gb / 2, -gb / 6, gb / 6, gb / 2 })
                Quader(new Vector3(x, 3.6f, vorne + 0.7f), new Vector3(0.15f, 7.2f, 0.15f), holz, g);
            foreach (var y in new[] { 2.6f, 5.2f })
                Quader(new Vector3(0, y, vorne + 0.7f), new Vector3(gb + 0.5f, 0.12f, 0.9f), holz, g);
        }

        if (kennung is not null)
        {
            Klickflaeche(kennung, new Vector3(0, 5, 0), new Vector3(Mathf.Min(16, platzBreite), 10, platzTiefe), g);
            var text = name + (zusatz is not null ? $" ({zusatz})" : imBau ? " (Ausbau)" : brand ? " (nach Brand)" : leer ? " (Leerstand)" : "");
            Beschriftung(text, new Vector3(0, 16, 0), 44, g, kennung);
        }
        return g;
    }

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
