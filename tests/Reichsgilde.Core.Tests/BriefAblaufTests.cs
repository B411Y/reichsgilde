using Reichsgilde;

/// <summary>
/// Ablauf der 3D-Briefe der Meldungen ohne Godot: der große Brief (<c>game/scripts/BriefAblauf.cs</c>: Auffalten → offen → Zufalten → weg, Inhalt als Bild
/// nur in den Zeiten des Modells, echte Tafel nur offen, Streckregel) und der kleine Faltbrief der Leiste (<c>SchriftrolleAblauf.cs</c> mit den Maßen des
/// Briefs: Sprung nur beim Erscheinen, Leerlauf nur um Y mit höchstens ±2° und je Brief versetzt, Weggehen ohne Sprung).
/// </summary>
static class BriefAblaufTests
{
    static readonly BriefMasse Masse = new(AufSekunden: 0.9, ZuSekunden: 0.9, InhaltAb: 0.733, InhaltBis: 0.067, EinblendSekunden: 0.1, AusblendSekunden: 0.15);

    // Der kleine Faltbrief hat keine Clips: sein „Clip“ ist die Zeit des Ausblendens (SchriftrollenBuehne.Nehmen)
    static readonly RollenMasse Klein = new(ClipSekunden: 0.18, SymbolAb: 0, NeigungGrad: 12, PendelGrad: 2, HuepfHoehe: 0.022, HuepfSekunden: 0.26, AusblendSekunden: 0.18);

    static void Laufen(BriefAblauf a, double sekunden, Action<BriefAblauf>? jeBild = null)
    {
        for (var i = 0; i < (int)Math.Round(sekunden * 60); i++)
        {
            a.Weiter(1 / 60.0, true);
            jeBild?.Invoke(a);
        }
    }

    static void Laufen(SchriftrolleAblauf a, double sekunden, Action<SchriftrolleAblauf>? jeBild = null)
    {
        for (var i = 0; i < (int)Math.Round(sekunden * 60); i++)
        {
            a.Weiter(1 / 60.0, true);
            jeBild?.Invoke(a);
        }
    }

    static void Nahe(double soll, double ist, double toleranz, string was) { if (Math.Abs(soll - ist) > toleranz) throw new Exception($"Fehlgeschlagen: {was} (erwartet {soll}, war {ist})"); }

