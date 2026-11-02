using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Fels R3 + R5: die elf Formen des Felswand-Satzes (Einsinken, Überlappung, Datei), das Drehen und Skalieren mehrerer Stücke in Weltlage
/// (Entscheid 02.11.2026) und „Hang mitformen“ (Felshang: hebt das Gelände hinter den Wänden an, ein Schritt, rückgängig, Standard aus).
/// </summary>
static class FelsgizmoTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein Hügel mit unebenem Boden: Westen eben, dazwischen ein Buckel (damit die Höhe unter den Stücken verschieden ist).</summary>
    static Kartenentwurf Huegel()
    {
        var e = Leer();
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 12, Kante: 0.5), new[] { O(20, 0) });
        return e;
    }

    static void Nahe(double erwartet, double ist, double toleranz, string text) =>
        Pruefe.Wahr(Math.Abs(erwartet - ist) <= toleranz, $"{text}: erwartet {erwartet}, ist {ist}");

    /// <summary>Der Aufsetzpunkt: Ort und Boden + Versatz (damit rechnet der Kern in Weltlage).</summary>
    static Felsvektor Ursprung(Kartenentwurf e, EntwurfFels f) => new(f.X, Felsraum.KontaktY(f.AlsLage(), e.Hoehen), f.Z);

    /// <summary>
    /// Ein fester Ablauf für den Gleichheitstest: Große Wand mit „Hang mitformen“ an einem Hügel, einzelne Wände gedreht, Stücke um x und z gedreht und
    /// skaliert (Weltlage), aufgesetzt, Rückgängig und Wiederholen – die Kartendatei am Ende.
    /// </summary>
    internal static string Ablauf()
    {
        var e = Huegel();
        var hang = new Felspinsel(Gruppe: "wandgross", HangFormen: true, Boden: true);
        e.FelsStempeln(hang, O(-40, 60), drehung: 30, startwert: 8);
        e.FelsStempeln(hang with { Gruppe = null, Formen = new[] { "wand_mittel_2", "pfeilerbuendel", "gesims_1" } }, O(60, 90), startwert: 3);
        e.FelsStempeln(new Felspinsel(Formen: new[] { "wand_gross_2" }, Boden: false), O(110, -40), drehung: 200, startwert: 4);
        var alle = e.Felsen.ToList();
        e.FelsTransformieren(alle.Take(4), new Felsaenderung(Drehachse: Felsachse.X, Winkel: 21.7));
        e.FelsTransformieren(alle.Skip(2).Take(5), new Felsaenderung(Drehachse: Felsachse.Z, Winkel: -33.3));
        e.FelsTransformieren(alle.Skip(1).Take(6), new Felsaenderung(FaktorX: 1.4, FaktorY: 1.1, FaktorZ: 0.8));
        e.FelsTransformieren(alle.Skip(3).Take(3), new Felsaenderung(Drehachse: Felsachse.Y, Winkel: 77.7));
        e.FelsEinrasten(alle.Skip(5).Take(2), ausrichten: true);
        e.Rueckgaengig();
        e.Rueckgaengig();
        e.Wiederholen();
        return e.ZuJson();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels R4: Die elf Formen des Wand-Satzes – Einsinken je Form, Datei lesen und schreiben, Stadtkarte, Überlappung enger als bei F1", () =>
        {
            var e = Leer();
            var p = new Felspinsel(Formen: new[] { "wand_gross_1" }, Boden: false);
            var neue = e.FelsStempeln(p, O(0, 0), drehung: 0, startwert: 3);
            Pruefe.Gleich(1, neue.Count);
            // 30 % des Pinsels mal 0,33 = 10 % (± 0,05 aus dem Wurf)
            Nahe(0.1, neue[0].Einsinken, 0.06, "Wand steckt etwa 10 % tief");
            var gesims = e.FelsStempeln(new Felspinsel(Formen: new[] { "gesims_1" }, Boden: false), O(30, 0), drehung: 0, startwert: 3)[0];
            Nahe(0, gesims.Einsinken, 0.06, "Gesims setzt auf");
            var f1 = e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(60, 0), drehung: 0, startwert: 3)[0];
            Nahe(0.3, f1.Einsinken, 0.06, "F1 wie vorher");
            var text = e.ZuJson();
            Pruefe.Wahr(text.Contains("\"form\": \"wand_gross_1\"") && text.Contains("\"form\": \"gesims_1\""), "die Formen stehen in der Datei:\n" + text);
            var zwei = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, zwei.ZuJson(), "wieder geschrieben");
            Pruefe.Gleich(3, Stadtkarte.AusJson(text, Katalog.Value).Felsen!.Stuecke.Count);
            Pruefe.Gleich(0, Kartenpruefung.Pruefen(zwei).Count(b => b.Regel == "fels"), "keine Hinweise zu den Formen");
            // Wände dürfen sich mehr überlappen: Zwei Wände 14 m auseinander (Radius je knapp 7) halten Abstand wie 35 % nicht, aber wie 60 %
            var r = Felsformen.Finde("wand_gross_1")!.Radius;
            var streu = Leer();
            streu.FelsStreuen(new Felspinsel(Formen: new[] { "wand_gross_1" }, Radius: 40, Dichte: 0.5, Boden: false),
                Enumerable.Range(0, 12).Select(i => O(-60 + i * 10, 0)).ToList(), startwert: 11);
            var wand = streu.Felsen.ToList();
            Pruefe.Wahr(wand.Count >= 3, "es entstehen Wände");
            var dichtester = double.MaxValue;
            for (var i = 0; i < wand.Count; i++)
                for (var j = i + 1; j < wand.Count; j++)
                    dichtester = Math.Min(dichtester, Math.Sqrt(Math.Pow(wand[i].X - wand[j].X, 2) + Math.Pow(wand[i].Z - wand[j].Z, 2)) / (wand[i].Radius + wand[j].Radius));
            Pruefe.Wahr(dichtester >= 0.4 - 1e-9, $"Abstand mindestens 40 % der Radiensumme, ist {dichtester}");
            Pruefe.Wahr(dichtester < 0.65, $"enger als bei F1 (65 %), ist {dichtester}");
            _ = r;
        }),

        ("Fels R3: Mehrere Stücke um Welt-x drehen und skalieren rechnet in Weltlage und den Versatz am neuen Ort zurück", () =>
        {
            var e = Huegel();
            var p = new Felspinsel(Formen: new[] { "block" }, Boden: false);
            e.FelsStempeln(p, O(5, -12), drehung: 0, startwert: 1);
            e.FelsStempeln(p, O(25, 12), drehung: 0, startwert: 2);
            e.FelsStempeln(p, O(40, -3), drehung: 0, startwert: 3);
            var alle = e.Felsen.ToList();
            e.FelsTransformieren(alle, new Felswerte(Kippen: 0, Rollen: 0, Neigung: 0));
            var vorher = alle.Select(f => Ursprung(e, f)).ToList();
            var mitte = e.FelsMitteWelt(alle);
            Nahe(vorher.Average(v => v.X), mitte.X, 1e-9, "Mitte x");
            Nahe(vorher.Average(v => v.Y), mitte.Y, 1e-9, "Mitte y (Höhe der Ursprünge)");
            // um die x-Achse der Welt: jedes Stück landet dort, wo der starre Körper es hinbringt
            e.FelsTransformieren(alle, new Felsaenderung(Drehachse: Felsachse.X, Winkel: 25));
            var um = Felsbasis.Um(Felsachse.X, 25);
            for (var i = 0; i < alle.Count; i++)
            {
                var soll = new Felsvektor(mitte.X, mitte.Y, mitte.Z) + um.Mal(vorher[i] - mitte);
                var ist = Ursprung(e, alle[i]);
                Nahe(soll.X, ist.X, 0.06, $"Stück {i}: x");
                Nahe(soll.Z, ist.Z, 0.06, $"Stück {i}: z");
                Nahe(soll.Y, ist.Y, 0.08, $"Stück {i}: Höhe (Versatz am neuen Ort zurückgerechnet)");
            }
            Pruefe.Gleich("3 Felsstücke gedreht", e.Geschichte.RueckgaengigText);
            e.Rueckgaengig();
            for (var i = 0; i < alle.Count; i++) Nahe(vorher[i].Y, Ursprung(e, alle[i]).Y, 0.06, $"Stück {i} nach Rückgängig");

            // skalieren um die Mitte: die Ursprünge rücken in Weltachsen auseinander (Höhe eingeschlossen)
            var mitte2 = e.FelsMitteWelt(alle);
            var vor2 = alle.Select(f => Ursprung(e, f)).ToList();
            e.FelsTransformieren(alle, new Felsaenderung(FaktorX: 2, FaktorY: 2, FaktorZ: 2));
            for (var i = 0; i < alle.Count; i++)
            {
                var soll = mitte2 + (vor2[i] - mitte2) * 2;
                var ist = Ursprung(e, alle[i]);
                Nahe(soll.X, ist.X, 0.06, $"skaliert {i}: x");
                Nahe(soll.Z, ist.Z, 0.06, $"skaliert {i}: z");
                Nahe(soll.Y, ist.Y, 0.2, $"skaliert {i}: Höhe");
            }

            // um die Senkrechte bleibt der Versatz: die Stücke gehen mit dem Gelände (kein Schweben auf dem Hügel)
            var versatz = alle.Select(f => f.Versatz).ToList();
            e.FelsTransformieren(alle, new Felsaenderung(Drehachse: Felsachse.Y, Winkel: 90));
            for (var i = 0; i < alle.Count; i++) Pruefe.Gleich(versatz[i], alle[i].Versatz, $"Stück {i}: Versatz bleibt bei der Drehung um die Senkrechte");
        }),

        ("Fels R5: Hang mitformen hebt das Gelände hinter der Wand, nicht davor; Standard aus; ein Schritt; Rückgängig Byte für Byte", () =>
        {
            Pruefe.Wahr(!new Felspinsel().HangFormen, "Standard: aus");
            var ohne = Leer();
            var vorher = ohne.ZuJson();
            ohne.FelsStempeln(new Felspinsel(Formen: new[] { "wand_gross_1" }, Boden: false), O(0, 0), drehung: 0, startwert: 5);
            Pruefe.Gleich(false, ohne.Hoehen.HatHoehen, "ohne das Häkchen bleibt das Gelände eben");

            var e = Leer();
            var p = new Felspinsel(Formen: new[] { "wand_gross_1" }, Boden: false, HangFormen: true);
            var neue = e.FelsStempeln(p, O(0, 0), drehung: 0, startwert: 5);
            var wand = neue[0];
            var rahmen = e.FelsRahmen(wand);
            var oben = rahmen.Ursprung.Y + rahmen.Groesse.Y;
            // Drehung 0: Schauseite +Z (Süden), der Hang liegt bei negativem z. Vorn und an der Fußlinie bleibt es eben.
            Nahe(0, e.Hoehen.Hoehe(0, 20), 1e-9, "vor der Wand eben");
            Nahe(0, e.Hoehen.Hoehe(0, 2), 0.05, "an der Fußlinie unverändert");
            Nahe(0, e.Hoehen.Hoehe(0, 0), 0.05, "bis 2,5 m hinter der Fußlinie unverändert");
            // an der Rückseite (z = −5 · Maßstab · Achse) reicht der Hang fast bis zur Oberkante, dahinter bleibt er so hoch
            var s = wand.Massstab * wand.AchseZ;
            var rueck = e.Hoehen.Hoehe(0, -5.2 * s);
            Pruefe.Wahr(rueck > 0.8 * (oben - Felshang.Unterkante), $"Hang an der Rückseite {rueck:0.0} m, Oberkante {oben:0.0} m");
            Pruefe.Wahr(e.Hoehen.Hoehe(0, -5.2 * s - 6) >= rueck - 0.05, "dahinter nicht niedriger");
            Pruefe.Wahr(e.Hoehen.Hoehe(0, -5.2 * s) <= oben, "nicht über der Oberkante an der Rückseite");
            // seitlich läuft er aus: weit neben der Wand wieder eben
            Nahe(0, e.Hoehen.Hoehe(wand.Massstab * 10.2 + 14, -8 * s), 0.05, "weit neben der Wand eben");
            Pruefe.Gleich("Große Wand 1 gestempelt", e.Geschichte.RueckgaengigText);
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: Byte für Byte die Karte vorher (Stück und Gelände zusammen)");
            Pruefe.Gleich(false, e.Hoehen.HatHoehen);
            e.Wiederholen();
            Pruefe.Wahr(e.Hoehen.Hoehe(0, -5.2 * s) > 0.8 * (oben - Felshang.Unterkante), "Wiederholen hebt den Hang wieder");
            // Formen ohne offene Rückseite (F1, Gesims, Pfeiler) formen nichts
            var f1 = Leer();
            f1.FelsStempeln(new Felspinsel(Formen: new[] { "wand", "gesims_1", "pfeiler_1", "block" }, Boden: false, HangFormen: true), O(0, 0), drehung: 0, startwert: 5);
            Pruefe.Gleich(false, f1.Hoehen.HatHoehen, "nur die Wände formen den Hang");
        }),

        ("Fels R3/R5: Der feste Ablauf mit Hang, Weltlage und Rückgängig gibt zweimal dieselben Bytes; Hang und Stücke stehen in der Datei", () =>
        {
            var (a, b) = (Ablauf(), Ablauf());
            Pruefe.Gleich(a, b, "zweiter Lauf");
            Pruefe.Wahr(a.Contains("\"hoehen\"") && a.Contains("\"form\": \"wand_gross_1\"") && a.Contains("\"form\": \"wand_gross_2\""), "Gelände und Stücke in der Datei:\n" + a[..Math.Min(a.Length, 600)]);
            var zwei = Kartenentwurf.AusJson(a, Katalog.Value);
            Pruefe.Gleich(a, zwei.ZuJson(), "wieder geschrieben");
        }),

        ("Fels R5: Hang mitformen hebt nur an (nie ab), ist von der Reihenfolge der Stücke eines Stempels unabhängig und gibt zweimal gleiche Bytes", () =>
        {
            // zwei Wände in einem Aufruf: Das Gelände ist der Höchstwert beider, gleich in welcher Reihenfolge sie kommen
            string Lauf(bool umgekehrt)
            {
                var e = Huegel();
                var lagen = new List<Felslage>
                {
                    new("wand_gross_2", null, -30, 40, 40, 1, 0, 0.1, 0, 0),
                    new("wand_mittel_1", null, -10, 40, 70, 1, 0, 0.1, 0, 0),
                    new("wand_gross_3", null, -20, 52, 55, 1, 0, 0.1, 0, 0),
                };
                if (umgekehrt) lagen.Reverse();
                var aenderung = Felshang.Formen(e.Hoehen, e.Kartengrenzen.Aussen, lagen);
                Pruefe.Wahr(aenderung is not null, "die Wände heben das Gelände");
                return e.Hoehen.AlsJson()?.ToJsonString() ?? "";
            }
            var (a, b, c) = (Lauf(false), Lauf(false), Lauf(true));
            Pruefe.Gleich(a, b, "zweimal dasselbe");
            Pruefe.Gleich(a, c, "die Reihenfolge der Stücke ändert das Gelände nicht (Höchstwert)");

            var hang = Huegel();
            var vorher = hang.Hoehen.Kopie();
            hang.FelsStempeln(new Felspinsel(Formen: new[] { "wand_gross_3" }, Boden: false, HangFormen: true), O(-20, 30), drehung: 135, startwert: 1);
            var gesenkt = 0;
            for (var j = -50; j < 150; j++)
                for (var i = -80; i < 100; i++)
                    if (hang.Hoehen.Wert(i, j) < vorher.Wert(i, j)) gesenkt++;
            Pruefe.Gleich(0, gesenkt, "nie gesenkt");
            Pruefe.Wahr(hang.Hoehen.Wert(-10, 20) != vorher.Wert(-10, 20) || hang.Hoehen.Wert(-30, 40) != vorher.Wert(-30, 40) || hang.Geschichte.KannRueckgaengig,
                "etwas wurde gehoben");
        }),
    };
}
