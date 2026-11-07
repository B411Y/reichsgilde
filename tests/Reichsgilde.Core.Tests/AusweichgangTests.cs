using Reichsgilde;

/// <summary>
/// Diebe weichen der Streife aus (nur Bild, <see cref="Ausweichgang"/>): Das Fenster aus den Abständen und die Abschnitte der zweiten Bahn.
/// Die Figuren und Wege setzt die Stadtansicht (Stadtansicht.Ausweichen.cs); hier die reine Rechnung.
/// </summary>
static class AusweichgangTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ausweichgang: Bleibt die Streife fern, gibt es kein Ausweichen", () =>
        {
            Pruefe.Wahr(Ausweichgang.Finden(0, 20, _ => 30f) is null, "immer 30 m weit weg");
            Pruefe.Wahr(Ausweichgang.Finden(0, 20, _ => float.MaxValue) is null, "keine Streife zu sehen");
            Pruefe.Wahr(Ausweichgang.Finden(0, 20, _ => Ausweichgang.Radius) is null, "genau am Rand zählt nicht als nah");
        }),

        ("Ausweichgang: Kommt sie nah, beginnt das Fenster etwas vorher und endet etwas nach der letzten Nähe", () =>
        {
            // Streife kommt auf dem Weg von 30 m bei Minute 0 auf 2 m bei Minute 14 heran und geht wieder (2 m je Minute)
            float Abstand(double t) => (float)(2 + Math.Abs(t - 14) * 2);
            var f = Ausweichgang.Finden(0, 40, Abstand) ?? throw new Exception("kein Fenster");
            Pruefe.Wahr(f.Von < 10 && f.Von >= 5, $"Beginn vor der ersten Nähe (Minute 10) um den Anlauf: {f.Von}");
            Pruefe.Wahr(f.Bis > 18 && f.Bis <= 24, $"Ende nach der letzten Nähe (Minute 18): {f.Bis}");
            Pruefe.Wahr(Math.Abs(f.Naechste - 14) < 1.01, $"größte Nähe bei Minute 14: {f.Naechste}");
            Pruefe.Wahr(f.Bis - f.Von >= Ausweichgang.MinDauer && f.Bis - f.Von <= Ausweichgang.MaxDauer, "Dauer in den Grenzen");
        }),

        ("Ausweichgang: Das Fenster liegt immer im erlaubten Zeitraum und ist nie kürzer als die Mindestdauer", () =>
        {
            for (var von = 0; von < 30; von += 3)
                for (var ende = von + 5; ende < von + 40; ende += 4)
                    for (var mitte = von - 5; mitte < ende + 5; mitte += 2)
                    {
                        // die Streife ist nur kurz nah (um die Minute <mitte>), sonst fern
                        var f = Ausweichgang.Finden(von, ende, t => Math.Abs(t - mitte) <= 1 ? 5f : 40f);
                        if (f is not { } w) continue;
                        Pruefe.Wahr(w.Von >= von - 1e-9 && w.Bis <= ende + 1e-9, $"im Zeitraum {von}–{ende}: {w.Von}–{w.Bis}");
                        Pruefe.Wahr(w.Bis - w.Von >= Ausweichgang.MinDauer - 1e-6, $"mindestens {Ausweichgang.MinDauer} Minuten: {w.Bis - w.Von}");
                        Pruefe.Wahr(w.Naechste >= w.Von && w.Naechste <= w.Bis, "die größte Nähe liegt im Fenster");
                    }
        }),

        ("Ausweichgang: Bleibt die Streife lange nah, kehrt der Dieb nach der Höchstdauer dennoch auf seinen Weg zurück", () =>
        {
            var f = Ausweichgang.Finden(0, 60, t => t >= 5 ? 6f : 40f) ?? throw new Exception("kein Fenster");
            Pruefe.Wahr(f.Bis - f.Von <= Ausweichgang.MaxDauer + 1e-9, $"höchstens {Ausweichgang.MaxDauer} Minuten: {f.Bis - f.Von}");
        }),

        ("Ausweichgang: Ein zu kurzer Zeitraum (Schlussphase, Tat) gibt kein Ausweichen", () =>
        {
            Pruefe.Wahr(Ausweichgang.Finden(0, Ausweichgang.MinDauer - 1, _ => 3f) is null, "kürzer als die Mindestdauer");
            Pruefe.Wahr(Ausweichgang.Finden(10, 10, _ => 3f) is null, "leer");
        }),

        ("Ausweichgang: Fremde Diebe (kurzer Weg, 3,6 Minuten) bekommen ein kürzeres Ausweichen, eigene brauchen mindestens vier", () =>
        {
            // das erlaubte Fenster eines fremden Diebes beim Anschleichen: 60 % von 6 Minuten
            var erlaubt = Taschendiebgang.AusweichBisAnteil * Taschendiebgang.VorlaufFremd;
            Pruefe.Wahr(erlaubt < Ausweichgang.MinDauer, "kürzer als die Mindestdauer der Eigenen");
            Pruefe.Wahr(Ausweichgang.Finden(100, 100 + erlaubt, _ => 5f) is null, "mit der Standard-Mindestdauer kein Ausweichen");
            var f = Ausweichgang.Finden(100, 100 + erlaubt, _ => 5f, Ausweichgang.MinDauerFremd) ?? throw new Exception("kein Fenster für den Fremden");
            Pruefe.Wahr(f.Von >= 100 - 1e-9 && f.Bis <= 100 + erlaubt + 1e-9, "im erlaubten Zeitraum");
            Pruefe.Wahr(f.Bis - f.Von >= Ausweichgang.MinDauerFremd - 1e-6, "mindestens die Mindestdauer der Fremden");
            Pruefe.Wahr(Ausweichgang.Finden(100, 101.5, _ => 5f, Ausweichgang.MinDauerFremd) is null, "auch für Fremde nicht kürzer als 2,5 Minuten");
        }),

        ("Ausweichgang: Am Rand des Weges zum Opfer (60 %) fällt das Ausweichen nicht wegen Gleitkomma weg", () =>
        {
            var ohne = 0;
            var geprueft = 0;
            foreach (var eigen in new[] { true, false })
                for (var griff = 500; griff < 1100; griff += 7)
                    for (var vorlauf = Taschendiebgang.VorlaufKuerzest; vorlauf <= Taschendiebgang.Vorlauf(eigen); vorlauf += 0.37)
                    {
                        var bis = griff - vorlauf + Taschendiebgang.AusweichBisAnteil * vorlauf;
                        geprueft++;
                        var a = Taschendiebgang.AnschleichenAusweichAnteil(bis, griff + 0.0, eigen, vorlauf);
                        if (a is null) ohne++;
                        else Pruefe.Wahr(a <= Taschendiebgang.AusweichBisAnteil + 1e-12, $"nie über 60 %: {a}");
                        // kurz danach geht er auf die Stelle hinter dem Opfer zu: kein Ausweichen mehr
                        Pruefe.Wahr(Taschendiebgang.AnschleichenAusweichAnteil(bis + 0.05 * vorlauf, griff, eigen, vorlauf) is null, "danach kein Ausweichen");
                    }
            Pruefe.Gleich(0, ohne, $"Ende des Fensters ohne Wert in {ohne} von {geprueft} Fällen");
        }),

        ("Ausweichgang: Der Abgang liefert seinen Anteil von 0 bis 1 und sonst nichts", () =>
        {
            foreach (var eigen in new[] { true, false })
            {
                var anfang = 600 + Taschendiebgang.GriffDauer;
                Pruefe.Wahr(Taschendiebgang.AbgangAnteil(anfang - 0.1, 600, eigen) is null, "vor dem Abgang");
                Pruefe.Wahr(Taschendiebgang.AbgangAnteil(anfang, 600, eigen) is { } a0 && a0 < 1e-9, "Anfang");
                Pruefe.Wahr(Taschendiebgang.AbgangAnteil(anfang + Taschendiebgang.Abgang(eigen) - 0.01, 600, eigen) is > 0.9 and <= 1, "Ende");
                Pruefe.Wahr(Taschendiebgang.AbgangAnteil(anfang + Taschendiebgang.Abgang(eigen), 600, eigen) is null, "danach");
            }
        }),

        ("Ausweichgang: Hinaus, Halt, Zurück folgen der Zeit; am Ende ist er zurück auf dem Weg", () =>
        {
            var f = new Ausweichgang.Fenster(100, 110, 105);
            Pruefe.Gleich(Ausweichgang.Phase.Vorher, Ausweichgang.Bei(f, 99.9, 0.5, out _));
            Pruefe.Gleich(Ausweichgang.Phase.Hinaus, Ausweichgang.Bei(f, 100, 0.5, out var a0));
            Pruefe.Wahr(a0 < 0.01, "Anfang");
            Pruefe.Gleich(Ausweichgang.Phase.Halt, Ausweichgang.Bei(f, 105, 0.5, out var ah));
            Pruefe.Wahr(ah is > 0.3 and < 0.7, "Mitte des Halts");
            Pruefe.Gleich(Ausweichgang.Phase.Zurueck, Ausweichgang.Bei(f, 109.9, 0.5, out var az));
            Pruefe.Wahr(az > 0.95, "fast zurück");
            Pruefe.Gleich(Ausweichgang.Phase.Nachher, Ausweichgang.Bei(f, 110, 0.5, out var an));
            Pruefe.Gleich(1.0, an, "Anteil 1 nach dem Ausweichen");
            // der Halt dauert 30 Prozent der Zeit, der Rest teilt sich nach den Weglängen (langer Hinweg → längere Hinzeit)
            var halt = 0;
            var hinKurz = 0;
            var hinLang = 0;
            for (var t = 100.0; t < 110; t += 0.01)
            {
                if (Ausweichgang.Bei(f, t, 0.5, out _) == Ausweichgang.Phase.Halt) halt++;
                if (Ausweichgang.Bei(f, t, 0.2, out _) == Ausweichgang.Phase.Hinaus) hinKurz++;
                if (Ausweichgang.Bei(f, t, 0.8, out _) == Ausweichgang.Phase.Hinaus) hinLang++;
            }
            Pruefe.Wahr(Math.Abs(halt - 300) <= 2, $"Halt 30 Prozent: {halt}");
            Pruefe.Wahr(hinLang > hinKurz * 2, "längerer Hinweg braucht mehr Zeit");
        }),

        ("Ausweichgang: Die Anteile laufen stetig von 0 bis 1 (kein Sprung zwischen Hinaus, Halt und Zurück)", () =>
        {
            var f = new Ausweichgang.Fenster(0, 12, 6);
            var letztePhase = Ausweichgang.Phase.Vorher;
            var letzter = 0.0;
            for (var t = 0.0; t < 12; t += 0.05)
            {
                var p = Ausweichgang.Bei(f, t, 0.4, out var a);
                Pruefe.Wahr(a is >= 0 and <= 1, $"Anteil im Bereich bei {t}: {a}");
                if (p == letztePhase) Pruefe.Wahr(a >= letzter - 1e-9, $"wächst bei {t}");
                else Pruefe.Wahr(a < 0.1, $"neue Phase beginnt bei 0 ({p} bei {t}: {a})");
                (letztePhase, letzter) = (p, a);
            }
        }),
    };
}
