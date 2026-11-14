using Reichsgilde;

/// <summary>Zeiger-Symbole des 3D-Handschuhs (<c>ZeigerSymbole.cs</c>), ohne Godot: Wechsel Hand ↔ Messing-I über Textfeldern und Windrose statt Fragezeichen.</summary>
static class ZeigerSymboleTests
{
    const double Bild = 1.0 / 60.0;

    /// <summary>Läuft Bild für Bild, bis die Bedingung gilt; liefert die verstrichene Zeit (oder 99 s).</summary>
    static double Bis(SymbolWechsel w, bool ueberText, Func<bool> fertig)
    {
        var t = 0.0;
        while (!fertig() && t < 99) { w.Schritt(Bild, ueberText); t += Bild; }
        return t;
    }

    /// <summary>Die Dauer (0,1 s) auf ein Bild genau (Rundung der Bildzeiten).</summary>
    static void EtwaDauer(double t) => Pruefe.Wahr(Math.Abs(t - SymbolWechsel.DauerSekunden) <= Bild + 1e-9, $"0,1 s erwartet, war {t:0.000} s");

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zeiger-Symbole: Hand -> Schrumpfen -> I wachsen -> I -> I schrumpfen -> Hand wachsen, je 0,1 s", () =>
        {
            var w = new SymbolWechsel();
            Pruefe.Gleich(SymbolZustand.Hand, w.Zustand);
            Pruefe.Gleich(1.0, w.HandGroesse);
            Pruefe.Gleich(0.0, w.IGroesse);
            w.Schritt(Bild, true);
            Pruefe.Gleich(SymbolZustand.HandSchrumpft, w.Zustand);
            var t = Bis(w, true, () => w.HandGroesse <= 0);
            EtwaDauer(t + Bild); // ein Bild ist schon gelaufen
            w.Schritt(Bild, true);
            Pruefe.Gleich(SymbolZustand.IWaechst, w.Zustand);
            t = Bis(w, true, () => w.Zustand == SymbolZustand.I);
            EtwaDauer(t + Bild);
            Pruefe.Gleich(1.0, w.IGroesse);
            Pruefe.Gleich(0.0, w.HandGroesse);
            w.Schritt(Bild, false);
            Pruefe.Gleich(SymbolZustand.ISchrumpft, w.Zustand);
            t = Bis(w, false, () => w.IGroesse <= 0);
            EtwaDauer(t + Bild);
            w.Schritt(Bild, false);
            Pruefe.Gleich(SymbolZustand.HandWaechst, w.Zustand);
            t = Bis(w, false, () => w.Zustand == SymbolZustand.Hand);
            EtwaDauer(t + Bild);
            Pruefe.Gleich(1.0, w.HandGroesse);
        }),
        ("Zeiger-Symbole: Dauer und Kurve – 0,1 s je Richtung, Größe folgt Smoothstep t²(3−2t)", () =>
        {
            Pruefe.Gleich(0.1, SymbolWechsel.DauerSekunden);
            var w = new SymbolWechsel();
            w.Schritt(0.025, true); // t = 0,25
            Pruefe.Ungefaehr(1 - 0.25 * 0.25 * (3 - 2 * 0.25), w.HandGroesse, 1e-9);
            w.Schritt(0.025, true); // Hälfte des Schrumpfens
            Pruefe.Ungefaehr(0.5, w.HandGroesse, 1e-9);
            w.Schritt(0.05 + 0.075, true); // Hand weg, I bei t = 0,75
            Pruefe.Ungefaehr(0.75 * 0.75 * (3 - 2 * 0.75), w.IGroesse, 1e-9);
        }),
        ("Zeiger-Symbole: Unterbrechung – Maus verlässt das Textfeld mitten im Wachsen des I: zurück ohne Sprung", () =>
        {
            var w = new SymbolWechsel();
            Bis(w, true, () => w.IGroesse >= 0.4);
            var vorher = w.IGroesse;
            w.Schritt(Bild, false);
            Pruefe.Gleich(SymbolZustand.ISchrumpft, w.Zustand);
            Pruefe.Wahr(w.IGroesse < vorher && vorher - w.IGroesse < 0.3, $"kein Sprung: {vorher} -> {w.IGroesse}");
            var letzteI = w.IGroesse; var letzteHand = w.HandGroesse;
            for (var i = 0; i < 60; i++)
            {
                w.Schritt(Bild, false);
                Pruefe.Wahr(Math.Abs(w.IGroesse - letzteI) < 0.3 && Math.Abs(w.HandGroesse - letzteHand) < 0.3, $"stetig in Bild {i}");
                letzteI = w.IGroesse; letzteHand = w.HandGroesse;
            }
            Pruefe.Gleich(SymbolZustand.Hand, w.Zustand);
            // und umgekehrt: mitten im Schrumpfen der Hand wieder hinaus, die Hand wächst vom selben Stand zurück
            Bis(w, true, () => w.HandGroesse <= 0.7);
            var hand = w.HandGroesse;
            w.Schritt(Bild, false);
            Pruefe.Gleich(SymbolZustand.HandWaechst, w.Zustand);
            Pruefe.Wahr(w.HandGroesse > hand && w.HandGroesse - hand < 0.3, $"kein Sprung: {hand} -> {w.HandGroesse}");
            Pruefe.Gleich(0.0, w.IGroesse);
        }),
        ("Zeiger-Symbole: Gegenprobe – nie Hand und I zugleich, nie beide über 50 %, Zeigerpunkt wechselt nur, wenn beides (fast) weg ist", () =>
        {
            var w = new SymbolWechsel();
            var zufall = new Random(7);
            var punktIVorher = w.ZeigerpunktIstI;
            for (var i = 0; i < 3000; i++)
            {
                w.Schritt(zufall.NextDouble() / 60, zufall.Next(4) != 0 ? i / 40 % 2 == 0 : zufall.Next(2) == 0); // Bildzeiten bis 1/60 s (bei 0,1 s Dauer ein Sechstel des Wegs je Bild), Maus wechselt oft
                Pruefe.Wahr(!(w.HandGroesse > 0 && w.IGroesse > 0), $"beide sichtbar in Schritt {i}");
                Pruefe.Wahr(!(w.HandGroesse > 0.5 && w.IGroesse > 0.5), $"beide über 50 % in Schritt {i}");
                if (w.IGroesse >= 1) Pruefe.Gleich(0.0, w.HandGroesse);
                if (w.ZeigerpunktIstI != punktIVorher) Pruefe.Wahr(w.HandGroesse < 0.08 && w.IGroesse < 0.08, $"Zeigerpunkt sprang bei sichtbarem Symbol in Schritt {i}");
                punktIVorher = w.ZeigerpunktIstI;
                Pruefe.Wahr(w.HandGroesse is >= 0 and <= 1 && w.IGroesse is >= 0 and <= 1, "Größen in 0..1");
            }
        }),
        ("Zeiger-Symbole: Windrose wie das Fragezeichen – an über verschiebbarer Fläche, sonst aus; geht dem Fragezeichen vor", () =>
        {
            Pruefe.Gleich((false, false), ZeigerMarken.Waehlen(false, false));
            Pruefe.Gleich((true, false), ZeigerMarken.Waehlen(true, false));
            Pruefe.Gleich((false, true), ZeigerMarken.Waehlen(false, true));
            Pruefe.Gleich((false, true), ZeigerMarken.Waehlen(true, true)); // am selben Platz: nie beide
        }),
    };
}
