using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Hilfe für Tests, die eine freie Werkstatt in der Stadt zum Kauf brauchen. Seit Datenversion 40 stehen unter „liegenschaften“
/// nur noch Umlandbetriebe: Frei wird eine Werkstatt erst, wenn ein Haus sie verkauft, bankrott geht oder seinen Neubau nicht mehr erlebt.
/// Hier steht sie wie ein Neubau ohne Bauherrn zum Verkauf – Stufe 1, zum Neubaupreis, ab Bürger.
/// </summary>
static class Werkstatthilfe
{
    public static LiegenschaftDaten FreieWerkstatt(Stadtwirtschaft w, string name, Betriebsart art)
    {
        w.NeubauFreigeben(name, art, "Bürger");
        return w.FreieLiegenschaften.Single(l => l.Name == name);
    }
}
