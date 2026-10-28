namespace Reichsgilde.Core.Ki;

/// <summary>
/// Wie ein Charakterzug des Oberhaupts ein KI-Haus handeln lässt (Teil "ki" eines Zugs in "charakterzuege.zuege", PR #227).
/// Der Charakter des Hauses (vorsichtig, ausgewogen, wagemutig im <see cref="KiProfil"/>) bleibt; die Züge verschieben ihn.
/// Mehrere Züge wirken zusammen: Faktoren werden multipliziert, Zuschläge addiert (<see cref="ZugNeigung"/>).
/// </summary>
public sealed class ZugKiDaten
{
    /// <summary>Faktor auf das Budget für Geschenke an Ratsherren (Großzügig 1,3, Gierig 0,7).</summary>
    public double Geschenke { get; init; } = 1;

    /// <summary>Zuschlag auf den Zins der eigenen Geldleihe (Gierig +0,04 = 4 Prozentpunkte Wucher).</summary>
    public double Zins { get; init; }

    /// <summary>Mehr (oder weniger) bezahlte Gaunerstücke im Jahr (Verschlagen +1, Ehrlich −1 – weniger aber nie unter eins, wenn der Charakter sie nutzt).</summary>
    public int Gaunerstuecke { get; init; }

    /// <summary>Beschenkt vor dem Gerichtstag keine Richter (Ehrlich).</summary>
    public bool Unbestechlich { get; init; }

    /// <summary>Beweise mehr (oder weniger), bevor die KI anklagt – gegen die Beweise für einen sicheren Schuldspruch (Geduldig +1, Jähzornig −1).</summary>
    public int KlageBeweise { get; init; }

    /// <summary>Faktor auf das Polster, das nach einem Ablass bleiben muss (Fromm 0,5 – kauft eher, Zynisch 2).</summary>
    public double Ablass { get; init; } = 1;

    /// <summary>Wechselt nicht zum Stadtglauben, auch wenn der Glaubenszins kostet (Fromm).</summary>
    public bool GlaubensTreu { get; init; }

    /// <summary>Jahre mehr (oder weniger), die ein Kauf, Ausbau oder Brief zum Bezahlen brauchen darf (Feige −1).</summary>
    public double Amortisation { get; init; }

    /// <summary>Faktor auf die Furcht vor Raub – mehr oder weniger Eskorte (Feige 1,5, Mutig 0,85).</summary>
    public double Vorsicht { get; init; } = 1;

    /// <summary>Faktor auf die Schwelle, ab der die Werkstatt ein anderes Produkt nimmt (z. B. Geduldig 1,3, Jähzornig 0,7).</summary>
    public double Wechselschwelle { get; init; } = 1;

    /// <summary>Verlustjahre mehr, bevor ein Betrieb verkauft wird (derzeit bei keinem Zug gesetzt).</summary>
    public int Verlustjahre { get; init; }

    /// <summary>+1: übt Talente, sobald die Aktionspunkte reichen (Fleißig); −1: übt nie (Träge).</summary>
    public int Ueben { get; init; }

    /// <summary>+1: holt in jeder Schänke einen Barden (Gesellig, Großzügig); −1: nie (Scheu).</summary>
    public int Geselligkeit { get; init; }

    /// <summary>+1: wertet in der Brautschau je Standesstufe der Braut +25 % (Hochmütig); −1: kauft keine Wohnhaus-Einrichtung (Bescheiden).</summary>
    public int Prunk { get; init; }

    internal void Pruefen(List<string> f, string p)
    {
        if (Geschenke < 0 || Ablass < 0 || Vorsicht <= 0 || Wechselschwelle < 0)
            f.Add($"{p}: geschenke, ablass und wechselschwelle ≥ 0, vorsicht > 0.");
        if (KlageBeweise is < -3 or > 3) f.Add($"{p}.klageBeweise: zwischen −3 und 3.");
        if (Math.Abs(Zins) > 0.2) f.Add($"{p}.zins: höchstens ±0,2.");
        if (Ueben is < -1 or > 1 || Geselligkeit is < -1 or > 1 || Prunk is < -1 or > 1)
            f.Add($"{p}: ueben, geselligkeit und prunk sind −1, 0 oder 1.");
    }
}

/// <summary>Alle Züge eines Oberhaupts zusammen – so handelt das KI-Haus.</summary>
public sealed record ZugNeigung(
    double Geschenke, double Zins, int Gaunerstuecke, int KlageBeweise, double Ablass, bool GlaubensTreu,
    double Amortisation, double Vorsicht, double Wechselschwelle, int Verlustjahre, int Ueben, int Geselligkeit, int Prunk, bool Unbestechlich = false)
{
    public static ZugNeigung Neutral { get; } = new(1, 0, 0, 0, 1, false, 0, 1, 1, 0, 0, 0, 0);

    public static ZugNeigung Aus(IEnumerable<Dynastie.Charakterzug> zuege)
    {
        var n = Neutral;
        foreach (var z in zuege)
        {
            var k = z.Daten.Ki;
            n = new ZugNeigung(n.Geschenke * k.Geschenke, n.Zins + k.Zins, n.Gaunerstuecke + k.Gaunerstuecke, n.KlageBeweise + k.KlageBeweise,
                n.Ablass * k.Ablass, n.GlaubensTreu || k.GlaubensTreu, n.Amortisation + k.Amortisation, n.Vorsicht * k.Vorsicht,
                n.Wechselschwelle * k.Wechselschwelle, n.Verlustjahre + k.Verlustjahre, Math.Clamp(n.Ueben + k.Ueben, -1, 1),
                Math.Clamp(n.Geselligkeit + k.Geselligkeit, -1, 1), Math.Clamp(n.Prunk + k.Prunk, -1, 1), n.Unbestechlich || k.Unbestechlich);
        }
        return n;
    }
}
