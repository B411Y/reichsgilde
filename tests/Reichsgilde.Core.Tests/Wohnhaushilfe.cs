using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Hilfe für Tests, die einen Aufstieg im Stand nur als Vorbereitung brauchen (Rat, Gericht, Liegenschaften …):
/// Das Haus bekommt kostenlos das Wohnhaus, das der nächste Stand verlangt. Die Regeln des Wohnhauses selbst prüft WohnhausTests.
/// </summary>
static class Wohnhaushilfe
{
    public static Konto MitWohnhaus(Stadtwirtschaft w, Konto k)
    {
        k.WohnhausStufe = Math.Max(k.WohnhausStufe, w.Wohnen.NoetigeStufe(k.Stand + 1));
        return k;
    }
}
