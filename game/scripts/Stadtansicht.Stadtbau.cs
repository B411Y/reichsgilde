using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Bauvorhaben und Fest der Stadt (PR #200) – ein Teil der <c>partial class Stadtansicht</c>. Der Rat beschließt Bauten aus der Stadtkasse
/// (<see cref="Ratsantraege.Baustellen"/>); hier stehen sie am passenden Ort:
/// <list type="bullet">
/// <item><b>brunnen</b>: in der freiesten Ecke des Marktplatzes – erst Baustelle (der Steinkranz wächst), danach ein Brunnen mit Dach.</item>
/// <item><b>marktplatz</b>: vor der Seite des Marktes mit dem meisten Raum eine Baustelle mit Pflastersteinen; fertig verschwindet sie (die Wirkung zeigt der Markt).</item>
/// <item><b>mauer</b>: am Mauerstück mit dem meisten Platz davor ein Gerüst auf der Stadtseite.</item>
/// <item><b>hafen</b>: am Kai neben dem ersten Hafen der Karte.</item>
/// </list>
/// Eine Baustelle ist schlicht wie die der Häuser (Baugrund, Gerüst, Holzstapel, Steinhaufen) und wächst in vier Stufen; Klick zeigt Name, Antragsteller
/// und Restzeit. Während eines Fests wehen an den Ecken des Marktplatzes Fahnen. Neu gebaut wird nur, wenn sich etwas ändert.
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _stadtbau;
    private string _stadtbauKennzeichen = "";

    /// <summary>Die Kennung einer Baustelle der Stadt in der Auswahl („stadtbau:brunnen“).</summary>
    public static string StadtbauKennung(string bauvorhaben) => "stadtbau:" + bauvorhaben;

    private static readonly Color[] Festfarben =
    {
        new(0.70f, 0.14f, 0.12f), new(0.86f, 0.66f, 0.18f), new(0.18f, 0.30f, 0.58f), new(0.20f, 0.45f, 0.24f),
    };

    /// <summary>Wie weit eine Baustelle der Stadt ist, in vier Stufen (0 bis 3).</summary>
    private static int StadtbauStufe(Stadtwirtschaft w, Stadtbaustelle b, long minute)
    {
        var dauer = Math.Max(1, (w.Ratsantraege.BauvorhabenNamens(b.Kennung)?.BauzeitStunden ?? Reichsgilde.Core.Zeit.Spielzeit.StundenProTag) * 60.0);
        return Math.Clamp((int)((1 - (b.FertigAb - minute) / dauer) * 4), 0, 3);
    }

    /// <summary>Jedes Bild: Haben sich Baustellen, Bauten oder das Fest geändert, wird alles neu gestellt.</summary>
    private void StadtbauAktualisieren(Stadtwirtschaft w)
    {
        var ra = w.Ratsantraege;
        if (!ra.IstAktiv) return;
        var minute = w.Uhr.VergangeneMinuten;
        var kennzeichen = string.Join(",", ra.Baustellen.Select(b => b.Kennung + StadtbauStufe(w, b, minute))) + "|" + string.Join(",", ra.Gebaut) + "|" + ra.FestLaeuft;
        if (kennzeichen == _stadtbauKennzeichen) return;
        _stadtbauKennzeichen = kennzeichen;
        _stadtbau?.QueueFree();
        _stadtbau = new Node3D { Name = "Stadtbau" };
        AddChild(_stadtbau);
        foreach (var b in ra.Baustellen)
            if (StadtbauLage(w, b.Kennung) is { } lage)
            {
                StadtbaustelleBauen(w, b, lage, StadtbauStufe(w, b, minute));
                GD.Print($"Baustelle der Stadt: {b.Name} bei x {lage.Origin.X:0}, z {lage.Origin.Z:0}"); // für Klicktests (--blick)
            }
        if (ra.IstGebaut("brunnen") && StadtbauLage(w, "brunnen") is { } brunnen)
            BrunnenBauen(brunnen);
        if (ra.FestLaeuft && _marktHalb > 0)
            FestFahnenBauen();
    }

    /// <summary>Wo ein Bauvorhaben steht (Ursprung, X entlang, Z nach außen); null, wenn die Karte den Ort nicht hat.</summary>
    private Transform3D? StadtbauLage(Stadtwirtschaft w, string kennung)
    {
        var markt = new Transform3D(_marktLage.Basis.Orthonormalized(), _markt);
        switch (kennung)
        {
            case "brunnen" when _marktHalb > 0:
                // in einer Ecke des Platzes, wo am wenigsten steht
                var ecke = _marktHalb - 5;
                return AmBoden(FreiesterOrt(w, new[] { (-ecke, -ecke), (ecke, -ecke), (ecke, ecke), (-ecke, ecke) }.Select(e => markt * new Vector3(e.Item1, 0, e.Item2))),
                    markt.Basis);
            case "marktplatz" when _marktHalb > 0:
                // vor der Seite des Platzes, an der am meisten Raum ist (dorthin wird er größer)
                var rand = _marktHalb + 4;
                var seite = FreiesterOrt(w, new[] { (rand, 0f), (0f, rand), (-rand, 0f), (0f, -rand) }.Select(e => markt * new Vector3(e.Item1, 0, e.Item2)));
                var nachAussen = (seite - _markt).Normalized();
                return AmBoden(seite, new Basis(Vector3.Up.Cross(nachAussen), Vector3.Up, nachAussen));
            case "mauer" when w.Karte is { Mauern.Count: > 0 } karte:
                // nicht im Torweg: Stücke nahe einem Tor fallen weg (dort ist frei, weil die Straße hindurchführt)
                var plaene = karte.Mauern.Where(m => m.Linie.Count >= 2).Select(Mauerplan.Von).ToList();
                var tore = plaene.SelectMany(p => p.Tore).Select(t => new Vector2((float)t.Lage.X, (float)t.Lage.Z)).ToList();
                var stuecke = plaene.SelectMany(p => p.Stuecke).Where(s => tore.All(t => t.DistanceTo(new Vector2((float)s.X, (float)s.Z)) > 20)).ToList();
                if (stuecke.Count == 0) return null;
                // das Mauerstück, vor dem auf der Stadtseite am meisten Platz ist (Feldseite = +Z des Stücks)
                var stadtseite = stuecke.Select(s => MauerOrt(s) - new Vector3((float)s.FeldX, 0, (float)s.FeldZ) * 3).ToList();
                var frei = stuecke[stadtseite.IndexOf(FreiesterOrt(w, stadtseite))];
                return new Transform3D(MauerBasis(frei), MauerOrt(frei));
            case "hafen" when w.Schiffahrt.Haefen.FirstOrDefault() is { } hafen:
                var nachWasser = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ);
                var entlang = new Vector3(-nachWasser.Z, 0, nachWasser.X);
                var ort = new Vector3((float)hafen.Liegeplatz.X, 0, (float)hafen.Liegeplatz.Z) - nachWasser * (float)(Schiffahrt.LiegeplatzAbstand + 6) + entlang * 14;
                return AmBoden(ort, new Basis(entlang, Vector3.Up, nachWasser));
            default:
                return null;
        }
    }

    private Transform3D AmBoden(Vector3 ort, Basis basis) => new(basis, AufBoden(ort));

    /// <summary>
    /// Der Ort unter <paramref name="orte"/>, der am weitesten von allem entfernt ist, was dort steht: Gebäude der Karte (bis zu ihrem Rand, der Markt
    /// selbst zählt nicht) und Brunnen, Kreuz, Bänke und Stände des Marktes.
    /// </summary>
    private Vector3 FreiesterOrt(Stadtwirtschaft w, IEnumerable<Vector3> orte)
    {
        var gebaeude = w.Karte?.Gebaeude.Where(g => new Vector2((float)g.Punkt[0] - _markt.X, (float)g.Punkt[1] - _markt.Z).Length() > 1).ToList() ?? new();
        float Abstand(Vector3 o) => Mathf.Min(
            gebaeude.Select(g => new Vector2((float)g.Punkt[0] - o.X, (float)g.Punkt[1] - o.Z).Length() - GroesseVon(g).Length() / 2).DefaultIfEmpty(99).Min(),
            _marktHindernisse.Select(h => new Vector2(h.Mitte.X - o.X, h.Mitte.Z - o.Z).Length() - h.Radius).DefaultIfEmpty(99).Min());
        return orte.MaxBy(Abstand);
    }

    /// <summary>
    /// Eine Baustelle der Stadt: Baugrund mit Zaun, Gerüst, Holzstapel und Steinhaufen; der Brunnen bekommt seinen wachsenden Steinkranz, der Marktplatz
    /// Pflastersteine, die Mauer ein hohes Gerüst auf der Stadtseite (−Z).
    /// </summary>
    private void StadtbaustelleBauen(Stadtwirtschaft w, Stadtbaustelle b, Transform3D lage, int stufe)
    {
        var kennung = StadtbauKennung(b.Kennung);
        var wurzel = new Node3D { Name = "Baustelle_" + b.Kennung, Transform = lage };
        _stadtbau!.AddChild(wurzel);
        var holz = Stoff(Holz);
        var stein = Stoff(Bruchstein);
        var mauer = b.Kennung == "mauer";
        var (breite, tiefe) = mauer ? (9f, 3f) : (7f, 6f);
        var mitte = mauer ? new Vector3(0, 0, -tiefe / 2 - 1.2f) : Vector3.Zero; // an der Mauer: auf der Stadtseite davor

        Platte(wurzel, mitte + new Vector3(0, 0.04f, 0), new Vector2(breite, tiefe), new Color(0.44f, 0.38f, 0.28f)); // Baugrund
        // Zaun aus Pfosten und einer Latte ringsum (an der Mauer nur vorn)
        foreach (var (von, bis) in mauer
                     ? new[] { (new Vector2(-breite / 2, tiefe / 2), new Vector2(breite / 2, tiefe / 2)) }
                     : new[]
                     {
                         (new Vector2(-breite / 2, -tiefe / 2), new Vector2(breite / 2, -tiefe / 2)), (new Vector2(breite / 2, -tiefe / 2), new Vector2(breite / 2, tiefe / 2)),
                         (new Vector2(breite / 2, tiefe / 2), new Vector2(-breite / 2, tiefe / 2)), (new Vector2(-breite / 2, tiefe / 2), new Vector2(-breite / 2, -tiefe / 2)),
                     })
        {
            var a = mitte + new Vector3(von.X, 0, von.Y);
            var e = mitte + new Vector3(bis.X, 0, bis.Y);
            var laenge = a.DistanceTo(e);
            var richtung = (e - a).Normalized();
            for (var t = 0f; t <= laenge + 0.01f; t += laenge / Mathf.Max(1, Mathf.Round(laenge / 1.8f)))
                Quader(a + richtung * t + new Vector3(0, 0.5f, 0), new Vector3(0.1f, 1.0f, 0.1f), holz, wurzel);
            var latte = Quader((a + e) / 2 + new Vector3(0, 0.8f, 0), new Vector3(laenge, 0.08f, 0.05f), holz, wurzel);
            latte.Rotation = new Vector3(0, -Mathf.Atan2(richtung.Z, richtung.X), 0);
        }

        switch (b.Kennung)
        {
            case "brunnen":
                // Der Steinkranz wächst mit der Bauzeit
                var hoehe = 0.25f + 0.2f * stufe;
                wurzel.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 1.3f, BottomRadius = 1.35f, Height = hoehe, RadialSegments = 16 },
                    MaterialOverride = stein, Position = new Vector3(0, hoehe / 2, 0),
                });
                break;
            case "marktplatz":
                for (var i = 0; i <= stufe; i++) // Stapel Pflastersteine
                    Quader(new Vector3(-2 + i * 1.3f, 0.3f, -1.2f), new Vector3(1.0f, 0.6f, 1.0f), Stoff(Mauerstein), wurzel);
                break;
        }

        // Gerüst: an der Mauer hoch an der Stadtseite, sonst klein hinten auf dem Grund
        var gh = mauer ? 5f + stufe : 2.5f + 0.5f * stufe;
        var gz = mauer ? -0.9f : -tiefe / 2 + 0.8f;
        var gb = mauer ? breite - 1 : breite * 0.5f;
        foreach (var x in new[] { -gb / 2, 0, gb / 2 })
            Quader(new Vector3(x, gh / 2, gz), new Vector3(0.14f, gh, 0.14f), holz, wurzel);
        for (var y = 1.4f; y < gh; y += 1.5f)
            Quader(new Vector3(0, y, gz), new Vector3(gb + 0.4f, 0.1f, 0.8f), holz, wurzel);
        Holzstapel(wurzel, mitte + new Vector3(breite / 2 - 1.6f, 0, tiefe / 2 - 1.0f));
        Quader(mitte + new Vector3(-breite / 2 + 1.0f, 0.4f, tiefe / 2 - 1.0f), new Vector3(1.4f, 0.8f, 1.0f), stein, wurzel); // Steinhaufen

        Klickflaeche(kennung, mitte + new Vector3(0, 2, 0), new Vector3(breite, 4, tiefe), wurzel);
        Beschriftung($"{b.Name} (Baustelle der Stadt)", mitte + new Vector3(0, 8, 0), 40, wurzel, kennung);
    }

    /// <summary>Der fertige Brunnen: Steinkranz mit Wasser, zwei Pfosten, Querbalken mit Eimer und ein kleines Dach.</summary>
    private void BrunnenBauen(Transform3D lage)
    {
        var wurzel = new Node3D { Name = "Brunnen", Transform = lage };
        _stadtbau!.AddChild(wurzel);
        var holz = Stoff(Holz);
        wurzel.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.3f, BottomRadius = 1.35f, Height = 0.9f, RadialSegments = 20 },
            MaterialOverride = Stoff(Bruchstein), Position = new Vector3(0, 0.45f, 0),
        });
        wurzel.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.05f, BottomRadius = 1.05f, Height = 0.02f, RadialSegments = 20 },
            MaterialOverride = Stoff(new Color(0.18f, 0.28f, 0.32f)), Position = new Vector3(0, 0.82f, 0), // Wasser
        });
        foreach (var x in new[] { -1.2f, 1.2f })
            Quader(new Vector3(x, 1.5f, 0), new Vector3(0.18f, 2.2f, 0.18f), holz, wurzel);
        Quader(new Vector3(0, 2.3f, 0), new Vector3(2.6f, 0.14f, 0.14f), holz, wurzel);       // Querbalken
        Quader(new Vector3(0, 1.6f, 0), new Vector3(0.35f, 0.4f, 0.35f), holz, wurzel);       // Eimer
        Dach(new Vector3(0, 2.6f, 0), new Vector3(3.2f, 0.9f, 2.2f), Stoff(new Color(0.42f, 0.22f, 0.16f)), quer: true, eltern: wurzel);
        Klickflaeche(StadtbauKennung("brunnen"), new Vector3(0, 1.5f, 0), new Vector3(3, 3, 3), wurzel);
        Beschriftung("Brunnen der Stadt", new Vector3(0, 5, 0), 40, wurzel, StadtbauKennung("brunnen"));
    }

    /// <summary>Fest der Stadt: an jeder Ecke des Marktplatzes ein Mast mit Fahne in den Farben der Stadt.</summary>
    private void FestFahnenBauen()
    {
        var markt = new Transform3D(_marktLage.Basis.Orthonormalized(), _markt);
        var ecke = _marktHalb - 1.2f;
        var nummer = 0;
        foreach (var (x, z) in new[] { (-ecke, -ecke), (ecke, -ecke), (ecke, ecke), (-ecke, ecke) })
        {
            const float hoehe = 9;
            var fuss = AufBoden(markt * new Vector3(x, 0, z));
            var mast = new Node3D { Name = "Festfahne", Position = fuss };
            _stadtbau!.AddChild(mast);
            mast.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.14f, Height = hoehe, RadialSegments = 8 },
                Position = new Vector3(0, hoehe / 2, 0), MaterialOverride = Stoff(Holz),
            });
            var weg = (markt.Basis * new Vector3(x, 0, z)).Normalized(); // nach außen
            FahnenTuchAmMast(new Vector3(0, hoehe - 1.6f, 0), weg, 4.0f, 2.4f, Festfarben[nummer++ % Festfarben.Length], mast, kerbe: 0.8f);
        }
    }

    /// <summary>Text für die Auswahl einer Baustelle der Stadt (oder des fertigen Brunnens); null, wenn die Kennung keine ist.</summary>
    public string? StadtbauText(string kennung)
    {
        if (!kennung.StartsWith("stadtbau:") || _wirtschaft is not { } w) return null;
        var ra = w.Ratsantraege;
        var k = kennung["stadtbau:".Length..];
        var daten = ra.BauvorhabenNamens(k);
        if (ra.Baustellen.FirstOrDefault(b => b.Kennung == k) is { } b)
        {
            var rest = Math.Max(0, b.FertigAb - w.Uhr.VergangeneMinuten);
            return $"{b.Name} – Baustelle der Stadt\n\nBeschlossen vom Rat{(b.Antragsteller is { } a ? $" auf Antrag von {a.Inhaber}" : "")}, " +
                   $"bezahlt aus der Stadtkasse ({Zahlenformat.Ganz(b.Kosten)} {w.Daten.Allgemein.Waehrung}).\nFertig in etwa {Math.Ceiling(rest / 60.0)} Stunden." +
                   (daten is null ? "" : $"\n\n{daten.Beschreibung}");
        }
        return ra.IstGebaut(k) ? $"{daten?.Name ?? k} der Stadt\n\n{daten?.Beschreibung}" : null;
    }
}
