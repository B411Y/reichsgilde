using Reichsgilde;

/// <summary>
/// Ablauf der 3D-Bücher der Innenansichten ohne Godot (<c>game/scripts/BuchAblauf.cs</c>): Reihenfolge Hin → Offen → Blättern → Zurück → Ruhe,
/// Schwenk 0,8 s und abgebremst, Tafel erst bei flachen Seiten und nicht beim Umblättern, Sperre während des Schwenks, Stillstand ohne Bewegung.
/// </summary>
static class BuchAblaufTests
{
    const double Bild = 1 / 60.0;

    static void Wahr(bool b, string was) { if (!b) throw new Exception("Fehlgeschlagen: " + was); }

    static void Laufen(BuchAblauf a, double sekunden, Action<BuchAblauf>? jeBild = null)
    {
        for (var i = 0; i < (int)Math.Round(sekunden * 60); i++)
        {
            a.Weiter(Bild);
            jeBild?.Invoke(a);
        }
    }

    static BuchAblauf Offen(BuchArt art)
    {
        var a = new BuchAblauf(BuchMasse.Von(art));
        a.Oeffnen(true);
        Laufen(a, 3);
        Wahr(a.Zustand == BuchZustand.Offen, $"{art}: nach 3 s offen");
        return a;
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Buch: Ruhe ist zu, ohne Tafel und ohne Sperre", () =>
        {
            var a = new BuchAblauf(BuchMasse.Von(BuchArt.Personal));
            Wahr(!a.Aktiv && !a.Gesperrt && !a.TafelSichtbar, "Ruhe");
            Wahr(a.Clip == "zu" && a.Kamera == 0, "Clip zu, Kamera am alten Blick");
            Wahr(!a.Schliessen(true) && !a.Blaettern(true, true), "in der Ruhe gibt es nichts zu schließen oder zu blättern");
        });
        yield return ("Buch: Schwenk hin dauert 0,8 s, ist abgebremst und läuft nie zurück", () =>
        {
            foreach (var art in Enum.GetValues<BuchArt>())
            {
                var a = new BuchAblauf(BuchMasse.Von(art));
                Wahr(a.Oeffnen(true), "öffnet");
                Wahr(!a.Oeffnen(true), "zweimal öffnen geht nicht");
                double vorher = 0, ersterSchritt = 0, letzterSchritt = 0;
                var bilder = 0;
                Laufen(a, BuchAblauf.SchwenkSekunden, x =>
                {
                    Wahr(x.Kamera >= vorher - 1e-9, "Kamera läuft nur vorwärts");
                    if (bilder == 0) ersterSchritt = x.Kamera - vorher;
                    letzterSchritt = x.Kamera - vorher;
                    vorher = x.Kamera;
                    bilder++;
                    Wahr(x.Gesperrt, "während des Schwenks gesperrt");
                });
                Wahr(Math.Abs(a.Kamera - 1) < 1e-6, $"{art}: nach 0,8 s vor dem Buch");
                Wahr(ersterSchritt > 5 * letzterSchritt, "abgebremst: am Anfang schnell, am Ende langsam");
            }
        });
        yield return ("Buch: klappt beim Schwenk auf, die Tafel kommt erst bei flachen Seiten", () =>
        {
            foreach (var art in Enum.GetValues<BuchArt>())
            {
                var m = BuchMasse.Von(art);
                var a = new BuchAblauf(m);
                a.Oeffnen(true);
                Laufen(a, BuchAblauf.AufklappenAb - 0.05);
                Wahr(a.Clip == "aufklappen" && a.Clipzeit == 0 && !a.TafelSichtbar, "erst schwenken, Buch noch zu");
                Laufen(a, 0.05 + m.InhaltAb - 0.05);
                Wahr(!a.TafelSichtbar, "Seiten noch nicht flach: keine Tafel");
                Laufen(a, 0.1);
                Wahr(a.TafelSichtbar && a.Zustand == BuchZustand.Hin, "Seiten flach: Tafel da");
                Laufen(a, 2);
                Wahr(a.Zustand == BuchZustand.Offen && a.Clip == "offen" && !a.Gesperrt && a.Kamera == 1, $"{art}: offen, frei, Kamera am Buch");
            }
        });
        yield return ("Buch: Umblättern nimmt die Tafel weg und bringt sie am Ende wieder", () =>
        {
            foreach (var art in Enum.GetValues<BuchArt>())
            {
                var m = BuchMasse.Von(art);
                var a = Offen(art);
                Wahr(a.Blaettern(true, true), "blättert");
                Wahr(a.Clip == "umblaettern" && !a.TafelSichtbar && !a.Gesperrt, "Blatt wendet, Tafel weg, nicht gesperrt");
                Laufen(a, m.BlattInhaltAb - 0.05);
                Wahr(!a.TafelSichtbar, "Blatt noch in der Luft");
                Laufen(a, m.Umblaettern - m.BlattInhaltAb + 0.1);
                Wahr(a.Zustand == BuchZustand.Offen && a.TafelSichtbar && a.Kamera == 1, $"{art}: wieder offen");
                a.Blaettern(false, true);
                Wahr(a.Clip == "umblaettern_zurueck", "zurück blättern nimmt den anderen Clip");
            }
        });
        yield return ("Buch: Schließen klappt zu, dann fährt die Kamera zurück – ohne Sprung", () =>
        {
            foreach (var art in Enum.GetValues<BuchArt>())
            {
                var a = Offen(art);
                Wahr(a.Schliessen(true), "schließt");
                Wahr(a.Clip == "zuklappen" && !a.TafelSichtbar && a.Gesperrt, "zuklappen, Tafel weg, gesperrt");
                Wahr(!a.Schliessen(true) && !a.Blaettern(true, true) && !a.Oeffnen(true), "während des Rückwegs nimmt das Buch nichts an");
                double vorher = 1, groessterSchritt = 0;
                Laufen(a, 3, x =>
                {
                    Wahr(x.Kamera <= vorher + 1e-9, "Kamera läuft nur zurück");
                    groessterSchritt = Math.Max(groessterSchritt, vorher - x.Kamera);
                    vorher = x.Kamera;
                });
                Wahr(groessterSchritt < 0.05, "kein Sprung auf dem Rückweg");
                Wahr(a.Zustand == BuchZustand.Ruhe && a.Kamera == 0 && a.Clip == "zu", $"{art}: wieder in Ruhe");
                Wahr(a.Oeffnen(true), "danach lässt es sich wieder öffnen");
            }
        });
        yield return ("Buch: ohne Bewegung sofort offen und sofort zu, kein Umblättern", () =>
        {
            var a = new BuchAblauf(BuchMasse.Von(BuchArt.Familie));
            a.Oeffnen(false);
            Wahr(a.Zustand == BuchZustand.Offen && a.TafelSichtbar && a.Kamera == 1 && !a.Gesperrt, "sofort offen");
            Wahr(!a.Blaettern(true, false) && a.Zustand == BuchZustand.Offen && a.TafelSichtbar, "Reiterwechsel ohne Blatt");
            a.Schliessen(false);
            Wahr(a.Zustand == BuchZustand.Ruhe && a.Kamera == 0, "sofort zu");
        });
        yield return ("Buch: Abbrechen führt aus jedem Zustand sofort in die Ruhe", () =>
        {
            var a = new BuchAblauf(BuchMasse.Von(BuchArt.Kontor));
            a.Oeffnen(true);
            Laufen(a, 0.3);
            a.Abbrechen();
            Wahr(!a.Aktiv && a.Kamera == 0 && !a.TafelSichtbar, "aus dem Schwenk");
            a = Offen(BuchArt.Kontor);
            a.Abbrechen();
            Wahr(!a.Aktiv, "aus dem offenen Buch");
        });
        yield return ("Buch: Maße der drei Modelle sind stimmig", () =>
        {
            foreach (var art in Enum.GetValues<BuchArt>())
            {
                var m = BuchMasse.Von(art);
                Wahr(m.InhaltAb < m.Aufklappen && m.InhaltBis < m.Zuklappen && m.BlattInhaltAb <= m.Umblaettern, $"{art}: Inhaltszeiten liegen in den Clips");
                Wahr(m.BreiteOffen > m.HoeheOffen && m.HoeheOffen > 0.2, $"{art}: aufgeschlagen breiter als hoch");
            }
        });
    }
}
