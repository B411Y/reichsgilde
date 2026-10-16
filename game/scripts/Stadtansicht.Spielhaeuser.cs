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
    private sealed class Spielhaus
    {
        public required Konto Konto { get; init; }
        public required Hausplatz Platz { get; init; }
        public Node3D? Modell { get; set; }
        /// <summary>Was zuletzt gebaut wurde (Stufe, Ausbau, Brand, verlassen) – nur bei Änderung neu bauen.</summary>
        public (int Stufe, bool Bau, bool Brand, bool Leer)? Stand { get; set; }
    }

    private readonly List<Spielhaus> _spielhaeuser = new();
    private List<Hausplatz>? _spielhausPlaetze;

    /// <summary>
    /// Die Doppelplätze der Spielhäuser: je zwei benachbarte Ringplätze derselben Seite, die nächsten am Markt zuerst.
    /// Gibt die Mitte (zwischen den beiden Plätzen) zurück; die beiden Einzelplätze stehen in <see cref="BelegteRingplaetze"/>.
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

    /// <summary>Jedes Bild: Wohnhäuser der Spielhäuser anlegen (einmal) und bei Änderungen neu bauen.</summary>
    private void SpielhaeuserAktualisieren()
    {
        if (_simulation?.Wirtschaft is not { } w || !w.Wohnen.IstAktiv) return;
        if (_spielhaeuser.Count == 0)
        {
            var plaetze = SpielhausPlaetze(w.Konten.Count);
            for (var i = 0; i < w.Konten.Count && i < plaetze.Count; i++)
            {
                var platz = plaetze[i];
                _spielhaeuser.Add(new Spielhaus { Konto = w.Konten[i], Platz = platz });
                var tuer = platz.Mitte + platz.Vorne * (PlatzTiefe / 2);
                var knoten = _netz.Anschliessen(tuer);
                _netz.Weg(_netz.Knoten[knoten], tuer);
            }
        }
        var minute = _simulation.Zeit.VergangeneMinuten;
        foreach (var h in _spielhaeuser)
        {
            var k = h.Konto;
            var stand = (k.WohnhausStufe, k.WohnhausFertigAb is { } f && f > minute, k.WohnhausInstandBis is { } b && b > minute, !k.IstAktiv);
            if (h.Stand == stand) continue;
            h.Stand = stand;
            if (h.Modell is not null) KnotenEntfernen(h.Modell);
            h.Modell = SpielhausModell(h.Platz, stand.WohnhausStufe, stand.Item2, stand.Item3, stand.Item4,
                                       stand.Item4 ? (Color?)null : Besitzerfarbe(k), WohnhausKennung(k), k.Inhaber);
        }
    }

    /// <summary>Im Hauptmenü: vier Spielhäuser in verschiedenen Stufen, damit man sieht, was es gibt.</summary>
    private void SpielhaeuserMuster()
    {
        var plaetze = SpielhausPlaetze(4);
        var farben = new[] { Spieler, Rivale, ZweiterRivale, DritterRivale };
        for (var i = 0; i < plaetze.Count; i++)
            SpielhausModell(plaetze[i], 4 - i, false, false, false, farben[i], null, null);
    }

    public static string WohnhausKennung(Konto k) => "wohnhaus:" + k.Inhaber;

    // ═════════════════════════════════════════════════════════════════════════
    //  Modelle der Stufen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut das Wohnhaus einer Stufe auf einem Doppelplatz (16,5 × 8,4 m; Vorderseite lokal +Z, zur Gasse):
    /// 0 Fachwerkhaus 6 × 6, 1 Bürgerhaus 7 × 7 mit Giebel, 2 Steinhaus 8 × 8 mit drei Geschossen,
    /// 3 Stadtpalais 15 × 8 mit Erker und zwei Ecktürmchen, 4 Hof: Haupthaus hinten, zwei Flügel vorn, Mauer mit Tor.
    /// Bei laufendem Ausbau steht ein Gerüst davor, nach einem Brand ist das Dach rußig, im Leerstand sind Tür und Fenster vernagelt.
    /// </summary>
    private Node3D SpielhausModell(Hausplatz platz, int stufe, bool imBau, bool brand, bool leer, Color? farbe, string? kennung, string? name)
    {
        var g = new Node3D { Name = "Spielhaus", Position = platz.Mitte, Rotation = new Vector3(0, Mathf.Atan2(platz.Vorne.X, platz.Vorne.Z), 0) };
        AddChild(g);
        var russ = new Color(0.16f, 0.14f, 0.13f);
        Color Dachfarbe(Color c) => brand ? russ : c;
        var dunkel = Stoff(new Color(0.18f, 0.13f, 0.10f));
        var glas = Stoff(new Color(0.20f, 0.22f, 0.26f));
        var bretter = Stoff(new Color(0.30f, 0.22f, 0.14f));
        const float vorne = PlatzTiefe / 2; // lokale Z-Koordinate der Gassenseite

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
            case 3: // Stadtpalais
                Bau(new Vector3(0, 0, vorne - 4.2f), new Vector3(15, 10, 8), new Color(0.72f, 0.68f, 0.60f), Schiefer, false, 3, true);
                Quader(new Vector3(0, 6.2f, vorne + 0.5f), new Vector3(3, 2.6f, 1f), Stoff(Putz), g); // Erker
                if (!leer)
                    foreach (var x in new[] { -7f, 7f })
                        Turm(new Vector3(x, 0, vorne - 0.6f), 1.1f, 13, Dachfarbe(Schiefer), eltern: g);
                break;
            default: // Hof
                Bau(new Vector3(0, 0, vorne - 6.2f), new Vector3(16, 10, 4), new Color(0.72f, 0.68f, 0.60f), Schiefer, false, 3, false);
                foreach (var x in new[] { -6.5f, 6.5f })
                    Bau(new Vector3(x, 0, vorne - 2.1f), new Vector3(3, 7, 4.2f), Mauerstein, Schiefer, true, 2, false);
                Quader(new Vector3(0, 1.4f, vorne - 0.15f), new Vector3(10, 2.8f, 0.5f), Stoff(Mauerstein), g);            // Hofmauer
                Quader(new Vector3(0, 1.5f, vorne + 0.12f), new Vector3(2.6f, 3f, 0.1f), leer ? bretter : dunkel, g);  // Tor
                break;
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
            foreach (var x in new[] { -6f, -2f, 2f, 6f })
                Quader(new Vector3(x, 3.6f, vorne + 0.7f), new Vector3(0.15f, 7.2f, 0.15f), holz, g);
            foreach (var y in new[] { 2.6f, 5.2f })
                Quader(new Vector3(0, y, vorne + 0.7f), new Vector3(12.5f, 0.12f, 0.9f), holz, g);
        }

        if (kennung is not null)
        {
            Klickflaeche(kennung, new Vector3(0, 5, 0), new Vector3(16, 10, PlatzTiefe), g);
            var text = name + (imBau ? " (Ausbau)" : brand ? " (nach Brand)" : leer ? " (Leerstand)" : "");
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
        var h = _spielhaeuser.FirstOrDefault(x => WohnhausKennung(x.Konto) == kennung);
        if (h is null) return null;
        var k = h.Konto;
        var minute = _simulation.Zeit.VergangeneMinuten;
        var zeilen = new List<string> { $"Wohnhaus – {k.Inhaber}", "" };
        if (!k.IstAktiv)
        {
            zeilen.Add($"Leerstand: Das Haus {k.Inhaber} ist erloschen. Das {w.Wohnen.Stufe(k)?.Name} steht leer.");
            return string.Join("\n", zeilen);
        }
        zeilen.Add($"{w.Wohnen.Stufe(k)?.Name}" + (w.Wohnen.Stufe(k) is { Ansehen: > 0 } s ? $" · +{s.Ansehen:0} Ansehen" : ""));
        if (k.WohnhausFertigAb is { } fertig && fertig > minute)
        {
            var zeit = new Reichsgilde.Core.Zeit.Spielzeit(fertig);
            zeilen.Add($"Ausbau zum {w.Wohnen.NaechsteStufe(k)?.Name} – fertig {(zeit.Jahr == _simulation.Zeit.Jahr ? "gegen " + zeit.UhrzeitText + " Uhr" : zeit.DatumText)}");
        }
        if (k.WohnhausInstandBis is { } instand && instand > minute)
            zeilen.Add($"Nach einem Brand wird es instand gesetzt (bis {new Reichsgilde.Core.Zeit.Spielzeit(instand).UhrzeitText} Uhr) – bis dahin fehlt sein Ansehen.");
        if (k.Hauswaechter) zeilen.Add("Ein Hauswächter bewacht es.");
        return string.Join("\n", zeilen);
    }
}
