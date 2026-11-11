using Reichsgilde;

/// <summary>Zuordnung Übergang zu Türklang (<c>game/scripts/KlangTuerModell.cs</c>): Tür auf nur hinein, Tür zu nur hinaus.</summary>
static class KlangTuerTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Tür: Betreten spielt Tür auf", () => Pruefe.Gleich(TuerKlang.Auf, KlangTuerModell.Fuer(UebergangRichtung.Betreten)));
        yield return ("Tür: Verlassen spielt Tür zu", () => Pruefe.Gleich(TuerKlang.Zu, KlangTuerModell.Fuer(UebergangRichtung.Verlassen)));
        yield return ("Tür: Gegenprobe, Tür auf nie beim Verlassen, Tür zu nie beim Betreten", () =>
        {
            Pruefe.Wahr(KlangTuerModell.Fuer(UebergangRichtung.Verlassen) != TuerKlang.Auf, "Verlassen nicht Tür auf");
            Pruefe.Wahr(KlangTuerModell.Fuer(UebergangRichtung.Betreten) != TuerKlang.Zu, "Betreten nicht Tür zu");
        });
    }
}
