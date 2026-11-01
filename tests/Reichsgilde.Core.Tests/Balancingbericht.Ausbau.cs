using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Partie;

// ═════════════════════════════════════════════════════════════════════════════
//  Balancing-Bericht, Zusatz „Ausbau“ (Messung, kein Test; Befund H2 der Prüfung von phase5/gehtempo, 1.11.2026)
//
//  Mit --betriebe=datei.csv schreibt der Bericht jeden Betrieb jeder Partie an jedem Jahresende in die Datei (Partie, Haus, Name, Ort, Art,
//  Neubau, seit welchem Jahr, Fahrzeit hin, Karren, Fahrten, Stillstand, Umsatz, Gewinn) und fasst im JSON unter „ausbau“ zusammen, wie es
//  den Werkstätten geht, die ein Haus im Lauf der Partie neu baut: wie viele, wann, wie weit vom Markt (Fahrzeit des Kerns) und was sie
//  bringen – gegen die Startwerkstätten und das Umland. Gruppen: Karte, wer den Spieler führt, Schwierigkeit. Ändert keinen Wert.
// ═════════════════════════════════════════════════════════════════════════════

static partial class Balancingbericht
{
    /// <summary>Grenzen der Fahrzeit (Minuten hin) für die Aufteilung der Neubauten: bis 45, bis 120, bis 240, darüber.</summary>
    private static readonly int[] Fahrzeitstufen = { 45, 120, 240 };

