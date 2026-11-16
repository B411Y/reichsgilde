using System.Buffers.Binary;
using System.Text.Json;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Tests;

/// <summary>
/// Das Modell der Klappbrücke (<c>game/assets/modelle/bruecke/klappbruecke.glb</c>) und sein Einbau: Knoten, Animationen, Maße in den glTF-extras (aus denen die
/// Stadtansicht liest, keine festen Zahlen im Darstellungscode) und Texturen; dazu die Aufteilung der Felder aus den Pfeilern des Wasserplans (Köln).
/// </summary>
public static class KlappbrueckeModellTests
{
    static string Assets => Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "..", "assets");

    static JsonDocument Glb()
    {
        var b = File.ReadAllBytes(Path.Combine(Assets, "modelle", "bruecke", "klappbruecke.glb"));
        var laenge = BitConverter.ToInt32(b, 12);
        return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(b, 20, laenge));
    }

    /// <summary>Fehler des Modells (leer = in Ordnung): Knoten, Animationen, Maße.</summary>
    static List<string> Pruefen(JsonDocument glb)
    {
        var fehler = new List<string>();
        var knoten = glb.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var namen = knoten.Select(k => k.GetProperty("name").GetString()!).ToHashSet();
        foreach (var n in new[] { "Klappe_L", "Klappe_R", "Torhaus_L", "Torhaus_R", "Zugwerk_L", "Zugwerk_R", "Pfeiler_L", "Pfeiler_R", "Bogenfeld", "Zwischenpfeiler", "Ende",
                                  "Anker_Durchfahrt", "Anker_Schranke_L", "Anker_Warte_R", "Schranke_L", "Schranke_R", "Schrankenpfosten_L", "Schrankenpfosten_R", "Kette_L_N", "Gegenkette_R_S", "Gegengewicht_L_S" })
            if (!namen.Contains(n)) fehler.Add("Knoten fehlt: " + n);
        var anim = glb.RootElement.TryGetProperty("animations", out var a) ? a.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList() : new List<string?>();
        foreach (var n in new[] { "Oeffnen", "Schliessen", "Schranke_zu", "Schranke_auf" })
            if (!anim.Contains(n)) fehler.Add("Animation fehlt: " + n);
        var anker = knoten.FirstOrDefault(k => k.GetProperty("name").GetString() == "Anker_Durchfahrt");
        if (anker.ValueKind == JsonValueKind.Undefined || !anker.TryGetProperty("extras", out var e)) { fehler.Add("extras am Anker_Durchfahrt fehlen"); return fehler; }
        double Wert(string k) => e.TryGetProperty(k, out var v) ? v.GetDouble() : double.NaN;
        void Soll(string k, double soll) { if (!(Math.Abs(Wert(k) - soll) < 1e-6)) fehler.Add($"extras.{k} = {Wert(k)}, soll {soll}"); }
        Soll("durchfahrt", Wasserplan.KlappDurchfahrt);       // wie der Wasserplan des Kerns
        Soll("mittelteil", Wasserplan.KlappMittelteil);
        Soll("feld", Wasserplan.SteinBogen);
        Soll("zwischenpfeiler", Wasserplan.SteinPfeiler);
        Soll("ufer_tief", Wasserplan.SteinDeck);
        if (!(Wert("rampe") > 0)) fehler.Add("extras.rampe fehlt");
        if (!(Wert("schranke_auf_winkel") > 45) || !(Wert("schranke_dauer") > 0)) fehler.Add("extras zur Schranke fehlen");
        if (!(Wert("deck") > Wert("ufer_tief"))) fehler.Add("Fahrbahn nicht ueber dem Ufer");
        return fehler;
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Klappbrücke Modell: Knoten, Animationen Oeffnen/Schliessen und Maße in den extras stimmen mit dem Wasserplan überein; Gegenprobe ohne Knoten und mit falschem Maß", () =>
        {
            using var glb = Glb();
            var fehler = Pruefen(glb);
            Pruefe.Wahr(fehler.Count == 0, "Modell: " + string.Join("; ", fehler));
            var text = glb.RootElement.GetRawText().Replace("\"name\":\"Klappe_L\"", "\"name\":\"Klappe_X\"").Replace("\"durchfahrt\":13", "\"durchfahrt\":12");
            using var kaputt = JsonDocument.Parse(text);
            var f2 = Pruefen(kaputt);
            Pruefe.Wahr(f2.Any(x => x.Contains("Klappe_L")), "fehlender Knoten wird gemeldet");
            Pruefe.Wahr(f2.Any(x => x.Contains("durchfahrt")), "abweichende Durchfahrt wird gemeldet");
        });
        yield return ("Klappbrücke Texturen: Trim-Sheet (Albedo, Normal, ORM) und Verdeckungsatlas liegen da, 2048 × 2048", () =>
        {
            foreach (var n in new[] { "bruecke_ebenen_albedo.png", "bruecke_ebenen_normal.png", "bruecke_ebenen_orm.png", "klappbruecke_ao_atlas.png" })
            {
                var b = File.ReadAllBytes(Path.Combine(Assets, "texturen", "bruecke", n));
                Pruefe.Wahr(b.Length > 24 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G', n + " ist kein PNG");
                var (breite, hoehe) = (BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20)));
                Pruefe.Wahr(breite == 2048 && hoehe == 2048, $"{n}: {breite} × {hoehe}");
            }
        });
        yield return ("Klappbrücke Einbau: die Felder der Stadtansicht stimmen mit den Pfeilern des Wasserplans überein (Köln: vier Felder, Zwischenpfeiler an Pfeilern des Plans); andere Brücken ohne Durchfahrt", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var ohne = karte.Wasserplan.Bruecken.Single(b => b.Karren && b.GewaesserName == "Rhein");
            Pruefe.Gleich(0.0, ohne.Durchfahrt);            // Karte ohne gesetzte Klappbrücke (Stadtwirtschaft setzt sie aus den Daten): keine Durchfahrt
            var klapp = new Wasserplan(karte.Gelaendeform,
                karte.Wege.Select(w => new Planweg(w.Name, w.Linie.Count >= 2 ? w.Verlauf.Select(Kartenort.Aus).ToList() : new List<Kartenort>(), w.BreiteWirksam, w.IstKarrenstrasse)).ToList(),
                karte.Uebergaenge, "Rhein");
            var k = klapp.Bruecken.Single(b => b.Karren && b.GewaesserName == "Rhein");
            Pruefe.Gleich(13.0, k.Durchfahrt);
            var (dx, dz) = (k.UferB.X - k.UferA.X, k.UferB.Z - k.UferA.Z);
            var l = Math.Sqrt(dx * dx + dz * dz);
            var stellen = k.Pfeiler.Select(p => ((p.Ort.X - k.UferA.X) * dx + (p.Ort.Z - k.UferA.Z) * dz) / l).OrderBy(s => s).ToList();
            var lage = Klappbrueckenlage.Aus(l, stellen, k.Durchfahrt, (Wasserplan.KlappMittelteil - Wasserplan.KlappDurchfahrt) / 2, Wasserplan.SteinPfeiler);
            Pruefe.Wahr(lage is not null, "die Pfeiler des Wasserplans ergeben eine Aufteilung");
            Pruefe.Gleich(4, lage!.Felder.Count);
            Pruefe.Gleich(2, lage.Zwischenpfeiler.Count);
            Pruefe.Ungefaehr(l / 2, lage.Mitte, 1e-6);
            foreach (var z in lage.Zwischenpfeiler) Pruefe.Wahr(stellen.Any(s => Math.Abs(s - z) < 1e-6), $"Zwischenpfeiler {z:0.##} m ist ein Pfeiler des Wasserplans");
            Pruefe.Wahr(lage.Felder.All(f => Math.Abs(f.Laenge - k.Feldlaenge) < 1e-6), "Felder so lang wie die Feldlaenge des Wasserplans");
            Pruefe.Wahr(klapp.Bruecken.Where(b => b.GewaesserName != "Rhein").All(b => b.Durchfahrt == 0), "andere Brücken ohne Durchfahrt");
        });
        yield return ("Klappbrücke Zierschiff: Das Zierschiff fährt nach seinem Plan genau dann unter der Brücke durch, wenn sie offen ist (Köln, 60 Fenster); Gegenprobe: Schiffe von der anderen Seite brauchen keine Öffnung", () =>
        {
            var sim = KlappbrueckeTests.Partie(Stadtkarte.AusDatei(Datenpfad.Karte("koeln")), KlappbrueckeTests.DatenMit(_ => { }));
            var w = sim.Wirtschaft!;
            var kb = w.Klappbruecke;
            var hafen = w.Schiffahrt.Haefen[0];
            var q = kb.Bruecken[0];
            var mitte = (X: (q.UferA.X + q.UferB.X) / 2, Z: (q.UferA.Z + q.UferB.Z) / 2);
            // Abstand der Brücke vom Kai entlang der Route (nächster Routenpunkt) und wie nah die Route der Brücke kommt
            (double Strecke, double Abstand) Lage(bool flussauf)
            {
                var route = w.Schiffahrt.Route(hafen.Kennung, flussauf);
                var (best, abstand, lauf) = (0.0, double.MaxValue, 0.0);
                for (var i = 0; i + 1 < route.Count; i++)
                {
                    var (ax, az, bx, bz) = (route[i].X, route[i].Z, route[i + 1].X, route[i + 1].Z);
                    var l = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    var t = l > 0 ? Math.Clamp(((mitte.X - ax) * (bx - ax) + (mitte.Z - az) * (bz - az)) / l, 0, l) : 0;
                    var d = Math.Sqrt(Math.Pow(ax + (bx - ax) / l * t - mitte.X, 2) + Math.Pow(az + (bz - az) / l * t - mitte.Z, 2));
                    if (d < abstand) (abstand, best) = (d, lauf + t);
                    lauf += l;
                }
                return (best, abstand);
            }
            var (auf, ab) = (Lage(true), Lage(false));
            Pruefe.Wahr(auf.Abstand < 12 ^ ab.Abstand < 12, $"genau eine Route führt unter der Brücke durch (flussauf {auf.Abstand:0.#} m, flussab {ab.Abstand:0.#} m)");
            var brueckenSeite = auf.Abstand < 12;             // true: die Brücke liegt auf der Flussauf-Route
            var dB = brueckenSeite ? auf.Strecke : ab.Strecke;
            var laenge = Math.Max(w.Schiffahrt.RoutenLaenge(hafen.Kennung, true), w.Schiffahrt.RoutenLaenge(hafen.Kennung, false));
            var fahrt = Zierfahrplan.FahrtMinuten(laenge);
            var laengeRoute = w.Schiffahrt.RoutenLaenge(hafen.Kennung, brueckenSeite);
            var gepruefte = 0;
            for (long f = 0; f < 60; f++)
            {
                var (ankunft, liegen, vonAuf, nachAuf) = Zierfahrplan.Streuung(f);
                var anfang = Zierfahrplan.Anfang(f);
                var an = anfang + ankunft;
                var ablegen = an + liegen;
                var offen = kb.OeffnungenIm(f);
                // Unter der Brücke: bei der Anfahrt kurz vor der Ankunft (Abstand dB vom Kai, fahrt Minuten für die ganze Route), bei der Abfahrt kurz nach dem Ablegen
                var zeitAn = an - fahrt * dB / laengeRoute;
                var zeitAb = ablegen + fahrt * dB / laengeRoute;
                var anBruecke = vonAuf == brueckenSeite;
                var abBruecke = nachAuf == brueckenSeite;
                foreach (var o in offen)
                {
                    if (o.Bis == an) { Pruefe.Wahr(anBruecke, $"Fenster {f}: Öffnung zur Anfahrt, obwohl das Schiff nicht unter der Brücke durchfährt"); Pruefe.Wahr(zeitAn >= o.Von && zeitAn < o.Bis, $"Fenster {f}: Schiff unter der Brücke bei {zeitAn:0.#}, offen {o.Von}–{o.Bis}"); gepruefte++; }
                    else if (o.Von == ablegen) { Pruefe.Wahr(abBruecke, $"Fenster {f}: Öffnung zur Abfahrt ohne Durchfahrt"); Pruefe.Wahr(zeitAb >= o.Von && zeitAb < o.Bis, $"Fenster {f}: Schiff unter der Brücke bei {zeitAb:0.#}, offen {o.Von}–{o.Bis}"); gepruefte++; }
                    else Pruefe.Wahr(false, $"Fenster {f}: Öffnung {o.Von}–{o.Bis} gehört zu keiner Fahrt");
                }
                // Gegenprobe: jede Durchfahrt, für die geöffnet ist, fällt in die Öffnung; Fahrten von der anderen Seite haben keine
                Pruefe.Wahr(offen.Count(o => o.Bis == an) <= (anBruecke ? 1 : 0), $"Fenster {f}: Öffnung für ein Schiff, das nicht unter der Brücke fährt");
            }
            Pruefe.Wahr(gepruefte >= 10, $"genug Durchfahrten geprüft ({gepruefte})");
        });
    }
}
