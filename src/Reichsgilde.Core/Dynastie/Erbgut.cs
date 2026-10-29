namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Das Erbgut einer Person – die Grundlage ihres Aussehens im 3D-Kopf (Charakter-Editor, Phase 1: nur der Kern).
///
/// Je Gen der <see cref="Erbgutvorlage"/> trägt die Person zwei Allele, jedes ein Byte (0–255): eines ist sichtbar, das andere verdeckt.
/// Die Darstellung liest nur den sichtbaren Wert (<see cref="SichtbarerWert(string)"/>). Das verdeckte Allel kann sie an ein Kind
/// weitergeben – so taucht die Nase des Großvaters beim Enkel wieder auf, obwohl der Vater sie nicht hatte.
///
/// Wie es entsteht (alles nur mit ganzen Zahlen, Würfel aus <see cref="Zufallsquelle"/>):
///   • Ohne Eltern (<see cref="Wuerfeln"/>): beide Allele aus der Glockenkurve des Volkes, abgeschnitten bei ±2 Streuungen,
///     dann die Korrelationsregeln (breiter Kiefer → großes Kinn …).
///   • Mit Eltern (<see cref="Vererben"/>): je Gen ein Allel vom Vater und eins von der Mutter (welches der beiden, entscheidet ein Wurf),
///     dazu eine kleine Mutation. Ein Allel, das nicht ins Volk des Kindes passt (Ehe zwischen Völkern), kommt auf die nächste Grenze.
///   • Welches Allel sichtbar ist, entscheidet die <see cref="GenVorlage.Dominanz"/> des Gens.
///
/// Jedes Gen würfelt mit einem eigenen Würfel aus dem Startwert der Person und der Kennung des Gens. Deshalb ergeben gleiche Eltern und
/// gleicher Startwert immer dieselben Bytes, und ein neues Gen in der Vorlage ändert keines der anderen.
///
/// Das Erbgut wirkt auf nichts in der Simulation und lässt sich nicht ändern (nur als Ganzes ersetzen).
/// </summary>
public sealed class Erbgut
{
    // Je Gen zwei Bytes in der Reihenfolge der Vorlage: [2·i] das sichtbare Allel, [2·i + 1] das verdeckte.
    private readonly byte[] _allele;

    /// <summary>Die Vorlage, nach der die Allele geordnet sind.</summary>
    public Erbgutvorlage Vorlage { get; }