    /// <summary>Schreibt alle Betriebsjahre als CSV (Semikolon, Dezimalpunkt).</summary>
    private static void BetriebeSchreiben(string datei, IEnumerable<Partieergebnis> partien)
    {
        var sb = new StringBuilder("karte;startwert;bezug;schwierigkeit;rotation;beruf;haus;hausname;jahr;betrieb;ort;art;neubau;seitJahr;fahrzeitHin;karren;fahrten;stillstandProzent;umsatz;gewinn\n");
        foreach (var p in partien.Where(p => p.Fehler is null)
                     .OrderBy(p => p.Aufbau.Karte, StringComparer.Ordinal).ThenBy(p => p.Aufbau.Bezug).ThenBy(p => p.Aufbau.Schwierigkeit)
                     .ThenBy(p => p.Aufbau.Startwert).ThenBy(p => p.Aufbau.Rotation).ThenBy(p => p.Aufbau.Beruf, StringComparer.Ordinal))
            foreach (var b in p.Betriebe)
            {
                var a = p.Aufbau;
                var name = b.Haus >= 0 && b.Haus < p.Haeuser.Count ? p.Haeuser[b.Haus].Name : "";
                sb.Append(string.Join(';', a.Karte, a.Startwert, a.Bezug, a.Schwierigkeit, a.Rotation, a.Beruf, b.Haus, name, b.Jahr, b.Name, b.Ort, b.Art,
                    b.Neubau ? 1 : 0, b.SeitJahr, b.FahrzeitHin, b.Karren, b.Fahrten, F(b.StillstandProzent), F(b.Umsatz), F(b.Gewinn))).Append('\n');
            }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(datei))!);
        File.WriteAllText(datei, sb.ToString());
        static string F(double x) => Math.Round(x, 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Zusammenfassung je Gruppe (Karte, Führung des Spielers, Schwierigkeit): Neubauten der Häuser (Anzahl, Baujahr, Fahrzeit hin) und ihre Jahre
    /// danach (Gewinn, Stillstand, Fahrten) gegen Startwerkstätten und Umland; die Neubauten zusätzlich nach Fahrzeitstufen.
    /// </summary>
    private static JsonArray Ausbau(IEnumerable<Partieergebnis> partien)
    {
        var gruppen = new JsonArray();
        foreach (var g in partien.Where(p => p.Fehler is null)
                     .GroupBy(p => (p.Aufbau.Karte, p.Aufbau.Bezug, p.Aufbau.Schwierigkeit))
                     .OrderBy(g => g.Key.Karte, StringComparer.Ordinal).ThenBy(g => g.Key.Bezug).ThenBy(g => g.Key.Schwierigkeit))
        {
            var zeilen = g.SelectMany(p => p.Betriebe.Select(b => (Partie: p, B: b))).ToList();
            // Ein Betrieb einer Partie: seine Zeile im ersten Jahr (Lage, Fahrzeit) und die vollen Jahre danach (ab dem Jahr nach dem ersten Jahresende)
            var betriebe = zeilen.GroupBy(x => (x.Partie, x.B.Name)).Select(x => (Erste: x.OrderBy(y => y.B.Jahr).First(), Jahre: x.Where(y => y.B.Jahr > y.B.SeitJahr).ToList())).ToList();
            JsonObject Jahreswerte(IEnumerable<Betriebsjahr> j)
            {
                var l = j.ToList();
                return new JsonObject
                {
                    ["betriebsjahre"] = l.Count, ["gewinn"] = Kennzahl(l.Select(x => x.Gewinn)), ["stillstandProzent"] = Kennzahl(l.Select(x => x.StillstandProzent), 1),
                    ["fahrten"] = Kennzahl(l.Select(x => (double)x.Fahrten), 1), ["fahrzeitHin"] = Kennzahl(l.Select(x => (double)x.FahrzeitHin)),
                };
            }
            JsonObject Art(Func<Betriebsjahr, bool> welche, bool nurSpieler = false, bool nurKi = false)
            {
                var b = betriebe.Where(x => welche(x.Erste.B) && (!nurSpieler || x.Erste.B.Haus == 0) && (!nurKi || x.Erste.B.Haus > 0)).ToList();
                return new JsonObject
                {
                    ["betriebe"] = b.Count, ["jeHausUndPartie"] = R(b.Count / (double)Math.Max(1, g.Sum(p => nurSpieler ? 1 : nurKi ? p.Haeuser.Count - 1 : p.Haeuser.Count)), 2),
                    ["seitJahr"] = Kennzahl(b.Select(x => (double)x.Erste.B.SeitJahr), 1),
                    ["fahrzeitHinBeimBau"] = Kennzahl(b.Select(x => (double)x.Erste.B.FahrzeitHin)),
                    ["ueber60Minuten"] = b.Count(x => x.Erste.B.FahrzeitHin > 60),
                    ["jahreDanach"] = Jahreswerte(b.SelectMany(x => x.Jahre.Select(y => y.B))),
                };
            }
            var neubauStufen = new JsonArray();
            var grenzen = Fahrzeitstufen.Prepend(0).Append(int.MaxValue).ToArray();
            for (var i = 0; i + 1 < grenzen.Length; i++)
            {
                var (von, bis) = (grenzen[i], grenzen[i + 1]);
                var b = betriebe.Where(x => x.Erste.B.Neubau && x.Erste.B.Ort == "Werkstatt" && x.Erste.B.FahrzeitHin > von && x.Erste.B.FahrzeitHin <= bis).ToList();
                neubauStufen.Add(new JsonObject
                {
                    ["fahrzeitHin"] = bis == int.MaxValue ? $"über {von}" : $"{von + 1}–{bis}", ["betriebe"] = b.Count,
                    ["jahreDanach"] = Jahreswerte(b.SelectMany(x => x.Jahre.Select(y => y.B))),
                });
            }
            gruppen.Add(new JsonObject
            {
                ["karte"] = g.Key.Karte, ["spielerGefuehrt"] = g.Key.Bezug.ToString(), ["schwierigkeit"] = g.Key.Schwierigkeit.ToString(), ["partien"] = g.Count(),
                ["startwerkstaetten"] = Art(b => b.Ort == "Werkstatt" && !b.Neubau && b.SeitJahr == 1),
                ["neubauten"] = Art(b => b.Neubau && b.Ort == "Werkstatt"),
                ["neubautenSpieler"] = Art(b => b.Neubau && b.Ort == "Werkstatt", nurSpieler: true),
                ["neubautenKi"] = Art(b => b.Neubau && b.Ort == "Werkstatt", nurKi: true),
                ["werkstaettenGekauft"] = Art(b => b.Ort == "Werkstatt" && !b.Neubau && b.SeitJahr > 1),
                ["umland"] = Art(b => b.Ort == "Umland"),
                ["neubautenNachFahrzeit"] = neubauStufen,
            });
            foreach (var x in betriebe.Where(x => x.Erste.B.Neubau).GroupBy(x => x.Erste.B.Haus == 0).OrderBy(x => x.Key))
                Console.WriteLine($"Ausbau {g.Key.Karte} {g.Key.Bezug} {g.Key.Schwierigkeit}, {(x.Key ? "Spieler" : "KI")}: {x.Count()} Neubauten in {g.Count()} Partien, " +
                                  $"Fahrzeit hin Median {Zahl(Quantil(x.Select(y => (double)y.Erste.B.FahrzeitHin).OrderBy(y => y).ToList(), 0.5), "0")} min, " +
                                  $"Gewinn je Jahr danach Ø {Zahl(x.SelectMany(y => y.Jahre).Select(y => y.B.Gewinn).DefaultIfEmpty(double.NaN).Average(), "0")}");
        }
        return gruppen;
    }
}
