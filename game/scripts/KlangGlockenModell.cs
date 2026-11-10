namespace Reichsgilde;

/// <summary>
/// Die Rechnung der Kirchenglocken nach Entfernung (kennt kein Godot, geprüft in <c>KlangGlockenTests.cs</c> und im Selbsttest <c>--glockentest=1</c>).
/// <para><b>Warum.</b> Vorher läutete eine einzige Glocke vom Dom und war im Umland nur 12 dB leiser als in der Stadt: Man hörte sie auf der Wiese
/// fast so laut wie auf dem Markt. Jetzt hat jede Kirche ihren Ort; der Pegel am Hörer folgt dem Abstand zur jeweiligen Kirche.</para>
/// <para><b>Kurve.</b> Hörer = Blickpunkt der Kamera am Boden (wie beim Regen und allen Umgebungsklängen). Je Kirche: Pegel = Quellpegel
/// − <see cref="AbfallJeVerdopplung"/> · log2(Abstand / Nahbereich), innerhalb des Nahbereichs voll. 9 dB je Verdopplung statt der 6 dB des
/// Freifelds, weil Häuser, Boden und Luft Glockenklang zusätzlich schlucken und die Karte (Maßstab etwa 1 : 10) kurz ist: Bei 6 dB wäre das Umland
/// kaum leiser als die Stadt. Der Dom hat mehr Quellpegel (+3 dB) und einen doppelt so weiten Nahbereich (60 statt 30 m): Er trägt weiter.
/// Mehrere Kirchen addieren sich <b>energetisch</b> (Summe der Leistungen, nicht der Pegel), die Summe ist bei 0 dB (= Pegel einer Glocke
/// aus nächster Nähe) gedeckelt und nie leiser als der <see cref="Mindestpegel"/> (−36 dB): Glocken tragen weit, man hört sie noch im Umland,
/// leise, aber nicht weg.</para>
/// <para><b>Tiefpass.</b> Die Höhen gehen zuerst: je Kirche von 16 kHz im Nahbereich auf 2,5 kHz beim Sechsfachen des Nahbereichs; bei mehreren
/// Kirchen der nach Leistung gewichtete Mittelwert im Logarithmus (die lauteste bestimmt den Klang).</para>
/// </summary>
public static class KlangGlockenModell
{
    /// <summary>Art der Kirche: Quellpegel (dB gegen eine Pfarrkirche), Nahbereich (Meter, innerhalb voll).</summary>
    public readonly record struct Kirchenart(string Name, float Quelle, float Nah);

    public static readonly Kirchenart Pfarrkirche = new("Kirche", 0f, 30f);
    public static readonly Kirchenart Dom = new("Dom", 3f, 60f);

    public const float AbfallJeVerdopplung = 9f;
    public const float Obergrenze = 0f;
    public const float Mindestpegel = -36f;
    public const float TiefpassNah = 16000f, TiefpassFern = 2500f;
    /// <summary>Ab dem Wievielfachen des Nahbereichs der Tiefpass ganz unten ist.</summary>
    public const float TiefpassFaktor = 6f;

    /// <summary>Eine Kirche vom Hörer aus gesehen: Abstand in Metern, Seite im Bild (−1…+1), ob Dom.</summary>
    public readonly record struct Quelle(float Abstand, float Seite, bool IstDom);

    /// <summary>Das Ergebnis für den einen Spieler: Pegel gegen die Nähe (≤ 0 dB), Seite (−1…+1) und Tiefpass.</summary>
    public readonly record struct Ergebnis(float Db, float Seite, float Tiefpass);

    static Kirchenart Art(bool dom) => dom ? Dom : Pfarrkirche;

    /// <summary>Pegel einer einzelnen Kirche am Hörer in dB gegen den Pegel aus nächster Nähe einer Pfarrkirche (Dom bis +3).</summary>
    public static float Pegel(float abstand, bool dom)
    {
        var a = Art(dom);
        var d = MathF.Max(abstand, a.Nah);
        return a.Quelle - AbfallJeVerdopplung * MathF.Log2(d / a.Nah);
    }

    /// <summary>Tiefpass einer einzelnen Kirche in Hz (glatt von 16 kHz im Nahbereich bis 2,5 kHz beim Sechsfachen).</summary>
    public static float Tiefpass(float abstand, bool dom)
    {
        var a = Art(dom);
        return TiefpassNah - (TiefpassNah - TiefpassFern) * KlangUmgebungModell.Glatt(a.Nah, a.Nah * TiefpassFaktor, abstand);
    }

    static float Leistung(float db) => MathF.Pow(10f, db / 10f);
    static float Zuruck(float leistung) => 10f * MathF.Log10(leistung);