    private Erbgut(Erbgutvorlage vorlage, byte[] allele)
    {
        if (allele.Length != 2 * vorlage.Gene.Count)
            throw new ArgumentException($"Das Erbgut braucht {2 * vorlage.Gene.Count} Allele, nicht {allele.Length}.", nameof(allele));
        Vorlage = vorlage;
        _allele = allele;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen (für die Darstellung)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Gibt es dieses Gen in der Vorlage?</summary>
    public bool Hat(string gen) => Vorlage.Index(gen) >= 0;

    /// <summary>Der sichtbare Wert des Gens (0–255) – das, was man sieht.</summary>
    /// <exception cref="ArgumentException">Wenn es das Gen nicht gibt (Tippfehler sollen auffallen; vorher mit <see cref="Hat"/> fragen).</exception>
    public int SichtbarerWert(string gen) => _allele[2 * IndexVon(gen)];

    /// <summary>Der sichtbare Wert des Gens an dieser Stelle der Vorlage.</summary>
    public int SichtbarerWert(GenVorlage gen) => _allele[2 * gen.Index];

    /// <summary>Das verdeckte Allel (0–255) – man sieht es nicht, aber es wird vererbt.</summary>
    public int VerdeckterWert(string gen) => _allele[2 * IndexVon(gen) + 1];

    /// <summary>Der sichtbare Wert mit der Alterskurve des Gens (<see cref="GenVorlage.Altersverschiebung"/>), wieder in 0–255.</summary>
    public int WertImAlter(string gen, int menschenjahre)
    {
        var g = Vorlage.Gene[IndexVon(gen)];
        return Math.Clamp(SichtbarerWert(g) + g.Altersverschiebung(menschenjahre), 0, 255);
    }

    /// <summary>Alle Allele als Text: je Gen der Vorlage vier Hexadezimalziffern, erst das sichtbare, dann das verdeckte Allel – so im Spielstand.</summary>
    public string AlsText() => Convert.ToHexString(_allele);

    public override string ToString() => AlsText();

    private int IndexVon(string gen)
    {
        var i = Vorlage.Index(gen);
        return i >= 0 ? i : throw new ArgumentException($"Ein Gen „{gen}“ gibt es in der Gen-Vorlage nicht.", nameof(gen));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Entstehen
    // ═════════════════════════════════════════════════════════════════════════

    // Zweck im Würfel eines Gens: So würfelt dasselbe Gen beim Würfeln und beim Vererben verschieden.
    private const int ZweckWuerfeln = 1, ZweckVererben = 2;

    /// <summary>
    /// Ein Erbgut für jemanden ohne Eltern im Spiel (Oberhaupt bei Spielbeginn, Heiratskandidat; später auch Arbeiter oder Ratsherren,
    /// die die Darstellung mit einem festen Startwert würfeln kann).
    /// </summary>
    /// <param name="startwert">Der Startwert der Person – gleicher Startwert, gleiches Erbgut.</param>
    public static Erbgut Wuerfeln(Erbgutvorlage vorlage, int startwert, Volk volk)
    {
        var allele = new byte[2 * vorlage.Gene.Count];
        foreach (var gen in vorlage.Gene)
            GenWuerfeln(gen, startwert, volk, allele);
        KorrelationenAnwenden(vorlage, volk, allele, _ => true);
        return new Erbgut(vorlage, allele);
    }

    /// <summary>
    /// Das Erbgut eines Kindes. Fehlt ein Elternteil (Kind aus einer früheren Ehe), würfelt das Kind dessen Allel aus der Glockenkurve
    /// seines Volkes; fehlen beide, ist es <see cref="Wuerfeln"/>.
    /// </summary>
    /// <param name="startwert">Der Startwert des Kindes – gleiche Eltern und gleicher Startwert ergeben dasselbe Kind.</param>
    /// <param name="volk">Das Volk des Kindes (das des Hauses); seine Grenzen gelten für jedes Allel.</param>
    public static Erbgut Vererben(Erbgutvorlage vorlage, int startwert, Volk volk, Erbgut? vater, Erbgut? mutter)
    {
        if (vater is null && mutter is null) return Wuerfeln(vorlage, startwert, volk);
        var allele = new byte[2 * vorlage.Gene.Count];
        foreach (var gen in vorlage.Gene)
            GenVererben(gen, startwert, volk, vater, mutter, allele);
        return new Erbgut(vorlage, allele);
    }

    /// <summary>
    /// Das Erbgut aus dem Spielstand. Gene, die die Vorlage nicht mehr kennt, fallen weg; Gene, die im Spielstand fehlen (neu in der Vorlage
    /// oder ein alter Spielstand ganz ohne Erbgut), entstehen so, wie sie bei der Geburt entstanden wären: vererbt, wenn ein Elternteil
    /// bekannt ist, sonst gewürfelt – immer aus dem Startwert der Person, also bei jedem Laden gleich. Jedes Allel kommt in die Grenzen
    /// des Volkes (falls die Vorlage sie inzwischen enger gezogen hat).
    /// </summary>
    /// <param name="gene">Die Kennungen in der Reihenfolge von <paramref name="text"/> (null = ein Spielstand von vor dem Erbgut).</param>
    /// <exception cref="InvalidDataException">Wenn Text und Genliste nicht zusammenpassen.</exception>
    internal static Erbgut AusSpielstand(Erbgutvorlage vorlage, int startwert, Volk volk, IReadOnlyList<string>? gene, string? text,
        Erbgut? vater, Erbgut? mutter)
    {
        var allele = new byte[2 * vorlage.Gene.Count];
        var bekannt = new bool[vorlage.Gene.Count];
        if (gene is not null && text is not null)
        {
            byte[] gelesen;
            try { gelesen = Convert.FromHexString(text); }
            catch (FormatException) { throw new InvalidDataException($"Das Erbgut „{text}“ ist kein Hexadezimaltext."); }
            if (gelesen.Length != 2 * gene.Count)
                throw new InvalidDataException($"Das Erbgut hat {gelesen.Length} Allele, die Genliste braucht {2 * gene.Count}.");
            for (var j = 0; j < gene.Count; j++)
            {
                if (vorlage.Gen(gene[j]) is not { } gen || bekannt[gen.Index]) continue; // unbekannt (oder doppelt): weg
                var g = gen.Grenzen(volk);
                allele[2 * gen.Index] = (byte)g.Begrenzen(gelesen[2 * j]);
                allele[2 * gen.Index + 1] = (byte)g.Begrenzen(gelesen[2 * j + 1]);
                bekannt[gen.Index] = true;
            }
        }
        var mitEltern = vater is not null || mutter is not null;
        foreach (var gen in vorlage.Gene.Where(g => !bekannt[g.Index]))
        {
            if (mitEltern) GenVererben(gen, startwert, volk, vater, mutter, allele);
            else GenWuerfeln(gen, startwert, volk, allele);
        }
        if (!mitEltern) KorrelationenAnwenden(vorlage, volk, allele, i => !bekannt[i]);
        return new Erbgut(vorlage, allele);
    }

    /// <summary>Würfelt beide Allele eines Gens aus der Verteilung des Volkes und legt fest, welches sichtbar ist.</summary>
    private static void GenWuerfeln(GenVorlage gen, int startwert, Volk volk, byte[] allele)
    {
        var z = Genwuerfel(startwert, gen, ZweckWuerfeln);
        var g = gen.Grenzen(volk);
        var a = Ziehen(z, gen, g);
        var b = Ziehen(z, gen, g);
        Ablegen(z, gen, a, b, allele);
    }

    /// <summary>Ein Allel von jedem Elternteil (welches seiner zwei, würfelt das Kind), mit Mutation und in den Grenzen des Kindes.</summary>
    private static void GenVererben(GenVorlage gen, int startwert, Volk volk, Erbgut? vater, Erbgut? mutter, byte[] allele)
    {
        var z = Genwuerfel(startwert, gen, ZweckVererben);
        var g = gen.Grenzen(volk);
        int VonElternteil(Erbgut? e)
        {
            var i = e?.Vorlage.Index(gen.Kennung) ?? -1;
            var wert = i >= 0 ? e!._allele[2 * i + z.Next(2)] : Ziehen(z, gen, g); // unbekannter Elternteil: einer des Volkes
            if (gen.Mutation > 0) wert += z.Next(2 * gen.Mutation + 1) - gen.Mutation;
            return g.Begrenzen(wert);
        }
        var a = VonElternteil(vater);
        var b = VonElternteil(mutter);
        Ablegen(z, gen, a, b, allele);
    }

    /// <summary>
    /// Legt die zwei Allele ab, das sichtbare zuerst: Sind sie verschieden, wird mit <see cref="GenVorlage.Dominanz"/> Prozent das höhere
    /// sichtbar, sonst das niedrigere. Würfelt immer (auch bei gleichen Allelen), damit die Zahl der Würfe fest bleibt.
    /// </summary>
    private static void Ablegen(Zufallsquelle z, GenVorlage gen, int a, int b, byte[] allele)
    {
        var hoeherSichtbar = z.Next(100) < gen.Dominanz;
        var (hoch, tief) = a >= b ? (a, b) : (b, a);
        var (sichtbar, verdeckt) = hoeherSichtbar ? (hoch, tief) : (tief, hoch);
        allele[2 * gen.Index] = (byte)sichtbar;
        allele[2 * gen.Index + 1] = (byte)verdeckt;
    }

    /// <summary>
    /// Ein Wert aus der Verteilung des Gens. Glockenkurve ohne Kommazahlen: Zwölf Würfel von 0 bis 1000 zusammengezählt liegen fast genau
    /// normalverteilt um 6000, mit einer Streuung von etwa 1000 (zentraler Grenzwertsatz). Die Abweichung von 6000 ist also „Tausendstel
    /// einer Streuung“. Was weiter als ±2 Streuungen weg liegt, wird neu gewürfelt (abgeschnittene Normalverteilung) – nach 20 Fehlversuchen,
    /// die praktisch nie vorkommen, gilt das Mittel.
    /// </summary>
    private static int Ziehen(Zufallsquelle z, GenVorlage gen, GenGrenzen g)
    {
        if (gen.Verteilung == Genverteilung.Gleich) return g.Min + z.Next(g.Max - g.Min + 1);
        if (g.Streuung == 0) return g.Mittel;
        for (var versuch = 0; versuch < 20; versuch++)
        {
            var summe = 0;
            for (var i = 0; i < 12; i++) summe += z.Next(1001);
            var abweichung = summe - 6000;
            if (abweichung is < -2000 or > 2000) continue;
            return g.Begrenzen(g.Mittel + GerundetGeteilt(abweichung * g.Streuung, 1000));
        }
        return g.Mittel;
    }

    /// <summary>
    /// Die Korrelationsregeln der Vorlage, nacheinander: Der sichtbare Wert von „von“ verschiebt beide Allele von „zu“ gleich weit – so bleibt
    /// das sichtbare Allel sichtbar. Nur Gene, für die <paramref name="ziel"/> ja sagt (beim Laden nur die neu gewürfelten).
    /// </summary>
    private static void KorrelationenAnwenden(Erbgutvorlage vorlage, Volk volk, byte[] allele, Func<int, bool> ziel)
    {
        foreach (var k in vorlage.Korrelationen)
        {
            var von = vorlage.Gen(k.Von)!;
            var zu = vorlage.Gen(k.Zu)!;
            if (!ziel(zu.Index)) continue;
            var schub = GerundetGeteilt((allele[2 * von.Index] - von.Grenzen(volk).Mittel) * k.Staerke, 100);
            var g = zu.Grenzen(volk);
            allele[2 * zu.Index] = (byte)g.Begrenzen(allele[2 * zu.Index] + schub);
            allele[2 * zu.Index + 1] = (byte)g.Begrenzen(allele[2 * zu.Index + 1] + schub);
        }
    }

    /// <summary>Der eigene Würfel eines Gens: fest aus Startwert der Person, Kennung des Gens und Zweck.</summary>
    private static Zufallsquelle Genwuerfel(int startwert, GenVorlage gen, int zweck) => new(Zugwuerfel.Mischen(startwert, gen.Schluessel, zweck));

    /// <summary>Ganzzahlig geteilt und kaufmännisch gerundet (0,5 von der Null weg) – auf jedem Rechner gleich.</summary>
    internal static int GerundetGeteilt(int zaehler, int nenner) =>
        zaehler >= 0 ? (zaehler + nenner / 2) / nenner : -((-zaehler + nenner / 2) / nenner);
}
