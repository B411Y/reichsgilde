using Reichsgilde;

/// <summary>
/// Ablauf der 3D-Gegenstände ohne Godot (<c>game/scripts/GegenstandAblauf.cs</c>): Kartentisch und Geldtruhe (Hin → Offen → Zurück → Ruhe,
/// Schwenk 0,8 s, Sperre während des Schwenks, Stillstand ohne Bewegung) und die Füllstufen der Truhe.
/// </summary>
static class GegenstandAblaufTests
{
    const double Bild = 1 / 60.0;

    static void Wahr(bool b, string was) { if (!b) throw new Exception("Fehlgeschlagen: " + was); }

    static void Laufen(GegenstandAblauf a, double sekunden, Action<GegenstandAblauf>? jeBild = null)
    {
        for (var i = 0; i < (int)Math.Round(sekunden * 60); i++)
        {
            a.Weiter(Bild);
            jeBild?.Invoke(a);
        }
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        foreach (var art in Enum.GetValues<GegenstandArt>())
        {
            var m = GegenstandMasse.Von(art);
            yield return ($"Gegenstand {art}: Ruhe ist zu, ohne Sperre", () =>
            {
                var a = new GegenstandAblauf(m);
                Wahr(!a.Aktiv && !a.Gesperrt && !a.OffenSichtbar, "Ruhe");
                Wahr(a.Clip == "zu" && a.Kamera == 0 && a.Clipzeit == 0, "Clip zu, Kamera am alten Blick");
                Wahr(!a.Schliessen(true), "in der Ruhe gibt es nichts zu schließen");
            });
            yield return ($"Gegenstand {art}: öffnen mit Schwenk, Clip läuft nur vorwärts, dann offen", () =>
            {
                var a = new GegenstandAblauf(m);
                Wahr(a.Oeffnen(true) && !a.Oeffnen(true), "einmal öffnen");
                Wahr(a.Gesperrt && a.Clip == m.ClipOeffnen, "Schwenk hin: gesperrt, Clip des Öffnens");
                double kamera = 0, clip = 0;
                var offenAb = -1.0;
                var zeit = 0.0;
                Laufen(a, 0.8 + 0.01, x =>
                {
                    zeit += Bild;
                    Wahr(x.Kamera >= kamera - 1e-9, "die Kamera läuft nie zurück");
                    kamera = x.Kamera;
                    if (x.Zustand != GegenstandZustand.Hin) return;
                    Wahr(x.Clipzeit >= clip - 1e-9 && x.Clipzeit <= m.Oeffnen + 1e-9, "der Clip läuft vorwärts und nicht über sein Ende");
                    clip = x.Clipzeit;
                    if (offenAb < 0 && x.OffenSichtbar) offenAb = zeit;
                });
                Wahr(Math.Abs(a.Kamera - 1) < 1e-6, "nach 0,8 s steht die Kamera vor dem Gegenstand");
                Wahr(!a.Schliessen(true), "während des Schwenks lässt er sich nicht schließen");
                Laufen(a, 3, x => { if (offenAb < 0 && x.OffenSichtbar) offenAb = 0; });
                Wahr(a.Zustand == GegenstandZustand.Offen && !a.Gesperrt && a.OffenSichtbar && a.Clip == "offen", "danach offen und frei");
            });
            yield return ($"Gegenstand {art}: ruhig offen erst ab der Schwelle des Modells", () =>
            {
                var a = new GegenstandAblauf(m);
                a.Oeffnen(true);
                Laufen(a, BuchAblauf.AufklappenAb + m.OffenAb - 0.05);
                Wahr(!a.OffenSichtbar, "kurz vor der Schwelle noch nicht");
                Laufen(a, 0.1);
                Wahr(a.OffenSichtbar, "kurz danach schon");
            });
            yield return ($"Gegenstand {art}: schließen spielt den Clip zurück, die Kamera fährt zum alten Blick", () =>
            {
                var a = new GegenstandAblauf(m);
                a.Oeffnen(true);
                Laufen(a, 3);
                Wahr(a.Schliessen(true) && !a.Schliessen(true), "einmal schließen");
                Wahr(a.Gesperrt && a.Clip == m.ClipSchliessen && !a.OffenSichtbar, "Rückweg: gesperrt, Clip des Schließens");
                var kamera = 1.0;
                Laufen(a, 3, x =>
                {
                    Wahr(x.Kamera <= kamera + 1e-9, "die Kamera läuft nur zurück");
                    kamera = x.Kamera;
                    Wahr(x.Clipzeit <= m.Schliessen + 1e-9, "der Clip läuft nicht über sein Ende");
                });
                Wahr(!a.Aktiv && a.Kamera == 0 && a.Clip == "zu", "am Ende Ruhe");
            });
            yield return ($"Gegenstand {art}: ohne Bewegung sofort offen und sofort zu", () =>
            {
                var a = new GegenstandAblauf(m);
                a.Oeffnen(false);
                Wahr(a.Zustand == GegenstandZustand.Offen && a.OffenSichtbar && !a.Gesperrt && a.Kamera == 1, "sofort offen");
                a.Schliessen(false);
                Wahr(!a.Aktiv, "sofort zu");
            });
            yield return ($"Gegenstand {art}: abbrechen führt aus jedem Zustand in die Ruhe", () =>
            {
                var a = new GegenstandAblauf(m);
                a.Oeffnen(true);
                Laufen(a, 0.3);
                a.Abbrechen();
                Wahr(!a.Aktiv && a.Kamera == 0, "Ruhe nach dem Abbruch");
                Wahr(a.Oeffnen(true), "danach lässt er sich wieder öffnen");
            });
        }
        yield return ("Gegenstand: Maße wie in der Doku des Modells", () =>
        {
            var k = GegenstandMasse.Von(GegenstandArt.Kartentisch);
            var t = GegenstandMasse.Von(GegenstandArt.Geldtruhe);
            Wahr(k.ClipOeffnen == "entrollen" && k.ClipSchliessen == "zusammenrollen" && k.Oeffnen == 1.233 && k.Schliessen == 1.000, "Kartentisch: Clips und Längen");
            Wahr(t.ClipOeffnen == "aufspringen" && t.ClipSchliessen == "zuklappen" && t.Oeffnen == 1.033 && t.Schliessen == 0.700, "Geldtruhe: Clips und Längen");
            Wahr(k.OffenAb <= k.Oeffnen && t.OffenAb <= t.Oeffnen, "die Schwelle liegt im Clip");
        });
        yield return ("Geldtruhe: Füllstufe nach Bargeld", () =>
        {
            Wahr(Truhenfuellung.Stufe(-200) is null && Truhenfuellung.Stufe(0) is null && Truhenfuellung.Stufe(Truhenfuellung.AbViertel - 1) is null, "leer");
            Wahr(Truhenfuellung.Stufe(Truhenfuellung.AbViertel) == "viertel" && Truhenfuellung.Stufe(Truhenfuellung.AbHalb - 1) == "viertel", "viertel");
            Wahr(Truhenfuellung.Stufe(Truhenfuellung.AbHalb) == "halb" && Truhenfuellung.Stufe(Truhenfuellung.AbVoll - 1) == "halb", "halb");
            Wahr(Truhenfuellung.Stufe(Truhenfuellung.AbVoll) == "voll" && Truhenfuellung.Stufe(1e9) == "voll", "voll");
            Wahr(Truhenfuellung.AbViertel < Truhenfuellung.AbHalb && Truhenfuellung.AbHalb < Truhenfuellung.AbVoll, "Schwellen steigen");
            Wahr(Truhenfuellung.Stufen.SequenceEqual(new[] { "viertel", "halb", "voll" }), "Namen wie die Knoten Fuellstufe_* des Modells");
        });
    }
}
