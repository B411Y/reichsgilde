using Reichsgilde.Core.Bauen;

/// <summary>Bodenstempel (Darstellung, Boden Stufe 2): weicher, ausgefranster Rand, nur auf Wiese, deterministisch, Kopie bleibt getrennt.</summary>
static class BodenstempelTests
{
    static IReadOnlyList<IReadOnlyList<(double X, double Z)>> Quadrat(double x0, double z0, double x1, double z1) =>
        new IReadOnlyList<(double, double)>[] { new[] { (x0, z0), (x1, z0), (x1, z1), (x0, z1) } };

    static readonly Bodenstempel Erde = new(Bodentextur.Erde, 1, 1.5, 0.4, 0, 3);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bodenstempel: Innen ganz Erde, weit draußen Wiese, der Rand fällt weich ab", () =>
        {
            var r = new Bodenraster();
            r.Stempeln(Quadrat(0, 0, 20, 20), Erde);
            Pruefe.Gleich(255, (int)r.Gewicht(10, 10, Bodentextur.Erde));
            Pruefe.Gleich(255, (int)r.Gewicht(10, 10, Bodentextur.Erde) + r.Gewicht(10, 10, Bodentextur.Wiese) - r.Gewicht(10, 10, Bodentextur.Wiese));
            Pruefe.Gleich(0, (int)r.Gewicht(-4, 10, Bodentextur.Erde));
            Pruefe.Gleich(255, (int)r.Gewicht(-4, 10, Bodentextur.Wiese));
            var rand = Enumerable.Range(-3, 5).Select(x => (int)r.Gewicht(x, 10, Bodentextur.Erde)).ToList();
            Pruefe.Wahr(rand.Any(w => w is > 10 and < 245), "mindestens ein Punkt am Rand ist Mischung: " + string.Join(",", rand));
            for (var i = 0; i < rand.Count - 1; i++) Pruefe.Wahr(rand[i] <= rand[i + 1] + 60, "nach innen im Ganzen steigend");
        }),

        ("Bodenstempel: Gemaltes bleibt, die Anteile ergeben nie mehr als 255, ein zweiter Stempel derselben Art ändert nichts mehr", () =>
        {
            var r = new Bodenraster();
            var w = new byte[11];
            w[(int)Bodentextur.Fels - 1] = 200;
            r.WerteSetzen(10, 10, w);
            r.Stempeln(Quadrat(0, 0, 20, 20), Erde);
            Pruefe.Gleich(200, (int)r.Gewicht(10, 10, Bodentextur.Fels));
            Pruefe.Gleich(55, (int)r.Gewicht(10, 10, Bodentextur.Erde));
            Pruefe.Gleich(0, (int)r.Gewicht(10, 10, Bodentextur.Wiese));
            var vorher = r.Kopie();
            r.Stempeln(Quadrat(0, 0, 20, 20), Erde);
            for (var j = -3; j < 24; j++)
                for (var i = -3; i < 24; i++)
                    foreach (var t in Enum.GetValues<Bodentextur>())
                        Pruefe.Gleich((int)vorher.Gewicht(i, j, t), (int)r.Gewicht(i, j, t));
        }),

        ("Bodenstempel: zwei aneinanderstoßende Vielecke haben an der Naht keine Lücke; Stärke und Fleck lassen Wiese stehen", () =>
        {
            var r = new Bodenraster();
            var zwei = new IReadOnlyList<(double, double)>[] { new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0) }, new[] { (10.0, 0.0), (20.0, 0.0), (20.0, 10.0), (10.0, 10.0) } };
            r.Stempeln(zwei, Erde with { Rauschen = 0 });
            Pruefe.Gleich(255, (int)r.Gewicht(10, 5, Bodentextur.Erde));
            var halb = new Bodenraster();
            halb.Stempeln(Quadrat(0, 0, 20, 20), new Bodenstempel(Bodentextur.Kies, 0.5, 1, 0, 0, 1));
            Pruefe.Gleich(128, (int)halb.Gewicht(10, 10, Bodentextur.Kies));
            var fleck = new Bodenraster();
            fleck.Stempeln(Quadrat(0, 0, 30, 30), new Bodenstempel(Bodentextur.Erde, 1, 1, 0, 1, 5));
            var werte = Enumerable.Range(3, 24).SelectMany(i => Enumerable.Range(3, 24).Select(j => (int)fleck.Gewicht(i, j, Bodentextur.Erde))).ToList();
            Pruefe.Wahr(werte.Min() < 100 && werte.Max() > 200, "Fleck: Innen wechselt Erde und Wiese");
        }),

        ("Bodenstempel: gleiche Angaben geben dasselbe Raster (Datei), die Kopie lässt das Original unberührt, Wiese und Stärke 0 stempeln nichts", () =>
        {
            var original = new Bodenraster();
            var a = original.Kopie();
            var b = original.Kopie();
            a.Stempeln(Quadrat(-5, -5, 30, 12), Erde);
            b.Stempeln(Quadrat(-5, -5, 30, 12), Erde);
            Pruefe.Gleich(a.AlsJson()!.ToJsonString(), b.AlsJson()!.ToJsonString());
            Pruefe.Wahr(original.IstLeer, "das Original bleibt leer");
            var c = original.Kopie();
            c.Stempeln(Quadrat(0, 0, 10, 10), new Bodenstempel(Bodentextur.Wiese, 1, 1, 0, 0, 0));
            c.Stempeln(Quadrat(0, 0, 10, 10), Erde with { Staerke = 0 });
            Pruefe.Wahr(c.IstLeer, "nichts gestempelt");
            // ein Dreieck, ein konkaves Vieleck: nur Punkte darin sind Erde
            var d = original.Kopie();
            d.Stempeln(new IReadOnlyList<(double, double)>[] { new[] { (0.0, 0.0), (20.0, 0.0), (0.0, 20.0) } }, Erde with { Rauschen = 0 });
            Pruefe.Gleich(255, (int)d.Gewicht(3, 3, Bodentextur.Erde));
            Pruefe.Gleich(0, (int)d.Gewicht(17, 17, Bodentextur.Erde));
        }),
    };
}
