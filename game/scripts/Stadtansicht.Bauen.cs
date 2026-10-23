using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Bauwesen in der Stadt – neunter Teil der <c>partial class Stadtansicht</c>.
///
/// Der Kern (<c>w.Bauen</c>) kennt Grundstücke: die Wohnhäuser der Häuser, neue Werkstätten und Bauplätze nach einem Abriss.
/// Die Darstellung liest sie jedes Bild und baut je Grundstück ein Modell – aber nur, wenn sich sein Stand ändert:
///   • <b>Wohnhaus</b> (bewohnt oder Leerstand, vernagelt): <see cref="SpielhausModell"/> auf der Fläche des Grundstücks.
///   • <b>Baustelle</b> (Neubau, Sanierung, Abriss): Fundament, wachsende Mauern, Gerüst und Holzstapel; vier Baustufen bis „fertig".
///   • <b>Bauplatz</b>: Wiese mit Pflöcken und Schnur.
///   • Eine fertige neue <b>Werkstatt</b> zeichnet <see cref="BetriebHinzufuegen"/> wie jede andere (das Modell nach Beruf).
/// Die Ereignisse des Kerns (BauBegonnen, BauFertig, Abgerissen, BauplatzBebaut) müssen dafür nicht einzeln abonniert werden:
/// Der Stand der Grundstücke ist die Quelle – das gilt auch nach dem Laden eines Spielstands.
///
/// Dazu der <b>Baumodus</b>: Die Bauzonen der Karte erscheinen als farbige Flächen mit 2-m-Raster (Farbe je Lage), und ein
/// Umriss folgt der Maus, grün oder rot nach <c>PlatzVerboten</c>, mit dem Grund als Text. R dreht, Klick setzt.
/// Die Oberfläche (<c>BauAnzeige</c>) wählt das Gebäude und fragt vor dem Bauen nach; hier wird nur gezeigt und die Eingabe gelesen.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Der Vorplatz vor jedem neuen Gebäude (Meter) – die Hausfront liegt so weit hinter der Vorderkante der Fläche.</summary>
    private const float Vorplatz = 2f;

    private sealed class Grundstuecksbild
    {
        public Node3D? Modell { get; set; }
        /// <summary>Was zuletzt gebaut wurde – nur bei Änderung neu bauen.</summary>
        public string Stand { get; set; } = "";
    }

    private readonly Dictionary<int, Grundstuecksbild> _grundstuecksbilder = new();

    private static Vector3 Mitte3(Grundriss g) => new((float)g.X, 0, (float)g.Z);
    private static float DrehungY(Grundriss g) => g.Drehung * Mathf.Pi / 2;

    // ═════════════════════════════════════════════════════════════════════════
    //  Grundstücke
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild: Modelle der Grundstücke anlegen, bei Änderungen neu bauen, verschwundene entfernen.</summary>
    private void GrundstueckeAktualisieren()
    {
        if (_simulation?.Wirtschaft is not { } w || !w.Bauen.IstAktiv) return;
        var minute = _simulation.Zeit.VergangeneMinuten;
        var vorhanden = new HashSet<int>();
        foreach (var g in w.Bauen.Grundstuecke)
        {
            // Eine fertige neue Werkstatt zeichnet der Betrieb selbst (BetriebHinzufuegen); steht sie leer, das leere Haus.
            if (g.Art == Grundstuecksart.Werkstatt && g.Arbeit is null) continue;
            vorhanden.Add(g.Nummer);
            var stand = StandVon(g, w, minute);
            if (!_grundstuecksbilder.TryGetValue(g.Nummer, out var bild))
            {
                _grundstuecksbilder[g.Nummer] = bild = new Grundstuecksbild();
                // Die Plätze der Karte hängen die Haustür ans Gassennetz (wie die alten Spielhäuser); Neubauten bleiben außen vor.
                if (g.Kennung is not null && g.Art == Grundstuecksart.Wohnhaus)
                {
                    AnWegAnschliessen(new Vector3((float)g.Grundriss.Eingang.X, 0, (float)g.Grundriss.Eingang.Z));
                }
            }
            if (bild.Stand == stand) continue;
            bild.Stand = stand;
            if (bild.Modell is not null) KnotenEntfernen(bild.Modell);
            bild.Modell = GrundstueckModell(g, w, minute);
        }
        foreach (var nummer in _grundstuecksbilder.Keys.Where(n => !vorhanden.Contains(n)).ToList())
        {
            if (_grundstuecksbilder[nummer].Modell is { } modell) KnotenEntfernen(modell);
            _grundstuecksbilder.Remove(nummer);
        }
        // Zieht der Spieler um, steht seine Wohnstube im neuen Haus: Die Kamera fährt dorthin, wenn er es betritt.
        if (_wohnhausAnsicht is not null && _spielerKonto is { } spieler && w.Bauen.Wohnhaus(spieler) is { } haus)
            _wohnhausAnsicht.Mitte = Mitte3(haus.Grundriss);
    }

    /// <summary>Alles, woran sich das Bild eines Grundstücks ändert, als Text – gleicher Text, gleiches Bild.</summary>
    private static string StandVon(Grundstueck g, Stadtwirtschaft w, long minute)
    {
        var k = g.Besitzer;
        var bau = g.Arbeit is null && k is { } b && b.WohnhausFertigAb is { } f && f > minute;
        var brand = g.Arbeit is null && k is { } b2 && b2.WohnhausInstandBis is { } i && i > minute;
        var stufe = g.Arbeit is not null ? BaustufeVon(g, w, minute) : -1;
        return $"{g.Art}|{g.Zustand}|{g.Arbeit}|{g.Stufe}|{k?.Inhaber}|{k?.IstBankrott}|{k?.IstErloschen}|{bau}|{brand}|{stufe}|{g.Betriebsart}";
    }

    private Node3D? GrundstueckModell(Grundstueck g, Stadtwirtschaft w, long minute)
    {
        var gr = g.Grundriss;
        switch (g.Zustand)
        {
            case Bauzustand.Bewohnt or Bauzustand.Leerstand when g.Art == Grundstuecksart.Wohnhaus && g.Besitzer is { } k:
            {
                var leer = !k.IstAktiv;
                var bau = k.WohnhausFertigAb is { } f && f > minute;
                var brand = k.WohnhausInstandBis is { } i && i > minute;
                return SpielhausModell(Mitte3(gr), DrehungY(gr), (float)gr.Breite, (float)gr.Tiefe, HausFront(g), g.Stufe, bau, brand, leer,
                    leer ? null : Besitzerfarbe(k), WohnhausKennung(k), k.Inhaber, leer ? $"Leerstand, {(k.IstBankrott ? "bankrott" : "erloschen")}" : null);
            }
            case Bauzustand.Baustelle:
                return BaustellenModell(g, w, minute);
            case Bauzustand.Bauplatz:
                return BauplatzModell(g);
            default:
                return null;
        }
    }

    /// <summary>
    /// Wie viel Vorplatz die Fläche vor der Hausfront enthält: Neubauten nach den Normmaßen (10 oder 14 m tief) haben 2 m davor;
    /// die Wohnhausplätze der Karte (8 m tief, auch als Bauplatz und beim Neubau „eingepasst“) reichen mit der Hausfront bis an die Gasse.
    /// </summary>
    private static float VorplatzVon(Grundriss g) => g.Tiefe > 9 ? Vorplatz : 0;

    /// <summary>Lokale z-Koordinate der Hausfront (die Mitte der Fläche ist 0, vorne ist +z).</summary>
    private static float HausFront(Grundstueck g) => (float)g.Grundriss.Tiefe / 2 - VorplatzVon(g.Grundriss);

    public static string BaustellenKennung(Grundstueck g) => "baustelle:" + g.Nummer;
    public static string BauplatzKennung(Grundstueck g) => "bauplatz:" + g.Nummer;

    private MeshInstance3D Platte(Node3D eltern, Vector3 mitte, Vector2 groesse, Color farbe)
    {
        var platte = new MeshInstance3D { Mesh = new PlaneMesh { Size = groesse }, MaterialOverride = Stoff(farbe), Position = mitte };
        eltern.AddChild(platte);
        return platte;
    }

    // ── Bauplatz ────────────────────────────────────────────────────────────

    /// <summary>Ein freier Bauplatz: helle Wiese, vier Pflöcke mit roten Spitzen und eine Schnur ringsum.</summary>
    private Node3D BauplatzModell(Grundstueck g)
    {
        var gr = g.Grundriss;
        var (breite, tiefe) = ((float)gr.Breite, (float)gr.Tiefe);
        var wurzel = new Node3D { Name = $"Bauplatz_{g.Nummer}", Position = AufFundament(Mitte3(gr), DrehungY(gr), new Vector2(breite, tiefe)), Rotation = new Vector3(0, DrehungY(gr), 0) };
        AddChild(wurzel);
        GruppenSockel(wurzel, DrehungY(gr), new Vector2(breite, tiefe));
        Platte(wurzel, new Vector3(0, 0.03f, 0), new Vector2(breite - 0.3f, tiefe - 0.3f), new Color(0.56f, 0.68f, 0.32f));
        Platte(wurzel, new Vector3(-breite * 0.15f, 0.04f, tiefe * 0.1f), new Vector2(breite * 0.3f, tiefe * 0.25f), new Color(0.42f, 0.34f, 0.22f)); // aufgewühlte Erde
        var holz = Stoff(Holz);
        var rot = Stoff(new Color(0.78f, 0.18f, 0.14f));
        var schnur = Stoff(new Color(0.88f, 0.84f, 0.70f));
        var (hx, hz) = (breite / 2 - 0.3f, tiefe / 2 - 0.3f);
        foreach (var (x, z) in new[] { (-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz) })
        {
            Quader(new Vector3(x, 0.7f, z), new Vector3(0.2f, 1.4f, 0.2f), holz, wurzel);
            Quader(new Vector3(x, 1.5f, z), new Vector3(0.24f, 0.26f, 0.24f), rot, wurzel);
        }
        Quader(new Vector3(0, 1.1f, -hz), new Vector3(2 * hx, 0.07f, 0.07f), schnur, wurzel);
        Quader(new Vector3(0, 1.1f, hz), new Vector3(2 * hx, 0.07f, 0.07f), schnur, wurzel);
        Quader(new Vector3(-hx, 1.1f, 0), new Vector3(0.07f, 0.07f, 2 * hz), schnur, wurzel);
        Quader(new Vector3(hx, 1.1f, 0), new Vector3(0.07f, 0.07f, 2 * hz), schnur, wurzel);
        var kennung = BauplatzKennung(g);
        Klickflaeche(kennung, new Vector3(0, 1, 0), new Vector3(breite, 2, tiefe), wurzel);
        Beschriftung("Bauplatz", new Vector3(0, 4, 0), 40, wurzel, kennung);
        return wurzel;
    }

    // ── Baustelle ───────────────────────────────────────────────────────────

    /// <summary>Die Bauzeit der Baustelle in Spielminuten.</summary>
    private static double BauDauer(Grundstueck g, Stadtwirtschaft w) => g.Arbeit switch
    {
        Bauarbeit.Sanierung => w.Wohnen.Daten?.BauzeitMinuten ?? 240,
        Bauarbeit.Abriss => w.Bauen.Daten?.Leerstand.AbrissMinuten ?? 240,
        _ when g.Art == Grundstuecksart.Werkstatt => (w.Bauen.Daten?.Werkstatt.BauzeitJahre ?? 1) * Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag,
        _ => (w.Bauen.Daten?.Wohnhaus is { } l && g.Stufe < l.Count ? l[g.Stufe].BauzeitJahre : 1) * Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag,
    };

    /// <summary>Wie weit die Baustelle ist, in vier Stufen (0 bis 3) – die Mauern wachsen mit.</summary>
    private static int BaustufeVon(Grundstueck g, Stadtwirtschaft w, long minute)
    {
        if (g.FertigAb is not { } fertig) return 0;
        var dauer = Math.Max(1, BauDauer(g, w));
        var anteil = 1 - (fertig - minute) / dauer;
        return Math.Clamp((int)(anteil * 4), 0, 3);
    }

    /// <summary>
    /// Die Baustelle eines Neubaus, einer Sanierung oder eines Abrisses. Neubau: Fundament, Mauern, die mit der Bauzeit hochwachsen,
    /// zuletzt der Dachstuhl, dazu Gerüst und Holzstapel. Sanierung: das Haus selbst mit Gerüst. Abriss: niedrige Mauerreste, Schutt, Gerüst.
    /// </summary>
    private Node3D BaustellenModell(Grundstueck g, Stadtwirtschaft w, long minute)
    {
        var gr = g.Grundriss;
        var (breite, tiefe) = ((float)gr.Breite, (float)gr.Tiefe);
        var front = HausFront(g);
        var stufe = BaustufeVon(g, w, minute);
        var kennung = BaustellenKennung(g);
        var besitzer = g.Besitzer?.Inhaber ?? "";
        var titel = g.Arbeit switch { Bauarbeit.Sanierung => "Sanierung", Bauarbeit.Abriss => "Abriss", _ => "Neubau" };

        Node3D wurzel;
        if (g.Arbeit == Bauarbeit.Sanierung)
        {
            // Das Haus steht schon: neu verputzt und mit Gerüst
            wurzel = SpielhausModell(Mitte3(gr), DrehungY(gr), breite, tiefe, front, g.Stufe, true, false, false, null, kennung, besitzer, titel,
                ankerKennung: g.Besitzer is { } bk ? WohnhausKennung(bk) : null); // dieselben Anker wie das bewohnte Haus
            Holzstapel(wurzel, new Vector3(-(Mathf.Min(breite, 10) / 2 - 1.4f), 0, front + 1.0f));
            return wurzel;
        }

        wurzel = new Node3D { Name = $"Baustelle_{g.Nummer}", Position = AufFundament(Mitte3(gr), DrehungY(gr), new Vector2(breite, tiefe)), Rotation = new Vector3(0, DrehungY(gr), 0) };
        AddChild(wurzel);
        GruppenSockel(wurzel, DrehungY(gr), new Vector2(breite, tiefe));
        // Maße des Gebäudes, das hier entsteht (bzw. des Rests, der abgerissen wird)
        var (hb, ht, hh, wand) = g.Art == Grundstuecksart.Werkstatt ? (10f, 8f, 6f, Fachwerk) : g.Stufe switch
        {
            0 => (6f, 6f, 5f, Fachwerk),
            1 => (7f, 7f, 7f, Putz),
            2 => (8f, 8f, 9f, Mauerstein),
            3 => (Mathf.Min(15, breite - 1), 8f, 10f, new Color(0.72f, 0.68f, 0.60f)),
            _ => (Mathf.Min(16, breite), 8.4f, 10f, new Color(0.72f, 0.68f, 0.60f)),
        };
        hb = Mathf.Min(hb, breite - 0.4f);
        ht = Mathf.Min(ht, tiefe - VorplatzVon(gr) - 0.2f);
        var mitteZ = front - ht / 2;
        var abriss = g.Arbeit == Bauarbeit.Abriss;
        var anteil = abriss ? 0.75f - 0.2f * stufe : 0.2f + 0.25f * stufe; // Abriss: Mauern sinken; Neubau: sie wachsen
        var hoehe = Mathf.Max(0.5f, hh * anteil);
        var dicke = 0.5f;

        Platte(wurzel, new Vector3(0, 0.03f, 0), new Vector2(breite - 0.3f, tiefe - 0.3f), new Color(0.44f, 0.38f, 0.28f)); // Baugrund
        Quader(new Vector3(0, 0.15f, mitteZ), new Vector3(hb + 0.4f, 0.3f, ht + 0.4f), Stoff(Bruchstein), wurzel);          // Fundament
        var mauer = Stoff(wand);
        // vier Mauern; beim Abriss sind sie unterschiedlich weit abgetragen
        var faktoren = abriss ? new[] { 1f, 0.7f, 0.5f, 0.85f } : new[] { 1f, 1f, 1f, 1f };
        Quader(new Vector3(0, hoehe * faktoren[0] / 2, mitteZ - ht / 2 + dicke / 2), new Vector3(hb, hoehe * faktoren[0], dicke), mauer, wurzel);
        Quader(new Vector3(0, hoehe * faktoren[1] / 2, mitteZ + ht / 2 - dicke / 2), new Vector3(hb, hoehe * faktoren[1], dicke), mauer, wurzel);
        Quader(new Vector3(-hb / 2 + dicke / 2, hoehe * faktoren[2] / 2, mitteZ), new Vector3(dicke, hoehe * faktoren[2], ht - 2 * dicke), mauer, wurzel);
        Quader(new Vector3(hb / 2 - dicke / 2, hoehe * faktoren[3] / 2, mitteZ), new Vector3(dicke, hoehe * faktoren[3], ht - 2 * dicke), mauer, wurzel);
        if (!abriss && stufe >= 3) // zuletzt der Dachstuhl: Sparren aus rohem Holz
            Dach(new Vector3(0, hoehe, mitteZ), new Vector3(hb + 0.4f, Mathf.Min(hb, ht) * 0.5f, ht + 0.4f), Stoff(new Color(0.62f, 0.48f, 0.30f)), quer: true, eltern: wurzel);
        if (abriss)
            Schutt(wurzel, new Vector3(0, 0, mitteZ), hb, ht, stufe);

        // Gerüst vor der Front
        var holz = Stoff(Holz);
        var gh = hoehe + 1.6f;
        foreach (var x in new[] { -hb / 2, -hb / 6, hb / 6, hb / 2 })
            Quader(new Vector3(x, gh / 2, front + 0.6f), new Vector3(0.14f, gh, 0.14f), holz, wurzel);
        for (var y = 1.4f; y < gh; y += 1.5f)
            Quader(new Vector3(0, y, front + 0.6f), new Vector3(hb + 0.4f, 0.1f, 0.8f), holz, wurzel);
        Holzstapel(wurzel, new Vector3(hb / 2 - 1.6f, 0, front + 1.3f));
        Quader(new Vector3(-hb / 2 + 0.9f, 0.4f, front + 1.2f), new Vector3(1.4f, 0.8f, 1.0f), Stoff(Bruchstein), wurzel); // Steinhaufen

        Klickflaeche(kennung, new Vector3(0, 3, 0), new Vector3(breite, 6, tiefe), wurzel);
        Beschriftung($"{besitzer} ({titel})", new Vector3(0, 11, 0), 40, wurzel, kennung);
        return wurzel;
    }

    /// <summary>Ein Stapel Bauholz: drei Lagen Stämme.</summary>
    private void Holzstapel(Node3D eltern, Vector3 fuss)
    {
        var holz = Stoff(Holz);
        for (var lage = 0; lage < 3; lage++)
            Quader(fuss + new Vector3(0, 0.25f + lage * 0.45f, 0), new Vector3(2.2f - lage * 0.3f, 0.4f, 1.0f), holz, eltern);
    }

    /// <summary>Schutt: ein paar Haufen Steine und Balken, mit dem Fortschritt des Abrisses mehr.</summary>
    private void Schutt(Node3D eltern, Vector3 mitte, float breite, float tiefe, int stufe)
    {
        var stein = Stoff(Bruchstein);
        var balken = Stoff(Holz);
        var stellen = new (float X, float Z, float B, float H)[]
        {
            (-0.25f, -0.2f, 2.2f, 0.7f), (0.3f, 0.25f, 1.8f, 0.5f), (-0.1f, 0.3f, 1.4f, 0.4f), (0.2f, -0.3f, 1.6f, 0.9f),
        };
        for (var i = 0; i <= Math.Min(stufe + 1, 3); i++)
        {
            var (x, z, b, h) = stellen[i];
            Quader(mitte + new Vector3(x * breite * 0.6f, h / 2 + 0.3f, z * tiefe * 0.6f), new Vector3(b, h, b * 0.8f), stein, eltern);
        }
        Quader(mitte + new Vector3(0.5f, 1.0f, 0), new Vector3(0.2f, 0.2f, 3.0f), balken, eltern).RotationDegrees = new Vector3(0, 20, 12);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahltafel für Baustelle und Bauplatz
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Text für die Auswahltafel einer Baustelle oder eines Bauplatzes. Null, wenn die Kennung keine ist.</summary>
    public string? GrundstueckText(string kennung)
    {
        if (_simulation?.Wirtschaft is not { } w) return null;
        if (!kennung.StartsWith("baustelle:") && !kennung.StartsWith("bauplatz:")) return null;
        if (!int.TryParse(kennung[(kennung.IndexOf(':') + 1)..], out var nummer)) return null;
        var g = w.Bauen.Grundstuecke.FirstOrDefault(x => x.Nummer == nummer);
        if (g is null) return null;
        var gr = g.Grundriss;
        var flaeche = $"{Reichsgilde.Core.Zahlenformat.Ganz(gr.Breite)} × {Reichsgilde.Core.Zahlenformat.Ganz(gr.Tiefe)} m";

        if (g.Arbeit is not { } arbeit)
            return g.Zustand != Bauzustand.Bauplatz ? null
                : Bauwesen.IstWohnhausplatz(g)
                    ? string.Join("\n", "Bauplatz (Wohnhausplatz)", "", $"Freie Fläche, {flaeche}.",
                        "Hier darf jeder ein Wohnhaus jeder Stufe bauen – es passt sich der Fläche an. Eine Werkstatt darf hier nicht stehen. Im Kontor „Bauen“ wählen und die Maus über den Platz führen.")
                    : string.Join("\n", "Bauplatz", "", $"Freie Fläche, {flaeche} (mit Vorplatz).",
                        "Hier darf jeder bauen, was hineinpasst: Wohnhaus oder Werkstatt – im Kontor „Bauen“ und das Gebäude auf den Platz setzen.");

        var stufenname = w.Wohnen.Daten is { } wd ? wd.Stufen[Math.Clamp(g.Stufe, 0, wd.Stufen.Count - 1)].Name : "Wohnhaus";
        var bauherr = g.Besitzer?.Inhaber ?? "?";
        var fertig = g.FertigAb is { } f ? FertigText(f) : "bald";
        var zeilen = new List<string>();
        switch (arbeit)
        {
            case Bauarbeit.Neubau when g.Art == Grundstuecksart.Werkstatt:
                var art = g.Betriebsart is { } a ? w.Daten.Art(a).Name : "Werkstatt";
                zeilen.Add($"Baustelle – Neubau: {art}");
                zeilen.Add("");
                zeilen.Add($"„{g.Betrieb}“ für {bauherr}, {flaeche}.");
                zeilen.Add($"Fertig {fertig}. Dann öffnet die Werkstatt.");
                break;
            case Bauarbeit.Neubau:
                zeilen.Add($"Baustelle – Neubau: {stufenname}");
                zeilen.Add("");
                zeilen.Add($"Für {bauherr}, {flaeche}.");
                zeilen.Add($"Fertig {fertig}. Bis dahin wohnt {bauherr} im alten Haus; danach wird es abgerissen und zum Bauplatz.");
                break;
            case Bauarbeit.Sanierung:
                zeilen.Add($"Baustelle – Sanierung: {stufenname}");
                zeilen.Add("");
                zeilen.Add($"Das leere Haus wird für {bauherr} hergerichtet.");
                zeilen.Add($"Fertig {fertig}. Dann zieht {bauherr} ein; das alte Haus wird abgerissen und zum Bauplatz.");
                break;
            default:
                zeilen.Add($"Baustelle – Abriss: {stufenname}");
                zeilen.Add("");
                zeilen.Add($"Das leere Haus wird auf Rechnung von {bauherr} abgerissen.");
                zeilen.Add($"Fertig {fertig}. Dann ist hier ein freier Bauplatz.");
                break;
        }
        return string.Join("\n", zeilen);
    }

    /// <summary>
    /// Was ist aus einer ausgewählten Baustelle (oder einem Bauplatz) geworden? Solange sie besteht, die gleiche Kennung; ist der Bau fertig,
    /// die Kennung des neuen Hauses bzw. der neuen Werkstatt (oder des Bauplatzes nach einem Abriss); gibt es das Grundstück nicht mehr: null.
    /// </summary>
    public string? KennungNachBau(string kennung)
    {
        if (_simulation?.Wirtschaft is not { } w || !int.TryParse(kennung[(kennung.IndexOf(':') + 1)..], out var nummer)) return kennung;
        if (w.Bauen.Grundstuecke.FirstOrDefault(x => x.Nummer == nummer) is not { } g) return null;
        return g.Zustand switch
        {
            Bauzustand.Baustelle => BaustellenKennung(g),
            Bauzustand.Bauplatz => BauplatzKennung(g),
            Bauzustand.Werkstatt when g.Betrieb is { } name && w.Betriebe.FirstOrDefault(b => b.Name == name) is { } betrieb => BetriebsKennung(betrieb),
            Bauzustand.Bewohnt or Bauzustand.Leerstand when g.Besitzer is { } k => WohnhausKennung(k),
            _ => null,
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Neue Werkstätten: Platz und Drehung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dreht ein fertig gebautes Gebäude um <paramref name="mitte"/> (die Bausteine hängen mit festen Weltkoordinaten an der Gruppe):
    /// Sie wandern in einen gedrehten Knoten unter der Gruppe. <paramref name="drehung"/> ist der Winkel um die Hochachse im Bogenmaß
    /// (wie <c>Rotation.Y</c>, beliebig). Gibt die Drehung als Basis zurück, damit die Außenrichtungen der Wände folgen können. Die Räume im
    /// Innern (<paramref name="ausser"/>) liegen 200 m unter der Stadt und werden nicht mitgedreht: Die Raumkamera blickt fest nach Norden.
    /// </summary>
    private static Basis UmMitteDrehen(Node3D gruppe, Vector3 mitte, float drehung, Node? ausser = null)
    {
        var basis = new Basis(Vector3.Up, drehung);
        if (drehung == 0) return basis;
        var huelle = new Node3D { Name = "Gedreht" };
        foreach (var kind in gruppe.GetChildren())
        {
            if (kind == ausser) continue;
            gruppe.RemoveChild(kind);
            huelle.AddChild(kind);
        }
        huelle.Transform = new Transform3D(basis, mitte - basis * mitte);
        gruppe.AddChild(huelle);
        return basis;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Baumodus: Bauzonen
    // ═════════════════════════════════════════════════════════════════════════

    private Node3D? _bauzonen;
    private bool _baumodus;

    /// <summary>Farbe einer Bauzone je Lage – auch die Legende der Oberfläche nimmt sie.</summary>
    public static Color LageFarbe(Lage lage) => lage switch
    {
        Lage.Kern => new Color(1.0f, 0.62f, 0.15f),
        Lage.Mauerring => new Color(0.38f, 0.55f, 1.0f),
        Lage.Vorstadt => new Color(0.30f, 0.90f, 0.78f),
        _ => new Color(0.8f, 0.8f, 0.8f),
    };

    public static string LageName(Lage lage) => lage switch
    {
        Lage.Kern => "Kern", Lage.Mauerring => "Mauerring", Lage.Vorstadt => "Vorstadt", _ => "Umland",
    };

    /// <summary>Baumodus: Die Bauzonen der Karte werden als farbige Flächen mit Raster gezeigt.</summary>
    public bool Baumodus
    {
        get => _baumodus;
        set
        {
            _baumodus = value;
            if (value && _bauzonen is null) BauzonenBauen();
            if (_bauzonen is not null) _bauzonen.Visible = value;
            if (!value) BauPlatzieren(null);
        }
    }

    private void BauzonenBauen()
    {
        _bauzonen = new Node3D { Name = "Bauzonen", Visible = false };
        AddChild(_bauzonen);
        if (_karte is not { } karte) return;
        foreach (var zone in karte.Bauzonen) BauzoneZeichnen(zone, (float)karte.Raster, _bauzonen);
    }

    /// <summary>Eine Bauzone als farbige Fläche (Farbe der Lage) mit ihrem Raster und der Beschriftung „Kennung · Lage“ – auch für die Kartenwerkstatt.</summary>
    private void BauzoneZeichnen(Bauzone zone, float raster, Node3D eltern)
    {
        var farbe = LageFarbe(zone.Art);
        var punkte = zone.Flaeche.Select(p => new Vector2((float)p[0], (float)p[1])).ToArray();
        var dreiecke = Geometry2D.TriangulatePolygon(punkte);
        if (dreiecke.Length == 0) return;

        // Fläche
        var flaeche = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        if (BodenBewegt)
        {
            // auf Gelände genau auf den Dreiecken des Bodens (Stadtansicht.Gelaende.cs); die Fläche selbst liegt noch 8 cm höher (Position)
            arrays[(int)Mesh.ArrayType.Vertex] = RasterDreiecke(punkte, ZoneAmHang).ToArray();
        }
        else
        {
            arrays[(int)Mesh.ArrayType.Vertex] = punkte.Select(p => new Vector3(p.X, 0, p.Y)).ToArray();
            arrays[(int)Mesh.ArrayType.Index] = dreiecke;
        }
        flaeche.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        eltern.AddChild(new MeshInstance3D
        {
            Mesh = flaeche,
            Position = new Vector3(0, 0.08f, 0),
            MaterialOverride = Transparent(farbe, 0.34f),
        });

        // Raster: die Kanten aller Rasterfelder, deren Mitte in der Zone liegt (jede Kante nur einmal)
        var (minX, maxX) = (punkte.Min(p => p.X), punkte.Max(p => p.X));
        var (minZ, maxZ) = (punkte.Min(p => p.Y), punkte.Max(p => p.Y));
        var kanten = new HashSet<(int, int, int, int)>();
        var linien = new List<Vector3>();
        void Kante(int ax, int az, int bx, int bz)
        {
            if (!kanten.Add((ax, az, bx, bz))) return;
            linien.Add(new Vector3(ax * raster, BodenHoehe(ax * raster, az * raster), az * raster));
            linien.Add(new Vector3(bx * raster, BodenHoehe(bx * raster, bz * raster), bz * raster));
        }
        for (var x = Mathf.Floor(minX / raster) * raster; x < maxX; x += raster)
            for (var z = Mathf.Floor(minZ / raster) * raster; z < maxZ; z += raster)
            {
                if (!zone.Enthaelt(x + raster / 2, z + raster / 2)) continue;
                var (ix, iz) = ((int)Mathf.Round(x / raster), (int)Mathf.Round(z / raster));
                Kante(ix, iz, ix + 1, iz);
                Kante(ix, iz + 1, ix + 1, iz + 1);
                Kante(ix, iz, ix, iz + 1);
                Kante(ix + 1, iz, ix + 1, iz + 1);
            }
        if (linien.Count > 0)
        {
            var gitter = new ArrayMesh();
            var la = new Godot.Collections.Array();
            la.Resize((int)Mesh.ArrayType.Max);
            la[(int)Mesh.ArrayType.Vertex] = linien.ToArray();
            gitter.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, la);
            eltern.AddChild(new MeshInstance3D
            {
                Mesh = gitter,
                Position = new Vector3(0, 0.1f, 0),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(farbe.Lightened(0.35f), 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            });
        }

        // Beschriftung in der Mitte der Zone
        var mitte = new Vector3((minX + maxX) / 2, BodenHoehe((minX + maxX) / 2, (minZ + maxZ) / 2), (minZ + maxZ) / 2);
        var label = Beschriftung($"{zone.Kennung} · {LageName(zone.Art)}", mitte + new Vector3(0, 5, 0), 36, eltern);
        label.Modulate = farbe.Lightened(0.5f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Baumodus: Umriss an der Maus
    // ═════════════════════════════════════════════════════════════════════════

    private Func<double, double, int, BauBefehl>? _bauErzeuger;
    private Node3D? _bauUmriss;
    private MeshInstance3D? _bauFlaeche, _bauVorplatz, _bauKoerper, _bauPfeil;
    private StandardMaterial3D? _bauFarbe, _bauVorplatzFarbe, _bauKoerperFarbe, _bauPfeilFarbe;
    private Label3D? _bauText;
    private Vector2? _bauOrt;
    private float _rechtsGezogen;

    /// <summary>Der Auftrag, der gerade platziert wird – mit eingerasteter Position (null: kein Umriss).</summary>
    public BauBefehl? BauAuftrag { get; private set; }

    /// <summary>Warum der Umriss rot ist (null: grün, hier kann gebaut werden). Nur gültig mit <see cref="BauAuftrag"/>.</summary>
    public string? BauGrund { get; private set; }

    /// <summary>Die Drehung des Umrisses in Vierteln (R dreht).</summary>
    public int BauDrehung { get; set; }

    /// <summary>Ist der Umriss festgelegt (Rückfrage offen)? Dann folgt er der Maus nicht mehr.</summary>
    public bool BauFest { get; set; }

    /// <summary>Wird gerade platziert?</summary>
    public bool BauPlatziertGerade => _bauErzeuger is not null;

    /// <summary>Der Spieler hat auf eine erlaubte (grüne) Stelle geklickt. Der Auftrag ist schon eingerastet.</summary>
    public event Action<BauBefehl>? BauPlatzGewaehlt;

    /// <summary>Rechtsklick: Der Spieler will das Platzieren abbrechen (Esc meldet die Hauptszene selbst).</summary>
    public event Action? BauAbgebrochen;

    /// <summary>
    /// Beginnt das Platzieren: Der Umriss des Gebäudes folgt der Maus. <paramref name="erzeuger"/> macht aus Mitte und Drehung den
    /// Auftrag; null beendet das Platzieren.
    /// </summary>
    public void BauPlatzieren(Func<double, double, int, BauBefehl>? erzeuger, int drehung = 0)
    {
        _bauErzeuger = erzeuger;
        BauDrehung = drehung;
        BauFest = false;
        BauAuftrag = null;
        BauGrund = null;
        if (_bauUmriss is not null) _bauUmriss.Visible = false;
    }

    /// <summary>Nur für den Klicktest (--bauort): Der Umriss sitzt an dieser Weltstelle, bis die Maus bewegt wird.</summary>
    public void BauOrtSetzen(double x, double z) => _bauOrt = new Vector2((float)x, (float)z);

    /// <summary>Der Punkt auf dem Boden unter einer Bildschirmstelle (x, z) – auf hügeligem Boden dort, wo der Strahl das Gelände trifft (Stadtansicht.Gelaende.cs).</summary>
    private Vector2? BodenpunktBei(Vector2 bildschirm) => BodenTrefferBei(bildschirm) is { } p ? new Vector2(p.X, p.Z) : null;

    /// <summary>Lesen der Eingabe beim Platzieren: R dreht, Klick setzt, Rechtsklick (ohne Ziehen) bricht ab. Gibt true zurück, wenn verbraucht.</summary>
    private bool BauEingabe(InputEvent eingabe)
    {
        if (_bauErzeuger is null) return false;
        switch (eingabe)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R } when !BauFest:
                BauDrehung = (BauDrehung + 1) % 4;
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when !BauFest:
                if (BauAuftrag is { } befehl && BauGrund is null) BauPlatzGewaehlt?.Invoke(befehl);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rechts:
                if (rechts.Pressed) _rechtsGezogen = 0;
                else if (_rechtsGezogen < 6 && !BauFest) BauAbgebrochen?.Invoke();
                return false; // das Ziehen der Kamera läuft weiter
            case InputEventMouseMotion bewegung:
                _bauOrt = null;
                if (Input.IsMouseButtonPressed(MouseButton.Right)) _rechtsGezogen += bewegung.Relative.Length();
                return false;
        }
        return false;
    }

    /// <summary>Jedes Bild: Der Umriss folgt der Maus, rastet ein und wird grün oder rot.</summary>
    private void BauUmrissAktualisieren()
    {
        if (_bauErzeuger is null || _simulation?.Wirtschaft is not { } w || !w.Bauen.IstAktiv) return;
        if (!BauFest)
        {
            var ort = _bauOrt;
            if (ort is null && GetViewport().GuiGetHoveredControl() is null)
                ort = BodenpunktBei(GetViewport().GetMousePosition());
            if (ort is null)
            {
                if (_bauUmriss is not null) _bauUmriss.Visible = false;
                BauAuftrag = null;
                BauGrund = null;
                return;
            }
            var befehl = w.Bauen.Einrasten(_bauErzeuger(ort.Value.X, ort.Value.Y, BauDrehung));
            BauAuftrag = befehl;
            BauGrund = w.Bauen.PlatzVerboten(befehl);
        }
        if (BauAuftrag is { } b) UmrissZeigen(w, b);
    }

    private void UmrissZeigen(Stadtwirtschaft w, BauBefehl befehl)
    {
        var gr = w.Bauen.Grundriss(befehl);
        var (breite, tiefe) = ((float)gr.Breite, (float)gr.Tiefe);
        var hoehe = befehl is WohnhausBauen h ? h.Stufe switch { 0 => 5f, 1 => 7f, 2 => 9f, _ => 10f } : 6f;
        if (_bauUmriss is null)
        {
            _bauUmriss = new Node3D { Name = "Bauumriss" };
            AddChild(_bauUmriss);
            _bauFarbe = Transparent(Colors.White, 0.45f);
            _bauVorplatzFarbe = Transparent(Colors.White, 0.3f);
            _bauKoerperFarbe = Transparent(Colors.White, 0.16f);
            _bauPfeilFarbe = Transparent(Colors.White, 0.85f);
            _bauFlaeche = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _bauFarbe };
            _bauVorplatz = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _bauVorplatzFarbe };
            _bauKoerper = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _bauKoerperFarbe };
            _bauPfeil = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(2.4f, 1.2f, 0.3f) }, MaterialOverride = _bauPfeilFarbe, RotationDegrees = new Vector3(90, 0, 0) };
            foreach (var teil in new[] { _bauFlaeche, _bauVorplatz, _bauKoerper, _bauPfeil }) _bauUmriss.AddChild(teil);
            _bauText = Beschriftung("", Vector3.Zero, 40, _bauUmriss);
            _bauText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _bauText.Width = 520;
        }
        _bauUmriss.Visible = true;
        _bauUmriss.Position = AufFundament(Mitte3(gr), DrehungY(gr), new Vector2(breite, tiefe)); // auf Gelände auf der Höhe, auf der das Haus stünde
        _bauUmriss.Rotation = new Vector3(0, DrehungY(gr), 0);
        var farbe = BauGrund is null ? new Color(0.35f, 0.9f, 0.35f) : new Color(0.95f, 0.28f, 0.25f);
        _bauFarbe!.AlbedoColor = new Color(farbe, 0.45f);
        _bauVorplatzFarbe!.AlbedoColor = new Color(farbe.Lightened(0.35f), 0.3f);
        _bauKoerperFarbe!.AlbedoColor = new Color(farbe, 0.16f);
        _bauPfeilFarbe!.AlbedoColor = new Color(farbe.Lightened(0.5f), 0.85f);
        ((BoxMesh)_bauFlaeche!.Mesh).Size = new Vector3(breite, 0.12f, tiefe);
        _bauFlaeche.Position = new Vector3(0, 0.12f, 0);
        var vorplatz = VorplatzVon(gr);
        _bauVorplatz!.Visible = vorplatz > 0; // ein Wohnhausplatz der Karte reicht bis an die Gasse
        ((BoxMesh)_bauVorplatz.Mesh).Size = new Vector3(breite, 0.16f, Mathf.Max(vorplatz, 0.1f));
        _bauVorplatz.Position = new Vector3(0, 0.14f, tiefe / 2 - vorplatz / 2);
        ((BoxMesh)_bauKoerper!.Mesh).Size = new Vector3(breite - 0.4f, hoehe, tiefe - vorplatz - 0.4f);
        _bauKoerper.Position = new Vector3(0, hoehe / 2, -vorplatz / 2);
        _bauPfeil!.Position = new Vector3(0, 0.3f, tiefe / 2 - Mathf.Max(vorplatz, 1) / 2); // Spitze zeigt zur Straße (+z)
        _bauText!.Position = new Vector3(0, hoehe + 4, 0);
        _bauText.Text = BauGrund ?? "Hier kann gebaut werden";
        _bauText.Modulate = farbe.Lightened(0.35f);
    }

    private static StandardMaterial3D Transparent(Color farbe, float alpha) => new()
    {
        AlbedoColor = new Color(farbe, alpha), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
}
