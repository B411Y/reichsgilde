using Reichsgilde.Core.Zeit;

/// <summary>Tests für die Sonnenzeiten je Jahreszeit (Vorbild Köln).</summary>
static class SonnenzeitenTests
{
    static void Gleich(int soll, int ist, string was)
    {
        if (soll != ist) throw new Exception($"{was}: erwartet {soll}, bekommen {ist}");
    }

    static void Wahr(bool bedingung, string was)
    {
        if (!bedingung) throw new Exception(was);
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Sonnenzeiten: die Tabelle des Nutzers (Köln) je Jahreszeit", () =>
        {
            var fruehling = Sonnenzeiten.Fuer(Jahreszeit.Fruehling);
            Gleich(5 * 60 + 15, fruehling.Daemmerung, "Frühling Dämmerung");
            Gleich(5 * 60 + 55, fruehling.Aufgang, "Frühling Aufgang");
            Gleich(18 * 60 + 5, fruehling.Untergang, "Frühling Untergang");
            Gleich(18 * 60 + 45, fruehling.Dunkel, "Frühling dunkel");
            var sommer = Sonnenzeiten.Fuer(Jahreszeit.Sommer);
            Gleich(3 * 60, sommer.Daemmerung, "Sommer Dämmerung");
            Gleich(3 * 60 + 50, sommer.Aufgang, "Sommer Aufgang");
            Gleich(20 * 60 + 10, sommer.Untergang, "Sommer Untergang");
            Gleich(21 * 60, sommer.Dunkel, "Sommer dunkel");
            var herbst = Sonnenzeiten.Fuer(Jahreszeit.Herbst);
            Gleich(5 * 60 + 10, herbst.Daemmerung, "Herbst Dämmerung");
            Gleich(5 * 60 + 50, herbst.Aufgang, "Herbst Aufgang");
            Gleich(17 * 60 + 50, herbst.Untergang, "Herbst Untergang");
            Gleich(18 * 60 + 30, herbst.Dunkel, "Herbst dunkel");
            var winter = Sonnenzeiten.Fuer(Jahreszeit.Winter);
            Gleich(7 * 60 + 25, winter.Daemmerung, "Winter Dämmerung");
            Gleich(8 * 60 + 10, winter.Aufgang, "Winter Aufgang");
            Gleich(15 * 60 + 50, winter.Untergang, "Winter Untergang");
            Gleich(16 * 60 + 35, winter.Dunkel, "Winter dunkel");
        });

        yield return ("Sonnenzeiten: Reihenfolge im Tag stimmt, Tageslänge wächst von Winter über Frühling/Herbst zum Sommer", () =>
        {
            foreach (var jz in Enum.GetValues<Jahreszeit>())
            {
                var l = Sonnenzeiten.Fuer(jz);
                Wahr(l.Daemmerung < l.Aufgang && l.Aufgang < l.Untergang && l.Untergang < l.Dunkel, $"{jz}: Zeiten nicht aufsteigend");
            }
            int Tag(Jahreszeit jz) { var l = Sonnenzeiten.Fuer(jz); return l.Untergang - l.Aufgang; }
            Wahr(Tag(Jahreszeit.Winter) < Tag(Jahreszeit.Herbst) && Tag(Jahreszeit.Herbst) < Tag(Jahreszeit.Fruehling)
                 && Tag(Jahreszeit.Fruehling) < Tag(Jahreszeit.Sommer), "Tageslänge: Winter < Herbst < Frühling < Sommer");
        });

        yield return ("Sonnenzeiten: Spielbeginn um 4 Uhr ist nachts, außer im Sommer", () =>
        {
            const int beginn = Spielzeit.TagesbeginnMinute;
            Wahr(Sonnenzeiten.IstNacht(Jahreszeit.Fruehling, beginn), "Frühling um 4 Uhr Nacht");
            Wahr(Sonnenzeiten.IstNacht(Jahreszeit.Herbst, beginn), "Herbst um 4 Uhr Nacht");
            Wahr(Sonnenzeiten.IstNacht(Jahreszeit.Winter, beginn), "Winter um 4 Uhr Nacht");
            Wahr(!Sonnenzeiten.IstNacht(Jahreszeit.Sommer, beginn) && Sonnenzeiten.IstTag(Jahreszeit.Sommer, beginn), "Sommer um 4 Uhr Tag");
            Wahr(Sonnenzeiten.IstNacht(Jahreszeit.Sommer, Spielzeit.TagesendeMinute), "Spielende ist immer Nacht");
            Wahr(Sonnenzeiten.IstTag(Jahreszeit.Winter, 12 * 60) && !Sonnenzeiten.IstTag(Jahreszeit.Winter, 17 * 60), "Winter: mittags Tag, um 17 Uhr nicht");
        });
    }
}
