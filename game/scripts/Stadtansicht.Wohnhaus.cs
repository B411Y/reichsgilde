using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das eigene Wohnhaus von innen – achter Teil der <c>partial class Stadtansicht</c>.
///
/// Doppelklick auf das Wohnhaus des Spielers (Stadtansicht.Spielhaeuser.cs) betritt es in der Raumansicht
/// (Stadtansicht.Raeume.cs) mit zwei Räumen: Wohnstube (Familie) und Kontor (Kasse, Oberhaupt am Pult).
/// Beide standen früher in der Werkstatt; dort fehlen sie, sobald es das Wohnhaus gibt (<see cref="RaeumeImWohnhaus"/>).
///
/// Technisch ist das Wohnhaus ein Sonderbau ohne eigene Hülle: Das Haus in der Stadt wird bei jeder Stufe neu gebaut,
/// die Räume aber nur einmal – sie hängen an einem eigenen Knoten 200 m unter der Stadt wie die Räume des Rathauses.
/// Mit der Stufe des Hauses kommt Schmuck dazu (Teppich, Wandbehang, Leuchter, Wappen).
/// Häuser der Rivalen kann man nicht betreten.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Das Wohnhaus des Spielers als betretbares Gebäude (null, solange es keine Wohnhäuser gibt).</summary>
    private SonderbauAnsicht? _wohnhausAnsicht;

    /// <summary>
    /// Legt die Räume im Wohnhaus des Spielers an (einmal, beim Verbinden mit der Simulation).
    /// Der Platz ist derselbe, den <see cref="SpielhaeuserAktualisieren"/> dem Haus später gibt (gleiche Reihenfolge der Konten).
    /// </summary>
    private void WohnhausRaeumeAnlegen()
    {
        if (_wohnhausAnsicht is not null || _spielerKonto is not { } konto || _simulation?.Wirtschaft is not { } w || !w.Wohnen.IstAktiv) return;
        var nummer = w.Konten.ToList().IndexOf(konto);
        var plaetze = SpielhausPlaetze(w.Konten.Count);
        if (nummer < 0 || nummer >= plaetze.Count) return;

        var kennung = WohnhausKennung(konto);
        var innen = new Node3D { Name = "Innen_Wohnhaus", Visible = false, Position = new Vector3(0, RaumTiefe, 0) };
        AddChild(innen);
        var a = new SonderbauAnsicht { Mitte = plaetze[nummer].Mitte, Schluessel = kennung, Titel = "Euer Wohnhaus", Innen = innen };
        var haus = new Hausstand(() => konto, () => Besitzerfarbe(konto), () => true, () => true, "");
        var stube = Vector3.Zero;
        var kontor = new Vector3(RaumAbstand, 0, 0);
        WohnstubeEinrichten(a, stube, haus);
        KontorEinrichten(a, kontor, Putz, haus);
        WohnhausSchmuck(a, stube, kontor, konto);
        _sonderbauten[kennung] = a;
        _wohnhausAnsicht = a;
    }

    /// <summary>
    /// Schmuck nach der Stufe des Hauses (0 Fachwerkhaus … 4 Stadthof): ab Bürgerhaus Teppiche und ein Tellerbord,
    /// ab Steinhaus Wandbehang und Landkarte, ab Stadtpalais Kronleuchter und eine zweite Truhe, im Stadthof das Wappen.
    /// </summary>
    private void WohnhausSchmuck(SonderbauAnsicht a, Vector3 s, Vector3 k, Konto konto)
    {
        var schmuck = new List<(Node3D Teil, int AbStufe)>();
        void Ab(int stufe, params Node3D[] teile) { foreach (var t in teile) schmuck.Add((t, stufe)); }
        var hausfarbe = Material(Besitzerfarbe(konto));
        var gold = Metall(Gold);
        var eisen = Metall(new Color(0.18f, 0.18f, 0.2f));
        var zinn = Metall(new Color(0.62f, 0.62f, 0.64f));
        var dunkel = Material(DunklesHolz);
        var teppich = Material(new Color(0.42f, 0.14f, 0.12f));
        var kerze = Leuchtend(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.7f, 0.3f));

        // ── Wohnstube ──
        Ab(1, Teil(a, s + new Vector3(0.4f, 0.11f, 0), new Vector3(3.8f, 0.02f, 2.8f), teppich));
        // Tellerbord an der Ostwand
        Ab(1, Teil(a, s + new Vector3(4.05f, 2.2f, 0.2f), new Vector3(0.3f, 0.06f, 1.8f), dunkel));
        foreach (var dz in new[] { -0.55f, 0f, 0.55f })
            Ab(1, Rund(a, s + new Vector3(4.05f, 2.42f, 0.2f + dz), 0.2f, 0.03f, zinn, drehung: new Vector3(0, 0, 80)));
        // Wandbehang zwischen den Fenstern, im Stadthof mit Wappen davor
        Ab(2, Teil(a, s + new Vector3(0, 2.7f, -3.27f), new Vector3(1.6f, 2.0f, 0.04f), hausfarbe),
              Teil(a, s + new Vector3(0, 3.75f, -3.25f), new Vector3(1.9f, 0.08f, 0.08f), dunkel));
        Ab(4, Wappen(a, s + new Vector3(0, 2.8f, -3.2f), gold, hausfarbe));
        // Kronleuchter über dem Tisch – brennt, sobald er hängt
        Ab(3, Rund(a, s + new Vector3(0.4f, 3.0f, 0), 0.65f, 0.06f, eisen, segmente: 16),
              Teil(a, s + new Vector3(0.4f, 3.65f, 0), new Vector3(0.04f, 1.3f, 0.04f), eisen));
        for (var i = 0; i < 6; i++)
        {
            var winkel = i * Mathf.Tau / 6;
            var ort = s + new Vector3(0.4f + 0.6f * Mathf.Cos(winkel), 3.12f, 0.6f * Mathf.Sin(winkel));
            Ab(3, Rund(a, ort, 0.04f, 0.18f, Material(Leinen), segmente: 6), Teil(a, ort + new Vector3(0, 0.13f, 0), new Vector3(0.05f, 0.08f, 0.05f), kerze));
        }
        FeuerAnlegen(a, s + new Vector3(0.4f, 2.7f, 0), null, lichtAn: 1.1f, lichtAus: 0, an: () => konto.WohnhausStufe >= 3, reichweite: 7);

        // ── Kontor ──
        Ab(1, Teil(a, k + new Vector3(0, 0.11f, 0.4f), new Vector3(3.2f, 0.02f, 2.6f), teppich));
        // Landkarte an der Ostwand über der Geldtruhe
        Ab(2, Teil(a, k + new Vector3(3.66f, 2.4f, -0.9f), new Vector3(0.04f, 1.2f, 1.7f), Material(Pergament)),
              Teil(a, k + new Vector3(3.64f, 2.4f, -0.9f), new Vector3(0.04f, 1.32f, 1.82f), dunkel));
        // zweite Geldtruhe und ein Standleuchter
        Ab(3, Teil(a, k + new Vector3(2.9f, 0.4f, 1.4f), new Vector3(1.2f, 0.8f, 0.8f), Material(Holz)),
              Teil(a, k + new Vector3(2.9f, 0.82f, 1.4f), new Vector3(1.25f, 0.06f, 0.85f), eisen),
              Rund(a, k + new Vector3(-2.6f, 0.8f, 1.6f), 0.05f, 1.6f, eisen, segmente: 8),
              Rund(a, k + new Vector3(-2.6f, 1.68f, 1.6f), 0.05f, 0.18f, Material(Leinen), segmente: 6),
              Teil(a, k + new Vector3(-2.6f, 1.82f, 1.6f), new Vector3(0.06f, 0.1f, 0.06f), kerze));
        Ab(4, Wappen(a, k + new Vector3(0, 2.9f, -2.98f), gold, hausfarbe));

        a.Raumzustaende.Add((_, _) =>
        {
            hausfarbe.AlbedoColor = Besitzerfarbe(konto);
            foreach (var (teil, ab) in schmuck) teil.Visible = konto.WohnhausStufe >= ab;
        });
    }

    /// <summary>Wappenschild: goldener Rand, innen die Hausfarbe mit goldenem Schrägbalken, unten spitz (gedrehte Quader).</summary>
    private Node3D Wappen(InnenAnsicht a, Vector3 ort, StandardMaterial3D gold, StandardMaterial3D farbe)
    {
        var g = new Node3D { Position = a.Mitte + ort };
        a.Innen!.AddChild(g);
        Quader(new Vector3(0, 0.1f, 0), new Vector3(0.8f, 0.7f, 0.04f), gold, g);
        Quader(new Vector3(0, -0.3f, 0), new Vector3(0.56f, 0.56f, 0.04f), gold, g).RotationDegrees = new Vector3(0, 0, 45);
        Quader(new Vector3(0, 0.1f, 0.02f), new Vector3(0.66f, 0.58f, 0.04f), farbe, g);
        Quader(new Vector3(0, -0.28f, 0.02f), new Vector3(0.44f, 0.44f, 0.04f), farbe, g).RotationDegrees = new Vector3(0, 0, 45);
        Quader(new Vector3(0, 0, 0.04f), new Vector3(0.14f, 0.85f, 0.04f), gold, g).RotationDegrees = new Vector3(0, 0, 40);
        return g;
    }
}