    static void Wahr(bool b, string was) { if (!b) throw new Exception("Fehlgeschlagen: " + was); }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Brief: öffnet mit Auffalten, Inhalt als Bild erst ab 0,733 s, dann die echte Tafel", () =>
        {
            var a = new BriefAblauf(Masse);
            Pruefe.Gleich(BriefZustand.Weg, a.Zustand);
            Wahr(!a.Sichtbar, "vor dem Öffnen nicht zu sehen");
            a.Oeffnen(true);
            Pruefe.Gleich(BriefZustand.Auffalten, a.Zustand);
            Pruefe.Gleich("auffalten", a.Clip);
            Pruefe.Ungefaehr(0, a.Clipzeit, 1e-9);
            Wahr(!a.BildSichtbar && !a.TafelSichtbar, "am Anfang weder Bild noch Tafel");
            Laufen(a, 0.7);
            Wahr(!a.BildSichtbar, "Bild vor 0,733 s aus");
            Wahr(!a.TafelSichtbar, "Tafel während des Auffaltens aus");
            Laufen(a, 0.1);
            Wahr(a.BildSichtbar, "Bild ab 0,733 s an");
            Wahr(!a.TafelSichtbar, "Tafel erst, wenn der Brief offen ist");
            Laufen(a, 0.2);
            Pruefe.Gleich(BriefZustand.Offen, a.Zustand);
            Wahr(a.TafelSichtbar, "offen: die echte Tafel liegt darüber");
            Wahr(!a.BildSichtbar, "offen: kein Bild unter der Tafel (kein doppelter Text)");
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
            Laufen(a, 60);
            Pruefe.Gleich(BriefZustand.Offen, a.Zustand, "bleibt offen, bis die Meldung erledigt ist");
        }),
        ("Brief: erledigt faltet zu, Bild nur bis 0,067 s, dann weg", () =>
        {
            var a = new BriefAblauf(Masse);
            a.Oeffnen(true);
            Laufen(a, 2);
            a.Schliessen(true);
            Pruefe.Gleich(BriefZustand.Zufalten, a.Zustand);
            Pruefe.Gleich("zufalten", a.Clip);
            Pruefe.Ungefaehr(0, a.Clipzeit, 1e-9);
            Wahr(a.BildSichtbar, "Bild am Anfang des Zufaltens an");
            Wahr(!a.TafelSichtbar, "Tafel beim Zufalten sofort weg");
            Laufen(a, 0.1);
            Wahr(!a.BildSichtbar, "Bild nach 0,067 s aus");
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
            Laufen(a, 0.9);
            Pruefe.Gleich(BriefZustand.Weg, a.Zustand);
            Wahr(!a.Sichtbar, "am Ende weg");
            Pruefe.Ungefaehr(0, a.Deckkraft, 1e-9);
        }),
        ("Brief: die Clipzeit läuft nur vorwärts und nie über die Cliplänge", () =>
        {
            var a = new BriefAblauf(Masse);
            a.Oeffnen(true);
            var vorher = -1.0;
            Laufen(a, 1.5, b =>
            {
                Wahr(b.Clipzeit >= vorher && b.Clipzeit <= 0.9 + 1e-9, $"Auffalten: Clipzeit {b.Clipzeit}");
                vorher = b.Clipzeit;
            });
            a.Schliessen(true);
            vorher = -1;
            Laufen(a, 1.5, b =>
            {
                Wahr(b.Clipzeit >= vorher && b.Clipzeit <= 0.9 + 1e-9, $"Zufalten: Clipzeit {b.Clipzeit}");
                vorher = b.Clipzeit;
            });
        }),
        ("Brief: mitten im Auffalten erledigt – Zufalten setzt an der passenden Stelle an, und umgekehrt", () =>
        {
            var a = new BriefAblauf(Masse);
            a.Oeffnen(true);
            Laufen(a, 0.3);
            a.Schliessen(true);
            Pruefe.Gleich(BriefZustand.Zufalten, a.Zustand);
            Pruefe.Ungefaehr(0.6, a.Clipzeit, 1e-6);
            Laufen(a, 0.1);
            a.Oeffnen(true);
            Pruefe.Gleich(BriefZustand.Auffalten, a.Zustand);
            Pruefe.Ungefaehr(0.2, a.Clipzeit, 1e-6);
            a.Oeffnen(true);
            Nahe(0.2, a.Clipzeit, 1e-6, "ein zweites Öffnen fängt nicht von vorn an");
        }),
        ("Brief: ohne Bewegung sofort offen und sofort weg", () =>
        {
            var a = new BriefAblauf(Masse);
            a.Oeffnen(false);
            Pruefe.Gleich(BriefZustand.Offen, a.Zustand);
            Wahr(a.TafelSichtbar && !a.BildSichtbar, "sofort die Tafel");
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
            a.Schliessen(false);
            Pruefe.Gleich(BriefZustand.Weg, a.Zustand);
            // Bewegung wird mitten im Auffalten abgeschaltet: das nächste Bild steht offen; mitten im Zufalten: weg
            a.Oeffnen(true);
            Laufen(a, 0.2);
            a.Weiter(1 / 60.0, false);
            Pruefe.Gleich(BriefZustand.Offen, a.Zustand);
            a.Schliessen(true);
            Laufen(a, 0.2);
            a.Weiter(1 / 60.0, false);
            Pruefe.Gleich(BriefZustand.Weg, a.Zustand);
        }),
        ("Brief: blendet am Anfang kurz ein und am Ende kurz aus", () =>
        {
            var a = new BriefAblauf(Masse);
            a.Oeffnen(true);
            Pruefe.Ungefaehr(0, a.Deckkraft, 1e-9);
            Laufen(a, 0.05);
            Wahr(a.Deckkraft > 0.3 && a.Deckkraft < 0.7, $"halb eingeblendet: {a.Deckkraft}");
            Laufen(a, 0.1);
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
            Laufen(a, 2);
            a.Schliessen(true);
            Laufen(a, 0.7);
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
            Laufen(a, 0.15);
            Wahr(a.Deckkraft > 0 && a.Deckkraft < 0.5, $"blendet aus: {a.Deckkraft}");
        }),
        ("Brief: Streckregel des Modells – nur die Mitte dehnt sich, die Falze wandern", () =>
        {
            var (s, d, inhalt) = BriefAblauf.Streckung(0.42);
            Pruefe.Ungefaehr(1, s, 1e-9);
            Pruefe.Ungefaehr(0, d, 1e-9);
            Pruefe.Ungefaehr(1, inhalt, 1e-9);
            (s, d, inhalt) = BriefAblauf.Streckung(0.60);
            Pruefe.Ungefaehr(2, s, 1e-9);
            Pruefe.Ungefaehr(0.09, d, 1e-9);
            Pruefe.Ungefaehr(0.56 / 0.38, inhalt, 1e-9);
            (s, d, inhalt) = BriefAblauf.Streckung(0.51);
            Pruefe.Ungefaehr(1.5, s, 1e-9);
            Pruefe.Ungefaehr(0.045, d, 1e-9);
            // Blatthöhe = feste Falze + gestreckte Mitte; die Inhaltsfläche lässt oben und unten 2 cm Rand
            Pruefe.Ungefaehr(0.51, BriefAblauf.Falze + s * BriefAblauf.Mitte, 1e-9);
            Pruefe.Ungefaehr(0.51 - 0.04, inhalt * 0.38, 1e-9);
            // Außerhalb des Bereichs wird begrenzt
            Pruefe.Ungefaehr(1, BriefAblauf.Streckung(0.2).Skala, 1e-9);
            Pruefe.Ungefaehr(2, BriefAblauf.Streckung(0.9).Skala, 1e-9);
        }),
        ("Brief: Blatthöhe aus dem Inhalt – kurz bleibt 0,42 m, lang streckt bis 0,60 m", () =>
        {
            Pruefe.Ungefaehr(0.42, BriefAblauf.Blatthoehe(0.10), 1e-9);
            Pruefe.Ungefaehr(0.42, BriefAblauf.Blatthoehe(0.38), 1e-9);
            Pruefe.Ungefaehr(0.50, BriefAblauf.Blatthoehe(0.46), 1e-9);
            Pruefe.Ungefaehr(0.60, BriefAblauf.Blatthoehe(0.56), 1e-9);
            Pruefe.Ungefaehr(0.60, BriefAblauf.Blatthoehe(1.50), 1e-9);
            Pruefe.Ungefaehr(0.26, BriefAblauf.InhaltBreite, 1e-9);
        }),
        ("Faltbrief der Leiste: erscheint mit einem Sprung, Zeichen im Siegel von Anfang an", () =>
        {
            var a = new SchriftrolleAblauf(Klein, 0.3, 0.5);
            a.Erscheinen(true);
            Wahr(a.SymbolSichtbar, "das Zeichen ist ins Siegel gedrückt und steht sofort");
            var hoechste = 0.0;
            Laufen(a, 0.26, b => hoechste = Math.Max(hoechste, b.Hoehe));
            Wahr(hoechste > 0.015 && hoechste <= 0.022 + 1e-9, $"Sprung beim Erscheinen: {hoechste}");
            Laufen(a, 0.1);
            Nahe(0, a.Hoehe, 1e-9, "nach dem Sprung ruhig");
            Pruefe.Gleich(RollenZustand.Offen, a.Zustand);
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
        }),
        ("Faltbrief der Leiste: Leerlauf nur um Y, höchstens ±2°, je Brief zeitversetzt", () =>
        {
            var a = new SchriftrolleAblauf(Klein, 0.1, 0.2);
            var b = new SchriftrolleAblauf(Klein, 0.6, 0.9);
            a.Erscheinen(true);
            b.Erscheinen(true);
            double groesste = 0, unterschied = 0;
            for (var i = 0; i < 60 * 30; i++)
            {
                a.Weiter(1 / 60.0, true);
                b.Weiter(1 / 60.0, true);
                var (x, y, z) = a.Drehung;
                Nahe(12, x, 1e-9, "Neigung fest");
                Nahe(0, z, 1e-9, "Z ruht");
                Nahe(0, a.Hoehe * (i > 30 ? 1 : 0), 1e-9, "im Leerlauf kein Hüpfen");
                groesste = Math.Max(groesste, Math.Abs(y));
                unterschied = Math.Max(unterschied, Math.Abs(y - b.Drehung.Y));
            }
            Wahr(groesste > 1.5 && groesste <= 2 + 1e-9, $"Drehen um Y bis ±2°: {groesste}");
            Wahr(unterschied > 1, $"zwei Briefe pendeln versetzt: {unterschied}");
        }),
        ("Faltbrief der Leiste: geht ohne Sprung, blendet aus und ist weg", () =>
        {
            var a = new SchriftrolleAblauf(Klein, 0.3, 0.5);
            a.Erscheinen(true);
            Laufen(a, 3);
            a.Schliessen(true);
            Pruefe.Gleich(RollenZustand.Zurollen, a.Zustand);
            var vorher = 1.0;
            Laufen(a, 0.17, b =>
            {
                Nahe(0, b.Hoehe, 1e-9, "Weggehen ohne Hüpfen");
                Wahr(b.Deckkraft <= vorher + 1e-9, "blendet nur aus");
                vorher = b.Deckkraft;
            });
            Wahr(vorher < 0.2, $"fast ausgeblendet: {vorher}");
            Laufen(a, 0.05);
            Pruefe.Gleich(RollenZustand.Weg, a.Zustand);
            // Ohne Bewegung: sofort da, still, sofort weg
            var s = new SchriftrolleAblauf(Klein, 0.3, 0.5);
            s.Erscheinen(false);
            Pruefe.Gleich(RollenZustand.Offen, s.Zustand);
            s.Weiter(5, false);
            Pruefe.Ungefaehr(0, s.Drehung.Y, 1e-9);
            Pruefe.Ungefaehr(0, s.Hoehe, 1e-9);
            s.Schliessen(false);
            Pruefe.Gleich(RollenZustand.Weg, s.Zustand);
        }),
    };
}