    /// <summary>
    /// Summe aller Kirchen am Hörer. Ohne Kirche (Karte ohne Dom): eine Pfarrkirche im Abstand <paramref name="ersatzAbstand"/> (40 m, wie nahe am Markt) in der Mitte.
    /// </summary>
    public static Ergebnis Mischen(IReadOnlyList<Quelle> kirchen, float ersatzAbstand = 40f)
    {
        if (kirchen.Count == 0) return Mischen(new[] { new Quelle(ersatzAbstand, 0f, false) });
        float summe = 0f, seiteSumme = 0f, logHz = 0f;
        foreach (var k in kirchen)
        {
            var p = Leistung(Pegel(k.Abstand, k.IstDom));
            summe += p;
            seiteSumme += p * Math.Clamp(k.Seite, -1f, 1f);
            logHz += p * MathF.Log(Tiefpass(k.Abstand, k.IstDom));
        }
        var db = Math.Clamp(Zuruck(summe), Mindestpegel, Obergrenze);
        return new Ergebnis(db, seiteSumme / summe, MathF.Exp(logHz / summe));
    }

    /// <summary>Pegel am Spieler (Dateien liegen bei −20 LUFS, nah −3 dB), mit dem Ausgleich der Seite wie bisher.</summary>
    public static float SpielerDb(Ergebnis e, float nahDb)
    {
        var seite = e.Seite * 0.8f;
        return nahDb + e.Db - 20f * MathF.Log10(1f + MathF.Abs(seite));
    }

    /// <summary>Selbsttest mit dB-Werten: Hörer in Stadtmitte, am Stadtrand und im Umland (Karte Köln, 1 : 10), einmal mit sechs Kirchen und Dom, einmal mit einer Kirche, einmal nur der Dom.</summary>
    public static bool Selbsttest(Action<string> log)
    {
        var fehler = new List<string>();
        void Pruefe(bool gut, string was) { if (!gut) fehler.Add(was); }
        // Kirchen Kölns (game/karten/koeln.json): Dom, Groß St. Martin, St. Gereon, St. Severin, St. Aposteln, St. Kunibert
        var kirchen = new (float X, float Z, bool Dom)[] { (6, -112, true), (22, -44, false), (-55, -86, false), (7.5f, 96, false), (-54, -34, false), (-16.6f, -76, false) };
        var orte = new (string Name, float X, float Z)[] { ("Stadtmitte (0|0)", 0, 0), ("Stadtrand (0|200)", 0, 200), ("Umland (0|400)", 0, 400), ("fernes Umland (0|800)", 0, 800) };
        List<Quelle> Von(float x, float z, Func<(float X, float Z, bool Dom), bool> wahl) => kirchen.Where(wahl)
            .Select(k => new Quelle(MathF.Sqrt((k.X - x) * (k.X - x) + (k.Z - z) * (k.Z - z)), (k.X - x) / 200f, k.Dom)).ToList();
        Ergebnis? vorher = null;
        foreach (var (name, x, z) in orte)
        {
            var alle = Mischen(Von(x, z, _ => true));
            var eine = Mischen(Von(x, z, k => k.X == 22f));
            var dom = Mischen(Von(x, z, k => k.Dom));
            log($"GLOCKENTEST {name,-22} sechs Kirchen {alle.Db,6:0.0} dB {alle.Tiefpass,6:0} Hz | nur Martin {eine.Db,6:0.0} dB {eine.Tiefpass,6:0} Hz | nur Dom {dom.Db,6:0.0} dB {dom.Tiefpass,6:0} Hz");
            if (vorher is { } v) Pruefe(alle.Db < v.Db && alle.Tiefpass <= v.Tiefpass + 1f, $"{name}: nicht leiser und dumpfer als näher");
            vorher = alle;
            Pruefe(alle.Db is >= Mindestpegel and <= Obergrenze, $"{name}: Pegel {alle.Db} außerhalb");
            if (z >= 400) Pruefe(alle.Db is <= -18f and >= Mindestpegel, $"{name}: Umland {alle.Db:0.0} dB nicht leise oder verschwunden");
            if (z >= 400) Pruefe(dom.Db > eine.Db, $"{name}: Dom trägt nicht weiter als eine Kirche");
        }
        Pruefe(Mischen(Von(0, 0, _ => true)).Db > -3f, "Stadtmitte nicht laut");
        log("GLOCKENTEST " + (fehler.Count == 0 ? "ok" : "FEHLER: " + string.Join("; ", fehler)));
        return fehler.Count == 0;
    }
}
