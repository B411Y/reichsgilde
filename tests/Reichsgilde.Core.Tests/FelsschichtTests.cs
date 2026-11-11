using System.Text.Json;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Speichern;

/// <summary>
/// Felspinsel P4a: der Hang als eigene Schicht („hang“ am Stück, Bauen/Felsschicht.cs, Kartenentwurf.Felsschicht.cs) – folgt dem Stück beim
/// Verschieben, Drehen und Löschen, Grundhöhe bleibt gespeichert, „Hebung ablösen“, schwebende Stücke. Jeder Test mit Gegenprobe (fester Hang).
/// </summary>
static class FelsschichtTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein Buckel östlich der Mitte (unebener Boden).</summary>
    static Kartenentwurf Huegel()
    {
        var e = Leer();
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 12, Kante: 0.5), new[] { O(20, 0) });
        return e;
    }

    /// <summary>Ein gleichmäßiger Hang (je Rasterpunkt nach Süden 20 cm tiefer), aus einer Höhenkarte: genau eben geneigt.</summary>
    static Kartenentwurf Schraeg()
    {
        var e = Leer();
        const int n = 120;
        var werte = new ushort[n * n];
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++) werte[j * n + i] = (ushort)(3000 - 20 * j);
        e.HoehenkarteEinlesen(werte, new Hoehenkartenlage(n, n, 200, -12000, -12000, 0, 65535, StufeCm: 1));
        return e;
    }

    /// <summary>Wand mit Hang: als Schicht oder (Gegenprobe) fest ins Gelände.</summary>
    static Felspinsel Wand(bool schicht, string form = "wand_gross_1") => new(Formen: new[] { form }, Streuung: 0, Boden: false, HangFormen: true, HangSchicht: schicht);

    static string R(Hoehenraster r) => r.AlsJson()?.ToJsonString() ?? "";

    /// <summary>Größter Unterschied (cm) zweier Raster im Rechteck.</summary>
    static int Abstand(Hoehenraster a, Hoehenraster b, double x0 = -200, double z0 = -150, double x1 = 200, double z1 = 150)
    {
        var (i0, j0, i1, j1) = Hoehenraster.Punkte(new Kartenrechteck(x0, z0, x1, z1));
        var aus = 0;
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++) aus = Math.Max(aus, Math.Abs(a.Wert(i, j) - b.Wert(i, j)));
        return aus;
    }

    /// <summary>
    /// Ein fester Ablauf für den Gleichheitstest (Zeile „felshang …“): Große Wand als Schicht, eine Wand fest gestempelt und abgelöst, verschieben, drehen,
    /// Hang aus, Gelände darunter pinseln, löschen, Rückgängig und Wiederholen – Kartendatei und wirksame Höhe am Ende.
    /// </summary>
    internal static string Ablauf()
    {
        var e = Huegel();
        e.FelsStempeln(new Felspinsel(Gruppe: "wandgross", Boden: true, HangFormen: true, HangSchicht: true), O(-40, 60), drehung: 30, startwert: 8);
        var fest = e.FelsStempeln(Wand(false, "wand_gross_3"), O(80, -40), drehung: 170, startwert: 2);
        e.FelsHebungAbloesen(fest);
        var alle = e.Felsen.ToList();
        e.FelsVerschieben(alle[0], alle[0].X + 13.3, alle[0].Z - 7.1);
        e.FelsTransformieren(alle.Skip(1).Take(3), new Felsaenderung(Drehachse: Felsachse.Y, Winkel: 41.5));
        e.FelsHangSetzen(new[] { alle[2] }, an: false);
        e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 18, Staerke: 3, Kante: 0.5), new[] { O(-40, 40), O(-20, 50) });
        e.FelsenLoeschen(new[] { alle[1] });
        e.FelsKopieren(new[] { alle[0] }, dx: 25, dz: 10);
        e.Rueckgaengig();
        e.Rueckgaengig();
        e.Wiederholen();
        return e.ZuJson() + "\n" + R(e.Hoehen);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P4a: Ohne Stück mit „hang“ gibt es nur ein Raster (Entwurf und Stadtkarte), das Feld fehlt in Datei und Spielstand; mit „hang“ steht es dort", () =>
        {
            var e = Huegel();
            e.FelsStempeln(Wand(false), O(-50, 0), drehung: 0, startwert: 1);
            Pruefe.Wahr(!e.HatHangschicht && ReferenceEquals(e.Hoehen, e.Grundhoehen), "fester Hang: ein Raster");
            var text = e.ZuJson();
            Pruefe.Wahr(!text.Contains("\"hang\""), "kein Feld hang");
            var karte = Stadtkarte.AusJson(text, Katalog.Value);
            Pruefe.Wahr(ReferenceEquals(karte.Hoehen, karte.Grundhoehen), "Stadtkarte: ein Raster");
            var stand = JsonSerializer.Serialize(karte, Speicherung.Optionen);
            Pruefe.Wahr(stand.Contains("\"Hoehen\"") && !stand.Contains("\"Hang\"") && !stand.Contains("Grundhoehen"), "Spielstand wie bisher");
            Pruefe.Gleich(text, Kartenentwurf.AusJson(text, Katalog.Value).ZuJson(), "wieder geschrieben");
            // Gegenprobe: mit Schicht
            var s = Huegel();
            s.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1);
            Pruefe.Wahr(s.HatHangschicht && !ReferenceEquals(s.Hoehen, s.Grundhoehen) && s.HangschichtPunkte > 50, $"Schicht: {s.HangschichtPunkte} Punkte");
            var mit = s.ZuJson();
            Pruefe.Wahr(mit.Contains("\"hang\": true"), "hang steht in der Datei:\n" + mit);
            var k2 = Stadtkarte.AusJson(mit, Katalog.Value);
            Pruefe.Wahr(!ReferenceEquals(k2.Hoehen, k2.Grundhoehen) && k2.Felsen!.Stuecke[0].Hang, "Stadtkarte: zwei Raster");
            Pruefe.Wahr(JsonSerializer.Serialize(k2, Speicherung.Optionen).Contains("\"Hang\": true"), "Spielstand: Hang steht drin");
        }),

        ("Fels P4a: Die Schicht hebt das Gelände wie der feste Hang, die Grundhöhe bleibt; Löschen stellt sie exakt her (Gegenprobe: der feste Hang bleibt als Wall)", () =>
        {
            var e = Huegel();
            var grund = R(e.Hoehen);
            var original = e.Hoehen.Kopie();
            var w = e.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1)[0];
            Pruefe.Wahr(w.Hang, "das Stück trägt den Hang");
            Pruefe.Gleich(grund, R(e.Grundhoehen), "Grundhöhe unverändert");
            var hinten = e.Hoehen.Hoehe(-50, -8) - e.Grundhoehen.Hoehe(-50, -8);
            Pruefe.Wahr(hinten > 5, $"hinter der Wand gehoben: {hinten:0.00} m");
            // wie der feste Hang (dieselbe Rechnung): auf wenige cm gleich
            var f = Huegel();
            f.FelsStempeln(Wand(false), O(-50, 0), drehung: 0, startwert: 1);
            Pruefe.Wahr(Abstand(e.Hoehen, f.Hoehen) <= 2, $"wie fest gestempelt, Abstand {Abstand(e.Hoehen, f.Hoehen)} cm");
            e.FelsenLoeschen(new[] { w });
            Pruefe.Gleich(0, Abstand(e.Hoehen, original), "gelöscht: Grundhöhe exakt");
            Pruefe.Gleich(0, e.HangschichtPunkte, "Schicht leer");
            e.Rueckgaengig();
            Pruefe.Wahr(e.Hoehen.Hoehe(-50, -8) - e.Grundhoehen.Hoehe(-50, -8) > 5, "Rückgängig: wieder gehoben");
            // Gegenprobe
            f.FelsenLoeschen(f.Felsen.ToList());
            Pruefe.Wahr(Abstand(f.Hoehen, original) > 500, "fester Hang bleibt nach dem Löschen");
        }),

        ("Fels P4a: Verschieben und Drehen nehmen den Hang mit, am alten Platz bleibt nichts; ein Rückgängig-Schritt ohne Kacheln setzt Stück und Gelände zurück", () =>
        {
            var e = Huegel();
            var original = e.Hoehen.Kopie();
            var w = e.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1)[0];
            var vorher = R(e.Hoehen);
            e.FelsVerschieben(w, 60, 40);
            Pruefe.Wahr(e.Geschichte.LetzterOhneWiederholen is TeilAendern, "der Schritt ist nur das Stück (keine Kacheln in der Geschichte)");
            Pruefe.Wahr(Math.Abs(e.Hoehen.Hoehe(-50, -8) - original.Hoehe(-50, -8)) < 0.005, "alter Platz frei");
            Pruefe.Wahr(e.Hoehen.Hoehe(60, 32) - e.Grundhoehen.Hoehe(60, 32) > 5, "neuer Platz gehoben");
            Pruefe.Gleich(0, Abstand(e.Hoehen, original, -200, -150, 20, 150), "westlich nichts mehr gehoben");
            e.FelsDrehen(w, 180);
            Pruefe.Wahr(e.Hoehen.Hoehe(60, 48) - e.Grundhoehen.Hoehe(60, 48) > 5 && e.Hoehen.Hoehe(60, 32) - e.Grundhoehen.Hoehe(60, 32) < 0.5, "gedreht: Hang auf der anderen Seite");
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, R(e.Hoehen), "zwei Schritte zurück: Gelände wie vorher");
            Pruefe.Gleich((-50.0, 0.0), (w.X, w.Z), "Stück wieder am Platz");
            // Gegenprobe: der feste Hang bleibt am alten Platz
            var f = Huegel();
            var fw = f.FelsStempeln(Wand(false), O(-50, 0), drehung: 0, startwert: 1)[0];
            f.FelsVerschieben(fw, 60, 40);
            Pruefe.Wahr(f.Hoehen.Hoehe(-50, -8) - original.Hoehe(-50, -8) > 5, "fester Hang bleibt stehen");
            Pruefe.Wahr(f.Hoehen.Hoehe(60, 32) - original.Hoehe(60, 32) < 0.5, "und kommt nicht mit");
        }),

        ("Fels P4a: Mehrere Stücke wirken als Höchstwert – die Reihenfolge der Stücke ist gleichgültig; eine Wand allein hebt weniger (Gegenprobe)", () =>
        {
            // dieselben Stücke in umgekehrter Reihenfolge in der Datei (gestempelt wird auf das sichtbare Gelände: dort zählt die Reihenfolge des Stempelns)
            var e = Huegel();
            e.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1);
            e.FelsStempeln(Wand(true), O(-38, -6), drehung: 25, startwert: 1);
            var text = e.ZuJson();
            var knoten = System.Text.Json.Nodes.JsonNode.Parse(text)!;
            var stuecke = knoten["felsen"]!["stuecke"]!.AsArray();
            var umgekehrt = new System.Text.Json.Nodes.JsonArray(stuecke.Reverse().Select(s => s!.DeepClone()).ToArray());
            knoten["felsen"]!["stuecke"] = umgekehrt;
            var u = Kartenentwurf.AusJson(knoten.ToJsonString(), Katalog.Value);
            Pruefe.Gleich(e.Felsen[0].X, u.Felsen[1].X, "umgekehrt geöffnet");
            Pruefe.Gleich(R(e.Hoehen), R(u.Hoehen), "gleiche wirksame Höhe");
            Pruefe.Gleich(R(e.Hoehen), R(Stadtkarte.AusJson(knoten.ToJsonString(), Katalog.Value).Hoehen!), "Stadtkarte umgekehrt: gleiche wirksame Höhe");
            var allein = Huegel();
            allein.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1);
            Pruefe.Wahr(R(allein.Hoehen) != R(e.Hoehen), "zwei Wände heben mehr als eine");
        }),

        ("Fels P4a: Hebt die Schicht die Mitte einer Wand, rechnet ihr Hang von der wirksamen Höhe – die Gruppe „wandgross“ als Schicht wie fest gestempelt (Gegenprobe: ab Grundhöhe zu tief)", () =>
        {
            var pinsel = new Felspinsel(Gruppe: "wandgross", Boden: false, HangFormen: true, HangSchicht: true);
            var e = Huegel();
            e.FelsStempeln(pinsel, O(-40, 60), drehung: 30, startwert: 8);
            var f = Huegel();
            f.FelsStempeln(pinsel with { HangSchicht = false }, O(-40, 60), drehung: 30, startwert: 8);
            Pruefe.Wahr(Abstand(e.Hoehen, f.Hoehen) <= 3, $"Schicht wie fester Hang, Abstand {Abstand(e.Hoehen, f.Hoehen)} cm");
            for (var k = 0; k < e.Felsen.Count; k++)
                Pruefe.Wahr(Math.Abs(Felsraum.UrsprungY(e.Felsen[k].AlsLage(), e.Hoehen) - Felsraum.UrsprungY(f.Felsen[k].AlsLage(), f.Hoehen)) <= 0.03, $"Stück {k} steht gleich");
            // neu geöffnet: dieselbe Höhe (der Ursprung wird aus Grundhöhe und Versatz wiedergefunden)
            Pruefe.Gleich(R(e.Hoehen), R(Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value).Hoehen), "geöffnet gleich");
            // Gegenprobe: die Mitte mindestens eines Stücks ist gehoben – ab der Grundhöhe gerechnet läge der Hang deutlich zu tief
            var lagen = e.Felsen.Select(x => x.AlsLage()).ToList();
            Pruefe.Wahr(lagen.Any(l => e.Hoehen.Hoehe(l.X, l.Z) - e.Grundhoehen.Hoehe(l.X, l.Z) > 0.5), "eine Mitte gehoben");
            var alt = Felsschicht.Rechnen(e.Grundhoehen, e.Kartengrenzen.Aussen, lagen.Select(l => (l, (double?)Felsraum.UrsprungY(l, e.Grundhoehen))).ToList());
            var tiefer = alt.Count(p => e.Hoehen.Wert(p.Key.I, p.Key.J) - p.Value > 50);
            Pruefe.Wahr(tiefer > 20, $"ab Grundhöhe: {tiefer} Punkte über 0,5 m zu tief");
        }),

        ("Fels P4a: Zwei Wände hintereinander als Schicht – hinter der hinteren steigt das Gelände bis unter ihre Oberkante, wie fest gestempelt", () =>
        {
            Kartenentwurf Zwei(bool schicht)
            {
                var e = Huegel();
                e.FelsStempeln(Wand(schicht), O(-50, 0), drehung: 0, startwert: 1);
                e.FelsStempeln(Wand(schicht, "wand_gross_3"), O(-50, -10), drehung: 0, startwert: 1);
                return e;
            }
            var (s, f) = (Zwei(true), Zwei(false));
            var hinten = s.Felsen[1];
            var l = hinten.AlsLage();
            Pruefe.Wahr(s.Hoehen.Hoehe(l.X, l.Z) - s.Grundhoehen.Hoehe(l.X, l.Z) > 2, "die vordere Wand hebt die Mitte der hinteren");
            var oberkante = Felsraum.UrsprungY(l, s.Hoehen) + Felsraum.Groesse(l).Y;
            var dahinter = s.Hoehen.Hoehe(l.X, l.Z - 8);
            Pruefe.Wahr(dahinter > oberkante - 1.5, $"Gelände hinter der hinteren Wand {dahinter:0.00} m, Oberkante {oberkante:0.00} m");
            Pruefe.Wahr(Math.Abs(dahinter - f.Hoehen.Hoehe(l.X, l.Z - 8)) < 0.1, $"wie fest gestempelt ({f.Hoehen.Hoehe(l.X, l.Z - 8):0.00} m)");
            Pruefe.Wahr(Math.Abs(oberkante - Felsraum.UrsprungY(f.Felsen[1].AlsLage(), f.Hoehen) - Felsraum.Groesse(l).Y) <= 0.03, "die hintere Wand steht gleich");
            // Gegenprobe: ohne die vordere Wand liegt das Gelände dahinter tiefer
            var allein = Huegel();
            allein.FelsStempeln(Wand(true, "wand_gross_3"), O(-50, -10), drehung: 0, startwert: 1);
            Pruefe.Wahr(allein.Hoehen.Hoehe(l.X, l.Z - 8) < dahinter - 2, "allein niedriger");
        }),

        ("Fels P4a: Stempel mit Schicht – der ausgeglichene Versatz bleibt in ±20 m wie beim Verschieben und Streuen (Gegenprobe: ohne Grenze darunter)", () =>
        {
            var e = Leer();
            var pinsel = new Felspinsel(Gruppe: "wandgross", Boden: false, HangFormen: true, HangSchicht: true, Versatzbereich: new Felsbereich(-20, -19.9), AchseY: new Felsbereich(3, 3.01));
            var vorschau = e.FelsStempelVorschau(pinsel, O(0, 0), drehung: 0, startwert: 3);
            var vorher = e.Hoehen.Kopie();
            var neue = e.FelsStempeln(pinsel, O(0, 0), drehung: 0, startwert: 3);
            Pruefe.Wahr(neue.Count > 0 && neue.All(f => Math.Abs(f.Versatz) <= Felsformen.VersatzMax), "alle in ±20 m: " + string.Join(", ", neue.Select(f => f.Versatz)));
            // Gegenprobe: mindestens ein Stück hätte tiefer gemusst (es steht jetzt höher, als die Vorschau es zeigte)
            var gekappt = neue.Where((f, k) => f.Versatz == -Felsformen.VersatzMax && Felsraum.UrsprungY(f.AlsLage(), e.Hoehen) > Felsraum.UrsprungY(vorschau[k], vorher) + 0.05).Count();
            Pruefe.Wahr(gekappt > 0, $"gekappt: {gekappt}");
        }),

        ("Fels P4a: Speichern und Laden – Datei gleich, Entwurf, Stadtkarte und Spielstand rechnen dieselbe wirksame Höhe; gespeichert ist die Grundhöhe", () =>
        {
            var e = Huegel();
            e.FelsStempeln(new Felspinsel(Gruppe: "wandgross", Boden: true, HangFormen: true, HangSchicht: true), O(-40, 60), drehung: 30, startwert: 8);
            var text = e.ZuJson();
            var zwei = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, zwei.ZuJson(), "wieder geschrieben");
            Pruefe.Gleich(R(e.Hoehen), R(zwei.Hoehen), "Entwurf geöffnet: dieselbe wirksame Höhe");
            Pruefe.Gleich(R(e.Grundhoehen), R(zwei.Grundhoehen), "dieselbe Grundhöhe");
            var karte = Stadtkarte.AusJson(text, Katalog.Value);
            Pruefe.Gleich(R(e.Hoehen), R(karte.Hoehen!), "Stadtkarte: dieselbe wirksame Höhe");
            Pruefe.Gleich(R(e.Grundhoehen), R(karte.Grundhoehen!), "Stadtkarte: Grundhöhe");
            var stand = JsonSerializer.Serialize(karte, Speicherung.Optionen);
            var geladen = JsonSerializer.Deserialize<Stadtkarte>(stand, Speicherung.Optionen)!;
            Pruefe.Gleich(R(karte.Hoehen!), R(geladen.Hoehen!), "Spielstand: dieselbe wirksame Höhe");
            Pruefe.Gleich(stand, JsonSerializer.Serialize(geladen, Speicherung.Optionen), "Spielstand wieder geschrieben");
            // Gegenprobe: die Grundhöhe ist nicht die wirksame
            Pruefe.Wahr(R(karte.Grundhoehen!) != R(karte.Hoehen!), "Grundhöhe ohne Hang");
            Pruefe.Wahr(!stand.Contains(R(karte.Hoehen!)) && stand.Contains(karte.Grundhoehen!.AlsJson()!["kacheln"]![0]!["daten"]!.GetValue<string>()), "im Spielstand steht die Grundhöhe");
        }),

        ("Fels P4a: Geländepinsel formen die Grundhöhe, der Hang bleibt darüber; Rückgängig setzt beides zurück; am Fuß der Wand rechnet der Hang neu", () =>
        {
            var e = Huegel();
            var w = e.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1)[0];
            var (wirksam, grund) = (R(e.Hoehen), R(e.Grundhoehen));
            // weit vor der Wand: wirksam = Grund + 2 m
            var vorn = e.Hoehen.Hoehe(-50, 40);
            e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 6, Staerke: 1, Kante: 0, Zielhoehe: vorn + 2), new[] { O(-50, 40) });
            Pruefe.Wahr(Math.Abs(e.Hoehen.Hoehe(-50, 40) - (vorn + 2)) < 0.02 && Math.Abs(e.Grundhoehen.Hoehe(-50, 40) - (vorn + 2)) < 0.02, "vorn: Grund und wirksam gleich");
            // im Hang (voll gehoben): ein Meter mehr Grund ändert die wirksame Höhe nicht
            var oben = e.Hoehen.Hoehe(-50, -8);
            e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 3, Staerke: 1, Kante: 0, Zielhoehe: e.Grundhoehen.Hoehe(-50, -8) + 1), new[] { O(-50, -8) });
            Pruefe.Wahr(Math.Abs(e.Hoehen.Hoehe(-50, -8) - oben) < 0.02, $"im Hang bleibt die wirksame Höhe ({oben:0.00} → {e.Hoehen.Hoehe(-50, -8):0.00})");
            // am Fuß (Mitte des Stücks) gehoben: der ganze Hang steigt mit
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 4, Staerke: 2, Kante: 0), new[] { O(-50, 0) });
            Pruefe.Wahr(e.Hoehen.Hoehe(-50, -8) > oben + 0.5, "Hang neu gerechnet");
            e.Rueckgaengig();
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich((wirksam, grund), (R(e.Hoehen), R(e.Grundhoehen)), "drei Schritte zurück");
            // Gegenprobe: Hang aus – dann wirkt das Pinseln im Hang
            e.FelsHangSetzen(new[] { w }, an: false);
            Pruefe.Gleich(R(e.Grundhoehen), R(e.Hoehen), "ohne Hang: wirksam = Grund");
        }),

        ("Fels P4a: Hang an und aus ist ein Schritt, das Stück bleibt, wo es steht; nur Formen mit Hang; aus stellt die Grundhöhe her", () =>
        {
            var e = Huegel();
            var original = R(e.Hoehen);
            var w = e.FelsStempeln(Wand(false) with { HangFormen = false }, O(-50, 0), drehung: 0, startwert: 1)[0];
            var block = e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(50, 0), drehung: 0, startwert: 1)[0];
            var oben = Felsraum.KontaktY(w.AlsLage(), e.Hoehen);
            var schritte = e.Geschichte.Schritte;
            Pruefe.Wahr(e.FelsHangSetzen(new[] { w, block }, an: true), "an");
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Wahr(w.Hang && !block.Hang, "nur die Wand");
            Pruefe.Wahr(Math.Abs(Felsraum.KontaktY(w.AlsLage(), e.Hoehen) - oben) <= 0.03, "das Stück steht noch da");
            Pruefe.Wahr(e.Hoehen.Hoehe(-50, -8) - e.Grundhoehen.Hoehe(-50, -8) > 5, "gehoben");
            Pruefe.Wahr(!e.FelsHangSetzen(new[] { w }, an: true), "noch einmal an: nichts");
            Pruefe.Wahr(e.FelsHangSetzen(new[] { w }, an: false), "aus");
            Pruefe.Gleich(original, R(e.Hoehen), "aus: Grundhöhe");
            Pruefe.Wahr(Math.Abs(Felsraum.KontaktY(w.AlsLage(), e.Hoehen) - oben) <= 0.03, "das Stück steht noch da");
            e.Rueckgaengig();
            Pruefe.Wahr(w.Hang && e.Hoehen.Hoehe(-50, -8) - e.Grundhoehen.Hoehe(-50, -8) > 5, "Rückgängig: wieder an");
        }),

        ("Fels P4a: Hebung ablösen nimmt den festen Hang aus der Grundhöhe (gleichmäßiger Hang genau zurück), das Gelände sieht aus wie vorher; ohne Ablösen entsteht ein Buckel", () =>
        {
            var e = Schraeg();
            var original = e.Hoehen.Kopie();
            var w = e.FelsStempeln(Wand(false), O(-20, 10), drehung: 0, startwert: 1)[0];
            var fest = e.Hoehen.Kopie();
            Pruefe.Wahr(Abstand(fest, original) > 500, "fest gehoben");
            var vorschau = e.FelsHebungVorschau(new[] { w });
            Pruefe.Wahr(vorschau is { Punkte: > 50, Tiefste: > 5 }, $"Vorschau: {vorschau}");
            Pruefe.Gleich(R(fest), R(e.Hoehen), "die Vorschau ändert nichts");
            var oben = Felsraum.KontaktY(w.AlsLage(), e.Hoehen);
            Pruefe.Wahr(e.FelsHebungAbloesen(new[] { w }), "abgelöst");
            Pruefe.Wahr(w.Hang, "das Stück trägt jetzt den Hang");
            Pruefe.Wahr(Abstand(e.Grundhoehen, original) <= 1, $"Grundhöhe wie vor dem Stempel (Abstand {Abstand(e.Grundhoehen, original)} cm)");
            Pruefe.Wahr(Abstand(e.Hoehen, fest) <= 3, $"das Gelände sieht aus wie vorher (Abstand {Abstand(e.Hoehen, fest)} cm)");
            Pruefe.Wahr(Math.Abs(Felsraum.KontaktY(w.AlsLage(), e.Hoehen) - oben) <= 0.03, "das Stück steht noch da");
            e.FelsVerschieben(w, 60, 10);
            Pruefe.Wahr(Abstand(e.Hoehen, original, -200, -150, 25, 150) <= 1, "verschoben: am alten Platz der Hang wie vor dem Stempel");
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(R(fest), R(e.Hoehen), "Rückgängig: der feste Hang wieder");
            // Gegenprobe: nur „hang“ an, ohne Ablösen – der Hang liegt doppelt (Buckel an den Rändern), und verschoben bleibt der Wall
            Pruefe.Wahr(e.FelsHangSetzen(new[] { w }, an: true), "an");
            Pruefe.Wahr(Abstand(e.Hoehen, fest) > 20, $"Buckel ohne Ablösen ({Abstand(e.Hoehen, fest)} cm)");
            e.FelsVerschieben(w, 60, 10);
            Pruefe.Wahr(Abstand(e.Hoehen, original, -200, -150, 25, 150) > 500, "ohne Ablösen bleibt der Wall");
        }),

        ("Fels P4a: Schwebende Stücke – nach dem Senken des Geländes gefunden, nach dem Aufsetzen nicht mehr; vorher keins", () =>
        {
            var e = Huegel();
            var b = e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false, Streuung: 0), O(20, 0), drehung: 0, startwert: 1)[0];
            var k = e.FelsStempeln(new Felspinsel(Formen: new[] { "kuppe" }, Boden: false, Streuung: 0), O(-80, 60), drehung: 0, startwert: 1)[0];
            Pruefe.Gleich(0, e.FelsSchwebende().Count, "vorher keins");
            // gleichmäßig gesenkt sinkt das Stück mit (der Versatz zählt ab dem Boden unter der Mitte)
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 10, Staerke: 3, Kante: 0), new[] { O(20, 0) });
            Pruefe.Gleich(0, e.FelsSchwebende().Count, "mit dem Boden gesunken");
            // ein echter Geländestrich unter dem Rand (die Mitte bleibt): der Boden fällt unter dem Stück weg, es schwebt
            var g = Felsraum.Groesse(b.AlsLage());
            var (hx, hz) = (g.X / 2, g.Z / 2);
            Pruefe.Wahr(Math.Min(hx, hz) > 1, $"Block groß genug ({g.X:0.0} × {g.Z:0.0} m)");
            var mitte = e.Hoehen.Hoehe(b.X, b.Z);
            var rand = new[] { O(b.X - hx, b.Z - hz), O(b.X + hx, b.Z - hz), O(b.X + hx, b.Z + hz), O(b.X - hx, b.Z + hz), O(b.X - hx, b.Z - hz) };
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 0.8, Staerke: 2, Kante: 0), rand);
            Pruefe.Wahr(Math.Abs(e.Hoehen.Hoehe(b.X, b.Z) - mitte) < 0.01 && e.Hoehen.Hoehe(b.X - hx, b.Z - hz) < mitte - 0.5, "unter dem Rand gesenkt, die Mitte nicht");
            var weg = e.FelsSchwebende();
            Pruefe.Wahr(weg.Count == 1 && ReferenceEquals(weg[0], b), $"nach dem Geländestrich schwebt der Block ({weg.Count})");
            e.Rueckgaengig();
            Pruefe.Gleich(0, e.FelsSchwebende().Count, "Strich zurückgenommen: keins");
            // ein Stück, das höher gesetzt wurde (oder dessen Boden unter dem Rand wegfiel), schwebt
            e.FelsTransformieren(new[] { b }, new Felswerte(Versatz: 1.5));
            var schwebend = e.FelsSchwebende();
            Pruefe.Wahr(schwebend.Count == 1 && ReferenceEquals(schwebend[0], b), $"der Block schwebt ({schwebend.Count})");
            Pruefe.Gleich(0, e.FelsSchwebende(toleranz: 2).Count, "unter der Toleranz nicht");
            e.FelsEinrasten(schwebend);
            Pruefe.Gleich(0, e.FelsSchwebende().Count, "aufgesetzt");
            Pruefe.Wahr(k.Versatz == 0, "die Kuppe blieb");
        }),

        ("Fels P4a: Die Hang-Vorschau zeigt genau das Gelände nach dem Verschieben, ohne etwas zu ändern; ohne Hang leer", () =>
        {
            var e = Huegel();
            var w = e.FelsStempeln(Wand(true), O(-50, 0), drehung: 0, startwert: 1)[0];
            var vorher = R(e.Hoehen);
            var raster = e.Hoehen.Kopie();
            var lage = e.FelsVorschau(new[] { w }, new Felsaenderung(DX: 31, DZ: 12));
            var punkte = e.FelsHangVorschau(lage);
            Pruefe.Wahr(punkte.Count > 100, $"{punkte.Count} Punkte ändern sich");
            Pruefe.Gleich(vorher, R(e.Hoehen), "nichts geändert");
            e.FelsTransformieren(new[] { w }, new Felsaenderung(DX: 31, DZ: 12));
            foreach (var (i, j, h) in punkte) Pruefe.Gleich(h, e.Hoehen.Wert(i, j) / 100.0, $"Punkt {i},{j}");
            // alle übrigen Punkte bleiben, wie sie waren
            var genannt = punkte.Select(p => (p.I, p.J)).ToHashSet();
            var (pi0, pj0, pi1, pj1) = Hoehenraster.Punkte(new Kartenrechteck(-200, -150, 200, 150));
            var anders = 0;
            for (var j = pj0; j <= pj1; j++)
                for (var i = pi0; i <= pi1; i++)
                    if (!genannt.Contains((i, j)) && e.Hoehen.Wert(i, j) != raster.Wert(i, j)) anders++;
            Pruefe.Gleich(0, anders, "nicht genannte Punkte unverändert");
            Pruefe.Wahr(Kartenentwurf.FelsHangBereich(w) is { } r && r.Enthaelt(w.X, w.Z - 8), "Bereich des Hangs");
            // Gegenprobe: ein Block ohne Hang
            var l = Leer();
            var block = l.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(0, 0), startwert: 1)[0];
            Pruefe.Gleich(0, l.FelsHangVorschau(l.FelsVorschau(new[] { block }, new Felsaenderung(DX: 5))).Count, "ohne Hang leer");
            Pruefe.Wahr(Kartenentwurf.FelsHangBereich(block) is null, "Block: kein Bereich");
        }),

        ("Fels P4a: Der Ablauf des Gleichheitstests ergibt zweimal dieselbe Datei; Schicht, Ablösen und Löschen haben gewirkt", () =>
        {
            var a = Ablauf();
            Pruefe.Gleich(a, Ablauf(), "zweiter Lauf");
            Pruefe.Wahr(a.Contains("\"hang\": true"), "Stücke mit Hang");
            var e = Kartenentwurf.AusJson(a[..a.LastIndexOf('\n')], Katalog.Value);
            Pruefe.Gleich(a[(a.LastIndexOf('\n') + 1)..], R(e.Hoehen), "geöffnet: dieselbe wirksame Höhe");
        }),
    };
}
